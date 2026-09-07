# PR #3109 shutdown investigation

Published head 597108c3907b77ab1ddc4887be187be640f62db7. Published comparison product is the preserved revised library from the isolated audit (same source product as 01185af9ca7ac31f3391abe8ecacf1d1e9a2fd0b/current head). Candidate freshly built from the exact head plus candidate.patch.

StopAllBoundedAsync now directly returns the existing StopAllAsync ValueTask for Drain and infinite-timeout paths. The bounded Cancel path retains an async helper owning and disposing its CancellationTokenSource. The shared shutdown deadline, cancellation and drain ordering remain intact. This removes an unnecessary suspended wrapper; it changes shutdown work, not per-record processing.

The MemoryDiagnoser fixture fills a 1,024-record partition queue, blocks the handler, starts shutdown, releases the handler and checks complete ordered processing, committed checkpoint and rejected blocked writer. IterationSetup is excluded from measurement. Existing per-record offset tracking allocations inside the full drain remain. The older accepted-main product lacks the fixture's shutdown methods; it is not passed to an incompatible fixture or treated as an equivalent baseline here. The preceding accepted-main command-drain evidence remains in the original audit.

One shared performance reservation covers preparation, both experiments and tests. No competing .NET jobs or Kafka containers during timing; lock ownership verified before and after. Windows 11/i7-12700K, SDK 10.0.400/runtime 10.0.11, BDN 0.15.8; affinity mask 4, DOTNET_TieredCompilation=0. Published → candidate → published control, separate fresh processes. All outliers retained.

| Experiment and metric | Published | Candidate | Published control |
|---|---:|---:|---:|
| Initial 30 observations, mean | 98.673 us | 117.383 us | 108.173 us |
| Initial max | 146.9 us | 464.9 us | 116.4 us |
| Expanded 600 observations, mean | 103.114 us | 106.767 us | 107.129 us |
| Expanded median | 99.800 us | 99.300 us | 99.975 us |
| Expanded max | 203.4 us | 382.2 us | 474.2 us |
| Allocation per complete shutdown, both runs | 51,720 B | 51,600 B | 51,720 B |

Initial run: eight warmups, 30 iterations, one invocation per iteration. Its candidate spike and control movement prevented a timing conclusion. The expanded experiment uses 30 warmups and 300 iterations across each of two fresh process launches, 600 measured observations per product job. The mean and maximum fall between controls; timing remains inconclusive. The 120 B allocation saving is consistent in both configurations. This is approximately 0.23% of the fixture's total shutdown allocation, not a claim of 0 B/message or reduced whole-system delivery latency.

Validation: 87 partitioned unit cases pass on each net10.0/net8.0, including shutdown timeout/cancellation paths. Three Kafka integration cases pass on net10.0, including handler commits while full and while draining. Product/fixture builds have zero warnings, Dry validation passes, git diff --check and scoped simplification review pass. Six measured BDN cases retained across two configurations.

Decision: retain a provisional local allocation reduction. No shutdown speedup or whole-PR Pareto acceptance claimed; no protected metric traded for allocation savings. No push, merge or gate override. Raw reports, all iterations, summary.json, hashes and candidate.patch accompany this report.
