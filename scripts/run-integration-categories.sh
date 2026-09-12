#!/usr/bin/env bash
# Runs Dekaf integration test categories against prebuilt test binaries.
#
# Usage: run-integration-categories.sh <net8.0|net10.0|aot> <comma-separated-categories> [--list-tests]
#
# Expects binaries at tests/Dekaf.Tests.Integration/bin/Release/<tfm>/ (managed)
# or artifacts/aot/integration/ (NativeAOT), as produced by the CI build job.
# Fallback if the apphost lost its executable bit and chmod is unavailable:
#   dotnet exec tests/Dekaf.Tests.Integration/bin/Release/<tfm>/Dekaf.Tests.Integration.dll <args>
#
# Single source of truth for category filters and hang budgets. Every CI lane
# calls this script. Tests declare shared-resource constraints with NotInParallel.
set -euo pipefail

if { [ $# -ne 2 ] && [ $# -ne 3 ]; } || { [ $# -eq 3 ] && [ "$3" != "--list-tests" ]; }; then
  echo "Usage: $0 <net8.0|net10.0|aot> <comma-separated-categories> [--list-tests]" >&2
  exit 2
fi

tfm="$1"
IFS=',' read -ra categories <<< "$2"

if [ "$tfm" = "aot" ]; then
  exe="artifacts/aot/integration/Dekaf.Tests.Integration"
  export NET_VERSION="net10.0"
else
  exe="tests/Dekaf.Tests.Integration/bin/Release/$tfm/Dekaf.Tests.Integration"
  export NET_VERSION="$tfm"
fi

if [ ! -f "$exe" ]; then
  echo "Test executable not found: $exe" >&2
  exit 1
fi
chmod +x "$exe"

# Aggressive GC to reduce memory pressure on CI runners.
export DOTNET_GCConserveMemory=9

# Per-test OTel spans are the primary post-mortem evidence for timing failures
# (#2199 was solved from them). The default 100-span cap truncated them in run
# 29612262949, leaving the failing test with zero spans. Overridable from the
# environment.
export TUNIT_OTEL_MAX_EXTERNAL_SPANS="${TUNIT_OTEL_MAX_EXTERNAL_SPANS:-5000}"

for category in "${categories[@]}"; do
  if [ "$tfm" = "aot" ] && [ "$category" = "Interop" ]; then
    echo "Skipping Interop for NativeAOT because Confluent.Kafka requires runtime reflection"
    continue
  fi

  # The two ShareConsumer shards partition the original category. Untagged
  # future tests automatically join ShareConsumerOther, preserving coverage.
  filter="/**[Category=$category]"
  case "$category" in
    ShareConsumerCore)
      filter="/**[(Category=ShareConsumer)&(Category=ShareConsumerCore)]"
      ;;
    ShareConsumerOther)
      filter="/**[(Category=ShareConsumer)&(Category!=ShareConsumerCore)]"
      ;;
    CatchAll)
      # EventHubs has a dedicated CI lane outside this matrix. Expand virtual
      # ShareConsumer shards to their parent so uncategorized/new tests remain
      # selected without rerunning anything assigned to another job.
      covered_categories="$(python3 -c 'import json, sys
matrix = json.load(sys.stdin)
groups, categories = matrix["groups"], matrix["categories"]
if not groups or len(groups) != len(set(groups)) or set(groups) != set(categories):
    sys.exit("Scheduled matrix groups and category map must match exactly")
print(",".join(categories[group] for group in groups))' \
        <<< "${INTEGRATION_TEST_MATRIX_JSON:?CatchAll requires the matrix category map}")"
      IFS=',' read -ra covered <<< "$covered_categories"
      filter="/**[(Category!=EventHubs)"
      for excluded in "${covered[@]}"; do
        case "$excluded" in
          CatchAll) continue ;;
          ShareConsumerCore|ShareConsumerOther) excluded=ShareConsumer ;;
        esac
        if [[ ! "$excluded" =~ ^[A-Za-z][A-Za-z0-9]*$ ]]; then
          echo "Invalid matrix category: $excluded" >&2
          exit 2
        fi
        if [[ "$filter" != *"(Category!=$excluded)"* ]]; then
          filter+="&(Category!=$excluded)"
        fi
      done
      filter+="]"
      ;;
  esac

  echo "::group::Category $category"
  args=(
    --hangdump
    --hangdump-timeout 5m
    --log-level Debug
    --treenode-filter "$filter"
    --results-directory "TestResults/$category"
  )
  if [ "${3:-}" = "--list-tests" ]; then
    args+=(--list-tests --no-ansi)
  fi
  "$exe" "${args[@]}"
  echo "::endgroup::"
done
