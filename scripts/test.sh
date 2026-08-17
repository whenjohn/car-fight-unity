#!/bin/zsh
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"

mkdir -p TestResults
unity test . \
  --mode EditMode \
  --output "$project_root/TestResults/editmode-results.xml" \
  --timeout 600
