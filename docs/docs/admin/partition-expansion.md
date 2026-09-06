---
sidebar_position: 5
---

# Partition expansion

Use `CreatePartitionsAsync` to increase a topic's partition count. `TotalCount`
is the **final total**, including existing partitions. Existing partition replica
assignments remain unchanged.

```csharp
using Dekaf.Admin;

await using var admin = new AdminClientBuilder()
    .WithBootstrapServers("localhost:9092")
    .Build();

// A topic with 3 partitions and replication factor 2 grows to 5 partitions.
var expansion = new Dictionary<string, NewPartitions>
{
    ["orders"] = new()
    {
        TotalCount = 5,
        ReplicaAssignments = [[2, 1], [1, 3]]
    }
};

await admin.CreatePartitionsAsync(expansion, new CreatePartitionsOptions
{
    ValidateOnly = true,
    TimeoutMs = 10_000
});

// Apply the same specification after successful validation.
await admin.CreatePartitionsAsync(expansion);
```

Assignments describe only additional partitions, in ascending partition order.
In this example, partition 3 prefers broker 2 and partition 4 prefers broker 1.
Replica ordering is preserved. Leave `ReplicaAssignments` null to let the
controller choose replicas. Each assignment must contain unique, nonnegative
broker IDs and have the same replication factor. The controller checks assignment
count, replication factor, broker availability and current topic state.

`ValidateOnly` performs broker validation without creating partitions. A successful
validation does not reserve the expansion: cluster state can change before it is
applied. Validation retries never infer success from an earlier ambiguous mutation.
`TimeoutMs` controls the broker operation timeout; pass a cancellation token to
bound the caller's entire operation, including initialization and retries.

The original count-only overload remains available:

```csharp
await admin.CreatePartitionsAsync(new Dictionary<string, int> { ["orders"] = 5 });
```

Typed expansion is an optional `IPartitionExpansionAdminClient` capability implemented
by the built-in `AdminClient`. Extensions expose it through `IAdminClient` without
adding required members to existing custom implementations. Clients without this
capability throw `NotSupportedException` for the typed overload.

See Kafka's [NewPartitions contract](https://kafka.apache.org/43/javadoc/org/apache/kafka/clients/admin/NewPartitions.html)
and [CreatePartitionsOptions](https://kafka.apache.org/43/javadoc/org/apache/kafka/clients/admin/CreatePartitionsOptions.html).
