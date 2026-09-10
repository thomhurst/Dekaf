# Dekaf Development Guide

Dekaf is a high-performance, pure C# Apache Kafka client. Performance is the product: improve throughput, latency, CPU, and allocations together.

## Performance requirements

- Protected metrics are throughput, p50/p99 latency, CPU per message, allocations per message, and stability. Do not trade one for another without explicit maintainer approval, including for refactoring or review feedback.
- Hot paths include serialization, batch append/drain, channel writes, per-message produce/consume, and receive/parse loops. Keep per-message fast paths synchronous with `ValueTask`, allocate 0 B per message, and amortize necessary work per batch, connection, or epoch. Say in the PR which costs are per message and which are amortized.
- Hot paths must avoid LINQ, capturing lambdas, uncached delegates, boxing, string formatting, allocating collection conversions/iterators, interface-enumerator allocations, per-message async state machines, `Task.Run`, thread-pool hops, locks, exceptions as control flow, and O(n) scans or cleanup loops. Prefer spans, pooled buffers, static callbacks with explicit state, channels, and `Interlocked`. Remove completed operations from tracking collections and coordinate disposal of in-flight work, observing failures.
- A hot-path change needs a steady-state `[MemoryDiagnoser]` fixture in `tools/Dekaf.Benchmarks/Benchmarks/Unit` that exercises the changed code. Add or extend the fixture in the same PR as the product change; the gate builds the PR's fixture source against both revisions, so nothing has to land on `main` first.

## Performance gate

Every PR that changes `src/`, `tools/Dekaf.Benchmarks/` or the build inputs runs the [performance gate](.github/workflows/performance-gate.yml). It selects benchmark classes with `.github/scripts/performance_gate.py` (four hot-path sentinels for any product change, a few classes per hot component directory, plus every fixture class the PR touches) and runs one `ubuntu-latest` job per class. Each job measures baseline, candidate, baseline (A1/B/A2) back to back on one VM and compares medians. A class finishes in about 10 to 25 minutes, or up to about 50 when a regression is re-measured.

- `REGRESSION` fails the check only when the candidate is slower than both controls beyond the tolerance for that case size (20% under 100 ns, 15% under 1 µs, 10% from 1 µs), or allocates at least 24 B/op and 1% more than both controls, and the loss reproduces on an immediate repeat of those cases. Everything else passes; control drift and one-sided differences are notes. When the PR fixtures do not compile against the baseline, cases from fixture files the PR changed are reported as `NOT COMPARED`, and a class the PR adds is measured on the candidate alone; run them locally against `main` if that comparison matters.
- Hosted runners drift 10% to 30% between processes on nanosecond cases even for identical binaries. A single delta inside the tolerance is noise, not a win or a loss. Local measurements are diagnostic only.
- On a red gate, open the job summary table and download the artifact (original BenchmarkDotNet exports and logs), then fix the product or state the tradeoff in the PR for maintainer approval. Do not rerun until green, widen tolerances, add PR-specific rules to `performance_gate.py`, or split fixtures into a separate PR. A `PASS` needs no rerun.
- Fixtures the gate runs must be steady-state: no `[IterationSetup]`/`[IterationCleanup]`, `InvocationCount` or cold-start strategies (those are skipped with a reason), and under about 16 expanded cases per class. Check a fixture locally before pushing:

  ```powershell
  dotnet run -c Release --project tools/Dekaf.Benchmarks -- --filter '*.Unit.MyBenchmarks.*' --job Short
  ```

- Microbenchmarks do not show loaded behaviour. Changes to send/receive loops, batching and linger, fetch scheduling, connection lifecycle, shutdown, or stress-lane defaults also need one stress lane run (below) with `baseline_sha` set; link the run in the PR.
- Do not commit benchmark outputs, logs, reports, traces or evidence Markdown. Job summaries and artifacts are the evidence store; PR comments link them. The repository artifact-policy check enforces this.

## Library conventions and contracts

- Use nullable reference types, init-only options, fluent builders, and existing modern C# conventions. Public APIs expose interfaces; implementations are internal or sealed.
- All awaits in `src/` use `ConfigureAwait(false)`; tests do not require it. Do not block on tasks with `.Result` or `.Wait()`.
- Kafka-specific exceptions derive from `KafkaException`; use `IsRetriable` for retry decisions.
- Producer `BufferMemory` limits apply to every append path, including the arena fast path. Exhaustion backpressures `ProduceAsync` until space is available.
- `ProduceAsync` cancellation before append prevents delivery, including during metadata lookup, channel writes, and memory reservation. After append, cancellation stops the caller's wait while delivery continues.
- `FlushAsync` cancellation stops waiting while batches continue sending. `FireAsync` has no cancellation-token overload; use `FlushAsync(cancellationToken)` for cancellable delivery waiting.

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
- Create ready-for-review PRs unless the user explicitly requests a draft. Before merging, squash to one commit, rebase onto fresh `origin/main`, and push with `--force-with-lease`.

## Stress testing (loaded evidence)

Read `.github/workflows/stress-tests.yml` for lanes and inputs before dispatching. Manual runs are Dekaf-only; there are no scheduled runs, so preserve paid-run limits.

- Pick one lane that exercises the change. Use `duration_minutes=15` or less; reserve 30+ minutes for elapsed-time hypotheses such as leaks or late-run collapse. Duration applies per sample.
- For acceptance set `baseline_sha` to the fresh-main SHA the PR contains. The workflow runs baseline → candidate → baseline on one `ubuntu-latest` VM; `stress_warmup.py` marks a run that never reaches steady state `INCONCLUSIVE`, and `stress_aba.py` gates throughput, p50/p95/p99, CPU/msg, allocations/msg and stability against both controls with maximum latency informational and latency rows n/a for consumer lanes.
- Accept `PASS`. A `REGRESSION` needs a product fix or an explicitly recorded maintainer tradeoff. One exact repeat is allowed after an `INCONCLUSIVE`; after a second, change the experiment or ask the maintainer instead of rerunning.
- Record run URL, lane, dispatch shape, duration and verdict in the PR before merging or dispatching another paid run. `full_run=true` dispatched from `main` is the only run that publishes paired Dekaf/Confluent baselines and history.

## Repository map

- `src/Dekaf/`: core client; `Protocol/` contains unsafe, allocation-sensitive reader/writer ref structs; `Networking/` uses pipelines and multiplexed connections; `Producer/`, `Consumer/`, and `Serialization/` implement the main paths.
- `src/Dekaf.Compression.*/`, `Dekaf.Serialization.Json/`, `Dekaf.Extensions.*/`, and `Dekaf.SchemaRegistry*/`: optional integrations.
- `tests/Dekaf.Tests.Unit/`, `tests/Dekaf.Tests.Integration/`: TUnit suites.
- `tools/Dekaf.Benchmarks/`, `tools/Dekaf.StressTests/`: performance validation.
- `tools/profile-stress-test.sh`, `tools/Dekaf.TraceAnalyzer/`: phased trace capture and analysis.

Keep agent instructions focused on non-obvious project requirements. Prefer links to maintained code or workflows over duplicated tutorials, examples, and inventories.
