#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
temp_root="$(mktemp -d)"
ryuk_image="testcontainers/ryuk:0.14.0@sha256:7c1a8a9a47c780ed0f983770a662f80deb115d95cce3e2daa3d12115b8cd28f0"
eventhubs_image="mcr.microsoft.com/azure-messaging/eventhubs-emulator:2.2.1@sha256:be413f0d59541621879e6d197d73f64f3b3ac5fa45861641fdc1430252b8b44b"
azurite_image="mcr.microsoft.com/azure-storage/azurite:3.36.0@sha256:76b8127d608fab8287a14a4bfeb9a5502cdcffb4bf1e86f09f324ebb0e70edba"
trap 'rm -rf "$temp_root"' EXIT

mkdir -p "$temp_root/scripts" \
  "$temp_root/fake-bin" \
  "$temp_root/artifacts/aot/integration" \
  "$temp_root/tests/Dekaf.Tests.Integration/bin/Release/net10.0"
cp "$repo_root/scripts/run-integration-categories.sh" "$temp_root/scripts/"
cp "$repo_root/scripts/prepull-kafka-images.sh" "$temp_root/scripts/"

fake_runner='#!/usr/bin/env bash
printf "%s\n" "$*" >> "$CALLS_FILE"'
printf '%s\n' "$fake_runner" > "$temp_root/artifacts/aot/integration/Dekaf.Tests.Integration"
printf '%s\n' "$fake_runner" > "$temp_root/tests/Dekaf.Tests.Integration/bin/Release/net10.0/Dekaf.Tests.Integration"

export CALLS_FILE="$temp_root/calls.log"
cd "$temp_root"

bash scripts/run-integration-categories.sh aot "Messaging,Interop,Serialization"
if grep -q 'Category=Interop' "$CALLS_FILE"; then
  echo "NativeAOT run included Interop category" >&2
  exit 1
fi
grep -q 'Category=Messaging' "$CALLS_FILE"
grep -q 'Category=Serialization' "$CALLS_FILE"

: > "$CALLS_FILE"
bash scripts/run-integration-categories.sh net10.0 "Messaging,Interop,Serialization,EventHubs"
grep -q 'Category=Interop' "$CALLS_FILE"
grep -q 'Category=Serialization.*--maximum-parallel-tests 1' "$CALLS_FILE"
grep -q 'Category=EventHubs.*--maximum-parallel-tests 1' "$CALLS_FILE"

grep -Eq 'MaximumParallelTests[[:space:]]*=>[[:space:]]*4' \
  "$repo_root/tools/Dekaf.Pipeline/Modules/RunProducerIntegrationTestsModule.cs"
grep -Fq "\"$ryuk_image\"" "$repo_root/.github/workflows/ci.yml"
grep -Fq "\"$ryuk_image\"" "$repo_root/.github/workflows/integration-groups.yml"
grep -Fq "\"$eventhubs_image\"" "$repo_root/.github/workflows/ci.yml"
grep -Fq "\"$azurite_image\"" "$repo_root/.github/workflows/ci.yml"

fake_docker='#!/usr/bin/env bash
printf "%s\n" "$*" >> "$DOCKER_CALLS_FILE"
if [ "$1" = "pull" ]; then
  attempts="$(cat "$DOCKER_ATTEMPTS_FILE" 2>/dev/null || printf "0")"
  attempts=$((attempts + 1))
  printf "%s" "$attempts" > "$DOCKER_ATTEMPTS_FILE"
  [ "$attempts" -gt "${DOCKER_FAIL_UNTIL:-0}" ]
fi'
fake_sleep='#!/usr/bin/env bash
printf "%s\n" "$*" >> "$SLEEP_CALLS_FILE"'
fake_timeout='#!/usr/bin/env bash
printf "%s\n" "$1" >> "$TIMEOUT_CALLS_FILE"
shift
"$@"'
printf '%s\n' "$fake_docker" > "$temp_root/fake-bin/docker"
printf '%s\n' "$fake_sleep" > "$temp_root/fake-bin/sleep"
printf '%s\n' "$fake_timeout" > "$temp_root/fake-bin/timeout"
chmod +x "$temp_root/fake-bin/docker" "$temp_root/fake-bin/sleep" "$temp_root/fake-bin/timeout"

export PATH="$temp_root/fake-bin:$PATH"
export DOCKER_ATTEMPTS_FILE="$temp_root/docker-attempts"
export DOCKER_CALLS_FILE="$temp_root/docker-calls"
export SLEEP_CALLS_FILE="$temp_root/sleep-calls"
export TIMEOUT_CALLS_FILE="$temp_root/timeout-calls"

export DOCKER_FAIL_UNTIL=1
bash scripts/prepull-kafka-images.sh 4.3.1
[ "$(cat "$DOCKER_ATTEMPTS_FILE")" -eq 2 ]
[ ! -s "$SLEEP_CALLS_FILE" ]
[ "$(cat "$TIMEOUT_CALLS_FILE")" = $'60s\n30s' ]
grep -qx 'pull apache/kafka:4.3.1' "$DOCKER_CALLS_FILE"
grep -qx 'pull mirror.gcr.io/apache/kafka:4.3.1' "$DOCKER_CALLS_FILE"
grep -qx 'tag mirror.gcr.io/apache/kafka:4.3.1 apache/kafka:4.3.1' "$DOCKER_CALLS_FILE"

rm -f "$DOCKER_ATTEMPTS_FILE" "$DOCKER_CALLS_FILE" "$SLEEP_CALLS_FILE" "$TIMEOUT_CALLS_FILE"
export DOCKER_FAIL_UNTIL=2
bash scripts/prepull-kafka-images.sh 4.3.1
[ "$(cat "$DOCKER_ATTEMPTS_FILE")" -eq 3 ]
[ "$(cat "$SLEEP_CALLS_FILE")" = '15' ]
[ "$(cat "$TIMEOUT_CALLS_FILE")" = $'60s\n30s\n60s' ]

rm -f "$DOCKER_ATTEMPTS_FILE" "$DOCKER_CALLS_FILE" "$SLEEP_CALLS_FILE" "$TIMEOUT_CALLS_FILE"
export GITHUB_ENV="$temp_root/github-env"
export DOCKER_FAIL_UNTIL=1
bash scripts/prepull-kafka-images.sh "$ryuk_image"
grep -Fqx "TESTCONTAINERS_RYUK_CONTAINER_IMAGE=mirror.gcr.io/$ryuk_image" "$GITHUB_ENV"
grep -Fqx "pull $ryuk_image" "$DOCKER_CALLS_FILE"
grep -Fqx "pull mirror.gcr.io/$ryuk_image" "$DOCKER_CALLS_FILE"
grep -Fqx "tag mirror.gcr.io/$ryuk_image ${ryuk_image%@*}" "$DOCKER_CALLS_FILE"

rm -f "$DOCKER_ATTEMPTS_FILE" "$DOCKER_CALLS_FILE" "$TIMEOUT_CALLS_FILE"
export DOCKER_FAIL_UNTIL=0
bash scripts/prepull-kafka-images.sh 4.3.1 4.2.1 4.3.1 "$ryuk_image"
[ "$(grep -c '^pull ' "$DOCKER_CALLS_FILE")" -eq 3 ]
grep -qx 'pull apache/kafka:4.2.1' "$DOCKER_CALLS_FILE"
grep -qx 'pull apache/kafka:4.3.1' "$DOCKER_CALLS_FILE"
grep -Fqx "pull $ryuk_image" "$DOCKER_CALLS_FILE"
[ "$(grep -cx '60s' "$TIMEOUT_CALLS_FILE")" -eq 3 ]

echo "run-integration-categories tests passed"
