# Ubuntu A–B–A investigation

The shared microbenchmark fixtures cover nine PR investigations. Dispatch the dedicated `performance-comparison.yml` workflow with `suite=micro` and independent exact harness/baseline/candidate SHAs; see [maintained harness instructions](../README.md). The original investigation settings below are historical and do not establish complete performance acceptance.

Each dispatch has one `ubuntu-latest` job. Both products and fixture hosts build before timing. Dry runs require the declared case count and successful measurements. A1, B and A2 then run sequentially as fresh .NET processes pinned to the same available logical CPU, with tiered compilation disabled. A1 and A2 use the same saved DLLs; their hashes are checked. BenchmarkDotNet uses in-process emit inside each isolated host, avoiding benchmark builds during the three phases. All outliers remain. The workflow records the runner image, CPU, runtime, timestamps, process snapshots, raw JSON/CSV/logs, fixture sources, allocations and both candidate deltas separately.

Most cases use eight warmups and 25 measured iterations with a 250 ms target. Full-queue shutdown uses 30 warmups and 300 one-invocation iterations because the queue must reset before every shutdown. These Linux results are a new configuration and cannot be pooled with the earlier Windows measurements.

Fixture differences required for a fresh-main comparison:

- #3082: repeated and distinct binary keys plus string control; main's reference equality has a known correctness difference for separately fetched equal keys. Existing dispatch allocations remain measured.
- #3083: typed/raw traversal and completion-bound construction/parsing, with equal batch shapes.
- #3085: synchronous/pending relay cycles and actual publisher/direct-publisher controls, with listeners disabled/enabled. Main has no new Outbox instruments; the enabled baseline means an enabled listener with no corresponding instruments. Delegate binding adapts Task/ValueTask return types once during setup.
- #3086: successful, follower-error, leader-error and response-pool paths for foreground/prefetch. Main's follower-error offset reset is explicitly asserted as its known incorrect behavior; the candidate must retain the position. The two-response retry case is excluded because main cannot perform the same correct retry. Empty-fetch allocation and this semantic difference prevent interpreting faster incorrect behavior as acceptance.
- #3109: a complete 1,024-record queue drain. Main starts its timeout inside StopAllBoundedAsync; the candidate starts the shared deadline through StopHandlerCommits. Both deadline creation paths are measured. Cleanup tolerates the field absent on main.
- #3116: 1,024 records, no headers; synchronous/warm/cold parsing and retained traversal. Main has no borrowed API, so the Borrowed-named rows use its equivalent legacy parser; candidate rows use borrowed parsing. Both compute the same record/header checksum. Legacy rows remain as common controls. This is an API implementation comparison, not an assertion that main has the new API.
- #3117: sustained distinct and paired record/batch dispatch; identical lifecycle and zero-allocation probes, with main's existing allocation reported as measured.

A successful workflow means a complete measurement set. It does not set `agent/performance-gate` to success, invent performance thresholds, infer message latency percentiles from BDN iteration statistics, or establish CPU/message and long-run stability. Inspect control movement and all protected metrics before accepting a PR. Raw artifacts are retained for 30 days.
