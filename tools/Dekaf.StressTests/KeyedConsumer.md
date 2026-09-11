# Key-ordered consumer replay

`consumer-keyed` exercises the public `RunPartitionedAsync` record-handler API with `Ordering = Key`. The manual `consumer-keyed-1b` workflow lane is Dekaf-only and excluded from `lane=all`, `full_run=true`, and the runner's `--scenario all`. It does not change the existing 12-job full matrix or runtime defaults.

Choose one shape for the hypothesis being tested:

| `keyed_shape` / `--keyed-shape` | Deserialized key | Workload |
| --- | --- | --- |
| `scalar` (default) | `int` | Ordinary scalar keys |
| `binary` | `byte[16]` | Repeated equal content in independently deserialized buffers |
| `large-distinct` | `byte[4096]` | Distinct IDs in the first sampled window |
| `large-colliding` | `byte[4096]` | Distinct IDs outside all four sampled windows; equal sampled hashes, unequal full content |

The workflow fixes six partitions, 32 keys per partition, 32,768 records per partition, four handlers per partition, and a 256-record buffer per partition. Values contain their partition-relative sequence in the first eight bytes. The key ID is the offset modulo 32. Seeding uses explicit partitions and waits for delivery before measurement; every partition's end offset must equal the declared seed size. The largest default shape with 1,000-byte values seeds approximately 956 MiB of key/value bytes, plus Kafka framing. Each process gets a fresh topic and the exact-SHA path gets a fresh broker.

The consumer uses explicit assignment, the existing high-throughput preset, and manual commits without broker offset commits. This isolates dispatch and replay rather than group rebalancing. Every pass creates a new partitioned runtime and checks every record's partition, key, offset, payload sequence and per-key handler exclusion. Different keys and partitions can finish concurrently. A pass stops only after every seeded record completes. The runtime then drains and returns before the harness seeks. A missing tail, duplicate, sequence gap, handler failure or 60-second pass deadline fails the run. Explicit caller cancellation cancels the runtime and produces no accepted result.

The duration boundary stops starting new passes and finishes the current pass. That final pass, runtime startup/shutdown, seeking and validation remain inside elapsed time, CPU and allocation measurement. Throughput, resource and optional fetch-diagnostic samplers remain active until the pass drains, including any overrun beyond the nominal duration. A timed cycle can therefore exceed its nominal duration by one pass. The pass deadline and the runtime's 30-second stop deadline bound failure handling. No partial pass is treated as complete or silently removed from counts.

## Observer cost and results

The existing six-cycle warmup, process CPU/allocation counters, runtime samples, throughput sampler, watchdog and stability analysis run unchanged. JSON retains `keyedConsumer` dimensions and completed pass/record counts. `replayBookkeepingSeconds` measures seek, resume, pass-state allocation and final sequence validation; it does **not** isolate runtime construction or drain time. Those costs are still in the main measurements. Small local seed sizes deliberately magnify replay overhead and are correctness probes, not throughput acceptance experiments.

Per record, the oracle reads the sequence/key ID, checks one array slot, uses `Interlocked` for exclusion and completion, and updates the existing throughput counter. Most handlers return a completed `ValueTask`; one in 64 records per key uses `Task.Yield` to keep asynchronous key lanes active. That sampled handler allocates an async state machine and schedules a continuation. Normal byte-array deserialization also allocates key/value buffers. Per pass, delegates, cancellation sources, oracle arrays and the partitioned runtime are recreated. All these costs apply equally to both revisions and remain measured. This fixture makes no claim of zero allocation per record.

Consumer replay does not record delivery latency: replay age is not processing latency. The existing A/B/A screen compares throughput, CPU/message, allocations/message and stability; latency rows remain n/a. Separate evidence is required for a product change's p50/p99 claims. The comparer rejects different workload dimensions or incomplete passes before comparing metrics.

## Run locally

Docker is required unless `KAFKA_BOOTSTRAP_SERVERS` supplies a broker. This bounded correctness smoke uses fewer records than the acceptance lane:

```powershell
dotnet run -c Release --project tools/Dekaf.StressTests -- --scenario consumer-keyed --client dekaf --keyed-shape large-colliding --partitions 2 --keyed-records-per-partition 2048 --message-size 128 --producer-warmup-seconds 20 --duration 1
```

Local dimensions are validated: 1..6 partitions, 32..65,536 records per partition in multiples of 32, and 8..4,096 value bytes. The workflow validates shape and value size before starting the runner.

## Exact-SHA acceptance

Use one relevant shape, five to fifteen minutes per sample, and the fresh-main SHA contained in the candidate. Candidate and controls build the identical harness against their own pinned product sources. The existing workflow runs correctness preflights, then baseline/candidate/baseline with unchanged warmup and acceptance rules. Fixed-worker setup remains the existing optional experiment setting; zero preserves runtime defaults.

```powershell
gh workflow run stress-tests.yml --ref <candidate-branch> -f lane=consumer-keyed-1b -f keyed_shape=large-colliding -f dispatch_shape=cheap -f duration_minutes=5 -f producer_warmup_seconds=180 -f message_size=1000 -f baseline_sha=<fresh-main-40-character-SHA> -f profile_mode=off
```

Record the run URL, exact SHAs, lane, shape, duration and verdict in the product PR. Landing this harness does not establish #3048 acceptance and requires no paid acceptance run of its own. The collision-promotion implementation and its latency/CPU assessment remain separate product work.
