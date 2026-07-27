#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <kafka-image-tag-or-image> [<kafka-image-tag-or-image> ...]" >&2
  exit 2
fi

max_attempts=8

while IFS= read -r image_or_tag; do
  if [[ "$image_or_tag" == */* ]]; then
    image="$image_or_tag"
  else
    image="apache/kafka:$image_or_tag"
  fi

  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    if timeout 60s docker pull "$image"; then
      break
    fi

    if [ "$attempt" -eq "$max_attempts" ]; then
      exit 1
    fi

    delay=$((attempt * 15))
    if [ "$delay" -gt 60 ]; then
      delay=60
    fi

    echo "Image pull failed for $image (attempt $attempt); retrying in ${delay}s"
    sleep "$delay"
  done
done < <(printf '%s\n' "$@" | sort -u)
