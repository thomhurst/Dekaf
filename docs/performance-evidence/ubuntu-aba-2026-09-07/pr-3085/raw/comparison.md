# PR #3085: Ubuntu A–B–A

A: `a48fafe4121350da7ad83fcdd238f0f8039d6d59`; B: `3feaeaa1f544e7f2d308f9083d4e0134aeafcd0b`.

Measurement completion is not performance acceptance. No automatic performance-gate override.

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| None / ActualPublisherBenchmarks / PublisherControl / Enabled=False | 65168.875 | 65018.299 | 66838.409 | -0.23% | -2.72% | +2.56% | 368024 / 368024 / 368024 |
| None / ActualPublisherBenchmarks / PublisherControl / Enabled=True | 64995.462 | 65041.885 | 66477.925 | +0.07% | -2.16% | +2.28% | 368024 / 368024 / 368024 |
| None / ActualPublisherBenchmarks / RelayBatch / Enabled=False | 68172.662 | 67980.549 | 65900.249 | -0.28% | +3.16% | -3.33% | 368168 / 368024 / 368168 |
| None / ActualPublisherBenchmarks / RelayBatch / Enabled=True | 70050.218 | 66315.513 | 67420.119 | -5.33% | -1.64% | -3.75% | 368168 / 368024 / 368168 |
| None / RelayMetricsBenchmarks / PendingBatch / Enabled=False | 974.766 | 850.881 | 948.786 | -12.71% | -10.32% | -2.67% | 480 / 0 / 480 |
| None / RelayMetricsBenchmarks / PendingBatch / Enabled=True | 955.575 | 1398.296 | 959.202 | +46.33% | +45.78% | +0.38% | 480 / 176 / 480 |
| None / RelayMetricsBenchmarks / PublisherControl / Enabled=False | 5.575 | 5.582 | 5.559 | +0.13% | +0.42% | -0.29% | 0 / 0 / 0 |
| None / RelayMetricsBenchmarks / PublisherControl / Enabled=True | 5.558 | 5.536 | 5.559 | -0.40% | -0.42% | +0.03% | 0 / 0 / 0 |
| None / RelayMetricsBenchmarks / SynchronousBatch / Enabled=False | 519.069 | 538.134 | 521.076 | +3.67% | +3.27% | +0.39% | 144 / 0 / 144 |
| None / RelayMetricsBenchmarks / SynchronousBatch / Enabled=True | 513.593 | 747.671 | 517.182 | +45.58% | +44.57% | +0.70% | 144 / 0 / 144 |
