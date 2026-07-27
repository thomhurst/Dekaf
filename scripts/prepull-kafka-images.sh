#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <kafka-image-tag> [<kafka-image-tag> ...]" >&2
  exit 2
fi

while IFS= read -r tag; do
  for attempt in 1 2 3 4 5; do
    if timeout 60s docker pull "apache/kafka:$tag"; then
      break
    fi

    if [ "$attempt" -eq 5 ]; then
      exit 1
    fi

    delay=$((attempt * 15))
    echo "Kafka image pull failed for $tag (attempt $attempt); retrying in ${delay}s"
    sleep "$delay"
  done
done < <(printf '%s\n' "$@" | sort -u)
