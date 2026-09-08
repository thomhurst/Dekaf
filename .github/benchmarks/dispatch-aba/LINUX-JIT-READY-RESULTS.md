# PR #3117 prepared Linux JIT diagnosis — 2026-09-08

The revised broker preparation succeeds in all three sequential processes. The trace attributes four measured compilations to runtime Tier1 methods, but control drift and residual JIT activity leave performance acceptance **INCONCLUSIVE**. This is a local attribution experiment on the same candidate binary throughout, not a hosted baseline/candidate comparison or a performance gate update.

## Configuration and correctness

Producer A `5df2f0d03607389384b5c1466e17812a9084fac9`, consumer B `2a550007ccb091e8f2bf9275a7646277e984dff8`, and hosted fixture `7614406221b63c0b758f25e0722c8a3bf3ef9d0f` are the exact retained binaries, without rebuilding. Readiness driver/plan: `462f0e214df9525c1815c6db545e7331473ac2d4`. Main subsequently advanced to tooling-only `9eec358dad2a081dedbbfc75f02aee743e6bdad9`; old pinned binaries are retained for diagnosis only.

Each phase uses a fresh Kafka 4.3.1 broker. The Java CLI must confirm zero committed offsets for all four seed partitions, then a separate Dekaf seed must process and commit 4,000 records. The measured consumer starts fresh on a new topic/group. No failed Dekaf process is caught and retried. Broker request DEBUG logging is disabled for these measurements. See [request exchanges](COMMIT-EXCHANGE-RESULTS.md) for the evidence motivating this preparation.

U1 is untraced, T traced, U2 untraced. Each receives 121 seconds of offered warmup and 120 seconds measured at 50,000 records/s: four partitions, 256-byte payloads, 1,024 keys, capacity 128, concurrency 2, maximum batch size 16, synchronous batch handler. All phases acknowledge and handle 12,050,000 records: 6,050,000 warmup and 6,000,000 measured. Each verifies offsets `[3012500, 3012500, 3012500, 3012500]`, zero failures, zero final backlog and zero pending handlers after stop. All actual handler batches contain one record; this does not cover saturated batching.

Local Docker Ubuntu 24.04, SDK 10.0.400, runtime 10.0.11; workstation GC, `DOTNET_TieredCompilation=1`, `DOTNET_GCDynamicAdaptationMode=0`. Immutable SDK image `sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c`; Kafka image `sha256:77e3df9054047a88b520d0cc46e16696d3b22022e1d580aeccd2632df6532837`. Broker CPU 0, producer CPU 1, consumer CPUs 2/3, collector CPU 4 on a 20-logical-CPU local VM. This differs from hosted hardware and reserves an additional collector CPU.

Latency spans scheduled producer offer to handler processing completion, before automatic frontier bookkeeping. CPU and allocation cover the consumer process, including sampler and handler fixture, from first to last measured completion; broker and producer are separate. Throughput is completed rate at fixed offered load, not maximum capacity. Process-wide allocations include receive and batch costs; they do not replace the separately retained `[MemoryDiagnoser]` hot-path evidence.

## Full measurements

| Metric | U1 | T | U2 | T vs U1 | T vs U2 | U2 vs U1 drift |
|---|---:|---:|---:|---:|---:|---:|
| Completed records/s | 50000.357926 | 50000.170505 | 50000.390935 | -0.000375% | -0.000441% | +0.000066% |
| Consumer CPU ns/message | 2710.289333 | 2647.970833 | 2858.497500 | -2.299330% | -7.364941% | +5.468352% |
| Consumer B/message | 28.812384 | 28.787404 | 28.643984 | -0.086699% | +0.500699% | -0.584471% |
| Message p50 ms | 5.670281 | 5.536607 | 5.722338 | -2.357449% | -3.245719% | +0.918067% |
| Message p99 ms | 8.433777 | 8.295492 | 9.682337 | -1.639657% | -14.323453% | +14.804280% |
| Message maximum ms | 18.927575 | 15.572084 | 27.308066 | -17.728055% | -42.976247% | +44.276623% |

All 18,000,000 measured raw latency samples were independently re-read: p50, p99 and maximum match the summaries and every latency-series count is accounted for. Warmup samples are retained too. There is one process per phase; message count does not establish independent-run precision. No intervals or equivalence are inferred from this design. CPU drift exceeds the contextual 3% threshold, and p99/maximum drift exceeds 5%. Traced differences cannot be isolated as trace overhead or a product improvement amid this drift.

| Runtime diagnostic | U1 | T | U2 |
|---|---:|---:|---:|
| Actual warmup seconds | 120.779971647 | 120.859324136 | 120.867918995 |
| Measured JIT counter delta | 13 | 4 | 48 |
| Measured JIT counter time ms | 4.089900 | 1.534100 | 14.209100 |
| Sampled thread-pool thread range | [3, 4] | [3, 4] | [4, 4] |
| Sampled Gen0 / Gen1 / Gen2 increases | 13 / 1 / 0 | 14 / 2 / 1 | 14 / 2 / 1 |
| Maximum completion seconds into measurement | 1.056224144 | 117.192860681 | 6.346151135 |
| Sampled heap bytes | 100192120–111877776 | 99847464–111702120 | 98872496–111756104 |
| Sampled RSS bytes | 193138688–193187840 | 196345856–196612096 | 192507904–192966656 |
| Sampled backlog records | 128–512 | 0–512 | 0–640 |
| Sampled pending thread-pool work | 0–6 | 0–5 | 0–6 |

The raw one-second runtime and latency series, quarter-window views and maximum neighborhoods are retained in `assessment-ready.json`. GC deltas and ranges above cover sampled interior boundaries, not exact start/end GC counters. No long-run stability is inferred from two-minute measurement windows. Stop occurs after all processing completes, so its timing is not loaded shutdown evidence.

## Method attribution

The trace lasts 242.281 seconds, contains 17,667 decoded events and reports zero lost events. Consumer PID 25 matches the wrapper record. Raw QPC scale is 1,000,000 ticks/ms and event coverage spans both measurement boundaries. The four `JittingStarted` events agree with the measured runtime counter delta. Matched method-load events identify the following optimization tiers.

| Method | Start seconds into measurement | JIT-start to load ms | Loaded tier |
|---|---:|---:|---|
| `System.Threading.CancellationTokenSource+Registrations.<EnterLock>g__Contention\|13_0` | 58.158873975 | 0.518976 | `OptimizedTier1` |
| `System.Threading.LowLevelLock.TryAcquire_NoFastPath` | 79.067642603 | 0.229337 | `OptimizedTier1` |
| `System.Threading.Monitor.Enter_Slowpath` | 93.740352273 | 0.265550 | `OptimizedTier1Instrumented` |
| `System.GC.RunFinalizers` | 106.856074855 | 0.588117 | `OptimizedTier1` |

These are Tier1 compilations of runtime contention and finalizer helpers, not proof of first use. JIT-start-to-load elapsed time is not CPU cost or application pause duration. There are no Dekaf-named JIT starts during this traced measurement; that does not identify the callers of these runtime helpers or establish that product behavior is uninvolved. The JIT-only provider does not collect native/kernel waits or causal call stacks.

The traced maximum completes at 117.192860681 seconds, over ten seconds after the final recorded compilation starts. Thus this capture does not show the traced maximum occurring during a recorded JIT-start/load interval. The untraced controls have different JIT counts and no method attribution. This does not explain or dismiss the hosted candidate maximum of 396.452168 ms, which was 10.541%/10.381% above its within-run controls. Local hardware, broker preparation and tracing differ; absolute local/hosted metrics are not interchangeable.

## Decision and retention

No production change, hosted dispatch, acceptance waiver or new performance PASS follows. A further experiment needs a specific causal hypothesis and controlled design; extending warmup alone is not demonstrated to remove these runtime transitions. The hosted maximum-latency loss, residual JIT/control drift, and missing lifecycle/long-run evidence remain unresolved. All three declared phases are complete; no automatic additional identical run is launched.

Raw traces, logs, complete latency arrays, runtime series, exact wrapper inputs, parsers and source are retained outside removable worktrees at `C:/git/Dekaf-evidence/pr-3117/commit-exchange-20260908/`. Input binaries remain in the independently verified immutable `C:/git/Dekaf-evidence/pr-3117/linux-jit-20260908/raw/inputs/` archive; the new retention manifest records their hashes and paths. Earlier terminal commit failures remain preserved in that earlier archive. All task-owned containers and anonymous volumes were removed after capture. This diagnostic branch must not be merged into the product.
