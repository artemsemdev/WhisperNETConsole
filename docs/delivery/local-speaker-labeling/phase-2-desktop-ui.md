# Phase 2 — Desktop UI

**Parent:** [Local Speaker Labeling — Delivery Plan](README.md)
**ADR:** [ADR-024](../../adr/024-local-speaker-labeling-pipeline.md)
**Status:** Shipped on `speaker-labeling/phase-2-desktop-redesign` (PR #27 → `Local-Speaker-Labeling`). This document has been updated to match the implementation: P2.1 – P2.5 match the sub-PRs that landed; P2.6 (visual-system refresh) and P2.7 (sidecar packaging fix) are retrospective addenda covering work that shipped on the same branch but was not in the original plan.

---

## Goal

Expose the Phase 1 enrichment pipeline through the Mac Catalyst Desktop app: let users toggle speaker labeling from the Ready screen, watch transcription progress on a **three-ring phase tracker** that matches the CLI's stacked `Transcription / Diarization / Merge` layout, and read a colored speaker-labeled transcript on the completion screen. At the end of Phase 2, a Desktop user can enable the feature, transcribe a multi-speaker file, see phase-by-phase progress with per-phase elapsed clocks and status lines, and see turn-taking rendered with a colorblind-safe palette — without any CLI or MCP changes yet.

Phase 2 is a pure presentation phase: no new Core services, no new output formats, no config shape changes. It consumes the `TranscribeFileResult.SpeakerTranscript` and `EnrichmentWarnings` fields that Phase 1 already populates, reuses the `ProgressUpdate` stream the `CliProgressHandler` already bands into three phases, and persists the new Ready-screen toggle through the existing `DesktopConfigurationService.SaveUserOverridesAsync` mechanism (the same mechanism `SettingsPanel.razor` uses today for the output format picker).

## Exit Criteria

- The Ready-screen `SettingsPanel` has a new speaker-labeling toggle whose initial state is bound to `TranscriptionOptions.SpeakerLabeling.Enabled`.
- Toggling the switch persists back to `appsettings.json` via `DesktopConfigurationService.SaveUserOverridesAsync` under the `transcription.speakerLabeling.enabled` key.
- `AppViewModel.TranscribeFileAsync` forwards the toggle state to the `TranscribeFileRequest` via `EnableSpeakers` so the user's last toggle is honored even if the underlying config has not been reloaded.
- `RunningView` renders **three concentric phase rings** inside one 320×320 SVG container (outer = Transcription at `r=92`, middle = Diarization at `r=72`, inner = Merge at `r=52`). The center of the stack shows the **focus phase's** local `0..100%` percent plus a small phase label (`TRANSCRIPTION` / `DIARIZATION` / `MERGE`). Sub-status line (`"loading model"`, `"embeddings"`, `"writing output"`, …) and phase-local elapsed time live in a sibling **`.activity-panel`** card directly below the stack so the reader always sees a live heartbeat even when pyannote goes silent for minutes. Color tokens (`--phase-transcription` / `--phase-diarization` / `--phase-merge`) — see the P2.3 color table for the concrete hex values.
- Phase state transitions (`Idle → Running → Done`) are driven off `ProgressStage` via the same `ProgressPhase` banding used by `CliProgressHandler` (`Transcribing/LoadingModel/Converting/...` → Transcription, `Diarizing` → Diarization, `Writing/Complete` → Merge). When a phase transitions to `Done` its ring renders at 100 %, shows `done`, and its elapsed clock stops.
- The per-phase elapsed clock keeps ticking between sparse producer events via a 1 s view-model heartbeat (mirroring `CliProgressHandler.EnsureHeartbeatLocked`), so diarization doesn't look frozen during the multi-minute pyannote silence between sub-steps.
- When speaker labeling is **disabled**, the Diarization (middle) ring renders as `Skipped` (muted `--phase-skipped` color, no arc progression) rather than stuck at 0 %, so the user doesn't think the pipeline is hung.
- `CompleteView` renders the `TranscriptDocument` as colored speaker turns when `TranscriptionResult.SpeakerTranscript` is not null. When it is null (feature off, or sidecar failure), the view falls back to the existing plain-text preview — no regressions.
- Speaker colors come from the Okabe-Ito 8-color colorblind-safe palette. Speakers beyond the palette size wrap around modulo 8, and the cycle is documented inline so contributors know why.
- Enrichment warnings (`result.EnrichmentWarnings`) are visible to the user via a non-blocking info banner on the completion screen, matching the existing `message message-warning` styling.
- New Razor component tests cover the toggle, the three-phase tracker (phase transitions, local percent banding, elapsed heartbeat, skipped state), the colored transcript renderer, the warning banner, and the fallback-to-plain-text branch.
- `dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj` is fully green locally.
- The only Core change allowed in Phase 2 is the introduction of `ProgressPhaseBanding` (the shared `PhaseOf` / `LocalPercent` / `PhaseUpperBound` helpers extracted from `CliProgressHandler`) and the `ProgressPhase` enum that carries it. The CLI change is purely a delete-and-delegate: behavior is byte-identical, guarded by existing CLI tests. **No MCP files are touched in Phase 2.** No new Core services, no new output formats, no config shape changes.

## Pre-conditions

- Phase 1 is merged into `Local-Speaker-Labeling` and green on the integration branch.
- `TranscribeFileResult.SpeakerTranscript` and `EnrichmentWarnings` are populated by `TranscriptionService` when the flag is on.
- `DesktopConfigurationService` already supports `SaveUserOverridesAsync` with a `Dictionary<string, object>` — see [SettingsPanel.razor](../../../src/VoxFlow.Desktop/Components/Shared/SettingsPanel.razor) for the canonical usage pattern.
- **Sidecar packaging contract.** The Intel Mac Catalyst CLI-bridge path resolves `voxflow_diarize.py` at `AppContext.BaseDirectory/python/voxflow_diarize.py`, which at runtime is `<app>/Contents/MonoBundle/cli/python/voxflow_diarize.py`. [VoxFlow.Desktop.csproj](../../../src/VoxFlow.Desktop/VoxFlow.Desktop.csproj)'s `CopyBundledCliBridge` target must copy `python/**/*` from the CLI output alongside the DLLs. Regression-covered by [DesktopCliBundleTests](../../../tests/VoxFlow.Desktop.Tests/DesktopCliBundleTests.cs).

## Non-goals

- CLI `--speakers` flag (Phase 3).
- MCP `enableSpeakers` tool parameter (Phase 3).
- Runbook and long-form user documentation (Phase 3).
- Editing, renaming, or re-assigning speakers (explicitly out of scope for this delivery — see [README.md](README.md) "Out of Scope").
- Custom palettes, dark-mode variants beyond what the existing Desktop CSS already supports.

---

## TDD Sequence

Five sub-PRs planned in strict order, plus one retrospective addendum (P2.6) documenting the design-system refresh that shipped alongside P2.3 in the same integration branch.

Conventions for every PR below:
- **Branch:** `speaker-labeling/p2.M-<slug>` off `Local-Speaker-Labeling`.
- **Base for PR:** `Local-Speaker-Labeling`.
- **Before `gh pr create`:** run `dotnet test VoxFlow.sln` locally. The Desktop test suite does not spawn a Python sidecar, so there is no separate `RequiresPython` command to run for Phase 2 PRs unless the PR touches the orchestrator path (which it should not).
- **Test tagging:** Razor component tests live in `VoxFlow.Desktop.Tests` and use the existing infrastructure under `tests/VoxFlow.Desktop.Tests/Infrastructure`. No new trait is introduced.
- **Commit authorship:** user only; no Co-Authored-By trailers.
- **PR body:** no "Generated with Claude Code" footer.
- **Disabled-path invariant:** every PR must keep existing Desktop UI tests green without edits. If a test requires a touch-up, re-examine whether the new code is accidentally leaking into the disabled path. The one exception is P2.3's replacement of the single organic-progress SVG with the three-ring stack — existing `RunningView` tests that asserted on the old SVG must be re-pointed at the new component, and the migration must be explicit in the PR body.

---

### P2.1 — Ready-screen speaker labeling toggle

**Branch:** `speaker-labeling/p2.1-ready-toggle`

**Why first:** The toggle is the user's entry point into the feature; until it exists, the rest of the Desktop experience is unreachable without hand-editing `appsettings.json`. It is also the smallest self-contained UI change, so it sets the testing pattern for the next three PRs.

**Files touched (new):**
- `src/VoxFlow.Desktop/Components/Shared/SpeakerLabelingToggle.razor` — a component encapsulating the toggle switch, its label, and an explanatory subtitle (`"Identify who spoke each segment"`).
- `tests/VoxFlow.Desktop.Tests/Components/SpeakerLabelingToggleTests.cs`

**Files touched (modified):**
- `src/VoxFlow.Desktop/Components/Shared/SettingsPanel.razor` — add the new component below the format picker with `IsDisabled="IsDisabled"`.
- `src/VoxFlow.Desktop/ViewModels/AppViewModel.cs` — add `public bool SpeakerLabelingEnabled { get; set; }`, initialized from `options.SpeakerLabeling.Enabled` in the same place `SelectedResultFormat` is initialized (`LoadOptionsAsync` or equivalent). Raise `OnPropertyChanged` on set if the view model uses change notification; otherwise follow the existing `SelectedResultFormat` pattern verbatim.
- `src/VoxFlow.Desktop/Services/DesktopConfigurationService.cs` — extend `SaveUserOverridesAsync` if needed so it can accept nested keys like `"speakerLabeling.enabled"`, then write to the `transcription.speakerLabeling.enabled` path in the JSON. If the existing implementation already supports dotted keys (check before editing), use it as-is. If it flattens to a single `transcription.X` layer, add a small extension branch that recognizes `speakerLabeling` as a nested object and merges instead of overwriting.

**Toggle component shape:**
```razor
@* SpeakerLabelingToggle.razor *@
@inject AppViewModel ViewModel
@inject DesktopConfigurationService ConfigService

<div class="speaker-toggle" id="speaker-labeling-toggle" aria-label="Speaker labeling toggle">
    <div class="speaker-toggle-body">
        <div class="speaker-toggle-labels">
            <span class="speaker-toggle-title">Speaker labeling</span>
            <span class="speaker-toggle-subtitle">Identify who spoke each segment</span>
        </div>
        <button role="switch"
                id="speaker-labeling-switch"
                aria-checked="@(ViewModel.SpeakerLabelingEnabled ? "true" : "false")"
                class="speaker-toggle-switch @(ViewModel.SpeakerLabelingEnabled ? "on" : "off")"
                disabled="@IsDisabled"
                @onclick="Toggle">
            <span class="speaker-toggle-thumb"></span>
        </button>
    </div>
</div>

@code {
    [Parameter] public bool IsDisabled { get; set; }

    private async Task Toggle()
    {
        if (IsDisabled) return;
        ViewModel.SpeakerLabelingEnabled = !ViewModel.SpeakerLabelingEnabled;
        try
        {
            await ConfigService.SaveUserOverridesAsync(new Dictionary<string, object>
            {
                ["speakerLabeling.enabled"] = ViewModel.SpeakerLabelingEnabled
            });
        }
        catch
        {
            // Persistence is best-effort; in-memory state wins the current session.
        }
    }
}
```

**TDD steps:**

1. **Red.** `SpeakerLabelingToggleTests.InitialState_MatchesViewModel`. Render the component under a test host with `ViewModel.SpeakerLabelingEnabled = false`; assert `aria-checked="false"`. Compile fails: component doesn't exist.
2. **Green.** Create the component with the markup above (minus the toggle logic) and wire the parameter. Test passes.
3. **Red.** `Click_Toggles_ViewModelState`. Click the switch; assert `ViewModel.SpeakerLabelingEnabled == true`.
4. **Green.** Implement the `Toggle` handler.
5. **Red.** `Click_PersistsOverride_ViaDesktopConfigurationService`. Use a fake `DesktopConfigurationService` that records calls; click; assert it was called once with `speakerLabeling.enabled = true`.
6. **Green.** Pass a fake via the existing test DI setup (see `DesktopUiComponentTests` for the pattern).
7. **Red.** `Click_PersistenceFailure_DoesNotCrash_AndKeepsInMemoryState`. Fake throws; click; assert no exception bubbles up and the view-model flag is still set.
8. **Green.** Wrap the save in try/catch.
9. **Red.** `Disabled_PreventsClicks_AndToggling`. Render with `IsDisabled=true`; click; assert state unchanged and `disabled` attribute is present.
10. **Green.** Already handled by the early return.
11. **Red.** `AppViewModelTests.SpeakerLabelingEnabled_InitializesFromOptions`. Load options with `SpeakerLabeling.Enabled=true`; assert the view-model flag matches after `LoadOptionsAsync`.
12. **Green.** Initialize in the same helper that sets `SelectedResultFormat`.
13. **Red.** `SettingsPanelTests.RendersSpeakerLabelingToggle_BelowFormatPicker`. Render the parent panel; assert the new component is present with the correct element id.
14. **Green.** Add the `<SpeakerLabelingToggle />` markup at the bottom of `SettingsPanel.razor`.
15. **Refactor.** If `DesktopConfigurationService.SaveUserOverridesAsync` needed an adjustment to handle nested keys, add a dedicated unit test for that helper under `DesktopConfigurationTests.cs`.

**Local verification:**
```
dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj
dotnet test VoxFlow.sln
```

**PR description template:**
```
Add speaker labeling toggle to Ready-screen settings panel

New SpeakerLabelingToggle.razor component rendered below the format picker.
Reflects transcription.speakerLabeling.enabled from appsettings.json on load
and persists user changes via DesktopConfigurationService.SaveUserOverridesAsync.
AppViewModel.SpeakerLabelingEnabled is the single source of truth for the
current session and will be forwarded to TranscribeFileRequest.EnableSpeakers
in P2.2.

Test plan:
- [x] dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj — green
- [x] dotnet test VoxFlow.sln — green, no regressions in other projects
```

---

### P2.2 — Forward toggle to `TranscribeFileRequest`

**Branch:** `speaker-labeling/p2.2-forward-toggle`

**Why second:** Once the toggle exists it must actually reach the pipeline. This is a tiny, surgical change that we ship on its own because it's a prerequisite for anything that depends on `ProgressStage.Diarizing` being emitted (the three-ring progress screen in P2.3 can't be manually tested without it). Keeping it as a separate PR means the three-ring work below doesn't have to re-review request-wiring logic.

**Files touched (modified):**
- `src/VoxFlow.Desktop/ViewModels/AppViewModel.cs` — in `TranscribeFileAsync`, change `new TranscribeFileRequest(filePath)` to pass `EnableSpeakers: SpeakerLabelingEnabled ? true : null` as the sixth positional argument. The null fallback means the config-file default still applies when the toggle is off — identical to the user manually setting the flag in JSON.
- `tests/VoxFlow.Desktop.Tests/AppViewModelTests.cs` — new tests for the request forwarding.

**TDD steps:**

1. **Red.** `AppViewModelTests.TranscribeFileAsync_SpeakerLabelingEnabled_PassesEnableSpeakersTrue`. Use the existing `FakeTranscriptionService` from the Desktop test infra; record the received request; assert `request.EnableSpeakers == true`. Compile fails because AppViewModel currently constructs a 1-arg request.
2. **Green.** Pass the sixth positional argument `EnableSpeakers: SpeakerLabelingEnabled`. Use `SpeakerLabelingEnabled ? true : null` so the off-toggle falls back to config. Test passes.
3. **Red.** `TranscribeFileAsync_SpeakerLabelingDisabled_PassesEnableSpeakersNull`. Assert the field is `null`, not `false`, so the config default still wins.
4. **Green.** Already handled by the ternary.

**Local verification:**
```
dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj
```

**PR description template:**
```
Forward speaker labeling toggle to TranscribeFileRequest

AppViewModel.TranscribeFileAsync now passes EnableSpeakers based on the
Ready-screen toggle. When the toggle is off, EnableSpeakers stays null so
the config-file default wins — no accidental override.

Test plan:
- [x] dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj — green
```

---

### P2.3 — Three-ring phase progress on `RunningView`

**Branch:** `speaker-labeling/p2.3-three-ring-progress`

**Why third:** Today the Running screen is a single 200×200 SVG that shows one overall percent with one message and one elapsed clock. When speaker labeling is enabled, the overall percent stalls at 90 % for the multi-minute pyannote run and the user has no way to tell what's actually happening, because `ProgressStage.Diarizing` and `ProgressStage.Writing` both roll up into the same bar. The CLI already solved this with three per-phase bars; Phase 2 brings the same model to Desktop.

The change is presentation-only — it subscribes to the exact same `ProgressUpdate` stream and re-uses the `ProgressPhase` banding already shipped in `CliProgressHandler` (`PhaseOf`, `LocalPercent`, `PhaseUpperBound`). That logic is extracted into a shared internal helper so Desktop and CLI stay bit-for-bit consistent instead of silently drifting.

**Files touched (new):**
- `src/VoxFlow.Core/Models/ProgressPhase.cs` — shared enum `{ Transcription, Diarization, Merge }` used by both the CLI and Desktop. Lives under Core because both hosts depend on Core.
- `src/VoxFlow.Core/Models/ProgressPhaseBanding.cs` — shared static helper exposing `PhaseOf(ProgressStage)`, `LocalPercent(ProgressStage, double)`, and `PhaseUpperBound(ProgressStage)`. The existing private methods on `CliProgressHandler` are deleted and the CLI consumes this helper instead.
- `src/VoxFlow.Desktop/ViewModels/PhaseProgressTracker.cs` — an `INotifyPropertyChanged` view-model layer sitting between `AppViewModel.CurrentProgress` and `RunningView`. Exposes an immutable `IReadOnlyList<PhaseState> Phases` where `PhaseState` is a record `(ProgressPhase Phase, PhaseStatus Status, double LocalPercent, string? SubStatus, TimeSpan Elapsed)`. Status is `{ Idle, Running, Done, Skipped, Failed }`. Runs a heartbeat (via `TimeProvider`) identical in shape to `CliProgressHandler`'s so `Elapsed` keeps advancing between producer events; timer is disposed the moment the terminal `Complete`/`Failed` frame arrives.
- `src/VoxFlow.Desktop/Components/Pages/PhaseRingStack.razor` — **the only ring component `RunningView` actually embeds.** Renders all three phase arcs concentrically inside one SVG (radii `92 / 72 / 52`, viewBox `0 0 200 200`, stroke 6). The center shows the focus-phase percent + phase label; it does **not** render per-ring sub-status or elapsed (those live in the sibling `.activity-panel`).
- `src/VoxFlow.Desktop/Components/Pages/PhaseRing.razor` — **auxiliary single-ring component** authored during early iterations (radius 50, its own arc math) but ultimately not rendered on RunningView after the concentric redesign. Kept in-tree with its own tests because it exercises the same `PhaseState` / `PhaseStatus` contract and guards against view-model regressions; may be reused by future single-ring affordances (e.g. a mini-tracker in the menu bar).
- `src/VoxFlow.Desktop/wwwroot/css/app.css` — phase tokens (`--phase-transcription`, `--phase-diarization`, `--phase-merge`, `--phase-track`, `--phase-skipped`, `--phase-failed`), concentric `.phase-ring-stack` container, `.phase-track` / `.phase-arc` strokes, `.phase-center` sphere (96×96 radial-gradient via `::before`), and `.activity-panel` card. Rules are concatenated into the existing app-wide stylesheet; no separate `phase-ring.css` file was added.
- `tests/VoxFlow.Desktop.Tests/Components/PhaseProgressTrackerTests.cs`
- `tests/VoxFlow.Desktop.Tests/Components/PhaseRingTests.cs` — covers the auxiliary single-ring component.
- `tests/VoxFlow.Desktop.Tests/Components/PhaseRingStackTests.cs` — covers the concentric contract (three arcs in phase order, `r=92/72/52`, per-phase color tokens, dash-offset from `LocalPercent`, `--phase-skipped` for skipped Diarization, center `.phase-center-percent` / `.phase-center-label`).
- `tests/VoxFlow.Core.Tests/Models/ProgressPhaseBandingTests.cs`

**Files touched (modified):**
- `src/VoxFlow.Core/ConsoleProgress/CliProgressHandler.cs` — delete the private `PhaseOf`/`LocalPercent`/`PhaseUpperBound` helpers, delegate to `ProgressPhaseBanding`. Zero behavior change; the existing CLI tests guard the contract.
- `src/VoxFlow.Desktop/Components/Pages/RunningView.razor` — replace the single organic-progress SVG block with a `<PhaseRingStack Tracker="@ViewModel.PhaseTracker" />` component. Keep the `cancel` button and the top-level progress-info caption (`"Starting transcription..."`, file name) unchanged.
- `src/VoxFlow.Desktop/ViewModels/AppViewModel.cs` — construct the `PhaseProgressTracker` on init, feed `ProgressUpdate` events into it from the existing progress pipeline, and expose it as `public PhaseProgressTracker PhaseTracker { get; }`. `CurrentProgress` stays for the caption but the ring state comes from the tracker.

**Color palette (shipped in `app.css`; design traded CLI ANSI parity for the concentric mockup's outer-to-inner purple → cyan → green gradient):**

| Ring | Token | Hex | Rationale |
|---|---|---|---|
| Transcription (outer, `r=92`) | `--phase-transcription` | `#a78bfa` | Purple; outermost ring leads the eye inward in the concentric layout. Replaces the original cyan from the early stacked-strip mockup. |
| Diarization (middle, `r=72`) | `--phase-diarization` | `#4db8ff` | Cyan/blue; middle ring, distinct from both the outer purple and inner green under CVD. Replaces the original magenta. |
| Merge (inner, `r=52`) | `--phase-merge` | `var(--success)` (`#28c840`) | Green; reuses the existing `--success` token. |
| Idle track | `--phase-track` | `rgba(255,255,255,0.06)` | Near-invisible white wash; lets the concentric ring set feel embedded in the dark surface rather than sitting on a visible groove. |
| Skipped ring | `--phase-skipped` | `var(--drop-zone-border)` (`#4a4a6a`) | Communicates "off by design" without looking like a stall. |
| Failed arc | `--phase-failed` | `var(--error)` (`#ff5f57`) | Matches the app-wide error token used by `FailedView` and existing error banners. |

*Note on CLI divergence:* the CLI still uses cyan/magenta/green (ANSI 96/95/92). Desktop's concentric stack deliberately remaps the outer/middle hues to purple/cyan so the rings read as a single cohesive gradient rather than three competing accents. `ProgressPhaseBanding` remains shared, so the phase *mapping* (stage → phase) is bit-for-bit identical across hosts — only the color presentation diverges.

**`PhaseProgressTracker` contract:**

- Default state on construction: three `PhaseState` entries, all `Idle`, `LocalPercent = 0`, `Elapsed = 0`.
- On `OnProgress(ProgressUpdate update)`:
  - Determine `newPhase = ProgressPhaseBanding.PhaseOf(update.Stage)`.
  - For every phase strictly before `newPhase` still marked `Running` or `Idle`: mark `Done`, set `LocalPercent = 100`, freeze `Elapsed` at its current value. This handles the "pyannote never emitted a closing 100 % frame" case the CLI already works around.
  - For `newPhase`: if currently `Idle`, set `Status = Running` and record `StartedAtUtc = DateTime.UtcNow`. Update `LocalPercent = ProgressPhaseBanding.LocalPercent(update.Stage, update.PercentComplete)`, `SubStatus = update.Message ?? StageSubLabel(update.Stage)`, `Elapsed = DateTime.UtcNow - StartedAtUtc`.
  - For phases strictly after `newPhase`: leave as-is (typically still `Idle`).
- On the terminal `Complete` frame: mark Merge `Done` at 100 %, mark any still-`Running` phase `Done` at 100 %, stop the heartbeat.
- On `Failed`: mark the current phase `Failed`, stop the heartbeat.
- Skipped phases: when `speakerLabeling` is off, the producer never emits `Diarizing`. The tracker **does not** pre-mark Diarization as `Skipped` — it only knows what the producer says. To surface `Skipped`, `AppViewModel` passes `speakerLabelingEnabled` into the tracker constructor; when `false`, the Diarization entry is born `Skipped` and the tracker never transitions it.
- Heartbeat: `System.Threading.Timer` ticks every 1 s, projects the `Running` phase's `Elapsed = DateTime.UtcNow - StartedAtUtc`, and raises `PropertyChanged` on `Phases`. Matches `CliProgressHandler.OnHeartbeat` beat-for-beat.

**`PhaseRingStack.razor` rendering contract (the concentric stack `RunningView` actually uses):**

- One SVG, `viewBox="0 0 200 200"`, rendered inside a 320×320 CSS container with an ambient blue/cyan radial-gradient glow layered via `.phase-ring-stack::before` and a `drop-shadow` filter on the SVG itself (matches the desktop redesign mockup).
- **Three tracks** (full circles) at `r=92/72/52`, each with `stroke: var(--phase-track)` and `stroke-width: 6`.
- **Three arcs** at the same radii, one per phase, each with `stroke: var(--phase-{slug})` and rounded caps. Arc length is driven by `stroke-dasharray = 2πr` + `stroke-dashoffset = 2πr · (1 - localPct/100)`, so `LocalPercent` from `PhaseProgressTracker` drives each arc independently. A subtle `phase-arc-breathe` animation on `.phase-arc-running` keeps the live arc visually alive during pyannote's silent stretches.
- **Center** is a 96×96 dark radial-gradient sphere (`.phase-center::before`) holding the focus-phase's percent (38 px, `.phase-center-percent`) + unit (`%`) on one baseline-aligned row, with the focus phase label (`TRANSCRIPTION` / `DIARIZATION` / `MERGE`) stacked underneath in `.phase-center-label` (uppercase tracking). Focus-phase selection: `Failed → Running → last Done → first Idle`.
- **Per-phase states** (apply to each arc independently):
  - `idle` — arc not emitted; only the track is visible.
  - `running` — arc drawn at `circumference · (1 - localPct/100)`, breathing animation on.
  - `done` — arc drawn at 100 %, no animation.
  - `skipped` — arc uses `--phase-skipped`; no animation, no clock. The center never focuses a skipped phase.
  - `failed` — arc uses `--phase-failed` with a `drop-shadow` halo; the center shows `!` + `FAILED` label via `FocusPercent` / `FocusLabel`.
- **Sub-status and elapsed live outside the stack**, in the sibling `.activity-panel` card below. The card shows a color-pulsed dot (`.activity-pulse-{phase}` class, phase-tinted via `--phase-{slug}`) plus a single human-readable line synthesized from the focus phase's `SubStatus` + `mm:ss` elapsed (`RunningView.BuildActivityMessage`). This is what the user watches while pyannote is grinding.

**`PhaseRing.razor` rendering contract (auxiliary single-ring component; not on RunningView today):**

- SVG with `radius=50`, one track circle + one arc circle, center showing `LocalPercent` + phase-dependent glyph, footer with `SubStatus` + `mm:ss` elapsed.
- Same `PhaseState` / `PhaseStatus` contract as the stack so either layout stays interchangeable.
- Tests in `PhaseRingTests.cs` cover: `idle` = track only, no arc, no elapsed; `running` = animated arc at `circumference · (1 - localPct/100)`; `done` = full arc in phase color with frozen clock; `skipped` = track-only in `--phase-skipped`, no clock; `failed` = full arc in `--phase-failed` with static clock.

**TDD steps:**

1. **Red.** `ProgressPhaseBandingTests.PhaseOf_MapsKnownStagesToCorrectPhase`. Table-driven over all `ProgressStage` members. Compile fails: helper doesn't exist.
2. **Green.** Extract `PhaseOf` from `CliProgressHandler` into `ProgressPhaseBanding`; re-point the CLI to it. CLI tests must stay green.
3. **Red.** `ProgressPhaseBandingTests.LocalPercent_RemapsOverallToBand`. Parameterized: `(Transcribing, 45%)` → `50%`; `(Diarizing, 92.5%)` → `50%`; `(Writing, 97.5%)` → `50%`. Compile fails until the helper exposes `LocalPercent`.
4. **Green.** Move `LocalPercent` across.
5. **Red.** `PhaseProgressTrackerTests.NewTracker_AllIdle_AllZero`. Construct with `speakerLabelingEnabled=true`; assert three phases `Idle`, percent 0, elapsed 0.
6. **Green.** Implement the constructor.
7. **Red.** `OnProgress_TranscribingFrame_MovesTranscriptionToRunning`. Push `{Stage=Transcribing, Percent=45%}`; assert Transcription = `Running`, local percent ≈ 50, sub-status "transcribing", elapsed > 0.
8. **Green.** Implement the "first frame starts the phase" branch.
9. **Red.** `OnProgress_DiarizingAfterTranscribing_MarksTranscriptionDone`. Push Transcribing then Diarizing; assert Transcription flips to `Done` at 100 %, elapsed frozen.
10. **Green.** Implement the "finalize earlier phases" branch.
11. **Red.** `OnProgress_TerminalCompleteFrame_MarksAllRunningDone`. Push Transcribing then Complete; assert all three phases end `Done`.
12. **Green.** Implement the terminal handler.
13. **Red.** `OnProgress_Failed_MarksCurrentPhaseFailed`. Push Transcribing then Failed; assert Transcription = `Failed`.
14. **Green.** Implement the failure handler.
15. **Red.** `Tracker_WithSpeakerLabelingDisabled_DiarizationBornSkipped`. Construct with `speakerLabelingEnabled=false`; assert Diarization = `Skipped` from the start and never flips.
16. **Green.** Wire the constructor parameter.
17. **Red.** `Heartbeat_TickAdvancesElapsed_WhileRunning`. Inject a `TimeProvider` fake (or reuse the existing test-time pattern); push Transcribing; advance 2 s without another `OnProgress`; assert Transcription's elapsed is ≥ 2 s.
18. **Green.** Implement the timer with `TimeProvider` so tests don't sleep.
19. **Red.** `Heartbeat_StopsOnTerminal`. Push Complete; advance 2 s; assert elapsed didn't move.
20. **Green.** Dispose the timer in the terminal branch.
21. **Red.** `PhaseRingTests.Idle_RendersTrackOnlyAndNoClock`. Render with `Status=Idle`; assert no arc `<circle class="phase-arc">` appears and no `.phase-elapsed` element.
22. **Green.** Implement the idle branch.
23. **Red.** `Running_RendersArcProportionalToLocalPercent`. LocalPercent=25; assert the `stroke-dashoffset` equals circumference × 0.75 ± 0.01.
24. **Green.** Implement the arc math (mirror `RunningView`'s existing formula).
25. **Red.** `Done_RendersFullArcAndFrozenClock`. Status=Done, Elapsed=4:20; assert arc dashoffset=0 and clock shows `4:20`.
26. **Green.** Implement the done branch.
27. **Red.** `Skipped_RendersInSkippedColorNoClock`. Assert the arc stroke is `var(--phase-skipped)` and no `.phase-elapsed` element.
28. **Green.** Implement the skipped branch.
29. **Red.** `PhaseRingStackTests.RendersThreeRingsInTranscriptionDiarizationMergeOrder`. Assert the three child components in correct DOM order with the correct phase tokens.
30. **Green.** Implement the stack layout.
31. **Red.** `RunningViewTests.EmbedsPhaseRingStack_NotOrganicProgress`. Assert the new component is rendered and the old `.organic-progress-wrapper` element is gone.
32. **Green.** Replace the block.
33. **Red.** `RunningViewTests.CancelButton_StillWorks`. Regression: click cancel; assert `ViewModel.CancelTranscription` was called once.
34. **Green.** Should pass untouched; added as a regression guard.
35. **Refactor.** Pull shared SVG math (circumference, dashoffset) into a private helper used by both `PhaseRing` and any future ring variants.

**Local verification:**
```
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj
dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj
dotnet test VoxFlow.sln
```

**Manual verification:**
- Launch Desktop, drop an audio file, enable speaker labeling → three rings appear, Transcription fills, then Diarization, then Merge. Each ring's clock shows only its own phase elapsed.
- Disable speaker labeling, drop a file → Diarization ring renders in `--phase-skipped` color with "skipped" label. Transcription and Merge still run.
- Leave Desktop running during pyannote's embeddings step → Diarization clock keeps ticking every second; no freeze.

**PR description template:**
```
Render three-ring phase progress on Desktop RunningView

Replaces the single organic-progress SVG on RunningView with a stack of
three rings (Transcription / Diarization / Merge) mirroring the CLI's
phase-banded progress layout. Each ring shows its own local percent,
sub-status, and phase-local elapsed clock; clocks freeze when the phase
transitions to Done.

The CLI's PhaseOf / LocalPercent / PhaseUpperBound helpers are promoted
from CliProgressHandler's private members to a shared
Core.Models.ProgressPhaseBanding helper so Desktop and CLI stay
bit-for-bit consistent. CLI behavior is unchanged.

When speaker labeling is disabled, the Diarization ring renders as
"skipped" (muted color, no clock) so the user can see at a glance which
phases the pipeline will run.

Test plan:
- [x] dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj — green (CLI-shared helper)
- [x] dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj — green
- [x] dotnet test VoxFlow.sln — green, no regressions
- [x] Manual: rings tick through Transcription → Diarization → Merge on a multi-speaker file
- [x] Manual: Diarization ring shows Skipped when toggle is off
```

---

### P2.4 — Colored speaker transcript renderer

**Branch:** `speaker-labeling/p2.4-colored-renderer`

**Why fourth:** The renderer is the largest piece of net-new UI work on the CompleteView side and the most testable. It must handle the full range of document shapes (0 speakers, 1 speaker, N speakers), wrap the palette at 8 speakers, and fall back cleanly to the existing plain-text preview when no document is present. Shipping it as its own PR lets the palette and wrapping logic be reviewed in isolation.

**Files touched (new):**
- `src/VoxFlow.Desktop/Components/Shared/SpeakerTranscriptView.razor` — renders a `TranscriptDocument` as a vertical list of colored turns.
- `src/VoxFlow.Desktop/Theme/OkabeItoPalette.cs` (or similar path — check whether Desktop already has a theme folder; if not, place under `src/VoxFlow.Desktop/Services`) — the 8-color palette and a `ColorForSpeaker(SpeakerInfo)` helper.
- `src/VoxFlow.Desktop/wwwroot/css/speaker-transcript.css` — or extend the existing stylesheet; define CSS variables for the palette, a `.speaker-turn` class, and a `.speaker-label` class. If the Desktop app already concatenates CSS in a single file, append there instead of adding a new file.
- `tests/VoxFlow.Desktop.Tests/Components/SpeakerTranscriptViewTests.cs`
- `tests/VoxFlow.Desktop.Tests/Theme/OkabeItoPaletteTests.cs`

**Palette definition:**

The [Okabe-Ito colorblind-safe palette](https://jfly.uni-koeln.de/color/) contains eight colors that remain distinguishable under all common forms of color vision deficiency. They are:

| Index | Name | Hex |
|---|---|---|
| 0 | Orange | `#E69F00` |
| 1 | Sky Blue | `#56B4E9` |
| 2 | Bluish Green | `#009E73` |
| 3 | Yellow | `#F0E442` |
| 4 | Blue | `#0072B2` |
| 5 | Vermillion | `#D55E00` |
| 6 | Reddish Purple | `#CC79A7` |
| 7 | Black | `#000000` |

`ColorForSpeaker` maps the first-appearance ordinal (`0 → A`, `1 → B`, ...) to an index modulo 8. Speakers beyond the palette size reuse colors from the start — this is documented in a one-line comment next to the palette definition. The ordinal is derived from `SpeakerInfo.Label` by indexing into the alphabet: `A=0`, `B=1`, ..., `Z=25` (consistent with `SpeakerMergeService.OrdinalLabel`), then taken modulo 8.

**Renderer contract:**

- When `Document` is `null`, render nothing (the parent view decides whether to show a fallback).
- When `Document.Turns` is empty, render a subdued message `"No speaker segments detected."`.
- Otherwise render one block per turn: a small colored swatch, the `Speaker {Label}` name bold in the speaker color, the joined word text, and a timestamp range `[00:03 – 00:12]` in the corner.
- Styling is scoped to the component via CSS variables so the palette can be themed later without a rewrite.

**TDD steps:**

1. **Red.** `OkabeItoPaletteTests.ColorForSpeaker_FirstEightSpeakers_ReturnsDistinctColors`. Create 8 `SpeakerInfo` instances with labels `A..H`; assert `ColorForSpeaker` returns 8 distinct hex strings from the palette. Compile fails: type doesn't exist.
2. **Green.** Create the palette array and the helper.
3. **Red.** `ColorForSpeaker_NinthSpeaker_WrapsToFirstColor`. Label `I` (ordinal 8); assert the returned color equals the palette[0].
4. **Green.** `ordinal % palette.Length`. Test passes.
5. **Red.** `ColorForSpeaker_InvalidLabel_ThrowsArgumentException`. Label `"zz"` or empty; assert `ArgumentException`. This locks the contract: the palette does not silently accept garbage labels, because a silent failure here would mean all speakers unexpectedly share a color.
6. **Green.** Validate the label against the `[A-Z]+` pattern; throw with a descriptive message.
7. **Red.** `SpeakerTranscriptViewTests.Renders_NullDocument_ProducesEmptyMarkup`. Render with `Document=null`; assert the component output is empty (zero non-whitespace children).
8. **Green.** Guard the top of the render block on `Document is not null`.
9. **Red.** `Renders_EmptyTurns_ShowsNoSegmentsMessage`. Document with 0 turns.
10. **Green.** Add the empty-state branch.
11. **Red.** `Renders_SingleSpeakerTurn_HasCorrectLabel_AndColor`. One turn, speaker A; assert the turn is rendered with `data-speaker-label="A"` and an inline `style="color: #E69F00"` matching palette[0].
12. **Green.** Iterate `Document.Turns` and emit one block per turn. Apply the color via `style` attribute (not CSS class) so tests can read it by value.
13. **Red.** `Renders_TwoSpeakers_AlternatingTurns_HaveDifferentColors`. Four turns A-B-A-B; assert the speaker colors alternate.
14. **Green.** Already handled by iterating and calling the palette per turn.
15. **Red.** `Renders_TimestampRange_PerTurn`. Assert each turn contains a `[MM:SS – MM:SS]` range. The formatting helper `FormatRange(startTime, endTime)` is private to the component.
16. **Green.** Implement the formatter; cover 0-padding and the en-dash.
17. **Red.** `Renders_NineSpeakers_ColorsWrapAroundPalette`. Nine turns with speakers A..I; assert the first and ninth have the same color.
18. **Green.** Already handled by the modulo helper.
19. **Refactor.** Extract the render helper into a small `SpeakerTurnRow` sub-component so the test assertions can target one row at a time and the top-level view stays a clean foreach.

**Local verification:**
```
dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj
```

**PR description template:**
```
Add SpeakerTranscriptView renderer with Okabe-Ito palette

New Desktop component renders a TranscriptDocument as a list of colored
speaker turns. Colors come from the 8-entry Okabe-Ito colorblind-safe
palette mapped by speaker ordinal, wrapping at 8 speakers. Fallback cases
(null document, empty turns) render empty or a subdued message. Component
is not yet wired into CompleteView — that lands in P2.5.

Test plan:
- [x] dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj — green
```

---

### P2.5 — CompleteView integration + enrichment warning banner

**Branch:** `speaker-labeling/p2.5-complete-view`

**Why fifth (last):** This is the smallest change once the renderer, the toggle, and the three-ring tracker exist: swap the plain-text preview for the colored view when a document is present and add a warning banner for non-empty `EnrichmentWarnings`. Shipping it last means the rest of Phase 2 can be reviewed without the CompleteView churn, and users only see the finished experience in one step.

**Files touched (modified):**
- `src/VoxFlow.Desktop/Components/Pages/CompleteView.razor` — replace the plain-text preview block with a conditional: if `result.SpeakerTranscript is not null`, render `<SpeakerTranscriptView Document="result.SpeakerTranscript" />` inside the existing `complete-transcript-section` wrapper; otherwise render the existing plain-text preview markup unchanged. Add a new `EnrichmentWarningsBanner` block above the transcript section that shows each warning from `result.EnrichmentWarnings` in a `message message-info` style row.
- `tests/VoxFlow.Desktop.Tests/DesktopUiComponentTests.cs` — extend the existing component tests with the new branches.

**TDD steps:**

1. **Red.** `CompleteViewTests.NullDocument_RendersPlainTextPreview_Unchanged`. Regression guard: render CompleteView with `TranscriptionResult.SpeakerTranscript = null` and an existing `TranscriptPreview`; assert the preview is present and no `SpeakerTranscriptView` is rendered.
2. **Green.** Add the conditional around the existing preview markup so the null branch keeps the old behavior bit-for-bit.
3. **Red.** `NonNullDocument_RendersSpeakerTranscriptView_AndHidesPlainTextPreview`. Assert the plain preview is **not** rendered when a document is present.
4. **Green.** Swap in the `SpeakerTranscriptView` for the non-null case.
5. **Red.** `EnrichmentWarnings_RendersBanner_WithEachMessage`. Two warnings on the result; assert both render in the banner.
6. **Green.** Add the banner. Use the existing `.message .message-info` CSS class for visual consistency.
7. **Red.** `EnrichmentWarnings_EmptyList_DoesNotRenderBanner`. Assert no banner element appears.
8. **Green.** Guard with `result.EnrichmentWarnings.Count > 0`.
9. **Red.** `DocumentAndWarningsBoth_RendersBothSections`. Both present; banner first, then colored transcript.
10. **Green.** Should already pass given the previous two changes.
11. **Regression.** Run the full Desktop suite including `DesktopUiComponentTests` — every existing test must stay green with zero edits.

**Local verification:**
```
dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj
dotnet test VoxFlow.sln
```

**PR description template:**
```
Render colored speaker transcript + enrichment warnings on CompleteView

CompleteView now swaps its plain-text preview for SpeakerTranscriptView
when TranscriptionResult.SpeakerTranscript is not null. When it is null
(feature off, or sidecar failure), the existing plain preview renders
unchanged — verified by regression tests that make zero edits to existing
assertions. EnrichmentWarnings are rendered in a non-blocking
message-info banner above the transcript section.

Test plan:
- [x] dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj — green
- [x] dotnet test VoxFlow.sln — green, no regressions
```

---

### P2.6 — Visual-system refresh (retrospective addendum)

**Status:** Shipped alongside P2.3 on `speaker-labeling/phase-2-desktop-redesign` (PR #27). Not a separate branch — documented here so future readers see that the "Desktop redesign" commits were part of Phase 2 scope, not undocumented drift.

**Why this exists:** P2.3's three-ring stack forced a broader conversation about the Running screen's visual language — the old single-organic-circle aesthetic didn't scale cleanly to "three rings + a live heartbeat line". Rather than bolt the new ring stack onto the legacy chrome and accept a visual seam, the chrome around the rings was refreshed in lockstep: top bar, drop zone, settings panel, font stack, and the broader palette all moved to a single coherent dark-UI direction (purple-accented, Inter + JetBrains Mono, bottom-sheet Settings). The three-ring stack and the chrome were designed to read as one piece, so they were implemented as one piece.

**What shipped (by commit on the branch):**

- `986e1d9 — style(desktop): shift color palette + load Inter/JetBrains Mono fonts.` New font stack (Inter for UI, JetBrains Mono for numerics/timestamps) loaded from local `wwwroot/fonts/`. Palette shifted to dark purple-accented surfaces so the concentric ring gradient reads as intentional rather than out-of-place against the old Logic-Pro blue chrome.
- `a78799c — feat(desktop): add TopBar component and bottom-sheet Settings shell.` New [`TopBar.razor`](../../../src/VoxFlow.Desktop/Components/Shared/TopBar.razor) (shared across Ready / Running / Complete / Failed views) carrying the app title and a settings trigger. Settings moved from an inline Ready-screen card into a bottom-sheet shell so the speaker toggle and format picker don't crowd the drop zone.
- `f0fb6c6 — style(desktop): restyle drop zone as 180x180 purple pad with two outer rings.` [`DropZone.razor`](../../../src/VoxFlow.Desktop/Components/Shared/DropZone.razor) now renders a 180×180 purple landing pad surrounded by two concentric outer rings, visually rhyming with the three-ring progress stack on `RunningView`.
- `bcf6548 — style(desktop): resize pill switch and segment the format picker into 5 columns.` The speaker-labeling pill switch and the output-format picker in `SettingsPanel` were reshaped into a consistent segmented-control style so they read as one family of controls in the bottom sheet.
- `67f2e33 — feat(desktop): replace three horizontal phase rings with nested concentric arcs.` **The actual P2.3 pivot.** The originally-planned three-vertically-stacked rings were replaced with three concentric arcs in one SVG. The `PhaseState` / `PhaseProgressTracker` contract was preserved, so the view-model side of P2.3 needed no rewrite — only `PhaseRingStack.razor` and the CSS tokens changed. `PhaseRing.razor` remains in-tree as an auxiliary component (see P2.3 Files-touched note).
- `d48fdcc — feat(desktop): add Activity panel with pulsing dot and phase-aware caption.` New `.activity-panel` card directly below the concentric ring stack. Renders a phase-tinted pulsing dot (`--phase-transcription` / `--phase-diarization` / `--phase-merge`) plus a synthesized `"<sub-status> · <mm:ss>"` line keyed off the focus phase. This is the surface that replaces the per-ring sub-status/elapsed footers from the original P2.3 plan — one shared heartbeat line for the focus phase instead of three parallel ones.
- `75bc2a6 — feat(desktop): wire shared TopBar into CompleteView and FailedView.` `TopBar` now sits above every top-level view for consistent chrome.
- `eeb2f75 — fix(desktop): anchor TopBar at top and center running-body in column.` Layout fix: `.page-column { flex: 1 0 auto; min-height: 100%; }` + `.running-body { flex: 1 1 auto; justify-content: center; }` so the TopBar stays docked and the ring stack sits visually centered in the remaining vertical space.
- `2588617 — fix(desktop): match mockup sizing for concentric phase rings.` Container 240×240 → 320×320, SVG viewBox 240 → 200 (so `r=92` fills ~92 % instead of 77 %), stroke 10 → 6, center sphere 80×80 → 96×96 (drawn via `.phase-center::before` radial gradient), percent 30 → 38 px. Activity panel reshaped into a full-width bg-secondary card to match the `.activity` card in the mockup.

**What did NOT follow strict TDD:** the visual-refresh commits were primarily CSS + markup restructuring driven by a live mockup ([`artifacts/design/running-screen.html`](../../../artifacts/design/running-screen.html) and a subsequent iteration owned by the user). Regression coverage comes from the P2.3 component tests (ring radii, phase tokens, dash-offset math, skipped state, percent/label assertions), the `DesktopUiComponentTests` that guard `progressbar` ARIA contracts, and the packaging test added in P2.7-adjacent work (see below). New visual affordances that *were* behavior-bearing (Activity panel's phase-pulse class, focus-phase label, skipped-state rendering) are covered directly in `PhaseRingStackTests` / `DesktopUiComponentTests` rather than through a standalone P2.6 TDD sequence.

**Files touched (summary):**

- `src/VoxFlow.Desktop/Components/Shared/TopBar.razor` (new)
- `src/VoxFlow.Desktop/Components/Shared/DropZone.razor` (restyled)
- `src/VoxFlow.Desktop/Components/Shared/SettingsPanel.razor` (moved into bottom-sheet shell, segmented controls)
- `src/VoxFlow.Desktop/Components/Pages/RunningView.razor` (composes `TopBar` + `.running-body` + `PhaseRingStack` + `.activity-panel`)
- `src/VoxFlow.Desktop/Components/Pages/CompleteView.razor` and `FailedView.razor` (adopt `TopBar`)
- `src/VoxFlow.Desktop/Components/Pages/PhaseRingStack.razor` (concentric rewrite)
- `src/VoxFlow.Desktop/wwwroot/css/app.css` (palette shift, new phase tokens, `.activity-panel`, `.page-column`, `.running-body`, sphere `::before`, drop-zone ring chrome)
- `src/VoxFlow.Desktop/wwwroot/fonts/` (Inter + JetBrains Mono)
- `artifacts/design/running-screen.html` (design source of truth — do not ship; referenced from this doc and P2.3)

---

### P2.7 — Mac Catalyst sidecar packaging fix (retrospective)

**Status:** Shipped on `speaker-labeling/phase-2-desktop-redesign` (PR #27, commit [`47bec69`](../../../src/VoxFlow.Desktop/VoxFlow.Desktop.csproj)). Root cause sat dormant through Phase 1 and only surfaced once P2.1's toggle let a user actually request diarization from the Desktop app.

**The bug:** The Intel Mac Catalyst build path spawns a bundled `VoxFlow.Cli.dll` child process. That child resolves `voxflow_diarize.py` via `ServiceCollectionExtensions.ResolveSidecarScriptPath()` as `{AppContext.BaseDirectory}/python/voxflow_diarize.py`, which at runtime is `<app>/Contents/MonoBundle/cli/python/voxflow_diarize.py`. The CLI project's `None Include` / `Link="python\voxflow_diarize.py"` correctly copies the script into `bin/<Configuration>/net9.0/python/` during the CLI build, but `CopyBundledCliBridge` in [`VoxFlow.Desktop.csproj`](../../../src/VoxFlow.Desktop/VoxFlow.Desktop.csproj) was globbing only `*.dll`, `*.deps.json`, `*.runtimeconfig.json`, `ggml-metal.metal`, and `runtimes/macos-*/**/*` — the `python/` subtree was silently dropped. At runtime the sidecar exited with code 2 (Python `[Errno 2] No such file or directory`), surfaced to the user as `speaker-labeling: process-crashed`.

**The fix (TDD):**

1. **Red.** `DesktopCliBundleTests.MonoBundleCli_IncludesPyannoteSidecarScript` + `MonoBundleCli_IncludesPythonRequirementsTxt` — locate the most-recently-built `.app` bundle (rank by `VoxFlow.Cli.dll` mtime so stale Release/arm64 outputs don't mask a fresh Debug/x64 build) and assert both files exist under `Contents/MonoBundle/cli/python/`. Tests initially fail; `SkippableFact` skip path kicks in when no bundle has been built (e.g., CI without the maccatalyst workload).
2. **Green.** Add a `BundledCliSidecarFiles` ItemGroup (`python/**/*` off the CLI bin dir) and a second `Copy` step in `CopyBundledCliBridge` that preserves the `python/` subdirectory under `MonoBundle/cli/`. `Xunit.SkippableFact 1.4.13` added to `VoxFlow.Desktop.Tests.csproj` (already used in `VoxFlow.Core.Tests`).
3. **Verify.** Debug rebuild produces `MonoBundle/cli/python/voxflow_diarize.py` and `python-requirements.txt`; Desktop suite 145/2 skipped green; full CLI + MCP suites green.

**Files touched:**

- `src/VoxFlow.Desktop/VoxFlow.Desktop.csproj` — new `BundledCliSidecarFiles` ItemGroup + second `Copy` step in `CopyBundledCliBridge`.
- `tests/VoxFlow.Desktop.Tests/DesktopCliBundleTests.cs` (new) — bundle-contents regression tests.
- `tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj` — add `Xunit.SkippableFact` reference.

---

## Post-Phase-2 Checklist

Before declaring Phase 2 complete and moving to Phase 3 planning:

- [ ] All planned sub-PRs (P2.1 – P2.5) plus the retrospective addenda (P2.6 visual refresh, P2.7 sidecar packaging fix) are merged into `Local-Speaker-Labeling`.
- [ ] `dotnet test VoxFlow.sln` on `Local-Speaker-Labeling` is fully green.
- [ ] Manual smoke on a real Mac Catalyst build: toggle on, transcribe the Obama clip, confirm the colored transcript renders on CompleteView with `Speaker A:` turns in the first palette color.
- [ ] Manual smoke: toggle on, transcribe a multi-speaker file, confirm the concentric ring stack advances outer → middle → inner (Transcription → Diarization → Merge), the focus-phase percent in the center tracks the active ring, the sibling Activity panel pulses in the matching phase color, and CompleteView speakers beyond index 0 have distinct palette colors.
- [ ] Manual smoke: toggle off, transcribe any file, confirm the Diarization (middle) ring renders as Skipped, and CompleteView renders the plain-text preview exactly as before (no colored view, no warning banner).
- [ ] Manual smoke: leave Desktop running through pyannote's embeddings step on a 5+ minute file; confirm the Activity panel's `mm:ss` keeps ticking every second (no freeze) via the heartbeat, and the Diarization arc is breathing (not flat-lined).
- [ ] Manual smoke: force a sidecar failure (point `modelId` at a non-existent model), toggle on, transcribe; confirm the warning banner shows `speaker-labeling: …` and the plain-text preview still renders because `SpeakerTranscript` is null.
- [ ] Okabe-Ito palette is documented in a one-line comment inside `OkabeItoPalette.cs` linking to the canonical source.
- [ ] `ProgressPhaseBanding` is consumed by both `CliProgressHandler` and `PhaseProgressTracker`; no duplicate banding logic survives on either side.
- [ ] No files under `src/VoxFlow.Cli` or `src/VoxFlow.McpServer` were touched in this phase **for behavior changes**. The sidecar bundling fix (P2.7) edits `src/VoxFlow.Desktop/VoxFlow.Desktop.csproj` only — CLI / MCP stay untouched.
- [ ] Built `.app` bundle contains `Contents/MonoBundle/cli/python/voxflow_diarize.py` and `Contents/MonoBundle/cli/python/python-requirements.txt` (verified by `DesktopCliBundleTests` and by running speaker labeling end-to-end against a real audio file).
- [ ] User has reviewed the integration branch state and approved starting Phase 3.
