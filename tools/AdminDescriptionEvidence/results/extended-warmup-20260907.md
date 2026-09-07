# Extended elapsed warmup and BDN phase evidence

**Verdict: INCONCLUSIVE.** Increasing local workload warmup from 60 to 120 seconds produces a clean inventory interval but does not eliminate runtime tier transitions in asynchronous recovery. The harness default is not changed to 120 seconds on the basis of this partial result. No new hosted acceptance campaign has been dispatched.

The [predecessor investigation](warmup-boundary-20260907.md) identifies complete measurement entry as one startup cause and retains all 60-second observations. This follow-up uses its unmodified archived candidate probe: product `e85fcb90bf1ba746eadb604f6f1347670eb04b98`, harness `b7aefef97cf7759ab5f7ddbd6546362863e4a3fe`, copied from the verified full-smoke archive. Source `811b569ca3cdfa3b8863068973841d30137b92fd` differs only in documentation. All seven loaded application assembly identities in each follow-up process match the retained copies with SHA-256.

Configuration remains Windows 11/i7-12700K, SDK 10.0.400/runtime 10.0.11, Release/net10.0, tiered compilation and dynamic PGO enabled, workstation GC. Every process executes 128 complete 50-ms primer measurements and one complete one-second primer, then 120 seconds of actual workload warmup and 20 measured seconds. The two untraced cases run sequentially; no other owned benchmark/build runs concurrently. These local cases lack A/B/A controls and are not performance comparisons.

| Case | Warmup seconds | Warmup completions | Measured seconds | Measured completions | Measured JIT methods | Compilation ms |
|---|---:|---:|---:|---:|---:|---:|
| Inventory, untraced | 120.0000013 | 130,320,263 | 20.0000017 | 21,981,734 | 0 | 0 |
| Retry, untraced | 120.008646 | 15,203 | 20.0139918 | 2,505 | 9 | 2.1287 |
| Retry, traced | 120.0072296 | 15,117 | 20.0120642 | 2,363 | 8 | 3.4524 |

The last five warmup seconds contain zero compilations in both untraced cases. Inventory's last warmup compilation occurs in its 76th second. The untraced recovery case still compiles during measurement, including late intervals; none is discarded.

The recovery trace lasts 149.399 seconds, retains 5,668 parsed events, and loses zero events. It places these Tier 1 transitions inside the measured phase:

- `System.Threading.TimerQueue.UnlinkTimer` and `LinkTimer`, about 130.147 seconds after process trace start.
- `System.RuntimeTypeHandle.GetElementType` and `ObjectEqualityComparer<T>.GetHashCode`, about 134.748 seconds.
- `PortableThreadPool.HillClimbing.Complex.op_Multiply`, `op_Subtraction` and `op_Division`, about 142.702 seconds.
- `HashSet<T>.FindItemIndex`, about 146.885 seconds.

Measurement begins at 129.361 seconds and finishes at 149.385 seconds. Timer and collection methods have earlier instrumented Tier 1 versions; the hill-climbing methods receive instrumented versions near 82.518 seconds before their later Tier 1 transition. These observations identify runtime activity that remains after the entry/sampler fix. They do not prove that the untraced process compiles exactly the same methods, identify call stacks, or establish that these transitions have no material effect on protected metrics. A targeted Ubuntu diagnosis is a more useful next check than assuming Windows results establish Linux startup behavior or blindly extending warmup again.

## BDN measurement boundaries

The follow-up harness records host actual-workload signals and the worker runtime sampler's monotonic clock origin. The [BDN 0.15.8 engine](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/Engine.cs) emits those signals around the actual workload stage. The driver rejects missing/repeated/reversed boundaries, mismatched worker/frequency identities, and runtime samples that do not bracket that interval. It retains all overlapping one-second observer intervals. Boundary intervals include adjacent-stage time and host signaling latency, so this provides conservative stage attribution rather than exact per-iteration tracing. No per-call or per-iteration hooks are added.

Two targeted `Job.Dry` cases validate both synchronous inventory and asynchronous recovery signal/clock recording, primer evidence and runtime coverage. They use 0.2-second workload warmup and do not establish performance or steady state. The harness build, eleven evaluator/integrity tests and actionlint pass. The earlier 14-case A1/B/A2 smoke remains scoped to b7aefef97; no new full campaign is claimed.

## Retention

Durable root: `C:/git/Dekaf-evidence/pr-3128/extended-warmup/`. It retains the predeclared diagnostic plan, every primer segment, all latency histograms and maxima, warmup/measured CPU/allocation/JIT/GC/heap/RSS/thread-pool data, raw trace and parsed events, and `summary.json` with exact metrics and completion counts. The measured product binaries remain in `warmup-boundary/full-smoke/archive/binaries/B/`, with their loaded identities verified again. `phase-smoke/` retains the new host signals, worker clock origins, BDN reports and runtime intervals; source/build archival accompanies this harness update.
