#!/bin/zsh
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
source "$project_root/scripts/lib/multiplayer_launcher.sh"

cf_multiplayer_main "$@"
