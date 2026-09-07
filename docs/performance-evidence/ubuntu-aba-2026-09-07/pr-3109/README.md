# PR #3109: Ubuntu A–B–A

Candidate `5446a4f35718f08496a846c7581160204717ea1a`; main baseline `a48fafe4121350da7ad83fcdd238f0f8039d6d59`. [Completed Actions run](https://github.com/thomhurst/Dekaf/actions/runs/34145879464).

**Acceptance: INCONCLUSIVE.** Complete-queue shutdown timing lies between main controls; baseline allocation also varies.

Mean full 1,024-record shutdown is 210.319 us versus 204.976/212.485 us (+2.61%/-1.02%). Allocation is 51,600 B versus 54,656/51,680 B. The unchanged baseline allocation differs by 2,976 B between controls; do not present the larger apparent saving as a stable causal improvement. The fixture includes ordered processing, checkpoint and rejected-writer validation, but not end-to-end handler-commit latency or long-run stability.

The table below reports ns per benchmark operation, using each fixture's OperationsPerInvoke denominator. Shutdown and parser rows are whole operations/batches; sustained dispatch is per message. BDN iteration statistics are not message latency percentiles. CPU/message and long-run stability were not measured by this run. The existing performance gate remains blocking; no prior protected-metric finding is erased by a mean-time improvement.

See [raw provenance](raw/provenance.json), [complete statistics](raw/comparison.json), logs and before/candidate/control reports in [raw](raw). Fixture adapters and scope are documented in the [runner README](https://github.com/thomhurst/Dekaf/blob/06aa795796080ce6879139ac2a253d4f0b4266ea/.github/benchmarks/aba/README.md).

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dekaf.Benchmarks / PartitionedShutdownBenchmarks / DrainFullQueue /  | 204975.860 | 210318.633 | 212485.267 | +2.61% | -1.02% | +3.66% | 54656 / 51600 / 51680 |
