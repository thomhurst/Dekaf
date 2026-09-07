# PR #3086: Ubuntu A–B–A

Candidate `3d63058d67d273dbada8137558087bfdfbae5c92`; main baseline `a48fafe4121350da7ad83fcdd238f0f8039d6d59`. [Completed Actions run](https://github.com/thomhurst/Dekaf/actions/runs/34145873466).

**Acceptance: INCONCLUSIVE.** Follower-error handling improves 23â€“27% and saves 64 B/fetch; prefetch-success timing remains uncertain.

Foreground follower error is 1.204 us versus 1.578/1.564 us, and prefetch follower error is 1.202 us versus 1.580/1.636 us. Allocation falls from 248 to 184 B per empty fetch. Main incorrectly resets the follower offset; the candidate retains it. Successful prefetch is 1.248 us versus 1.190/1.211 us (+4.93%/+3.07%), but timing intervals are not separated from both controls. Foreground success lies between controls. CPU/message, delivery tails and stability remain unmeasured; gains on the error path do not erase uncertainty elsewhere.

The table below reports ns per benchmark operation, using each fixture's OperationsPerInvoke denominator. Shutdown and parser rows are whole operations/batches; sustained dispatch is per message. BDN iteration statistics are not message latency percentiles. CPU/message and long-run stability were not measured by this run. The existing performance gate remains blocking; no prior protected-metric finding is erased by a mean-time improvement.

See [raw provenance](raw/provenance.json), [complete statistics](raw/comparison.json), logs and before/candidate/control reports in [raw](raw). Fixture adapters and scope are documented in the [runner README](https://github.com/thomhurst/Dekaf/blob/06aa795796080ce6879139ac2a253d4f0b4266ea/.github/benchmarks/aba/README.md).

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dekaf.Benchmarks.Benchmarks.Unit / ConsumerFollowerOffsetRetryBenchmarks / FollowerError / Prefetch=False | 1577.593 | 1204.291 | 1564.061 | -23.66% | -23.00% | -0.86% | 248 / 184 / 248 |
| Dekaf.Benchmarks.Benchmarks.Unit / ConsumerFollowerOffsetRetryBenchmarks / FollowerError / Prefetch=True | 1580.377 | 1201.826 | 1636.111 | -23.95% | -26.54% | +3.53% | 248 / 184 / 248 |
| Dekaf.Benchmarks.Benchmarks.Unit / ConsumerFollowerOffsetRetryBenchmarks / LeaderError / Prefetch=False | 1570.818 | 1444.298 | 1590.276 | -8.05% | -9.18% | +1.24% | 248 / 248 / 248 |
| Dekaf.Benchmarks.Benchmarks.Unit / ConsumerFollowerOffsetRetryBenchmarks / LeaderError / Prefetch=True | 1582.879 | 1473.300 | 1553.470 | -6.92% | -5.16% | -1.86% | 248 / 248 / 248 |
| Dekaf.Benchmarks.Benchmarks.Unit / ConsumerFollowerOffsetRetryBenchmarks / ResponsePoolControl / Prefetch=False | 159.287 | 154.502 | 157.905 | -3.00% | -2.16% | -0.87% | 0 / 0 / 0 |
| Dekaf.Benchmarks.Benchmarks.Unit / ConsumerFollowerOffsetRetryBenchmarks / ResponsePoolControl / Prefetch=True | 155.305 | 157.630 | 162.658 | +1.50% | -3.09% | +4.73% | 0 / 0 / 0 |
| Dekaf.Benchmarks.Benchmarks.Unit / ConsumerFollowerOffsetRetryBenchmarks / SuccessfulFetch / Prefetch=False | 1184.329 | 1186.151 | 1216.402 | +0.15% | -2.49% | +2.71% | 184 / 184 / 184 |
| Dekaf.Benchmarks.Benchmarks.Unit / ConsumerFollowerOffsetRetryBenchmarks / SuccessfulFetch / Prefetch=True | 1189.745 | 1248.405 | 1211.199 | +4.93% | +3.07% | +1.80% | 184 / 184 / 184 |
