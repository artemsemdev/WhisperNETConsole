#!/bin/bash
set -euo pipefail

echo "Building VoxFlow Desktop for macOS..."

HOST_ARCH=$(uname -m)
case "$HOST_ARCH" in
    x86_64)
        RID="maccatalyst-x64"
        ;;
    arm64)
        RID="maccatalyst-arm64"
        ;;
    *)
        echo "Unsupported host architecture: $HOST_ARCH" >&2
        exit 1
        ;;
esac

dotnet publish src/VoxFlow.Desktop/VoxFlow.Desktop.csproj \
    -f net9.0-maccatalyst \
    -c Release \
    -r "$RID" \
    -p:CreatePackage=true

ARTIFACT_DIR="src/VoxFlow.Desktop/bin/Release/net9.0-maccatalyst/$RID/publish"
echo "Build artifacts in: $ARTIFACT_DIR"

# Generate SHA-256 checksum for file artifacts produced in the publish directory.
PKG_PATH=$(find "$ARTIFACT_DIR" -maxdepth 1 -type f -name "*.pkg" -print -quit 2>/dev/null)
if [ -n "$PKG_PATH" ]; then
    shasum -a 256 "$PKG_PATH" > "${PKG_PATH}.sha256"
    echo "Checksum: ${PKG_PATH}.sha256"
else
    APP_PATH=$(find "$ARTIFACT_DIR" -maxdepth 1 -type d -name "*.app" -print -quit 2>/dev/null)
    if [ -n "$APP_PATH" ]; then
        echo "Skipping checksum for app bundle directory: $APP_PATH" >&2
    else
        echo "No packaged artifact found in: $ARTIFACT_DIR" >&2
    fi
fi
