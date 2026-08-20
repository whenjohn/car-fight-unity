#!/bin/zsh
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
player="$project_root/Build/CarFight.app/Contents/MacOS/Car Fight"
web_root="$project_root/Build/WebGL"
chrome="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
state_dir="/private/tmp/car-fight-unity-browser-review"
state_file="$state_dir/processes"
game_port=7770
web_port=8080
run_id="browser-review"

stop_review() {
  if [[ ! -f "$state_file" ]]; then
    printf 'CF_BROWSER_REVIEW status=stopped\n'
    return
  fi

  source "$state_file"
  for pid in "$BROWSER_PID" "$NATIVE_PID" "$WEB_PID" "$SERVER_PID"; do
    if kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
    fi
  done
  pkill -f -- "--user-data-dir=$RUN_DIR/chrome-profile" 2>/dev/null || true
  unlink "$state_file"
  printf 'CF_BROWSER_REVIEW status=stopped run_dir=%s\n' "$RUN_DIR"
}

if [[ "${1:-start}" == "stop" ]]; then
  stop_review
  exit 0
fi
if [[ "${1:-start}" != "start" ]]; then
  printf 'usage: ./scripts/browser_network_review.sh [start|stop]\n' >&2
  exit 2
fi

mkdir -p "$state_dir" "$project_root/TestResults/browser-review"
if [[ -f "$state_file" ]]; then
  source "$state_file"
  if kill -0 "$SERVER_PID" 2>/dev/null || kill -0 "$NATIVE_PID" 2>/dev/null || kill -0 "$BROWSER_PID" 2>/dev/null; then
    printf 'CF_BROWSER_REVIEW status=already_running run_dir=%s\n' "$RUN_DIR" >&2
    exit 1
  fi
  unlink "$state_file"
fi
if [[ ! -x "$player" || ! -f "$web_root/index.html" || ! -x "$chrome" ]]; then
  printf 'CF_BROWSER_REVIEW status=failed reason=missing_build_or_browser\n' >&2
  exit 1
fi
if lsof -nP -iTCP:"$game_port" -sTCP:LISTEN >/dev/null 2>&1 ||
   lsof -nP -iUDP:"$game_port" >/dev/null 2>&1 ||
   lsof -nP -iTCP:"$web_port" -sTCP:LISTEN >/dev/null 2>&1; then
  printf 'CF_BROWSER_REVIEW status=failed reason=port_in_use\n' >&2
  exit 1
fi

run_dir=$(mktemp -d "$project_root/TestResults/browser-review/run.XXXXXX")
server_log="$run_dir/server.log"
native_log="$run_dir/native.log"
web_log="$run_dir/web.log"
browser_log="$run_dir/browser-process.log"

cleanup_partial() {
  for pid in "${console_pid:-}" "${browser_pid:-}" "${native_pid:-}" "${web_pid:-}" "${server_pid:-}"; do
    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
    fi
  done
  if [[ -n "${run_dir:-}" ]]; then
    pkill -f -- "--user-data-dir=$run_dir/chrome-profile" 2>/dev/null || true
  fi
}

wait_for_pattern() {
  local file="$1"
  local pattern="$2"
  local pid="$3"
  local attempts="${4:-100}"
  while (( attempts > 0 )); do
    if [[ -f "$file" ]] && rg -q "$pattern" "$file"; then
      return 0
    fi
    if ! kill -0 "$pid" 2>/dev/null; then
      return 1
    fi
    sleep 0.1
    attempts=$((attempts - 1))
  done
  return 1
}

wait_for_http() {
  local pid="$1"
  local attempts=100
  while (( attempts > 0 )); do
    if curl -fsS "http://127.0.0.1:$web_port/" >/dev/null 2>&1; then
      return 0
    fi
    if ! kill -0 "$pid" 2>/dev/null; then
      return 1
    fi
    sleep 0.1
    attempts=$((attempts - 1))
  done
  return 1
}

"$player" -batchmode -nographics -logFile "$server_log" \
  --server --interactive --port "$game_port" --scenario baseline --run-id "$run_id" \
  > "$run_dir/server-process.log" 2>&1 &
server_pid=$!
if ! wait_for_pattern "$server_log" 'event=SERVER_READY' "$server_pid"; then
  cleanup_partial
  printf 'CF_BROWSER_REVIEW status=failed reason=server_not_ready run_dir=%s\n' "$run_dir" >&2
  exit 1
fi

python3 "$project_root/scripts/webgl_server.py" \
  --port "$web_port" --directory "$web_root" > "$web_log" 2>&1 &
web_pid=$!
if ! wait_for_http "$web_pid"; then
  cleanup_partial
  printf 'CF_BROWSER_REVIEW status=failed reason=web_server_not_ready run_dir=%s\n' "$run_dir" >&2
  exit 1
fi

"$player" -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "$native_log" \
  --client --host 127.0.0.1 --port "$game_port" --name alpha --script interactive \
  --scenario baseline --run-id "$run_id" > "$run_dir/native-process.log" 2>&1 &
native_pid=$!
if ! wait_for_pattern "$server_log" 'event=OWNERSHIP_ASSIGNED.*name=alpha' "$native_pid"; then
  cleanup_partial
  printf 'CF_BROWSER_REVIEW status=failed reason=native_not_connected run_dir=%s\n' "$run_dir" >&2
  exit 1
fi

url="http://127.0.0.1:$web_port/?host=127.0.0.1&port=$game_port&run=$run_id&name=bravo"
"$chrome" --app="$url" --user-data-dir="$run_dir/chrome-profile" --no-first-run \
  --remote-debugging-port=9222 > "$browser_log" 2>&1 &
browser_pid=$!
for _ in {1..100}; do
  if curl -fsS http://127.0.0.1:9222/json/list >/dev/null 2>&1; then
    node "$project_root/scripts/capture_browser_console.mjs" http://127.0.0.1:9222 \
      > "$run_dir/browser-console.log" 2>&1 &
    console_pid=$!
    break
  fi
  sleep 0.1
done
if ! wait_for_pattern "$server_log" 'event=OWNERSHIP_ASSIGNED.*name=bravo' "$server_pid" 600; then
  cleanup_partial
  printf 'CF_BROWSER_REVIEW status=failed reason=browser_not_connected run_dir=%s\n' "$run_dir" >&2
  exit 1
fi
if [[ -z "${console_pid:-}" ]] ||
   ! wait_for_pattern "$native_log" 'event=INPUT_SENT.*name=alpha' "$native_pid" 300 ||
   ! wait_for_pattern "$run_dir/browser-console.log" 'event=INPUT_SENT.*name=bravo' "$browser_pid" 300; then
  cleanup_partial
  printf 'CF_BROWSER_REVIEW status=failed reason=gameplay_input_not_verified run_dir=%s\n' "$run_dir" >&2
  exit 1
fi

{
  printf 'SERVER_PID=%q\n' "$server_pid"
  printf 'WEB_PID=%q\n' "$web_pid"
  printf 'NATIVE_PID=%q\n' "$native_pid"
  printf 'BROWSER_PID=%q\n' "$browser_pid"
  printf 'RUN_DIR=%q\n' "$run_dir"
} > "$state_file"

printf 'CF_BROWSER_REVIEW status=ready clients=2 ownership=verified input=verified run_dir=%s\n' "$run_dir"
printf 'CF_BROWSER_REVIEW stop=./scripts/browser_network_review.sh\ stop\n'
