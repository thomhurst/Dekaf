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

