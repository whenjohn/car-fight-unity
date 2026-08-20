#!/bin/zsh

readonly CF_INFRASTRUCTURE_EXIT=20
readonly CF_GAMEPLAY_EXIT=10
readonly CF_SUCCESS_EXIT=0
readonly CF_SEED=20260818
readonly CF_LAUNCHER_LIBRARY_PATH="${${(%):-%N}:A}"

typeset -g CF_PROJECT_ROOT=""
typeset -g CF_PLAYER=""
typeset -g CF_RUN_ROOT=""
typeset -g CF_RUN_DIR=""
typeset -g CF_RUN_ID=""
typeset -g CF_PORT_LOCK_ROOT=""
typeset -g CF_PORT_LOCK=""
typeset -g CF_PORT=""
typeset -g CF_ALPHA_PORT=""
typeset -g CF_BRAVO_PORT=""
typeset -g CF_SCENARIO="baseline"
typeset -g CF_LATENCY_MS=0
typeset -g CF_JITTER_MS=0
typeset -g CF_LOSS_PERCENT=0
typeset -g CF_USE_PROXY=0
typeset -g CF_PROXY_PID=""
typeset -g CF_PROXY_STATUS=""
typeset -g CF_STARTED_AT=""
typeset -g CF_STARTED_SECONDS=0
typeset -g CF_SERVER_PID=""
typeset -g CF_ALPHA_PID=""
typeset -g CF_BRAVO_PID=""
typeset -g CF_SERVER_STATUS=""
typeset -g CF_ALPHA_STATUS=""
typeset -g CF_BRAVO_STATUS=""
typeset -g CF_WAIT_REASON=""
typeset -g CF_VALIDATION_REASON=""
typeset -g CF_CLEANED_UP=0
typeset -ga CF_SERVER_ARGS
typeset -ga CF_ALPHA_ARGS
typeset -ga CF_BRAVO_ARGS

cf_json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//$'\n'/\\n}"
  print -rn -- "$value"
}

cf_json_string() {
  printf '"%s"' "$(cf_json_escape "$1")"
}

cf_json_array() {
  local separator=""
  local value
  printf '['
  for value in "$@"; do
    printf '%s' "$separator"
    cf_json_string "$value"
    separator=','
  done
  printf ']'
}

cf_json_number_or_null() {
  if [[ -n "$1" ]]; then
    printf '%s' "$1"
  else
    printf 'null'
  fi
}

cf_write_run_json() {
  local destination="$CF_RUN_DIR/run.json"
  local temporary="$destination.tmp.$$"
  local revision
  local dirty=false
  local build_hash
  local build_size
  revision=$(git -C "$CF_PROJECT_ROOT" rev-parse HEAD)
  if [[ -n "$(git -C "$CF_PROJECT_ROOT" status --porcelain)" ]]; then
    dirty=true
  fi
  if [[ -f "$CF_PLAYER" ]]; then
    build_hash=$(shasum -a 256 "$CF_PLAYER" | awk '{print $1}')
    build_size=$(stat -f '%z' "$CF_PLAYER")
  else
    build_hash=""
    build_size=""
  fi

  {
    printf '{\n'
    printf '  "runId": '; cf_json_string "$CF_RUN_ID"; printf ',\n'
    printf '  "scenario": '; cf_json_string "$CF_SCENARIO"; printf ',\n'
    printf '  "startedAtUtc": '; cf_json_string "$CF_STARTED_AT"; printf ',\n'
    printf '  "git": {"revision": '; cf_json_string "$revision"; printf ', "dirty": %s},\n' "$dirty"
    printf '  "build": {"executable": '; cf_json_string "$CF_PLAYER"; printf ', "sha256": '; cf_json_string "$build_hash"; printf ', "sizeBytes": '; cf_json_number_or_null "$build_size"; printf '},\n'
    printf '  "seed": %s,\n' "$CF_SEED"
    printf '  "port": '; cf_json_number_or_null "$CF_PORT"; printf ',\n'
    printf '  "impairment": {"mode": '; cf_json_string "$CF_SCENARIO"; printf ', "latencyMs": %s, "jitterMs": %s, "lossPercent": %s, "alphaPort": %s, "bravoPort": %s},\n' "$CF_LATENCY_MS" "$CF_JITTER_MS" "$CF_LOSS_PERCENT" "$CF_ALPHA_PORT" "$CF_BRAVO_PORT"
    printf '  "arguments": {\n'
    printf '    "server": '; cf_json_array "$CF_PLAYER" "${CF_SERVER_ARGS[@]}"; printf ',\n'
    printf '    "alpha": '; cf_json_array "$CF_PLAYER" "${CF_ALPHA_ARGS[@]}"; printf ',\n'
    printf '    "bravo": '; cf_json_array "$CF_PLAYER" "${CF_BRAVO_ARGS[@]}"; printf '\n'
    printf '  },\n'
    printf '  "processes": {\n'
    printf '    "server": {"pid": '; cf_json_number_or_null "$CF_SERVER_PID"; printf ', "exitStatus": '; cf_json_number_or_null "$CF_SERVER_STATUS"; printf '},\n'
    printf '    "alpha": {"pid": '; cf_json_number_or_null "$CF_ALPHA_PID"; printf ', "exitStatus": '; cf_json_number_or_null "$CF_ALPHA_STATUS"; printf '},\n'
    printf '    "bravo": {"pid": '; cf_json_number_or_null "$CF_BRAVO_PID"; printf ', "exitStatus": '; cf_json_number_or_null "$CF_BRAVO_STATUS"; printf '},\n'
    printf '    "impairmentProxy": {"pid": '; cf_json_number_or_null "$CF_PROXY_PID"; printf ', "exitStatus": '; cf_json_number_or_null "$CF_PROXY_STATUS"; printf '}\n'
    printf '  }\n'
    printf '}\n'
  } > "$temporary"
  mv "$temporary" "$destination"
}

cf_write_result_json() {
  local result_status="$1"
  local category="$2"
  local reason="$3"
  local destination="$CF_RUN_DIR/result.json"
  local temporary="$destination.tmp.$$"
  local duration=$((SECONDS - CF_STARTED_SECONDS))
  {
    printf '{\n'
    printf '  "status": '; cf_json_string "$result_status"; printf ',\n'
    printf '  "category": '; cf_json_string "$category"; printf ',\n'
    printf '  "reason": '; cf_json_string "$reason"; printf ',\n'
    printf '  "durationSeconds": %s,\n' "$duration"
    printf '  "runDirectory": '; cf_json_string "$CF_RUN_DIR"; printf '\n'
    printf '}\n'
  } > "$temporary"
  mv "$temporary" "$destination"
}

cf_release_port() {
  if [[ -z "$CF_PORT_LOCK" || ! -d "$CF_PORT_LOCK" ]]; then
    return
  fi
  local owner_file="$CF_PORT_LOCK/owner"
  local expected="$$ $CF_RUN_ID"
  if [[ -f "$owner_file" && "$(<"$owner_file")" == "$expected" ]]; then
    unlink "$owner_file"
    rmdir "$CF_PORT_LOCK"
  fi
}

cf_reserve_port() {
  local base_port="${CAR_FIGHT_PORT_BASE:-19873}"
  local final_port=$((base_port + 499))
  local candidate
  mkdir -p "$CF_PORT_LOCK_ROOT"

  for (( candidate = base_port; candidate <= final_port; candidate++ )); do
    local lock="$CF_PORT_LOCK_ROOT/$candidate.lock"
    if ! mkdir "$lock" 2>/dev/null; then
      local owner_file="$lock/owner"
      if [[ -f "$owner_file" ]]; then
        local owner_pid="${$(<"$owner_file")%% *}"
        if [[ -n "$owner_pid" ]] && ! kill -0 "$owner_pid" 2>/dev/null; then
          unlink "$owner_file" 2>/dev/null || true
          rmdir "$lock" 2>/dev/null || true
        fi
      fi
      if ! mkdir "$lock" 2>/dev/null; then
        continue
      fi
    fi

    local alpha_candidate=$((candidate + 500))
    local bravo_candidate=$((candidate + 1000))
    if (( bravo_candidate > 65535 )) ||
       lsof -nP -iUDP:"$candidate" >/dev/null 2>&1 ||
       lsof -nP -iUDP:"$alpha_candidate" >/dev/null 2>&1 ||
       lsof -nP -iUDP:"$bravo_candidate" >/dev/null 2>&1; then
      rmdir "$lock"
      continue
    fi

    printf '%s %s\n' "$$" "$CF_RUN_ID" > "$lock/owner"
    CF_PORT="$candidate"
    CF_ALPHA_PORT="$alpha_candidate"
    CF_BRAVO_PORT="$bravo_candidate"
    CF_PORT_LOCK="$lock"
    return 0
  done

  return 1
}

cf_pid_matches_run() {
  local exact_pid="$1"
  [[ -n "$exact_pid" ]] || return 1
  local command_line
  command_line=$(ps -p "$exact_pid" -o command= 2>/dev/null || true)
  [[ "$command_line" == *"--run-id $CF_RUN_ID"* ]]
}

cf_stop_exact_process() {
  local exact_pid="$1"
  if ! kill -0 "$exact_pid" 2>/dev/null || ! cf_pid_matches_run "$exact_pid"; then
    return
  fi

  kill "$exact_pid" 2>/dev/null || true
  local attempt
  for attempt in {1..20}; do
    if ! kill -0 "$exact_pid" 2>/dev/null; then
      return
    fi
    sleep 0.1
  done
  if cf_pid_matches_run "$exact_pid"; then
    kill -KILL "$exact_pid" 2>/dev/null || true
  fi
}

cf_cleanup() {
  if (( CF_CLEANED_UP )); then
    return
  fi
  CF_CLEANED_UP=1
  cf_stop_exact_process "$CF_ALPHA_PID"
  cf_stop_exact_process "$CF_BRAVO_PID"
  cf_stop_exact_process "$CF_SERVER_PID"
  cf_stop_exact_process "$CF_PROXY_PID"
  cf_release_port
}

cf_wait_for_event() {
  local log_file="$1"
  local event_name="$2"
  local exact_pid="$3"
  local timeout_seconds="$4"
  local deadline=$((SECONDS + timeout_seconds))
  CF_WAIT_REASON=""

  while (( SECONDS < deadline )); do
    if rg -q "event=$event_name" "$log_file" 2>/dev/null; then
      return 0
    fi
    if ! kill -0 "$exact_pid" 2>/dev/null; then
      CF_WAIT_REASON="process_exited_before_$event_name"
      return 1
    fi
    sleep 0.1
  done

  CF_WAIT_REASON="launcher_timeout_waiting_for_$event_name"
  return 1
}

cf_wait_for_pattern() {
  local log_file="$1"
  local pattern="$2"
  local exact_pid="$3"
  local timeout_seconds="$4"
  local deadline=$((SECONDS + timeout_seconds))
  CF_WAIT_REASON=""

  while (( SECONDS < deadline )); do
    if rg -q "$pattern" "$log_file" 2>/dev/null; then
      return 0
    fi
    if ! kill -0 "$exact_pid" 2>/dev/null; then
      CF_WAIT_REASON="process_exited_before_pattern"
      return 1
    fi
    sleep 0.1
  done

  CF_WAIT_REASON="launcher_timeout_waiting_for_pattern"
  return 1
}

cf_wait_for_all_processes() {
  local timeout_seconds="$1"
  local deadline=$((SECONDS + timeout_seconds))
  while (( SECONDS < deadline )); do
    if ! kill -0 "$CF_SERVER_PID" 2>/dev/null &&
       ! kill -0 "$CF_ALPHA_PID" 2>/dev/null &&
       ! kill -0 "$CF_BRAVO_PID" 2>/dev/null; then
      return 0
    fi
    sleep 0.1
  done
  CF_WAIT_REASON="launcher_timeout_waiting_for_process_exit"
  return 1
}

cf_collect_statuses() {
  set +e
  wait "$CF_ALPHA_PID"
  CF_ALPHA_STATUS=$?
  wait "$CF_BRAVO_PID"
  CF_BRAVO_STATUS=$?
  wait "$CF_SERVER_PID"
  CF_SERVER_STATUS=$?
  if [[ -n "$CF_PROXY_PID" ]]; then
    kill "$CF_PROXY_PID" 2>/dev/null || true
    wait "$CF_PROXY_PID"
    CF_PROXY_STATUS=$?
  fi
  set -e
  cf_write_run_json
}

cf_print_failure_context() {
  local log_file
  for log_file in "$CF_RUN_DIR/server.log" "$CF_RUN_DIR/alpha.log" "$CF_RUN_DIR/bravo.log"; do
    if [[ -f "$log_file" ]]; then
      printf '\n== %s ==\n' "${log_file:t}"
      tail -40 "$log_file"
    fi
  done
}

cf_fail_infrastructure() {
  local reason="$1"
  cf_write_run_json
  cf_write_result_json "failed" "infrastructure" "$reason"
  printf 'CF_LAUNCHER result=failed category=infrastructure reason=%s logs=%s\n' "$reason" "$CF_RUN_DIR"
  cf_print_failure_context
  return "$CF_INFRASTRUCTURE_EXIT"
}

cf_fail_gameplay() {
  local reason="$1"
  cf_write_run_json
  cf_write_result_json "failed" "gameplay" "$reason"
  printf 'CF_LAUNCHER result=failed category=gameplay reason=%s logs=%s\n' "$reason" "$CF_RUN_DIR"
  cf_print_failure_context
  return "$CF_GAMEPLAY_EXIT"
}

cf_validate_results() {
  local log_file
  CF_VALIDATION_REASON=""
  for log_file in "$CF_RUN_DIR/server.log" "$CF_RUN_DIR/alpha.log" "$CF_RUN_DIR/bravo.log"; do
    if rg -q 'event=SCENARIO_RESULT.*passed=false' "$log_file"; then
      CF_VALIDATION_REASON="scenario_reported_failure"
      return 1
    fi
    if ! rg -q 'event=SCENARIO_RESULT.*passed=true' "$log_file"; then
      CF_VALIDATION_REASON="missing_success_result"
      return 1
    fi
    # FishyWebRTC can report an ObjectDisposedException from its listener
    # teardown after the scenario has already completed successfully. Treat
    # actual unhandled/assertion failures as fatal, but do not turn this known
    # shutdown race into a false gameplay failure.
    if rg -qi 'unhandled|assertion failed' "$log_file"; then
      CF_VALIDATION_REASON="runtime_error_in_log"
      return 1
    fi
  done

  local alpha_status_ok=0
  if [[ "$CF_ALPHA_STATUS" == 0 ||
        ("$CF_SCENARIO" == "stall" && "$CF_ALPHA_STATUS" == 145) ]]; then
    alpha_status_ok=1
  fi
  if [[ "$CF_SERVER_STATUS" != 0 || "$alpha_status_ok" != 1 || "$CF_BRAVO_STATUS" != 0 ]]; then
    CF_VALIDATION_REASON="nonzero_process_exit"
    return 1
  fi
  if ! rg -q 'event=SERVER_EVIDENCE_COMPLETE.*assigned=2.*moved=1.*contact=1.*unauthorized_input_accepted=0' "$CF_RUN_DIR/server.log"; then
    CF_VALIDATION_REASON="server_evidence_missing"
    return 1
  fi
  if ! rg -q 'event=CONTACT_OBSERVED' "$CF_RUN_DIR/alpha.log" ||
     ! rg -q 'event=CONTACT_OBSERVED' "$CF_RUN_DIR/bravo.log"; then
    CF_VALIDATION_REASON="client_contact_evidence_missing"
    return 1
  fi
  case "$CF_SCENARIO" in
    late_join)
      if ! rg -q 'event=LATE_JOIN_READY.*vehicles=2' "$CF_RUN_DIR/bravo.log"; then
        CF_VALIDATION_REASON="late_join_evidence_missing"
        return 1
      fi
      ;;
    reconnect)
      if [[ "$(rg -c 'event=OWNERSHIP_ASSIGNED.*name=bravo' "$CF_RUN_DIR/server.log")" -lt 2 ]] ||
         ! rg -q 'event=SESSION_RELEASED.*name=bravo' "$CF_RUN_DIR/server.log" ||
         ! rg -q 'event=RECONNECT_LAUNCHED' "$CF_RUN_DIR/harness.log"; then
        CF_VALIDATION_REASON="reconnect_evidence_missing"
        return 1
      fi
      ;;
    invalid_authority)
      if ! rg -q 'event=INVALID_AUTHORITY_SENT' "$CF_RUN_DIR/bravo.log" ||
         ! rg -q 'event=INVALID_AUTHORITY_REJECTED.*foreign=1.*authority_changed=0' "$CF_RUN_DIR/server.log" ||
         ! rg -q 'event=INPUT_ACCEPTED.*vehicle=2' "$CF_RUN_DIR/server.log"; then
        CF_VALIDATION_REASON="invalid_authority_positive_control_missing"
        return 1
      fi
      ;;
    stall)
      if [[ "$(rg -c 'event=STALE_HISTORY_SKIPPED' "$CF_RUN_DIR/alpha.log")" -ne 1 ]] ||
         ! rg -q 'event=STALL_RECOVERY_COMPLETE' "$CF_RUN_DIR/alpha.log" ||
         ! rg -q 'event=CLIENT_STALLED.*duration_ms=1500' "$CF_RUN_DIR/harness.log"; then
        CF_VALIDATION_REASON="stall_recovery_evidence_missing"
        return 1
      fi
      ;;
  esac
  if [[ -n "$CF_PROXY_PID" ]]; then
    if [[ "$CF_PROXY_STATUS" != 0 ]] ||
       ! rg -q 'event=PROXY_COUNTERS.*forwarded=[1-9][0-9]*' "$CF_RUN_DIR/proxy.log"; then
      CF_VALIDATION_REASON="impairment_counter_missing"
      return 1
    fi
    if (( CF_LATENCY_MS > 0 )) &&
       ! rg -q 'event=PROXY_COUNTERS.*delayed=[1-9][0-9]*' "$CF_RUN_DIR/proxy.log"; then
      CF_VALIDATION_REASON="delay_positive_control_missing"
      return 1
    fi
    if (( CF_LOSS_PERCENT > 0 )) &&
       ! rg -q 'event=PROXY_COUNTERS.*dropped=[1-9][0-9]*' "$CF_RUN_DIR/proxy.log"; then
      CF_VALIDATION_REASON="loss_positive_control_missing"
      return 1
    fi
    if (( CF_JITTER_MS > 0 )) &&
       ! rg -q 'event=PROXY_COUNTERS.*reordered=[1-9][0-9]*' "$CF_RUN_DIR/proxy.log"; then
      CF_VALIDATION_REASON="reorder_positive_control_missing"
      return 1
    fi
  fi
  return 0
}

cf_interactive_main() {
  CF_SCENARIO="baseline"
  CF_PROJECT_ROOT="$(cd "$(dirname "$CF_LAUNCHER_LIBRARY_PATH")/../.." && pwd)"
  CF_PLAYER="${CAR_FIGHT_PLAYER:-$CF_PROJECT_ROOT/Build/CarFight.app/Contents/MacOS/Car Fight}"
  CF_RUN_ROOT="${CAR_FIGHT_RUN_ROOT:-$CF_PROJECT_ROOT/TestResults/multiplayer}"
  CF_PORT_LOCK_ROOT="${CAR_FIGHT_PORT_LOCK_ROOT:-${TMPDIR:-/private/tmp}/car-fight-unity-port-locks}"
  CF_STARTED_AT=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
  CF_STARTED_SECONDS=$SECONDS
  mkdir -p "$CF_RUN_ROOT"
  CF_RUN_DIR=$(mktemp -d "$CF_RUN_ROOT/play.XXXXXX")
  CF_RUN_ID="${CF_RUN_DIR:t}"
  trap cf_cleanup EXIT
  trap 'cf_cleanup; exit 130' INT TERM

  if [[ ! -x "$CF_PLAYER" ]]; then
    cf_fail_infrastructure "player_missing_or_not_executable"
    return $?
  fi
  if ! cf_reserve_port; then
    cf_fail_infrastructure "no_available_port"
    return $?
  fi

  # The late_join scenario permits prediction to become ready with one client;
  # baseline intentionally waits for two assignments for its automated proof.
  CF_SERVER_ARGS=(-batchmode -nographics -logFile - --server --port "$CF_PORT" --scenario late_join --run-id "$CF_RUN_ID")
  CF_ALPHA_ARGS=(-logFile - --client --host 127.0.0.1 --port "$CF_PORT" --name alpha --script interactive --scenario late_join --run-id "$CF_RUN_ID")
  CF_BRAVO_ARGS=()
  cf_write_run_json

  "$CF_PLAYER" "${CF_SERVER_ARGS[@]}" > "$CF_RUN_DIR/server.log" 2>&1 &
  CF_SERVER_PID=$!
  cf_write_run_json
  if ! cf_wait_for_event "$CF_RUN_DIR/server.log" SERVER_READY "$CF_SERVER_PID" 15; then
    cf_fail_infrastructure "$CF_WAIT_REASON"
    return $?
  fi

  "$CF_PLAYER" "${CF_ALPHA_ARGS[@]}" > "$CF_RUN_DIR/client.log" 2>&1 &
  CF_ALPHA_PID=$!
  cf_write_run_json
  if ! cf_wait_for_event "$CF_RUN_DIR/client.log" OWNERSHIP_ASSIGNED "$CF_ALPHA_PID" 20 ||
     ! cf_wait_for_event "$CF_RUN_DIR/client.log" FIRST_COMPLETE_SNAPSHOT "$CF_ALPHA_PID" 20; then
    cf_fail_infrastructure "$CF_WAIT_REASON"
    return $?
  fi

  printf 'CF_LAUNCHER result=interactive_ready port=%s run_id=%s client_pid=%s logs=%s\n' "$CF_PORT" "$CF_RUN_ID" "$CF_ALPHA_PID" "$CF_RUN_DIR"
  printf 'Drive the visible alpha client. Press Ctrl-C here to stop the exact server/client pair.\n'
  set +e
  wait "$CF_ALPHA_PID"
  CF_ALPHA_STATUS=$?
  set -e
  CF_SERVER_STATUS=""
  cf_write_run_json
  cf_write_result_json "stopped" "interactive" "client_exited"
  return "$CF_SUCCESS_EXIT"
}

cf_multiplayer_main() {
  if [[ "$#" -eq 1 && "$1" == "play" ]]; then
    cf_interactive_main
    return $?
  fi
  if [[ "$#" -ne 1 || "$1" != (baseline|latency|jitter|loss|late_join|reconnect|invalid_authority|stall) ]]; then
    printf 'usage: ./scripts/multiplayer_test.sh baseline|latency|jitter|loss|late_join|reconnect|invalid_authority|stall|play\n' >&2
    return 2
  fi
  CF_SCENARIO="$1"
  case "$CF_SCENARIO" in
    latency) CF_LATENCY_MS=120; CF_USE_PROXY=1 ;;
    jitter) CF_LATENCY_MS=120; CF_JITTER_MS=30; CF_USE_PROXY=1 ;;
    loss) CF_LATENCY_MS=120; CF_LOSS_PERCENT=5; CF_USE_PROXY=1 ;;
    stall) CF_LATENCY_MS=120; CF_USE_PROXY=1 ;;
  esac

  CF_PROJECT_ROOT="$(cd "$(dirname "$CF_LAUNCHER_LIBRARY_PATH")/../.." && pwd)"
  CF_PLAYER="${CAR_FIGHT_PLAYER:-$CF_PROJECT_ROOT/Build/CarFight.app/Contents/MacOS/Car Fight}"
  CF_RUN_ROOT="${CAR_FIGHT_RUN_ROOT:-$CF_PROJECT_ROOT/TestResults/multiplayer}"
  CF_PORT_LOCK_ROOT="${CAR_FIGHT_PORT_LOCK_ROOT:-${TMPDIR:-/private/tmp}/car-fight-unity-port-locks}"
  CF_STARTED_AT=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
  CF_STARTED_SECONDS=$SECONDS
  mkdir -p "$CF_RUN_ROOT"
  CF_RUN_DIR=$(mktemp -d "$CF_RUN_ROOT/run.XXXXXX")
  CF_RUN_ID="${CF_RUN_DIR:t}"
  trap cf_cleanup EXIT
  trap 'cf_cleanup; exit 130' INT TERM

  if [[ ! -x "$CF_PLAYER" ]]; then
    cf_fail_infrastructure "player_missing_or_not_executable"
    return $?
  fi
  if ! cf_reserve_port; then
    cf_fail_infrastructure "no_available_port"
    return $?
  fi

  local alpha_connect_port="$CF_PORT"
  local bravo_connect_port="$CF_PORT"
  if (( CF_USE_PROXY )); then
    alpha_connect_port="$CF_ALPHA_PORT"
    bravo_connect_port="$CF_BRAVO_PORT"
  fi
  CF_SERVER_ARGS=(-batchmode -nographics -logFile - --server --port "$CF_PORT" --scenario "$CF_SCENARIO" --run-id "$CF_RUN_ID")
  CF_ALPHA_ARGS=(-batchmode -nographics -logFile - --client --host 127.0.0.1 --port "$alpha_connect_port" --name alpha --script converge --scenario "$CF_SCENARIO" --network-delay-ms "$CF_LATENCY_MS" --run-id "$CF_RUN_ID")
  CF_BRAVO_ARGS=(-batchmode -nographics -logFile - --client --host 127.0.0.1 --port "$bravo_connect_port" --name bravo --script converge --scenario "$CF_SCENARIO" --network-delay-ms "$CF_LATENCY_MS" --run-id "$CF_RUN_ID")
  cf_write_run_json

  if (( CF_USE_PROXY )); then
    "$CF_PROJECT_ROOT/scripts/udp_impairment.py" \
      --server-port "$CF_PORT" \
      --alpha-port "$CF_ALPHA_PORT" \
      --bravo-port "$CF_BRAVO_PORT" \
      --latency-ms "$CF_LATENCY_MS" \
      --jitter-ms "$CF_JITTER_MS" \
      --loss-percent "$CF_LOSS_PERCENT" \
      --seed "$CF_SEED" \
      --run-id "$CF_RUN_ID" > "$CF_RUN_DIR/proxy.log" 2>&1 &
    CF_PROXY_PID=$!
    cf_write_run_json
    if ! cf_wait_for_event "$CF_RUN_DIR/proxy.log" PROXY_READY "$CF_PROXY_PID" 5; then
      cf_fail_infrastructure "$CF_WAIT_REASON"
      return $?
    fi
  fi

  "$CF_PLAYER" "${CF_SERVER_ARGS[@]}" > "$CF_RUN_DIR/server.log" 2>&1 &
  CF_SERVER_PID=$!
  cf_write_run_json
  if ! cf_wait_for_event "$CF_RUN_DIR/server.log" SERVER_READY "$CF_SERVER_PID" 15; then
    cf_fail_infrastructure "$CF_WAIT_REASON"
    return $?
  fi

  "$CF_PLAYER" "${CF_ALPHA_ARGS[@]}" > "$CF_RUN_DIR/alpha.log" 2>&1 &
  CF_ALPHA_PID=$!
  cf_write_run_json
  if ! cf_wait_for_event "$CF_RUN_DIR/alpha.log" OWNERSHIP_ASSIGNED "$CF_ALPHA_PID" 10 ||
     ! cf_wait_for_event "$CF_RUN_DIR/alpha.log" FIRST_COMPLETE_SNAPSHOT "$CF_ALPHA_PID" 10; then
    cf_fail_infrastructure "$CF_WAIT_REASON"
    return $?
  fi

  if [[ "$CF_SCENARIO" == "late_join" ]]; then
    if ! cf_wait_for_event "$CF_RUN_DIR/alpha.log" PREDICTION_READY "$CF_ALPHA_PID" 10 ||
       ! cf_wait_for_pattern "$CF_RUN_DIR/server.log" 'event=INPUT_ACCEPTED.*vehicle=1' "$CF_SERVER_PID" 10; then
      cf_fail_infrastructure "$CF_WAIT_REASON"
      return $?
    fi
  fi

  "$CF_PLAYER" "${CF_BRAVO_ARGS[@]}" > "$CF_RUN_DIR/bravo.log" 2>&1 &
  CF_BRAVO_PID=$!
  cf_write_run_json
  if ! cf_wait_for_event "$CF_RUN_DIR/bravo.log" OWNERSHIP_ASSIGNED "$CF_BRAVO_PID" 10 ||
     ! cf_wait_for_event "$CF_RUN_DIR/bravo.log" FIRST_COMPLETE_SNAPSHOT "$CF_BRAVO_PID" 10; then
    cf_fail_infrastructure "$CF_WAIT_REASON"
    return $?
  fi


  if [[ "$CF_SCENARIO" == "reconnect" ]]; then
    if ! cf_wait_for_pattern "$CF_RUN_DIR/server.log" 'event=INPUT_ACCEPTED.*vehicle=2' "$CF_SERVER_PID" 10; then
      cf_fail_infrastructure "$CF_WAIT_REASON"
      return $?
    fi
    local initial_bravo_pid="$CF_BRAVO_PID"
    if ! cf_pid_matches_run "$initial_bravo_pid"; then
      cf_fail_infrastructure "bravo_pid_identity_lost"
      return $?
    fi
    kill "$initial_bravo_pid" 2>/dev/null || true
    wait "$initial_bravo_pid" 2>/dev/null || true
    mv "$CF_RUN_DIR/bravo.log" "$CF_RUN_DIR/bravo-initial.log"
    if ! cf_wait_for_pattern "$CF_RUN_DIR/server.log" 'event=SESSION_RELEASED.*name=bravo' "$CF_SERVER_PID" 5; then
      cf_fail_infrastructure "$CF_WAIT_REASON"
      return $?
    fi
    printf 'CF_HARNESS event=RECONNECT_LAUNCHED prior_pid=%s\n' "$initial_bravo_pid" > "$CF_RUN_DIR/harness.log"
    "$CF_PLAYER" "${CF_BRAVO_ARGS[@]}" > "$CF_RUN_DIR/bravo.log" 2>&1 &
    CF_BRAVO_PID=$!
    cf_write_run_json
    if ! cf_wait_for_event "$CF_RUN_DIR/bravo.log" OWNERSHIP_ASSIGNED "$CF_BRAVO_PID" 10 ||
       ! cf_wait_for_event "$CF_RUN_DIR/bravo.log" FIRST_COMPLETE_SNAPSHOT "$CF_BRAVO_PID" 10; then
      cf_fail_infrastructure "$CF_WAIT_REASON"
      return $?
    fi
  fi

  if [[ "$CF_SCENARIO" == "stall" ]]; then
    if ! cf_wait_for_event "$CF_RUN_DIR/alpha.log" INPUT_SENT "$CF_ALPHA_PID" 10 ||
       ! cf_wait_for_event "$CF_RUN_DIR/bravo.log" INPUT_SENT "$CF_BRAVO_PID" 10; then
      cf_fail_infrastructure "$CF_WAIT_REASON"
      return $?
    fi
    if ! cf_pid_matches_run "$CF_ALPHA_PID"; then
      cf_fail_infrastructure "alpha_pid_identity_lost"
      return $?
    fi
    kill -STOP "$CF_ALPHA_PID"
    sleep 1.5
    kill -CONT "$CF_ALPHA_PID"
    printf 'CF_HARNESS event=CLIENT_STALLED name=alpha duration_ms=1500\n' > "$CF_RUN_DIR/harness.log"
  fi

  local process_timeout=35
  if [[ "$CF_SCENARIO" != "baseline" ]]; then
    process_timeout=60
  fi
  if ! cf_wait_for_all_processes "$process_timeout"; then
    cf_fail_infrastructure "$CF_WAIT_REASON"
    return $?
  fi
  cf_collect_statuses
  if ! cf_validate_results; then
    cf_fail_gameplay "$CF_VALIDATION_REASON"
    return $?
  fi

  cf_write_result_json "passed" "gameplay" "${CF_SCENARIO}_complete"
  cf_release_port
  CF_PORT_LOCK=""
  printf 'CF_LAUNCHER result=passed scenario=%s port=%s run_id=%s logs=%s\n' "$CF_SCENARIO" "$CF_PORT" "$CF_RUN_ID" "$CF_RUN_DIR"
  return "$CF_SUCCESS_EXIT"
}
