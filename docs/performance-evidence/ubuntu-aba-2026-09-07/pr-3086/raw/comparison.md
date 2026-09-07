# PR #3086: Ubuntu A–B–A

A: `a48fafe4121350da7ad83fcdd238f0f8039d6d59`; B: `3d63058d67d273dbada8137558087bfdfbae5c92`.

Measurement completion is not performance acceptance. No automatic performance-gate override.

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
