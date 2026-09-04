#!/usr/bin/env bash
set -euo pipefail
root="$PWD"
mkdir -p evidence
trap 'docker rm -f kafka >/dev/null 2>&1 || true' EXIT

start_broker() {
  docker run -d --name kafka --cpuset-cpus="$BROKER_CPUS" \
    --tmpfs "/var/lib/kafka/data:rw,size=$BROKER_TMPFS,mode=1777" -p 9092:9092 \
    -e KAFKA_HEAP_OPTS="$BROKER_HEAP" -e KAFKA_NODE_ID=1 \
    -e KAFKA_PROCESS_ROLES=broker,controller \
    -e KAFKA_LISTENERS=PLAINTEXT://:9092,CONTROLLER://:9093 \
    -e KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092 \
    -e KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER \
    -e KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT \
    -e KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093 \
    -e KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1 \
    -e KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR=1 \
    -e KAFKA_TRANSACTION_STATE_LOG_MIN_ISR=1 \
    -e KAFKA_LOG_DIRS=/var/lib/kafka/data \
    -e CLUSTER_ID=MkU3OEVBNTcwNTJENDM2Qg \
    -e KAFKA_LOG_RETENTION_MS=300000 -e KAFKA_LOG_RETENTION_BYTES=67108864 \
    -e KAFKA_LOG_SEGMENT_BYTES=16777216 -e KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS=500 \
    -e KAFKA_LOG_INITIAL_TASK_DELAY_MS=1000 apache/kafka:4.3.1
  for attempt in $(seq 1 60); do
    if docker exec kafka /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server localhost:9092 >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done
  docker logs kafka
  return 1
}

for linger in $LINGERS; do
  for segment in main-a candidate main-b; do
    version=main
    sha="$BASELINE_SHA"
    if [[ "$segment" == candidate ]]; then version=candidate; sha="$CANDIDATE_SHA"; fi
    output="$root/evidence/linger-$linger/$segment"
    mkdir -p "$output"
    # Fresh broker per segment prevents retained consumer seeds from accumulating
    # on tmpfs and gives both revisions the same broker setup.
    start_broker > "$output/broker-start.log" 2>&1
    args=(--duration "$DURATION_MINUTES" --message-size 1000 --scenario "$SCENARIO"
      --client dekaf --brokers 1 --connections-per-broker 1 --partitions 6
      --linger-ms "$linger" --seed-messages 2000000 --output "$output")
    if [[ "$SCENARIO" == producer* ]]; then args+=(--producer-delivery-diagnostics); fi
    printf '%s\n' "sha=$sha" "scenario=$SCENARIO" "lingerMs=$linger" "durationMinutes=$DURATION_MINUTES" \
      "clientCpus=$CLIENT_CPUS" "brokerCpus=$BROKER_CPUS" "profiling=off" > "$output/identity.txt"
    printf '%q ' taskset -c "$CLIENT_CPUS" dotnet "$root/versions/$version/tools/Dekaf.StressTests/bin/Release/net10.0/Dekaf.StressTests.dll" "${args[@]}" > "$output/command.txt"
    echo "START PR=$PR_NUMBER linger=$linger segment=$segment sha=$sha $(date -u +%FT%TZ)"
    set +e
    timeout --signal=TERM --kill-after=30s 9m taskset -c "$CLIENT_CPUS" \
      dotnet "$root/versions/$version/tools/Dekaf.StressTests/bin/Release/net10.0/Dekaf.StressTests.dll" \
      "${args[@]}" 2>&1 | tee "$output/run.log"
    run_exit=${PIPESTATUS[0]}
    set -e
    printf '%s\n' "$run_exit" > "$output/exit-code.txt"
    docker logs kafka > "$output/broker.log" 2>&1
    broker_state=$(docker inspect -f '{{.State.Status}}' kafka)
    docker exec kafka df -h /var/lib/kafka/data > "$output/broker-disk.txt" || true
    docker rm -f kafka >/dev/null
    if [[ "$run_exit" != 0 || "$broker_state" != running ]]; then
      echo "Invalid segment: exit=$run_exit broker=$broker_state"
      exit 1
    fi
    echo "DONE PR=$PR_NUMBER linger=$linger segment=$segment $(date -u +%FT%TZ)"
  done
done
