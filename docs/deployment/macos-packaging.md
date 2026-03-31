# macOS Packaging

How to build and package VoxFlow Desktop for local macOS distribution.

## Prerequisites

- .NET SDK 9
- `maui-maccatalyst` workload installed (`dotnet workload install maui-maccatalyst`)
- macOS with Xcode command-line tools

## Build Script

The repository provides a packaging helper:

```bash
./scripts/build-macos.sh
```

The script:

1. Detects the host architecture (`arm64` or `x86_64`)
2. Maps it to the matching Mac Catalyst runtime identifier (`maccatalyst-arm64` or `maccatalyst-x64`)
3. Publishes `src/VoxFlow.Desktop/VoxFlow.Desktop.csproj` in Release configuration with `-p:CreatePackage=true`
4. Writes a SHA-256 checksum file next to the generated `.pkg` or `.app` artifact

Build artifacts are written to:

```
src/VoxFlow.Desktop/bin/Release/net9.0-maccatalyst/<rid>/publish/
```

## Manual Build

To build without the script:

```bash
dotnet publish src/VoxFlow.Desktop/VoxFlow.Desktop.csproj \
    -f net9.0-maccatalyst \
    -c Release \
    -r maccatalyst-arm64 \
    -p:CreatePackage=true
```

Replace `maccatalyst-arm64` with `maccatalyst-x64` for Intel targets.

## Desktop CLI Bridge

Desktop builds automatically invoke the `BuildDesktopCliBridge` MSBuild target, which builds `src/VoxFlow.Cli` for `net9.0`. This ensures the Intel Mac Catalyst CLI bridge is aligned with the current CLI code. No manual CLI build step is needed before packaging.

## What This Script Does Not Do

This is a local packaging helper, not a complete release pipeline. The following are not currently automated:

- **Code signing** with a Developer ID certificate
- **Notarization** via Apple's notary service
- **Gatekeeper-compatible DMG or installer** generation
- **Stapling** the notarization ticket to the artifact
- **Universal binary** (fat binary for both arm64 and x64 in one artifact)

These are separate release-engineering tasks. See [docs/delivery/release-process.md](../delivery/release-process.md) for the current release process scope.

## Verifying the Build

After packaging, verify the artifact:

```bash
# Check the checksum file was generated
cat src/VoxFlow.Desktop/bin/Release/net9.0-maccatalyst/*/publish/*.sha256

# On Apple Silicon, launch the app directly
open src/VoxFlow.Desktop/bin/Release/net9.0-maccatalyst/maccatalyst-arm64/publish/VoxFlow.Desktop.app
```

For local development, prefer `dotnet run` over the packaged artifact:

```bash
dotnet run --project src/VoxFlow.Desktop/VoxFlow.Desktop.csproj -f net9.0-maccatalyst
```
