# Developer Setup Guide

This guide covers prerequisites, repository bootstrap, configuration, building, running, and testing VoxFlow locally.

All commands assume your shell is running from the repository root.

## Solution Structure

| Project | Role | Target |
|---|---|---|
| `src/VoxFlow.Core` | Shared transcription pipeline, configuration, validation, model handling, output writing | `net9.0` |
| `src/VoxFlow.Cli` | Thin CLI host over `VoxFlow.Core` | `net9.0` |
| `src/VoxFlow.McpServer` | Stdio MCP host over `VoxFlow.Core` | `net9.0` |
| `src/VoxFlow.Desktop` | MAUI Blazor Hybrid macOS desktop app | `net9.0-maccatalyst` |

Test projects:

| Project | Purpose |
|---|---|
| `tests/VoxFlow.Core.Tests` | Core unit tests |
| `tests/VoxFlow.Cli.Tests` | CLI end-to-end and regression tests |
| `tests/VoxFlow.McpServer.Tests` | MCP configuration and path-policy tests |
| `tests/VoxFlow.Desktop.Tests` | Desktop view-model, configuration, headless Razor UI/component, and UI integration tests |
| `tests/VoxFlow.Desktop.UiTests` | Real macOS desktop UI automation against the built `.app` bundle |

## Prerequisites

| Dependency | Required For | Notes |
|---|---|---|
| .NET SDK 9 | All hosts | `dotnet restore`, `build`, `run`, and `test` |
| `ffmpeg` | CLI, Desktop, MCP transcription | Must be on `PATH` or configured via `ffmpegExecutablePath` |
| Writable `models/` directory | CLI, Desktop, MCP transcription | Model is reused if present; downloaded if missing |
| macOS + Xcode command-line tools | Desktop | Required for Mac Catalyst builds |
| `maui-maccatalyst` workload | Desktop | Install with `dotnet workload install maui-maccatalyst` |

Notes:

- Runtime transcription is local, but the first model download requires network access unless you place the GGML model file manually.
- The Desktop project targets both `maccatalyst-x64` and `maccatalyst-arm64`, and defaults to the current `dotnet` process architecture.
- Apple Silicon Desktop runs the shared Core transcription pipeline in-process.
- Intel Mac Catalyst Desktop uses a local CLI bridge for transcription so the UI follows the same working path as `VoxFlow.Cli`.

Environment checks:

```bash
dotnet --version
dotnet workload list
ffmpeg -version
```

## Repository Bootstrap

Restore dependencies:

```bash
dotnet restore VoxFlow.sln
```

Recommended working directories:

```bash
mkdir -p artifacts/input artifacts/output models
```

If you want a fresh local config without editing tracked files, create one from the example:

```bash
cp appsettings.example.json appsettings.local.json
```

## Configuration Model

### Shared Transcription Settings

`VoxFlow.Core` resolves transcription settings in this order:

1. Explicit `configurationPath` passed by the caller
2. `TRANSCRIPTION_SETTINGS_PATH`
3. `appsettings.json` next to the running host

Relevant files in this repo:

| File | Purpose |
|---|---|
| `appsettings.example.json` | Combined example file with `transcription` and `mcp` sections; starts in single-file mode |
| `appsettings.json` | Shared root development config; currently batch-oriented |
| `src/VoxFlow.Cli/appsettings.json` | CLI runtime config copied to CLI output |
| `src/VoxFlow.Desktop/appsettings.json` | Bundled Desktop defaults |
| `src/VoxFlow.McpServer/appsettings.json` | MCP host settings; contains only the `mcp` section |

Important:

- The checked-in root config and host configs currently default to `processingMode: "batch"`.
- `src/VoxFlow.Desktop/appsettings.json` now defaults to Desktop-oriented single-file behavior.
- `appsettings.example.json` is the easiest starting point for single-file CLI runs.
- Relative paths such as `artifacts/output` and `models/ggml-base.bin` are easiest to reason about when you run commands from the repo root.

### Desktop Configuration Resolution

`VoxFlow.Desktop` does not use `TRANSCRIPTION_SETTINGS_PATH` by default. Instead it merges:

1. Bundled `appsettings.json` inside the app
2. User overrides at `~/Library/Application Support/VoxFlow/appsettings.json`
3. An explicit override path only when a caller passes one programmatically

Desktop-specific notes:

- Persistent Desktop overrides are file-based. Edit `~/Library/Application Support/VoxFlow/appsettings.json` directly.
- `DesktopConfigurationService` writes a temporary merged snapshot before startup and before any CLI-bridge invocation.
- When the Intel CLI bridge is active, the startup snapshot disables in-process Whisper-specific validation checks (`checkModelLoadability`, `checkWhisperRuntime`, `checkLanguageSupport`) so the app can start and delegate transcription to CLI. The actual CLI transcription run still uses the full merged config.

Example Desktop override file:

```json
{
  "transcription": {
    "processingMode": "single",
    "ffmpegExecutablePath": "/opt/homebrew/bin/ffmpeg",
    "modelType": "Base"
  }
}
```

For Desktop development, prefer absolute paths in user overrides for `modelFilePath`, `wavFilePath`, and `resultFilePath`.
Bundled Desktop defaults already resolve into `~/Library/Application Support/VoxFlow/` and `~/Documents/VoxFlow/`.

### MCP Configuration

`src/VoxFlow.McpServer/appsettings.json` currently contains only MCP host settings:

```json
{
  "mcp": {
    "enabled": true,
    "transport": "stdio",
    "serverName": "voxflow",
    "serverVersion": "1.0.0",
    "allowBatch": true,
    "allowedInputRoots": [],
    "allowedOutputRoots": [],
    "maxBatchFiles": 100,
    "requireAbsolutePaths": true
  }
}
```

Important:

- MCP tools that need transcription settings must get them from `configurationPath` or `TRANSCRIPTION_SETTINGS_PATH`.
- If you launch the MCP server without either of those, the host starts, but transcription-related tool calls will not have a default `transcription` section to load.
- With empty `allowedInputRoots` and `allowedOutputRoots`, any absolute path is accepted.
- `requireAbsolutePaths` defaults to `true`.

## Build

Build the CLI:

```bash
dotnet build src/VoxFlow.Cli/VoxFlow.Cli.csproj --no-restore
```

Build the MCP server:

```bash
dotnet build src/VoxFlow.McpServer/VoxFlow.McpServer.csproj --no-restore
```

Build the Desktop app:

```bash
dotnet build src/VoxFlow.Desktop/VoxFlow.Desktop.csproj -f net9.0-maccatalyst --no-restore
```

Desktop builds automatically invoke the `BuildDesktopCliBridge` target first, which builds `src/VoxFlow.Cli` for `net9.0`. This keeps the Intel Mac Desktop bridge aligned with the current CLI code without adding an executable project reference.

Package the Desktop app:

```bash
./scripts/build-macos.sh
```

See [docs/deployment/macos-packaging.md](../deployment/macos-packaging.md) for packaging details.

## Running VoxFlow

### CLI: Single File

Use a dedicated config file based on the example:

```bash
cp appsettings.example.json appsettings.local.json
```

Edit at least these fields in `appsettings.local.json`:

- `transcription.processingMode`
- `transcription.inputFilePath`
- `transcription.wavFilePath`
- `transcription.resultFilePath`
- `transcription.modelFilePath`
- `transcription.ffmpegExecutablePath` if `ffmpeg` is not on `PATH`

Run:

```bash
TRANSCRIPTION_SETTINGS_PATH=$PWD/appsettings.local.json \
dotnet run --project src/VoxFlow.Cli/VoxFlow.Cli.csproj
```

### CLI: Batch

The checked-in root `appsettings.json` already uses batch mode. Adjust it or point the CLI to a custom batch config.

Run with the root config:

```bash
TRANSCRIPTION_SETTINGS_PATH=$PWD/appsettings.json \
dotnet run --project src/VoxFlow.Cli/VoxFlow.Cli.csproj
```

Relevant batch settings:

- `transcription.batch.inputDirectory`
- `transcription.batch.outputDirectory`
- `transcription.batch.tempDirectory`
- `transcription.batch.filePattern`
- `transcription.batch.summaryFilePath`

### Desktop

Run the macOS desktop app:

```bash
dotnet run --project src/VoxFlow.Desktop/VoxFlow.Desktop.csproj -f net9.0-maccatalyst
```

Recommended before first launch:

- Create `~/Library/Application Support/VoxFlow/appsettings.json`
- Override bundled defaults only if you want different output/model locations
- Use absolute paths in overrides when you want fully explicit locations
- On Intel Mac, expect Desktop transcription to launch `VoxFlow.Cli` under the hood after file selection. On Apple Silicon, the Desktop app transcribes in-process.

### MCP Server

Recommended launch command:

```bash
TRANSCRIPTION_SETTINGS_PATH=$PWD/appsettings.json \
dotnet run --project src/VoxFlow.McpServer/VoxFlow.McpServer.csproj
```

This uses the root `appsettings.json` for `transcription` settings and the MCP host project config for the `mcp` section.

Behavior notes:

- Transport is stdio-only
- MCP diagnostics are redirected to stderr; stdout is reserved for protocol frames
- PathPolicy validates tool-provided paths against `allowedInputRoots`, `allowedOutputRoots`, and `requireAbsolutePaths`

Available MCP tools:

- `validate_environment`
- `transcribe_file`
- `transcribe_batch`
- `get_supported_languages`
- `inspect_model`
- `read_transcript`
- `get_effective_config`

Available MCP prompts:

- `transcribe-local-audio`
- `batch-transcribe-folder`
- `diagnose-transcription-setup`
- `inspect-last-transcript`

## Testing

Run individual test projects:

```bash
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj --no-restore
dotnet test tests/VoxFlow.Cli.Tests/VoxFlow.Cli.Tests.csproj --no-restore
dotnet test tests/VoxFlow.McpServer.Tests/VoxFlow.McpServer.Tests.csproj --no-restore
dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj --no-restore
```

Run the full solution:

```bash
dotnet test VoxFlow.sln --no-restore
```

Run only the Desktop UI/component suite:

```bash
dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj \
  --filter FullyQualifiedName~DesktopUiComponentTests \
  --no-restore
```

Desktop UI integration fixtures currently used in the repo:

- `artifacts/Input/Test 1.m4a`
- `artifacts/Input/Test 2.m4a`

For real Desktop UI automation, see [docs/runbooks/desktop-ui-automation.md](../runbooks/desktop-ui-automation.md).
