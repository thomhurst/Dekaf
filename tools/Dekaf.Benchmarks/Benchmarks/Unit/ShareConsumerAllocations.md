# Share-consumer allocation coverage

These fixtures measure the production parser and acknowledgement tracker without Kafka. They provide the baseline for issues #3032 and #3103; they do not establish that share consumption is allocation-free.

## Measured boundaries

Every operation is **one partition batch**, containing `RecordCount` records. `Allocated` is bytes per batch, not bytes per message. No `OperationsPerInvoke` division hides list growth, per-record objects, header copies, or acknowledgement storage.

| Method | Included | Excluded |
| --- | --- | --- |
| `ParseSynchronousBatch` | The actual `KafkaShareConsumer<int, int>.ParsePartitionRecords`, deserialization, result list, record/header objects, pooled batch disposal, and result traversal | Fixture generation, consumer construction, network requests, polling state machine, acknowledgement tracking |
| `ParseWarmPreparedBatch` | The actual preparation-aware parser with a fresh cursor and empty result list, warm key/value preparers, batch disposal, and result traversal | As above; cold preparation and its durable input copies |
| `TraverseRetainedBatch` | Synchronous traversal of already parsed public results, including keys, values, offsets, delivery counts, timestamps, and header data | Parsing and result creation |
| `AccumulateFreshBatch` | A fresh production `AcknowledgementTracker`, storage growth, and delivery/acknowledgement of distinct offsets | Parsing, wire materialization, network requests, consumer renewal replay state |
| `MaterializeWireBatch` | `Flush` on a populated production tracker, including replacement pending dictionary, wire arrays, sorting, and merging | Accumulation, which runs in iteration setup |

The parsing fixtures use the library's allocation-free Int32 deserializer. The preparation-aware wrapper always succeeds synchronously and throws if cold preparation is unexpectedly requested. Protocol input comes from `RecordBatch.Write` and `Serializers.Int32`; no handwritten Kafka bytes are involved. The synchronous parser delegate is bound once in setup, without `MethodInfo.Invoke`, boxing, or argument arrays in the measured path.

Two scales, 64 and 1024 records, reveal fixed batch costs and costs that grow with delivery count. Header cases use either no headers or two headers, including a null value. Setup validates both parser outputs after their internal batch disposal: record count, payloads, topic/partition, offset, timestamp, delivery count, header keys, values, and nullability.

Implicit acknowledgements track each delivered offset as `PollAsync` does. Explicit mode acknowledges distinct offsets with alternating Accept, Release, and Reject, without implicit delivery tracking. Every accumulation invocation starts with empty storage; repeatedly overwriting a warmed dictionary key would miss the allocation defect. Setup checks every resulting wire disposition and verifies that flush clears pending state.

`MaterializeWireBatch` consumes its prepared state, so it uses one invocation per iteration. This gives a separate allocation boundary, but its short iteration timings are sensitive to scheduling and are **not performance acceptance evidence**. BDN reports the expected minimum-iteration-time warning. Use an end-to-end batch/commit benchmark for timing acceptance.

## Run

From the repository root in an isolated worktree:

```powershell
dotnet run --project tools/Dekaf.Benchmarks --configuration Release --framework net10.0 -- --filter '*ShareConsumerParsingBenchmarks*' '*ShareAcknowledgementAllocationBenchmarks*' --job Dry --artifacts .artifacts/share-dry > share-dry.log 2>&1

dotnet run --no-build --project tools/Dekaf.Benchmarks --configuration Release --framework net10.0 -- --filter '*ShareConsumerParsingBenchmarks.ParseSynchronousBatch*RecordCount: 64, HeaderCount: 0*' --job Default --artifacts .artifacts/share-representative > share-representative.log 2>&1

dotnet run --no-build --project tools/Dekaf.Benchmarks --configuration Release --framework net10.0 -- --filter '*ShareConsumerParsingBenchmarks*' '*ShareAcknowledgementAllocationBenchmarks*' --job Short --artifacts .artifacts/share-short > share-short.log 2>&1
```

The full matrix contains 20 cases. Dry validates setup and execution only. Use Short for allocation investigation and default/longer measurement jobs for comparative timing after the experiment is stable. Reports are under each artifacts directory's `results` subdirectory. Retain the exact product SHA, runtime, machine, job, and raw reports with every comparison.

## Acceptance limits

This coverage isolates existing parser and tracker costs. It does not measure `PollAsync`'s asynchronous delivery overhead, cold schema preparation, Renew replay ownership, partial enumeration, retries, broker acknowledgement/redelivery behavior, CPU per message, or latency percentiles. Those remain implementation and acceptance requirements of #3103. Header materialization costs inside protocol decoding and ArrayPool misses remain included; pooled storage must not be assumed to eliminate allocations without measurement.

Compare a saved exact baseline build against the candidate with identical inputs and settings. Preserve the compatibility API's behavior and performance. Extend these fixtures for the new batch API and add its per-record traversal/acknowledgement boundary explicitly: a combined batch average cannot prove zero per-message infrastructure allocation. Apply the repository's full Pareto and stress gates before accepting a product change.

## Baseline recorded 2026-09-07

Product source: `aefb825462cd6f6d6e956fca3374a12d98a0e128`. BenchmarkDotNet 0.15.8; .NET SDK 10.0.400; .NET 10.0.11, x64 RyuJIT, concurrent workstation GC; Windows 11 25H2; Intel Core i7-12700K (12 physical / 20 logical cores). No product source changed for these measurements.

All 20 Dry cases and all 20 ShortRun cases completed with setup validation passing. ShortRun used one launch, three warmups, and three measured iterations. The representative default job (`ParseSynchronousBatch`, 64 records, no headers) measured **6.107 us ± 0.1185 us**, 5.73 KB allocated per batch (BDN's Error is the half-width of its 99.9% confidence interval). The ShortRun allocation results below are exact bytes per batch:

| Records | Headers | Synchronous parser | Warm preparation-aware parser | Retained traversal |
| ---: | ---: | ---: | ---: | ---: |
| 64 | 0 | 5,872 B | 5,752 B | 0 B |
| 64 | 2 | 12,528 B | 12,408 B | 0 B |
| 1024 | 0 | 90,448 B | 90,328 B | 0 B |
| 1024 | 2 | 451,256 B | 451,136 B | 0 B |

| Records | Acknowledgement mode | Fresh accumulation | Wire materialization |
| ---: | --- | ---: | ---: |
| 64 | Implicit | 448 B | 560 B |
| 64 | Explicit | 4,952 B | 3,040 B |
| 1024 | Implicit | 448 B | 1,520 B |
| 1024 | Explicit | 102,544 B | 36,832 B |

The fixed 448 B implicit accumulation cost is batch storage for one contiguous offset range. Explicit accumulation grows with distinct offsets; it must not be described as a fixed batch cost. Likewise, the parser returns a newly allocated public record object per message and copies headers per message. At 64 records, the two-header case adds exactly 104 B per record. The 1024-record header case has additional costs beyond that copy; these measurements do not attribute all of them. ArrayPool retention/misses and protocol header materialization need separate investigation before claiming zero allocations.

These numbers reproduce the existing defects and provide a baseline, **not a product performance PASS**. ShortRun timing intervals are broad, wire timings use a single invocation, and no CPU, latency, or stability acceptance was attempted. The raw BDN Markdown reports, including all timing/error/GC columns, are attached to the coverage PR alongside the exact product SHA. Re-measure baseline and candidate under the same conditions for #3103.
