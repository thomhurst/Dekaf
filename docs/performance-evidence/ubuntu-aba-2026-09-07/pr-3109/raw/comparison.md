# PR #3109: Ubuntu A–B–A

A: `a48fafe4121350da7ad83fcdd238f0f8039d6d59`; B: `5446a4f35718f08496a846c7581160204717ea1a`.

Measurement completion is not performance acceptance. No automatic performance-gate override.

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dekaf.Benchmarks / PartitionedShutdownBenchmarks / DrainFullQueue /  | 204975.860 | 210318.633 | 212485.267 | +2.61% | -1.02% | +3.66% | 54656 / 51600 / 51680 |
