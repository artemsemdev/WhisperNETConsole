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


def _write_response(payload: dict[str, Any]) -> None:
    sys.stdout.write(json.dumps(payload))
    sys.stdout.flush()


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

    try:
        pipeline = Pipeline.from_pretrained(PYANNOTE_MODEL)
    except Exception as exc:  # pragma: no cover - exercised in integration tests
        return _error_envelope(f"failed to load pyannote pipeline '{PYANNOTE_MODEL}': {exc}")

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
