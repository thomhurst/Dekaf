# Hosted benchmark runs

The `Benchmarks` workflow has two paths:

| Trigger | Work | Publication |
|---|---|---|
| Daily schedule, or manual `benchmark_filter` equal to `*` or empty | Existing unit shards, Linux client cases, Windows native-memory cases, and summary | History/docs on main |
| Manual nonempty filter other than `*` | One Linux benchmark runner executing that BDN glob | Artifact only |

A filtered run builds the benchmark project and passes the filter as one quoted argument. It does not split the value into shell words or interpret it as a command. Match the whole benchmark name with wildcards, including a leading `*` when omitting the namespace. Whitespace-only input is a filter that matches nothing, not a request to launch the full suite.

For example, run one cache lookup case from a branch containing the desired source:

```powershell
gh workflow run benchmarks.yml --ref my-branch -f 'benchmark_filter=*SchemaResolutionFiniteTtlBenchmarks<Int32>.LookupHit*'
```

Download `benchmark-results-filtered` for the complete BDN log, full JSON measurements, and Markdown reports. A filter matching no cases fails. The benchmark executable returns a failure exit code for critical validation errors or unsuccessful BDN reports, including a later launch failing after earlier measurements succeeded. The workflow also rejects missing or invalid measurement reports. The terminal gate requires the selected runner to succeed and all unselected jobs to remain skipped; partial results never enter full-suite history/docs.

Filtered runs use the normal Linux managed-memory benchmark configuration. They do not reproduce the full pipeline's separate Windows native-memory profiler or its explicitly pinned external Kafka broker. Client fixtures can start their own broker through the existing [KafkaTestEnvironment](Infrastructure/KafkaTestEnvironment.cs); network-free unit cases need none. Generated benchmark builds have a 600-second timeout because the project's dependency graph can exceed BenchmarkDotNet's 120-second default on hosted runners. The runner has a 90-minute ceiling, so prefer a small class or method filter.

Runner isolation removes interference from local builds, but a hosted benchmark report alone is not a protected-metric acceptance result. Product acceptance still needs exact baseline/candidate inputs, matching configuration, unchanged controls, allocation evidence and all applicable latency/CPU/stability checks. Paid stress acceptance follows [the stress workflow](../../.github/workflows/stress-tests.yml) and repository guidance separately.
