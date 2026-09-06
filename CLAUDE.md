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

## Build and test

`global.json` is the source of truth for SDK selection and Microsoft.Testing.Platform (MTP). Install the .NET 8 runtime when running the net8.0 tests.

```powershell
dotnet build
dotnet test --project tests/Dekaf.Tests.Unit --configuration Release --framework net10.0
dotnet test --project tests/Dekaf.Tests.Integration --configuration Release --framework net10.0
dotnet run --project tools/Dekaf.Benchmarks --configuration Release -- --filter "*Memory*"
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
- Final duration-based producer acceptance requires `baseline_sha` set to the immediately preceding exact product SHA. This runs baseline → candidate → baseline on one VM and triples measured duration. Accept only the formal Pareto `PASS`; reject `REGRESSION`.
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
