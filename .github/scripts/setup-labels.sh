#!/usr/bin/env bash
# Bootstrap or sync the repo's GitHub labels.
# Idempotent: --force creates the label if missing or updates color/description if it already exists.
# Run from anywhere with the gh CLI authenticated against this repo:
#   ./.github/scripts/setup-labels.sh

set -euo pipefail

if ! command -v gh >/dev/null 2>&1; then
  echo "error: gh CLI is required" >&2
  exit 1
fi

label() {
  local name="$1" color="$2" desc="$3"
  gh label create "$name" --color "$color" --description "$desc" --force >/dev/null
  echo "  ✓ $name"
}

echo "Type"
label "bug"             "d73a4a" "Reproducible defect"
label "enhancement"     "a2eeef" "Feature request or improvement"
label "documentation"   "0075ca" "Docs change, gap, or correction"
label "task"            "cfd3d7" "Chore, refactor, or non-feature work"

echo "Category"
label "architecture"    "5319e7" "Design and structural decisions"
label "testing"         "fbca04" "Test coverage or testing infrastructure"
label "ui/ux"           "c5def5" "User-facing surface and interaction design"

echo "Contributor signals"
label "good first issue" "7057ff" "Suitable for first-time contributors"
label "help wanted"      "008672" "Maintainer is looking for help here"

echo "Priority"
label "priority-high"    "b60205" "Blocking or near-blocking; address ASAP"
label "priority-medium"  "d93f0b" "Should be addressed in the next cycle"
label "priority-low"     "0e8a16" "Nice to have; no urgency"

echo "Done."
