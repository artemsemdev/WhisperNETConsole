#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$SCRIPT_DIR/VoxFlowXcUiTests.xcodeproj"

if [[ "${VOXFLOW_RUN_DESKTOP_UI_TESTS:-}" != "1" ]]; then
    echo "Skipped: set VOXFLOW_RUN_DESKTOP_UI_TESTS=1 to run Desktop UI tests."
    exit 0
fi

echo "=== VoxFlow Desktop XCUITests ==="
echo "Project: $PROJECT"
echo ""

xcodebuild test \
    -project "$PROJECT" \
    -scheme VoxFlowXcUiTests \
    -destination 'platform=macOS' \
    2>&1
