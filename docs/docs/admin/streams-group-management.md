---
sidebar_position: 2
description: "List, alter, and delete Kafka Streams group offsets and groups."
---

# Streams group management

Kafka 4.3 adds Streams-specific Admin API names while deliberately reusing the
existing `OffsetFetch`, `OffsetCommit`, `OffsetDelete`, and `DeleteGroups` wire
protocols. Dekaf exposes those operations through
`IStreamsGroupManagementAdminClient` and matching `IAdminClient` extension
methods.

```csharp
await using IAdminClient admin = Kafka.CreateAdminClient()
    .WithBootstrapServers("localhost:9092")
    .Build();

var partition = new TopicPartition("orders", 0);

var altered = await admin.AlterStreamsGroupOffsetsAsync(
    "orders-streams",
    [new TopicPartitionOffset("orders", 0, 42)]);

var listed = await admin.ListStreamsGroupOffsetsAsync(
    new Dictionary<string, ListStreamsGroupOffsetsSpec>
    {
        ["orders-streams"] = new() { TopicPartitions = [partition] }
    });

var offset = listed["orders-streams"].Offsets[partition];
```

Omit `TopicPartitions` to list every committed partition for a group. Set
`RequireStable` when an unstable transactional offset must not be returned.
Dekaf batches groups that share a coordinator and negotiates OffsetFetch v6–v10;
fetch-all requests use v9 because v10 replaces topic names with IDs and provides
no request-local name mapping for an unrestricted topic set.

All mutation calls return one result per requested item. Inspect `ErrorCode`
instead of losing successful items when another group or partition fails:

```csharp
var deletedOffsets = await admin.DeleteStreamsGroupOffsetsAsync(
    "orders-streams",
    [partition]);

var deletedGroups = await admin.DeleteStreamsGroupsAsync(["orders-streams"]);
```

Input validation and pre-cancellation happen before network access. Empty
requests return empty results. Each options type has a client-side `TimeoutMs`;
expiration throws `TimeoutException`, while caller cancellation preserves
`OperationCanceledException`. Ambiguous offset/group deletion retries treat a
subsequent `GroupIdNotFound` as success.

The four operations reuse protocol shapes already covered by golden wire
snapshots: OffsetFetch v6–v10, OffsetCommit v8–v10, OffsetDelete v0, and
DeleteGroups v2.

Dekaf's built-in and in-memory admin clients implement the optional capability.
Custom `IAdminClient` implementations can add it without an interface binary
compatibility break.
