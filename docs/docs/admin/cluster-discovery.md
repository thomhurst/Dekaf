---
description: "Discover registered Kafka brokers, including fenced nodes, without changing routing."
---

# Cluster discovery

Use the options overload to query live cluster information through Kafka's DescribeCluster RPC:

```csharp
using Dekaf;
using Dekaf.Admin;

await using var admin = Kafka.CreateAdminClient()
    .WithBootstrapServers("localhost:9092")
    .Build();

var snapshot = await admin.DescribeClusterAsync(new DescribeClusterOptions
{
    IncludeFencedBrokers = true
});

foreach (var node in snapshot.Nodes)
{
    Console.WriteLine($"{node.NodeId}: {node.Host}:{node.Port}, fenced={node.IsFenced}");
}
```

Fenced brokers remain registered with the cluster but are unavailable for normal client traffic. This read-only discovery does not unregister nodes, change cluster state, or add fenced nodes to producer/consumer routing metadata.

`IncludeFencedBrokers` defaults to `false`. Requesting it requires DescribeCluster v2 on the actual destination broker. Dekaf throws `BrokerVersionException` when that capability is absent; it never silently drops the option.

`ClusterDescriptionSnapshot.EndpointType` distinguishes broker nodes from controller nodes. With `WithBootstrapControllers(...)`, the options overload describes controllers and rejects `IncludeFencedBrokers = true` with `NotSupportedException`. Use broker bootstrap endpoints to discover fenced brokers. `ClusterNodeDescription.IsFenced` is nullable: controller nodes and responses older than v2 have no broker fencing status.

The existing `DescribeClusterAsync(cancellationToken)` overload retains its metadata-cache semantics and result type. The new overload always makes a live request and returns a separate administrative snapshot. Pass a cancellation token to bound initialization, retries, and the request.

Custom `IAdminClient` implementations remain compatible. They can opt into `IClusterDiscoveryAdminClient`; otherwise the extension method throws `NotSupportedException`.

`Dekaf.Testing.InMemoryAdminClient` implements this capability with its single in-memory broker. Both option values return that broker with `IsFenced = false`; the in-memory cluster does not simulate fencing. Discovery observes the configured admin fault plan and cancellation, and leaves routing metadata unchanged.
