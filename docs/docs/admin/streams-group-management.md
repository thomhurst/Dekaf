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
DeleteGroups v2–v3.

On Kafka 4.4+ (DeleteGroups v3, KIP-1331), `DeleteStreamsGroupResult.ErrorMessage`
carries the broker's reason for a failed deletion. `GroupDeletionFailed` means the
broker kept the group because it could not delete the group's stored topology
description. It is not retriable: the group still exists, so fix the cause and
delete it again.

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

### Publishing a topology description

An `IStreamsGroupMember` publishes the description when you set
`StreamsGroupTopology.Description`. Use the same model that describe returns:

```csharp
await member.JoinAsync(new Dekaf.Streams.StreamsGroupMemberUpdate
{
    Topology = new Dekaf.Streams.StreamsGroupTopology
    {
        Epoch = 0,
        Subtopologies = subtopologies,
        Description = new StreamsGroupTopologyDescription
        {
            Subtopologies = describedSubtopologies,
            GlobalStores = []
        }
    },
    ProcessId = processId
});
```

A Kafka 4.4 coordinator with a topology-description plugin sets
`TopologyDescriptionRequired` on a heartbeat when it has no description for
the group's current topology epoch. The member then sends
StreamsGroupTopologyDescriptionUpdate in the background. Heartbeats are not
delayed while the update is in flight.

- Coordinator moves and other retriable failures are retried with the
  member's retry backoff.
- `UNKNOWN_MEMBER_ID` or a fenced epoch triggers an immediate heartbeat, and
  the heartbeat rejoins the group.
- Other errors, such as `STREAMS_TOPOLOGY_DESCRIPTION_UPDATE_FAILED`, end
  the attempt. The coordinator asks again on a later heartbeat.
- A new topology or losing membership cancels an in-flight update without
  waiting for it to finish.
- Closing the member or disposing it cancels an in-flight update and waits
  for it to finish.

Without a description, the member never publishes.
`StreamsGroupHeartbeatResult.TopologyDescriptionRequired` still reports the
coordinator's request.

## Task assignor selection

Kafka 4.4 lets a Streams group choose its broker-side task assignor
([KIP-1357][kip1357]). StreamsGroupDescribe v1 reports the selection as
`StreamsGroupDescription.AssignorName`:

```csharp
var described = await admin.DescribeStreamsGroupsAsync(["orders-streams"]);
Console.WriteLine(described["orders-streams"].AssignorName); // "sticky" unless the group selects another
```

`AssignorName` is the assignor for the next assignment computation, not
necessarily the one that produced the current assignment. It is `null` when the
coordinator only supports StreamsGroupDescribe v0, and the in-memory admin client
always reports `null`.

Select an assignor for one group with the `streams.assignor.name` group
configuration. Dekaf uses the existing group config support, so no
Streams-specific method is needed:

```csharp
await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
{
    [new ConfigResource { Type = ConfigResourceType.Group, Name = "orders-streams" }] =
        [ConfigAlter.Set("streams.assignor.name", "rack-balanced")]
});
```

Changing the configuration does not trigger a rebalance. The new assignor takes
effect the next time the coordinator computes an assignment, for example when
a member joins or leaves or the topology changes. Delete the configuration with
`ConfigAlter.Delete("streams.assignor.name")` to return the group to the broker
default.

The name must be registered in `group.streams.assignors` (see below). Otherwise
Kafka rejects the change with `ErrorCode.InvalidConfig`. `IncrementalAlterConfigsAsync` throws a
non-retriable `KafkaException` that carries the broker's message, which lists the
registered assignors. `IncrementalAlterConfigsDetailedAsync` reports the same
error for that resource.

### Installing a custom assignor

Dekaf only selects and reports assignors. The assignor itself is a Java
`org.apache.kafka.coordinator.group.api.streams.assignor.TaskAssignor` that runs
inside the group coordinator:

1. Put the assignor's JAR on the classpath of every broker and every KRaft
   controller, for example in each node's `libs` directory. Brokers run the
   assignor in the group coordinator; controllers load it to validate
   `streams.assignor.name` changes.
2. Add the class to the `group.streams.assignors` setting on the same nodes. Each entry is
   either the name of a built-in assignor (Kafka 4.4 ships `sticky`) or the fully
   qualified class name of a custom assignor. The first entry is the default
   for groups that set no `streams.assignor.name`. Keep `sticky` first unless
   you mean to change that default. Changing the default does not trigger a
   rebalance either.
3. Restart those nodes to load the assignor. Each assignor's `name()` becomes the value accepted by
   `streams.assignor.name`. A custom assignor cannot reuse a built-in name, and
   two configured assignors cannot share a name.

Keep the setting identical on every broker and controller. A controller that
does not list the assignor rejects a configuration that selects it, and a
broker that does not list it cannot run it when it becomes the group's
coordinator.

[kip1331]: https://cwiki.apache.org/confluence/spaces/KAFKA/pages/421957212/KIP-1331+Streams+Group+Topology+Description+Plugin
[kip1357]: https://cwiki.apache.org/confluence/spaces/KAFKA/pages/430408758/KIP-1357+Add+broker+side+custom+assignors+for+streams+groups
