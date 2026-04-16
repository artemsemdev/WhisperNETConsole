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
    try:
        pipeline = Pipeline.from_pretrained(PYANNOTE_MODEL, use_auth_token=hf_token)
    except Exception as exc:  # pragma: no cover - exercised in integration tests
        return _error_envelope(f"failed to load pyannote pipeline '{PYANNOTE_MODEL}': {exc}")

    if pipeline is None:
        return _error_envelope(
            f"Pipeline.from_pretrained('{PYANNOTE_MODEL}') returned None. "
            "This usually means HUGGING_FACE_HUB_TOKEN is missing or invalid, or you "
            "have not accepted the license on one of the pipeline's component models. "
            "Ensure the token is set and that you have clicked 'Agree and access "
            "repository' on BOTH https://huggingface.co/pyannote/speaker-diarization-3.1 "
            "AND https://huggingface.co/pyannote/segmentation-3.0."
        )

    try:
        diarization = pipeline(wav_path)
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
