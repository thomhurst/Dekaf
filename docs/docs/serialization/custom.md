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
