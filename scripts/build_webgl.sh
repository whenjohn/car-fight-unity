#!/bin/zsh
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"

unity build . \
  --target WebGL \
  --execute-method CarFight.Editor.WebGlBuild.Build \
  --output-path "$project_root/Build/WebGL" \
  --log-file "$project_root/Logs/build-webgl.log" \
  --no-tail \
  --allow-dirty-build
