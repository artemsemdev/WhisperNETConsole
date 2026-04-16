# Phase 1 — Enrichment

**Parent:** [Local Speaker Labeling — Delivery Plan](README.md)
**ADR:** [ADR-024](../../adr/024-local-speaker-labeling-pipeline.md)
**Status:** Planned

---

## Goal

Wire the Phase 0 components into the real transcription pipeline behind the `speakerLabeling.enabled` flag. At the end of Phase 1, a developer who sets the flag to `true` in `appsettings.json` (or passes `--speakers` on the CLI / `enableSpeakers:true` via MCP) sees a speaker-labeled transcript in every supported output format and a new `.voxflow.json` artifact — without any Desktop UI work yet.

Phase 1 is the first phase that touches user-visible behavior. Its most important invariant is **zero regression for users who do not turn on the flag**: every existing test for disabled speaker labeling must stay green with byte-identical output, and the pipeline must not spawn the sidecar at all in that path.

## Exit Criteria

- `transcription.speakerLabeling` section exists in both [appsettings.json](../../../appsettings.json) and [appsettings.example.json](../../../appsettings.example.json) with all four keys (`enabled`, `timeoutSeconds`, `pythonRuntimeMode`, `modelId`), defaults are `enabled=false`, and `TranscriptionOptions.LoadFromPath` exposes a typed `SpeakerLabelingOptions` property.
- `TranscribeFileRequest` carries an optional `bool? EnableSpeakers` override that takes precedence over the config flag for a single invocation.
- `ISpeakerEnrichmentService` exists and orchestrates `IDiarizationSidecar` + `ISpeakerMergeService`, including timeout and failure handling that never lets a sidecar failure abort transcription.
- `TranscriptionService.TranscribeFileAsync` calls the enrichment service only when the effective flag is `true`; the disabled path is byte-identical to pre-Phase-1 (verified by a dedicated golden test).
- `TranscribeFileResult` exposes `TranscriptDocument? SpeakerTranscript` and `IReadOnlyList<string> EnrichmentWarnings`; all failure modes surface through the warnings list and leave `SpeakerTranscript = null`.
- Every output writer (`.txt`, `.srt`, `.vtt`, `.json`, `.md`) renders `Speaker A:` / `Speaker B:` prefixes when a `TranscriptDocument` is present, and produces its pre-Phase-1 output when `SpeakerTranscript` is `null` — proven by unit tests on both branches per formatter.
- A new `.voxflow.json` artifact is written next to the configured result file whenever `SpeakerTranscript` is not `null`; its contents round-trip against `docs/contracts/voxflow-transcript-v1.schema.json`.
- `IValidationService` has a new `CheckSpeakerLabelingPrerequisites` that runs only when the effective flag is `true`, surfaces actionable diagnostics for "runtime not ready" and "pyannote model not cached", and contributes to `ValidationResult.Checks` using the same status shape as the existing checks.
- First-run bootstrap of `ManagedVenvRuntime` is reachable from the pipeline: when the runtime reports `NotReady` and `enabled=true`, the orchestrator triggers venv creation + requirements install, surfaces progress via `IProgress<ProgressUpdate>`, and is cancellable.
- A new `ProgressStage.Diarizing` is plumbed end-to-end so the host receives progress updates while the sidecar is running.
- `dotnet test` is fully green locally with zero regressions in the disabled path; the enabled path is covered by unit tests on mocked sidecar responses and (where Python 3.10+ is present) by `Category=RequiresPython` integration tests that go through the real orchestrator.
- No Desktop UI changes, no CLI arg parser yet, no MCP schema changes yet — those land in Phase 2 and Phase 3.

## Pre-conditions

- Phase 0 is merged into `Local-Speaker-Labeling` and green on the integration branch.
- `SpeakerMergeService` (P0.7), `PyannoteSidecarClient` (P0.6), both `IPythonRuntime` implementations (P0.3/P0.4), the sidecar script (P0.5), the transcript model (P0.2), and audio fixtures (P0.8) all exist and are unit-tested.
- Developer machine has Python 3.10+ available locally so the orchestrator integration tests can be run before opening sub-PRs.

## Non-goals (deferred to Phase 2 / Phase 3)

- Desktop Ready-screen toggle and completion-screen colored rendering (Phase 2).
- CLI `--speakers` argument parser and `appsettings` override mechanism (Phase 3).
- MCP `enableSpeakers` tool parameter + tool schema update (Phase 3).
- `StandaloneRuntime` / `python-build-standalone` bundle (Phase 3, conditional on spike outcome).
- Speaker renaming, colored palettes, Okabe-Ito palette — all display-only concerns that belong in Phase 2.

---

## TDD Sequence

Seven sub-PRs, in strict order. Each PR is the smallest unit that leaves the integration branch in a green-tests state, and each PR either introduces a new unit of behavior or wires an existing one into the pipeline — never both.

Conventions for every PR below:
- **Branch:** `speaker-labeling/p1.M-<slug>` off `Local-Speaker-Labeling`.
- **Base for PR:** `Local-Speaker-Labeling`.
- **Before `gh pr create`:** run `dotnet test VoxFlow.sln` locally and confirm no previously-green test has turned red. If the PR touches integration code, also run `dotnet test --filter "Category=RequiresPython"` on a machine with Python 3.10+ + pyannote available. Report both in the PR body.
- **Test tagging:** same rules as Phase 0 — xUnit 2.9 traits; `[Trait("Category", "RequiresPython")]` for tests that spawn the real sidecar; `SkippableFact` when a test depends on environment state (runtime readiness, fixture presence, etc.).
- **Commit authorship:** user only; no Co-Authored-By trailers.
- **PR body:** no "Generated with Claude Code" footer.
- **Disabled-path invariant:** every PR below must preserve byte-identical output when `speakerLabeling.enabled=false` AND `TranscribeFileRequest.EnableSpeakers` is `null`. Each sub-PR adds at least one regression guard test for this invariant on the code path it touches.

---

### P1.1 — `SpeakerLabelingOptions` config binding

**Branch:** `speaker-labeling/p1.1-options`

**Why first:** Everything downstream reads from `TranscriptionOptions.SpeakerLabeling`. The options type has to exist and be validated before any service can depend on it, and introducing it first keeps the PR a pure additive change — no pipeline wiring yet, only configuration shape plus loader.

**Files touched (new):**
- `src/VoxFlow.Core/Configuration/SpeakerLabelingOptions.cs` — immutable `sealed record` carrying `Enabled`, `TimeoutSeconds`, `PythonRuntimeMode`, `ModelId`. Includes a `Disabled` static instance for the default branch.
- `src/VoxFlow.Core/Configuration/PythonRuntimeMode.cs` — enum `{ SystemPython, ManagedVenv, Standalone }`. `Standalone` is declared here even though its runtime lands in Phase 3; declaring the enum now keeps the config schema stable.
- `tests/VoxFlow.Core.Tests/Configuration/SpeakerLabelingOptionsTests.cs`

**Files touched (modified):**
- `src/VoxFlow.Core/Configuration/TranscriptionOptions.cs` — add `public SpeakerLabelingOptions SpeakerLabeling { get; }`, populate it in the private constructor via a new `CreateSpeakerLabelingOptions` helper, and add a matching nullable `SpeakerLabelingConfiguration` class to `TranscriptionConfiguration` so JSON binding works. When the JSON section is absent, default to `SpeakerLabelingOptions.Disabled`.
- [appsettings.json](../../../appsettings.json) and [appsettings.example.json](../../../appsettings.example.json) — add the `speakerLabeling` nested section inside `transcription` with `enabled=false`, `timeoutSeconds=600`, `pythonRuntimeMode="ManagedVenv"`, `modelId="pyannote/speaker-diarization-3.1"`. The example file must mirror the loader-compatible shape exactly; otherwise `TestSettingsFileFactory` drifts.

**TDD steps:**

1. **Red.** `SpeakerLabelingOptionsTests.Disabled_StaticInstance_HasEnabledFalse_AndHarmlessDefaults`. Compile fails: type doesn't exist.
2. **Green.** Create the record and a `static readonly SpeakerLabelingOptions Disabled = new(Enabled: false, TimeoutSeconds: 600, RuntimeMode: PythonRuntimeMode.ManagedVenv, ModelId: "pyannote/speaker-diarization-3.1");`. Test passes.
3. **Red.** `SpeakerLabelingOptionsTests.Construct_ValidInputs_ExposesFields`. Assert every property round-trips from the constructor.
4. **Green.** Nothing to change — the record satisfies it. This is a guard test.
5. **Red.** `SpeakerLabelingOptionsTests.Construct_NegativeTimeout_Throws`. Construct with `TimeoutSeconds = -1`, expect an `InvalidOperationException` mentioning the setting name.
6. **Green.** Enforce validation in the record's primary constructor via a private helper.
7. **Red.** `TranscriptionOptionsTests.LoadFromPath_SpeakerLabelingSectionPresent_ParsesAllFields`. Use `TestSettingsFileFactory` to write a settings file containing the section; load it; assert `options.SpeakerLabeling.Enabled == true` and the other three fields match.
8. **Green.** Add `SpeakerLabelingConfiguration` DTO, `SpeakerLabeling` property on `TranscriptionConfiguration`, `SpeakerLabeling` property on `TranscriptionOptions`, and `CreateSpeakerLabelingOptions` helper that maps configuration → options (or returns `Disabled` if the section is null). Map `pythonRuntimeMode` string to the enum case-insensitively.
9. **Red.** `TranscriptionOptionsTests.LoadFromPath_SpeakerLabelingSectionMissing_DefaultsToDisabled`. Existing test-generated settings files omit the section; assert `options.SpeakerLabeling.Enabled == false` and `options.SpeakerLabeling.Equals(SpeakerLabelingOptions.Disabled)`.
10. **Green.** The helper already returns `Disabled` for null; this test confirms that backwards-compat promise holds.
11. **Red.** `TranscriptionOptionsTests.LoadFromPath_UnknownRuntimeMode_Throws`. Write `pythonRuntimeMode="Wat"`; expect `InvalidOperationException` listing the valid names.
12. **Green.** Add explicit switch-expression mapping with a default arm that throws.
13. **Red.** Commit the updated `appsettings.json` and `appsettings.example.json`. Add `TranscriptionOptionsTests.LoadFromPath_RealAppsettingsJson_ParsesSpeakerLabelingDefaults` that uses the repo-root `appsettings.json` (via `TestSettingsFileFactory.LoadReal` or the existing test helper); asserts `enabled=false` by default, runtime mode is `ManagedVenv`.
14. **Green.** Run the test; commit.
15. **Refactor.** Make `SpeakerLabelingOptions.Disabled` the single source of truth for default values, and reuse it from `CreateSpeakerLabelingOptions` when any field is missing from JSON.

**Blast radius — other test files that load `TranscriptionOptions`:**
- `tests/VoxFlow.Core.Tests/TranscriptionOptionsTests.cs`
- `tests/VoxFlow.Core.Tests/ConfigurationServiceTests.cs`
- `tests/TestSupport/TestSettingsFileFactory.cs` (shared helper)
- `tests/VoxFlow.Cli.Tests/*`, `tests/VoxFlow.Desktop.Tests/*`, `tests/VoxFlow.McpServer.Tests/*` that copy `appsettings.json` via the test support helper.

The `SpeakerLabelingOptions.Disabled` fallback means these files do **not** need edits; the loader simply returns `Disabled` when their JSON is missing the section. Verifying this is the PR's self-check: `dotnet build` and `dotnet test` must succeed with no edits to the host tests.

**Local verification before PR:**
```
dotnet test VoxFlow.sln
```

**PR description template:**
```
Add SpeakerLabelingOptions config section

Introduces transcription.speakerLabeling nested section in appsettings.json
with enabled/timeoutSeconds/pythonRuntimeMode/modelId keys (default
enabled=false). TranscriptionOptions now exposes SpeakerLabeling via a
typed record. Missing JSON section falls back to SpeakerLabelingOptions.Disabled
so existing test fixtures and host apps continue to parse without edits.
Pure additive change — no pipeline wiring, no Desktop/CLI/MCP surface yet.

Test plan:
- [x] dotnet test VoxFlow.sln — green, no regressions
```

---

### P1.2 — `TranscribeFileRequest.EnableSpeakers` override

**Branch:** `speaker-labeling/p1.2-request-override`

**Why second:** Before the orchestrator exists, we need the public contract for per-invocation overrides. Adding it as a second trailing nullable parameter on `TranscribeFileRequest` is a pure-additive change that lets the CLI and MCP hosts override the config flag without duplicating configuration state. Shipping it as its own PR means P1.3 can depend on the field being present without also dragging in orchestrator logic.

**Files touched (modified):**
- `src/VoxFlow.Core/Models/TranscribeFileRequest.cs` — add `bool? EnableSpeakers = null` as a **trailing positional record parameter with a default**. The null default is load-bearing: it preserves every existing call site in `TranscriptionService`, `BatchTranscriptionService`, `DesktopCliTranscriptionService`, and the MCP tools.
- `tests/VoxFlow.Core.Tests/Models/TranscribeFileRequestTests.cs` — new file holding one positional-construct test and one override-semantics test.

**TDD steps:**

1. **Red.** `TranscribeFileRequestTests.Construct_WithExistingPositionalArgs_StillCompiles_AndEnableSpeakersDefaultsToNull`. Calls the pre-existing 5-arg positional constructor and asserts `request.EnableSpeakers is null`. Compile fails: the field doesn't exist.
2. **Green.** Add `bool? EnableSpeakers = null` as the sixth positional record parameter. Test passes.
3. **Red.** `TranscribeFileRequestTests.Construct_WithEnableSpeakersTrue_PreservesValue`.
4. **Green.** Already satisfied by the record's positional ctor.
5. **Red.** Add a pipeline-invariant assertion elsewhere: in `TranscriptionServiceTests.TranscribeFileAsync_DisabledConfig_AndNullOverride_DoesNotCallEnrichment` (guard test that will be wired in P1.3 — for P1.2 this test is a placeholder that is compiled and passes trivially by checking that existing pipeline output is byte-identical for a request constructed with `EnableSpeakers=null`). Write it as a compiling but shallow test now; P1.3 will tighten it.

**Blast radius:** every call site that constructs `TranscribeFileRequest` positionally continues to compile thanks to the trailing default. Touch nothing else in this PR. If any existing call site fails to build, the default is wrong and must be revisited before landing.

**Local verification:**
```
dotnet test VoxFlow.sln
```

**PR description template:**
```
Add TranscribeFileRequest.EnableSpeakers override

Adds an optional bool? EnableSpeakers parameter as a trailing positional
record field, defaulting to null. This is the public hook that CLI and
MCP hosts will use later to override speakerLabeling.enabled per request.
Pure additive change — no new behavior, no pipeline wiring. Every existing
positional call site compiles unchanged because of the null default.

Test plan:
- [x] dotnet test VoxFlow.sln — green
```

---

### P1.3 — `ISpeakerEnrichmentService` orchestrator

**Branch:** `speaker-labeling/p1.3-enrichment-service`

**Why third:** This is where the Phase 0 components are finally composed. The service owns the runtime-readiness check, the sidecar call, the merge, the timeout envelope, and the failure taxonomy. It is the sole consumer of `IDiarizationSidecar` and `ISpeakerMergeService` within `TranscriptionService`. Shipping it as its own PR lets the downstream pipeline wiring (P1.4) be a trivial one-line call.

**Files touched (new):**
- `src/VoxFlow.Core/Interfaces/ISpeakerEnrichmentService.cs`
- `src/VoxFlow.Core/Services/Diarization/SpeakerEnrichmentService.cs`
- `src/VoxFlow.Core/Models/SpeakerEnrichmentResult.cs` — `{ TranscriptDocument? Document, IReadOnlyList<string> Warnings, bool RuntimeBootstrapped }`.
- `tests/VoxFlow.Core.Tests/Services/Diarization/SpeakerEnrichmentServiceTests.cs`
- `tests/VoxFlow.Core.Tests/Services/Diarization/FakeDiarizationSidecar.cs` — test double whose `DiarizeAsync` is driven by a delegate supplied per test.
- `tests/VoxFlow.Core.Tests/Services/Diarization/FakePythonRuntime.cs` — test double that returns configurable `PythonRuntimeStatus` and simulates venv bootstrap via an `IProgress<VenvBootstrapStage>` reporter.

**Interface shape:**
```csharp
public interface ISpeakerEnrichmentService
{
    Task<SpeakerEnrichmentResult> EnrichAsync(
        string wavPath,
        IReadOnlyList<FilteredSegment> segments,
        TranscriptMetadata metadata,
        SpeakerLabelingOptions options,
        IProgress<ProgressUpdate>? progress,
        CancellationToken cancellationToken);
}
```

**Orchestration contract:**

1. If `options.Enabled == false`, return an empty `SpeakerEnrichmentResult` synchronously without touching `IPythonRuntime` or `IDiarizationSidecar`. This is the hot-path short-circuit that guarantees zero cost for disabled users.
2. Get runtime status via `IPythonRuntime.GetStatusAsync`.
3. If status is `NotReady` and the runtime is `ManagedVenvRuntime` and the failure is "venv not yet created", attempt to bootstrap the venv. Forward bootstrap progress as `ProgressStage.Diarizing` updates with descriptive messages ("Creating Python environment...", "Installing diarization runtime...", "Verifying..."). Set `RuntimeBootstrapped=true` on success.
4. If status is still `NotReady` after any bootstrap attempt, return with `Document=null` and a warning prefixed `"speaker-labeling: runtime not ready: "` followed by the status error.
5. Apply the per-call timeout from `options.TimeoutSeconds` via `CancellationTokenSource.CreateLinkedTokenSource`.
6. Call `IDiarizationSidecar.DiarizeAsync`. Forward the sidecar's own `IProgress<SpeakerLabelingProgress>` into the outer `IProgress<ProgressUpdate>` as `Diarizing` stage updates (percent maps from sidecar stage → linear within the 85–95 band of the pipeline, leaving the `Writing` stage at 95+).
7. On `DiarizationSidecarException`, return with `Document=null` and a warning built from `reason` + `message`. Log (via tracing) but never rethrow.
8. On `OperationCanceledException` where the outer token is not cancelled (i.e. timeout fired), return with `Document=null` and a warning `"speaker-labeling: timed out after {timeoutSeconds}s"`. If the outer token *is* cancelled, rethrow — the caller is shutting down.
9. On success, call `ISpeakerMergeService.Merge(segments, diarization, metadata)` and return the resulting document with an empty warnings list.

**TDD steps (unit, mocked runtime + sidecar):**

1. **Red.** `SpeakerEnrichmentServiceTests.EnrichAsync_Disabled_ReturnsEmptyDocument_WithoutTouchingRuntime`. Fake runtime throws if called, fake sidecar throws if called. Pass `options.Enabled=false`. Assert `result.Document is null`, `result.Warnings` empty, fake counters both zero. Compile fails: types don't exist.
2. **Green.** Create service and short-circuit path. Test passes.
3. **Red.** `EnrichAsync_Enabled_RuntimeReady_CallsSidecarAndMerges`. Fake runtime returns `Ready`, fake sidecar returns a canned `DiarizationResult`. Assert `result.Document` non-null and equal to what the merge service would produce for the same inputs.
4. **Green.** Wire `IDiarizationSidecar` + `ISpeakerMergeService`. Test passes.
5. **Red.** `EnrichAsync_Enabled_RuntimeNotReady_VenvNotCreated_BootstrapsAndRecoversSuccessfully`. Fake runtime starts `NotReady` with the venv-missing error; on retry after bootstrap it reports `Ready`. Assert `result.RuntimeBootstrapped == true` and the sidecar was called exactly once.
6. **Green.** Add the bootstrap branch. Because `SpeakerEnrichmentService` only knows about `IPythonRuntime`, the bootstrap call is exposed through a new `IManagedVenvBootstrapper` interface defaulted to `ManagedVenvRuntime`'s own method; inject a fake of this interface.
7. **Red.** `EnrichAsync_Enabled_RuntimeNotReady_NonBootstrapable_ReturnsWarning`. Fake runtime returns `NotReady` with `"python3 not found on PATH"`; no bootstrapper should be called. Assert `result.Document is null`, single warning starts with `"speaker-labeling: runtime not ready:"`.
8. **Green.** Branch on the error string or (better) on a new `PythonRuntimeStatus.CanBootstrap` boolean that `ManagedVenvRuntime` sets when the error is recoverable.
9. **Red.** `EnrichAsync_Enabled_SidecarReturnsErrorResponse_ReturnsWarning`. Fake sidecar throws `DiarizationSidecarException(ErrorResponseReturned, "pyannote: CUDA OOM")`. Assert warning `"speaker-labeling: error-response-returned: pyannote: CUDA OOM"`, `Document` null.
10. **Green.** Catch the exception, format the warning using `reason` in kebab-case.
11. **Red.** `EnrichAsync_Enabled_SidecarCrashes_ReturnsWarning`. Throws with `ProcessCrashed`.
12. **Green.** Same handling — the catch block already covers all `SidecarFailureReason` values.
13. **Red.** `EnrichAsync_Enabled_SidecarTimesOut_ReturnsWarning_AndDoesNotRethrow`. Fake sidecar waits indefinitely; options.TimeoutSeconds=1. Assert warning `"speaker-labeling: timed out after 1s"` and no exception propagates.
14. **Green.** Implement the linked-CTS + catch logic.
15. **Red.** `EnrichAsync_OuterCancellation_Rethrows`. Cancel the outer token before the sidecar would return. Assert `OperationCanceledException` propagates.
16. **Green.** Distinguish "timeout fired" from "outer cancel" using the linked-CTS source.
17. **Red.** `EnrichAsync_Enabled_RuntimeReady_ForwardsProgressAsDiarizingStage`. Fake sidecar emits two `SpeakerLabelingProgress` updates at 25% and 75% sidecar-local. Assert the outer `IProgress<ProgressUpdate>` received two `Diarizing`-stage updates whose percent lies in [85, 95].
18. **Green.** Implement the mapping helper.
19. **Red.** `EnrichAsync_MergeServiceReturnsEmpty_ProducesWarning`. Fake sidecar returns an empty `DiarizationResult`; merge produces an empty `TranscriptDocument`. Assert `result.Document` is **not** null but has zero speakers, and `result.Warnings` contains `"speaker-labeling: diarization returned zero speakers"`. This guards the contract that merge failures do not crash — they are reported as warnings while still returning the (empty) document for debugging.
20. **Green.** Add the post-merge sanity check.
21. **Refactor.** Extract warning formatting into a private `FormatWarning(SidecarFailureReason, string)` method; ensure every reason enum value has a mapping.

**Local verification:**
```
dotnet test VoxFlow.sln
```

**PR description template:**
```
Add SpeakerEnrichmentService orchestrator

Composes IPythonRuntime, IDiarizationSidecar, and ISpeakerMergeService
into a single EnrichAsync call. Owns runtime readiness, venv bootstrap,
timeouts, cancellation, sidecar failure taxonomy, and progress mapping.
Sidecar failures never crash the pipeline — they surface as warnings
with a null Document. Fully unit-tested against fake runtime, bootstrapper,
and sidecar; no real process spawned in tests.

Test plan:
- [x] dotnet test VoxFlow.sln — green
```

---

### P1.4 — Pipeline wiring in `TranscriptionService`

**Branch:** `speaker-labeling/p1.4-pipeline-wiring`

**Why fourth:** Now that `ISpeakerEnrichmentService` exists, wiring it into [TranscriptionService.TranscribeFileAsync](../../../src/VoxFlow.Core/Services/TranscriptionService.cs) is a small, surgical change. The PR stays narrow: it does not touch output writers or the `.voxflow.json` artifact (P1.5 and P1.6). It only threads the enrichment result through the result record and plumbs the new progress stage.

**Files touched (modified):**
- `src/VoxFlow.Core/Services/TranscriptionService.cs` — add an injected `ISpeakerEnrichmentService`, inject `TranscriptionOptions` into `TranscribeFileAsync` as before, compute the effective `enabled` flag (`request.EnableSpeakers ?? options.SpeakerLabeling.Enabled`), call `EnrichAsync` between steps 7 (write output) and 8 (build preview) — **no**, between steps 6 and 7: enrichment must happen *before* the output is written so the writers in P1.5 can pick it up. Add the returned document and warnings to the result record.
- `src/VoxFlow.Core/Models/TranscribeFileResult.cs` — add `TranscriptDocument? SpeakerTranscript` and `IReadOnlyList<string> EnrichmentWarnings` as **trailing positional record parameters with defaults** (`null` and `Array.Empty<string>()`). Existing call sites keep compiling.
- `src/VoxFlow.Core/Models/ProgressUpdate.cs` — add `Diarizing` to the `ProgressStage` enum, positioned between `Filtering` and `Writing`. This is an additive enum change with no wire-format break.
- `src/VoxFlow.Core/DependencyInjection/ServiceCollectionExtensions.cs` (or wherever `AddVoxFlowCore` lives) — register `ISpeakerEnrichmentService`, the sidecar client, runtime, and bootstrapper in the composition root. Resolve `IPythonRuntime` based on `options.SpeakerLabeling.PythonRuntimeMode` via a factory delegate (`SystemPython` → `SystemPythonRuntime`, `ManagedVenv` → `ManagedVenvRuntime`, `Standalone` → throw `NotSupportedException("Standalone runtime lands in Phase 3")`).
- `tests/VoxFlow.Core.Tests/TranscriptionServiceTests.cs` — new test section for speaker-labeling branches.

**TDD steps:**

1. **Red.** `TranscriptionServiceTests.TranscribeFileAsync_DisabledConfig_AndNullOverride_DoesNotCallEnrichment`. Fake `ISpeakerEnrichmentService` that records calls; assert count is zero. Assert the returned `TranscribeFileResult.SpeakerTranscript is null` and `EnrichmentWarnings` is empty. Compile fails: `SpeakerTranscript`/`EnrichmentWarnings` don't exist.
2. **Green.** Add the two new trailing record parameters with default values. Wire `TranscriptionService` to call `_enrichment.EnrichAsync` only when `effectiveEnabled == true`. For disabled, leave the defaults. Test passes.
3. **Red.** `TranscribeFileAsync_EnabledViaConfig_CallsEnrichment_AndPropagatesDocument`. Load options with `enabled=true`; fake enrichment returns a document; assert the result carries the same document.
4. **Green.** Pass the document through. Test passes.
5. **Red.** `TranscribeFileAsync_EnabledViaConfig_ButRequestOverrideFalse_DoesNotCallEnrichment`. Request has `EnableSpeakers=false`, config has `enabled=true`; assert the enrichment fake was never called.
6. **Green.** Add the `request.EnableSpeakers ?? options.SpeakerLabeling.Enabled` line.
7. **Red.** `TranscribeFileAsync_DisabledViaConfig_ButRequestOverrideTrue_CallsEnrichment`. Mirror case.
8. **Green.** Already covered by the same line.
9. **Red.** `TranscribeFileAsync_EnrichmentReturnsWarnings_AreAppendedToResultWarnings`. Fake returns two enrichment warnings; assert both appear in `result.Warnings` AND in `result.EnrichmentWarnings`. (`Warnings` is the cross-pipeline bag; `EnrichmentWarnings` is a typed subset for the writers that want to filter by source.)
10. **Green.** Append + populate both lists.
11. **Red.** `TranscribeFileAsync_EnrichmentThrows_IsWrappedAsWarning_AndPipelineStillSucceeds`. This is a defence-in-depth test — `ISpeakerEnrichmentService` is contracted not to throw, but `TranscriptionService` must treat any thrown exception as a warning and continue so that a future bug in the enrichment service cannot take down the transcription pipeline.
12. **Green.** Wrap the call in try/catch, format the warning as `"speaker-labeling: internal error: {message}"`.
13. **Red.** `TranscribeFileAsync_ReportsProgressStageDiarizing_BetweenTranscribingAndWriting`. Assert the progress reporter received a `Diarizing` update with percent in [85, 95]. Ordering check: `Transcribing` → `Diarizing` → `Writing` → `Complete`.
14. **Green.** Emit the progress bracket around the enrichment call.
15. **Red.** Regression guard `TranscribeFileAsync_DisabledPath_ProducesByteIdenticalOutputAsPrePhase1`. Snapshot-compare against a pre-Phase-1 golden output file committed under `tests/goldens/transcribe-file-result-disabled.json`. The golden is captured before P1.4 lands by running the tests on the parent commit and checked in as part of this PR.
16. **Green.** Confirm test passes without touching the disabled path.
17. **Red.** `ServiceCollectionExtensionsTests.AddVoxFlowCore_RegistersSpeakerEnrichmentService`. Build a provider, resolve `ISpeakerEnrichmentService`, assert non-null. Assert resolving `IPythonRuntime` for each `PythonRuntimeMode` returns the expected concrete type (using an in-memory `TranscriptionOptions` seeded via the composition root's override hook).
18. **Green.** Register the services with the correct runtime factory.
19. **Refactor.** Extract `TranscriptionService.ComputeEffectiveSpeakerFlag(request, options)` into a single private static helper so the override rule has exactly one implementation.

**Local verification:**
```
dotnet test VoxFlow.sln
dotnet test VoxFlow.sln --filter "Category=RequiresPython"   # optional: only runs real sidecar when Python 3.10+ present
```

**PR description template:**
```
Wire SpeakerEnrichmentService into TranscriptionService

Calls enrichment between language selection and output writing when the
effective speaker flag is true (request.EnableSpeakers ?? options.SpeakerLabeling.Enabled).
Propagates the resulting TranscriptDocument and warnings through
TranscribeFileResult via new trailing record fields (defaults preserve
every existing call site). Adds ProgressStage.Diarizing between
Transcribing and Writing. Disabled path is byte-identical to pre-Phase-1,
verified by a golden-output regression test.

Test plan:
- [x] dotnet test VoxFlow.sln — green
- [x] dotnet test VoxFlow.sln --filter "Category=RequiresPython" — green locally
```

---

### P1.5 — Output writer speaker rendering

**Branch:** `speaker-labeling/p1.5-output-writers`

**Why fifth:** Rendering is purely about presentation and can happen once the pipeline delivers a `TranscriptDocument`. Splitting it from P1.4 keeps each PR small and lets the writer work land without touching the pipeline again.

**Files touched (modified):**
- `src/VoxFlow.Core/Models/TranscriptOutputContext.cs` — add `TranscriptDocument? SpeakerTranscript` as a trailing init-only property with default `null`.
- `src/VoxFlow.Core/Services/TranscriptionService.cs` — pass `outputContext with { SpeakerTranscript = enrichmentResult.Document }` into the existing `_outputWriter.WriteAsync` call. One-line change, no behavioral shift when `Document` is null.
- `src/VoxFlow.Core/Services/Formatters/TxtTranscriptFormatter.cs` — when `context.SpeakerTranscript is not null`, render each `SpeakerTurn` as `Speaker {Label}: {text}` on its own line; when null, render the current segment-based format unchanged.
- `src/VoxFlow.Core/Services/Formatters/MdTranscriptFormatter.cs` — mirror the same rule using `**Speaker {Label}:**` markdown emphasis.
- `src/VoxFlow.Core/Services/Formatters/SrtTranscriptFormatter.cs` — prepend `Speaker {Label}: ` to the cue text of the first word of each turn; timing cues are still segment-based, not turn-based (we do not rewrite the cue boundaries — the subtitle track stays compatible with existing player expectations). When `SpeakerTranscript` is null, the formatter's output is unchanged.
- `src/VoxFlow.Core/Services/Formatters/VttTranscriptFormatter.cs` — same rule as SRT but using `<v Speaker A>` voice tags when a `TranscriptDocument` is present (standard WebVTT speaker syntax); fallback unchanged.
- `src/VoxFlow.Core/Services/Formatters/JsonTranscriptFormatter.cs` — when `SpeakerTranscript` is not null, emit the full `TranscriptDocument` as the top-level object alongside the existing segments array; when null, emit the current segment-based shape unchanged. The shape follows `voxflow-transcript-v1` schema, not a new shape.
- `tests/VoxFlow.Core.Tests/Formatters/*.cs` — add one "with speakers" test per formatter alongside existing tests; keep every existing test unchanged.

**TDD steps (one formatter at a time — five mini-cycles):**

For each formatter in the order `Txt`, `Md`, `Json`, `Srt`, `Vtt`:

1. **Red.** `<Formatter>Tests.Format_WithTwoSpeakerDocument_RendersSpeakerPrefixes`. Construct a `TranscriptDocument` with two speakers and 4 words → 2 turns. Call the formatter. Assert output contains `Speaker A:` (or format-specific equivalent) and `Speaker B:` in the expected positions. Compile passes (context field already exists from the first sub-step below), assertion fails.
2. **Green.** Implement the speaker-aware branch in the formatter. The branch is gated on `context.SpeakerTranscript is not null`; otherwise the formatter's existing body runs untouched.
3. **Red.** `<Formatter>Tests.Format_WithNullSpeakerTranscript_ProducesLegacyOutputUnchanged`. Same input, `context.SpeakerTranscript is null`. Byte-compare against a golden string captured before this PR. Must pass — the regression guard.
4. **Green.** Should already pass. If it doesn't, the branch is leaking into the disabled path and needs to be restructured.

The `TranscriptOutputContext.SpeakerTranscript` field is added as the first sub-step of the whole PR (before the per-formatter cycles) so every formatter test compiles. That single-line model change does not need its own TDD cycle beyond a compile check.

**Blast radius — existing formatter tests:**
- `TxtTranscriptFormatterTests`, `MdTranscriptFormatterTests`, `JsonTranscriptFormatterTests`, `SrtTranscriptFormatterTests`, `VttTranscriptFormatterTests`, `OutputWriterTests`.

All of these must stay green without edits. The golden-output regression tests in step 3 above are the explicit check. If any existing test needs a touch-up, the PR is wrong.

**Local verification:**
```
dotnet test VoxFlow.sln
```

**PR description template:**
```
Render speaker labels from TranscriptDocument in output writers

All five formatters (.txt, .srt, .vtt, .json, .md) now emit
"Speaker A:" / "Speaker B:" prefixes when TranscriptOutputContext
carries a non-null TranscriptDocument. Disabled path (null document)
produces byte-identical legacy output, verified per-formatter against
golden strings. JSON formatter emits the full voxflow-transcript-v1
shape when speakers are present.

Test plan:
- [x] dotnet test VoxFlow.sln — green (no existing formatter test touched)
```

---

### P1.6 — `.voxflow.json` artifact

**Branch:** `speaker-labeling/p1.6-voxflow-json`

**Why sixth:** The `.voxflow.json` artifact is a separate sidecar file, not a replacement for the primary output. Isolating it into its own PR keeps the blast radius small: only the writer pipeline changes, not any existing formatter.

**Files touched (new):**
- `src/VoxFlow.Core/Services/VoxflowTranscriptArtifactWriter.cs` — serializes a `TranscriptDocument` to `{resultPath}.voxflow.json` using the schema-compatible shape.
- `src/VoxFlow.Core/Interfaces/IVoxflowTranscriptArtifactWriter.cs`
- `tests/VoxFlow.Core.Tests/Services/VoxflowTranscriptArtifactWriterTests.cs`

**Files touched (modified):**
- `src/VoxFlow.Core/Services/TranscriptionService.cs` — after the main output write, if `enrichmentResult.Document is not null`, also call the artifact writer. Single new conditional statement.
- `src/VoxFlow.Core/DependencyInjection/ServiceCollectionExtensions.cs` — register the writer as `IVoxflowTranscriptArtifactWriter`.
- `docs/contracts/voxflow-transcript-v1.schema.json` — already added in P0.2; no change expected, but add a test that round-trips a realistic 3-speaker document against the schema to catch any drift.

**TDD steps:**

1. **Red.** `VoxflowTranscriptArtifactWriterTests.WriteAsync_WithDocument_WritesFileAtExpectedPath`. Given `resultPath="/tmp/out.txt"`, assert file exists at `/tmp/out.txt.voxflow.json`. Compile fails: type doesn't exist.
2. **Green.** Create the writer. Path rule: append `.voxflow.json` to the caller's result path (even if the result path ends in `.txt` / `.srt` / etc.). This keeps the `.voxflow.json` paired with whatever primary format was selected.
3. **Red.** `WriteAsync_RoundTrip_ProducesEqualDocument`. Serialize, read back via `System.Text.Json`, assert equality.
4. **Green.** Use the same `JsonSerializerOptions` from `JsonTranscriptFormatter`: camelCase, indented, ignore nulls. Test passes.
5. **Red.** `WriteAsync_ValidatesAgainstVoxflowTranscriptSchema`. Write, read, validate with NJsonSchema. Must pass the schema committed in P0.2.
6. **Green.** If schema and writer diverge, fix whichever is wrong — schema takes precedence (`v1` is frozen once Phase 0 ships).
7. **Red.** `WriteAsync_Cancelled_DoesNotLeavePartialFile`. Cancel the token mid-write; assert the `.voxflow.json` file does not exist (or at most an empty file is cleaned up).
8. **Green.** Write to a temp file + atomic rename (`File.Move` with overwrite).
9. **Red.** `TranscriptionServiceTests.TranscribeFileAsync_EnabledWithDocument_WritesVoxflowJsonArtifact`. Run the service with a fake enrichment returning a document; assert the writer was called once with the expected path.
10. **Green.** Wire the single conditional call in `TranscriptionService`.
11. **Red.** `TranscribeFileAsync_EnabledWithNullDocument_DoesNotWriteArtifact`. Enrichment returns a null document (e.g. sidecar failure); assert the writer was NOT called.
12. **Green.** Already handled by the conditional.
13. **Red.** `TranscribeFileAsync_DisabledPath_DoesNotWriteArtifact`. Regression guard.
14. **Green.** Already covered.

**Local verification:**
```
dotnet test VoxFlow.sln
```

**PR description template:**
```
Write .voxflow.json transcript artifact

New VoxflowTranscriptArtifactWriter produces a sidecar JSON file at
{resultPath}.voxflow.json whenever the enrichment pipeline returns a
TranscriptDocument. Schema-validated against voxflow-transcript-v1.
Atomic temp-file + rename for cancellation safety. No effect on the
primary output file. Disabled path (enabled=false or null document)
writes no artifact — verified by regression tests.

Test plan:
- [x] dotnet test VoxFlow.sln — green
```

---

### P1.7 — `IValidationService` preflight for speaker labeling

**Branch:** `speaker-labeling/p1.7-validation`

**Why seventh:** Preflight surfaces "your setup is not ready for speaker labeling" errors before the pipeline runs, which is valuable but not load-bearing — the pipeline already degrades gracefully when the runtime is unavailable. Isolating validation into its own PR keeps the earlier PRs pure pipeline changes.

**Files touched (modified):**
- `src/VoxFlow.Core/Services/ValidationService.cs` — add `CheckSpeakerLabelingPrerequisitesAsync` that runs only when `options.SpeakerLabeling.Enabled==true`, returns a sequence of `ValidationCheck` entries, and is appended to the existing `Checks` list. Gated by the existing `options.StartupValidation.Enabled` switch so users can turn preflight off entirely.
- `src/VoxFlow.Core/Configuration/TranscriptionOptions.cs` — extend `StartupValidationOptions` and its configuration DTO with a new `bool CheckSpeakerLabelingRuntime { get; }` that defaults to `true` when absent. Mirror in `appsettings.json` and `appsettings.example.json`.
- `tests/VoxFlow.Core.Tests/Services/ValidationServiceTests.cs` — new test section.

**Check semantics:**

1. Runtime-ready check: call `IPythonRuntime.GetStatusAsync`. If `IsReady`, record an info-level success check. If not, record a warning (pipeline still runs, but enrichment will self-report).
2. Model-cache check: inspect the pyannote cache directory (`~/Library/Caches/VoxFlow/models/`). If the configured `modelId` is not cached, record a warning — "first run will download ~300 MB". This is informational, not blocking.
3. Both checks contribute to `ValidationResult.Checks` via the existing `ValidationCheck` record with `ValidationCheckStatus.Warning` or `.Passed`. Neither blocks startup — the pipeline is resilient by design.

**TDD steps:**

1. **Red.** `ValidationServiceTests.ValidateAsync_SpeakerLabelingDisabled_DoesNotRunSpeakerChecks`. Fake runtime throws if called; `options.SpeakerLabeling.Enabled=false`. Assert no checks mention speaker labeling.
2. **Green.** Gate the new checks on the effective flag.
3. **Red.** `ValidateAsync_Enabled_RuntimeReady_AddsPassedCheck`.
4. **Green.** Emit `ValidationCheck(Name="Speaker labeling runtime", Status=Passed, Details=$"Python {version} ready")`.
5. **Red.** `ValidateAsync_Enabled_RuntimeNotReady_AddsWarningCheck`. Assert status is `Warning` (not `Failed`) and `CanStart` is unaffected.
6. **Green.** Map `PythonRuntimeStatus.NotReady` to a warning check.
7. **Red.** `ValidateAsync_Enabled_ModelNotCached_AddsInformationalWarning`. Fake `IFileSystem` (add a minimal wrapper if none exists) reports the cache directory is missing.
8. **Green.** Add the cache-path probe. Use an injectable helper so tests don't touch the real filesystem.
9. **Red.** `ValidateAsync_Enabled_StartupValidationGloballyDisabled_SkipsSpeakerChecks`. Sets `StartupValidation.Enabled=false`; assert no speaker-labeling checks added, matching existing behavior for every other check.
10. **Green.** Respect the existing gate.
11. **Red.** `ValidateAsync_SpeakerChecks_DoNotAffectCanStart_EvenOnFailure`. Multiple warnings in a row; `ValidationResult.CanStart` must remain `true` because none of the new checks are marked `Failed`.
12. **Green.** Covered by the warning-only mapping.

**Local verification:**
```
dotnet test VoxFlow.sln
```

**PR description template:**
```
Add speaker-labeling preflight checks to ValidationService

When transcription.speakerLabeling.enabled is true AND startupValidation
is on, ValidationService probes IPythonRuntime.GetStatusAsync and the
pyannote model cache directory. Both emit informational warnings on
misconfiguration — never Failed — so the pipeline remains resilient
even if preflight misreports. New startupValidation.checkSpeakerLabelingRuntime
toggle (default true) for users who want to disable the probe explicitly.

Test plan:
- [x] dotnet test VoxFlow.sln — green
```

---

## Post-Phase-1 Checklist

Before declaring Phase 1 complete and moving to Phase 2 planning:

- [ ] All 7 sub-PRs merged into `Local-Speaker-Labeling`.
- [ ] `dotnet test VoxFlow.sln` on `Local-Speaker-Labeling` is fully green.
- [ ] `dotnet test --filter "Category=RequiresPython"` is fully green on a developer machine with Python 3.10+ + pyannote installed (runs the real orchestrator end-to-end against the P0.8 fixtures through `TranscriptionService`).
- [ ] Disabled-path regression: running the existing Desktop headless test suite (`VoxFlow.Desktop.Tests`) produces the same results as before Phase 1 — no test required edits, golden outputs unchanged.
- [ ] Manual smoke: enable `speakerLabeling.enabled=true` locally, run the CLI against `artifacts/input/President Obama Speech.m4a`, confirm `Speaker A:` prefixes appear in the output file and a `.voxflow.json` sidecar is written.
- [ ] Manual smoke: set `enabled=true` but force a sidecar failure (point `modelId` at a non-existent model) and confirm the transcript still writes successfully, the warnings list contains a `speaker-labeling:` entry, and no `.voxflow.json` artifact appears.
- [ ] `docs/runbooks/speaker-labeling.md` has a stub committed (full content lands in Phase 3, but a placeholder reserves the path and links back to this phase doc for context).
- [ ] No files under `src/VoxFlow.Desktop`, `src/VoxFlow.Cli`, or `src/VoxFlow.McpServer` touch speaker labeling yet — those are Phase 2 and Phase 3.
- [ ] User has reviewed the integration branch state and approved starting Phase 2.
