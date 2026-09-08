# PR #3138 startup diagnosis — INCONCLUSIVE

Product: `0583a5013c6cd777cb02bbf6596d73b5087dd247`. Harness: `529112144af19b2d00ad8a38a05c9f7353df3542`.

This is a Windows same-product untraced/traced/untraced diagnostic of `unregistered:16`, not product A1/B/A2 acceptance. No product source changed and no hosted run was dispatched. All phases use one common runner/product binary, workstation GC, tiered compilation and PGO enabled. SDK 10.0.400/runtime 10.0.11 on Windows 11/i7-12700K. The pinned local dotnet-trace tool is 9.0.652701. The trace inspector reports 3,783 retained JIT/load/phase events and zero lost events over 149.042 seconds.

Every fresh process executes 128 complete primer segments of at least 50 ms and a full one-second segment, then 120 seconds of actual workload warmup and 20 seconds of measured calls. The unit is a completed administrative call returning 16 NotAttempted outcomes. It includes constructing/throwing the controller lease exception and observing the complete result count. The synthetic connection performs zero mutation RPCs. Process CPU/allocation includes the probe histogram/counter bookkeeping; no broker or message-delivery metrics are inferred.

| Phase | Warmup s | Warmup calls | Measured s | Completed calls | Calls/s | CPU ns/call | B/call | p50 ns | p99 ns | max ns |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| untraced1 | 120.000001 | 29,292,791 | 20.000004 | 4,880,461 | 244023.002 | 4049.950 | 4368.189864 | 3800 | 6500 | 4317100 |
| traced | 120.000004 | 28,423,831 | 20.000004 | 4,647,222 | 232361.052 | 4229.677 | 4368.203363 | 3900 | 8400 | 1880400 |
| untraced2 | 120.000003 | 29,127,840 | 20.000003 | 4,825,725 | 241286.209 | 4044.081 | 4368.195089 | 3800 | 7200 | 3800800 |

| Metric | Traced vs first untraced | Traced vs second untraced | Second vs first untraced |
|---|---:|---:|---:|
| CallsPerSecond | -4.779% | -3.699% | -1.122% |
| CpuNsPerCall | +4.438% | +4.589% | -0.145% |
| AllocatedBytesPerCall | +0.000% | +0.000% | +0.000% |
| P50Ns | +2.632% | +2.632% | +0.000% |
| P99Ns | +29.231% | +16.667% | +10.769% |
| MaxNs | -56.443% | -50.526% | -11.959% |

| Phase | Measured JIT count | JIT time delta ms | Thread-pool min–max | Max pending | GC 0/1/2 deltas | Heap min–max B | RSS min–max B |
|---|---:|---:|---:|---:|---|---:|---:|
| untraced1 | 2 | 1.273100 | 0–2 | 0 | 1631/2/1 | 6981848–18448672 | 60366848–62042112 |
| traced | 1 | 0.416600 | 0–2 | 0 | 1553/2/0 | 6679160–19169984 | 63070208–63844352 |
| untraced2 | 4 | 1.683700 | 0–2 | 0 | 1613/2/1 | 7091768–18447816 | 60588032–62189568 |

Each phase retains 20 one-second rows and the exact-tick distribution of every completed call, including all maxima. These rows and calls are correlated observations; the single pair of untraced processes does not establish statistical equivalence. The 10.769% untraced p99 drift and -11.959% maximum drift exceed the planned 5% latency allowance. Tracing also changes observed throughput/CPU/tails. Favorable maxima do not offset other losses, and these are observer/configuration comparisons, not a product REGRESSION or PASS.

The traced process promotes Thread.GetNativeHandle, ThreadPool.RequestWorkerThread, List.RemoveAt, ThreadPoolWorkQueue.EnqueueAtHighPriority, TimerQueue.IThreadPoolWorkItem.Execute, TimerQueue.FireNextTimers and WorkerThread.ShouldStopProcessingWorkNow around 111–113 seconds after elapsed warmup starts. The only JittingStarted event between measured phase markers is System.SpanHelpers.ClearWithoutReferences at 148022.9944 ms of trace time, about 19.012 seconds after the measured marker. Its load event identifies OptimizedTier1Instrumented code. This is inside the measured phase rather than its bookkeeping edge, and the runtime counter records one compilation. The caller that drove that helper promotion was not attributed. No Dekaf method JIT-start event occurs between those markers in this trace; that observation does not attribute the two/four untraced compilations or prove the timer/runtime activity is harmless.

The startup design therefore remains unaccepted. A longer uniform warmup is a testable follow-up because the latest attributed transition occurs around 139 seconds after warmup begins, but 180 seconds must not be presumed sufficient without repeating runtime attribution on each relevant configuration. No identical repeat, further local timing campaign, full hosted campaign or tolerance change is part of this result. Fresh-main acceptance and the immediately preceding product control for the earlier unregistered-controller timing signal remain missing.

Validation: 14 cached candidate fixture cases, two cached main controls, and two real Kafka 4.3.1 fixtures pass. Five accounting tests reject lost maxima, incorrect CPU accounting, broken intervals and false percentiles. Traced smoke validation passes with zero lost events. Full diagnostic copies all application binaries before execution and verifies every loaded application identity against its retained copy. See local/retention-notes.md in the archive for the explicitly missing original preliminary runner binary and the distinction between phase markers and snapshot boundaries.

The source and failure/passing outputs are archived alongside raw traces, all primer/warmup/measurement distributions, tracer/inspector/probe binaries, both product build inputs, environment metadata and SHA-256 inventories. Client performance acceptance remains INCONCLUSIVE on the unchanged product head.

Durable archive: C:/git/Dekaf-evidence/pr-3138/startup-20260908/529112144af19b2d00ad8a38a05c9f7353df3542/. All 3,270 copied original files were SHA-256 verified before publication; two exact committed source ZIPs and their hashes are retained alongside the inventory.

