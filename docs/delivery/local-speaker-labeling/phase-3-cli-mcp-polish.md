# Phase 3 — CLI, MCP, Documentation, Release Prep

**Parent:** [Local Speaker Labeling — Delivery Plan](README.md)
**ADR:** [ADR-024](../../adr/024-local-speaker-labeling-pipeline.md)
**Status:** Planned

---

## Goal

Close the feature by reaching host parity across CLI, Desktop, and MCP, then producing the user-facing documentation that makes speaker labeling operable on a fresh machine. Phases 1 and 2 have already delivered the enrichment pipeline and the Desktop UX; Phase 3 is the "polish" phase that makes the feature discoverable, configurable from every host, and recoverable when something goes wrong.

By the end of Phase 3, a new developer can `git clone`, read `docs/runbooks/speaker-labeling.md`, run a single `voxflow transcribe --speakers` command, and see a colored, speaker-labeled transcript — with any failure surfaced via a clear diagnostic that points back to the runbook. The `Local-Speaker-Labeling` integration branch is then ready for user-driven promotion to `master`. Promotion itself is **not** part of this phase.

Phase 3 is deliberately sequenced last because CLI argument parsing and MCP schema changes are trivial compared to the enrichment pipeline, but they are the first thing a user touches — so the underlying pipeline must be proven correct (Phases 0–2) before the surface is decorated.

## Exit Criteria

- `voxflow --speakers` (and `--speakers=false`) overrides `transcription.speakerLabeling.enabled` for a single CLI invocation, on both single-file and batch commands.
- `voxflow --help` documents the `--speakers` flag alongside existing flags (or, if no help infrastructure exists yet, the P3.1 PR introduces the minimal help surface needed to cover the new flag).
- The MCP `transcribe_file` tool exposes an `enableSpeakers` boolean parameter that overrides the configured default per request. The tool's JSON schema reflects the new parameter and is verified by a schema snapshot test in `VoxFlow.McpServer.Tests`.
- `docs/runbooks/speaker-labeling.md` exists, has been manually walked through on a clean developer machine, and covers first-run setup, model download troubleshooting, sidecar diagnostics, and the `Category=RequiresPython` test gate.
- `docs/ARCHITECTURE.md` has a new subsection describing the enrichment pipeline, the `IPythonRuntime` abstraction, and the sidecar process boundary. Existing architecture content is not rewritten — only extended.
- `docs/developer/setup.md` documents the Python prerequisites for running `Category=RequiresPython` tests locally, and the repo `README.md` gains a one-paragraph mention of the feature with a link to the runbook.
- `appsettings.example.json` has the complete `transcription.speakerLabeling` block with comments explaining each key.
- The `python-build-standalone` spike outcome (produced in parallel to Phase 0) has been turned into a concrete go/no-go decision. If **go**, `StandaloneRuntime` is implemented and wired in as a third `IPythonRuntime` mode in a dedicated sub-PR. If **no-go**, the `"Standalone"` config value is removed from the allowed set and `SpeakerLabelingOptions` throws a clear validation error on load; the decision is recorded in ADR-024.
- `ADR-024` has a `Status: Accepted` stamp and a dated note summarizing delivery outcomes (three phases merged into `Local-Speaker-Labeling`).
- All 13 acceptance criteria from the [README](README.md#acceptance-criteria) are verified. The "Acceptance Check Before Promotion" checklist is fully ticked.
- `dotnet test VoxFlow.sln` is fully green locally on the integration branch **and** `dotnet test --filter "Category=RequiresPython"` is fully green on a machine with Python 3.10+ and `pyannote.audio` installed.
- Branch `Local-Speaker-Labeling` is tagged internally as "ready for user promotion review". Promotion to `master` is deliberately outside Phase 3 and outside this delivery.

## Pre-conditions

- Phase 1 (enrichment) and Phase 2 (Desktop UI) are merged into `Local-Speaker-Labeling`.
- `SpeakerLabelingOptions.Enabled` is already honored by the pipeline (P1.1, P1.4).
- `TranscribeFileRequest.EnableSpeakers` (nullable bool override) is already defined and respected by `TranscriptionService` (P1.2, P1.4).
- The Desktop app already forwards its Ready-screen toggle through `TranscribeFileRequest.EnableSpeakers` (P2.2), so the pattern of "host override wins over config default" is already live; Phase 3 is extending the same pattern to CLI and MCP.
- `python-build-standalone` spike has a documented outcome (repo issue or a markdown note under `docs/delivery/local-speaker-labeling/spikes/`). If the spike is still in flight at the start of Phase 3, P3.5 is postponed and the rest of Phase 3 lands without it — the exit criterion above handles both outcomes.
- `Local-Speaker-Labeling` branch is checked out, up to date, and fully green under `dotnet test`.

## Non-goals

These are deliberately excluded from Phase 3 so the phase stays scoped and shippable:

- **No CLI argument-parsing framework migration.** P3.1 introduces the **minimum** hand-written parser needed to recognize `--speakers` (and surface a `--help` line). Adopting `System.CommandLine`, Spectre.Console.Cli, or any other framework is a separate refactor and is not blocked by this delivery.
- **No `voxflow diarize <file>` standalone subcommand.** Speaker labeling is an enrichment of the existing `transcribe` / `batch` flows, not a new top-level command.
- **No interactive Desktop runbook / in-app help.** The runbook is a markdown document; surfacing it inside the Desktop app is a future enhancement.
- **No localization of the runbook or help text.** English only for v1.
- **No package-manager distribution of the managed venv.** `ManagedVenvRuntime` continues to bootstrap on first enable; Phase 3 does not add a separate installer or Homebrew formula.
- **No promotion of `Local-Speaker-Labeling` to `master`.** This is explicitly a user decision made after Phase 3 is complete.
- **No performance benchmarks.** Diarization latency is documented qualitatively in the runbook ("10-minute audio file ≈ 2 minutes of enrichment on an M1") but is not asserted by any automated test.
- **No new telemetry or analytics.** Consistent with VoxFlow's privacy-first principle.

---

## TDD Sequence

Six sub-PRs, in the order below. P3.5 is conditional on the `python-build-standalone` spike outcome and may be skipped (see its "Conditional" note). Each PR is the smallest unit that leaves `Local-Speaker-Labeling` in a green-tests state.

Conventions for every PR below:
- **Branch:** `speaker-labeling/p3.M-<slug>` off `Local-Speaker-Labeling`.
- **Base for PR:** `Local-Speaker-Labeling` (**never** `master`).
- **Before `gh pr create`:** run `dotnet test VoxFlow.sln` locally; if the PR touches integration code also run `dotnet test --filter "Category=RequiresPython"`. Both must be fully green.
- **Test tagging:** Python-gated tests use `[Trait("Category", "RequiresPython")]` on the test class (xUnit 2.9.2 syntax).
- **Commit authorship:** user only; no Co-Authored-By trailers.
- **PR body:** no "Generated with Claude Code" footer.

---

### P3.1 — CLI `--speakers` flag

**Branch:** `speaker-labeling/p3.1-cli-speakers-flag`

**Why first:** CLI parity is the simplest of the three host surfaces and establishes the "host override wins" pattern on the CLI side without touching any protocol schema. It is entirely self-contained inside `VoxFlow.Cli`. Every subsequent PR in this phase either documents or decorates work that already exists.

**Files touched:**
- `src/VoxFlow.Cli/Program.cs` — add a minimal hand-written argument parser in `Main` that recognizes `--speakers`, `--speakers=true`, `--speakers=false`, `--no-speakers`, and `--help`. The parser is called **before** `configService.LoadAsync()` so the parsed value can be applied as an override after the options load. Everything else in `Main` is untouched.
- `src/VoxFlow.Cli/CliArguments.cs` — **new** file with a `CliArguments` record holding `EnableSpeakers: bool?` and `ShowHelp: bool` and a static `CliArguments.Parse(string[] args)` method. Pure logic, no IO — the whole point of extracting it is testability.
- `src/VoxFlow.Cli/Program.cs` — after parsing, if `args.EnableSpeakers is not null`, set `options = options with { SpeakerLabeling = options.SpeakerLabeling with { Enabled = args.EnableSpeakers.Value } }`. Then pass through to the existing `RunSingleFileAsync` / `RunBatchAsync`. The CLI does **not** need to thread the flag through `TranscribeFileRequest.EnableSpeakers` because it mutates the loaded `TranscriptionOptions` instance directly — this is the CLI-specific shortcut and it's simpler than the Desktop / MCP per-request override pattern, because the CLI invocation *is* the request.
- `tests/VoxFlow.Cli.Tests/CliArgumentsTests.cs` — **new** test file. Pure-logic tests for the parser. No DI container, no host.

**TDD steps:**

1. **Red.** Add `CliArgumentsTests.Parse_NoArgs_ReturnsNullEnableSpeakers_AndShowHelpFalse`. Expect `args.EnableSpeakers == null` and `args.ShowHelp == false`. Compile: fails because `CliArguments` doesn't exist.
2. **Green.** Add `CliArguments.cs` with the record shape and an empty `Parse` that returns default values. Test passes.
3. **Red.** Add `CliArgumentsTests.Parse_SpeakersFlag_SetsEnableSpeakersTrue` — `Parse(["--speakers"])` → `EnableSpeakers == true`. Fails.
4. **Green.** Implement the `--speakers` branch. Passes.
5. **Red.** Add `CliArgumentsTests.Parse_SpeakersEqualsFalse_SetsEnableSpeakersFalse` — `Parse(["--speakers=false"])` → `EnableSpeakers == false`. Fails.
6. **Green.** Add the `--speakers=<value>` branch. Passes. Accepted values are exactly `true` and `false` (case-insensitive); anything else throws `ArgumentException` with a message naming the flag.
7. **Red.** Add `CliArgumentsTests.Parse_NoSpeakers_SetsEnableSpeakersFalse` — `Parse(["--no-speakers"])` → `EnableSpeakers == false`. Fails.
8. **Green.** Add the `--no-speakers` branch. Passes.
9. **Red.** Add `CliArgumentsTests.Parse_HelpFlag_SetsShowHelpTrue` — `Parse(["--help"])` → `ShowHelp == true`. Fails.
10. **Green.** Add the `--help` branch. Passes.
11. **Red.** Add `CliArgumentsTests.Parse_UnknownFlag_Throws` — `Parse(["--bogus"])` → `ArgumentException` whose message contains `--bogus`. Fails.
12. **Green.** Default branch in `Parse` throws. The error message must include the unknown token, **not** a generic "invalid argument" — the CLI is the user's only error channel here. Passes.
13. **Red.** Add `CliArgumentsTests.Parse_ConflictingFlags_LastWins_ExplicitAssertion` — `Parse(["--speakers", "--no-speakers"])` → `EnableSpeakers == false`. Document the "last writer wins" rule explicitly in a test so nobody has to re-derive it. Fails.
14. **Green.** Parser already handles this if it iterates left-to-right; add an assertion comment inside the parser that this is a deliberate contract, not an accident.
15. **Refactor.** Collapse duplicate branches in `Parse` (the three forms all produce a bool — they should funnel through one helper). Keep the test surface unchanged.
16. **Red.** In `Program.cs`, add a guard: if `CliArguments.Parse` throws, print the message to `Console.Error` and `return 2` (distinct from validation failure = 1). Write an integration-level test only if a straightforward harness exists (`VoxFlow.Cli.Tests` currently has no such harness, so this may be a manual verification step documented in the PR body — do not invent a harness for one assertion).
17. **Green.** Add the `try { var cliArgs = CliArguments.Parse(args); }` guard at the top of `Main`. If `cliArgs.ShowHelp`, print a short help block and return 0 **before** building the DI container.
18. **Manual verification (documented in PR body):**
    - `dotnet run --project src/VoxFlow.Cli -- --help` prints the help block and exits 0.
    - `dotnet run --project src/VoxFlow.Cli -- --bogus` prints the unknown-flag error and exits 2.
    - `dotnet run --project src/VoxFlow.Cli -- --speakers` with `enabled=false` in appsettings overrides the config and runs the feature (observed via `ProgressStage.Diarizing` appearing in progress output — smoke-only, not asserted).
19. **Refactor.** The help block text lives next to `CliArguments.Parse` as a `const string` — not inside `Program.cs` — so future flags are added in one place.

**Local verification before PR:**
```
dotnet test tests/VoxFlow.Cli.Tests/VoxFlow.Cli.Tests.csproj
dotnet test VoxFlow.sln
```
Both fully green. `CliArgumentsTests` is the only new test file; nothing else in `VoxFlow.Cli.Tests` should change.

**PR description template:**
```
CLI: add --speakers flag for per-invocation speaker-labeling override

Part of ADR-024 Phase 3. Introduces a minimal hand-written argument
parser to VoxFlow.Cli, supporting --speakers / --speakers=<bool> /
--no-speakers / --help / unknown-flag diagnostics. The parsed value
overrides transcription.speakerLabeling.enabled for the current
invocation only. Config file is not mutated. Desktop and MCP behavior
are unchanged by this PR.

No dependency on System.CommandLine — adopting a framework parser is
out of scope for this delivery.

Test plan:
- [x] dotnet test VoxFlow.sln — fully green
- [x] CliArgumentsTests covers --speakers, --speakers=true/false,
      --no-speakers, --help, unknown-flag error, and conflict resolution
- [x] Manual: --help / --bogus / --speakers smoke-tested against a
      real config file
```

---

### P3.2 — MCP `enableSpeakers` tool parameter

**Branch:** `speaker-labeling/p3.2-mcp-enable-speakers`

**Why second:** MCP is the second-simplest host surface. It already has `TranscribeFileAsync` at [WhisperMcpTools.cs:70](../../../src/VoxFlow.McpServer/Tools/WhisperMcpTools.cs#L70), so the change is a single optional parameter plus a schema snapshot update. No plumbing beyond forwarding to `TranscribeFileRequest.EnableSpeakers`, which already exists.

**Files touched:**
- `src/VoxFlow.McpServer/Tools/WhisperMcpTools.cs` — add `bool? enableSpeakers = null` to `TranscribeFileAsync` parameters with a `[Description]` attribute explaining "Override `transcription.speakerLabeling.enabled` for this request only. `null` (omitted) uses the server's configured default." Pass it through to `new TranscribeFileRequest(..., EnableSpeakers: enableSpeakers)`.
- `tests/VoxFlow.McpServer.Tests/WhisperMcpToolsTests.cs` — new tests asserting the parameter is forwarded correctly into `TranscribeFileRequest` via an `ITranscriptionService` fake.
- `tests/VoxFlow.McpServer.Tests/TranscribeFileToolSchemaTests.cs` — **new** test file (or add to the existing schema-snapshot test if one already lives there) that asserts the discovered MCP tool schema for `transcribe_file` contains an `enableSpeakers` property of type `boolean` and marks it optional. If no snapshot infrastructure exists, assert the `MethodInfo` of `TranscribeFileAsync` has a parameter named `enableSpeakers` with type `bool?` — this is a low-fidelity proxy but it catches the most common regression (accidental rename).

**TDD steps:**

1. **Red.** Add `WhisperMcpToolsTests.TranscribeFileAsync_ForwardsEnableSpeakersTrue`. Stub `ITranscriptionService` with a delegate that captures the `TranscribeFileRequest`; call `TranscribeFileAsync(..., enableSpeakers: true)`; assert the captured request's `EnableSpeakers == true`. Compile: fails because `TranscribeFileAsync` has no such parameter.
2. **Green.** Add the parameter (`bool? enableSpeakers = null`), forward it into the `TranscribeFileRequest` named argument at [WhisperMcpTools.cs:108](../../../src/VoxFlow.McpServer/Tools/WhisperMcpTools.cs#L108). Test passes.
3. **Red.** Add `WhisperMcpToolsTests.TranscribeFileAsync_ForwardsEnableSpeakersFalse`. Same test with `false`. Passes after step 2 — this is a guard that the parameter is actually wired, not hard-coded.
4. **Red.** Add `WhisperMcpToolsTests.TranscribeFileAsync_OmittedEnableSpeakers_ForwardsNull`. Call without the parameter and assert `EnableSpeakers == null`. Passes after step 2 — another guard.
5. **Red.** Add `TranscribeFileToolSchemaTests.TranscribeFileTool_SchemaContainsEnableSpeakersBooleanParameter`. Use reflection on `typeof(WhisperMcpTools).GetMethod("TranscribeFileAsync")!.GetParameters()` and assert a parameter named `enableSpeakers` with type `bool?` and a `DescriptionAttribute` whose text is non-empty. Fails only if P3.2 step 2 was reverted — catches rename regressions.
6. **Green.** Confirm the description attribute is present and descriptive. Passes.
7. **Refactor.** Confirm the `[Description]` attribute text is clear about precedence ("Overrides `transcription.speakerLabeling.enabled` for this request only"). Nothing else.
8. **Manual verification (documented in PR body):** start the MCP server with a config that has `speakerLabeling.enabled=false`, call the `transcribe_file` tool with `enableSpeakers: true`, and confirm the response contains the enrichment-enabled payload shape (non-null `SpeakerTranscript` in the result JSON or an empty one plus `EnrichmentWarnings`, depending on whether Python is available on the host).

**Local verification before PR:**
```
dotnet test tests/VoxFlow.McpServer.Tests/VoxFlow.McpServer.Tests.csproj
dotnet test VoxFlow.sln
```
Both fully green.

**PR description template:**
```
MCP: add enableSpeakers parameter to transcribe_file tool

Part of ADR-024 Phase 3. Exposes the speaker-labeling override to
MCP clients as a nullable boolean. null (omitted) uses the server's
configured default; true/false overrides per request. The parameter
is forwarded into TranscribeFileRequest.EnableSpeakers, which is
already respected by TranscriptionService from Phase 1.

Test plan:
- [x] dotnet test VoxFlow.sln — fully green
- [x] WhisperMcpToolsTests cover true/false/null forwarding
- [x] Schema test asserts the parameter shape via reflection
- [x] Manual: transcribe_file tool called via MCP stdio with
      enableSpeakers=true against a config where enabled=false
```

---

### P3.3 — Runbook `docs/runbooks/speaker-labeling.md`

**Branch:** `speaker-labeling/p3.3-runbook`

**Why third:** The runbook is the operational contract a user reads when something goes wrong. It must exist before the feature is advertised in `README.md` (P3.4), so users who hit the feature for the first time have a landing page. This PR is pure documentation — no code, no tests, but the runbook **must be manually walked through** on a clean machine before merging, and that walkthrough is the PR's "test plan".

**Files touched (new):**
- `docs/runbooks/speaker-labeling.md`
- `docs/runbooks/README.md` — add a single-line link entry (if this index file exists; otherwise skip).

**Files touched (updated):**
- `appsettings.example.json` — if the `speakerLabeling` block is not yet present here (it should be, from P1.1), verify it exists and has inline-adjacent comments in a sibling README snippet. `appsettings.example.json` is valid JSON so it cannot carry comments; the per-key documentation lives in the runbook, not in the example file.

**Runbook structure (sections — every section must exist, order is fixed):**

1. **What speaker labeling does** — one paragraph summary: "Speaker labeling adds `Speaker A` / `Speaker B` prefixes to your transcript by running a local pyannote diarization sidecar. It is off by default and entirely local — no audio ever leaves your machine."
2. **Prerequisites** — Python 3.10+, ~300 MB disk for the pyannote model, a Hugging Face access token (free) with the pyannote model license accepted. Link to the upstream license page and the ADR-024 license note.
3. **First-run setup (ManagedVenv mode, the default)** — exact commands the user runs. If there's a Desktop-app first-run flow (P1.3), describe what the user sees and what the progress messages mean. If Desktop is not the host, describe the CLI equivalent.
4. **First-run setup (SystemPython mode, the escape hatch)** — `pip install pyannote.audio` into the user's system Python, how to point VoxFlow at it, why this mode is **not** the default.
5. **First-run setup (Standalone mode)** — **conditional**: if P3.5 lands, describe the bundled-runtime path. If P3.5 is skipped (spike no-go), this section contains a single sentence noting that Standalone mode is not yet supported and links to the spike outcome.
6. **How to enable the feature** — three host surfaces:
   - Desktop: Ready-screen toggle.
   - CLI: `voxflow --speakers <input.wav>` or set `transcription.speakerLabeling.enabled=true` in `appsettings.json`.
   - MCP: call `transcribe_file` with `enableSpeakers: true`, or set the server default in its config file.
7. **What a successful run looks like** — sample progress output, sample `.voxflow.json` excerpt (two speakers), sample colored Desktop screenshot (or a text description if screenshots are not yet in the repo).
8. **Troubleshooting: "Python runtime not found"** — what `IValidationService` reports, how to install Python 3.10+, how to point VoxFlow at a non-default interpreter.
9. **Troubleshooting: "pyannote model download failed"** — HF token missing, network error, licensing not accepted, model cache location, how to force a re-download.
10. **Troubleshooting: "Sidecar exited with code N"** — how to read the stderr tail that `PyannoteSidecarClient` logs, how to re-run `voxflow_diarize.py` manually for further diagnostics, and (when the issue is reproducible) where to file a bug.
11. **Troubleshooting: "Sidecar timed out"** — what `timeoutSeconds` controls, why the default is 600, when to raise it.
12. **Running the `RequiresPython` test suite locally** — `dotnet test --filter "Category=RequiresPython"`. Mention that this is required before merging any PR that touches sidecar or enrichment code.
13. **Known limitations and non-goals** — links out to the "Out of Scope" section of the delivery README.
14. **Changelog** — phase-dated entries: "2026-MM-DD: Phase 3 shipped. Feature available on Local-Speaker-Labeling branch."

**Pseudo-TDD discipline for a docs-only PR:**

Documentation does not have automated tests, but the TDD principle (**don't write it if you haven't verified it works**) still applies. The "red/green" cycle for each section is:

1. **Red.** Write the section as a user action or troubleshooting step: "If X, do Y."
2. **Verify.** Do X on a clean machine (or in a clean `git worktree` + fresh `~/Library/Caches/VoxFlow/`), follow Y, confirm the user reaches the stated outcome.
3. **Green.** Leave the section as-is if the walkthrough matched. If the walkthrough deviated, **update the section to match reality**, not the other way around — the runbook describes the system as it is, not as it was designed.

**Manual walkthrough checklist (PR body must include all of these checked):**

- [ ] On a clean machine, follow "First-run setup (ManagedVenv mode)" end-to-end; the first run completes and produces a labeled transcript.
- [ ] Deliberately break the HF token (set a bogus value); follow "pyannote model download failed"; recover.
- [ ] Deliberately rename the Python interpreter; follow "Python runtime not found"; recover.
- [ ] Run `dotnet test --filter "Category=RequiresPython"` from the runbook's instructions verbatim; the full suite passes.
- [ ] Read the runbook top-to-bottom as a new user would; every internal link resolves; every command is copy-pasteable; every file path matches the current repo layout.

**Local verification before PR:**
```
dotnet test VoxFlow.sln
```
(Docs-only PR, but the full suite must still be green to guard against accidental stray edits.)

**PR description template:**
```
docs(runbook): local speaker labeling operational runbook

Part of ADR-024 Phase 3. Adds docs/runbooks/speaker-labeling.md
covering prerequisites, first-run setup for ManagedVenv and SystemPython
modes, how to enable from each host, and troubleshooting for the four
most common failure modes (missing Python, failed model download,
sidecar crash, sidecar timeout). Content was validated by a clean-
machine walkthrough before merging.

No code changes. appsettings.example.json is verified to already
contain the speakerLabeling block from Phase 1.

Test plan:
- [x] dotnet test VoxFlow.sln — fully green (no code changes)
- [x] Manual walkthrough of each troubleshooting section on a clean
      machine; every procedure reached its stated outcome
- [x] Every path, command, and link verified against current repo
```

---

### P3.4 — `ARCHITECTURE.md` + `setup.md` + `README.md` snippet

**Branch:** `speaker-labeling/p3.4-architecture-and-readme`

**Why fourth:** The repo-level documentation (`README.md`, `ARCHITECTURE.md`, `docs/developer/setup.md`) is the discoverability layer that points new users at the runbook written in P3.3. P3.4 must come after P3.3 so the `README.md` "Speaker Labeling" paragraph can link to a runbook that already exists.

**Files touched:**
- `README.md` — add a short "Speaker labeling (local, opt-in)" section near the feature overview. One paragraph + a link to `docs/runbooks/speaker-labeling.md`. Do **not** duplicate the runbook content.
- `docs/ARCHITECTURE.md` — add a new subsection under the existing transcription pipeline description. Describe (a) the `ISpeakerEnrichmentService` orchestrator, (b) the `IPythonRuntime` abstraction and its two (or three, if P3.5 lands) implementations, (c) the sidecar process boundary and the versioned JSON contract, (d) how failures are isolated to `EnrichmentWarnings` without breaking the transcription-only pipeline. Reference the relevant ADR section numbers.
- `docs/developer/setup.md` — new subsection "Running `RequiresPython` tests locally": install Python 3.10+, install `pyannote.audio`, set the HF token, run `dotnet test --filter "Category=RequiresPython"`. Link to the runbook for the full first-run story.
- `docs/adr/024-local-speaker-labeling-pipeline.md` — flip `Status` to `Accepted` with a dated outcome note. This is a small amendment and is bundled into P3.4 so the ADR and the docs that describe it are updated together.

**"TDD" cycle for documentation changes:**

There are no automated tests here, but each of the three updated docs has a verification step that matches the red/green cycle:

1. **Red.** Draft the section. Assume nothing.
2. **Verify.** For `ARCHITECTURE.md`, grep the repo for every type name you mention and confirm it exists at the path you cite. For `setup.md`, walk through the commands verbatim on a machine where Python is not yet installed. For `README.md`, confirm the link resolves and the runbook anchor target exists.
3. **Green.** If any verification step fails, fix the doc to match reality.

**Manual walkthrough checklist (PR body must include all of these checked):**

- [ ] Every type name cited in `ARCHITECTURE.md` is grep-findable at the cited path.
- [ ] Every command in `setup.md` is executed verbatim on a clean machine and produces the stated outcome.
- [ ] The `README.md` link to `docs/runbooks/speaker-labeling.md` resolves on GitHub.
- [ ] `docs/adr/024-local-speaker-labeling-pipeline.md` is flipped to `Status: Accepted` with a dated note (`YYYY-MM-DD — Phase 3 shipped on Local-Speaker-Labeling branch, awaiting user promotion to master`).

**Local verification before PR:**
```
dotnet test VoxFlow.sln
```
Fully green. No code changes — the test run is a guard against accidental stray edits.

**PR description template:**
```
docs: architecture + setup + README entries for speaker labeling

Part of ADR-024 Phase 3. Adds repo-level discoverability for the
feature:
- README.md: one-paragraph overview + link to runbook
- docs/ARCHITECTURE.md: enrichment pipeline subsection covering
  ISpeakerEnrichmentService, IPythonRuntime, sidecar boundary,
  and failure isolation
- docs/developer/setup.md: instructions for running the
  Category=RequiresPython test suite locally
- docs/adr/024-local-speaker-labeling-pipeline.md: flipped to
  Status: Accepted with a dated outcome note

Every cited path and type name was verified against the current
repo; every command in setup.md was walked through on a clean
machine.

Test plan:
- [x] dotnet test VoxFlow.sln — fully green (no code changes)
- [x] Manual walkthrough of setup.md instructions
- [x] Every internal link resolves
```

---

### P3.5 — `StandaloneRuntime` implementation (**conditional**)

**Branch:** `speaker-labeling/p3.5-standalone-runtime`

**Conditional on:** the `python-build-standalone` spike outcome being **go**. If the spike is no-go or still open, this PR is **not** written — instead, a tiny "close the loop" PR lands that removes the `"Standalone"` value from `SpeakerLabelingOptions.pythonRuntimeMode` validation and documents the decision in the ADR. The contingency is:

- **Spike = go, spike complete:** write P3.5 as described below.
- **Spike = no-go:** skip P3.5. Add one PR (`speaker-labeling/p3.5-standalone-decision`) that removes `"Standalone"` from the accepted `pythonRuntimeMode` values, makes `SpeakerLabelingOptions` throw on load if the value is set to `"Standalone"` with a clear message ("Standalone mode is not supported in this release — see ADR-024 for the spike outcome"), updates ADR-024 with the decision, and updates the runbook's "First-run setup (Standalone mode)" section accordingly.
- **Spike not yet complete:** defer P3.5 past Phase 3. Phase 3 can still exit successfully without it — see Exit Criteria.

**Why fifth (when it runs):** Standalone runtime is an additive feature behind an already-existing abstraction (`IPythonRuntime`). It cannot destabilize the CLI, MCP, or Desktop host changes that already landed in P3.1–P3.4 because those paths default to `ManagedVenv`. Running P3.5 last means a spike outcome slipping does not block the rest of Phase 3.

**Files touched (go path):**
- `src/VoxFlow.Core/Services/Python/StandaloneRuntime.cs` — new implementation of `IPythonRuntime` that resolves its interpreter path from a bundled `python-build-standalone` tree, verifies the tree's existence and version, and produces a `PythonInvocation` pointing at the bundled interpreter.
- `src/VoxFlow.Core/Services/Python/StandaloneRuntimePaths.cs` — new path helper that knows the layout of the bundled tree for the target platform (initially macOS-only, mirroring the existing Desktop-only scope for v1).
- `src/VoxFlow.Core/DependencyInjection/VoxFlowCoreServiceCollectionExtensions.cs` — register `StandaloneRuntime` alongside `SystemPythonRuntime` and `ManagedVenvRuntime`; the resolver picks one based on `SpeakerLabelingOptions.PythonRuntimeMode`.
- `src/VoxFlow.Core/Configuration/SpeakerLabelingOptions.cs` — validation now accepts `"Standalone"` as a legal `PythonRuntimeMode` value.
- `tests/VoxFlow.Core.Tests/Services/Python/StandaloneRuntimeTests.cs` — new unit tests using the same `IProcessLauncher` fake pattern as `SystemPythonRuntimeTests` and `ManagedVenvRuntimeTests`. Covers: tree present and valid; tree missing; tree present but interpreter missing; version check parsing.

**TDD steps (go path):**

1. **Red.** Add `StandaloneRuntimeTests.EnsureReadyAsync_TreeMissing_ReturnsNotReady`. Fails — class doesn't exist.
2. **Green.** Create `StandaloneRuntime` with a constructor taking `StandaloneRuntimePaths`, `IProcessLauncher`, and a logger. Implement `EnsureReadyAsync` to return `NotReady` when the tree directory does not exist. Test passes.
3. **Red.** Add `StandaloneRuntimeTests.EnsureReadyAsync_TreePresentButInterpreterMissing_ReturnsNotReady`. Fails.
4. **Green.** Add the interpreter existence check. Passes.
5. **Red.** Add `StandaloneRuntimeTests.EnsureReadyAsync_VersionBelow310_ReturnsNotReady` — stub the process launcher so `python --version` returns `Python 3.9.6`. Fails.
6. **Green.** Parse the stdout, compare to the 3.10 floor. Passes. Reuse the same parser the existing `SystemPythonRuntime` uses — extract it into a shared helper if it's not already shared.
7. **Red.** Add `StandaloneRuntimeTests.EnsureReadyAsync_Ready_ReturnsReady_AndProducesCorrectInvocationPaths`. Fails.
8. **Green.** Produce a `PythonInvocation` whose `InterpreterPath` points inside the bundled tree and whose environment variables are populated (e.g., `PYTHONHOME`, `PYTHONPATH`) per the `python-build-standalone` README. Passes.
9. **Refactor.** Consolidate path constants into `StandaloneRuntimePaths`. Extract the version parser into `PythonVersionParser` if not already shared with `SystemPythonRuntime`. Keep tests green.
10. **Red.** Add a DI wiring test in `VoxFlowCoreServiceCollectionTests` (or the nearest equivalent) asserting that `SpeakerLabelingOptions { PythonRuntimeMode = "Standalone" }` resolves an `IPythonRuntime` implementation of type `StandaloneRuntime`. Fails.
11. **Green.** Wire the runtime into the DI extension. Passes.
12. **Red.** Add `SpeakerLabelingOptionsTests.PythonRuntimeMode_Standalone_IsAccepted`. Fails if the validator still rejects it.
13. **Green.** Expand the validator. Passes.
14. **Manual verification:** Download the `python-build-standalone` tree for the current platform into the expected bundle path. Enable the feature with `pythonRuntimeMode=Standalone`. Run a CLI transcription against `obama-speech-1spk-10s.wav`. Confirm the transcript has `Speaker A:` labels and the process tree shows the bundled interpreter (not the system Python).
15. **RequiresPython integration test:** Add a `[Trait("Category", "RequiresPython")]` test that end-to-end-drives `StandaloneRuntime` against the pyannote sidecar. This test is gated by both `Category=RequiresPython` **and** a file-existence check for the bundled tree, so it skips cleanly on machines without the bundle.

**Local verification before PR:**
```
dotnet test VoxFlow.sln
dotnet test --filter "Category=RequiresPython"
```
Both fully green.

**PR description template (go path):**
```
core: StandaloneRuntime implementation for speaker labeling

Part of ADR-024 Phase 3. Adds a third IPythonRuntime implementation
that runs pyannote against a bundled python-build-standalone tree.
Enabled by setting transcription.speakerLabeling.pythonRuntimeMode
to "Standalone" in appsettings.json. ManagedVenv remains the default
for new users.

Spike outcome (linked in ADR-024) was go: python-build-standalone
ships a usable CPython + numpy + torch stack under the repo size
budget for the Desktop scope of this delivery.

Test plan:
- [x] dotnet test VoxFlow.sln — fully green
- [x] StandaloneRuntimeTests cover tree-missing, interpreter-missing,
      version-below-floor, and happy-path invocation construction
- [x] DI wiring resolves StandaloneRuntime for pythonRuntimeMode=Standalone
- [x] Manual: full speaker-labeled transcription of obama-speech-1spk-10s
      using the bundled runtime, process-tree verified
- [x] dotnet test --filter "Category=RequiresPython" — fully green
      on a machine with Python 3.10+ and the bundle present
```

**PR description template (no-go path, for the `speaker-labeling/p3.5-standalone-decision` PR):**
```
docs(adr-024): record Standalone runtime spike as no-go

Part of ADR-024 Phase 3. The python-build-standalone spike concluded
no-go: [one-sentence reason, e.g., "bundled pyannote + torch exceeds
the acceptable repo/binary size budget"]. SpeakerLabelingOptions no
longer accepts "Standalone" as a legal pythonRuntimeMode; users are
limited to "ManagedVenv" (default) and "SystemPython" (escape hatch)
for this delivery. The runbook's "First-run setup (Standalone mode)"
section is updated to reflect this.

Test plan:
- [x] dotnet test VoxFlow.sln — fully green
- [x] SpeakerLabelingOptionsTests now asserts "Standalone" throws
      with a clear message pointing at ADR-024
```

---

### P3.6 — Release prep

**Branch:** `speaker-labeling/p3.6-release-prep`

**Why last:** Release prep is the final pass that ties off loose ends, verifies every acceptance criterion, and hands the branch over to the user. It must come after every other phase and every other Phase 3 sub-PR.

**Scope:**

1. **Acceptance criteria audit.** Walk the [README acceptance criteria](README.md#acceptance-criteria) one by one. For each item, link to the test or the manual verification that proves it. Items without evidence get a follow-up sub-PR **before** P3.6 merges — P3.6 is blocked until every item has evidence.
2. **`Acceptance Check Before Promotion` checklist.** Tick every item in the README's `## Acceptance Check Before Promotion` section. Do not tick items that have not actually been done.
3. **Full manual smoke test on a real recording.** Run the pipeline end-to-end against one of:
   - The full `artifacts/input/President Obama Speech.m4a` (long-form, single speaker — tests failure modes when diarization finds only one speaker).
   - A multi-speaker real-world recording the user has on hand (if available — not blocking).
4. **Test-log triage.** Run `dotnet test VoxFlow.sln` and `dotnet test --filter "Category=RequiresPython"` on a machine that has Python 3.10+ and pyannote installed. Capture the summary line in the PR body. Any new `[Skipped]` counts must be justified.
5. **Changelog entry.** Add an entry to `CHANGELOG.md` (if one exists) or to a new `docs/delivery/local-speaker-labeling/CHANGELOG-phase3.md` otherwise, summarizing what shipped across all three phases.
6. **Hand-off note for user review.** Add a dated "Ready for user promotion review" note to the bottom of the delivery README. This note lists the PR numbers that landed across the three phases and says "branch is ready for the user to review the full diff and decide whether to promote to master". Promotion itself is **not** done in this PR.

**"TDD" cycle for release prep:**

P3.6 is process, not code. The discipline is:

1. **Red.** Claim an acceptance criterion is met.
2. **Verify.** Link to the specific test or walkthrough that proves it.
3. **Green.** Only if the link resolves and the evidence is real.

No acceptance criterion is ticked without evidence. If evidence is missing, the correct response is to either add a test, run the walkthrough, or open a follow-up sub-PR — **not** to tick the box and move on.

**Files touched:**
- `docs/delivery/local-speaker-labeling/README.md` — "Acceptance Check Before Promotion" checklist fully ticked, with a "Ready for user promotion review" footer.
- `CHANGELOG.md` (or equivalent) — one entry per phase, grouped under the current release version or an explicit "Unreleased on Local-Speaker-Labeling" section.

**Local verification before PR:**
```
dotnet test VoxFlow.sln
dotnet test --filter "Category=RequiresPython"
```
Both fully green. The PR body must include the full summary line (`Passed: N, Failed: 0, Skipped: M`) from each run.

**PR description template:**
```
release: close Phase 3 and hand Local-Speaker-Labeling over for review

Part of ADR-024 Phase 3. Final pass:
- Acceptance criteria audited, each tied to a test or walkthrough
- Acceptance Check Before Promotion checklist fully ticked
- Full smoke test on <input file> — result: <pass/fail summary>
- CHANGELOG entries for Phases 0–3

Local-Speaker-Labeling is now ready for user review and promotion
decision. Promotion to master is a separate user-driven step and
is intentionally not part of this PR.

Test plan:
- [x] dotnet test VoxFlow.sln — fully green (<summary line>)
- [x] dotnet test --filter "Category=RequiresPython" — fully green
      on <machine description> (<summary line>)
- [x] Manual smoke: <input file> → labeled transcript verified
```

---

## Post-Phase-3 Checklist

Before declaring Phase 3 — and the delivery as a whole — complete, every box below must be ticked:

- [ ] P3.1 merged into `Local-Speaker-Labeling`. CLI `--speakers` flag works end-to-end.
- [ ] P3.2 merged into `Local-Speaker-Labeling`. MCP `enableSpeakers` parameter works end-to-end.
- [ ] P3.3 merged into `Local-Speaker-Labeling`. Runbook exists and has been clean-machine walked through.
- [ ] P3.4 merged into `Local-Speaker-Labeling`. README / ARCHITECTURE / setup docs updated and verified. ADR-024 flipped to `Accepted`.
- [ ] P3.5 resolved (go path merged, or no-go decision PR merged, or explicitly deferred past Phase 3 with a recorded reason).
- [ ] P3.6 merged into `Local-Speaker-Labeling`. Acceptance criteria audit complete.
- [ ] Full `dotnet test VoxFlow.sln` green locally on the integration branch.
- [ ] Full `dotnet test --filter "Category=RequiresPython"` green on a machine with Python 3.10+ and `pyannote.audio` installed.
- [ ] Manual smoke test run on at least one real-world recording (not just the 10-second fixtures).
- [ ] All 13 README acceptance criteria have evidence attached.
- [ ] `docs/delivery/local-speaker-labeling/README.md` footer says "Ready for user promotion review" with the landing PR numbers listed.
- [ ] User has been notified that `Local-Speaker-Labeling` is ready for review. Promotion to `master` is the user's decision and is **outside the scope of this delivery**.

When every box is ticked, Phase 3 is complete and the delivery is closed.
