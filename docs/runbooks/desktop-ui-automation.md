# Desktop UI Automation Runbook

How to run the real macOS Desktop UI automation suite in `tests/VoxFlow.Desktop.UiTests`.

This suite launches the actual Mac Catalyst app bundle and drives the desktop window through macOS Accessibility plus the native Open dialog. It is separate from the headless Razor/component tests in `tests/VoxFlow.Desktop.Tests`.

## Prerequisites

- macOS
- Built Desktop app bundle
- `ffmpeg` available on `PATH`
- `models/ggml-base.bin` present locally
- Accessibility permission granted in `System Settings > Privacy & Security > Accessibility`
  - Allow your terminal app, IDE, and the `dotnet`/`osascript` host that runs the tests
- `VoxFlow.Desktop` should not already be running before the suite starts

## Build the Desktop App

Build for the current Mac architecture:

```bash
dotnet build src/VoxFlow.Desktop/VoxFlow.Desktop.csproj -f net9.0-maccatalyst
```

## Run the UI Automation Suite

Using the environment variable directly:

```bash
VOXFLOW_RUN_DESKTOP_UI_TESTS=1 \
dotnet test tests/VoxFlow.Desktop.UiTests/VoxFlow.Desktop.UiTests.csproj
```

Or use the helper script:

```bash
./scripts/run-desktop-ui-tests.sh
```

## Helper Script Output

The helper script prints timing for both the Desktop build step and the UI test step, and writes a timestamped `trx` report plus timing logs under:

```
artifacts/test-results/desktop-ui/<utc-timestamp>/
```

Typical files in that directory:

- `desktop-ui-<utc-timestamp>.trx`
- `build.time.txt`
- `test.time.txt`
- `progress.log`

During execution, the helper script prints live progress lines to the console with prefixes:

- `[ui-progress]` — active scenario steps (app launch, waiting for window, file dialog, result screen)
- `[heartbeat]` — long-running but healthy waits

This makes it easier to distinguish between active steps, healthy waits, and likely hangs (where only the heartbeat continues and no new UI progress appears).

## Targeting a Specific App Bundle

Set `VOXFLOW_DESKTOP_UI_APP_PATH` when you want the suite to target a specific built `.app` bundle instead of the default `Debug/net9.0-maccatalyst/<rid>/VoxFlow.Desktop.app`:

```bash
VOXFLOW_DESKTOP_UI_APP_PATH=/path/to/VoxFlow.Desktop.app \
VOXFLOW_RUN_DESKTOP_UI_TESTS=1 \
dotnet test tests/VoxFlow.Desktop.UiTests/VoxFlow.Desktop.UiTests.csproj
```

## Current Test Coverage

- App starts and renders the main ready screen
- Primary `Browse Files` action works against the native file picker
- Single-file happy path reaches the final result screen
- Transcript copy action updates the system clipboard
- Corrupt/invalid audio drives the real failure screen and recovery flow
- Repeated sequential processing works without restarting the app

## Diagnostics on Failure

When a test fails, the suite captures:

- Full-screen screenshot under `artifacts/ui-tests/<timestamp>-<scenario>/diagnostics/`
- Accessibility snapshot of the active app window
- Captured Desktop process log

## Limitations

- Drag-and-drop still requires manual verification against the built `.app`
- The suite exercises the `Browse Files` path only; other file-entry paths are not automated
- Current UI scope is single-file transcription
