#!/usr/bin/env bash
set -euo pipefail
source .github/pr-comparison/broker.sh
for segment in main-a candidate main-b; do
  version=main
  sha="$BASELINE_SHA"
  if [[ "$segment" == candidate ]]; then version=candidate; sha="$CANDIDATE_SHA"; fi
  output="$root/evidence/linger-0/$segment"
  mkdir -p "$output"
  start_broker > "$output/broker-start.log" 2>&1
  printf '%s\n' "sha=$sha" 'scenario=producer-async' 'lingerMs=0' 'durationMinutes=5' \
    'profiling=gc; windows=60:20:early,240:20:late' > "$output/identity.txt"
  set +e
  PROFILE_OUTPUT_DIR="$output/profile" TRACE_PROFILE=gc TRACE_WINDOWS='60:20:early,240:20:late' \
    PROFILE_COUNTERS=false PROFILE_STACKS=false PROFILE_GCDUMP=false \
    STRESS_CPUSET='6,7' PROFILER_CPUSET=5 \
    timeout --signal=TERM --kill-after=30s 9m bash "$root/versions/$version/tools/profile-stress-test.sh" \
      --duration 5 --message-size 1000 --scenario producer-async --client dekaf \
      --brokers 1 --connections-per-broker 1 --partitions 6 --linger-ms 0 \
      --producer-delivery-diagnostics --output "$output" > "$output/run.log" 2>&1
  run_exit=$?
  set -e
  printf '%s\n' "$run_exit" > "$output/exit-code.txt"
  docker logs kafka > "$output/broker.log" 2>&1
  docker rm -f kafka >/dev/null
  if [[ "$run_exit" != 0 ]]; then tail -60 "$output/run.log"; exit 1; fi
done
