# Frontend Architecture

> How the VoxFlow Desktop UI is structured, rendered, and connected to the shared Core pipeline.

## Technology Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| UI framework | Blazor Hybrid (WebView) | Razor components rendered in WKWebView via MAUI |
| Host framework | .NET MAUI (Mac Catalyst) | Targets `net9.0-maccatalyst` for both arm64 and x64 |
| State management | MVVM with `INotifyPropertyChanged` | `AppViewModel` drives all view transitions |
| Styling | Single CSS file with custom properties | Dark theme; no component-scoped CSS |
| JavaScript interop | Minimal (drop zone + clipboard) | `wwwroot/js/interop.js` — 3 functions |

## Component Hierarchy

```
Routes.razor
└── MainLayout.razor
    └── switch (ViewModel.CurrentState)
        ├── ReadyView.razor
        │   └── DropZone.razor
        ├── RunningView.razor
        ├── FailedView.razor
        └── CompleteView.razor
```

## Razor Components

| Component | Location | Responsibility |
|-----------|----------|----------------|
| `Routes` | `Components/Routes.razor` | Root entry point. Calls `AppViewModel.InitializeAsync()` on first render. Displays a startup error screen with a Retry button if initialization throws; otherwise renders `MainLayout`. |
| `MainLayout` | `Components/Layout/MainLayout.razor` | Layout shell. Switches on `AppViewModel.CurrentState` to render the active view. Subscribes to `PropertyChanged` and calls `InvokeAsync(StateHasChanged)` to re-render on state changes. |
| `ReadyView` | `Components/Pages/ReadyView.razor` | Initial state. Shows app title, validation warning banner (when blocking failures exist), and the `DropZone` for file selection. `Browse Files` and drop zone are disabled when `CanStart` is false. |
| `RunningView` | `Components/Pages/RunningView.razor` | Processing state. Renders an SVG circular progress indicator with animated blobs and a spinning arc. Shows progress percentage, stage message, elapsed time, and current language. Includes a Cancel button. |
| `FailedView` | `Components/Pages/FailedView.razor` | Error state. Displays the error message. Provides Retry (same file) and Choose Different File buttons. |
| `CompleteView` | `Components/Pages/CompleteView.razor` | Success state. Shows detected language, transcript preview with truncation indicator, and action buttons: Copy Text, Open Folder, New Transcription. |
| `DropZone` | `Components/Shared/DropZone.razor` | File input. Handles HTML5 drag-and-drop via JS interop and native Mac file picker via `MacFilePicker`. Validates file type and size. |

## Navigation Model

The app uses **state-driven conditional rendering**, not traditional page routing.

```
Ready ──[file selected]──→ Running ──[success]──→ Complete
  ↑                          │                      │
  │                      [cancel]                   │
  │                          │               [new transcription]
  └──────────────────────────┴──────────────────────┘
                             │
                         [failure]
                             ↓
                          Failed ──[retry]──→ Running
                             │
                      [choose different]
                             ↓
                           Ready
```

State transitions are triggered by calling `AppViewModel` methods:

| User Action | Component | ViewModel Method | New State |
|-------------|-----------|-----------------|-----------|
| Select file (browse or drop) | ReadyView / DropZone | `TranscribeFileAsync(path)` | Running |
| Cancel transcription | RunningView | `CancelTranscription()` | Ready |
| Retry same file | FailedView | `RetryAsync()` | Running |
| Choose different file | FailedView | `GoToReady()` | Ready |
| New transcription | CompleteView | `GoToReady()` | Ready |

## State Management

### AppViewModel

`AppViewModel` (`ViewModels/AppViewModel.cs`) is the single source of UI state. It implements `INotifyPropertyChanged` and exposes:

| Property | Type | Purpose |
|----------|------|---------|
| `CurrentState` | `AppState` enum | Drives which view is rendered (Ready, Running, Failed, Complete) |
| `CurrentProgress` | `ProgressUpdate` | Progress percentage, stage name, message, elapsed time, language |
| `TranscriptionResult` | `TranscribeFileResult` | Success status, warnings, transcript preview, output path |
| `ValidationResult` | `ValidationResult` | Startup validation checks for the warning banner |
| `ErrorMessage` | `string` | Error text displayed in FailedView |
| `CanStart` | `bool` | Guard: true only when state is Ready and no blocking validation failures |

### Data Flow

```
Core Pipeline ──[IProgress<ProgressUpdate>]──→ BlazorProgressHandler
                                                    │
                                          MainThread.BeginInvokeOnMainThread()
                                                    │
                                                    ↓
                                              AppViewModel.CurrentProgress = value
                                                    │
                                              PropertyChanged event
                                                    │
                                                    ↓
                                          MainLayout.InvokeAsync(StateHasChanged)
                                                    │
                                                    ↓
                                            RunningView re-renders
```

`BlazorProgressHandler` (`Services/BlazorProgressHandler.cs`) implements `IProgress<ProgressUpdate>` and marshals progress updates from the background transcription thread to the MAUI UI thread via `MainThread.BeginInvokeOnMainThread()`.

## MAUI Shell

| File | Role |
|------|------|
| `App.xaml` / `App.xaml.cs` | Application root. Creates a single window titled "VoxFlow". Receives `MainPage` via DI. |
| `MainPage.xaml` / `MainPage.xaml.cs` | ContentPage containing a `BlazorWebView`. Hosts `Routes.razor` as the root Blazor component at the `#app` selector. Configures native drag-and-drop integration. |
| `MauiProgram.cs` | Composition root. Registers Core services via `AddVoxFlowCore()`, adds Desktop-specific services (`DesktopConfigurationService`, `ResultActionService`, `BlazorProgressHandler`, `DesktopDiagnostics`, `MacFilePicker`), conditionally replaces `ITranscriptionService` with `DesktopCliTranscriptionService` on Intel. |

## File Drop Implementation

File input uses three parallel mechanisms for maximum compatibility:

### 1. HTML5 Drag-and-Drop (JS Interop)

`DropZone.razor` calls `voxFlowInterop.initDropZone(elementId, inputId)` on first render. The JS handler attaches `drop`, `dragover`, `dragenter`, `dragleave` listeners, assigns dropped files to a hidden `<InputFile>` element, and toggles the `drag-over` CSS class for visual feedback. Respects `aria-disabled` when `CanStart` is false.

### 2. MAUI DropGestureRecognizer

`MainPage.xaml.cs` attaches `DropGestureRecognizer` to the root layout and BlazorWebView. Handles `DragOver` (accepts copy) and `Drop` events, then calls `ViewModel.TranscribeFileAsync(filePath)` directly.

### 3. Mac Catalyst Native UIDropInteraction

Under `#if MACCATALYST`, `MainPage.xaml.cs` attaches `UIDropInteraction` with a `NativeFileDropDelegate` to WKWebView and its ScrollView. Supports multiple UTI type identifiers (`public.file-url`, `com.apple.file-url`, `public.audio`, etc.) for drag sources. Resolves file paths from `NSItemProvider` and validates against supported audio formats.

**Supported audio formats:** `.m4a`, `.wav`, `.mp3`, `.aac`, `.flac`, `.ogg`, `.aif`, `.aiff`, `.mp4`, `.m4b`

Browser-dropped files are staged into a temp directory (`voxflow-drop-{guid}/`) before transcription. Native drops resolve the file path directly.

## Styling

All styles live in a single file: `wwwroot/css/app.css` (958 lines).

### Theme

Dark theme with CSS custom properties:

| Token | Value | Purpose |
|-------|-------|---------|
| `--bg-primary` | Dark background | App background |
| `--bg-surface` | Elevated surface | Cards, panels |
| `--accent` | `#4a4aff` | Primary action color |
| `--success` | `#28c840` | Completion state |
| `--warning` | `#febc2e` | Warning banner |
| `--error` | `#ff5f57` | Error state |

No light mode is implemented. The dark theme is designed to match the aesthetic of professional audio tools (Logic Pro, Audacity).

### Animations

| Animation | Where used |
|-----------|-----------|
| `ripple-wave` | DropZone idle state |
| `blob-pulse` | RunningView organic progress blobs |
| `organic-spin`, `organic-orbit` | RunningView spinning arc |
| `done-pulse`, `success-pop`, `check-pop` | CompleteView success feedback |
| `complete-enter` | CompleteView entrance |

### No Component-Scoped CSS

All component styles are in `app.css`, scoped by class naming conventions. No `.razor.css` isolation files are used.

## JavaScript Interop

`wwwroot/js/interop.js` provides three functions under `window.voxFlowInterop`:

| Function | Purpose |
|----------|---------|
| `initDropZone(elementId, inputId)` | Initializes HTML5 drag-and-drop for the drop zone element |
| `openInFinder(path)` | Placeholder; actual Finder integration is in C# via `ResultActionService` |
| `copyToClipboard(text)` | Copies text via `navigator.clipboard.writeText()` with `document.execCommand` fallback |

## Icons and Assets

- **Icons:** All SVG, inline in Razor components. Monochrome, colored via `stroke="currentColor"` and `fill="currentColor"`.
- **Fonts:** OpenSans-Regular.ttf, registered in `MauiProgram.cs`.
- **No image files.** No separate icon assets.

## Platform-Specific Code

### Mac Catalyst Conditionals

`MainPage.xaml.cs` uses `#if MACCATALYST` guards for:
- Native `UIDropInteraction` attachment to WKWebView
- `NativeFileDropDelegate` implementation (`UIDropInteractionDelegate`)
- `NSItemProvider` file path resolution

`#if DEBUG && MACCATALYST` guards:
- `DesktopUiAutomationHost` — HTTP bridge for UI test automation (not shipped in Release)

### Intel Mac Catalyst CLI Bridge

On `maccatalyst-x64`, `MauiProgram.cs` replaces the default `ITranscriptionService` with `DesktopCliTranscriptionService`, which launches `VoxFlow.Cli` as a local subprocess for transcription. Progress is communicated via structured JSON lines on stderr (enabled by `VOXFLOW_PROGRESS_STREAM=1`), parsed by `DesktopCliSupport.TryParseProgressUpdate()`.

## What Is Deliberately Excluded

- **No Blazor routing.** Single-page state machine; no `@page` directives or `NavigationManager`.
- **No light mode.** Dark theme only; appropriate for a professional audio tool.
- **No settings UI.** Configuration is file-based (`~/Library/Application Support/VoxFlow/appsettings.json`).
- **No batch UI.** Batch processing exists in Core but is not surfaced in the Desktop workflow.
- **No MCP integration in Desktop.** MCP is a separate stdio host.
