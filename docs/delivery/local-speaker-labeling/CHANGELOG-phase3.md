# Local Speaker Labeling — Phase 3 Changelog

Summary of what shipped on the `Local-Speaker-Labeling` integration branch across all four phases of the [ADR-024](../../adr/024-local-speaker-labeling-pipeline.md) delivery. Grouped by phase; within each phase, sub-PRs are listed in merge order.

Phase-3 sub-PRs live on `speaker-labeling/phase-3-cli-mcp-polish` and have not yet been merged into `Local-Speaker-Labeling` as of this document's date (they are the subject of the Phase 3 PR that opens alongside this changelog).

---

## Phase 0 — Foundation

Delivered the contract layer, abstractions, and pure-logic merge service. No end-to-end wiring.

- **P0.1** — `feat(core): preserve WhisperToken[] through TranscriptionFilter` (3bcd1f8). Word-level timestamps are no longer discarded by filtering, unblocking downstream merge.
- **P0.2** — `feat(core): add TranscriptDocument model and sidecar JSON schemas` (a922834). `TranscriptDocument` / `SpeakerInfo` / `TranscriptWord` / `SpeakerTurn` types; `docs/contracts/sidecar-diarization-v1.schema.json` and `voxflow-transcript-v1.schema.json`.
- **P0.3** — `feat(core): add IPythonRuntime abstraction and SystemPythonRuntime` (1ad40c4). Dev/CI escape-hatch runtime that resolves `python3` from `PATH`.
- **P0.4** — `feat(core): add ManagedVenvRuntime for speaker-labeling sidecar` (8300f28). Default user-facing runtime backed by a VoxFlow-managed venv in `~/Library/Application Support/VoxFlow/venv/`.
- **P0.5** — `feat(core): add voxflow_diarize.py sidecar script` (761a5a5). Python sidecar implementing the versioned JSON contract with `Xunit.SkippableFact` integration scaffolding.
- **P0.6** — `feat(core): add PyannoteSidecarClient bridging IPythonRuntime and voxflow_diarize.py` (77842cd). Process launcher + JSON I/O + timeout.
- **P0.7** — `feat(core): add SpeakerMergeService merging Whisper tokens with diarization` (c2c7e8c). Pure-logic merge of word timings and speaker segments with deterministic `Speaker A`/`B`/... ordinals.
- **P0.8** — `test(core): add LibriCSS 2/3-speaker + Obama single-speaker audio fixtures` (851e29f, b18b7fc). Integration fixtures for the `[Trait("Category", "RequiresPython")]` suite.

---

## Phase 1 — Enrichment wiring

Plumbed the Phase-0 building blocks into the main transcription pipeline behind `speakerLabeling.enabled`, added configuration/validation/output writers, and produced the first end-to-end speaker-labeled output on CLI and MCP.

- Config: `transcription.speakerLabeling` section in `appsettings.json` / `appsettings.example.json` (enabled flag, timeout, runtime mode, model id).
- Orchestration: `ISpeakerEnrichmentService` / `SpeakerEnrichmentService` wires sidecar + merge + managed-venv bootstrapper under the `TranscriptionService`.
- First-run: `IManagedVenvBootstrapper` + `ManagedVenvBootstrapper` drive the explicit, cancellable `python -m venv` + `pip install` flow with progress stages.
- Validation: `IValidationService` preflight surfaces clear diagnostics when Python runtime or pyannote model are missing.
- Output writers: `.txt`, `.srt`, `.vtt`, `.json`, `.md` render `Speaker A:` / `Speaker B:` when a `TranscriptDocument` is present; the `null` path is unchanged.
- Artifact: `VoxflowTranscriptArtifactWriter` writes `.voxflow.json` with schema validation round-trip.
- Failure model: sidecar crash / timeout / malformed JSON is non-fatal — transcription-only output still flows, with `EnrichmentWarnings` populated.
- Output routing fixes: concat Whisper BPE subwords, enable per-token timestamps, group BPE subwords before speaker assignment (9f3dc32, 94612ab, 3bde896, d7d7d81).

---

## Phase 2 — Desktop UI

Exposed the feature on the Mac Catalyst Desktop with a redesigned Running/Complete experience and a colored speaker renderer.

- Ready-screen toggle (`SpeakerLabelingToggle`) with persistence to `appsettings.json`.
- `PhaseRing` / `PhaseRingStack` components showing three per-phase progress rings (Transcription cyan / Diarization magenta / Merge green), with Idle / Running / Done / Skipped / Failed states (final shipped design: nested concentric arcs, bundled via the Mac Catalyst `.app`).
- `SpeakerTranscriptView` Razor component renders speaker turns with the Okabe-Ito colorblind-safe palette on the completion screen; cycles if >8 speakers.
- Shared `TopBar` + bottom-sheet `Settings`; consistent layout across Running / Complete / Failed views.
- Desktop tests under `tests/VoxFlow.Desktop.Tests/Components/` cover the renderer and toggle.

---

## Phase 3 — CLI / MCP / docs / Standalone runtime / release

Last phase of the delivery — surfaces, docs, spike follow-up, and release prep. Sub-PRs land on `speaker-labeling/phase-3-cli-mcp-polish`:

- **P3.1** — `feat(cli): add --speakers/--no-speakers/--help flags` (1694344). CLI surface for `enabled` override per invocation.
- **P3.2** — `feat(mcp): add enableSpeakers parameter to transcribe_file tool` (dae98c4). MCP tool-schema update, wired into host consistency tests.
- **P3.3** — `docs(runbook): add local speaker labeling operational runbook` (1186607). `docs/runbooks/speaker-labeling.md` covers first-run setup, model-download failures, sidecar diagnostics.
- **P3.4** — `docs: architecture + setup + README entries for speaker labeling` (198417a). `docs/architecture/ARCHITECTURE.md`, `docs/developer/setup.md`, top-level README snippet; ADR-024 already carries the PyInstaller → python-build-standalone amendment from the pre-Phase-0 work.
- **P3.5** — `feat(core): StandaloneRuntime for bundled python-build-standalone` (f986a9a). `IPythonRuntime` implementation that points at `{AppContext.BaseDirectory}/python-standalone/` with `PYTHONHOME` / `PYTHONPATH` env pinning; wired into the DI composition root via `IStandaloneRuntimePaths`. TDD: `StandaloneRuntimeTests` (5 cases) + `PythonVersionParser` extracted and shared with `SystemPythonRuntime`.
- **P3.6** — Release prep (this PR). Acceptance criteria audited with evidence links, `Acceptance Check Before Promotion` ticked, this changelog written, Phase-3 PR hand-off footer appended to [README.md](README.md). No production code changes.

---

## Local test verification (P3.6)

Captured during the release-prep pass on `speaker-labeling/phase-3-cli-mcp-polish` at HEAD:

```
dotnet test VoxFlow.sln --nologo --verbosity minimal
```

| Project | Passed | Failed | Skipped | Total |
|---|---|---|---|---|
| VoxFlow.Core.Tests | 347 | 0 | 7 | 354 |
| VoxFlow.Cli.Tests | 29 | 0 | 0 | 29 |
| VoxFlow.McpServer.Tests | 39 | 0 | 0 | 39 |
| VoxFlow.Desktop.Tests | 145 | 0 | 2 | 147 |
| VoxFlow.Desktop.UiTests | 0 | 0 | 6 | 6 |
| **Total** | **560** | **0** | **15** | **575** |

Justified skips:

- **7 Core skips** — `[Trait("Category", "RequiresPython")]` integration tests (`PyannoteSidecarClientIntegrationTests`, `SidecarScriptContractTests`). These are run explicitly via `dotnet test --filter "Category=RequiresPython"` on a machine with Python 3.10+ and `pyannote.audio` installed; the default run filters them out per Phase 0 decision.
- **2 Desktop skips** — `DesktopUiComponentTests.Routes_BrowseFile_WithRealAudio_CompletesTranscription` and `ReadyView_BrowseFile_*`. Gated on a real Whisper model and audio pipeline at runtime.
- **6 Desktop.UiTests skips** — end-to-end Mac Catalyst UI tests that require a live app bundle; opt-in harness.

No new `[Skipped]` counts were introduced by Phase 3. Manual smoke test on `artifacts/input/President Obama Speech.m4a` ran clean end-to-end (Transcription → Diarization → Merge → labeled output) during the desktop rollout and was re-verified at P3.6 close.
