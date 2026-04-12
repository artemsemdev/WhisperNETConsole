# VoxFlow Desktop XCUITests

Apple XCTest / XCUIAutomation UI tests for VoxFlow Desktop (Mac Catalyst).

This is a **separate, opt-in** project that lives alongside the existing .NET UI tests
in `tests/VoxFlow.Desktop.UiTests/`. Both projects test the same app using different
automation approaches.

## Prerequisites

- macOS 15.0+
- Xcode 15.4+ with command-line tools (`xcode-select --install`)
- VoxFlow.Desktop app built:
  ```bash
  dotnet build src/VoxFlow.Desktop -c Debug
  ```
- Sample audio files present: `artifacts/Input/Test 1.m4a`
- Whisper model present: `models/ggml-base.bin`
- macOS Accessibility access granted to Terminal / Xcode
  (System Settings > Privacy & Security > Accessibility)

## How to run

### Shell script (simplest)

```bash
VOXFLOW_RUN_DESKTOP_UI_TESTS=1 ./tests/VoxFlow.Desktop.XcUiTests/run-tests.sh
```

### xcodebuild (direct)

```bash
VOXFLOW_RUN_DESKTOP_UI_TESTS=1 \
xcodebuild test \
  -project tests/VoxFlow.Desktop.XcUiTests/VoxFlowXcUiTests.xcodeproj \
  -scheme VoxFlowXcUiTests \
  -destination 'platform=macOS'
```

### Xcode IDE

1. Open `tests/VoxFlow.Desktop.XcUiTests/VoxFlowXcUiTests.xcodeproj`
2. Edit scheme > Test > Arguments > Environment Variables:
   add `VOXFLOW_RUN_DESKTOP_UI_TESTS` = `1`
3. Press **Cmd+U** to run tests

## Environment variables

| Variable | Required | Description |
|---|---|---|
| `VOXFLOW_RUN_DESKTOP_UI_TESTS` | **Yes** (`1`) | Opt-in gate. Tests skip via `XCTSkip` if not set. |
| `VOXFLOW_DESKTOP_UI_APP_PATH` | No | Override path to `VoxFlow.Desktop.app`. Auto-discovered from build output if unset. |
| `VOXFLOW_REPO_ROOT` | No | Override repository root. Auto-detected from source file location if unset. |

## How this stays out of the default test flow

1. **Not a .NET project** — `dotnet test` never discovers it.
2. **Opt-in env var** — tests skip with `XCTSkip` unless `VOXFLOW_RUN_DESKTOP_UI_TESTS=1`.
3. **Separate Xcode project** — only runs when explicitly targeted via `xcodebuild test -project ... -scheme ...`.
4. **No CI integration** — not referenced in any CI workflow.

## Architecture

```
VoxFlow.Desktop.XcUiTests/
├── VoxFlowXcUiTests.xcodeproj/   # Xcode project (scheme inside)
├── DummyApp/
│   └── DummyApp.swift             # Minimal host app (required by XCUITest infra)
├── VoxFlowXcUiTests/
│   ├── VoxFlowHappyPathTests.swift  # The end-to-end test
│   └── TestEnvironment.swift        # Paths, config injection, CGEvent helpers
├── run-tests.sh                   # Convenience wrapper
└── README.md
```

The **DummyApp** target exists only because Xcode's UI Testing Bundle requires an
associated app target. The actual app under test is `VoxFlow.Desktop.app`, launched
via `XCUIApplication(url:)` in the test code.

## Differences from the .NET UI tests

| Aspect | .NET UiTests | XCUITests (this project) |
|---|---|---|
| Framework | xUnit + AppleScript + custom bridge | XCTest + XCUIAutomation |
| Language | C# | Swift |
| UI interaction | File-based JSON bridge + osascript | XCUIElement queries + CGEvent |
| File picker | AppleScript `key code` | CGEvent `CGKeyCode` |
| WebView access | Custom DOM snapshot bridge | Native accessibility tree |
| Config injection | Same mechanism | Same mechanism |

## Known limitations

- **WKWebView accessibility** — XCUITest sees web content through the native
  accessibility tree. Elements with `aria-label` (like "Browse Files",
  "Copy Transcript") are visible as `buttons["Label"]`. If the accessibility
  tree does not expose a specific element, the test will fail with a clear
  message saying which element was not found.

- **Keyboard layout** — File-picker shortcuts (Cmd+Shift+G, Cmd+V) use
  `CGEvent` with hardware key codes for input-language independence (same
  approach as the .NET tests after the Russian-layout fix). Requires
  Accessibility access for the test runner process.

- **Desktop-only** — these are real GUI tests. They cannot run in Docker,
  headless, or remote environments.
