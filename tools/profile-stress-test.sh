#!/usr/bin/env bash
# Profile Dekaf stress tests using dotnet trace (attach mode).
#
# This script starts the stress test as a normal process, waits for it to
# begin producing, then attaches dotnet-trace for a fixed sample window.
# Attaching separately avoids the massive overhead of launching through
# dotnet trace, which can make a 2-minute test take 30+ minutes.
#
# Usage:
#   ./tools/profile-stress-test.sh [options]
#
# Options (passed through to Dekaf.StressTests):
#   --duration <minutes>     Stress test duration (default: 5)
#   --scenario <name>        producer|consumer|all (default: producer)
#   --client <name>          dekaf|confluent|all (default: dekaf)
#   --message-size <bytes>   Message size (default: 1000)
#
# Environment:
#   TRACE_DURATION           Trace sample window in seconds (default: 180)
#   TRACE_SETTLE_TIME        Seconds to wait before attaching (default: 45)
#   KAFKA_BOOTSTRAP_SERVERS  External Kafka address (default: localhost:9092)
#   TRACE_OUTPUT             Output .nettrace file path
#   TRACE_PROVIDERS          dotnet-trace --providers value
#                            (default: Microsoft-DotNETCore-SampleProfiler)
#   TRACE_PROFILE            Preset profile: cpu (default), contention, gc, full
#                            Overridden by TRACE_PROVIDERS if set explicitly.
#   WAIT_FOR_EXIT            If "true", wait for stress test to finish and show
#                            its throughput output (default: true)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
STRESS_EXE="$REPO_ROOT/tools/Dekaf.StressTests/bin/Release/net10.0/Dekaf.StressTests.exe"

# Defaults
TRACE_DURATION="${TRACE_DURATION:-180}"
TRACE_SETTLE_TIME="${TRACE_SETTLE_TIME:-45}"
TRACE_PROFILE="${TRACE_PROFILE:-cpu}"
WAIT_FOR_EXIT="${WAIT_FOR_EXIT:-true}"
TRACE_OUTPUT="${TRACE_OUTPUT:-$REPO_ROOT/stress_profile.nettrace}"
export KAFKA_BOOTSTRAP_SERVERS="${KAFKA_BOOTSTRAP_SERVERS:-localhost:9092}"

# --- Provider presets ---
# Use TRACE_PROVIDERS env var to override, otherwise select by TRACE_PROFILE.
if [ -z "${TRACE_PROVIDERS:-}" ]; then
    case "$TRACE_PROFILE" in
        cpu)
            TRACE_PROVIDERS="Microsoft-DotNETCore-SampleProfiler"
            ;;
        contention)
            # ContentionKeyword (0x4000) + ThreadingKeyword (0x10000) + GC (0x1) + ExceptionKeyword (0x8000)
            TRACE_PROVIDERS="Microsoft-Windows-DotNETRuntime:0x000000000001C001:4,Microsoft-DotNETCore-SampleProfiler"
            ;;
        gc)
            # GCKeyword (0x1) + GCHeapSurvivalAndMovement (0x400000) + Type (0x80000)
            TRACE_PROVIDERS="Microsoft-Windows-DotNETRuntime:0x0000000000480001:5,Microsoft-DotNETCore-SampleProfiler"
            ;;
        full)
            # CPU + Contention + GC + Threading + Exceptions
            TRACE_PROVIDERS="Microsoft-Windows-DotNETRuntime:0x000000000049C001:4,Microsoft-DotNETCore-SampleProfiler"
            ;;
        *)
            echo "ERROR: Unknown TRACE_PROFILE '$TRACE_PROFILE'. Use: cpu, contention, gc, full"
            exit 1
            ;;
    esac
fi

# Pass remaining args to stress test (defaults if none given)
STRESS_ARGS=("$@")
if [ ${#STRESS_ARGS[@]} -eq 0 ]; then
    STRESS_ARGS=(--duration 5 --scenario producer --client dekaf --message-size 1000)
fi

# --- Preflight checks ---

if ! command -v dotnet-trace &>/dev/null && ! dotnet tool list -g 2>/dev/null | grep -q dotnet-trace; then
    echo "ERROR: dotnet-trace not found. Install with: dotnet tool install -g dotnet-trace"
    exit 1
fi

if [ ! -f "$STRESS_EXE" ]; then
    echo "Building stress tests in Release mode..."
    dotnet build "$REPO_ROOT/tools/Dekaf.StressTests" --configuration Release -q
fi

# --- Start stress test ---

STRESS_LOG=$(mktemp)

echo "Starting stress test: ${STRESS_ARGS[*]}"
echo "Profile: $TRACE_PROFILE | Settle: ${TRACE_SETTLE_TIME}s | Trace: ${TRACE_DURATION}s"
echo ""

"$STRESS_EXE" "${STRESS_ARGS[@]}" > >(tee "$STRESS_LOG") 2>&1 &
BASH_PID=$!

cleanup() {
    local exit_code=$?
    echo ""
    echo "Cleaning up..."
    kill "$BASH_PID" 2>/dev/null || true
    wait "$BASH_PID" 2>/dev/null || true
    # Also kill the Windows process if it's still running
    taskkill //F //IM "Dekaf.StressTests.exe" >/dev/null 2>&1 || true
    rm -f "$STRESS_LOG" 2>/dev/null || true
    exit $exit_code
}
trap cleanup EXIT

# On Windows/Git Bash, $! gives a bash PID, but dotnet trace needs the Windows PID.
# Wait briefly for the process to start, then find the real PID.
sleep 3
if command -v tasklist &>/dev/null; then
    STRESS_PID=$(tasklist 2>/dev/null | grep "Dekaf.StressTests" | awk '{print $2}' | head -1)
else
    STRESS_PID=$BASH_PID
fi

if [ -z "$STRESS_PID" ]; then
    echo "ERROR: Could not find Dekaf.StressTests.exe process."
    exit 1
fi

echo "Stress test PID: $STRESS_PID"
echo "Waiting ${TRACE_SETTLE_TIME}s for warmup..."
sleep "$TRACE_SETTLE_TIME"

# Verify process is still alive
if command -v tasklist &>/dev/null; then
    if ! tasklist 2>/dev/null | grep -q "Dekaf.StressTests"; then
        echo "ERROR: Stress test exited before trace could attach."
        echo ""
        echo "=== Stress test output ==="
        cat "$STRESS_LOG"
        exit 1
    fi
elif ! kill -0 "$STRESS_PID" 2>/dev/null; then
    echo "ERROR: Stress test exited before trace could attach."
    echo ""
    echo "=== Stress test output ==="
    cat "$STRESS_LOG"
    exit 1
fi

# --- Attach trace ---

echo "Attaching dotnet-trace for ${TRACE_DURATION}s (profile: $TRACE_PROFILE)..."
echo "Providers: $TRACE_PROVIDERS"
echo "Output: $TRACE_OUTPUT"
echo ""

# dotnet trace --duration uses HH:MM:SS format
HOURS=$((TRACE_DURATION / 3600))
MINS=$(( (TRACE_DURATION % 3600) / 60 ))
SECS=$((TRACE_DURATION % 60))
DURATION_FMT=$(printf '%02d:%02d:%02d' "$HOURS" "$MINS" "$SECS")

if ! dotnet trace collect \
    --process-id "$STRESS_PID" \
    --output "$TRACE_OUTPUT" \
    --providers "$TRACE_PROVIDERS" \
    --duration "$DURATION_FMT" \
    2>&1; then
    echo "WARNING: dotnet trace collect failed (process may have exited during collection)"
fi

echo ""
echo "Trace complete: $TRACE_OUTPUT"
echo ""

# --- Wait for stress test to finish (captures throughput output) ---

if [ "$WAIT_FOR_EXIT" = "true" ]; then
    echo "Waiting for stress test to finish..."
    # Disable exit-on-error for wait since the process may have been killed
    set +e
    wait "$BASH_PID" 2>/dev/null
    STRESS_EXIT=$?
    set -e

    # Reset BASH_PID so cleanup doesn't try to kill again
    BASH_PID=0

    echo ""
    echo "============================================"
    echo "=== Stress Test Results ==="
    echo "============================================"
    # Show the throughput/results portion of the output (skip container startup noise)
    grep -E '(msg/sec|MB/sec|messages|throughput|Total|Average|Median|P99|P95|p50|p95|p99|percentile|Duration|Results|===|Completed|Progress|GC|Gen[012]|Allocated|Ratio|Client|Flushing|Resources)' "$STRESS_LOG" 2>/dev/null || cat "$STRESS_LOG"
    echo ""
fi

# --- Trace Report ---

echo "============================================"
echo "=== Trace Analysis ==="
echo "============================================"
echo ""

echo "--- Top 30 methods by exclusive CPU time ---"
dotnet trace report "$TRACE_OUTPUT" topN -n 30 2>&1 || echo "(report failed — trace may be too short)"

echo ""
echo "--- Top 30 methods by inclusive CPU time ---"
dotnet trace report "$TRACE_OUTPUT" topN --inclusive -n 30 2>&1 || echo "(report failed)"

echo ""

# --- Speedscope conversion ---

SPEEDSCOPE_OUTPUT="${TRACE_OUTPUT%.nettrace}.speedscope.json"
echo "Converting to Speedscope format..."
if dotnet trace convert "$TRACE_OUTPUT" --format Speedscope --output "$SPEEDSCOPE_OUTPUT" 2>&1; then
    echo "Speedscope file: $SPEEDSCOPE_OUTPUT"
    echo "Open in https://www.speedscope.app/"
else
    echo "(Speedscope conversion failed)"
fi

echo ""
echo "Done."
