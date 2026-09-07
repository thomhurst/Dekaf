# PR #3109: performance evidence, 2026-09-07

The complete 1,024-record drain allocates 120 B less per shutdown. Expanded timing lies between unchanged controls. This remains a provisional allocation reduction, without a shutdown speedup claim.

**Performance acceptance: INCONCLUSIVE; merge remains blocked.** The GitHub `agent/performance-gate` status uses `failure` because legacy statuses have no terminal inconclusive state that also blocks merging. It is not a claim that every observed difference is a proven regression. CPU/message, p50/p99/max latency and stability are not all established; no protected-metric tradeoff is accepted.

87 unit cases pass on each of net10.0 and net8.0; 3 Kafka integration cases pass on net10.0. The product patch matches the previously validated patch byte for byte. Published commit identity is recorded by Git; [source-snapshot.json](source-snapshot.json) binds the tested parent, patch hash and source blobs to this publication. These are source-equivalent measurements, not a claim that the newly created Git commit was benchmarked again.

See [measurement-report.md](measurement-report.md) for before/candidate/control values, allocations, environment, limitations and the acceptance decision. It is an immutable report written before publication: references there to local-only patches or no push describe that earlier phase. This README records the later publication decision.

The [evidence](evidence) directory contains raw BenchmarkDotNet JSON/CSV reports, per-iteration logs, fixture snapshots, product hashes and validation logs. Different configurations, failed fixture attempts and rejected experiments remain distinct. Fixture source/project/solution files use a `.txt` suffix to keep archived inputs out of repository builds; remove that suffix and adjust recorded absolute paths when reproducing. Original measured DLLs and raw profile latency streams remain in the local evidence archive; binaries are not committed. Any absent end-to-end metrics remain unknown.

Local benchmark work used the shared performance reservation. This publication reuses that evidence and does not run concurrent benchmarks, change thresholds, merge the PR or dispatch paid stress workflows.
