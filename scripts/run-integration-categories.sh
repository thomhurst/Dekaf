#!/usr/bin/env bash
# Runs Dekaf integration test categories against prebuilt test binaries.
#
# Usage: run-integration-categories.sh <net8.0|net10.0|aot> <comma-separated-categories> [--list-tests]
#
# Expects binaries at tests/Dekaf.Tests.Integration/bin/Release/<tfm>/ (managed)
# or artifacts/aot/integration/ (NativeAOT), as produced by the CI build job.
# Fallback if the apphost lost its executable bit and chmod is unavailable:
#   dotnet exec tests/Dekaf.Tests.Integration/bin/Release/<tfm>/Dekaf.Tests.Integration.dll <args>
#
# Single source of truth for category filters and hang budgets. Every CI lane
# calls this script. Tests declare shared-resource constraints with NotInParallel.
set -euo pipefail

if { [ $# -ne 2 ] && [ $# -ne 3 ]; } || { [ $# -eq 3 ] && [ "$3" != "--list-tests" ]; }; then
  echo "Usage: $0 <net8.0|net10.0|aot> <comma-separated-categories> [--list-tests]" >&2
  exit 2
fi

tfm="$1"
IFS=',' read -ra categories <<< "$2"

if [ "$tfm" = "aot" ]; then
  exe="artifacts/aot/integration/Dekaf.Tests.Integration"
  export NET_VERSION="net10.0"
else
  exe="tests/Dekaf.Tests.Integration/bin/Release/$tfm/Dekaf.Tests.Integration"
  export NET_VERSION="$tfm"
fi

if [ ! -f "$exe" ]; then
  echo "Test executable not found: $exe" >&2
  exit 1
fi
chmod +x "$exe"

# Aggressive GC to reduce memory pressure on CI runners.
export DOTNET_GCConserveMemory=9

# Per-test OTel spans are the primary post-mortem evidence for timing failures
# (#2199 was solved from them). The default 100-span cap truncated them in run
# 29612262949, leaving the failing test with zero spans. Overridable from the
# environment.
export TUNIT_OTEL_MAX_EXTERNAL_SPANS="${TUNIT_OTEL_MAX_EXTERNAL_SPANS:-5000}"

# NativeAOT has no diagnostics IPC server, so MTP's --hangdump can never dump an
# AOT test process (run 38631204581 failed with ServerNotAvailableException and
# left only the list of running tests). AOT lanes use a watchdog instead: when a
# category passes its deadline, or the run reaches its absolute deadline, it
# captures native thread stacks with gdb (plus a capped gcore core) into
# TestResults/<category>/, then kills the process so the job fails with
# evidence instead of timing out. The executable's .dbg file sits next to it,
# so gdb resolves managed method names.
#
# Workflows set INTEGRATION_WATCHDOG_DEADLINE_EPOCH from the job's start time
# and timeout, minus a reserve for the diagnostics (bounded at about 6 minutes)
# and the artifact uploads. Without it, the run budget counts from this
# script's start.
watchdog_category_seconds="${INTEGRATION_WATCHDOG_CATEGORY_SECONDS:-720}"
watchdog_poll_seconds="${INTEGRATION_WATCHDOG_POLL_SECONDS:-5}"
watchdog_run_deadline="${INTEGRATION_WATCHDOG_DEADLINE_EPOCH:-$(( $(date +%s) + ${INTEGRATION_WATCHDOG_TOTAL_SECONDS:-1080} ))}"
if [ "$tfm" = "aot" ]; then
  # Let a crashing (SIGABRT/SIGSEGV) AOT process leave a core where the
  # workflow's kernel.core_pattern points.
  ulimit -c unlimited 2>/dev/null || true
fi

ensure_gdb() {
  command -v gdb >/dev/null 2>&1 && command -v gcore >/dev/null 2>&1 && return 0
  echo "Installing gdb for native stack capture..."
  timeout 45s sudo apt-get update -qq >/dev/null 2>&1 || true
  timeout 60s sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends gdb >/dev/null 2>&1 || true
  command -v gdb >/dev/null 2>&1
}

# Worst case about 6 minutes: gdb install 105 s, a backtrace per process (90 s
# cap, normally seconds), one core of the test process 120 s, gzip 60 s.
capture_native_diagnostics() {
  local root_pid="$1" out_dir="$2" pids pid
  mkdir -p "$out_dir"
  # The test process is normally the root; include descendants in case an
  # extension launched a separate test host.
  # pgrep exits 1 without children; pipefail must not turn that into a failure.
  pids="$root_pid $( { pgrep -P "$root_pid" 2>/dev/null || true; } | tr '\n' ' ')"
  if ! ensure_gdb; then
    echo "::warning::gdb is unavailable; no native stacks captured for $category"
    return 0
  fi
  for pid in $pids; do
    kill -0 "$pid" 2>/dev/null || continue
    echo "Capturing native thread stacks of process $pid into $out_dir"
    timeout 90s sudo gdb -p "$pid" -batch -nx \
      -ex "set debuginfod enabled off" \
      -ex "set pagination off" \
      -ex "set print frame-arguments none" \
      -ex "info threads" \
      -ex "thread apply all bt" \
      > "$out_dir/native-stacks-$pid.txt" 2>&1 || true
  done
  # The core supplements the stacks: only the test process, and capped.
  echo "Capturing core of process $root_pid"
  if timeout 120s sudo gcore -o "$out_dir/core" "$root_pid" > "$out_dir/gcore-$root_pid.log" 2>&1; then
    sudo chmod a+r "$out_dir/core.$root_pid" 2>/dev/null || true
    timeout 60s gzip -1 "$out_dir/core.$root_pid" || rm -f "$out_dir/core.$root_pid.gz"
  fi
}

# Runs the test executable, enforcing the category and run deadlines.
run_with_watchdog() {
  local out_dir="$1"; shift
  local category_started deadline status now
  category_started=$(date +%s)
  deadline=$((category_started + watchdog_category_seconds))
  [ "$watchdog_run_deadline" -lt "$deadline" ] && deadline=$watchdog_run_deadline

  "$@" &
  local pid=$!
  while kill -0 "$pid" 2>/dev/null; do
    now=$(date +%s)
    if [ "$now" -ge "$deadline" ]; then
      echo "::error::Category $category hit its watchdog deadline after $((now - category_started))s; capturing native diagnostics before killing process $pid"
      capture_native_diagnostics "$pid" "$out_dir"
      pkill -KILL -P "$pid" 2>/dev/null || true
      kill -KILL "$pid" 2>/dev/null || true
      wait "$pid" 2>/dev/null || true
      return 124
    fi
    sleep "$watchdog_poll_seconds"
  done
  status=0
  wait "$pid" || status=$?
  return "$status"
}

for category in "${categories[@]}"; do
  if [ "$tfm" = "aot" ] && [ "$category" = "Interop" ]; then
    echo "Skipping Interop for NativeAOT because Confluent.Kafka requires runtime reflection"
    continue
  fi

  # The two ShareConsumer shards partition the original category. Untagged
  # future tests automatically join ShareConsumerOther, preserving coverage.
  filter="/**[Category=$category]"
  case "$category" in
    ShareConsumerCore)
      filter="/**[(Category=ShareConsumer)&(Category=ShareConsumerCore)]"
      ;;
    ShareConsumerOther)
      filter="/**[(Category=ShareConsumer)&(Category!=ShareConsumerCore)]"
      ;;
    CatchAll)
      # EventHubs and Redpanda have dedicated CI lanes outside this matrix. Expand virtual
      # ShareConsumer shards to their parent so uncategorized/new tests remain
      # selected without rerunning anything assigned to another job.
      covered_categories="$(python3 -c 'import json, sys
matrix = json.load(sys.stdin)
groups, categories = matrix["groups"], matrix["categories"]
if not groups or len(groups) != len(set(groups)) or set(groups) != set(categories):
    sys.exit("Scheduled matrix groups and category map must match exactly")
print(",".join(categories[group] for group in groups))' \
        <<< "${INTEGRATION_TEST_MATRIX_JSON:?CatchAll requires the matrix category map}")"
      IFS=',' read -ra covered <<< "$covered_categories"
      filter="/**[(Category!=EventHubs)&(Category!=Redpanda)"
      for excluded in "${covered[@]}"; do
        case "$excluded" in
          CatchAll) continue ;;
          ShareConsumerCore|ShareConsumerOther) excluded=ShareConsumer ;;
        esac
        if [[ ! "$excluded" =~ ^[A-Za-z][A-Za-z0-9]*$ ]]; then
          echo "Invalid matrix category: $excluded" >&2
          exit 2
        fi
        if [[ "$filter" != *"(Category!=$excluded)"* ]]; then
          filter+="&(Category!=$excluded)"
        fi
      done
      filter+="]"
      ;;
  esac

  echo "::group::Category $category"
  args=()
  if [ "$tfm" != "aot" ]; then
    args+=(--hangdump --hangdump-timeout 5m)
  fi
  args+=(
    --log-level Debug
    --treenode-filter "$filter"
    --results-directory "TestResults/$category"
  )
  if [ "$category" = "CatchAll" ]; then
    # The fallback is empty when every test belongs to an explicit CI category.
    # MTP exit code 8 means zero tests; assertion and infrastructure failures still fail.
    args+=(--ignore-exit-code 8)
  fi
  if [ "${3:-}" = "--list-tests" ]; then
    args+=(--list-tests --no-ansi)
  fi
  if [ "$tfm" = "aot" ] && [ "${3:-}" != "--list-tests" ]; then
    run_with_watchdog "TestResults/$category" "$exe" "${args[@]}"
  else
    "$exe" "${args[@]}"
  fi
  echo "::endgroup::"
done
