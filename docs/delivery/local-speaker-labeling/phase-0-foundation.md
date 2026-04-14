# Phase 0 — Foundation

**Parent:** [Local Speaker Labeling — Delivery Plan](README.md)
**ADR:** [ADR-024](../../adr/024-local-speaker-labeling-pipeline.md)
**Status:** Planned

---

## Goal

Build every component required by Phase 1 **except** the orchestration that connects them. At the end of Phase 0, the code for merging, diarization, Python-runtime management, and the data model exists and is fully unit-tested — but nothing is wired into the main transcription pipeline and the user sees zero change.

Phase 0 is deliberately front-loaded with pure-logic work so that the riskiest piece (process boundaries and Python runtime) is tackled last, after the merge algorithm has been proven correct against fixtures that do not require Python at all.

## Exit Criteria

- `WhisperToken[]` is preserved through `TranscriptionFilter` with no behavioral change to the existing pipeline.
- `TranscriptDocument` record and supporting types exist with round-trip JSON serialization tests.
- `docs/contracts/sidecar-diarization-v1.schema.json` exists and is schema-validated in tests.
- `IPythonRuntime` abstraction exists with `SystemPythonRuntime` and `ManagedVenvRuntime` implementations, both unit-tested via a mocked process launcher.
- `voxflow_diarize.py` sidecar script exists, runs standalone, and honors the JSON contract.
- `PyannoteSidecarClient` exists and is contract-tested against fixture JSON plus integration-tested against the real Python script. Integration tests live in `VoxFlow.Core.Tests` tagged with `[Trait("Category", "RequiresPython")]` — xUnit 2.9 trait, filterable via `dotnet test --filter "Category=RequiresPython"`. No separate integration-test project is created (the `VoxFlow.EndToEndTests` folder in the repo is dead output, not a real project).
- `SpeakerMergeService` exists with ≥15 unit tests covering every merge rule from ADR-024.
- Three audio fixtures exist in `tests/fixtures/sidecar/audio/` and are referenced by at least one integration test each.
- `python-build-standalone` spike has a documented go/no-go outcome (parallel, non-blocking).
- `dotnet test` is fully green locally (both with and without `RequiresPython` filter).
- Nothing user-visible has changed. `speakerLabeling` section does not yet exist in `appsettings.json`.

## Pre-conditions

- `Local-Speaker-Labeling` branch checked out and up to date with its current state.
- Python 3.10+ available locally (for developer to run `RequiresPython` tests).
- `ffmpeg` available locally (for trimming the Obama audio fixture).

---

## TDD Sequence

Eight sub-PRs, in strict order. Each PR is the smallest unit that leaves the integration branch in a green-tests state.

Conventions for every PR below:
- **Branch:** `speaker-labeling/pN.M-<slug>` off `Local-Speaker-Labeling`.
- **Base for PR:** `Local-Speaker-Labeling`.
- **Before `gh pr create`:** run `dotnet test` locally; if the PR touches integration code also run `dotnet test --filter "Category=RequiresPython"`. Both must be fully green.
- **Test tagging:** Python-gated tests use `[Trait("Category", "RequiresPython")]` on the test class. xUnit 2.9.2 does not recognize MSTest's `[Category(...)]` nor `Assert.Inconclusive(...)`; those must not appear in any test added here.
- **Commit authorship:** user only; no Co-Authored-By trailers.
- **PR body:** no "Generated with Claude Code" footer.

---

### P0.1 — Preserve `WhisperToken[]` through `TranscriptionFilter`

**Branch:** `speaker-labeling/p0.1-preserve-tokens`

**Why first:** Every downstream component needs word-level timing. Without this change, there is nothing to merge with diarization output. This is the smallest possible change that unblocks everything else.

**Files touched:**
- `src/VoxFlow.Core/Models/FilteredSegment.cs` — add `Words` as a **new trailing positional parameter** with a default of `Array.Empty<WhisperToken>()` (or an `IReadOnlyList<WhisperToken>` with `[]` default via the record primary-constructor syntax). The default is load-bearing: it keeps every existing positional `new FilteredSegment(start, end, text, probability)` call-site compiling without edits.
- `src/VoxFlow.Core/Services/TranscriptionFilter.cs` — pass `segment.Tokens ?? Array.Empty<WhisperToken>()` as the fifth argument when constructing `FilteredSegment` around line 45.
- `tests/VoxFlow.Core.Tests/TranscriptionFilterTests.cs` — new tests that assert `Words` is populated on the accepted segments (these are the only new tests in the PR).

**Blast radius — other test files that construct `FilteredSegment` positionally:**
- `tests/VoxFlow.Core.Tests/OutputWriterTests.cs` (multiple sites around lines 21, 39, 66–68, 88, 109)
- `tests/VoxFlow.Core.Tests/TranscriptFormatterTests.cs` (lines 15–16)
- `tests/VoxFlow.Core.Tests/LanguageSelectionDecisionTests.cs` (~line 165)
- `tests/VoxFlow.Core.Tests/BatchTranscriptionServiceTests.cs` (~line 289)

Because `Words` is added as a trailing parameter *with a default*, these four files do **not** need to be touched in P0.1 and their existing assertions stay unchanged. Verifying this is the PR's self-check: `dotnet build` must succeed with no edits to those files. If any of them needs changes, the default is wrong and the PR should be rethought before landing.

**TDD steps:**

1. **Red.** Add `TranscriptionFilterTests.FilterSegments_PreservesWordTokens_FromAcceptedSegments`. Construct a `SegmentData` whose `Tokens` array has three `WhisperToken` entries with known timing. Call `FilterSegments`. Assert `result.Accepted[0].Words.Count == 3` and that each token's `Text` and timing match. Compile: fails because `FilteredSegment.Words` doesn't exist.
2. **Green.** Add `Words` as a trailing positional parameter on `FilteredSegment` with default `Array.Empty<WhisperToken>()`. Update `TranscriptionFilter.FilterSegments` line ~45 to pass `segment.Tokens ?? Array.Empty<WhisperToken>()` (guarding against a null `Tokens` array). Do **not** touch `OutputWriterTests`, `TranscriptFormatterTests`, `LanguageSelectionDecisionTests`, or `BatchTranscriptionServiceTests` — the default makes their positional constructor calls compile unchanged. Tests pass.
3. **Red.** Add `TranscriptionFilterTests.FilterSegments_ExcludesSkippedSegments_TokensStillAttachedOnAcceptedOnes`. Two segments: one accepted, one skipped. Assert the accepted one still carries its tokens and the filter's skipped-segment behavior is unchanged. Compile: passes to compile, may fail assertion.
4. **Green.** Should already pass if step 2 was correct — this is a guard test that the refactor didn't move tokens to the wrong place.
5. **Red.** Add `TranscriptionFilterTests.FilterSegments_DuplicateLoopFilter_DropsTokensAlongsideSegment`. Two identical segments beyond the duplicate-loop threshold; tokens should disappear with the skipped duplicate. Assert the filtered list has the expected tokens and the dropped one isn't attached to anything.
6. **Green.** Same as step 4 — verifies no regression.
7. **Refactor.** Review `TranscriptionFilter.FilterSegments`: the only change should be a single line (adding `segment.Tokens`). No new branches, no new methods. If anything else got touched, revert it.

**Local verification before PR:**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
```
All tests green. `TranscriptionFilterTests` is the only file with *new* tests. The four files listed in Blast radius above must still compile and pass with no edits — if any of them fails to build, the default value on `Words` is wrong and needs to be revisited before landing.

**PR description template:**
```
Preserve WhisperToken[] through TranscriptionFilter

Prerequisite for speaker labeling (ADR-024 Phase 0).
FilteredSegment now carries word-level timing tokens from Whisper.net
instead of discarding them. No behavioral change to existing pipeline;
output byte-identical for the same input.

Test plan:
- [x] dotnet test locally — full suite green
- [x] Added TranscriptionFilter tests for token preservation across
      accepted/skipped/duplicate-loop code paths
```

---

### P0.2 — `TranscriptDocument` model + sidecar contract schema

**Branch:** `speaker-labeling/p0.2-transcript-doc`

**Why second:** The model is the public shape that merge, output writers, and hosts all consume. It must exist and be stable before anything else is built on top of it. The sidecar schema is bundled into this PR because it's the paired artifact on the process-boundary side and has no runtime dependencies.

**Files touched (new):**
- `src/VoxFlow.Core/Models/TranscriptDocument.cs`
- `src/VoxFlow.Core/Models/SpeakerInfo.cs`
- `src/VoxFlow.Core/Models/TranscriptWord.cs`
- `src/VoxFlow.Core/Models/SpeakerTurn.cs`
- `src/VoxFlow.Core/Models/TranscriptMetadata.cs`
- `docs/contracts/sidecar-diarization-v1.schema.json`
- `docs/contracts/voxflow-transcript-v1.schema.json`
- `docs/delivery/local-speaker-labeling/contracts/` — symlinks or copies pointing to the above (optional convenience).
- `tests/VoxFlow.Core.Tests/Models/TranscriptDocumentTests.cs`
- `tests/VoxFlow.Core.Tests/Models/SpeakerTurnTests.cs`

**TDD steps:**

1. **Red.** `TranscriptDocumentTests.Construct_WithSpeakersAndWords_ExposesAllFields`. Create a document with 2 speakers, 4 words. Assert property access works and collections are non-null. Compile: fails, types don't exist.
2. **Green.** Create `TranscriptDocument`, `SpeakerInfo`, `TranscriptWord`, `SpeakerTurn`, `TranscriptMetadata` as `sealed record` types matching the shapes described in [README.md](README.md) and [ADR-024](../../adr/024-local-speaker-labeling-pipeline.md). No logic yet, just constructors. Test passes.
3. **Red.** `TranscriptDocumentTests.Serialize_RoundTrip_ProducesEqualDocument`. Use `System.Text.Json` to serialize and deserialize a fully-populated document. Assert equality. Fails if naming or attribute setup is wrong.
4. **Green.** Add JSON attribute configuration as needed. Tests pass.
5. **Red.** `SpeakerTurnTests.GroupConsecutive_TwoSpeakers_ProducesTwoTurns`. Take a list of 6 `TranscriptWord` (3 speaker A, 3 speaker B, in that order) and a helper `SpeakerTurn.GroupConsecutive(words)`. Assert exactly 2 turns with correct boundaries.
6. **Green.** Implement `GroupConsecutive` as a static helper on `SpeakerTurn`. This is pure function and will be used by merge service in P0.7.
7. **Red.** `SpeakerTurnTests.GroupConsecutive_AlternatingSpeakers_ProducesOneTurnPerWord`. A-B-A-B-A-B → 6 turns.
8. **Green.** Implementation should already handle this. If it doesn't, fix.
9. **Red.** `SpeakerTurnTests.GroupConsecutive_SingleSpeaker_ProducesOneTurn`.
10. **Green.** Already handled.
11. **Red.** `SpeakerTurnTests.GroupConsecutive_EmptyInput_ProducesEmptyList`.
12. **Green.** Handle edge case explicitly.
13. **Red.** `TranscriptDocumentTests.ValidatesAgainstVoxflowTranscriptSchema`. Load `docs/contracts/voxflow-transcript-v1.schema.json`, serialize a sample document, validate with `NJsonSchema` (add package reference if not present). Initial test fails: schema file is empty.
14. **Green.** Write the JSON Schema file by inspecting the .NET record shapes. Include `$schema`, `$id`, required/optional marking, type constraints. Test passes.
15. **Red.** Create an empty `sidecar-diarization-v1.schema.json` and a test `SidecarContractTests.ValidResponse_ValidatesAgainstSchema` with a hand-written valid sample. Fails: schema empty.
16. **Green.** Write the sidecar schema. Cover request envelope (`version`, `wavPath`) and response envelope (`version`, `status`, `error?`, `speakers`, `segments`). Test passes.
17. **Red.** `SidecarContractTests.InvalidResponse_MissingVersion_FailsSchema`. Sample with no version. Expect schema validation failure.
18. **Green.** Already handled if `version` is marked required in schema.

**Local verification:**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
```

**PR description template:**
```
Add TranscriptDocument model + sidecar JSON schemas

Speaker-aware transcript model for ADR-024 Phase 0. Pure additions:
no existing types touched, nothing wired in yet. Model plus two
versioned JSON Schema files for the sidecar contract and the
.voxflow.json artifact. Round-trip and schema-validation unit tests.

Test plan:
- [x] dotnet test — full suite green
- [x] New tests in Models/TranscriptDocumentTests, Models/SpeakerTurnTests,
      Models/SidecarContractTests
```

---

### P0.3 — `IPythonRuntime` + `SystemPythonRuntime`

**Branch:** `speaker-labeling/p0.3-python-runtime`

**Why third:** First piece that touches the process boundary. `SystemPythonRuntime` is the simplest implementation (just find `python3` in PATH) and unblocks local development for later PRs where we need to actually run a Python script.

**Files touched (new):**
- `src/VoxFlow.Core/Interfaces/IPythonRuntime.cs`
- `src/VoxFlow.Core/Interfaces/IProcessLauncher.cs` — thin wrapper over `System.Diagnostics.Process`, injectable for testing.
- `src/VoxFlow.Core/Services/Python/SystemPythonRuntime.cs`
- `src/VoxFlow.Core/Services/Python/DefaultProcessLauncher.cs`
- `src/VoxFlow.Core/Services/Python/PythonRuntimeStatus.cs` — result type `{ IsReady, InterpreterPath, Version, Error? }`.
- `tests/VoxFlow.Core.Tests/Services/Python/SystemPythonRuntimeTests.cs`
- `tests/VoxFlow.Core.Tests/Services/Python/FakeProcessLauncher.cs` — test double.

**Interface shape:**
```csharp
public interface IPythonRuntime
{
    Task<PythonRuntimeStatus> GetStatusAsync(CancellationToken ct);
    ProcessStartInfo CreateStartInfo(string scriptPath, IEnumerable<string> arguments);
}
```

**TDD steps:**

1. **Red.** `SystemPythonRuntimeTests.GetStatus_PythonInPath_ReturnsReady`. Construct `SystemPythonRuntime` with `FakeProcessLauncher` that returns `Python 3.11.5` for `python3 --version`. Assert `IsReady=true`, `Version="3.11.5"`.
2. **Green.** Create `IPythonRuntime`, `SystemPythonRuntime`. Implement `GetStatusAsync` using the launcher. Parse "Python 3.x.y" format.
3. **Red.** `GetStatus_PythonNotFound_ReturnsNotReady`. Launcher throws "command not found". Assert `IsReady=false`, `Error` non-null, no crash.
4. **Green.** Handle exception, map to `PythonRuntimeStatus.NotReady(error)`.
5. **Red.** `GetStatus_PythonTooOld_ReturnsNotReady`. Launcher returns "Python 3.8.10". Assert `IsReady=false`, error mentions minimum version.
6. **Green.** Parse version, compare against 3.10 minimum, reject.
7. **Red.** `CreateStartInfo_ValidInputs_ProducesRunnableProcessInfo`. Assert `FileName="python3"`, `Arguments` contains script path and passed arguments, `RedirectStandardInput/Output/Error=true`, `UseShellExecute=false`.
8. **Green.** Implement `CreateStartInfo`. Straightforward.
9. **Red.** `GetStatus_Cancelled_ThrowsOperationCanceled`. Launcher never returns. Cancel token. Assert `OperationCanceledException`.
10. **Green.** Forward the token to `FakeProcessLauncher`; throw on cancellation.
11. **Refactor.** Extract version parsing into a private helper, simplify the constructor, ensure all public members have XML docs.

**Local verification:**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
```
No `RequiresPython` tests yet.

**PR description template:**
```
Add IPythonRuntime abstraction and SystemPythonRuntime

First piece of the speaker-labeling Python sidecar plumbing (ADR-024
Phase 0). IPythonRuntime decouples the .NET side from the packaging
strategy. SystemPythonRuntime is the dev/CI escape hatch: resolves
python3 from PATH, validates version >= 3.10, returns a usable
ProcessStartInfo. Fully unit-tested with a fake process launcher;
no real process spawned by tests.

Test plan:
- [x] dotnet test — green
```

---

### P0.4 — `ManagedVenvRuntime`

**Branch:** `speaker-labeling/p0.4-managed-venv`

**Why fourth:** This is the default runtime for Phase 1 users. It's heavier than `SystemPythonRuntime` — needs to bootstrap a venv, install deps, cache the result — so it comes after the simpler implementation.

**Files touched (new):**
- `src/VoxFlow.Core/Services/Python/ManagedVenvRuntime.cs`
- `src/VoxFlow.Core/Services/Python/IVenvPaths.cs` — abstraction over filesystem paths so tests can inject a temp dir.
- `src/VoxFlow.Core/Services/Python/DefaultVenvPaths.cs` — real impl using `~/Library/Application Support/VoxFlow/python-runtime/`.
- `src/VoxFlow.Core/Services/Python/VenvRequirements.cs` — embedded resource or constant string with the pip requirements.
- `tests/VoxFlow.Core.Tests/Services/Python/ManagedVenvRuntimeTests.cs`
- `tests/VoxFlow.Core.Tests/Services/Python/FakeVenvPaths.cs`
- `src/VoxFlow.Core/Resources/python-requirements.txt` — pinned versions of pyannote.audio, torch, torchaudio.

**TDD steps (high level — each red/green cycle follows the same shape as P0.3):**

1. `GetStatus_VenvNotYetCreated_ReturnsNotReady_WithCreateHint`. 
2. `CreateVenv_FreshDirectory_CallsPythonVenvCreate`. Launcher records the commands issued; assert `python3 -m venv <path>` was the first call.
3. `CreateVenv_InstallsRequirements_AfterVenvCreated`. Assert `pip install -r <requirements>` was called next.
4. `CreateVenv_FailureDuringPipInstall_PropagatesError_AndCleansUp`. Launcher fails on second call; assert partial venv is deleted and error is returned.
5. `GetStatus_VenvExists_ReturnsReady_WithInterpreterPath`. No process launched; just checks filesystem (via `IVenvPaths`).
6. `CreateStartInfo_UsesVenvInterpreter`. Assert `FileName` points to `<venv>/bin/python3`, not system python.
7. `CreateVenv_WithProgressReporter_ReportsEachStage`. `IProgress<VenvBootstrapStage>` gets `CreatingVenv`, `InstallingRequirements`, `Verifying`, `Complete`.
8. `CreateVenv_Cancelled_StopsCleanly`. Cancel during pip install; process killed; state = `NotReady`.

No real venv creation in unit tests — everything driven by `FakeProcessLauncher` and `FakeVenvPaths` pointing at a temp dir. Filesystem effects are minimal (creating/deleting empty marker files).

**Local verification:**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
```

**PR description template:**
```
Add ManagedVenvRuntime for speaker-labeling sidecar

Default runtime for Phase 1+ users: creates a venv under
~/Library/Application Support/VoxFlow/python-runtime/, installs
pinned pyannote.audio + torch + torchaudio, caches for subsequent
runs, reports progress, supports cancellation. Unit-tested end to
end via FakeProcessLauncher + FakeVenvPaths — no real Python
executed in tests.

Test plan:
- [x] dotnet test — green
```

---

### P0.5 — `voxflow_diarize.py` sidecar script

**Branch:** `speaker-labeling/p0.5-sidecar-script`

**Why fifth:** This is the Python side of the contract. It needs to exist as a runnable script before the .NET client that invokes it can be integration-tested. No .NET code changes in this PR.

**Files touched (new):**
- `src/VoxFlow.Core/Resources/voxflow_diarize.py`
- `src/VoxFlow.Core/Resources/python-requirements.txt` — (if not already created in P0.4, create here; otherwise amend with exact pinned versions validated against the script).
- `tests/VoxFlow.Core.Tests/Services/Python/SidecarScriptContractTests.cs` — integration tests tagged `[Trait("Category", "RequiresPython")]` at the class level.

**Script shape:**

```python
# voxflow_diarize.py
# Reads a single JSON request on stdin, writes a single JSON response on stdout.
# Progress lines as NDJSON on stderr while diarization runs.
# Never writes to stdout until the final response.

# Request:  {"version": 1, "wavPath": "/abs/path.wav"}
# Response: {"version": 1, "status": "ok", "speakers": [...], "segments": [...]}
# Error:    {"version": 1, "status": "error", "error": "<message>"}
```

**TDD steps (Python side first, integration tests in .NET second):**

1. Manual scaffolding: write the skeleton that reads stdin, parses JSON, routes by `version`, prints an error for unknown versions. Commit this as the first local-only step.
2. Add the pyannote pipeline call. For now, just load the pretrained pipeline, pass `wavPath`, extract `speakers` and `segments`. Normalize speaker IDs to `A`, `B`, `C` in order of first appearance. Format per the schema.
3. **Red (integration test).** `SidecarScriptContractTests.RunAgainstSingleSpeakerWav_ReturnsOkResponse_WithOneSpeaker`. Requires the Obama fixture from P0.8. Until P0.8 lands, guard the test with `Skip.IfNot(File.Exists(fixturePath), "fixture not yet committed; will be enabled in P0.8")` using `Xunit.SkippableFact` (add the `Xunit.SkippableFact` NuGet package to `VoxFlow.Core.Tests.csproj` in this PR). Assert response matches schema, `status=ok`, `speakers.Count==1`.
4. **Green.** Fix the script until the test passes. This is where the contract gets exercised against real pyannote.
5. **Red.** `RunAgainstTwoSpeakerWav_ReturnsOkResponse_WithTwoSpeakers`. Same pattern.
6. **Green.** Ensure speaker ID normalization is stable across runs.
7. **Red.** `RunAgainstMissingWav_ReturnsErrorResponse`. Pass a non-existent path; expect `status=error` with a descriptive message, exit code 0 (the process itself succeeded at reporting an error; only schema violations or crashes produce non-zero).
8. **Green.** Add explicit file-exists check before loading pyannote.
9. **Red.** `RunWithMalformedJsonRequest_ReturnsErrorResponse_AndExitsNonZero`. Malformed JSON on stdin → parse error → non-zero exit. This is the "unrecoverable on the Python side" case; .NET side will treat it the same as a crash.
10. **Green.** Handle JSON parse errors in the top-level try/except.

Progress lines on stderr are not tested yet — they become useful in P0.6 when the .NET client parses them.

**Local verification (developer machine only, not CI):**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj --filter Category=RequiresPython
```
The developer needs Python 3.10+ with pyannote installed manually (via the same `pip install -r python-requirements.txt` that `ManagedVenvRuntime` will run in production). First-time pyannote model download happens during this step and is cached in `~/Library/Caches/VoxFlow/models/` — same cache the production path uses.

**PR description template:**
```
Add voxflow_diarize.py sidecar script

Python side of the speaker-labeling JSON contract (ADR-024 Phase 0).
Reads {wavPath} on stdin, runs pyannote diarization, writes speakers
and segments on stdout per sidecar-diarization-v1 schema. Error
envelope on recoverable failures; non-zero exit only on malformed
input. Integration tests behind [Trait("Category", "RequiresPython")]
— require pyannote installed locally. SkippableFact used where the
fixture isn't committed yet (P0.8).

Test plan:
- [x] dotnet test — green (excluding RequiresPython)
- [x] dotnet test --filter Category=RequiresPython — green locally
      with Python 3.11 + pyannote.audio 3.1 installed
```

---

### P0.6 — `IDiarizationSidecar` / `PyannoteSidecarClient`

**Branch:** `speaker-labeling/p0.6-sidecar-client`

**Why sixth:** Bridges `IPythonRuntime` and `voxflow_diarize.py`. First .NET code that really exercises the process boundary. Unit-tested with mocked runtime; integration-tested with real Python.

**Files touched (new):**
- `src/VoxFlow.Core/Interfaces/IDiarizationSidecar.cs`
- `src/VoxFlow.Core/Services/Diarization/PyannoteSidecarClient.cs`
- `src/VoxFlow.Core/Models/DiarizationRequest.cs`
- `src/VoxFlow.Core/Models/DiarizationResult.cs` — `{ int Version, IReadOnlyList<DiarizationSpeaker> Speakers, IReadOnlyList<DiarizationSegment> Segments }`.
- `src/VoxFlow.Core/Models/DiarizationSpeaker.cs`, `DiarizationSegment.cs`.
- `src/VoxFlow.Core/Models/SidecarFailureReason.cs` — enum: `RuntimeNotReady`, `ProcessCrashed`, `Timeout`, `MalformedJson`, `SchemaViolation`, `ErrorResponseReturned`.
- `tests/VoxFlow.Core.Tests/Services/Diarization/PyannoteSidecarClientTests.cs`
- `tests/VoxFlow.Core.Tests/Services/Diarization/PyannoteSidecarClientIntegrationTests.cs` — `[Trait("Category", "RequiresPython")]` on the class; any tests that depend on a not-yet-committed fixture use `SkippableFact` + `Skip.IfNot(File.Exists(path), ...)`.

**Interface shape:**
```csharp
public interface IDiarizationSidecar
{
    Task<DiarizationResult> DiarizeAsync(
        DiarizationRequest request,
        IProgress<SpeakerLabelingProgress>? progress,
        CancellationToken ct);
}
```

Failures throw `DiarizationSidecarException(SidecarFailureReason reason, string message, Exception? inner)`.

**TDD steps — unit tests (mocked runtime):**

1. **Red.** `DiarizeAsync_HappyPath_ReturnsResultFromStdout`. Mock `IPythonRuntime` returns a canned `ProcessStartInfo`; `FakeProcessLauncher` is set up to return a pre-recorded sidecar response JSON on stdout and exit 0. Assert result fields match.
2. **Green.** Implement `PyannoteSidecarClient` with start-process, write JSON to stdin, read stdout, parse, return.
3. **Red.** `DiarizeAsync_NonZeroExit_ThrowsWithProcessCrashed`. Exit code 1, empty stdout.
4. **Green.** Map exit != 0 with no parseable response to `SidecarFailureReason.ProcessCrashed`.
5. **Red.** `DiarizeAsync_MalformedJsonOnStdout_ThrowsWithMalformedJson`.
6. **Green.** Catch `JsonException`, map to `MalformedJson`.
7. **Red.** `DiarizeAsync_ResponseWithStatusError_ThrowsWithErrorResponseReturned`. Valid envelope, `status=error`.
8. **Green.** Branch on `status` after parsing; throw with the error message.
9. **Red.** `DiarizeAsync_Timeout_KillsProcessAndThrowsTimeout`. Process hangs; client's internal timeout fires.
10. **Green.** Use `CancellationTokenSource.CreateLinkedTokenSource(externalCt, timeoutCt)`; on timeout, kill process tree; throw `Timeout`.
11. **Red.** `DiarizeAsync_ExternalCancellation_KillsProcess_ThrowsOperationCanceled`. External cancel before completion.
12. **Green.** Same kill-tree logic, but `OperationCanceledException` instead.
13. **Red.** `DiarizeAsync_ProgressOnStderr_ForwardsToReporter`. Launcher feeds two NDJSON lines on stderr before the final result; assert `IProgress` got two updates.
14. **Green.** Background-read stderr line-by-line, parse each as JSON, forward to reporter. Handles partial lines and non-JSON gracefully (ignore).
15. **Red.** `DiarizeAsync_SchemaViolation_ThrowsWithSchemaViolation`. Response is valid JSON but missing required `speakers` field.
16. **Green.** Validate against `sidecar-diarization-v1.schema.json` before returning. Map failures to `SchemaViolation`.
17. **Refactor.** Extract stdout/stderr reading into helper; confirm no resource leaks (process disposed, streams flushed).

**TDD steps — integration tests (real Python):**

1. `DiarizeAsync_RealSidecar_SingleSpeakerWav_Returns1Speaker`. Uses the real `ManagedVenvRuntime` pointing at the developer's dev venv, real `voxflow_diarize.py`, real Obama fixture. Class tagged `[Trait("Category", "RequiresPython")]`; test is a `SkippableFact` that calls `Skip.IfNot(File.Exists(fixturePath), …)` so the suite still goes green before P0.8 commits the WAV. Confirms the full contract holds end-to-end.
2. `DiarizeAsync_RealSidecar_TwoSpeakerWav_Returns2Speakers`.
3. `DiarizeAsync_RealSidecar_ThreeSpeakerWav_ReturnsAtLeast3Speakers`.

**Local verification:**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj --filter Category=RequiresPython
```

**PR description template:**
```
Add PyannoteSidecarClient

Bridges IPythonRuntime and voxflow_diarize.py. Handles stdin writing,
stdout/stderr reading, schema validation, timeouts, cancellation,
progress forwarding, and failure taxonomy. Unit-tested with fake
runtime/launcher; integration-tested against real pyannote under
[Trait("Category", "RequiresPython")].

Test plan:
- [x] dotnet test — green
- [x] dotnet test --filter Category=RequiresPython — green locally
```

---

### P0.7 — `SpeakerMergeService`

**Branch:** `speaker-labeling/p0.7-merge-service`

**Why seventh:** The core business logic of the feature. Pure function, no I/O, maximum test coverage. All previous PRs lead up to this so that its tests can use realistic types (`WhisperToken`, `DiarizationResult`, `TranscriptDocument`) without mocking.

**Files touched (new):**
- `src/VoxFlow.Core/Interfaces/ISpeakerMergeService.cs`
- `src/VoxFlow.Core/Services/Diarization/SpeakerMergeService.cs`
- `tests/VoxFlow.Core.Tests/Services/Diarization/SpeakerMergeServiceTests.cs`
- `tests/fixtures/sidecar/responses/single-speaker.json`
- `tests/fixtures/sidecar/responses/two-speaker.json`
- `tests/fixtures/sidecar/responses/three-speaker.json`
- `tests/fixtures/sidecar/words/single-speaker-tokens.json`
- `tests/fixtures/sidecar/words/two-speaker-tokens.json`
- `tests/fixtures/sidecar/words/three-speaker-tokens.json`

**Interface shape:**
```csharp
public interface ISpeakerMergeService
{
    TranscriptDocument Merge(
        IReadOnlyList<FilteredSegment> segments,
        DiarizationResult diarization,
        TranscriptMetadata metadata);
}
```

**TDD sequence — each case red-then-green:**

1. `Merge_EmptyInputs_ReturnsEmptyDocument`.
2. `Merge_SingleSpeaker_AllWordsAssignedToA`.
3. `Merge_TwoSpeakers_AssignsByMaxTimeOverlap`. Two diarization segments, six words: four fully inside A, two fully inside B. All assigned correctly.
4. `Merge_WordStraddlingTwoSpeakers_AssignsToMaxOverlap`. One word straddling the boundary; assigned to whichever speaker has more overlap.
5. `Merge_WordNotCoveredByAnySegment_AssignsToNearestSpeaker`. A brief silence gap; the word is assigned to whichever segment's boundary is closest in time.
6. `Merge_OrdinalLabels_FirstAppearanceWins`. Diarization returns `[B at 0s, A at 5s]`; merge remaps so first speaker becomes `A`, second `B`. (This is where normalization happens — even if sidecar returns A/B/C already, merge re-normalizes to be defensive.)
7. `Merge_ProducesCorrectTurnBoundaries`. Assert `Turns` list is exactly what `SpeakerTurn.GroupConsecutive` produces.
8. `Merge_ComputesTotalSpeechDurationPerSpeaker`. `SpeakerInfo.TotalSpeechDuration` = sum of turn durations for that speaker.
9. `Merge_ThreeSpeakers_AllAssignedCorrectly`. Full three-speaker fixture.
10. `Merge_DiarizationFromSingleSpeakerFixture_MatchesExpected`. Load `single-speaker-tokens.json` + `single-speaker.json` from disk; assert result.
11. `Merge_DiarizationFromTwoSpeakerFixture_MatchesExpected`. Same for two-speaker.
12. `Merge_DiarizationFromThreeSpeakerFixture_MatchesExpected`.
13. `Merge_RecordsProvidedMetadata`. `DiarizationModel`, `SidecarVersion` passed through unchanged.
14. `Merge_OrdinalLabelsAreStableAcrossCalls`. Run twice on the same input; compare.
15. `Merge_DetectedSpeakerCountReflectsRosterSize`.
16. `Merge_FlattenWordsAcrossSegments_PreservesStartTimeOrdering`. Input: three `FilteredSegment`s where segment boundaries do **not** correspond to word boundaries (some segments have more than one word, one segment has none), passed in chronological order. Assert that the resulting `TranscriptWord` list is a single chronologically-sorted sequence and that a segment with an empty `Words` list (i.e. `Array.Empty<WhisperToken>()` — the default from P0.1) does not crash the merge and is simply contributed as zero words. This guards the contract with P0.1 explicitly.

**Fixture files** are hand-authored JSON:
- `*-tokens.json` — array of `{"text", "startSec", "endSec"}` objects representing Whisper word tokens. Tests deserialize them and convert to `WhisperToken`/`FilteredSegment` as appropriate.
- `*.json` (responses) — canned `DiarizationResult` objects. These are smaller than the real sidecar responses used in integration tests; they're designed to exercise specific merge cases, not to represent real audio.

**Local verification:**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
```

**PR description template:**
```
Add SpeakerMergeService

Pure-logic merge of Whisper word tokens and diarization speaker
segments into a TranscriptDocument. Handles overlap matching,
ordinal label normalization, turn grouping, total-duration
computation, flatten/order across segment boundaries, and edge
cases (empty, single-speaker, straddling, uncovered, empty-Words).
16 unit tests against hand-authored JSON fixtures. No I/O, no mocks.

Test plan:
- [x] dotnet test — green
```

---

### P0.8 — Integration test fixtures (audio)

**Branch:** `speaker-labeling/p0.8-audio-fixtures`

**Why last in Phase 0:** Fixture files are binary and belong at the end of the phase so Phase 0 can be reviewed without mixing code and large binaries earlier. Also, by the time we add real audio fixtures, the integration tests that depend on them (from P0.5 and P0.6) already exist and are expecting these files.

**Files touched (new):**
- `tests/fixtures/sidecar/audio/obama-speech-1spk-10s.wav` — trimmed from `artifacts/input/President Obama Speech.m4a`, 16kHz mono PCM, ~10s.
- `tests/fixtures/sidecar/audio/libricss-2spk-10s.wav` — extracted from LibriCSS `0L` session (0% overlap, 2-speaker mix), ~10s, 16kHz mono PCM. Source noted in `README-fixtures.md`.
- `tests/fixtures/sidecar/audio/libricss-3spk-10s.wav` — extracted from LibriCSS 4-speaker mix, ~10s window containing audio from 3+ speakers.
- `tests/fixtures/sidecar/audio/README-fixtures.md` — source URLs, licenses (CC BY 4.0 for LibriCSS, user-provided for Obama), exact ffmpeg commands used to trim, and any post-processing notes.
- `tests/fixtures/sidecar/SIZE-BUDGET.md` — 10s × 16 kHz × 16-bit mono PCM is ~320 KB per file, so the budget is **<400 KB per fixture** and **<1 MB total** across the three WAVs. Anything larger has to be shortened or down-mixed before commit; the .gitattributes section in that doc pins these files to plain LFS-unaware binary storage (no LFS) so the repo stays self-contained.

**TDD steps:** No new tests are introduced in this PR. Instead, the `SkippableFact`-guarded integration tests from P0.5 and P0.6 — which previously skipped via `Skip.IfNot(File.Exists(fixturePath), …)` — now run fully green because the fixtures exist.

**Operational steps (scripted, not TDD):**

1. Trim Obama: `ffmpeg -i "artifacts/input/President Obama Speech.m4a" -ss 00:00:05 -t 00:00:10 -ac 1 -ar 16000 -sample_fmt s16 tests/fixtures/sidecar/audio/obama-speech-1spk-10s.wav`. Pick a start offset containing clean speech.
2. Download LibriCSS from its published source (link in `README-fixtures.md`). Cache outside the repo. Extract a 10-second window from the `0L` session. Convert to the same WAV format.
3. Extract a 10-second window from the `OV20` or `OV30` session (overlap variants include >=3 distinct speakers in short windows). Convert similarly.
4. Verify each file plays in `afplay` (macOS) or equivalent, is ~10 seconds, is 16 kHz mono s16le.
5. Commit the three WAV files, the README, and the size budget doc.
6. Re-run the full integration test suite locally: every previously-skipped test from P0.5/P0.6 now passes.

**Local verification:**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj --filter Category=RequiresPython
```

**PR description template:**
```
Add integration test audio fixtures for speaker labeling

Three 10-second WAV fixtures for the sidecar integration test suite:
- obama-speech-1spk-10s.wav (trimmed from artifacts/input)
- libricss-2spk-10s.wav (LibriCSS 0L session, CC BY 4.0)
- libricss-3spk-10s.wav (LibriCSS overlap session, CC BY 4.0)
All 16 kHz mono s16le, ~10 s each, <400 KB per file, <1 MB total.
Sources, licenses, and reproduction commands in README-fixtures.md.
Completes Phase 0: [Trait("Category", "RequiresPython")] integration
tests (P0.5/P0.6) now run fully green against real audio, with no
SkippableFact skips.

Test plan:
- [x] dotnet test — green
- [x] dotnet test --filter Category=RequiresPython — green locally
```

---

## Parallel Spike: `python-build-standalone` packaging

**Branch:** `speaker-labeling/spike-standalone-python` (separate, not part of the P0.N sequence)

**Goal:** Determine whether a pre-built `python-build-standalone` interpreter plus a pre-populated venv (pyannote + PyTorch) can be bundled with the Desktop installer, code-signed, and notarized on macOS.

**Non-blocking:** This spike runs in parallel to P0.1–P0.8. Its outcome feeds into Phase 3, not Phase 0. If the spike fails, `ManagedVenvRuntime` remains the default and the feature still ships — Desktop users simply need Python 3.10+ installed on their machine, documented in the runbook.

**Steps:**

1. Download a recent `python-build-standalone` release for macOS (Apple Silicon and Intel).
2. Create a venv inside the extracted standalone tree, install `-r python-requirements.txt` with the exact pinned versions used in `ManagedVenvRuntime`.
3. Move the entire tree to a different path; verify pyannote still imports and runs (validates relocatability — the key property of `python-build-standalone`).
4. Sign every `.dylib` and `.so` under the tree with the VoxFlow developer certificate.
5. Bundle the tree inside a test `.app` directory and notarize via `xcrun notarytool submit`.
6. Record total size, signing time, notarization time, and any errors in `spike-outcome.md`.

**Go/no-go criteria:**
- ✅ Go: notarization succeeds, total bundle size <1.5 GB (including model download on first run), import + diarization works from the relocated tree.
- ❌ No-go: notarization fails repeatedly, or relocatability breaks, or size exceeds 2 GB.

**Outcome document:** `docs/delivery/local-speaker-labeling/spike-standalone-outcome.md` (written at end of spike, committed to integration branch).

---

## Test Fixture Index (reference)

| File | Type | Source | License | Used by |
|---|---|---|---|---|
| `tests/fixtures/sidecar/audio/obama-speech-1spk-10s.wav` | Audio | User-provided (trimmed) | As-is from `artifacts/input` | P0.5, P0.6 integration |
| `tests/fixtures/sidecar/audio/libricss-2spk-10s.wav` | Audio | [LibriCSS](https://github.com/chenzhuo1011/libri_css) | CC BY 4.0 | P0.5, P0.6 integration |
| `tests/fixtures/sidecar/audio/libricss-3spk-10s.wav` | Audio | [LibriCSS](https://github.com/chenzhuo1011/libri_css) | CC BY 4.0 | P0.5, P0.6 integration |
| `tests/fixtures/sidecar/responses/single-speaker.json` | JSON | Hand-authored | — | P0.7 unit |
| `tests/fixtures/sidecar/responses/two-speaker.json` | JSON | Hand-authored | — | P0.7 unit |
| `tests/fixtures/sidecar/responses/three-speaker.json` | JSON | Hand-authored | — | P0.7 unit |
| `tests/fixtures/sidecar/words/*-tokens.json` | JSON | Hand-authored | — | P0.7 unit |

---

## Post-Phase-0 Checklist

Before declaring Phase 0 complete and moving to Phase 1 planning:

- [ ] All 8 sub-PRs merged into `Local-Speaker-Labeling`.
- [ ] `dotnet test` on `Local-Speaker-Labeling` is fully green.
- [ ] `dotnet test --filter Category=RequiresPython` on `Local-Speaker-Labeling` is fully green on a developer machine with Python 3.10+ available.
- [ ] `docs/contracts/sidecar-diarization-v1.schema.json` and `voxflow-transcript-v1.schema.json` exist and are referenced by tests.
- [ ] `voxflow_diarize.py` runs standalone against a test WAV and produces schema-valid JSON.
- [ ] Spike `python-build-standalone` has a written outcome doc (go, no-go, or deferred).
- [ ] No files under `src/` touch `ISpeakerEnrichmentService`, `SpeakerLabelingOptions`, or any user-visible wiring — all of that is Phase 1.
- [ ] User has reviewed the integration branch state and approved starting Phase 1.
