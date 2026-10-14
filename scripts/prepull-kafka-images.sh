#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <kafka-image-tag-or-image> [<kafka-image-tag-or-image> ...]" >&2
  exit 2
fi

max_attempts=3
# Large images (cp-schema-registry:7.9.0 is ~2 GB) need longer per-attempt
# budgets than the Kafka images; callers raise these through the environment.
pull_timeout="${PREPULL_TIMEOUT:-60s}"
mirror_pull_timeout="${PREPULL_MIRROR_TIMEOUT:-30s}"

while IFS= read -r image_or_tag; do
  if [[ "$image_or_tag" == */* ]]; then
    image="$image_or_tag"
  else
    image="apache/kafka:$image_or_tag"
  fi

  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    if timeout "$pull_timeout" docker pull "$image"; then
      break
    fi

    mirror_image="mirror.gcr.io/$image"
    if timeout "$mirror_pull_timeout" docker pull "$mirror_image"; then
      docker tag "$mirror_image" "${image%@*}"

      if [[ "$image" == testcontainers/ryuk:* ]]; then
        if [ -z "${GITHUB_ENV:-}" ]; then
          echo "GITHUB_ENV is required to use the digest-pinned Ryuk mirror fallback" >&2
          exit 1
        fi

        printf 'TESTCONTAINERS_RYUK_CONTAINER_IMAGE=%s\n' "$mirror_image" >> "$GITHUB_ENV"
      fi

      echo "Pulled $image from the Docker Hub cache at mirror.gcr.io"
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
