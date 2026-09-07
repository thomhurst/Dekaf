---
sidebar_position: 8
description: "Queue-style consumption with share groups (KIP-932): record-level acknowledgement, Accept/Release/Reject, implicit vs explicit modes, and acquisition lock renewal."
---

# Share Consumers (KIP-932)

Share consumers implement [KIP-932 "Queues for Kafka"](https://cwiki.apache.org/confluence/display/KAFKA/KIP-932%3A+Queues+for+Kafka). Instead of assigning each partition to exactly one group member, a **share group** lets every member consume from any partition, with the broker handing out records under short-lived acquisition locks. Each record is acknowledged individually — accepted, released for redelivery, or rejected — giving you traditional message-queue semantics on top of Kafka topics.

## Consumer or Share Consumer?

The two models differ in who owns partitions and how progress is tracked:

| | Consumer group | Share group |
|---|---|---|
| Partition ownership | Each partition assigned to exactly one member | None — any member fetches from any partition |
| Max parallelism | Partition count (extra consumers idle) | Unlimited — scale consumers past partition count |
| Ordering | Guaranteed within a partition | Not guaranteed; records from one partition process concurrently |
| Progress tracking | Committed offset per partition | Per-record acknowledgement (Accept / Release / Reject) |
| Failure handling | Coarse: reprocess from committed offset; one poison message blocks the partition behind it | Per-record: release or reject one record, the rest keep flowing |
| Position control | Seek, pause, offset reset, replay history | None — the broker manages the delivery window |
| Delivery counting | Not tracked | `DeliveryCount` per record, enabling max-attempts logic |
| Broker requirement | Kafka 4.0+ | Kafka 4.2+ with `group.share.enable=true` |

**Pick a [regular consumer group](./consumer-groups)** when you need per-partition ordering, offset-based replay, or stream-processing semantics — event sourcing, changelog consumption, windowed aggregation.

**Pick a share consumer** when you want work-queue semantics — more workers than partitions, per-message retry without blocking neighbors, or you are replacing a queue system (RabbitMQ, SQS, Azure Service Bus) with Kafka.

If you are unsure, start with a regular consumer group: it is the standard Kafka model, has no broker feature flag, and supports the full offset toolbox. Reach for share groups when partition-count ceilings or head-of-line blocking become the actual problem.

## Requirements

Share groups require **Kafka 4.2+** with share groups enabled on the broker:

```properties
group.share.enable=true
```

## Creating a Share Consumer

Use the fluent builder:

```csharp
using Dekaf;

await using var consumer = await Kafka.CreateShareConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-workers")   // Share group ID (required)
    .SubscribeTo("orders")
    .BuildAsync();
```

Or from a root `KafkaClient` when multiple clients share connections:

```csharp
await using var kafka = Kafka.Connect("localhost:9092");

await using var consumer = await kafka.CreateShareConsumer<string, string>("order-workers")
    .SubscribeTo("orders")
    .BuildAsync();
```

Share groups do not support manual partition assignment — `Subscribe` is the only way to receive records. The share group coordinator decides which partitions each member fetches from; the current set is exposed via `consumer.Assignment`.

## Consuming and Acknowledging

`PollAsync` returns an `IAsyncEnumerable` of acquired records:

```csharp
await foreach (var record in shareConsumer.PollAsync(cancellationToken))
{
    try
    {
        await ProcessAsync(record.Value);
        shareConsumer.Acknowledge(record, AcknowledgeType.Accept);
    }
    catch (TransientException)
    {
        // Redeliver to any group member (this one or another)
        shareConsumer.Acknowledge(record, AcknowledgeType.Release);
    }
    catch (PoisonMessageException)
    {
        // Permanently reject - never redelivered
        shareConsumer.Acknowledge(record, AcknowledgeType.Reject);
    }
}
```

The three acknowledgement types:

| Type | Effect |
|------|--------|
| `Accept` | Record processed successfully; removed from the share partition |
| `Release` | Record returned to the group for redelivery (increments its delivery count) |
| `Reject` | Record is unprocessable; permanently discarded, never redelivered |

`ShareConsumeResult<TKey, TValue>` carries the usual `Topic`, `Partition`, `Offset`, `Key`, `Value`, `Headers`, and `Timestamp`, plus `DeliveryCount` — how many times the broker has delivered this record (first delivery = 1). Use it to dead-letter records that keep failing:

```csharp
if (record.DeliveryCount >= 5)
{
    await deadLetterProducer.ProduceAsync("orders-dlq", record.Key, record.Value);
    shareConsumer.Acknowledge(record, AcknowledgeType.Reject);
    return;
}
```

## Acknowledgement Modes

The mode controls what happens to records you do *not* explicitly acknowledge. It maps to Kafka's `share.acknowledgement.mode`:

```csharp
await using var consumer = await Kafka.CreateShareConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-workers")
    .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
    .BuildAsync(cancellationToken);
```

**Implicit (default):** records from the previous poll that were not passed to `Acknowledge` are automatically accepted when the next `PollAsync` iteration or `CommitAsync` sends acknowledgements. Call `Acknowledge(record, Release)` or `Reject` *before* the next poll if a record must not be auto-accepted.

**Explicit:** only records passed to `Acknowledge` are acknowledged. Unacknowledged records stay locked until their acquisition lock expires, then return to the group for redelivery.

Acknowledgements are batched and piggy-backed onto the next ShareFetch. To flush them immediately without fetching more records, call:

```csharp
await consumer.CommitAsync(cancellationToken);
```

## Observing Acknowledgement Outcomes

Register an acknowledgement commit callback when application bookkeeping must observe the broker's final result:

```csharp
await using var consumer = await Kafka.CreateShareConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-workers")
    .WithAcknowledgementCommitCallback(results =>
    {
        foreach (var result in results)
        {
            if (result.Exception is null)
            {
                Console.WriteLine($"Acknowledged {result.TopicPartition}: " +
                    $"{result.Offsets.Length} record(s)");
            }
            else
            {
                Console.Error.WriteLine(
                    $"Acknowledgement failed for {result.TopicPartition}: {result.Exception.Message}");
            }
        }
    })
    .BuildAsync();
```

One `ShareAcknowledgementCommitResult` is reported per topic-partition. `Offsets` are ascending, `Succeeded` is true when `Exception` is null, and results are ordered by topic (ordinal) then partition.

The result span is valid only while the callback runs. Copy individual result values when they must be retained. Each result's `Offsets` is an allocation-free view that supports indexed access, `foreach`, and `CopyTo`.

The callback covers both acknowledgement transports:

- inline acknowledgements piggy-backed by `PollAsync`;
- standalone acknowledgements sent by `CommitAsync` or the close/dispose flush.

Dekaf invokes it once after broker retries finish and after successful acknowledgements are applied and failed acknowledgements are requeued. If cancellation ends a commit, failed partitions are requeued and reported before `OperationCanceledException` reaches the caller. A callback exception is logged and ignored; it never replaces the broker outcome or changes retry state.

The callback runs synchronously on the thread continuing the poll, commit, or close operation. Keep it short and non-blocking. Re-entering the same consumer from the callback is unsupported; record work for later processing instead.

## Acquisition Locks and Renewal

Records are delivered under a broker-side acquisition lock (default 30 seconds, broker config `group.share.record.lock.duration.ms`). If the lock expires before the record is acknowledged, the broker redelivers it to another member. The active timeout is exposed via `consumer.AcquisitionLockTimeoutMs`.

For work that outlives the lock, renew it:

```csharp
shareConsumer.Acknowledge(record, AcknowledgeType.Renew);
await shareConsumer.CommitAsync(cancellationToken); // Sends the renewal
// ...continue long-running processing, then Accept/Release/Reject as normal
```

Renewal requires explicit acknowledgement mode and brokers supporting ShareFetch/ShareAcknowledge v2; older brokers throw `BrokerVersionException`.

## Configuration

Common builder options beyond the connection/TLS/SASL settings shared with other clients:

| Option | Default | Description |
|--------|---------|-------------|
| `WithGroupId` | — (required) | Share group ID |
| `WithAcknowledgementMode` | `Implicit` | Implicit vs explicit acknowledgement (`share.acknowledgement.mode`) |
| `WithAcknowledgementCommitCallback` | — | Reports ordered per-partition broker outcomes after retries and internal bookkeeping |
| `WithShareAcquireMode` | `BatchOptimized` | `BatchOptimized` acquires along producer batch boundaries; `RecordLimit` strictly caps at `MaxPollRecords` (`share.acquire.mode`) |
| `WithMaxPollRecords` | 500 | Maximum records per poll |
| `WithFetchMinBytes` / `WithFetchMaxBytes` | 1 / 50 MiB | Broker fetch accumulation bounds |
| `WithMaxPartitionFetchBytes` | 1 MiB | Per-partition fetch cap |
| `WithFetchMaxWaitMs` | 200 | Max broker wait for `FetchMinBytes` |
| `WithSessionTimeoutMs` | 45000 | Coordinator removes the member without a heartbeat within this window |
| `WithHeartbeatIntervalMs` | 3000 | Initial heartbeat interval (broker may adjust) |

## Application telemetry

Share consumers can publish application counters and gauges through [broker-side telemetry](../observability#broker-side-telemetry-kip-714). Register metrics on the builder or on a running consumer:

```csharp
using System.Threading;
using Dekaf.ShareConsumer;
using Dekaf.Telemetry;

long completed = 0;
await using var consumer = await Kafka.CreateShareConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("jobs")
    .RegisterMetricForSubscription(new ApplicationTelemetryMetric(
        "com.example.jobs.completed", ApplicationTelemetryMetricKind.Counter,
        () => Interlocked.Read(ref completed)))
    .BuildAsync();

consumer.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
    "com.example.jobs.queue.depth", ApplicationTelemetryMetricKind.Gauge,
    () => 42));
consumer.UnregisterMetricFromSubscription("com.example.jobs.queue.depth");
```

The broker's client-metrics configuration selects metric name prefixes, collection interval, compression, and counter temporality. Supply a cumulative monotonic value for a counter; Dekaf computes deltas when requested. Observation callbacks run on the telemetry background loop, so keep them fast, non-blocking, and safe to call alongside application work. Metrics outside requested prefixes are not observed.

Registering the same name replaces its previous metric and resets counter history. Removing a missing name does nothing. Builders snapshot registrations for each built consumer; `ShareConsumerOptions.ApplicationMetrics` also supplies initial registrations. Metric attributes are copied when the metric is created. Registration and removal after consumer disposal throw `ObjectDisposedException`.

Runtime methods use the optional `IApplicationTelemetryShareConsumer` capability. Existing implementations of `IKafkaShareConsumer<TKey, TValue>` remain compatible; the extension methods throw `NotSupportedException` when that capability is absent. A supported broker receives the encoded application metrics under the client's assigned instance identity, including the final telemetry push during shutdown.

## Built-in client telemetry

When the broker subscribes to `org.apache.kafka.consumer.share.`, Dekaf publishes the 28 client metrics specified by [KIP-932](https://cwiki.apache.org/confluence/spaces/KAFKA/pages/255070434/KIP-932%2BQueues%2Bfor%2BKafka). Application metrics use the same payload. Collection starts after subscription negotiation; an empty or unrelated subscription disables the share metric hooks.

All names below start with `org.apache.kafka.consumer.share.`:

| Suffix | Meaning in Dekaf |
|---|---|
| `fetch.manager.fetch.total`, `fetch.manager.fetch.rate` | ShareFetch attempts and attempts per second, including retries and renewal-only ShareFetch requests. |
| `fetch.manager.fetch.latency.avg`, `fetch.manager.fetch.latency.max` | Milliseconds from sending a ShareFetch attempt until its response or failure. Connection acquisition and retry backoff are excluded. |
| `fetch.manager.fetch.throttle.time.avg`, `fetch.manager.fetch.throttle.time.max` | Broker-reported throttle milliseconds from ShareFetch responses. Transport failures do not invent a throttle sample. |
| `fetch.manager.bytes.consumed.total`, `fetch.manager.bytes.consumed.rate` | Encoded sizes of successfully deserialized acquired records, including each record's length prefix, before delivery. Compressed batch envelopes, skipped offsets, and local renewal replay are excluded. |
| `fetch.manager.records.consumed.total`, `fetch.manager.records.consumed.rate` | Successfully deserialized acquired records. An eagerly parsed partition counts even if the caller stops after its first record; records excluded by `MaxPollRecords` do not count. This does not change which records are implicitly acknowledged. |
| `fetch.manager.fetch.size.avg`, `fetch.manager.fetch.size.max` | Parsed record bytes per processed successful broker response, including empty and partially consumed responses. |
| `fetch.manager.records.per.request.avg`, `fetch.manager.records.per.request.max` | Parsed records per processed successful broker response. |
| `fetch.manager.acknowledgements.send.total`, `fetch.manager.acknowledgements.send.rate` | Record acknowledgement attempts in ShareFetch and ShareAcknowledge, including retried attempts and renewals. |
| `fetch.manager.acknowledgements.error.total`, `fetch.manager.acknowledgements.error.rate` | Attempted record acknowledgements affected by a transport, top-level, or partition acknowledgement error. A retry that later succeeds does not erase its earlier error. |
| `coordinator.heartbeat.total`, `coordinator.heartbeat.rate` | ShareGroupHeartbeat attempts and attempts per second. |
| `coordinator.heartbeat.response.time.max` | Maximum milliseconds until a heartbeat response, including an error response. A transport failure without a response contributes no response-time sample. |
| `coordinator.last.heartbeat.seconds.ago` | Seconds since the last heartbeat attempt. |
| `coordinator.rebalance.total`, `coordinator.rebalance.rate.per.hour` | Accepted membership epoch changes, including initial assignment, and changes per hour. An unchanged epoch does not create another rebalance. |
| `last.poll.seconds.ago`, `time.between.poll.avg`, `time.between.poll.max` | Age of the latest fetch round in seconds, and intervals between fetch-round starts in milliseconds. A round corresponds to one `MaxPollRecords` delivery window; individual `MoveNextAsync` calls are not separate Kafka poll operations. |
| `poll.idle.ratio.avg` | Mean fraction of each round spent outside its delivery loops. Delivery-loop time includes caller processing between yields and the small amount of iterator/acknowledgement bookkeeping. Timing occurs at partition or replay-buffer boundaries, including early iterator disposal. |

Rates use elapsed time since the first matching subscription. Averages and maxima cover observations since that subscription; they are lifetime summaries rather than Java's configurable rolling sample windows. Totals use the broker's requested cumulative or delta temporality; gauges retain their meaning across delta pushes. A metric that has no timing/size observation is omitted until its first sample. Filtering one counter does not consume another counter's pending delta.

Following [KIP-714](https://cwiki.apache.org/confluence/spaces/KAFKA/pages/173085915/KIP-714%2BClient%2Bmetrics%2Band%2Bobservability), the OTLP resource includes `group_id`, optional `client_rack`, and the joined `group_member_id`. The broker adds `client_id` and client-instance attribution. These attributes are not duplicated onto every built-in data point. KIP-1103 broker metrics are not advertised as client metrics, and KIP-932 defines no separate acknowledgement-latency metric.

Metric accounting is aggregated at request, partition, or poll-round boundaries. Record parsing adds only local byte arithmetic when subscribed; there are no per-record metric objects, locks, callbacks, or timer reads. Existing class-based share-consumer record/header allocations remain separate from telemetry overhead.


## Thread Safety

`IKafkaShareConsumer<TKey, TValue>` is **not thread-safe**. Call `Subscribe`, `PollAsync`, `Acknowledge`, `CommitAsync`, and `Unsubscribe` from a single thread or with external synchronization. Run multiple consumer instances for parallelism — that is the point of share groups.

## Shutdown

`CloseAsync` and `DisposeAsync` release delivered records that have not yet been
implicitly acknowledged by the next poll or `CommitAsync`. This includes disposal
when application processing throws and records left after partial enumeration.
Records fetched or parsed but never yielded are not implicitly accepted; session
closure releases their acquisition locks.

Explicitly selected `Accept`, `Release` and `Reject` outcomes are preserved. Outcomes
already submitted by a previous poll/commit remain selected even if their failed
request is retried during close. A pending `Renew` is attempted as a renewal, never
as acceptance; session closure then releases remaining locks and stops local replay.
Shutdown is best-effort: if cancellation or broker failure prevents release, records
remain available for redelivery after the broker's acquisition lock expires.

`Unsubscribe` releases pending records and clears the subscription. To close:

```csharp
await consumer.CloseAsync();
// or rely on await using for disposal
```

## Administration

`IAdminClient` covers share group operations: `ListShareGroupsAsync`, `DescribeShareGroupsAsync`, `DeleteShareGroupsAsync`, `DescribeShareGroupOffsetsAsync`, `AlterShareGroupOffsetsAsync`, and `DeleteShareGroupOffsetsAsync`.

Group deletion returns one result per requested ID, so a batch preserves partial failures instead of throwing away successful results:

```csharp
var results = await admin.DeleteShareGroupsAsync(["jobs-a", "jobs-b"]);
foreach (var (groupId, result) in results)
{
    Console.WriteLine($"{groupId}: {result.ErrorCode}");
}
```

The operation uses the group coordinator and Kafka's `DeleteGroups` API, matching Kafka 4.3's `deleteShareGroups` implementation. Active groups normally return `NonEmptyGroup`; close their consumers before deletion.

Per-group results cover terminal error codes only. If a request keeps failing with a retriable error, the call throws after retries are exhausted and returns no results. Duplicate group IDs raise `ArgumentException` before any request is sent. Dekaf's built-in and in-memory admin clients expose deletion through `IShareGroupDeletionAdminClient`; the `IAdminClient` extension preserves the same call syntax for binary compatibility.

## Migration note: implicit acknowledgement on shutdown

Earlier versions treated outstanding implicit deliveries as `Accept` during close,
which could acknowledge records whose application processing failed. Close and
await-using disposal now release these deliveries. After successfully processing
the final records, call `CommitAsync` before closing when they should be accepted,
or explicitly call `Acknowledge(record, AcknowledgeType.Accept)` for each completed
record. Do not commit from an unconditional `finally` block after failed processing.
The in-memory share consumer follows the same provisional-delivery shutdown rule.

## Testing

`Dekaf.Testing` provides `InMemoryShareConsumer<TKey, TValue>` for broker-free unit tests, and `AddDekafInMemory()` swaps DI registrations for in-memory doubles. See [Testing](../testing).
