---
sidebar_position: 5
description: "Inspect supported Kafka features on a selected broker or controller."
---

# Node-specific feature discovery

Use `DescribeFeaturesOptions.NodeId` to inspect an individual node during a rolling upgrade:

```csharp
using Dekaf.Admin;

await using var admin = Kafka.CreateAdminClient()
    .WithBootstrapServers("localhost:9092")
    .Build();

var features = await admin.DescribeFeaturesAsync(new DescribeFeaturesOptions
{
    NodeId = 2,
    TimeoutMs = 10_000
});

foreach (var feature in features.SupportedFeatures)
{
    Console.WriteLine($"{feature.Key}: {feature.Value.MinVersion}..{feature.Value.MaxVersion}");
}
```

`SupportedFeatures` contains the ranges advertised by the selected connection. `FinalizedFeatures` and `FinalizedFeaturesEpoch` describe cluster-wide finalized levels; querying an older node does not replace a newer finalized epoch already observed by this client.

With broker bootstrap endpoints, `NodeId` must identify a broker in discovered metadata. With `WithBootstrapControllers`, it must identify a controller in discovered controller metadata. An explicit selection never falls back to another node. Unknown IDs, endpoint or identity mismatches, and connection failures produce errors. Version negotiation uses the selected connection, so different nodes can return different supported ranges.

Omit `NodeId` to select an arbitrary broker or the active controller. The original `DescribeFeaturesAsync(CancellationToken)` overload remains available. On the options overload, `TimeoutMs` bounds initialization, connection establishment, retries, and the query; it defaults to the client's request timeout. Negative values are invalid and zero times out immediately. Expiration throws `TimeoutException`; caller cancellation throws `OperationCanceledException`.

The built-in admin client exposes this additive operation through `INodeFeatureAdminClient` and an `IAdminClient` extension method. Existing custom implementations remain compatible; they must implement the optional capability to support the options overload. Unsupported implementations throw `NotSupportedException`.
