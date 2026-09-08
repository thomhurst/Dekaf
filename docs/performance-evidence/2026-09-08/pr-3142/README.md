# Pending-request reset accounting: hosted diagnostic

**INCONCLUSIVE for performance acceptance.** Correctness checks pass and the warm pool benchmark allocates 0 B/op in all three phases. Candidate mean is 9.50% lower than A1 and 9.27% lower than A2, with -0.25% control drift. Compilation continues across measured workload boundaries without method attribution. Actual per-message latency, loaded completed throughput, CPU per message, and sustained stability remain missing; this microbenchmark improvement cannot clear those requirements.

## Change and correctness

When reservation cleanup throws during `PendingRequestPool.Return`, Reservoir destroys the failed request and decrements the retained count, but the exception skips the wrapper's increment. Moving that existing increment into `PoolPolicy.TryReset`, before cleanup, lets destruction balance both reset failure and capacity rejection. The exception and discard behavior remain unchanged; successful return gains no allocation, exception handler, or counter operation.

Unchanged baseline fails two new tests: count -1 after one failure, and -8 after 16 failures among 32 concurrent returns into capacity 8. Nine other focused cases pass. The correction passes all 409 networking tests on both net10.0 and net8.0; net8.0 exercises core netstandard2.0. Coverage includes exception identity, disposal once, reuse, discard, null rejection, capacity, and concurrent returns. An initial fixture-constructor compilation error is retained separately from the red test run. This is internal fault injection, not an observed production incident. No public delivery behavior changes; no new Kafka integration scenario was added for this diagnostic counter correction.

## Exact experiment

- A: fresh main `9eec358dad2a081dedbbfc75f02aee743e6bdad9`.
- B: `4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d`, containing A. Reservoir stays 1.6.7.
- Harness: [`b26ffdc9070cc68ceaca07bf4193f334df6b2744`](https://github.com/thomhurst/Dekaf/tree/b26ffdc9070cc68ceaca07bf4193f334df6b2744/.github/benchmarks/pool-reset), including predeclared `PLAN.md` and the [driver](https://github.com/thomhurst/Dekaf/blob/b26ffdc9070cc68ceaca07bf4193f334df6b2744/.github/scripts/pool_reset_aba.py). Do not merge this experimental branch.
- [Run 34189269388](https://github.com/thomhurst/Dekaf/actions/runs/34189269388): one `ubuntu-latest` job; both products build and both fixtures validate before sequential A1/B/A2 fresh processes. Main remains unchanged at final assessment.
- Ubuntu 24.04.4, image `20260831.293.1`, Intel Xeon Platinum 8573C, 4 logical/2 physical cores. SDK 10.0.400, runtime 10.0.11, Release/net10.0, BenchmarkDotNet 0.15.8, InProcessEmit, workstation GC, affinity logical CPU 2; tiered compilation, PGO and ReadyToRun enabled. High-priority setup is denied in every phase; all use the ordinary permitted priority.

One operation rents and returns a warm request and reads `ApproximateCount`: capacity 256, `threadLocalFastPath: false`, same-thread return. Setup verifies identity and count. Every process warms the exact operation for at least 20 elapsed seconds, then runs 30 one-second BDN warmups and 25 one-second actual iterations. Elapsed warmup is verified below. `DontRemove` retains all 75 samples, including statistical outliers and every maximum. This fixture has no broker, offered message load, parsing, cross-thread completion, or error/recovery/shutdown workload. Exception-path allocations are cold cleanup failures and excluded from the warm-reuse claim.

The predeclared numerical screen requires 0 B/op, at most 2% control drift, and no candidate mean loss above 5% against either control. Those numerical thresholds are met. The plan also makes unresolved runtime transitions INCONCLUSIVE and withholds overall acceptance when protected metrics are missing. No identical repeat or paid stress run follows.

## Results

Times are BDN statistics per completed pool operation, with overhead subtraction, not individual message latencies. Each phase has one process and 25 measured iterations; iterations are not independent runner replications.

| Metric | A1 | B | A2 |
|---|---:|---:|---:|
| Mean ns/op | 48.854649 | 44.213577 | 48.732145 |
| Standard error ns/op | 0.036188 | 0.021620 | 0.014284 |
| 99.9% mean interval ns/op | 48.719108-48.990189 | 44.132601-44.294552 | 48.678644-48.785646 |
| Maximum iteration ns/op | 49.578234 | 44.513362 | 48.916007 |
| MemoryDiagnoser B/op | 0 | 0 | 0 |
| Actual operations | 481,439,200 | 528,508,000 | 483,653,600 |
| Actual workload seconds | 24.710806 | 24.670233 | 24.751927 |
| Setup warmup seconds | 20.000365 | 20.000006 | 20.000004 |
| Setup warmup operations | 147,100,336 | 153,269,023 | 147,459,578 |
| BDN workload warmup seconds | 29.741162 | 29.781642 | 29.829653 |
| BDN workload warmup operations | 577,727,040 | 634,209,600 | 580,384,320 |

Mean deltas: **B/A1 -9.4998%**, **B/A2 -9.2723%**, **A2/A1 -0.2508%**. MemoryDiagnoser reports exactly 0 B/op throughout, and each separate setup allocation check reports 0 bytes over 1,000 warm operations. Full JSON reports and `samples.csv` retain all samples, bounds, counts and maxima.

## Runtime evidence and limits

The logger samples immediately after each BDN workload interval without adding a background thread or entering the timed operation. From the last warmup record to the final actual record, JIT counts increase **57/47/43** in A1/B/A2; from the first actual record to the last, **36/36/34**. No method-level trace separates harness transitions from product compilation. Thus this data does not prove that measured intervals are free of runtime transitions.

No sampled process grows its thread pool or pending-work queue above zero. Observer-bracket CPU totals are 24,804.877/24,752.006/24,833.920 ms, including inter-iteration/harness activity; these do not measure client CPU per completed message. Each bracket observes 100 collections in each generation, while MemoryDiagnoser records zero collections in its actual workload accounting. Do not attribute those process-wide observer counts to per-message allocation. Heap boundaries are 422,760 to 427,496 / 423,456 to 428,888 / 423,656 to 429,344 bytes. RSS boundaries are 85,835,776 to 87,920,640 / 86,577,152 to 85,811,200 / 85,700,608 to 87,769,088 bytes. Full time series remain beside this report; these short processes cannot establish sustained stability or client backlog behavior.

The changed failure contract has deterministic correctness evidence, but loaded completion timing and error/recovery/shutdown performance remain unmeasured. Successful workflow execution and improved microbenchmark means are not a performance PASS. The PR gate remains failed until an appropriate exact-head campaign covers all applicable protected metrics and resolves runtime attribution. No tradeoff is approved.

## Retention and reanalysis

Local archive: `C:/git/Dekaf-evidence/pr-3142/4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d/local-20260908/`: 2,111 verified files (987,994,165 bytes), including red/green hosts, source ZIPs, both runtime test reports, assembly bindings, the initial compilation failure and two fixture smoke runs. No local timed comparison started because another worker occupied that machine.

Hosted archive: `C:/git/Dekaf-evidence/pr-3142/4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d/hosted-34189269388/`. The original 151,134,566-byte GitHub artifact ZIP has SHA-256 `ede8e8e1beb3b6016ff2f779bae516720ab7cca465ff4172e10524e6bb08b3c5`, matching GitHub's published digest. Its 162 extracted files include both original executable hosts, product/harness source ZIPs, build/smoke logs, BDN reports and runtime series. Eighty host-file bindings and nine actual loaded-assembly bindings match retained binaries. InProcessEmit executes inside those retained original hosts. Final archive/publication inventories verify every copy against its original.

Recompute derived results with `python -O assess.py.txt RAW_DIRECTORY OUTPUT_DIRECTORY`, passing the extracted artifact directory and a separate analysis output directory. This report directory contains selected byte-exact inputs and derived results; it is not a standalone benchmark project. Reanalysis performs no benchmark and changes no raw sample. New experiments require their own pins and outputs.
