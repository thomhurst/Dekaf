---
sidebar_position: 8
---

# Batch share-group offset queries

`ListShareGroupOffsetsAsync` queries offsets for several share groups and returns an outcome keyed by each requested group ID. It maps to Kafka Admin's `listShareGroupOffsets` operation. The existing `DescribeShareGroupOffsetsAsync(groupId, partitions)` method remains available for single-group callers.

```csharp
var results = await admin.ListShareGroupOffsetsAsync(
    new Dictionary<string, ListShareGroupOffsetsSpec>
    {
        ["all-orders"] = new(),
        ["selected-orders"] = new()
        {
            TopicPartitions = [new TopicPartition("orders", 0)]
        },
        ["no-partitions"] = new() { TopicPartitions = [] }
    },
    new ListShareGroupOffsetsOptions { TimeoutMs = 10_000 },
    cancellationToken);
```

In this batch API, a null `TopicPartitions` selection means all applicable partitions. An empty selection produces an empty successful result without a broker request or group-existence check. An empty group map performs no broker work. Invalid group IDs, null specifications, negative partition indexes, duplicate partitions, and negative timeouts fail before sending. The original single-group API retains its existing selection behavior; use the new explicit empty selection when no partitions should be queried.

Check group `ErrorCode` first, then each partition's `ErrorCode` and `ErrorMessage`. Successful partition outcomes retain start offset, leader epoch, and available lag. Lag is `-1` when unavailable. Missing or duplicate group/partition outcomes become `UnknownServerError`, never inferred success. Unrequested response entries are ignored. A nonzero group error has no partition outcomes.

Querying all offsets for a group with no stored share offsets can return an empty successful result. It is not a group-existence check.

Groups sharing a coordinator travel in one request. Both supported `DescribeShareGroupOffsets` versions, v0 and v1, support batching and null selection; v0 is the compatible fallback and omits lag. A destination lacking this API produces an unsupported-version outcome for its groups. This operation uses group coordinators, not the controller.

Retriable group and transport failures use the normal bounded retry policy and rediscover affected coordinators. Completed groups retain their first successful or terminal result and are not queried again while another group retries. Exhausted failures remain group outcomes. Partition errors are returned for caller-directed retry; to retry them, build a new specification containing the failed partitions. These queries do not provide an atomic snapshot across groups or requests.

`TimeoutMs` is one end-to-end budget covering initialization, discovery, sending, and retries. Cancellation throws `OperationCanceledException`; deadline expiry throws `KafkaTimeoutException`. Neither exception carries partial results. A cluster-wide initialization failure or unrelated local invariant failure can also throw before a result is available. This is a read-only operation.

The API uses the additive `IShareGroupOffsetQueryAdminClient` capability and an `IAdminClient` extension. Custom admin implementations remain source-compatible; the extension throws `NotSupportedException` when the capability is absent.

`Dekaf.Testing` implements the same input selection and result shapes over share-group membership and stored offsets. Group and partition fault scopes produce corresponding errors without discarding sibling results. Cancellation and deadlines interrupt fault barriers. The fake has no real coordinator routing or broker retry timing; use broker integration tests for those behaviors.

Protocol reference: [Apache Kafka DescribeShareGroupOffsets request](https://github.com/apache/kafka/blob/trunk/clients/src/main/resources/common/message/DescribeShareGroupOffsetsRequest.json).
