# PR #3109 shutdown latency investigation

The large 10–15 ms tails are dominated by runtime warmup in the short-lived probe. A fixed 20-second warmup removed every >1 ms stop in all three new phases. JIT compilation dropped from about one second per startup measurement phase to almost zero. This supports the warmup explanation; it does not establish a complete performance acceptance verdict. Residual maximum-latency and whole-lifecycle CPU differences remain unresolved.

## Provenance and scope

- Baseline: `c212c575528e055fe628568e19fb521fc54b0ab9`.
- Candidate: `36ba24db1203c6f464fd253b12c97109377fd28c`.
- Original run: https://github.com/thomhurst/Dekaf/actions/runs/34149484062 .
- Diagnostic run: https://github.com/thomhurst/Dekaf/actions/runs/34151392676 .
- Both experiments use exact-SHA A1/B/A2 on one GitHub-hosted `ubuntu-latest` VM per run. Product code was unchanged for this investigation. The diagnostic harness, runtime/image, DLL hashes and declared settings are recorded in `instrumented/provenance.json` and its fixture snapshots.
- Startup and warmed variants each retain all 10,000 measured stops per phase: 60,000 diagnostic samples. Every lifetime starts with 1,024 queued records and validates processing, checkpoint, queue drain and writer rejection. Both variants have identical diagnostic instrumentation; warmup is the intended difference.
- Original candidate validation passed 70 unit and two Kafka integration tests. The instrumented harness passed ten validator tests and local validation of 500 stage samples against each product. Local timing is diagnostic only.

## Original uncertainty

| Metric | A1 | Candidate | A2 |
|---|---:|---:|---:|
| p50, ms | 0.212099 | 0.206138 | 0.212851 |
| p99, ms | 1.609265 | 1.068204 | 1.056795 |
| Maximum, ms | 10.186681 | 15.301584 | 14.590295 |
| Stops >1 ms | 828 | 165 | 179 |

Every original >1 ms stop occurred in the first 2,000 samples. The three maximum samples were #537, #832 and #938. The diagnostic last-8,000 p99 values were 0.326064 / 0.327086 / 0.352594 ms. That retrospective split identified non-stationarity; no original sample was removed or used to relabel the original result as a pass.

## Instrumented evidence

The startup variant reproduces 10.09 / 14.44 / 15.71 ms maxima. Each maximum falls almost entirely inside handler drain, rather than release-to-handler scheduling or final shutdown return. Their 100-sample blocks show active JIT compilation. Startup phases record 1,765–1,798 method compilations and 1.03–1.06 seconds of compilation time, while thread-pool counts grow. These counters establish association with runtime warmup; they do not prove which instruction or OS scheduling event caused each individual pause.

With warmup fixed at 20 seconds before measurement:

| Protected metric | A1 | Candidate | A2 |
|---|---:|---:|---:|
| Stop p50, ms | 0.175978 | 0.177772 | 0.179886 |
| Stop p99, ms | 0.231230 | 0.232011 | 0.233562 |
| Stop maximum, ms | 0.318702 | 0.386720 | 0.323442 |
| Lifecycle CPU, us/operation | 538.6849 | 547.3712 | 531.5168 |
| Lifecycle allocated bytes/operation | 255827.2472 | 255811.1664 | 255809.4936 |
| Stops >1 ms | 0 | 0 | 0 |
| JIT compilation time during measurement, ms | 0 | 0.4134 | 0 |

Candidate p50 and p99 lie between the controls. The warmed candidate maximum is 0.387 ms, versus 0.319/0.323 ms; it is concentrated in handler drain with no GC count change or JIT activity in that sample block. Whole-lifecycle CPU is 1.6%/3.0% higher than the two controls. These remaining differences are not explained by the large startup effect and are not silently accepted.

The separate BDN stop-only benchmark in the diagnostic run measured 204.251 / 197.903 / 207.316 us and 51,680 / 51,600 / 51,680 allocated bytes per stop. Candidate mean time is 3.1%/4.5% lower and allocation is 80 bytes lower against both controls. These are amortized shutdown costs, not per-message hot-path allocations. BDN uses one CPU and tiered compilation off; the staged probe uses two CPUs and default tiered compilation. Do not mix their absolute costs as one measurement scope.

## GC and fixture effects

Each record struct is 96 bytes, so the full queue's element storage alone is 98,304 bytes. Rebuilding the fixture repeatedly allocates about 255 KB per lifecycle and produces roughly 2.5 GB of allocation per 10,000 samples. There were 312 Gen2 collections during each diagnostic measurement phase, including the warmed phases. None of the 60,000 instrumented stop brackets observed a GC count increment. Collection pressure therefore belongs primarily to the surrounding fixture lifetime in this experiment; it does not explain the observed stop spikes directly. GC counters do not capture exact pause duration or prove absence of all runtime suspension.

CPU and lifecycle throughput include setup, record construction, reflection binding, cleanup and instrumentation. Those measurements cannot by themselves identify a CPU regression in the production stop path. Stage timestamps include the handler's record validation and `MarkProcessed` calls. No product optimization was made from these aggregate figures.

## Decision

`INCONCLUSIVE` for full performance acceptance. The original large-tail concern is substantially narrowed to warmup, and measured warmed p99 is consistent across controls. Retain the candidate's higher warmed maximum and whole-lifecycle CPU for targeted assessment. Do not promote the gate, approve a tradeoff, discard tails, or infer application-wide startup/stability guarantees from this diagnostic fixture. No identical diagnostic repeat has been launched.

Raw BDN reports, all stage CSVs and latency binaries, JIT/GC/thread-pool time series, warmup series, fixture sources and original correctness logs are retained here. `analysis.json` and `blocks.csv` describe the original data; `stages-analysis-34151392676.json` locates diagnostic outliers. Relative deltas for every protected metric are in `instrumented/loaded-comparison.json`. The analysis scripts read the immutable artifact layout under the task workspace; the archived input subdirectories correspond to the original and instrumented run IDs above.
