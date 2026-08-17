#!/bin/zsh
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"

unity build . \
  --target StandaloneOSX \
  --architecture x86_64 \
  --output-path "$project_root/Build/CarFight.app" \
  --log-file "$project_root/Logs/build-macos.log" \
  --no-tail \
  --allow-dirty-build
