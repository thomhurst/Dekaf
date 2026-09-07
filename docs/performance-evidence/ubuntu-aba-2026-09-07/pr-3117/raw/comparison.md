# PR #3117: Ubuntu A–B–A

A: `a48fafe4121350da7ad83fcdd238f0f8039d6d59`; B: `e9e6e3c1584c95c990755979672cf41de67f62c3`.

Measurement completion is not performance acceptance. No automatic performance-gate override.

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dekaf.Benchmarks / PartitionedLifetimeBenchmarks / ProcessLifetime / Mode=KeyBatchesDistinct | 768.889 | 212.654 | 827.873 | -72.34% | -74.31% | +7.67% | 2248 / 0 / 2248 |
| Dekaf.Benchmarks / PartitionedLifetimeBenchmarks / ProcessLifetime / Mode=KeyBatchesPaired | 1029.982 | 222.373 | 1074.610 | -78.41% | -79.31% | +4.33% | 2440 / 0 / 2440 |
| Dekaf.Benchmarks / PartitionedLifetimeBenchmarks / ProcessLifetime / Mode=KeyRecordsDistinct | 735.710 | 217.910 | 729.063 | -70.38% | -70.11% | -0.90% | 928 / 0 / 928 |
| Dekaf.Benchmarks / PartitionedLifetimeBenchmarks / ProcessLifetime / Mode=KeyRecordsPaired | 933.673 | 225.071 | 965.655 | -75.89% | -76.69% | +3.43% | 1164 / 0 / 1164 |
