# Smoke Test Runbook

Quick verification routines for each VoxFlow host. Run these after builds, configuration changes, or environment setup to confirm basic functionality.

## CLI Smoke Test

```bash
cp appsettings.example.json appsettings.local.json
# Edit appsettings.local.json:
#   - set processingMode to "single"
#   - set inputFilePath to a real audio file
#   - set wavFilePath and resultFilePath to writable locations
#   - set modelFilePath to a valid model path (e.g. models/ggml-base.bin)

TRANSCRIPTION_SETTINGS_PATH=$PWD/appsettings.local.json \
dotnet run --project src/VoxFlow.Cli/VoxFlow.Cli.csproj
```

**Verify:**
- Exit code is `0`
- Result file exists at the configured `resultFilePath`
- Result file contains timestamped transcript lines in `{start}->{end}: {text}` format

## Desktop Smoke Test

```bash
dotnet build src/VoxFlow.Desktop/VoxFlow.Desktop.csproj -f net9.0-maccatalyst --no-restore
dotnet run --project src/VoxFlow.Desktop/VoxFlow.Desktop.csproj -f net9.0-maccatalyst
```

**Verify:**
- App launches and shows the Ready screen with "Audio Transcription" heading
- Click `Browse Files`, select an audio file
- Running screen shows progress (stage, percentage, elapsed time)
- Complete screen shows transcript preview and action buttons
- On Intel Mac: the Running screen shows "Running CLI transcription pipeline..." which is expected behavior

## MCP Server Smoke Test

```bash
TRANSCRIPTION_SETTINGS_PATH=$PWD/appsettings.json \
dotnet run --project src/VoxFlow.McpServer/VoxFlow.McpServer.csproj
```

**Verify:**
- The server starts on stdio without errors on stderr
- Send a JSON-RPC `initialize` request on stdin to confirm the server responds
- Diagnostic output appears on stderr, not stdout

## Full Test Suite

Run all unit and integration tests:

```bash
dotnet test VoxFlow.sln --no-restore
```

Recommended local smoke sequence before opening a pull request:

```bash
dotnet test tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj --no-restore
dotnet test tests/VoxFlow.Cli.Tests/VoxFlow.Cli.Tests.csproj --no-restore
dotnet test tests/VoxFlow.McpServer.Tests/VoxFlow.McpServer.Tests.csproj --no-restore
dotnet test tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj --no-restore
./scripts/run-desktop-ui-tests.sh --filter HappyPath_UserSelectsFile_SeesRunningState_AndGetsResult
```
