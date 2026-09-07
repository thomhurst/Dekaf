# Permanent key-ordered dispatch coverage

This update documents the effective batch cap and adds permanent coordinator benchmarks. It changes no executable product code. Performance acceptance for PR #3117 remains **INCONCLUSIVE**; this local, single-product coverage diagnostic does not replace the missing fresh-main Ubuntu A1/B/A2 comparison.

## Revisions and scope

- Executable product is unchanged from `7ac2e6672bec50297be69fcfe1b18e2f879a2c3f`.
- Documentation, contract tests and initial fixture: `f96f26d6f53a6c3bcb2f76c96577337ac39e989a`.
- Measured final fixture: `dccc20f2605324cfef7c973b6888708f3a1a71bf`.
- Pinned fresh main: `5df2f0d03607389384b5c1466e17812a9084fac9`, contained by the measured head and unchanged at the pre-publication check.

One benchmark operation completes an entire 262,144-record partition lifetime. It includes coordinator construction/shutdown, bounded input replenishment, per-key order assertions, clearing the fixture's preallocated order array, final checkpoint validation and deadline infrastructure. It excludes Kafka, fetch/deserialization, retained fetch payloads, thread-pool scheduling and real application work. Results are fixture-inclusive, not isolated library cost.

Capacity is 128 with two workers. Synchronous repeated/distinct-key input delivers singleton batches even when the configured cap is 16. Pending paired keys arrive in runs of twice the configured batch size, allowing records to queue while the first key waits for the second. The fixture asserts that its largest pending-handler batch equals the configured size. Every lifetime verifies the completed record count, per-key order and final `(key-dispatch, 0, 262144, epoch 7)` commit frontier.

## Local diagnostic

Windows 11 25H2 (10.0.26200.9168), Intel Core i7-12700K, SDK 10.0.400, .NET 10.0.11, BenchmarkDotNet 0.15.8, Release net10.0. `DOTNET_TieredCompilation=0`, process affinity mask 4. The [predeclared experiment](experiment.md) requests 30 workload warmups at a 1,000 ms target iteration time and 15 measured iterations with `DontRemove` outliers. All owned builds/tests/docs commands completed before measurement. This is a shared local host, not an isolated hosted acceptance runner.

[Raw output](benchmark-measured-batched.log), [BDN report](results/Dekaf.Benchmarks.Benchmarks.Unit.KeyOrderedDispatchBenchmarks-report-github.md), [CSV](results/Dekaf.Benchmarks.Benchmarks.Unit.KeyOrderedDispatchBenchmarks-report.csv) and [warmup verification](warmup-verification.csv) retain all samples. Error below is BDN's 99.9% confidence half-width. There are no A1/A2 controls or candidate deltas in this coverage run.

| Key pattern | Configured cap | Largest batch | Mean per lifetime | Error | MemoryDiagnoser per lifetime | Actual workload warmup | Warmup lifetimes |
|---|---:|---:|---:|---:|---:|---:|---:|
| Repeated | 1 | 1 | 35.95 ms | 1.218 ms | 45.66 KB | 34.63 s | 960 |
| Repeated | 16 | 1 | 34.06 ms | 2.272 ms | 47.00 KB | 34.00 s | 960 |
| Distinct | 1 | 1 | 33.65 ms | 1.407 ms | 45.66 KB | 32.99 s | 960 |
| Distinct | 16 | 1 | 34.95 ms | 1.195 ms | 47.00 KB | 33.39 s | 960 |
| Pending pairs | 1 | 1 | 35.35 ms | 2.241 ms | 46.02 KB | 33.53 s | 960 |
| Pending pairs | 16 | 16 | 31.78 ms | 3.691 ms | 48.70 KB | 44.19 s | 1440 |

Each warmup lifetime completes 262,144 records; warmup therefore completes 251,658,240 records per case, except the last case's 377,487,360 records. All six cases retain 15 measured samples. The exact current-thread allocation probe covers records 257 through 262144, requires execution to remain on the probe thread, and reports **0 bytes over 261,888 records** in every case. This separately verifies zero per-message allocation in that window. MemoryDiagnoser remains unnormalized at one complete lifetime per operation: its nonzero values include amortized construction, storage and shutdown plus fixture costs. They are not hidden by rounding per-message results to zero. All six raw BDN GC rows report zero collections during the allocation measurement; this is not a GC time series or long-run stability evidence.

No actual per-message p50/p99/max latency, process CPU per completed message, runtime/JIT/thread-pool time series, loaded broker backlog, heap/RSS trends or long-run stability was measured. BDN lifetime timings and confidence intervals cannot substitute for those metrics. Actual elapsed warmup exceeds 20 seconds, but without runtime time series this is not proof of steady state. Timing cannot establish a performance PASS or resolve the existing hosted control drift. No hosted or paid run was dispatched for this update.

## Fixture correction and validation

The first fixture alternated individual paired keys, which released pending work too promptly to build multi-record batches. Review caught the coverage gap. That run was stopped, preserved in [superseded-singleton-input.log](superseded-singleton-input.log), and excluded from the corrected fixture results. [Disposition](superseded-reason.md) records the source SHA and verified owned processes stopped. This was a workload correction, not selective removal of slow samples. The corrected fixture groups keys and asserts observed batch sizes; [all six Dry cases](benchmark-dry-batched.log) and the measured cases complete successfully.

The documented cap is the smaller of MaxHandlerBatchSize and buffered records divided by effective workers, using integer division. Effective workers are the smaller of configured concurrency and buffered records. Four contract cases verify 256/4/requested100 gives 64; 400/4/requested100 gives 100; 8/3/requested4 gives 2; and 8/16/requested10 gives 1. They also verify ordering, completed count and final checkpoint.

All 24 coordinator cases pass on each of net10.0 and net8.0, including registration failures and mutated-key collision cleanup. [Resolved project targets](test-product-targets.json) confirm net8.0 exercises the netstandard2.0 product branch. Release benchmark build has no warnings/errors; the docs build and all 468 C# documentation snippets pass. Earlier full product/integration validation remains attributed to the unchanged executable-product SHA above, not claimed as a new run here.

## Retention

Before publication, raw logs, result files, exact loaded benchmark and unit-test binaries, generated BDN worker files, and source snapshots are copied outside every removable worktree to `C:/git/Dekaf-evidence/pr-3117/<published-evidence-head>/`, with an inventory verified against original SHA-256 hashes. The PR comment records that exact directory and file count. The superseded worker is already retained separately at `C:/git/Dekaf-evidence/pr-3117/review-followup/superseded-worker/` with 701 verified files. The published head adds only this evidence to the measured fixture revision.

## Evidence-parser review follow-up

The post-processing validator now requires the exact six `(Pattern, BatchSize)` combinations, verifies the observed largest batch for each case, and rejects mismatched or duplicate allocation-result lines. `test-summarize-run.ps1` checks the retained valid log plus six malformed variants. The previous parser incorrectly accepted duplicate cases, a 1-record largest batch for PendingPairs/16, a mismatched allocation label and duplicate allocation rows. All seven final scenarios pass, and the valid input produces exactly the previously published CSV. Raw measurements and product/fixture code are unchanged; no benchmark or product test was rerun for this evidence-only correction. Red/green parser inputs and outputs are retained outside removable worktrees at `C:/git/Dekaf-evidence/pr-3117/parser-review/`.
