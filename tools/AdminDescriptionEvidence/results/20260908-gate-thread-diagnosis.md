# Gate-thread startup diagnosis, 2026-09-08

**Product performance acceptance remains INCONCLUSIVE.** This experiment isolates runtime startup behavior; it does not compare product performance.

Exact product/build checkout: `ce38ae8b84f3e53cd1bb4aaa5f8cf82f60f34768`. Fresh main: `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. Base harness/report lineage: `eb78cf09f73ff9a3215629e152ccb367e15348a5`.
No product source, workload input, timeout or runtime setting changes. The fixture uses the earlier experimental timer primer plus a new cold gate primer; full modified sources and hashes are retained.

Windows 11 x64, Intel Core i7-12700K, SDK 10.0.400/runtime 10.0.11, Release, workstation GC, tiered compilation and TieredPGO. Two fresh processes run sequentially with identical runtime JIT/load tracing and explicit phase events. Trace decoding rejects lost events.

Both configurations run a twenty-second, requested 1-ms-period timer primer, then sixty elapsed seconds of supplementary setup. The idle control waits in 1.2-second intervals. The wake candidate additionally creates, fires and fully drains one one-shot timer after each interval. Equal elapsed-time setup distinguishes wakeup work from process age. The shared calling thread must remain unchanged; every owned timer must drain before workload priming.

Both then run 128 complete measurement entries of at least 0.05 seconds plus a one-second complete primer, 120 seconds of actual classic:16 workload warmup, and sixty measured seconds. Predeclared criterion: final optimized GateThread.EnsureRunningSlow and other identified runtime methods before the final five warmup seconds, with no unresolved measured startup transition. No retrospective trimming or performance equivalence is permitted.

| Mode | Gate setup seconds | Cycles | Callbacks | Warmup seconds / calls | Measured seconds / calls |
| --- | ---: | ---: | ---: | --- | --- |
| idle | 60.6008544 | 50 | 0 | 120.0000019 / 33686390 | 60.0000024 / 13402239 |
| wake | 60.4633395 | 50 | 50 | 120.0000005 / 35438763 | 60.0000029 / 17778098 |

| Mode | Calls/s | CPU ns/call | B/call | p50 ns | p99 ns | max ns |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| idle | 223370.641 | 4092.133 | 11016.207 | 3200.000 | 7600.000 | 100343300.000 |
| wake | 296301.619 | 3317.811 | 11016.143 | 3000.000 | 5300.000 | 1220900.000 |

## idle

Trace duration: 271.037s; events: 4857; lost: 0
Timer/gate callbacks fully drained; managed calling thread 2 before and 2 after gate setup. All workload phase events retain one OS thread.
Measured JIT count: 2364 to 2366; compilation milliseconds: 1183.9040 to 1184.3317; thread-pool workers: 3 to 3.
Measured heap bytes: 14736856 to 19225720; RSS bytes: 63221760 to 65806336. All GC and one-second runtime/histogram samples are retained; endpoints do not establish long-run stability.

| Gate-thread compilation relative to measured entry (seconds) | Tier |
| ---: | --- |
| -193.084284 | OptimizedTier1Instrumented |
| 20.685564 | OptimizedTier1 |

| JIT during measurement or final five warmup seconds | Method | Tier |
| ---: | --- | --- |
| 20.685564 | ``System.Threading.PortableThreadPool+GateThread.EnsureRunningSlow`` | OptimizedTier1 |
| 20.685878 | ``System.Runtime.CompilerServices.StaticsHelpers.GetGCThreadStaticsByIndexSlow`` | OptimizedTier1 |

## wake

Trace duration: 269.378s; events: 4777; lost: 0
Timer/gate callbacks fully drained; managed calling thread 2 before and 2 after gate setup. All workload phase events retain one OS thread.
Measured JIT count: 2327 to 2327; compilation milliseconds: 544.6946 to 544.6946; thread-pool workers: 2 to 3.
Measured heap bytes: 11208424 to 9536504; RSS bytes: 63016960 to 67104768. All GC and one-second runtime/histogram samples are retained; endpoints do not establish long-run stability.

| Gate-thread compilation relative to measured entry (seconds) | Tier |
| ---: | --- |
| -192.518981 | OptimizedTier1Instrumented |
| -160.177635 | OptimizedTier1 |

| JIT during measurement or final five warmup seconds | Method | Tier |
| ---: | --- | --- |
| 60.010363 | ``System.Collections.Generic.ArraySortHelper`1[System.Int32].SwapIfGreater`` | OptimizedTier1 |

## Decision

Repeated wakeups move the targeted final gate-thread optimization from 20.685564 seconds after measured entry in the idle control to 160.177635 seconds before measured entry in the wake candidate. The thread-static helper also no longer compiles during candidate measurement; its last observed compilation is 38.669348 seconds before entry. Both supplemental setup windows complete fifty intervals and differ by only 0.138 seconds. This supports a wakeup-specific effect rather than an elapsed-process-age explanation for the targeted methods in this pair.

Candidate measured JIT count and compilation time remain unchanged. The retained trace also contains ArraySortHelper<int>.SwapIfGreater compilation at +60.010363 seconds, near finalization; it is absent from measured JIT-counter growth. The phase marker and internal probe measurement clock are not directly calibrated, so this boundary-adjacent event is retained and is not categorically assigned inside or outside the timed loop.

Steady-state acceptance is still unproven. Candidate thread-pool workers grow from two to three during seconds 55-56, without JIT-count growth; the idle control also grows from two to three at that point. Idle additionally shrinks from three to zero before its late gate/thread-static compilation. Pending-work counts are zero at those sampled endpoints, but this does not prove the cause or long-run stability. The idle maximum is 100.3433 ms in seconds 28-29 and the candidate maximum is 1.2209 ms in seconds 15-16; neither interval has JIT-count growth. These maxima and the large throughput/CPU difference cannot be attributed to the primer from this pair.

Overall verdict: **INCONCLUSIVE**, with a useful targeted JIT effect. No new primer is promoted into the comparison workflow and no full or paid acceptance run is dispatched. Further measurement needs precise loop-boundary calibration and thread-pool stability assessment on Ubuntu. New correctness findings in review comment `5579525553` (in-memory cancellation race and existing consumer-fallback parser coverage) remain outstanding and are the next product repair.

Raw timing, CPU and latency values are observations from two differently primed traced processes, not performance acceptance or a tradeoff approval. This single diagnostic does not establish repeatability or portability. The formal exact-head performance gate stays failed.

Durable evidence: `C:/git/Dekaf-evidence/pr-3128/gate-diagnosis-20260908/`. Exact source ZIPs, modified fixture/inspector sources, full build outputs and loaded binaries, both raw nettraces, all decoded events and measured samples, predeclared plan, hardware/SDK reports and hash inventory must be verified before publication.
