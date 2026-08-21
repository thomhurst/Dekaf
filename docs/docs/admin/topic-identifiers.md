---
sidebar_position: 1
description: "Describe and delete Kafka topics by stable topic ID."
---

# Topic identifiers

Kafka assigns every topic a UUID. Topic IDs remain unambiguous when a name is deleted and recreated, so administrative workflows can target the exact topic they discovered.

```csharp
await using var admin = Kafka.CreateAdminClient()
    .WithBootstrapServers("localhost:9092")
    .Build();

var listing = (await admin.ListTopicsAsync())
    .Single(topic => topic.Name == "orders");

var descriptions = await admin.DescribeTopicsAsync([listing.TopicId]);
var description = descriptions[listing.TopicId];

Console.WriteLine($"{description.Name}: {description.TopicId}");

await admin.DeleteTopicsAsync(
    [listing.TopicId],
    new DeleteTopicsOptions { TimeoutMs = 30_000 });
```

`DescribeTopicsAsync(IEnumerable<Guid>)` returns descriptions keyed by topic ID. Each `TopicDescription` preserves both the broker-provided name and ID, including per-topic errors such as `UnknownTopicId`. Duplicate IDs are sent once. An empty sequence returns immediately.

Topic-ID description requires Metadata API v10 or later. Topic-ID deletion requires DeleteTopics API v6. Dekaf throws `BrokerVersionException` before sending the operation when the connected broker does not support the required protocol version.

The built-in and in-memory admin clients expose these overloads through `ITopicIdAdminClient`. The `IAdminClient` extension methods preserve the normal call shape; custom admin-client implementations must implement the capability interface to support topic-ID operations. Cancellation is honored before and during network operations. Delete timeout behavior uses the existing `DeleteTopicsOptions.TimeoutMs` setting.
