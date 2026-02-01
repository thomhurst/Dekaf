---
sidebar_position: 1
---

# Built-in Serializers

Dekaf includes serializers for common types that are automatically used when you don't specify a custom serializer.

## Supported Types

| Type | Serializer | Description |
|------|------------|-------------|
| `string` | `Serializers.String` | UTF-8 encoded |
| `byte[]` | `Serializers.ByteArray` | Raw bytes |
| `ReadOnlyMemory<byte>` | `Serializers.RawBytes` | Zero-copy bytes |
| `int` | `Serializers.Int32` | Big-endian 32-bit |
| `long` | `Serializers.Int64` | Big-endian 64-bit |
| `Guid` | `Serializers.Guid` | 16 bytes |
| `Ignore` | `Serializers.Ignore` | Null/empty (for null keys) |

## Automatic Selection

When you create a producer or consumer without specifying serializers, Dekaf automatically uses the appropriate built-in serializer:

```csharp
using Dekaf;

// Automatically uses Serializers.String for both key and value
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

// Automatically uses Serializers.String for key, Serializers.Int64 for value
var producer = Kafka.CreateProducer<string, long>()
    .WithBootstrapServers("localhost:9092")
    .Build();
```

## String Serializer

Encodes strings as UTF-8:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

await producer.ProduceAsync("topic", "key", "Hello, World!");
```

Null strings are handled as null Kafka values.

## Byte Array Serializer

Passes bytes through unchanged:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, byte[]>()
    .WithBootstrapServers("localhost:9092")
    .Build();

await producer.ProduceAsync("topic", "key", new byte[] { 1, 2, 3, 4 });
```

## ReadOnlyMemory Serializer

Zero-copy byte handling:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, ReadOnlyMemory<byte>>()
    .WithBootstrapServers("localhost:9092")
    .Build();

ReadOnlyMemory<byte> data = GetData();
await producer.ProduceAsync("topic", "key", data);
```

## Integer Serializers

Big-endian encoding (network byte order):

```csharp
using Dekaf;

// 32-bit integer
var producer32 = Kafka.CreateProducer<int, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

await producer32.ProduceAsync("topic", 12345, "value");

// 64-bit integer
var producer64 = Kafka.CreateProducer<long, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

await producer64.ProduceAsync("topic", 123456789L, "value");
```

## Guid Serializer

16-byte binary representation:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<Guid, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

await producer.ProduceAsync("topic", Guid.NewGuid(), "value");
```

## Ignore Type

For topics where you don't need keys:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<Ignore, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

// Key is always null
await producer.ProduceAsync("topic", Ignore.Value, "value");
```

## Using Serializers Explicitly

You can also use the built-in serializers explicitly:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithKeySerializer(Serializers.String)
    .WithValueSerializer(Serializers.String)
    .Build();
```

## Unsupported Types

If you use a type without a built-in serializer:

```csharp
using Dekaf;

// This throws InvalidOperationException at Build()
var producer = Kafka.CreateProducer<string, MyCustomType>()
    .WithBootstrapServers("localhost:9092")
    .Build();  // Error: No default serializer for type MyCustomType
```

For custom types, see [JSON Serialization](./json) or [Custom Serializers](./custom).
