---
sidebar_position: 1
description: "Query current and future Kafka log directories for selected replicas."
---

# Replica log directories

`DescribeReplicaLogDirsAsync` queries only the selected topic-partition replicas. Dekaf groups replicas by broker and sends one `DescribeLogDirs` request to each broker.

```csharp
await using var admin = Kafka.CreateAdminClient()
    .WithBootstrapServers("localhost:9092")
    .Build();

var replica = new TopicPartitionReplica("orders", 0, 1);
var results = await admin.DescribeReplicaLogDirsAsync([replica], cancellationToken);
var info = results[replica];

if (info.ErrorCode == ErrorCode.None)
{
    Console.WriteLine(info.CurrentReplicaLogDir);
    Console.WriteLine(info.CurrentReplicaOffsetLag);
}
```

`CurrentReplicaLogDir` is `null` and `CurrentReplicaOffsetLag` is `-1` when Kafka does not report the replica. `FutureReplicaLogDir` is `null` and `FutureReplicaOffsetLag` is `-1` when no log-directory move is active.

Results are returned per distinct replica. A broker-level response error is stored in `ErrorCode` for that broker's requested replicas, so successful results from other brokers remain available. A directory-level error is stored only for replicas explicitly listed by that directory; Kafka does not provide enough information to attribute an empty failed-directory result to an omitted replica. Transport failures and timeouts still throw. Cancellation stops the operation through the supplied `CancellationToken`.
