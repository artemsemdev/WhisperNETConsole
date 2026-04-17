#!/usr/bin/env python3
"""VoxFlow local diarization sidecar (ADR-024 Phase 0).

Protocol (sidecar-diarization-v1):
- Reads a single JSON request object from stdin.
- Writes a single JSON response object to stdout.
- Progress lines may be written as NDJSON to stderr (not yet emitted).
- Exit code is 0 on every recoverable outcome (including error envelopes).
- Exit code is non-zero only for unrecoverable framing failures
  (malformed JSON on stdin) so the .NET client can distinguish a
  well-formed error envelope from a crashed process.

Request:  {"version": 1, "wavPath": "/abs/path.wav"}
Response: {"version": 1, "status": "ok",    "speakers": [...], "segments": [...]}
Error:    {"version": 1, "status": "error", "error": "<message>", "speakers": [], "segments": []}
"""
from __future__ import annotations

import io
import json
import os
import sys
from typing import Any


PROTOCOL_VERSION = 1
PYANNOTE_MODEL = "pyannote/speaker-diarization-3.1"


# ---------------------------------------------------------------------------
# Stdout isolation.
#
# sidecar-diarization-v1 reserves stdout for exactly one JSON envelope. But
# pyannote.audio pulls in torch, speechbrain, lightning, and huggingface_hub,
# and any of them (or their C backends) may emit loading banners, deprecation
# warnings, or "Could not load …" messages onto fd 1. A single stray byte
# corrupts the protocol and the .NET client sees "malformed JSON".
#
# Guarantee: before we import anything heavy, dup fd 1 off to a stashed fd
# and point fd 1 at fd 2 (stderr). Python-level sys.stdout is also rebound
# to stderr. All library chatter — Python or C — ends up on stderr, which
# PyannoteSidecarClient drains and only scans for NDJSON progress lines. The
# stashed fd is the *only* channel for _write_response, so the JSON envelope
# is immune to third-party stdout pollution.
# ---------------------------------------------------------------------------
_real_stdout_fd = os.dup(1)
os.dup2(2, 1)
sys.stdout = sys.stderr
_real_stdout = os.fdopen(_real_stdout_fd, "w", encoding="utf-8")


def _write_response(payload: dict[str, Any]) -> None:
    _real_stdout.write(json.dumps(payload))
    _real_stdout.flush()


class _NdjsonProgressHook:
    """Adapts pyannote.audio's ProgressHook protocol onto the sidecar's
    NDJSON stderr stream. pyannote's Pipeline.__call__ otherwise runs as a
    single blocking call that emits nothing parseable between "loaded" and
    "done" -- the CLI progress bar on the .NET side then freezes for the
    entire pyannote inference (often minutes). By passing an instance of
    this class as `hook=` to `pipeline(wav)`, we get one event per step
    boundary plus per-chunk fractional updates, which PyannoteSidecarClient
    already parses and SpeakerEnrichmentService maps into the Diarizing
    progress band.

    pyannote.audio 3.x calls hooks as
    hook(step_name, step_artifact, file=None, total=None, completed=None);
    step_name changes between steps ("segmentation", "embeddings",
    "discrete_diarization"); total/completed are only populated during
    chunked inference. Emission is best-effort: a broken progress stream
    must never abort the diarization.
    """

    def __init__(self, out_stream: Any) -> None:
        self._out = out_stream
        self._current_step: str | None = None

    def __enter__(self) -> "_NdjsonProgressHook":
        return self

    def __exit__(self, *args: Any) -> bool:
        return False

    def __call__(
        self,
        step_name: str,
        step_artifact: Any = None,
        file: Any = None,
        total: int | None = None,
        completed: int | None = None,
    ) -> None:
        if step_name != self._current_step:
            self._current_step = step_name
            self._emit({"stage": step_name})
        if total is not None and completed is not None and total > 0:
            fraction = float(completed) / float(total)
            if fraction < 0.0:
                fraction = 0.0
            elif fraction > 1.0:
                fraction = 1.0
            self._emit({"stage": step_name, "fraction": fraction})

    def _emit(self, payload: dict[str, Any]) -> None:
        try:
            self._out.write(json.dumps(payload) + "\n")
            self._out.flush()
        except Exception:
            pass


def _error_envelope(message: str) -> dict[str, Any]:
    return {
        "version": PROTOCOL_VERSION,
        "status": "error",
        "error": message,
        "speakers": [],
        "segments": [],
    }


def _parse_request() -> dict[str, Any]:
    raw = sys.stdin.read()
    return json.loads(raw)


def _normalize_speaker_labels(diarization: Any) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Remap pyannote's arbitrary speaker labels to A, B, C... in first-appearance order."""
    label_map: dict[str, str] = {}
    next_ord = ord("A")
    segments: list[dict[str, Any]] = []
    durations: dict[str, float] = {}

    for turn, _, raw_label in diarization.itertracks(yield_label=True):
        if raw_label not in label_map:
            label_map[raw_label] = chr(next_ord)
            next_ord += 1
        label = label_map[raw_label]
        segments.append({"speaker": label, "start": float(turn.start), "end": float(turn.end)})
        durations[label] = durations.get(label, 0.0) + float(turn.end - turn.start)

    speakers = [
        {"id": label, "totalDuration": round(duration, 6)}
        for label, duration in sorted(durations.items())
    ]
    return speakers, segments


def _diagnose_none_pipeline(hf_token: str | None) -> str:
    """Build a short diagnostic string describing *why* from_pretrained probably
    returned None. Reports what the child process actually sees (token
    presence/shape, HF cache location, whether the top-level model and its
    segmentation dependency have been downloaded) so a single error envelope
    tells the user exactly which of token-missing / token-invalid /
    license-not-accepted they are hitting.
    """
    parts: list[str] = []

    if not hf_token:
        parts.append(
            "Diagnostic: HUGGING_FACE_HUB_TOKEN and HF_TOKEN are both unset in "
            "the sidecar environment -- the token is not reaching the child "
            "process."
        )
    else:
        # Don't leak the token itself, just enough shape to confirm it arrived.
        prefix = hf_token[:4] if len(hf_token) >= 4 else "?"
        parts.append(
            f"Diagnostic: HF token present (prefix='{prefix}', length={len(hf_token)})."
        )

    cache_root = (
        os.environ.get("HF_HUB_CACHE")
        or os.path.join(os.environ.get("HF_HOME", os.path.expanduser("~/.cache/huggingface")), "hub")
    )
    parts.append(f"HF cache root: {cache_root}.")

    def _cache_dir_present(model_id: str) -> bool:
        folder = "models--" + model_id.replace("/", "--")
        return os.path.isdir(os.path.join(cache_root, folder))

    top_cached = _cache_dir_present(PYANNOTE_MODEL)
    seg_cached = _cache_dir_present("pyannote/segmentation-3.0")
    parts.append(
        f"Cached top-level model: {top_cached}. "
        f"Cached pyannote/segmentation-3.0 (required dependency): {seg_cached}."
    )

    if hf_token and top_cached and not seg_cached:
        parts.append(
            "Most likely cause: the segmentation-3.0 license has not been accepted "
            "on huggingface.co for this account, so its weights silently failed to "
            "download."
        )
    elif hf_token and not top_cached:
        # Probe the HF API directly to split 'bad token' from 'license not
        # accepted'. model_info returns 401 for invalid tokens and 403 for a
        # valid token that has not accepted the repo license.
        parts.append(_probe_hf_access(hf_token))

    return " ".join(parts)


def _probe_hf_access(hf_token: str) -> str:
    try:
        from huggingface_hub import HfApi  # type: ignore[import-not-found]
        from huggingface_hub.utils import (  # type: ignore[import-not-found]
            GatedRepoError,
            RepositoryNotFoundError,
        )
    except Exception as exc:  # pragma: no cover
        return f"(could not probe HF API: {exc})."

    api = HfApi()
    # First sanity check -- whoami is the cheapest way to verify the token.
    try:
        who = api.whoami(token=hf_token)
        username = who.get("name") or who.get("fullname") or "<unknown>"
    except Exception as exc:
        return (
            f"HF whoami() failed for this token: {exc}. "
            "Most likely cause: the token is invalid, revoked, or has no read "
            "permission. Generate a new read token at "
            "https://huggingface.co/settings/tokens."
        )

    # Token is valid; now test access to the top-level pipeline repo.
    try:
        api.model_info(PYANNOTE_MODEL, token=hf_token)
    except GatedRepoError:
        return (
            f"HF whoami() succeeded as '{username}', but access to {PYANNOTE_MODEL} "
            "is gated and this account has not accepted its license. Visit "
            f"https://huggingface.co/{PYANNOTE_MODEL} and click 'Agree and access "
            "repository'."
        )
    except RepositoryNotFoundError:
        return (
            f"HF whoami() succeeded as '{username}', but {PYANNOTE_MODEL} reports "
            "404 for this token. The repo may have been renamed, or the token may "
            "be scoped to a different org."
        )
    except Exception as exc:
        return (
            f"HF whoami() succeeded as '{username}', but model_info({PYANNOTE_MODEL}) "
            f"failed: {exc}."
        )

    # Top-level repo is accessible -- re-check segmentation separately.
    try:
        api.model_info("pyannote/segmentation-3.0", token=hf_token)
    except GatedRepoError:
        return (
            f"HF whoami() succeeded as '{username}' and {PYANNOTE_MODEL} is "
            "accessible, but pyannote/segmentation-3.0 is gated and this account "
            "has not accepted its license. Visit "
            "https://huggingface.co/pyannote/segmentation-3.0 and click "
            "'Agree and access repository'."
        )
    except Exception as exc:
        return (
            f"HF whoami() succeeded as '{username}' and {PYANNOTE_MODEL} is "
            f"accessible, but model_info(pyannote/segmentation-3.0) failed: {exc}."
        )

    return (
        f"HF whoami() succeeded as '{username}' and both {PYANNOTE_MODEL} and "
        "pyannote/segmentation-3.0 are accessible to this token. The None return "
        "is unexpected -- re-run with HF_HUB_VERBOSITY=info to see why "
        "from_pretrained bailed."
    )


def _run_diarization(wav_path: str) -> dict[str, Any]:
    try:
        from pyannote.audio import Pipeline  # type: ignore[import-not-found]
    except Exception as exc:  # pragma: no cover - exercised in integration tests
        return _error_envelope(f"failed to import pyannote.audio: {exc}")

    # Pipeline.from_pretrained silently returns None when the HF token is
    # missing OR when the user has not accepted the license for *any* of the
    # pipeline's transitive dependencies. speaker-diarization-3.1 depends on
    # pyannote/segmentation-3.0, which is separately gated and must be
    # accepted on huggingface.co independently from the top-level pipeline.
    # We intercept the None case so the .NET client surfaces a useful
    # diagnostic instead of the downstream "'NoneType' is not callable" crash.
    hf_token = (
        os.environ.get("HUGGING_FACE_HUB_TOKEN")
        or os.environ.get("HF_TOKEN")
    )

    # pyannote.audio 3.3.x wraps from_pretrained in a bare try/except that
    # prints "Could not download ..." to stdout on any failure and returns
    # None instead of raising. That print is normally lost: our startup guard
    # has already redirected fd 1 to fd 2, and the .NET client only scans
    # stderr for NDJSON progress lines. Capture both Python-level streams in
    # a StringIO for the duration of the call so we can surface the real
    # reason in the error envelope if pipeline ends up None.
    captured = io.StringIO()
    saved_out, saved_err = sys.stdout, sys.stderr
    sys.stdout = captured
    sys.stderr = captured
    try:
        try:
            pipeline = Pipeline.from_pretrained(PYANNOTE_MODEL, use_auth_token=hf_token)
        except Exception as exc:  # pragma: no cover - exercised in integration tests
            return _error_envelope(
                f"failed to load pyannote pipeline '{PYANNOTE_MODEL}': {exc}. "
                f"Captured pyannote output: {captured.getvalue().strip()!r}"
            )
    finally:
        sys.stdout, sys.stderr = saved_out, saved_err

    if pipeline is None:
        swallowed = captured.getvalue().strip()
        diag = _diagnose_none_pipeline(hf_token)
        swallowed_suffix = (
            f" Swallowed pyannote stdout/stderr during from_pretrained: {swallowed!r}."
            if swallowed
            else " No output was captured from pyannote during from_pretrained -- pipeline bailed silently."
        )
        return _error_envelope(
            f"Pipeline.from_pretrained('{PYANNOTE_MODEL}') returned None.{swallowed_suffix} "
            f"{diag} Required actions: (a) ensure HUGGING_FACE_HUB_TOKEN is exported in the "
            "shell that launches VoxFlow; (b) accept the license on BOTH "
            "https://huggingface.co/pyannote/speaker-diarization-3.1 AND "
            "https://huggingface.co/pyannote/segmentation-3.0 using the same HF account."
        )

    try:
        with _NdjsonProgressHook(sys.stderr) as progress_hook:
            diarization = pipeline(wav_path, hook=progress_hook)
    except Exception as exc:  # pragma: no cover - exercised in integration tests
        return _error_envelope(f"diarization failed: {exc}")

    speakers, segments = _normalize_speaker_labels(diarization)
    return {
        "version": PROTOCOL_VERSION,
        "status": "ok",
        "speakers": speakers,
        "segments": segments,
    }


def main() -> int:
    try:
        request = _parse_request()
    except json.JSONDecodeError as exc:
        _write_response(_error_envelope(f"malformed JSON request: {exc}"))
        return 2

    if not isinstance(request, dict):
        _write_response(_error_envelope("request must be a JSON object"))
        return 0

    version = request.get("version")
    if version != PROTOCOL_VERSION:
        _write_response(_error_envelope(f"unsupported protocol version: {version}"))
        return 0

    wav_path = request.get("wavPath")
    if not isinstance(wav_path, str) or not wav_path:
        _write_response(_error_envelope("wavPath must be a non-empty string"))
        return 0

    if not os.path.isfile(wav_path):
        _write_response(_error_envelope(f"wav file not found: {wav_path}"))
        return 0

    response = _run_diarization(wav_path)
    _write_response(response)
    return 0


if __name__ == "__main__":
    sys.exit(main())
