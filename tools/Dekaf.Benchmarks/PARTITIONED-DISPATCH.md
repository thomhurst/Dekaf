# Partitioned dispatch allocation coverage

These fixtures separate the operations involved in #3078. They provide a durable comparison surface for scheduler changes, not a performance acceptance verdict.

| Fixture | Measured work | Unit |
|---|---|---|
| `PartitionedDispatchBenchmarks` | Production partition/key record/batch processors, input queue, completion tracking, and bounded partition lifetime | One of 128 records |
| `PartitionedOffsetTrackingBenchmarks` | Partition creation alone, then creation plus ordered or out-of-order `MarkProcessed` calls | One of 128 records; creation row is an amortized control |
| Existing `PartitionedStorageLifetimeBenchmarks.QueueAndComplete` | Reused queue transfer with borrowed fetch storage, without handlers or completion tracking | One of 1,024 records |
| Existing `PartitionedStorageLifetimeBenchmarks.QueueAndCompleteConcurrent` | Same storage transfer with a dedicated reader thread | One of 1,024 records |

Dispatch cases cover partition records, partition batches, repeated-key records/batches, and distinct-key records/batches. Keys are integers so equality is already correct on the baseline; binary comparison costs belong to #3048. Each case runs with synchronous handlers and with its first handler suspended by a `TaskCompletionSource`. One concurrent handler makes the blocked-first-handler shape deterministic: key dispatch can queue all 128 records before the gate opens. Synchronous repeated keys exercise idle-lane removal and recreation instead of coalescing queued work. Handler batches contain at most 16 records.

The fixture binds the real private `CreateRecordProcessor`/`CreateBatchProcessor` factories once in `GlobalSetup`. Reflection, input deserialization, and validation-array allocation are outside measurement. No production dispatch implementation is copied into the fixture. A factory signature change must update the binding; it must not silently substitute a benchmark-only implementation.

Each measured invocation creates a bounded partition lane, enqueues the prepared records, completes its writer, invokes the production processor, and waits for its completion. It bypasses the runtime's outer broker loop and `PartitionLane.Start` task wrapper. The handler checks duplicate delivery and per-key order; completion checks all 128 records, an empty queue, next offset 128, and leader epoch 7. A suspended first handler must actually suspend processing. All work completes before another invocation starts; the completion wait has a 30-second deadline.

The reported allocations include lane/channel/collection construction, production handler contexts and tracking, and benchmark coordination. Suspended cases include one completion source, asynchronous continuation/wait costs, and the benchmark's own async coordinator per 128 records. Validation arrays are reused, and batch observation uses indexed access to avoid adding an interface-enumerator allocation. Inputs have no borrowed storage; use the existing storage fixture to measure that separately. These are bounded partition lifetimes, not a claim about a warmed, indefinitely running dispatcher.

The offset fixture deliberately constructs a fresh partition for each invocation so resetting state cannot bypass production invariants. Its out-of-order case completes offset 0, then 127 down to 1, leaving the low gap open while the real tracking collections grow. Construction is explicit in all three rows. Do not subtract unrelated timing means or treat one allocation difference as exhaustive stack attribution. Use allocation tracing when the source and component comparisons do not identify a cost.

## Run and compare

Run from an isolated worktree. No Kafka broker is required. Use a unique artifacts directory for each run:

```powershell
dotnet build tools/Dekaf.Benchmarks -c Release
dotnet run --project tools/Dekaf.Benchmarks -c Release --no-build -- --filter '*PartitionedDispatchBenchmarks*' '*PartitionedOffsetTrackingBenchmarks*' '*PartitionedStorageLifetimeBenchmarks.QueueAndComplete*' --job Dry --artifacts .artifacts/partitioned-dry
dotnet run --project tools/Dekaf.Benchmarks -c Release --no-build -- --filter '*PartitionedDispatchBenchmarks*' '*PartitionedOffsetTrackingBenchmarks*' '*PartitionedStorageLifetimeBenchmarks.QueueAndComplete*' --job Short --exporters fulljson --artifacts .artifacts/partitioned-short
```

The filters select 17 cases: 12 dispatch, three tracking, and two existing transfer cases. Dry validates execution only. Short results support initial allocation diagnosis; use the default job and appropriate controls for acceptance-quality timing.

For a product comparison, build the identical fixture in separate worktrees at the immediately preceding exact product SHA and candidate SHA. Record both SHAs, the fixture and loaded `Dekaf.dll` hashes, SDK/runtime, machine, job parameters, and all reports. Preserve the same processor binding and inputs; if an internal signature requires an adapter, record and inspect that difference. Compare matching cases against the last accepted baseline and repeated unchanged controls on a quiet host. Shared-host measurements cannot establish timing equivalence.

Report per-record allocations and explicit amortized lifetime costs together. This suite does not establish application p50/p99/max latency, CPU per message, broker throughput, sustained stability, handler concurrency above one, cancellation, rebalance correctness, or offset-gap correctness across missing Kafka records. The production optimization in #3107 must supply those correctness tests and protected-metric evidence in addition to these benchmarks. A zero-byte transfer row never establishes zero-byte whole dispatch.
