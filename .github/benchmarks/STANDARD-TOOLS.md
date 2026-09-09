# Standard performance tooling

Use BenchmarkDotNet for microbenchmark execution, statistics, allocation reports
and allocation diagnostics. Use `dotnet-counters` and `dotnet-trace` for manual
investigations in separate runs. Prefer their original exports and standard
viewers to custom listeners, samplers, trace parsers or replacement statistics.

Keep custom code for Kafka workloads, correctness assertions, delivery/processing
boundaries and thin exact-revision A1/B/A2 orchestration. Runtime tools cannot infer
which Kafka messages completed or measure their end-to-end latency automatically.
Use ordinary project references and let MSBuild/BDN resolve and build dependencies.
Do not introduce DLL swapping, generated project templates or binary-rebinding
scripts where a normal project reference works.

## Shared micro suite

The runner uses repository-pinned BenchmarkDotNet **0.15.8**, the SDK in
`global.json`, and Release `net10.0`. Settings apply identically to A1, B and A2.

| Setting | Configuration and reason |
| --- | --- |
| Toolchain | Default out-of-process: BDN generates/builds a harness and launches a fresh child for each case. No `InProcessEmitToolchain`. |
| Product references | Copy the same checked-in benchmark project into each pinned product checkout. Its normal `ProjectReference` targets that checkout's source. Build and dry-validate both before timing. BDN builds its generated harness before each phase's warmup, with build-server reuse disabled. A2 uses the unchanged A checkout. |
| Runtime | Tiered compilation, Dynamic PGO and ReadyToRun enabled. Default BDN workstation/concurrent GC. This is not server-GC stress acceptance. |
| Strategy | Default throughput strategy and pilot calibration, configured with standard CLI options. No hand-written timing loop, custom toolchain, or fixed invocation count. |
| Warmup | 50 workload iterations targeting 1 second each; share parsing retains its declared 130 iterations. Verify actual elapsed workload warmup from full JSON: at least 20 seconds, or 120 seconds for share parsing. Iteration time is a pilot target, not a minimum guarantee. |
| Sampling | One launch per case in each phase, 25 measured iterations. Keep the same configuration across controls; inspect precision rather than adding runs until green. |
| Outliers | `DontRemove`; retain every measured sample and maximum. |
| Allocations | Existing `[MemoryDiagnoser]` attributes. Report bytes per benchmark operation; identify batch size before deriving per-message costs. |
| Output | BDN full JSON, standard Markdown/CSV, original logs and generated executables. The workflow checks completeness, matching cases and elapsed warmup with `jq`; it shows mean-time deltas and allocations without reimplementing statistics or generating a product verdict. |
| Diagnostics | No runtime/JIT loggers or tracing in the benchmark runner. JIT is not an acceptance metric. Investigate it manually, separately from benchmark timing. |

Manual profiling results do not replace the plain A1/B/A2 triplet.
When investigating a benchmark, attach to the workload child process; the BDN
controller runs in a different process.

The timing command is ordinary BenchmarkDotNet CLI:

```text
dotnet run --project Dekaf.Benchmarks.csproj -c Release --no-build -- --filter FILTER --warmupCount 50 --iterationCount 25 --iterationTime 1000 --launchCount 1 --outliers DontRemove --artifacts RESULTS
```

Run it from the copied `.performance` project directory. The workflow sets
`ABA_PR` to select fixture source and `ABA_ROLE` to retain the documented baseline
API/behavior adaptations. It pins SDK, fixture package versions and runtime
environment equally across phases. Process affinity uses the normal runner defaults.

The old #3109 `[IterationSetup]` shutdown fixture is excluded from the generic
steady-state micro suite. BDN runs one invocation per iteration for that fixture;
50 short operations do not establish 20 seconds of workload warmup. Use the
dedicated lifecycle workload and its completion/correctness boundaries.

Neither a minimum warmup duration nor BDN's iteration statistics prove absence
of startup transitions or provide per-message p99, CPU per message or sustained
stability. Use throughput and latency trends to assess warmup, and collect the
CPU, allocation and stability evidence appropriate to the workload. A compilation
event is not a regression. Missing applicable performance evidence remains
INCONCLUSIVE; missing JIT logs does not.

## Loaded workload diagnostics

Reuse the pinned tools in `.config/stress-diagnostics/dotnet-tools.json` and
`tools/profile-stress-test.sh`. They already collect counters/traces and produce
standard `dotnet-trace report` and Speedscope output. Do not create another
profiler or implement allocation accounting by summing sampled allocation events.
Sampled stacks identify where work occurs; their counts are not process CPU time.

For a running workload, standard commands are:

```text
dotnet-counters collect --process-id PID --refresh-interval 1 --counters System.Runtime --format json --output counters.json
dotnet-trace collect --process-id PID --duration 00:00:00:30 --output diagnostic.nettrace
dotnet-trace report diagnostic.nettrace topN -n 50
dotnet-trace convert diagnostic.nettrace --format Speedscope
```

Run diagnostics manually, separately from acceptance timing. Inspect GC/JIT events
with a standard trace viewer when investigating a measured performance problem.
All collectors have overhead; identical instrumentation does not prove equal bias.

## Remaining migration

Custom BDN runtime loggers, JIT listeners and phase diagnosers are removed from
the shared, pool, outbox, dispatch and administrative fixtures. The custom BDN
engine primer and background host-resource sampler are also removed. Loaded workloads
retain completion/latency recorders and CPU/allocation/stability counters. Older
specialized runners still have project/build orchestration to simplify. Preserve
historical evidence at its recorded SHA; never rewrite old reports to the new
instrumentation or treat old and new measurements as interchangeable.

## References

- [BDN 0.15.8 runner/build pipeline](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Running/BenchmarkRunnerClean.cs)
- [BDN 0.15.8 job settings](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Jobs/JobExtensions.cs)
- [BenchmarkDotNet diagnosers](https://benchmarkdotnet.org/articles/configs/diagnosers.html)
- [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
