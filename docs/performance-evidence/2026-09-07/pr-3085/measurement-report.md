# PR #3085 ValueTask relay candidate

Candidate in `source/`, based on exact published ac40c2a3c0e036a345c78aadb0f6a0d797e8f7e7. Restored the preserved ValueTask prototype, simplified the redundant blank line, and adapted private-method test binding. Cycle/drain awaitables use pooled ValueTask builders; the already-valid lease path bypasses the refresh async state machine. Public APIs and lease/delivery/cancellation rules are unchanged.

Shared performance lock held throughout preparation and baseline → published → candidate → published → baseline. Identical fixture DLL and core Dekaf DLL across fresh hosts; candidate Outbox DLL freshly built. Runtime 10.0.11, SDK 10.0.400; TieredCompilation=0, affinity 4, in-process emit, 5 warmups/15 iterations/250 ms. All 40 measured cases and Dry/fixture validations completed. Small Redis containers and floci remained running; no competing benchmark/test workload. Two owned idle MSBuild nodes remained after preparation.

| Per 500-row batch | Published before | Candidate | Published control | Allocation before → candidate |
|---|---:|---:|---:|---:|
| Disabled synchronous | 259.604 ns | 247.461 ns | 260.177 ns | 144 → 0 B |
| Disabled pending | 485.983 ns | 413.964 ns | 489.525 ns | 480 → 0 B |
| Enabled synchronous | 381.499 ns | 371.763 ns | 385.878 ns | 144 → 0 B |
| Enabled pending | 774.016 ns | 696.642 ns | 778.516 ns | 784 → 176 B |
| Actual publisher, disabled relay | 42.75 us | 42.39 us | 48.56 us | 368168 → 368024 B |
| Actual publisher, enabled relay | 45.10 us | 47.14 us | 46.78 us | 368168 → 368024 B |

Prefeature baseline (5ad5f2c9f587976aed918201703cf07cf37000ef) synchronous controls 250.476/248.261 ns and pending controls 493.303/486.803 ns. Candidate removes the observed disabled relay slowdown in this configuration. Published synchronous controls differ only 0.22%; pending 0.73%. Actual publisher controls are much noisier, including unchanged direct-publisher measurements. Do not infer a full-system performance pass from the small fixture. Existing 368024 B actual publisher allocations are not cold noise or zero per-message publication; 176 B enabled pending is amortized per batch.

Validation: 96 Outbox unit cases pass on each net10.0 and net8.0; four Kafka/SQLite integration cases pass on net10.0. `git diff --check` passes. The restored patch has no newly introduced per-record tasks, delegates, allocation or concurrency.

Decision: retain local candidate as a measured relay improvement. Full Pareto acceptance remains INCONCLUSIVE because actual publication, CPU/message, delivery p50/p99/max and stability have not all been established. No PR push, merge or status override. Raw logs, reports, hashes and `summary.json` are alongside this file.
