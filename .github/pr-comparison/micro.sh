#!/usr/bin/env bash
set -euo pipefail
root="$PWD"
mkdir -p evidence
for version in main pool-before pool-candidate fetch-before fetch-candidate; do
  if [[ "$version" == main ]]; then
    cp versions/pool-before/tools/Dekaf.Benchmarks/Benchmarks/Unit/ResponseBufferPoolBenchmarks.cs \
      versions/main/tools/Dekaf.Benchmarks/Benchmarks/Unit/ResponseBufferPoolBenchmarks.cs
  fi
  dotnet build "versions/$version/tools/Dekaf.Benchmarks" -c Release > "evidence/build-$version.log" 2>&1 || { tail -60 "evidence/build-$version.log"; exit 1; }
  git -C "versions/$version" rev-parse HEAD > "evidence/$version.sha"
done
lscpu > evidence/cpu.txt
lscpu -e > evidence/cpu-topology.txt
dotnet --info > evidence/dotnet.txt

run_bench() {
  local version="$1" label="$2"
  shift 2
  mkdir -p "$root/evidence/$label"
  (
    cd "versions/$version"
    timeout --signal=TERM --kill-after=30s 12m taskset -c 6,7 dotnet \
      tools/Dekaf.Benchmarks/bin/Release/net10.0/Dekaf.Benchmarks.dll \
      --job Short --warmupCount 5 --iterationCount 10 --iterationTime 250 \
      --memory --exporters json --artifacts "$root/evidence/$label" --filter "$@"
  ) > "$root/evidence/$label/run.log" 2>&1
  # BDN can exit zero after a failed case; reject missing/failed measurement cases.
  python3 .github/pr-comparison/validate-bdn.py "$root/evidence/$label"
}

pool_filters=('*ResponseBufferPoolBenchmarks.NativeCeilingWave_128KB*'
  '*ResponseBufferPoolBenchmarks.NativeFetchFrames_1MB_AfterFrameSizeGrowth*'
  '*ResponseBufferOverflowBenchmarks*')
run_bench main pool-main-a "${pool_filters[@]}"
run_bench pool-before pool-before-a "${pool_filters[@]}"
run_bench pool-candidate pool-candidate "${pool_filters[@]}"
run_bench pool-before pool-before-b "${pool_filters[@]}"
run_bench main pool-main-b "${pool_filters[@]}"

fetch_filters=('*BrokerPrefetchSchedulerBenchmarks.Scheduler_*')
run_bench fetch-before fetch-before-a "${fetch_filters[@]}"
run_bench fetch-candidate fetch-candidate "${fetch_filters[@]}"
run_bench fetch-before fetch-before-b "${fetch_filters[@]}"
run_bench fetch-candidate fetch-dispatch '*BrokerPrefetchDispatchBenchmarks*'
