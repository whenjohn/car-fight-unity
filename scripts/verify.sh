#!/bin/zsh
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
"$project_root/scripts/test.sh"
"$project_root/scripts/build.sh"
