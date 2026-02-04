#!/bin/bash
set -e

CONTAINER_NAME="dekaf-local-kafka"
IMAGE="confluentinc/cp-kafka:7.5.0"
PORT=9092

# Stop and remove existing container if it exists
if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Stopping existing Kafka container..."
    docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
fi

echo "Starting Kafka container: $CONTAINER_NAME"
docker run -d \
    --name "$CONTAINER_NAME" \
    -p $PORT:9092 \
    -e KAFKA_NODE_ID=1 \
    -e KAFKA_PROCESS_ROLES=broker,controller \
    -e KAFKA_LISTENERS=PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093 \
    -e KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092 \
    -e KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER \
    -e KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT \
    -e KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093 \
    -e KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1 \
    -e KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR=1 \
    -e KAFKA_TRANSACTION_STATE_LOG_MIN_ISR=1 \
    -e KAFKA_LOG_DIRS=/var/lib/kafka/data \
    -e CLUSTER_ID=dekaf-local-cluster \
    -e KAFKA_LOG_RETENTION_MS=5000 \
    -e KAFKA_LOG_RETENTION_BYTES=67108864 \
    -e KAFKA_LOG_SEGMENT_BYTES=16777216 \
    -e KAFKA_LOG_SEGMENT_DELETE_DELAY_MS=100 \
    -e KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS=1000 \
    -e KAFKA_LOG_CLEANUP_POLICY=delete \
    "$IMAGE"

echo "Kafka container started successfully!"
echo ""
echo "Bootstrap servers: localhost:9092"
echo "Container name: $CONTAINER_NAME"
echo ""
echo "Retention settings:"
echo "  - Max retention: 5 seconds"
echo "  - Max size per partition: 64MB"
echo "  - Cleanup interval: 1 second"
echo ""
echo "To stop Kafka:"
echo "  docker stop $CONTAINER_NAME"
echo ""
echo "To remove Kafka:"
echo "  docker rm -f $CONTAINER_NAME"
echo ""
echo "Set environment variable for stress tests:"
echo "  export KAFKA_BOOTSTRAP_SERVERS=localhost:9092"
