#!/bin/zsh
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"

if [[ "$#" -eq 1 && "$1" == "matrix" ]]; then
  for scenario in baseline latency jitter loss late_join reconnect invalid_authority stall; do
    "$project_root/scripts/multiplayer_test.sh" "$scenario"
  done
  printf 'CF_MATRIX result=passed scenarios=8\n'
  exit 0
fi

source "$project_root/scripts/lib/multiplayer_launcher.sh"

cf_multiplayer_main "$@"
