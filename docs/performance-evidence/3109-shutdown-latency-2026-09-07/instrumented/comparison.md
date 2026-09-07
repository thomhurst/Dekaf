# PR #3109: Ubuntu A–B–A

A: `c212c575528e055fe628568e19fb521fc54b0ab9`; B: `36ba24db1203c6f464fd253b12c97109377fd28c`.

Measurement completion is not performance acceptance. No automatic performance-gate override.

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dekaf.Benchmarks / PartitionedShutdownBenchmarks / DrainFullQueue /  | 204250.997 | 197902.640 | 207316.380 | -3.11% | -4.54% | +1.50% | 51680 / 51600 / 51680 |
