#!/usr/bin/env bash
set -euo pipefail
source .github/pr-comparison/broker.sh

for segment in main-a fetch main-b pool main-c; do
  version=main
  [[ "$segment" == fetch || "$segment" == pool ]] && version="$segment"
  output="$root/evidence/$segment"
  mkdir -p "$output"
  start_broker > "$output/broker-start.log" 2>&1
  args=(--duration 5 --message-size 1000 --scenario consumer-raw --client dekaf
    --brokers 1 --connections-per-broker 1 --partitions 6 --linger-ms 5
    --seed-messages 2000000 --output "$output")
  git -C "versions/$version" rev-parse HEAD > "$output/sha.txt"
  printf '%q ' taskset -c "$CLIENT_CPUS" dotnet "$root/versions/$version/tools/Dekaf.StressTests/bin/Release/net10.0/Dekaf.StressTests.dll" "${args[@]}" > "$output/command.txt"
  echo "START $segment $(date -u +%FT%TZ)"
  set +e
  timeout --signal=TERM --kill-after=30s 10m taskset -c "$CLIENT_CPUS" \
    dotnet "$root/versions/$version/tools/Dekaf.StressTests/bin/Release/net10.0/Dekaf.StressTests.dll" \
    "${args[@]}" > "$output/run.log" 2>&1
  run_exit=$?
  set -e
  echo "$run_exit" > "$output/exit-code.txt"
  docker logs kafka > "$output/broker.log" 2>&1
  docker exec kafka /opt/kafka/bin/kafka-configs.sh --bootstrap-server localhost:9092 \
    --entity-type topics --describe > "$output/topic-configs.txt" 2>&1
  docker exec kafka df -h /var/lib/kafka/data > "$output/broker-disk.txt"
  docker inspect -f '{{.State.Status}}' kafka > "$output/broker-state.txt"
  docker rm -f kafka >/dev/null
  tail -35 "$output/run.log"
  [[ "$run_exit" == 0 ]] || exit "$run_exit"
  python3 .github/pr-comparison/consumer-verdict.py validate "$output"
  echo "DONE $segment $(date -u +%FT%TZ)"
done
