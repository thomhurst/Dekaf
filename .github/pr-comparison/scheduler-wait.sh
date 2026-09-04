#!/usr/bin/env bash
set -euo pipefail
root="$PWD"
mkdir -p evidence
for version in main candidate; do
  cp .github/pr-comparison/SchedulerWaitProbe.cs "versions/$version/tools/Dekaf.Benchmarks/Benchmarks/Unit/"
  git -C "versions/$version" rev-parse HEAD > "evidence/$version.sha"
  dotnet build "versions/$version/tools/Dekaf.Benchmarks" -c Release > "evidence/build-$version.log" 2>&1 || { tail -60 "evidence/build-$version.log"; exit 1; }
done
lscpu > evidence/cpu.txt
lscpu -e > evidence/cpu-topology.txt
dotnet --info > evidence/dotnet.txt
for segment in main-a candidate main-b; do
  version=main
  if [[ "$segment" == candidate ]]; then version=candidate; fi
  mkdir -p "$root/evidence/$segment"
  (
    cd "versions/$version"
    timeout --signal=TERM --kill-after=30s 6m taskset -c 6,7 dotnet tools/Dekaf.Benchmarks/bin/Release/net10.0/Dekaf.Benchmarks.dll \
      --filter '*SchedulerWaitProbe*' '*BrokerPrefetchSchedulerBenchmarks.Scheduler_*' \
      --job Short --warmupCount 5 --iterationCount 10 --iterationTime 250 --memory --exporters json \
      --artifacts "$root/evidence/$segment"
  ) > "$root/evidence/$segment/run.log" 2>&1
  python3 .github/pr-comparison/validate-bdn.py "$root/evidence/$segment"
done
