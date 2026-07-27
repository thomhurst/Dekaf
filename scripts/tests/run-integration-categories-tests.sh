#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
temp_root="$(mktemp -d)"
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
bash scripts/run-integration-categories.sh net10.0 "Messaging,Interop,Serialization"
grep -q 'Category=Interop' "$CALLS_FILE"
grep -q 'Category=Serialization.*--maximum-parallel-tests 1' "$CALLS_FILE"

grep -Eq 'MaximumParallelTests[[:space:]]*=>[[:space:]]*4' \
  "$repo_root/tools/Dekaf.Pipeline/Modules/RunProducerIntegrationTestsModule.cs"

fake_docker='#!/usr/bin/env bash
attempts="$(cat "$DOCKER_ATTEMPTS_FILE" 2>/dev/null || printf "0")"
attempts=$((attempts + 1))
printf "%s" "$attempts" > "$DOCKER_ATTEMPTS_FILE"
printf "%s\n" "$*" >> "$DOCKER_CALLS_FILE"
[ "$attempts" -gt "${DOCKER_FAIL_UNTIL:-0}" ]'
fake_sleep='#!/usr/bin/env bash
printf "%s\n" "$*" >> "$SLEEP_CALLS_FILE"'
printf '%s\n' "$fake_docker" > "$temp_root/fake-bin/docker"
printf '%s\n' "$fake_sleep" > "$temp_root/fake-bin/sleep"
chmod +x "$temp_root/fake-bin/docker" "$temp_root/fake-bin/sleep"

export PATH="$temp_root/fake-bin:$PATH"
export DOCKER_ATTEMPTS_FILE="$temp_root/docker-attempts"
export DOCKER_CALLS_FILE="$temp_root/docker-calls"
export SLEEP_CALLS_FILE="$temp_root/sleep-calls"

export DOCKER_FAIL_UNTIL=2
bash scripts/prepull-kafka-images.sh 4.3.1
[ "$(cat "$DOCKER_ATTEMPTS_FILE")" -eq 3 ]
grep -qx '15' "$SLEEP_CALLS_FILE"
grep -qx '30' "$SLEEP_CALLS_FILE"

rm "$DOCKER_ATTEMPTS_FILE" "$DOCKER_CALLS_FILE" "$SLEEP_CALLS_FILE"
export DOCKER_FAIL_UNTIL=0
bash scripts/prepull-kafka-images.sh 4.3.1 4.2.1 4.3.1
[ "$(wc -l < "$DOCKER_CALLS_FILE")" -eq 2 ]
grep -qx 'pull apache/kafka:4.2.1' "$DOCKER_CALLS_FILE"
grep -qx 'pull apache/kafka:4.3.1' "$DOCKER_CALLS_FILE"

echo "run-integration-categories tests passed"
