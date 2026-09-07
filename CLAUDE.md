# Dekaf Development Guide

Dekaf is a high-performance, pure C# Apache Kafka client. Performance is the product: improve throughput, latency, CPU, and allocations together.

## Performance requirements

- Changes to `src/` require benchmark or stress evidence appropriate to the affected path. Hot-path changes require before/after `[MemoryDiagnoser]` results in the PR, with `0 B` per message. Identify known cold-path noise explicitly (for example, the drainer-behind path in AccumulatorAppend benchmarks).
- Protected metrics are throughput, p50/p99/max latency, CPU per message, allocations, and stability. Do not trade one for another without explicit maintainer approval, including for refactoring or review feedback.
- Hot paths include serialization, batch append/drain, channel writes, per-message produce/consume, and receive/parse loops. Keep per-message fast paths synchronous with `ValueTask`; amortize necessary work per batch, connection, or epoch.
- Hot paths must avoid LINQ, capturing lambdas, uncached delegates, boxing, string formatting, allocating collection conversions/iterators, interface-enumerator allocations, per-message async state machines, `Task.Run`, thread-pool hops, locks, exceptions as control flow, and O(n) scans or cleanup loops. Prefer spans, pooled buffers, static callbacks with explicit state, channels, and `Interlocked` as appropriate.
- Distinguish per-message allocations from acceptable amortized per-batch costs. Remove completed operations from tracking collections and coordinate disposal of in-flight work, observing failures.

## Library conventions and contracts

- Use nullable reference types, init-only options, fluent builders, and existing modern C# conventions. Public APIs expose interfaces; implementations are internal or sealed.
- All awaits in `src/` use `ConfigureAwait(false)`; tests do not require it. Do not block on tasks with `.Result` or `.Wait()`.
- Kafka-specific exceptions derive from `KafkaException`; use `IsRetriable` for retry decisions.
- Producer `BufferMemory` limits apply to every append path, including the arena fast path. Exhaustion backpressures `ProduceAsync` until space is available.
- `ProduceAsync` cancellation before append prevents delivery, including during metadata lookup, channel writes, and memory reservation. After append, cancellation stops the caller's wait while delivery continues.
- `FlushAsync` cancellation stops waiting while batches continue sending. `FireAsync` has no cancellation-token overload; use `FlushAsync(cancellationToken)` for cancellable delivery waiting.

## Benchmark execution

- Run PR performance acceptance on GitHub-hosted Actions with `runs-on: ubuntu-latest`. Measure A1 (baseline), B (candidate), then A2 (the same baseline) sequentially in one job on one runner VM. Separate jobs or historical baseline artifacts do not substitute for these controls. Local measurements are diagnostic only. Hosted runners can still be noisy; assess control drift.
- Fetch fresh `main`, pin its exact SHA as A, and verify the candidate contains it before measuring. Rebase an outdated PR before acceptance measurements. Pin the resulting exact PR head as B; do not resolve moving branch names between phases. Use the same pinned main for a comparison campaign and record any later main movement. Product changes or rebases require new evidence for the new head. Compare an optimization against its immediately preceding product SHA as well when needed to isolate its effect; this does not replace the fresh-main comparison.
- Use identical benchmark fixtures, inputs, SDK/runtime, Release configuration, warmup, sampling, profiling, and load settings across all phases. Build both revisions and validate their fixtures before timing; stop build servers and unrelated work. Reset workload, broker, and application state consistently between phases. Record fixture adaptations and assert correctness when baseline behavior or APIs differ. Do not compare different work as equivalent performance.
- For steady-state acceptance, exercise each measured workload/configuration in each fresh process for at least 20 seconds before collecting samples. Predeclare the same warmup duration for A1, B, and A2; extend it when startup activity persists. A small fixed iteration count (such as 100) is insufficient evidence of warmup. For BenchmarkDotNet, configure and verify elapsed workload warmup rather than assuming an iteration count or preset is sufficient. Match runtime/JIT settings to the intended workload and record them.
- Record warmup duration and completed operations, plus time-series evidence of JIT compilation activity, thread-pool growth, CPU/latency trends, and GC activity. Twenty seconds is a minimum, not proof of steady state. If startup transitions still affect measured samples, report `INCONCLUSIVE` and design a longer-warmup experiment for all phases, subject to the repeat limits below. Preserve all measured samples, including maxima; do not retrospectively trim startup samples to pass a gate. Measure intentional cold-start behavior separately and label it explicitly.
- The [PR #3109 latency investigation](https://github.com/thomhurst/Dekaf/blob/d99db3b1dfcc25d614b7ea67f7130e2864ea15f4/docs/performance-evidence/3109-shutdown-latency-2026-09-07/README.md) found that 100 warmup iterations left JIT-related 10–15 ms shutdown spikes. A fixed 20-second warmup removed all stops above 1 ms in the measured A–B–A sample, but residual maximum-latency and CPU differences still left acceptance `INCONCLUSIVE`. Warmup removes measurement bias; it does not waive protected metrics.
- Define the affected workloads, protected metrics, measurement boundaries, and acceptance tolerances before running. Cover the normal hot path and changed error, recovery, or shutdown paths. Choose representative message sizes, batching, partition counts, concurrency, and backpressure. Use focused microbenchmarks to isolate costs and representative loaded runs to validate system effects; neither substitutes for missing evidence from the other.
- Measure completed-message throughput; actual per-message p50/p99/max latency; process CPU time per completed message; allocations per message and per batch; and stability over time. State latency start/end events (including queueing and delivery/processing completion where applicable), offered load, completion counts, duration, and CPU/allocation scope. BenchmarkDotNet iteration percentiles are not per-message latency, wall-clock time is not CPU time, and enqueue rate is not completed throughput. Capture failures, timeouts, backlog, GC activity, and managed heap/RSS trends so dropped work, accumulating queues, or memory growth cannot masquerade as a win. Keep broker CPU separate from client CPU. Use `[MemoryDiagnoser]` for hot-path allocation evidence, with `0 B` per message; identify amortized per-batch costs and cold-path noise separately.
- Match evidence to the change: sustained producer/consumer changes need end-to-end CPU, latency, and stability evidence; lifecycle changes need loaded completion timing, correctness, committed progress, and checks for leftover work. Explicitly justify any metric that is not applicable. An applicable metric that was not measured is missing evidence, not a pass. Do not infer long-run stability from a short microbenchmark.
- Report absolute metrics for A1, B, and A2, candidate deltas against each control, A1/A2 drift, and uncertainty/sample counts. Preserve raw results and time series. Use `PASS` only when correctness holds and all applicable protected metrics meet the declared acceptance criteria against both controls. Report `REGRESSION` for a confirmed protected-metric loss; report `INCONCLUSIVE` for material control drift, insufficient precision, missing evidence, or an invalid experiment. Overlapping intervals alone do not prove equivalence. Do not average away drift, offset a regression with an unrelated gain, or widen tolerances after seeing results. Apply the repeat limits below. Record explicit maintainer approval for any tradeoff without relabeling the measured verdict.
- Read [the benchmark workflow](.github/workflows/benchmarks.yml) and [stress workflow](.github/workflows/stress-tests.yml) before dispatching. If a workflow lacks same-VM A–B–A, `ubuntu-latest`, or required metrics, extend it or use a dedicated comparison workflow before claiming acceptance. Existing paid stress lanes do not satisfy the Ubuntu requirement by default. Preserve scheduled stress coverage and paid-run limits. Publish exact product and harness SHAs, runner image/hardware, SDK/runtime, workload/settings, run URL, raw artifacts, metric results, and decision rationale in the PR. Update the gate on the measured exact head only; successful workflow execution is not a performance `PASS`.
- The local Redis `performance` lock is no longer required for benchmarks, profiling, stress runs, builds, tests, restores, or other heavy work. Keep PR/issue ownership locks and the stress acceptance rules below.

## Build and test

`global.json` is the source of truth for SDK selection and Microsoft.Testing.Platform (MTP). Install the .NET 8 runtime when running the net8.0 tests.

```powershell
dotnet build
dotnet test --project tests/Dekaf.Tests.Unit --configuration Release --framework net10.0
dotnet test --project tests/Dekaf.Tests.Integration --configuration Release --framework net10.0
```

- TUnit uses `--treenode-filter "/*/*/ClassName/TestName"` with `/<Assembly>/<Namespace>/<Class>/<Test>` segments. With MTP, use `--project` and pass test options directly, without an extra `--` or VSTest's `--filter`.
- Wildcards and segment-local OR are supported: `/*/*/(ClassA|ClassB)/*`. Use separate commands for different path shapes. Built test executables also accept these options.
- Features require TUnit unit tests; client behavior changes require integration tests; performance-critical changes require benchmarks.
- Integration tests require Docker/Testcontainers.Kafka. `KafkaIntegrationTest` uses `KAFKA_TEST_IMAGE_TAG`; consult its fixture and CI for the current default and release-gate version matrix. The full broker-version matrix must pass before NuGet publishing.
- Producer/consumer benchmarks require Kafka; memory/serialization benchmarks do not require Docker.
- Investigate intermittent test failures instead of rerunning CI to obtain green. Use deterministic synchronization for timing-dependent tests and library serializers for protocol fixtures.
- Review final changes for reuse, quality, and efficiency before opening a PR (`/simplify` when available). Preserve the performance requirements above.
- Create ready-for-review PRs unless the user explicitly requests a draft.

## Stress testing and performance acceptance

Read `.github/workflows/stress-tests.yml` for supported lanes and inputs before dispatching.

- Select one lane when it can validate the change. Manual runs default to Dekaf-only, `dispatch_shape=cheap` (one sample, no 3-connection control). Use `cheap-with-3conn` for that control or `lane-default` for the lane's full sampling shape.
- Use `duration_minutes=15` or less for routine validation. Reserve 30+ minutes for elapsed-time hypotheses such as leaks or late-run collapse. Duration applies per sample, not per workflow.
- Final duration-based producer acceptance requires `baseline_sha` set to the pinned fresh-main SHA described above. For incremental optimization experiments, use the immediately preceding exact product SHA and retain the fresh-main acceptance comparison. Run baseline → candidate → baseline on one `ubuntu-latest` VM; this triples measured duration. Accept only the formal Pareto `PASS`; reject `REGRESSION` unless the maintainer explicitly approves the recorded tradeoff. Missing required metrics remain `INCONCLUSIVE` even if a narrower comparator passes.
- The first `INCONCLUSIVE` permits one automatic exact repeat. After a second, synthesize all same-SHA paired evidence and identify control noise, improve the experiment, test a new causal candidate, or obtain maintainer direction. No further identical paid run without explicit maintainer approval; never average away uncertainty or relabel the verdict.
- Before accepting a candidate or funding its next stress gate, compare like-for-like against the immediately preceding exact-configuration run, last accepted baseline, and all within-run controls. Record SHA, run URL, lane, dispatch shape, duration, profiling mode, absolute protected metrics, deltas, and acceptance decision before another paid run or merge. Resolve ambiguous cross-run differences with controls, one repeat, or quantified historical variance.
- Scheduled runs (Sunday 02:00 UTC) provide paired Dekaf/Confluent baselines and publish docs/history. Manual `full_run=true` forces all 12 paid lanes and the paired publishing shape. Manual `lane=all` alone stays Dekaf-only and does not publish. Reuse scheduled Confluent results; recover a failed scheduled publish by rerunning that workflow or explicitly selecting `full_run`.

## Repository map

- `src/Dekaf/`: core client; `Protocol/` contains unsafe, allocation-sensitive reader/writer ref structs; `Networking/` uses pipelines and multiplexed connections; `Producer/`, `Consumer/`, and `Serialization/` implement the main paths.
- `src/Dekaf.Compression.*/`, `Dekaf.Serialization.Json/`, `Dekaf.Extensions.*/`, and `Dekaf.SchemaRegistry*/`: optional integrations.
- `tests/Dekaf.Tests.Unit/`, `tests/Dekaf.Tests.Integration/`: TUnit suites.
- `tools/Dekaf.Benchmarks/`, `tools/Dekaf.StressTests/`: performance validation.
- `tools/profile-stress-test.sh`, `tools/Dekaf.TraceAnalyzer/`: phased trace capture and analysis.

Keep agent instructions focused on non-obvious project requirements. Prefer links to maintained code or workflows over duplicated tutorials, examples, and inventories.
