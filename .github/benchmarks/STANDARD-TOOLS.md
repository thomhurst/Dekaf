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

## Automatic performance gate

[performance-gate.yml](../workflows/performance-gate.yml) runs on every pull
request that changes `src/` or the build inputs. It pins A as the merge base
with `main` and B as the exact PR head, selects steady-state classes from
`tools/Dekaf.Benchmarks` by changed path with
[performance_gate.py](../scripts/performance_gate.py), builds each revision's
own benchmark project, dry-validates the selected cases on both, then measures
A1, B and A2 sequentially on one `ubuntu-latest` VM with the shared micro
settings below and screens them with the same `jq` comparison. Areas without a
steady-state fixture (compression, outbox, JSON serialization, extensions) are
reported as not applicable rather than measured with a single-invocation
fixture. Selections are capped at 48 cases; `workflow_dispatch` accepts explicit
`filters`, `base_sha` and `head_sha` for narrower or repeated runs. A PR that
changes `tools/Dekaf.Benchmarks` is flagged because A and B then measure
different fixture sources; cases present in only one revision are listed, never
compared.

## Shared micro suite

The runner uses repository-pinned BenchmarkDotNet **0.15.8**, the SDK in
`global.json`, and Release `net10.0`. Settings apply identically to A1, B and A2.

| Setting | Configuration and reason |
| --- | --- |
| Toolchain | Default out-of-process: BDN generates/builds a harness and launches a fresh child for each case. No `InProcessEmitToolchain`. |
| Product references | Copy the same checked-in benchmark project into each pinned product checkout. Its normal `ProjectReference` targets that checkout's source. Build and dry-validate both before timing. BDN builds its generated harness before each phase's warmup, with build-server reuse disabled. A2 uses the unchanged A checkout. |
| Runtime | Tiered compilation, Dynamic PGO and ReadyToRun enabled. Default BDN workstation/concurrent GC. This is not server-GC stress acceptance. |
| Strategy | Default throughput strategy and pilot calibration, configured with standard CLI options. No hand-written timing loop, custom toolchain, or fixed invocation count. |
| Warmup | 50 workload iterations targeting 1 second each; share parsing retains its declared 130 iterations. Verify actual elapsed workload warmup from full JSON: at least 20 seconds, or 120 seconds for share parsing. Iteration time is a pilot target, not a minimum guarantee: tiered code can speed up after the pilot, so `performance_gate.py validate` reports any per-case shortfall by phase and the screen marks that case `INCONCLUSIVE` instead of failing the whole run. |
| Sampling | One launch per case in each phase, 25 measured iterations. Keep the same configuration across controls; inspect precision rather than adding runs until green. |
| Outliers | `DontRemove`; retain every measured sample and maximum. |
| Allocations | Existing `[MemoryDiagnoser]` attributes. Report bytes per benchmark operation; identify batch size before deriving per-message costs. |
| Output | BDN full JSON, standard Markdown/CSV, original logs and generated executables. `performance_gate.py validate` checks completeness (case count, 25 measured iterations, an Allocated Memory metric per case) and names every failing case; it merges each phase into a sorted case array that [`compare_bdn_reports.jq`](../scripts/compare_bdn_reports.jq) pairs into `comparison.json` and `comparison.md`. It does not reimplement statistics. |
| Screen | 5% tolerance on BDN mean time against both controls, an 8 B/op allocation floor (`alloc_floor`) and a 20 s per-phase warmup floor (`min_warmup`). Per case, in order: `INCONCLUSIVE` when a phase warmed up below the floor (the note names the phase); `REGRESSION` (slower than both controls beyond tolerance, or allocating more than both beyond the floor); `INCONCLUSIVE` (slower or allocating more than one control only while lying outside the band between the two controls); `IMPROVEMENT`; `PASS`, which includes a candidate bracketed by drifting controls (identical code produced both ends of that band, so the note records the drift instead of an `INCONCLUSIVE`). Sub-floor allocation differences are noted, never decisive: the smallest managed object is 24 B, so they are cold-path or amortized noise. Control drift remains a diagnostic note and never overrides a decisive result against both controls. A1 and A2 must report the same case set; cases present only in B or only in A are listed and not compared. `REGRESSION` fails the job; `INCONCLUSIVE` warns and permits one exact repeat after reviewing the affected uncertainty. The screen is the acceptance result for changes within the micro scope in [AGENTS.md](../../AGENTS.md). |
| Build isolation | The copied directory ships `Directory.Build.props`/`.targets` stop-files, so the product checkout's repository build settings (warnings-as-errors, analyzers, packaging) do not apply to the fixture host or BDN's generated project. |
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
