#!/usr/bin/env bash
# Runs Dekaf integration test categories against prebuilt test binaries.
#
# Usage: run-integration-categories.sh <net8.0|net10.0|aot> <comma-separated-categories>
#
# Expects binaries at tests/Dekaf.Tests.Integration/bin/Release/<tfm>/ (managed)
# or artifacts/aot/integration/ (NativeAOT), as produced by the CI build job.
# Fallback if the apphost lost its executable bit and chmod is unavailable:
#   dotnet exec tests/Dekaf.Tests.Integration/bin/Release/<tfm>/Dekaf.Tests.Integration.dll <args>
#
# Single source of truth for per-category runner arguments (parallelism caps,
# hang budgets). Every CI lane calls this script, so these settings never drift.
set -euo pipefail

if [ $# -ne 2 ]; then
  echo "Usage: $0 <net8.0|net10.0|aot> <comma-separated-categories>" >&2
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

  echo "::group::Category $category"
  args=(
    --hangdump
    --hangdump-timeout 5m
    --log-level Debug
    --treenode-filter "/**[Category=$category]"
    --results-directory "TestResults/$category"
  )
  case "$category" in
    Producer|Compression)
      args+=(--maximum-parallel-tests 4)
      ;;
    NetworkPartition|ShareConsumer|ShareConsumerAdmin|Serialization)
      args+=(--maximum-parallel-tests 1)
      ;;
  esac
  "$exe" "${args[@]}"
  echo "::endgroup::"
done
