# Outbox metric state-machine diagnostic

**Acceptance: INCONCLUSIVE; do not merge.** The local experiment confirms removal of the enabled pending-publish allocation. It does not establish that the earlier hosted disabled-synchronous regression is resolved: the new local controls drift 5.12% on that path. No protected-metric tradeoff or hosted acceptance is claimed.

## Change and validation

Product `f93a46d0e11bb8b5bf95390c46e015f16c4745bb` moves cycle timing into the relay's existing pooled state machine, removing the extra cycle wrapper and completion wrapper. Measured publication also uses `PoolingAsyncValueTaskMethodBuilder<>`, so a suspended publisher no longer allocates a separate measurement task per batch. Enablement is still checked when each cycle/publish starts; counters, duration boundaries, listener isolation, cancellation and acknowledgment/deletion ordering remain covered by existing tests.

All 96 focused outbox/EF Core/publisher unit tests pass on .NET 10 and .NET 8. Both Kafka 4.3.1 outbox relay integration cases pass, including publication held for lease renewal. All four diagnostic fixtures validate exact acknowledged/deleted row counts before timing. Source changes were reviewed for reuse, clarity and efficiency; no public API changes or unrelated source refactors are included.

## Products and method

- A1/A2: `cfe18050a787998b659bc540df54aef09ad610bb`, the immediately preceding PR product rebased onto main `5df2f0d03607389384b5c1466e17812a9084fac9`.
- B: `f93a46d0e11bb8b5bf95390c46e015f16c4745bb`. Subsequent evidence-only changes do not alter product source.
- Fixture: [RelayMetricsBenchmarks.cs at 06aa795796080ce6879139ac2a253d4f0b4266ea](https://github.com/thomhurst/Dekaf/blob/06aa795796080ce6879139ac2a253d4f0b4266ea/.github/benchmarks/aba/3085/RelayMetricsBenchmarks.cs), copied without changing its workload methods. [Wrapper](Program.cs.txt), [fixture copy](RelayMetricsBenchmarks.cs.txt), [project](harness.csproj.txt), [runner](run.ps1), [predeclared plan](experiment-plan.md), [provenance hashes](provenance.json) and [summary](results/summary.json) are retained here.
- Local Windows 11 25H2, Intel Core i7-12700K, SDK 10.0.400/runtime 10.0.11, BDN 0.15.8, Release, InProcessEmit, affinity mask 4, concurrent workstation GC, tiered compilation disabled.
- Sequential A1, B, A2; one fresh process for each of the four cases per phase. Both products and all fixtures build and validate first; all owned builds/tests finish before timing. No competing owned heavy work runs during the campaign.
- Each workload executes for at least 20 elapsed seconds before eight BDN warmup iterations and 15 measured iterations targeting 250 ms each. All outliers are retained. One operation is a complete fake-store/fake-publisher relay cycle acknowledging and deleting 500 rows. It is neither Kafka throughput nor actual message latency.

This incremental comparison does not replace the required fresh-main hosted Ubuntu comparison. The [older hosted run](https://github.com/thomhurst/Dekaf/actions/runs/34146134261) measured product `3feaeaa1f544e7f2d308f9083d4e0134aeafcd0b` against main `a48fafe4121350da7ad83fcdd238f0f8039d6d59`. Its confirmed disabled-synchronous regression remains historical evidence and a blocker; allocation gains never waived it. Main remained `5df2f0d03607389384b5c1466e17812a9084fac9` at the end of this local comparison.

## Results

Times are ns per 500-row cycle. Error is the BDN 99.9% confidence half-width; every cell retains 15 samples. Raw logs preserve the complete distributions, including maxima.

| Workload | A1 mean ± error | B mean ± error | A2 mean ± error | B/A1 | B/A2 | A drift |
|---|---:|---:|---:|---:|---:|---:|
| Synchronous, metrics disabled | 256.0 ± 14.44 | 244.1 ± 3.57 | 242.9 ± 4.40 | -4.65% | +0.49% | -5.12% |
| Pending, metrics disabled | 424.5 ± 12.93 | 434.5 ± 47.66 | 436.4 ± 6.10 | +2.36% | -0.43% | +2.80% |
| Synchronous, metrics enabled | 365.8 ± 6.70 | 371.9 ± 18.62 | 367.0 ± 4.34 | +1.67% | +1.33% | +0.33% |
| Pending, metrics enabled | 714.6 ± 49.36 | 586.9 ± 36.93 | 699.2 ± 21.41 | -17.87% | -16.06% | -2.15% |

| Allocated bytes per cycle, MemoryDiagnoser | A1 | B | A2 |
|---|---:|---:|---:|
| Synchronous, disabled | 0 | 0 | 0 |
| Pending, disabled | 0 | 0 | 0 |
| Synchronous, enabled | 0 | 0 | 0 |
| Pending, enabled | 176 | 0 | 176 |

Thread-local probes around 1,000 completed cycles independently record exactly 0 B in every candidate case, and 176,000 B for enabled pending cycles in both controls. All other control probes are 0 B. Every raw BDN GC record includes a fixed 1,296 B of measurement overhead across its entire multi-invocation measurement; the displayed per-cycle values round that overhead. The raw counts and operation denominators are preserved rather than calling that overhead a message allocation. Initial state-machine pool misses, fixture setup, metric-state registration, cold gauge export and Kafka publisher costs are outside these measured intervals.

Warmup elapsed times range from 20.000 to 20.024 seconds. Completed cycles (synchronous disabled / pending disabled / synchronous enabled / pending enabled) were:

| Phase | Completed warmup cycles |
|---|---|
| A1 | 68,590,695 / 42,928,404 / 49,351,515 / 27,056,560 |
| B | 66,576,216 / 43,779,104 / 47,889,272 / 30,571,099 |
| A2 | 68,306,100 / 43,719,592 / 49,235,765 / 26,314,333 |

Each console log retains one-second warmup JIT compilation counts, thread-pool counts, process CPU, GC collections, heap and RSS. Correctness checks match 500 deleted rows per completed cycle and, when enabled, the same acknowledged count. These are controlled fixture operations, not independent broker deliveries.

The disabled-synchronous control drift exceeds the predeclared 3% diagnostic drift limit. Its candidate result lies between controls, so an improvement is not established. Enabled pending allocation and mean time improve in this fixture, but that gain does not offset or dismiss uncertainty in another protected path. Other point estimates lie within the diagnostic timing band; this and overlapping intervals do not prove equivalence.

Missing acceptance evidence remains: a new fresh-main hosted Ubuntu A1/B/A2 run, client CPU per completed message, actual message p50/p99/max latency, measured runtime time series, loaded error/recovery/shutdown paths and sustained stability. Warmup counters alone do not establish measured steady state or long-run stability. No paid run, merge, threshold change or tradeoff approval follows from this experiment.

## Retention

All loaded baseline/candidate binaries, fixture/probe sources, raw measurements, test binaries/reports and source archives are copied with SHA-256 verification to the final PR head's directory under `C:/git/Dekaf-evidence/pr-3085/`. The PR comment records the exact directory and verified inventory count. These repository files are a portable subset; paths inside removable worktrees are disposable.
