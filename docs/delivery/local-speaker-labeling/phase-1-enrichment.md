# Phase 1 — Enrichment

**Parent:** [Local Speaker Labeling — Delivery Plan](README.md)
**ADR:** [ADR-024](../../adr/024-local-speaker-labeling-pipeline.md)
**Status:** Implemented in current code
**Source of truth:** `src/VoxFlow.Core` and `src/VoxFlow.Cli`

---

## Purpose

This document describes the speaker-labeling enrichment behavior that is actually implemented now.

If this file disagrees with older planning notes, the code wins.

---

## Current Scope

Phase 1 is already wired into the Core transcription pipeline and the CLI host.

Implemented now:

- config-driven speaker labeling behind `transcription.speakerLabeling.enabled`
- single-file override via `TranscribeFileRequest.EnableSpeakers`
- batch support when speaker labeling is enabled in config
- startup validation warnings for Python runtime readiness and pyannote cache presence
- diarization + merge + formatter integration
- `.voxflow.json` sidecar artifact generation
- CLI progress reporting for the diarization phase

Not part of the current Phase 1 contract:

- CLI `--speakers` argument parsing
- per-request MCP `enableSpeakers` override
- Desktop UI toggle / rendering work
- standalone bundled Python runtime

---

## Current Configuration Surface

`SpeakerLabelingOptions` currently maps this shape:

```json
"speakerLabeling": {
  "enabled": false,
  "timeoutSeconds": 600,
  "pythonRuntimeMode": "ManagedVenv",
  "modelId": "pyannote/speaker-diarization-3.1"
}
```

Current behavior from code:

- If the section is missing, the loader falls back to `SpeakerLabelingOptions.Disabled`.
- `timeoutSeconds` must be greater than zero.
- `modelId` must be non-empty.
- `pythonRuntimeMode` accepts `ManagedVenv`, `SystemPython`, or `Standalone`.
- `startupValidation.checkSpeakerLabelingRuntime` exists in code and defaults to `true` when omitted.

Checked-in config files are intentionally not all the same:

- [`appsettings.example.json`](../../../appsettings.example.json) keeps `speakerLabeling.enabled=false`.
- [`appsettings.json`](../../../appsettings.json) currently has `speakerLabeling.enabled=true` and `processingMode=batch` for local/dev use.
- [`src/VoxFlow.Cli/appsettings.json`](../../../src/VoxFlow.Cli/appsettings.json) currently has `speakerLabeling.enabled=true` and `resultFormat=md`.

Request-level behavior:

- Single-file requests can override config via `TranscribeFileRequest.EnableSpeakers`.
- The effective single-file flag is `request.EnableSpeakers ?? options.SpeakerLabeling.Enabled`.
- Batch processing currently has no per-file speaker override; it uses config only.
- The CLI host reads configuration and does not parse a command-line speaker flag.

---

## Pipeline Behavior

### Single-file path

`TranscriptionService` currently does the following:

1. Load configuration.
2. Run startup validation.
3. Convert the input audio to WAV.
4. Load the Whisper model.
5. Load WAV samples.
6. Transcribe and select the winning language.
7. If the effective speaker flag is `true`, run speaker enrichment.
8. Write the primary output file in the configured format.
9. If enrichment produced a `TranscriptDocument`, write `{resultPath}.voxflow.json`.
10. Build the preview text for `TranscribeFileResult`.

Important invariants:

- When speaker labeling is disabled, the runtime and sidecar are not invoked.
- If enrichment fails, transcription still completes and writes the primary output using the accepted Whisper segments.
- `TranscribeFileResult.SpeakerTranscript` is populated only when enrichment returns a document.
- `TranscribeFileResult.EnrichmentWarnings` carries enrichment-specific warnings.
- The preview remains plain TXT built from accepted segments; it is not speaker-aware.

### Batch path

`BatchTranscriptionService` mirrors the same enrichment branch per discovered file:

- speaker enrichment runs only when `options.SpeakerLabeling.Enabled` is `true`
- warnings are attached to the per-file output context
- `{outputPath}.voxflow.json` is written only when enrichment produced a document
- batch requests currently do not expose a request-level speaker override

---

## Enrichment Behavior

`ISpeakerEnrichmentService` owns runtime readiness, optional bootstrap, sidecar invocation, timeout handling, cancellation handling, progress mapping, and merge orchestration.

### Runtime modes

Current runtime-mode behavior:

- `ManagedVenv`: uses `ManagedVenvRuntime` plus a managed-venv bootstrapper
- `SystemPython`: uses `python3` from `PATH`
- `Standalone`: declared in config, but currently returns a warning and no document

`CompositionSpeakerEnrichmentService` rebuilds the concrete runtime/sidecar tree per call from `SpeakerLabelingOptions.RuntimeMode`.

### Bootstrap behavior

If the runtime reports `NotReady` and `CanBootstrap == true`, `SpeakerEnrichmentService` triggers bootstrap and then re-checks runtime status.

Current limitation:

- bootstrap is reachable
- bootstrap-specific progress is **not** forwarded to the outer progress reporter because the bootstrapper is currently called with `progress: null`

### Failure behavior

Current non-fatal warning behavior:

- runtime not ready: `speaker-labeling: runtime not ready: ...`
- sidecar failure: `speaker-labeling: <reason>: <message>`
- timeout: `speaker-labeling: timed out after {timeoutSeconds}s`
- zero-speaker merge result: `speaker-labeling: diarization returned zero speakers`

Unexpected enrichment exceptions are caught one layer higher by `TranscriptionService` and `BatchTranscriptionService` and converted to:

- `speaker-labeling: internal error: ...`

Outer cancellation is still rethrown.

### Progress behavior

Current progress mapping:

- the sidecar stage reports as `ProgressStage.Diarizing`
- enrichment emits an initial `Diarizing` update at `90%` before the sidecar call
- sidecar-local progress maps into the `90..95` range
- writing stays at `95%`
- complete stays at `100%`

The CLI maps `ProgressStage.Diarizing` into its dedicated `Diarization` phase.

---

## Merge Behavior

`SpeakerMergeService` currently does all of the following:

- filters Whisper special tokens such as `[_BEG_]` / `[_EOT_]`
- groups BPE subwords and attached punctuation into one logical word before speaker assignment
- supports both relative test token timestamps and absolute runtime token timestamps
- assigns a speaker by maximum overlap with diarization segments
- falls back to the nearest diarization segment when there is no overlap
- renormalizes raw diarization labels to ordinal speaker ids `A`, `B`, `C`, ... by first appearance
- collapses consecutive same-speaker words into speaker turns

This means the speaker-aware transcript is derived from word-level timing, not by naively stamping whole Whisper segments.

---

## Output Behavior

### Primary output formats

Current formatter behavior when `SpeakerTranscript` is present:

- `txt`: one line per turn, `Speaker A: ...`
- `md`: keeps the metadata header, then renders turns as `**Speaker A:** ...`
- `json`: keeps legacy `segments` and `transcript`, and additionally includes `speakerTranscript`
- `srt`: keeps original cue boundaries and prefixes cue text with `Speaker A: ` using the best overlapping speaker turn
- `vtt`: keeps original cue boundaries and uses `<v Speaker A>` voice tags

When `SpeakerTranscript` is `null`, all formatters stay on their legacy branch.

### `.voxflow.json` artifact

`VoxflowTranscriptArtifactWriter` currently:

- writes to `{resultPath}.voxflow.json`
- serializes the `TranscriptDocument` directly
- uses indented camelCase JSON
- writes through a temp file and then atomically renames it
- deletes the temp file on failure or cancellation

The artifact is written only when enrichment produced a non-null `TranscriptDocument`.

---

## Validation Behavior

Speaker-labeling startup checks run only when both conditions are true:

- `options.SpeakerLabeling.Enabled`
- `options.StartupValidation.CheckSpeakerLabelingRuntime`

Current preflight behavior:

- runtime readiness is checked without spawning the diarization sidecar
- model-cache presence is checked in the Hugging Face cache
- cache root resolution follows `HF_HUB_CACHE`, then `HF_HOME/hub`, then the platform default cache path
- `ManagedVenv` and `SystemPython` are probed directly
- `Standalone` reports not ready in Phase 1

Important startup rule:

- speaker-labeling preflight problems surface as warnings, not hard startup failures

That is why the application can still start transcription even when speaker labeling is enabled but Python/model prerequisites are not ready.

---

## Tests Covering Phase 1

The current implementation is covered mainly by:

- `tests/VoxFlow.Core.Tests/Services/Diarization/SpeakerEnrichmentServiceTests.cs`
- `tests/VoxFlow.Core.Tests/Services/Diarization/CompositionSpeakerEnrichmentServiceTests.cs`
- `tests/VoxFlow.Core.Tests/Services/Diarization/CompositionSpeakerLabelingPreflightTests.cs`
- `tests/VoxFlow.Core.Tests/Services/Diarization/SpeakerMergeServiceTests.cs`
- `tests/VoxFlow.Core.Tests/TranscriptionServiceTests.cs`
- `tests/VoxFlow.Core.Tests/BatchTranscriptionServiceTests.cs`
- `tests/VoxFlow.Core.Tests/TranscriptFormatterTests.cs`
- `tests/VoxFlow.Core.Tests/Services/VoxflowTranscriptArtifactWriterTests.cs`
- `tests/VoxFlow.Core.Tests/ValidationServiceSpeakerLabelingTests.cs`
- `tests/VoxFlow.Cli.Tests/CliProgressHandlerTests.cs`

Useful commands:

```bash
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
dotnet test tests/VoxFlow.Cli.Tests/VoxFlow.Cli.Tests.csproj
```

Real Python/pyannote integration tests are opt-in:

```bash
VOXFLOW_RUN_REQUIRES_PYTHON_TESTS=1 dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj --filter Category=RequiresPython
```

For manual fixture-based checks, see [phase-1-manual-verification.md](phase-1-manual-verification.md).

---

## Known Gaps

These are real current limitations in code, not future placeholders:

- the CLI host does not expose a `--speakers` flag
- batch processing has no request-level speaker override
- `Standalone` runtime mode is declared but not implemented
- managed-venv bootstrap progress is not surfaced to the outer progress reporter
- the preview in `TranscribeFileResult` is still legacy plain text, not speaker-aware
- real Python/pyannote integration coverage is opt-in rather than always-on
