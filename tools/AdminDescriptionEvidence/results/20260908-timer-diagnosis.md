# Standalone timer startup diagnosis, 2026-09-08

**Performance acceptance remains INCONCLUSIVE.** This is a local, sequential method-attribution experiment, not a product comparison.

Product and build checkout: `146c86cc299b67d0491b36847e9ccdba1563b480`. Fresh main: `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. Base harness: `2b2f708457fe20c89c2a00e4238bdd2abcf1dbb6`.
The diagnostic patch only adds an optional synchronous timer primer before the existing complete measurement primer. Product files and workload fixtures are unchanged.

Windows 11 x64, Intel Core i7-12700K, .NET SDK 10.0.400/runtime 10.0.11, Release, workstation GC, tiered compilation and TieredPGO enabled. Both fresh processes were traced sequentially with runtime JIT/load events and explicit workload phase events. No simultaneous benchmark or smoke run was started by this worker.

Predeclared order: no timer primer, then a 20-second timer primer with a requested 1-ms callback period. Each process then runs 128 complete measurement segments of at least 0.05 seconds, a one-second complete primer, 120 seconds of actual classic:16 workload warmup, and 60 seconds of measurement. Timer callbacks must drain before workload priming; all phase events must retain the original OS thread.

The diagnostic criterion is removal of the identified timer/thread-pool compilation during measurement and the final five warmup seconds. Every sample, maximum, runtime interval, and trace event is retained. Timing changes cannot establish a performance win from these two traced samples.

| Configuration | Calls | Calls/s | CPU ns/call | B/call | p50 ns | p99 ns | max ns |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| unprimed | 17040263 | 284004.373 | 3448.634 | 11016.154 | 3100 | 5800 | 5244000 |
| primed | 17094716 | 284911.925 | 3470.553 | 11016.148 | 3100 | 5600 | 4047100 |

## unprimed

Timer primer: 0.0000021 seconds, 0 callbacks, drained=True, managed thread 2 before and 2 after.
Actual workload warmup: 120.0000008 seconds and 33023854 calls. Measurement: 60.0000021 seconds. Trace duration: 189.380s; events: 4386; lost: 0
Measured JIT count: 2124 to 2130; JIT compilation milliseconds: 716.0685 to 719.2845. Thread-pool workers: 2 to 2.
Measured heap bytes: 14232552 to 13468088; RSS bytes: 63295488 to 67510272. These endpoints do not establish long-run stability.

| Seconds relative to measured entry | Method | Tier |
| ---: | --- | --- |
| -0.968251 | `System.Runtime.CompilerServices.CastHelpers.ChkCastClass` | OptimizedTier1 |
| -0.596668 | `System.Array.Clear` | OptimizedTier1 |
| -0.000063 | ``System.Collections.Generic.ArraySortHelper`1[System.Int32].SwapIfGreater`` | OptimizedTier1 |
| 21.788786 | `System.SpanHelpers.ClearWithReferences` | OptimizedTier1 |
| 29.269102 | `System.Threading.PortableThreadPool.AdjustMaxWorkersActive` | OptimizedTier1 |
| 29.269840 | `System.Threading.ThreadInt64PersistentCounter.get_Count` | OptimizedTier1 |
| 29.270155 | `System.Threading.PortableThreadPool+HillClimbing.Update` | OptimizedTier1 |
| 51.364978 | `System.Runtime.CompilerServices.StaticsHelpers.GetGCThreadStaticsByIndexSlow` | OptimizedTier1 |
| 59.293849 | `System.Threading.PortableThreadPool+GateThread.EnsureRunningSlow` | OptimizedTier1Instrumented |

## primed

Timer primer: 20.0011614 seconds, 1267 callbacks, drained=True, managed thread 2 before and 2 after.
Actual workload warmup: 120.0000010 seconds and 34850657 calls. Measurement: 60.0000018 seconds. Trace duration: 209.027s; events: 4546; lost: 0
Measured JIT count: 2211 to 2212; JIT compilation milliseconds: 515.8855 to 516.2681. Thread-pool workers: 2 to 2.
Measured heap bytes: 16858224 to 14565480; RSS bytes: 63119360 to 67960832. These endpoints do not establish long-run stability.

| Seconds relative to measured entry | Method | Tier |
| ---: | --- | --- |
| 0.000090 | ``System.Collections.Generic.ArraySortHelper`1[System.Int32].SwapIfGreater`` | OptimizedTier1 |
| 2.614479 | `System.Threading.PortableThreadPool+GateThread.EnsureRunningSlow` | OptimizedTier1 |

## Decision and retention

The 20-second timer primer does not meet the predeclared startup criterion and is not promoted into the comparison harness. The unprimed process has six JIT events after measured entry; the primed process has two. The primed ArraySortHelper<int>.SwapIfGreater transition occurs only 90 microseconds after the phase marker; only one compilation is counted between the initial and final runtime snapshots. This is boundary-adjacent compilation, not proof of compilation inside a timed call. GateThread.EnsureRunningSlow compiles 2.614479 seconds after measured entry and increments the measured runtime JIT counter, so the remaining startup failure is unambiguous.

SpanHelpers.ClearWithReferences and the thread-pool adjustment/counter/hill-climbing helpers now optimize approximately 147.7 seconds before measured entry, during timer priming. This supports targeted timer preparation for those methods, but a single pair does not establish repeatability. The residual GateThread slow path needs a distinct experiment that exercises idle/restart behavior; another identical primer run is not justified. The [.NET 10.0.11 implementation](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.CoreLib/src/System/Threading/PortableThreadPool.GateThread.cs#L237) distinguishes gate-thread wakeup/creation from ordinary callbacks. No new idle/restart experiment is run here.

Maximum latency remains unresolved. The unprimed maximum is 5.244 ms during seconds 37-38; the primed maximum is 4.0471 ms during seconds 43-44. Both intervals have zero JIT-count growth. The primed interval containing GateThread compilation has a 0.3698-ms maximum. Removing compilation cannot by itself explain or accept these tails. CPU, throughput, latency and allocation figures above are retained observations, not a tradeoff approval or PASS.

Durable archive: `C:/git/Dekaf-evidence/pr-3128/timer-diagnosis-20260908/`. Verified 190 copied files against their originals and 14 loaded assembly identities against retained binaries. The archive includes exact product/main/harness source ZIPs, modified fixture and inspector sources, complete build outputs, raw probes/histograms/runtime series, both nettraces and decoded JIT/load events, predeclared plan, SDK report, scripts, and SHA-256 inventory.
No exact-head performance gate is changed to PASS. New parser-review work and any resulting product head require new evidence.
