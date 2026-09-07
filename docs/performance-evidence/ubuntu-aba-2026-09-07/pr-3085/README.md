# PR #3085: Ubuntu A–B–A

Candidate `3feaeaa1f544e7f2d308f9083d4e0134aeafcd0b`; main baseline `a48fafe4121350da7ad83fcdd238f0f8039d6d59`. [Completed Actions run](https://github.com/thomhurst/Dekaf/actions/runs/34146134261).

**Acceptance: REGRESSION (disabled synchronous relay).** Disabled pending relay improves 10â€“13% at 0 B/batch, but disabled synchronous relay is 3â€“4% slower.

Disabled synchronous relay is 538.134 ns per 500-row cycle versus main controls 519.069/521.076 ns (+3.67%/+3.27%), with separated timing intervals and only 0.39% control movement. Allocation falls from 144 to 0 B/batch. Disabled pending relay improves to 850.881 ns versus 974.766/948.786 ns, with 480 to 0 B/batch. Enabled relay paths add roughly 45â€“46% time; main has an enabled listener but lacks the new instruments, so this is explicitly the instrumentation cost, not identical instrumentation behavior. Actual disabled publication lies between controls; its existing allocation is 368,024 B/batch. Allocation improvements do not justify accepting the disabled synchronous loss. The first attempt stopped at Dry validation before timing because two publisher-control cases were omitted from the declared count; the corrected run validates all ten cases with unchanged product SHAs and fixture method bodies.

The table below reports ns per benchmark operation, using each fixture's OperationsPerInvoke denominator. Shutdown and parser rows are whole operations/batches; sustained dispatch is per message. BDN iteration statistics are not message latency percentiles. CPU/message and long-run stability were not measured by this run. The existing performance gate remains blocking; no prior protected-metric finding is erased by a mean-time improvement.

See [raw provenance](raw/provenance.json), [complete statistics](raw/comparison.json), logs and before/candidate/control reports in [raw](raw). Fixture adapters and scope are documented in the [runner README](https://github.com/thomhurst/Dekaf/blob/83d68296b78cfb88ef9b8a68d6688a3346469f28/.github/benchmarks/aba/README.md).

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
