# PR #3085: current-head outbox cycle diagnostic

This task-scoped harness branch is not a product PR and must not be merged. Its replacement benchmarks.yml is dispatched only on this branch; scheduled benchmark/stress coverage on main is unchanged. The product PR remains pinned to ac84625d630914b2ede4911478e7795364c8e664.

## Decision before dispatch

The last matching hosted campaign is run 34146134261 (https://github.com/thomhurst/Dekaf/actions/runs/34146134261), harness 83d68296b78cfb88ef9b8a68d6688a3346469f28, baseline a48fafe4121350da7ad83fcdd238f0f8039d6d59 and candidate 3feaeaa1f544e7f2d308f9083d4e0134aeafcd0b. It used one ubuntu-latest VM, tiered compilation disabled, affinity CPU 0, eight BDN warmups and 25 measured iterations at a 250 ms target. No profiler or stress lane/dispatch shape applies. Its four relay cases were:

| Case | A1 ns/cycle | B ns/cycle | A2 ns/cycle | B/A1 | B/A2 | A drift | Allocated B/cycle A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Synchronous, disabled | 519.069 | 538.134 | 521.076 | +3.67% | +3.27% | +0.39% | 144 / 0 / 144 |
| Pending, disabled | 974.766 | 850.881 | 948.786 | -12.71% | -10.32% | -2.67% | 480 / 0 / 480 |
| Synchronous, enabled listener | 513.593 | 747.671 | 517.182 | +45.58% | +44.57% | +0.70% | 144 / 0 / 144 |
| Pending, enabled listener | 955.575 | 1398.296 | 959.202 | +46.33% | +45.78% | +0.38% | 480 / 176 / 480 |

The disabled-synchronous loss was reported as REGRESSION and remains unresolved by local evidence. This new campaign tests a causal product correction: f93a46d0e11bb8b5bf95390c46e015f16c4745bb folds cycle timing into the existing pooled state machine and pools measured-publish completion. The immediately preceding product comparison is retained in the PR at docs/performance-evidence/2026-09-07/pr-3085/metrics-state-machine/: local A1/B/A2 sync-off 256.0/244.1/242.9 ns, pending-off 424.5/434.5/436.4 ns, sync-on 365.8/371.9/367.0 ns, pending-on 714.6/586.9/699.2 ns. Candidate allocations are 0 B in all four cases; preceding pending-on allocates 176 B. The 5.12% local sync-off control drift leaves that comparison INCONCLUSIVE.

This is a new corrected product and improved experiment, not an identical paid repeat. No tradeoff is approved. The next action is this limited hosted diagnostic before considering expensive loaded acceptance. Its new warmup and wrapper differ from the earlier hosted configuration, so absolute cross-run numbers cannot be pooled or treated as like-for-like deltas. Both within-run controls remain authoritative. Last accepted baseline is fresh main, not an earlier unaccepted PR candidate.

## Pinned design

- A1/A2: freshly fetched main 5df2f0d03607389384b5c1466e17812a9084fac9.
- B: exact current PR head ac84625d630914b2ede4911478e7795364c8e664, which contains A. No product changes or rebase are planned during measurement.
- One ubuntu-latest job builds both revisions and validates all four fixtures before timing; A1, B and A2 run sequentially. Each case starts a fresh process on the same CPU. Binaries are copied and SHA-256 verified before timing and checked again before each phase. InProcessEmit launches no builds between phases. Build servers are stopped on the dedicated hosted VM only.
- Each case performs a fixed minimum 20-second direct workload warmup with one-second JIT/thread-pool/CPU/GC/heap/RSS observations, then 30 BDN workload warmup iterations at a 1,000 ms target. The verifier requires their actual accumulated elapsed time to exceed 20 seconds. Twenty-five measured iterations follow; all outliers and maxima remain. DOTNET_TieredCompilation=0 matches the prior diagnostic. Source, environment, runtime, runner image, hardware and exact run/harness SHAs are retained.
- RuntimeLogger records JIT method/time totals, thread-pool size/backlog, process CPU, GC counts, total allocation, heap and RSS at every BDN workload boundary, outside the timed operation. Its preallocated sample array avoids a background sampler. These counters include framework and logger overhead and BDN-forced GCs; they are diagnostic time series, not isolated client CPU/message or application GC counts. All samples are retained.

## Work and correctness

One operation is a complete 500-row relay cycle with deterministic in-memory store/publisher stubs: acquire/refresh ownership, query pending buckets, fetch one reusable row array, complete publication and mark all rows published. BatchSize is 501 so that the 500-row response ends the cycle. One bucket, 1-byte payloads and one-day leases isolate cycle/drain/measurement overhead. Pending publication uses a reusable IValueTaskSource completed inline after RunCycleAsync suspends; no broker, thread-pool completion, database, recovery, shutdown or backlog sampler is exercised.

The fixture source matches the earlier investigation. A cached delegate adapts Task versus ValueTask return types once during setup. Both baseline and candidate bind through the same pooled wrapper. Cleanup validates total acknowledged/deleted counts after the measured run. Main has no Outbox instruments: enabled baseline means a running MeterListener with no corresponding published instruments, while the candidate emits real measurements. That behavior difference is explicit instrumentation cost, not identical instrumentation work. No dummy instruments are injected into the baseline.

MemoryDiagnoser reports bytes per entire 500-row cycle, without OperationsPerInvoke normalization. The exact same-thread 1,000-cycle probe is separate. Candidate should retain zero bytes in its warmed cycle paths; any nonzero result needs attribution. Baseline allocations are preserved, not asserted away. One operation's wall time is not CPU time or actual per-message latency.

## Predeclared interpretation and limits

For this diagnostic, inspect each candidate against both controls separately. Material control drift is more than 2%. The timing equivalence tolerance is 1%; a confirmed loss requires the candidate's 99.9% confidence lower bound to exceed both controls' upper bounds by more than 1%, with stable controls and no material startup transition in measured samples. A no-loss result needs the candidate upper bound within 1% of both control lower bounds; overlapping intervals alone do not establish equivalence. Other outcomes are INCONCLUSIVE. Allocation gains never offset a confirmed timing loss. Enabled-instrumentation cost is reported separately and is not waived. Inspect runtime series for startup transitions; do not trim samples or widen thresholds after seeing results.

Whole-PR performance acceptance cannot PASS from this run. Applicable actual per-message p50/p99/max completion latency, loaded CPU/message, error/recovery/shutdown behavior, backlog, and sustained stability remain missing. A confirmed protected-metric loss stays REGRESSION; otherwise overall acceptance remains INCONCLUSIVE until the full gate is met. The workflow never sets a successful performance status. The first inconclusive result permits only one exact repeat under repository rules; after a second, synthesize evidence and improve the experiment or pursue a new causal candidate. No further identical paid run without explicit maintainer approval.

Local validation compiles the current candidate harness, validates all four modes, and runs four short BDN smoke cases to confirm runtime logger and exporters. Seven Python tests reject incomplete samples, missing runtime intervals, short actual warmup and unmatched phase matrices. Local smoke numbers are not performance evidence. The hosted workflow separately builds and validates both exact product revisions before any measurements.
