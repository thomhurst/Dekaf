#!/bin/bash
set -e

CONTAINER_NAME="dekaf-local-kafka"

if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Stopping and removing Kafka container: $CONTAINER_NAME"
    docker rm -f "$CONTAINER_NAME"
    echo "Kafka container stopped and removed successfully!"
else
    echo "Kafka container '$CONTAINER_NAME' not found"
    exit 1
fi
