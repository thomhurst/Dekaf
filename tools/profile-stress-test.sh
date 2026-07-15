#!/usr/bin/env bash
# Capture comparable diagnostics at named offsets from a stress test's measured
# phase. The target runs normally; diagnostic tools attach to its exact PID.
#
# Example:
#   TRACE_WINDOWS='60:30:healthy,420:30:collapse,600:30:degraded' \
#   STRESS_CPUSET='6,7' PROFILER_CPUSET='5' \
#   bash ./tools/profile-stress-test.sh --duration 15 --scenario producer --client dekaf
#
# Environment:
#   TRACE_WINDOWS         Comma-separated offset:duration:label windows.
#                         Offsets are seconds from measured-phase start.
#   TRACE_START_PATTERN   Extended regex identifying measured-phase start.
#   TRACE_PROFILE         cpu (default), contention, gc, or full.
#   TRACE_PROVIDERS       Explicit dotnet-trace providers; overrides profile.
#   PROFILE_OUTPUT_DIR    Artifact directory (default: stress-profile).
#   PROFILE_COUNTERS      Collect System.Runtime counters (default: true).
#   PROFILE_STACKS        Capture a stack snapshot per window (default: true).
#   PROFILE_VALIDATE_ONLY Validate configuration without starting a test.
#   PROFILE_START_TIMEOUT Seconds allowed for measured phase to start (default: 300).
#   STRESS_CPUSET         Optional target process CPU affinity.
#   PROFILER_CPUSET       Optional diagnostics process CPU affinity.
#   KAFKA_BOOTSTRAP_SERVERS External Kafka address (default: localhost:9092).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
STRESS_EXE="$REPO_ROOT/tools/Dekaf.StressTests/bin/Release/net10.0/Dekaf.StressTests"
if [[ "${OS:-}" == "Windows_NT" ]]; then
    STRESS_EXE="${STRESS_EXE}.exe"
fi

TRACE_WINDOWS="${TRACE_WINDOWS:-60:30:healthy,330:30:pre-collapse,420:30:collapse,600:30:degraded}"
TRACE_START_PATTERN="${TRACE_START_PATTERN:-Running .* stress test for}"
TRACE_PROFILE="${TRACE_PROFILE:-cpu}"
PROFILE_OUTPUT_DIR="${PROFILE_OUTPUT_DIR:-$REPO_ROOT/stress-profile}"
PROFILE_COUNTERS="${PROFILE_COUNTERS:-true}"
PROFILE_STACKS="${PROFILE_STACKS:-true}"
PROFILE_VALIDATE_ONLY="${PROFILE_VALIDATE_ONLY:-false}"
PROFILE_START_TIMEOUT="${PROFILE_START_TIMEOUT:-300}"
export KAFKA_BOOTSTRAP_SERVERS="${KAFKA_BOOTSTRAP_SERVERS:-localhost:9092}"

fail() {
    echo "ERROR: $*" >&2
    exit 1
}

is_positive_integer() {
    [[ "$1" =~ ^[0-9]+$ ]] && (( 10#$1 > 0 ))
}

is_nonnegative_integer() {
    [[ "$1" =~ ^[0-9]+$ ]]
}

format_duration() {
    local total="$1"
    printf '%02d:%02d:%02d:%02d' \
        "$((total / 86400))" "$(((total % 86400) / 3600))" \
        "$(((total % 3600) / 60))" "$((total % 60))"
}

case "$TRACE_PROFILE" in
    cpu)
        DEFAULT_TRACE_PROVIDERS="Microsoft-DotNETCore-SampleProfiler"
        ;;
    contention)
        DEFAULT_TRACE_PROVIDERS="Microsoft-Windows-DotNETRuntime:0x000000000001C001:4,Microsoft-DotNETCore-SampleProfiler"
        ;;
    gc)
        DEFAULT_TRACE_PROVIDERS="Microsoft-Windows-DotNETRuntime:0x0000000000480001:5,Microsoft-DotNETCore-SampleProfiler"
        ;;
    full)
        DEFAULT_TRACE_PROVIDERS="Microsoft-Windows-DotNETRuntime:0x000000000049C001:4,Microsoft-DotNETCore-SampleProfiler"
        ;;
    *)
        fail "Unknown TRACE_PROFILE '$TRACE_PROFILE'. Use cpu, contention, gc, or full."
        ;;
esac
TRACE_PROVIDERS="${TRACE_PROVIDERS:-$DEFAULT_TRACE_PROVIDERS}"

IFS=',' read -r -a WINDOW_SPECS <<< "$TRACE_WINDOWS"
WINDOW_OFFSETS=()
WINDOW_DURATIONS=()
WINDOW_LABELS=()
declare -A SEEN_WINDOW_LABELS=()
previous_end=0
for spec in "${WINDOW_SPECS[@]}"; do
    IFS=':' read -r offset duration label extra <<< "$spec"
    [[ -n "${offset:-}" && -n "${duration:-}" && -n "${label:-}" && -z "${extra:-}" ]] \
        || fail "Invalid trace window '$spec'; expected offset:duration:label."
    is_nonnegative_integer "$offset" || fail "Window offset must be a non-negative integer: '$spec'."
    is_positive_integer "$duration" || fail "Window duration must be a positive integer: '$spec'."
    [[ "$label" =~ ^[A-Za-z0-9._-]+$ ]] || fail "Unsafe window label '$label'."
    [[ -z "${SEEN_WINDOW_LABELS[$label]:-}" ]] || fail "Duplicate window label '$label'."
    (( 10#$offset >= previous_end )) || fail "Trace windows must be sorted and non-overlapping: '$spec'."
    WINDOW_OFFSETS+=("$((10#$offset))")
    WINDOW_DURATIONS+=("$((10#$duration))")
    WINDOW_LABELS+=("$label")
    SEEN_WINDOW_LABELS[$label]=1
    previous_end=$((10#$offset + 10#$duration))
done
(( ${#WINDOW_LABELS[@]} > 0 )) || fail "TRACE_WINDOWS must contain at least one window."

STRESS_ARGS=("$@")
if (( ${#STRESS_ARGS[@]} == 0 )); then
    STRESS_ARGS=(--duration 15 --scenario producer --client dekaf --message-size 1000)
fi

stress_duration_minutes=""
for ((i = 0; i < ${#STRESS_ARGS[@]}; i++)); do
    if [[ "${STRESS_ARGS[$i]}" == "--duration" && $((i + 1)) -lt ${#STRESS_ARGS[@]} ]]; then
        stress_duration_minutes="${STRESS_ARGS[$((i + 1))]}"
        break
    fi
done
is_positive_integer "$stress_duration_minutes" \
    || fail "Stress arguments must contain '--duration <positive integer minutes>'."
(( previous_end <= 10#$stress_duration_minutes * 60 )) \
    || fail "Last trace window ends after the ${stress_duration_minutes}-minute measured phase."
is_positive_integer "$PROFILE_START_TIMEOUT" || fail "PROFILE_START_TIMEOUT must be a positive integer."

for toggle in PROFILE_COUNTERS PROFILE_STACKS PROFILE_VALIDATE_ONLY; do
    value="${!toggle}"
    [[ "$value" == "true" || "$value" == "false" ]] || fail "$toggle must be true or false."
done

if [[ "$PROFILE_VALIDATE_ONLY" == "true" ]]; then
    echo "Valid profile: $TRACE_PROFILE; measured duration: ${stress_duration_minutes}m; windows: $TRACE_WINDOWS"
    exit 0
fi

command -v dotnet-trace >/dev/null || fail "dotnet-trace not found."
if [[ "$PROFILE_COUNTERS" == "true" ]]; then
    command -v dotnet-counters >/dev/null || fail "dotnet-counters not found."
fi
if [[ "$PROFILE_STACKS" == "true" ]]; then
    command -v dotnet-stack >/dev/null || fail "dotnet-stack not found."
fi
if [[ -n "${STRESS_CPUSET:-}" || -n "${PROFILER_CPUSET:-}" ]]; then
    command -v taskset >/dev/null || fail "CPU affinity requested but taskset not found."
fi

if [[ ! -f "$STRESS_EXE" ]]; then
    echo "Building stress tests in Release mode..."
    dotnet build "$REPO_ROOT/tools/Dekaf.StressTests" --configuration Release -q
fi

mkdir -p "$PROFILE_OUTPUT_DIR"
STRESS_LOG="$PROFILE_OUTPUT_DIR/stress-output.log"
METADATA="$PROFILE_OUTPUT_DIR/profile-metadata.txt"
: > "$STRESS_LOG"

STRESS_PREFIX=()
PROFILER_PREFIX=()
[[ -n "${STRESS_CPUSET:-}" ]] && STRESS_PREFIX=(taskset -c "$STRESS_CPUSET")
[[ -n "${PROFILER_CPUSET:-}" ]] && PROFILER_PREFIX=(taskset -c "$PROFILER_CPUSET")

STRESS_PID=0
COUNTERS_PID=0
PROFILE_FAILED=0
cleanup() {
    local exit_code=$?
    trap - EXIT INT TERM
    if (( COUNTERS_PID > 0 )); then
        kill -INT "$COUNTERS_PID" 2>/dev/null || true
        wait "$COUNTERS_PID" 2>/dev/null || true
    fi
    if (( STRESS_PID > 0 )); then
        kill "$STRESS_PID" 2>/dev/null || true
        wait "$STRESS_PID" 2>/dev/null || true
        if [[ "${OS:-}" == "Windows_NT" ]]; then
            taskkill //F //PID "$STRESS_PID" >/dev/null 2>&1 || true
        fi
    fi
    exit "$exit_code"
}
trap cleanup EXIT INT TERM

echo "Starting stress test: ${STRESS_ARGS[*]}"
echo "Profile: $TRACE_PROFILE | windows: $TRACE_WINDOWS"
"${STRESS_PREFIX[@]}" "$STRESS_EXE" "${STRESS_ARGS[@]}" > >(tee "$STRESS_LOG") 2>&1 &
STRESS_PID=$!
echo "Stress test PID: $STRESS_PID"

start_waited=0
until grep -Eq "$TRACE_START_PATTERN" "$STRESS_LOG"; do
    kill -0 "$STRESS_PID" 2>/dev/null || fail "Stress test exited before measured phase started."
    (( start_waited < 10#$PROFILE_START_TIMEOUT )) \
        || fail "Measured phase marker not seen after ${PROFILE_START_TIMEOUT}s: $TRACE_START_PATTERN"
    sleep 1
    ((start_waited += 1))
done
MEASURED_START_EPOCH=$(date +%s)
echo "Measured phase detected at $(date -u '+%Y-%m-%dT%H:%M:%SZ')"

{
    echo "stress_pid=$STRESS_PID"
    echo "measured_start_epoch=$MEASURED_START_EPOCH"
    echo "trace_profile=$TRACE_PROFILE"
    echo "trace_providers=$TRACE_PROVIDERS"
    echo "trace_windows=$TRACE_WINDOWS"
    echo "stress_cpuset=${STRESS_CPUSET:-unrestricted}"
    echo "profiler_cpuset=${PROFILER_CPUSET:-unrestricted}"
    printf 'stress_args='
    printf '%q ' "${STRESS_ARGS[@]}"
    echo
} > "$METADATA"

if [[ "$PROFILE_COUNTERS" == "true" ]]; then
    echo "Starting System.Runtime counters..."
    "${PROFILER_PREFIX[@]}" dotnet-counters collect \
        --process-id "$STRESS_PID" \
        --refresh-interval 1 \
        --format csv \
        --output "$PROFILE_OUTPUT_DIR/runtime-counters.csv" \
        --counters System.Runtime \
        > "$PROFILE_OUTPUT_DIR/runtime-counters.log" 2>&1 &
    COUNTERS_PID=$!
fi

for ((i = 0; i < ${#WINDOW_LABELS[@]}; i++)); do
    offset="${WINDOW_OFFSETS[$i]}"
    duration="${WINDOW_DURATIONS[$i]}"
    label="${WINDOW_LABELS[$i]}"
    while (( $(date +%s) - MEASURED_START_EPOCH < offset )); do
        kill -0 "$STRESS_PID" 2>/dev/null || fail "Stress test exited before '$label' window."
        sleep 1
    done

    echo "Capturing '$label' at +${offset}s for ${duration}s..."
    if [[ "$PROFILE_STACKS" == "true" ]]; then
        if ! "${PROFILER_PREFIX[@]}" dotnet-stack report --process-id "$STRESS_PID" \
            > "$PROFILE_OUTPUT_DIR/${label}.stacks.txt" 2>&1; then
            echo "WARNING: stack capture failed for '$label'."
            PROFILE_FAILED=1
        fi
    fi

    trace_path="$PROFILE_OUTPUT_DIR/${label}.nettrace"
    if ! "${PROFILER_PREFIX[@]}" dotnet-trace collect \
        --process-id "$STRESS_PID" \
        --output "$trace_path" \
        --providers "$TRACE_PROVIDERS" \
        --duration "$(format_duration "$duration")" \
        > "$PROFILE_OUTPUT_DIR/${label}.collect.log" 2>&1; then
        echo "WARNING: trace capture failed for '$label'."
        PROFILE_FAILED=1
    fi
done

set +e
wait "$STRESS_PID"
STRESS_EXIT=$?
set -e
STRESS_PID=0

if (( COUNTERS_PID > 0 )); then
    kill -INT "$COUNTERS_PID" 2>/dev/null || true
    wait "$COUNTERS_PID" 2>/dev/null || true
    COUNTERS_PID=0
    if [[ ! -s "$PROFILE_OUTPUT_DIR/runtime-counters.csv" ]]; then
        echo "WARNING: runtime counter capture produced no data."
        PROFILE_FAILED=1
    fi
fi

# Analyze only after the measurement ends, so report/convert CPU cannot perturb it.
for label in "${WINDOW_LABELS[@]}"; do
    trace_path="$PROFILE_OUTPUT_DIR/${label}.nettrace"
    [[ -f "$trace_path" ]] || continue
    echo "Analyzing '$label'..."
    "${PROFILER_PREFIX[@]}" dotnet-trace report "$trace_path" topN -n 50 \
        > "$PROFILE_OUTPUT_DIR/${label}.topN-exclusive.txt" 2>&1 || true
    "${PROFILER_PREFIX[@]}" dotnet-trace report "$trace_path" topN --inclusive -n 50 \
        > "$PROFILE_OUTPUT_DIR/${label}.topN-inclusive.txt" 2>&1 || true
    "${PROFILER_PREFIX[@]}" dotnet-trace convert "$trace_path" --format Speedscope \
        --output "$PROFILE_OUTPUT_DIR/${label}.speedscope.json" \
        > "$PROFILE_OUTPUT_DIR/${label}.convert.log" 2>&1 || true
done

echo "Profile artifacts: $PROFILE_OUTPUT_DIR"
if (( PROFILE_FAILED > 0 && STRESS_EXIT == 0 )); then
    exit 1
fi
exit "$STRESS_EXIT"
