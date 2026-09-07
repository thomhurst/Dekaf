# PR #3117: Ubuntu A–B–A

Candidate `e9e6e3c1584c95c990755979672cf41de67f62c3`; main baseline `a48fafe4121350da7ad83fcdd238f0f8039d6d59`. [Completed Actions run](https://github.com/thomhurst/Dekaf/actions/runs/34145891257).

**Acceptance: INCONCLUSIVE (full acceptance).** All four sustained dispatch cases improve 70â€“79% versus both main controls, with 0 B/message.

Candidate distinct/paired records are 217.910/225.071 ns/message, versus main controls 735.710/729.063 and 933.673/965.655 ns. Distinct/paired batches are 212.654/222.373 ns versus 768.889/827.873 and 1029.982/1074.610 ns. Allocation falls from 928â€“2,440 B/message to 0 B/message. These are strong scoped timing/allocation gains. This run does not measure CPU/message, per-message p50/p99/max or stability, and therefore does not resolve the earlier CPU/tail acceptance findings for the incremental safety guard/candidate.

The table below reports ns per benchmark operation, using each fixture's OperationsPerInvoke denominator. Shutdown and parser rows are whole operations/batches; sustained dispatch is per message. BDN iteration statistics are not message latency percentiles. CPU/message and long-run stability were not measured by this run. The existing performance gate remains blocking; no prior protected-metric finding is erased by a mean-time improvement.

See [raw provenance](raw/provenance.json), [complete statistics](raw/comparison.json), logs and before/candidate/control reports in [raw](raw). Fixture adapters and scope are documented in the [runner README](https://github.com/thomhurst/Dekaf/blob/06aa795796080ce6879139ac2a253d4f0b4266ea/.github/benchmarks/aba/README.md).

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dekaf.Benchmarks / PartitionedLifetimeBenchmarks / ProcessLifetime / Mode=KeyBatchesDistinct | 768.889 | 212.654 | 827.873 | -72.34% | -74.31% | +7.67% | 2248 / 0 / 2248 |
| Dekaf.Benchmarks / PartitionedLifetimeBenchmarks / ProcessLifetime / Mode=KeyBatchesPaired | 1029.982 | 222.373 | 1074.610 | -78.41% | -79.31% | +4.33% | 2440 / 0 / 2440 |
| Dekaf.Benchmarks / PartitionedLifetimeBenchmarks / ProcessLifetime / Mode=KeyRecordsDistinct | 735.710 | 217.910 | 729.063 | -70.38% | -70.11% | -0.90% | 928 / 0 / 928 |
| Dekaf.Benchmarks / PartitionedLifetimeBenchmarks / ProcessLifetime / Mode=KeyRecordsPaired | 933.673 | 225.071 | 965.655 | -75.89% | -76.69% | +3.43% | 1164 / 0 / 1164 |
