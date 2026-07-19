---
sidebar_position: 4
---

# Custom Serializers

For maximum control or specialized formats, implement custom serializers.

## Interfaces

```csharp
public interface ISerializer<T>
{
    void Serialize(T value, IBufferWriter<byte> output, SerializationContext context);
}

public interface IDeserializer<T>
{
    T Deserialize(ReadOnlySequence<byte> data, SerializationContext context);
}
```

## Basic Example

```csharp
using Dekaf;

public class OrderSerializer : ISerializer<Order>, IDeserializer<Order>
{
    public void Serialize(Order value, IBufferWriter<byte> output, SerializationContext context)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value);
        var span = output.GetSpan(json.Length);
        json.CopyTo(span);
        output.Advance(json.Length);
    }

    public Order Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        return JsonSerializer.Deserialize<Order>(data.FirstSpan)
            ?? throw new SerializationException("Failed to deserialize Order");
    }
}

// Usage
var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new OrderSerializer())
    .BuildAsync();
```

## Zero-Allocation Serializer

Use `IBufferWriter<byte>` for zero-allocation serialization:

```csharp
public class Int32Serializer : ISerializer<int>, IDeserializer<int>
{
    public void Serialize(int value, IBufferWriter<byte> output, SerializationContext context)
    {
        var span = output.GetSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        output.Advance(4);
    }

    public int Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        Span<byte> buffer = stackalloc byte[4];
        data.Slice(0, 4).CopyTo(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }
}
```

## Using SerializationContext

Access message metadata during serialization:

```csharp
public class ContextAwareSerializer : ISerializer<MyType>
{
    public void Serialize(MyType value, IBufferWriter<byte> output, SerializationContext context)
    {
        // Access context
        string topic = context.Topic;
        var component = context.Component;  // Key or Value
        var headers = context.Headers;

        // Serialize based on context
        // ...
    }
}
```

## MessagePack Example

High-performance binary serialization:

```csharp
using Dekaf;

using MessagePack;

public class MessagePackSerializer<T> : ISerializer<T>, IDeserializer<T>
{
    public void Serialize(T value, IBufferWriter<byte> output, SerializationContext context)
    {
        MessagePackSerializer.Serialize(output, value);
    }

    public T Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        return MessagePackSerializer.Deserialize<T>(data);
    }
}

// Usage
var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new MessagePackSerializer<Order>())
    .BuildAsync();
```

## Null Handling

Handle null values appropriately:

```csharp
public class NullableSerializer<T> : ISerializer<T?>, IDeserializer<T?>
    where T : class
{
    private readonly ISerializer<T> _inner;
    private readonly IDeserializer<T> _innerDeserializer;

    public void Serialize(T? value, IBufferWriter<byte> output, SerializationContext context)
    {
        if (value is null)
        {
            // Don't write anything - Kafka will store as null
            return;
        }

        _inner.Serialize(value, output, context);
    }

    public T? Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        if (data.IsEmpty)
        {
            return null;
        }

        return _innerDeserializer.Deserialize(data, context);
    }
}
```

## Combining Serializer and Deserializer

Implement both interfaces in one class for convenience:

```csharp
using Dekaf;

public class OrderCodec : ISerializer<Order>, IDeserializer<Order>
{
    public void Serialize(Order value, IBufferWriter<byte> output, SerializationContext context)
    {
        // Serialization logic
    }

    public Order Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        // Deserialization logic
    }
}

// Use same instance for both
var codec = new OrderCodec();

var producer = await Kafka.CreateProducer<string, Order>()
    .WithValueSerializer(codec)
    .BuildAsync();

var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithValueDeserializer(codec)
    .BuildAsync();
```

## Async Serializers (IAsyncSerde)

When serialization itself requires I/O — for example an encryption stack that fetches short-lived
keys per message — implement `IAsyncSerializer<T>` / `IAsyncDeserializer<T>` (or the combined
`IAsyncSerde<T>`):

```csharp
using Dekaf;
using Dekaf.Serialization;

public class EncryptingSerde : IAsyncSerde<Order>
{
    public async ValueTask SerializeAsync(
        Order value, IBufferWriter<byte> destination,
        SerializationContext context, CancellationToken cancellationToken = default)
    {
        var key = await FetchEncryptionKeyAsync(cancellationToken);
        var payload = Encrypt(JsonSerializer.SerializeToUtf8Bytes(value), key);
        destination.Write(payload);
    }

    public async ValueTask<Order> DeserializeAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context, CancellationToken cancellationToken = default)
    {
        var key = await FetchEncryptionKeyAsync(cancellationToken);
        // `data` is pooled memory — only valid until this method's task completes.
        return JsonSerializer.Deserialize<Order>(Decrypt(data.Span, key))!;
    }
}

// Same builder methods — the async overload is picked by type
var producer = await Kafka.CreateProducer<string, Order>()
    .WithValueSerializer(new EncryptingSerde())
    .BuildAsync();

var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithValueDeserializer(new EncryptingSerde())
    .BuildAsync();
```

Behavior and trade-offs:

- **Prefer synchronous serializers** unless you genuinely need per-message async work. Async
  serdes route every message through a slower pooled-buffer path with async state-machine
  overhead; the synchronous zero-allocation fast path is unaffected when they are not configured.
- For a **one-time async setup** (like a Schema Registry fetch) keep a synchronous serializer and
  implement `IAsyncSerializerPreparer<T>` instead.
- Producer: `ProduceAsync`, `ProduceAllAsync`, and `FireAsync` all work. Fire-and-forget delivery
  errors go to the delivery handler (or the log) as usual.
- Consumer: records are delivered via `ConsumeAsync` and `ConsumeOneAsync`. `ConsumeBatchAsync`
  iterates records synchronously and throws `NotSupportedException` when an async deserializer is
  configured.
- Mixed configurations are fine — e.g. a synchronous key serializer with an async value serializer.
- Configuring both a synchronous and an asynchronous serializer for the same component fails at
  `Build()` with `InvalidOperationException`.
