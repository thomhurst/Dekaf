---
sidebar_position: 2
---

# Getting Started

Let's get you producing and consuming messages. This won't take long.

## Prerequisites

- .NET 10 SDK or later
- Access to a Kafka cluster (or we'll show you how to run one locally)

## Installation

Install Dekaf from NuGet:

```bash
dotnet add package Dekaf
```

If you need compression, add the relevant codec package:

```bash
dotnet add package Dekaf.Compression.Lz4    # Recommended for most use cases
dotnet add package Dekaf.Compression.Zstd   # Best compression ratio
dotnet add package Dekaf.Compression.Snappy # Alternative fast codec
```

## Using Dekaf

Dekaf's entry point is available globally - no `using` directive needed:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();
```

For advanced scenarios where you reference types directly (builders, interfaces, options), add:

```csharp
using Dekaf;
using Dekaf.Producer;
using Dekaf.Consumer;
```

But for typical usage, the static `Kafka` class is all you need.

## Running Kafka Locally

If you don't have a Kafka cluster, the easiest way to get one running is with Docker:

```bash
docker run -d --name kafka \
  -p 9092:9092 \
  -e KAFKA_CFG_NODE_ID=0 \
  -e KAFKA_CFG_PROCESS_ROLES=controller,broker \
  -e KAFKA_CFG_LISTENERS=PLAINTEXT://:9092,CONTROLLER://:9093 \
  -e KAFKA_CFG_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT \
  -e KAFKA_CFG_CONTROLLER_QUORUM_VOTERS=0@localhost:9093 \
  -e KAFKA_CFG_CONTROLLER_LISTENER_NAMES=CONTROLLER \
  -e KAFKA_CFG_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092 \
  bitnami/kafka:latest
```

## Your First Producer

Let's send a message to Kafka:

```csharp
// Create a producer
await using var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

// Send a message and wait for acknowledgment
var metadata = await producer.ProduceAsync("my-topic", "greeting", "Hello, Kafka!");

Console.WriteLine($"Message sent to partition {metadata.Partition} at offset {metadata.Offset}");
```

That's it! Let's break down what's happening:

1. **`Kafka.CreateProducer<TKey, TValue>()`** - Creates a builder for the producer. The type parameters define the key and value types.

2. **`WithBootstrapServers()`** - Tells the producer where to find your Kafka cluster. It will discover other brokers automatically.

3. **`Build()`** - Creates the producer instance. The producer is `IAsyncDisposable`, so use `await using` to ensure proper cleanup.

4. **`ProduceAsync()`** - Sends the message and waits for the broker to acknowledge it.

## Your First Consumer

Now let's consume messages:

```csharp
// Create a consumer
await using var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-first-consumer")
    .SubscribeTo("my-topic")
    .Build();

// Consume messages
Console.WriteLine("Waiting for messages... (Ctrl+C to exit)");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await foreach (var message in consumer.ConsumeAsync(cts.Token))
{
    Console.WriteLine($"Received: {message.Key} = {message.Value}");
}
```

Key points:

1. **`WithGroupId()`** - Consumers with the same group ID share the work of consuming a topic. Kafka tracks progress per group.

2. **`SubscribeTo()`** - Tells the consumer which topic(s) to read from. You can subscribe to multiple topics.

3. **`ConsumeAsync()`** - Returns an `IAsyncEnumerable<ConsumeResult<TKey, TValue>>` that you can iterate with `await foreach`.

## Putting It Together

Here's a complete example with a producer and consumer in one program:

```csharp
using Dekaf;
using Dekaf.Producer;
using Dekaf.Consumer;

const string bootstrapServers = "localhost:9092";
const string topic = "getting-started";

// Start the consumer in the background
var cts = new CancellationTokenSource();
var consumerTask = Task.Run(async () =>
{
    await using var consumer = Kafka.CreateConsumer<string, string>()
        .WithBootstrapServers(bootstrapServers)
        .WithGroupId("getting-started-group")
        .WithAutoOffsetReset(AutoOffsetReset.Earliest)
        .SubscribeTo(topic)
        .Build();

    await foreach (var msg in consumer.ConsumeAsync(cts.Token))
    {
        Console.WriteLine($"[Consumer] {msg.Key}: {msg.Value}");
    }
});

// Give the consumer time to join the group
await Task.Delay(2000);

// Produce some messages
await using var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers(bootstrapServers)
    .Build();

for (int i = 1; i <= 5; i++)
{
    var key = $"message-{i}";
    var value = $"Hello from message {i}!";

    await producer.ProduceAsync(topic, key, value);
    Console.WriteLine($"[Producer] Sent: {key}");
}

// Wait for messages to be consumed
await Task.Delay(2000);
cts.Cancel();

try { await consumerTask; }
catch (OperationCanceledException) { }

Console.WriteLine("Done!");
```

## Next Steps

That's the basics. From here:

- **[Producer Guide](./producer/basics)** - Batching, compression, delivery guarantees
- **[Consumer Guide](./consumer/basics)** - Offset management, consumer groups, rebalancing
- **[Configuration Presets](./configuration/presets)** - Pre-tuned configs for common scenarios
- **[Performance Tips](./performance)** - Squeezing out more throughput
