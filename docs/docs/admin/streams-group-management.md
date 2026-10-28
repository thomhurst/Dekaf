---
sidebar_position: 2
description: "List, alter, and delete Kafka Streams group offsets and groups, and read topology descriptions."
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
expiration throws `KafkaTimeoutException`, while caller cancellation preserves
`OperationCanceledException`. Ambiguous offset/group deletion retries treat a
subsequent `GroupIdNotFound` as success.

The four operations reuse protocol shapes already covered by golden wire
snapshots: OffsetFetch v6–v10, OffsetCommit v8–v10, OffsetDelete v0, and
DeleteGroups v2.

Dekaf's built-in and in-memory admin clients implement the optional capability.
Custom `IAdminClient` implementations can add it without an interface binary
compatibility break.

## Topology descriptions

Kafka 4.4 adds StreamsGroupDescribe v1 ([KIP-1331][kip1331]). A broker with a
topology-description plugin can return the processing graph a Streams
application published: subtopologies with their source, processor and sink
nodes, state stores, successor edges, and global stores. Request it with
`DescribeStreamsGroupsOptions`:

```csharp
var described = await admin.DescribeStreamsGroupsAsync(
    ["orders-streams"],
    new DescribeStreamsGroupsOptions { IncludeTopologyDescription = true });

var group = described["orders-streams"];
if (group.TopologyDescriptionStatus == StreamsGroupTopologyDescriptionStatus.Available)
{
    foreach (var subtopology in group.TopologyDescription!.Subtopologies)
    {
        foreach (var node in subtopology.Nodes)
        {
            Console.WriteLine($"{node.NodeType} {node.Name} -> {string.Join(", ", node.Successors)}");
        }
    }
}
```

`TopologyDescriptionStatus` is reported per group and is separate from the
group's describe error:

| Status | Meaning |
| --- | --- |
| `NotRequested` | The option was not set, or the broker predates v1. |
| `NotStored` | The broker has no description for the group, for example because no plugin is configured. |
| `Error` | The broker failed to fetch the description. The group itself is still described. |
| `Available` | `TopologyDescription` holds the graph. |

The existing `Topology` property still reports the assignment topology
(subtopologies and internal topics) on every broker version. Setting
`IncludeTopologyDescription` against a coordinator that only supports
StreamsGroupDescribe v0 throws `KafkaException` with
`ErrorCode.UnsupportedVersion` before a request is sent to that coordinator. When
the groups span several coordinators, requests to v1 coordinators may already be
in flight when the call fails. Without the
option, Dekaf negotiates the highest common version and older brokers keep
using v0.

The options overload is an `IAdminClient` extension backed by the optional
`IStreamsGroupDescriptionAdminClient` capability. The in-memory admin client
stores no descriptions and reports `NotStored` when one is requested.

[kip1331]: https://cwiki.apache.org/confluence/spaces/KAFKA/pages/421957212/KIP-1331+Streams+Group+Topology+Description+Plugin
