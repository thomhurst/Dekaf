#!/usr/bin/env bash
set -euo pipefail
root="$PWD"
out="$root/comparison-results"
mkdir -p "$out"
trap 'docker rm -f release-comparison-kafka >/dev/null 2>&1 || true' EXIT
cp tools/ProducerReleaseComparison/Protocol.md "$out/Protocol.md"
git rev-parse HEAD > "$out/harness-sha.txt"
git rev-parse HEAD:src > "$out/product-tree.txt"
dotnet --info > "$out/dotnet-info.txt"
lscpu > "$out/lscpu.txt"
env | sort | grep -E '^(DOTNET_|COMPlus_|Image)' > "$out/runtime-env.txt" || true
fixture="$RUNNER_TEMP/release-fixture"
cp -r tools/ProducerReleaseComparison "$fixture"
dotnet build "$fixture/Compare.csproj" -c Release -o "$out/a-bin" > "$out/build-a.log" 2>&1
# Separate source directory prevents NuGet assets / intermediate output cross-contamination.
cp -r tools/ProducerReleaseComparison "$RUNNER_TEMP/candidate-fixture"
dotnet build "$RUNNER_TEMP/candidate-fixture/Compare.csproj" -c Release \
  -p:CandidateProject="$root/src/Dekaf/Dekaf.csproj" -o "$out/b-bin" > "$out/build-b.log" 2>&1
cp "$fixture/obj/project.assets.json" "$out/a-assets.json"
cp "$RUNNER_TEMP/candidate-fixture/obj/project.assets.json" "$out/b-assets.json"
find "$out/a-bin" "$out/b-bin" -name '*.dll' -exec sha256sum {} + > "$out/binary-sha256.txt"
dotnet build-server shutdown
start_broker() {
  docker run -d --name release-comparison-kafka --cpuset-cpus=0-5 \
    --tmpfs /var/lib/kafka/data:rw,size=6g,mode=1777 -p 9092:9092 \
    -e KAFKA_HEAP_OPTS='-Xmx2g -Xms2g' \
    -e KAFKA_NODE_ID=1 -e KAFKA_PROCESS_ROLES=broker,controller \
    -e KAFKA_LISTENERS=PLAINTEXT://:9092,CONTROLLER://:9093 \
    -e KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092 \
    -e KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER \
    -e KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT \
    -e KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093 \
    -e KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1 \
    -e KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR=1 -e KAFKA_TRANSACTION_STATE_LOG_MIN_ISR=1 \
    -e KAFKA_LOG_DIRS=/var/lib/kafka/data -e CLUSTER_ID=MkU3OEVBNTcwNTJENDM2Qg \
    -e KAFKA_LOG_RETENTION_MS=10000 -e KAFKA_LOG_RETENTION_BYTES=134217728 \
    -e KAFKA_LOG_SEGMENT_BYTES=33554432 -e KAFKA_LOG_SEGMENT_DELETE_DELAY_MS=100 \
    -e KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS=1000 -e KAFKA_LOG_INITIAL_TASK_DELAY_MS=1000 \
    -e KAFKA_LOG_CLEANUP_POLICY=delete apache/kafka:4.3.1
  for i in $(seq 1 60); do
    if docker exec release-comparison-kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 \
      --create --topic comparison --partitions 6 --replication-factor 1 >/dev/null 2>&1; then return; fi
    sleep 2
  done
  return 1
}
for segment in validation-a validation-b A1 B1 A2 B2; do
  mkdir -p "$out/$segment"
  start_broker
  python3 tools/ProducerReleaseComparison/offsets.py --once > "$out/$segment/offset-start.jsonl"
  taskset -c 0-5 python3 tools/ProducerReleaseComparison/offsets.py > "$out/$segment/offsets.jsonl" &
  observer=$!
  case "$segment" in *a|A*) binary=a-bin;; *) binary=b-bin;; esac
  case "$segment" in validation-*) duration=20; warmup=20;; *) duration=900; warmup=180;; esac
  echo "Starting $segment: $binary, ${duration}s measured, ${warmup}s warmup"
  taskset -c 6,7 dotnet "$out/$binary/Dekaf.StressTests.dll" "$duration" "$warmup" "$out/$segment" \
    2>&1 | tee "$out/$segment/client.log"
  kill "$observer"; wait "$observer" || true
  python3 tools/ProducerReleaseComparison/offsets.py --once > "$out/$segment/offset-end.jsonl"
  docker logs release-comparison-kafka > "$out/$segment/broker.log" 2>&1
  docker inspect release-comparison-kafka > "$out/$segment/broker-inspect.json"
  docker rm -f release-comparison-kafka
done
