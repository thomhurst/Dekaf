#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
temp_root="$(mktemp -d)"
ryuk_image="testcontainers/ryuk:0.14.0@sha256:7c1a8a9a47c780ed0f983770a662f80deb115d95cce3e2daa3d12115b8cd28f0"
eventhubs_image="mcr.microsoft.com/azure-messaging/eventhubs-emulator:2.2.1@sha256:be413f0d59541621879e6d197d73f64f3b3ac5fa45861641fdc1430252b8b44b"
azurite_image="mcr.microsoft.com/azure-storage/azurite:3.36.0@sha256:76b8127d608fab8287a14a4bfeb9a5502cdcffb4bf1e86f09f324ebb0e70edba"
trap 'rm -rf "$temp_root"' EXIT

# Run against the actual prebuilt executable in CI. Use the production runner
# so discovery and execution cannot drift to different filters.
if [ "${1:-}" = "--discovery" ] && [ $# -eq 2 ]; then
  cd "$repo_root"
  for category in ShareConsumer ShareConsumerCore ShareConsumerOther; do
    if ! bash scripts/run-integration-categories.sh "$2" "$category" --list-tests > "$temp_root/$category.txt"; then
      cat "$temp_root/$category.txt" >&2
      exit 1
    fi
  done
  python3 - "$temp_root" <<'PY'
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1])

def discover(category):
    output = (root / f"{category}.txt").read_text(encoding="utf-8-sig")
    summary = re.search(r"^Test discovery summary: found (\d+) test\(s\)", output, re.MULTILINE)
    if summary is None:
        raise SystemExit(f"{category}: missing discovery summary\n{output}")
    # MTP lists indented display names before the summary. Reject ambiguous
    # names and format changes rather than silently collapsing test identities.
    names = [line.strip() for line in output[:summary.start()].splitlines() if line.startswith("  ")]
    if not names or len(names) != int(summary[1]) or len(names) != len(set(names)):
        raise SystemExit(f"{category}: empty, ambiguous, or unrecognized discovery output\n{output}")
    return set(names)

original = discover("ShareConsumer")
core = discover("ShareConsumerCore")
other = discover("ShareConsumerOther")
overlap = core & other
missing = original - (core | other)
extra = (core | other) - original
if overlap or missing or extra:
    raise SystemExit(f"Invalid ShareConsumer partition: overlap={sorted(overlap)}, missing={sorted(missing)}, extra={sorted(extra)}")
print(f"ShareConsumer discovery verified: {len(original)} tests = {len(core)} core + {len(other)} other; no overlap or omissions.")
PY
  exit 0
elif [ $# -ne 0 ]; then
  echo "Usage: $0 [--discovery <net8.0|net10.0|aot>]" >&2
  exit 2
fi

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
grep -q 'Category=Serialization' "$CALLS_FILE"
grep -q 'Category=EventHubs' "$CALLS_FILE"

# Preserve complementary category filters in managed and NativeAOT lanes.
# The original unsplit category remains available too.
for framework in net10.0 aot; do
  : > "$CALLS_FILE"
  bash scripts/run-integration-categories.sh "$framework" "ShareConsumer,ShareConsumerCore,ShareConsumerOther"
  [ "$(wc -l < "$CALLS_FILE")" -eq 3 ]
  grep -Fq -- '--treenode-filter /**[Category=ShareConsumer] --results-directory TestResults/ShareConsumer' "$CALLS_FILE"
  grep -Fq -- '--treenode-filter /**[(Category=ShareConsumer)&(Category=ShareConsumerCore)] --results-directory TestResults/ShareConsumerCore' "$CALLS_FILE"
  grep -Fq -- '--treenode-filter /**[(Category=ShareConsumer)&(Category!=ShareConsumerCore)] --results-directory TestResults/ShareConsumerOther' "$CALLS_FILE"
done

# Discovery uses the same filters and reaches the runner without executing tests.
: > "$CALLS_FILE"
bash scripts/run-integration-categories.sh net10.0 "ShareConsumerCore" --list-tests
grep -Fq -- '--treenode-filter /**[(Category=ShareConsumer)&(Category=ShareConsumerCore)]' "$CALLS_FILE"
grep -Fq -- '--list-tests --no-ansi' "$CALLS_FILE"
if bash scripts/run-integration-categories.sh net10.0 "ShareConsumerCore" --invalid; then
  echo "Integration runner accepted an unknown discovery option" >&2
  exit 1
fi

# The catch-all follows matrix changes, excludes the parent of both share
# shards, and fails closed if the matrix category map is missing.
: > "$CALLS_FILE"
INTEGRATION_TEST_MATRIX_JSON='{"groups":["produce","core","other","future","catch-all"],"categories":{"produce":"Producer","core":"ShareConsumerCore","other":"ShareConsumerOther","future":"FutureCategory","catch-all":"CatchAll"}}' \
  bash scripts/run-integration-categories.sh net10.0 "CatchAll" --list-tests
grep -Fq -- '--treenode-filter /**[(Category!=EventHubs)&(Category!=Producer)&(Category!=ShareConsumer)&(Category!=FutureCategory)]' "$CALLS_FILE"
if INTEGRATION_TEST_MATRIX_JSON= bash scripts/run-integration-categories.sh net10.0 "CatchAll"; then
  echo "Catch-all accepted a missing matrix category map" >&2
  exit 1
fi
if INTEGRATION_TEST_MATRIX_JSON='{"groups":["catch-all"],"categories":{"produce":"Producer","catch-all":"CatchAll"}}' \
  bash scripts/run-integration-categories.sh net10.0 "CatchAll"; then
  echo "Catch-all excluded an unscheduled category" >&2
  exit 1
fi

# Test-owned constraints must apply equally to direct runs, managed CI, and AOT.
for framework in net10.0 aot; do
  : > "$CALLS_FILE"
  bash scripts/run-integration-categories.sh "$framework" "Producer,Compression,EventHubs,NetworkPartition,ShareConsumer,ShareConsumerCore,ShareConsumerOther,ShareConsumerAdmin,Serialization"
  [ "$(wc -l < "$CALLS_FILE")" -eq 9 ]
  if grep -Fq -- '--maximum-parallel-tests' "$CALLS_FILE"; then
    echo "Integration runner overrides test-owned parallelism" >&2
    exit 1
  fi
done

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
