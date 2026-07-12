#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
temp_root="$(mktemp -d)"
trap 'rm -rf "$temp_root"' EXIT

mkdir -p "$temp_root/scripts" \
  "$temp_root/artifacts/aot/integration" \
  "$temp_root/tests/Dekaf.Tests.Integration/bin/Release/net10.0"
cp "$repo_root/scripts/run-integration-categories.sh" "$temp_root/scripts/"

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

echo "run-integration-categories tests passed"
