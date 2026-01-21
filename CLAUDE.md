# Dekaf Development Guide

Dekaf is a high-performance, pure C# Apache Kafka client library for .NET 10+. The project goal is "Taking the Java out of Kafka" - a native, zero-allocation implementation without interop overhead or JVM dependencies.

## Critical Rules

1. **Zero-Allocation in Hot Paths**: Protocol serialization, message production, and consumption paths must not allocate on the heap. Use `ref struct`, `Span<T>`, `IBufferWriter<byte>`, and `ArrayPool<T>`. Profile with BenchmarkDotNet before and after changes.

2. **Modern C# Features Required**: Use nullable reference types, `init` properties, pattern matching, and C# 13 features. Never use older patterns when modern alternatives exist.

3. **Comprehensive Testing**: All features require unit tests (TUnit). Integration tests with Testcontainers.Kafka are required for client behavior changes. Performance-critical code requires benchmark tests.

4. **ConfigureAwait(false) Everywhere**: This is a library. All `await` calls must use `ConfigureAwait(false)` to avoid deadlocks in consumer applications.

5. **Interface-First Design**: Public APIs expose interfaces (`IKafkaProducer<TKey, TValue>`, `IKafkaConsumer<TKey, TValue>`). Implementations are internal or sealed.

## Important Warnings

- **Test Projects Require Docker**: Integration tests use Testcontainers.Kafka. Ensure Docker is running before executing integration tests.
- **Benchmarks Compare Against Confluent.Kafka**: Producer/Consumer benchmarks spin up real Kafka instances. Memory/Serialization benchmarks run without Docker.
- **Protocol Code Uses Unsafe**: The `Dekaf.Protocol` namespace uses unsafe code for performance. Changes here require extra scrutiny.

## Project Structure

```
src/
  Dekaf/                    # Core client library
    Protocol/               # Kafka wire protocol (ref structs, zero-allocation)
    Producer/               # Producer implementation (channel-based workers)
    Consumer/               # Consumer implementation
    Networking/             # Connection pool, multiplexed I/O
    Serialization/          # ISerializer<T>/IDeserializer<T> interfaces
  Dekaf.Compression.*/      # Pluggable compression codecs (Lz4, Snappy, Zstd)
  Dekaf.Serialization.*/    # Pluggable serializers (Json, Avro, MessagePack, Protobuf)
  Dekaf.Extensions.*/       # DI and Hosting integrations
  Dekaf.SchemaRegistry/     # Confluent Schema Registry integration
tests/
  Dekaf.Tests.Unit/         # Unit tests (TUnit)
  Dekaf.Tests.Integration/  # Integration tests (TUnit + Testcontainers)
tools/
  Dekaf.Benchmarks/         # BenchmarkDotNet benchmarks
```

## Build Commands

```bash
# Restore and build
dotnet build

# Run unit tests
dotnet build tests/Dekaf.Tests.Unit --configuration Release
./tests/Dekaf.Tests.Unit/bin/Release/net10.0/Dekaf.Tests.Unit

# Run integration tests (requires Docker)
dotnet build tests/Dekaf.Tests.Integration --configuration Release
./tests/Dekaf.Tests.Integration/bin/Release/net10.0/Dekaf.Tests.Integration

# Run benchmarks
dotnet run --project tools/Dekaf.Benchmarks --configuration Release -- --filter "*Memory*"
```

## Code Principles

### Zero-Allocation Protocol Code

```csharp
// CORRECT: ref struct with IBufferWriter<byte>
public ref struct KafkaProtocolWriter
{
    private readonly IBufferWriter<byte> _output;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value)
    {
        var span = _output.GetSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        _output.Advance(4);
    }
}

// WRONG: Allocating arrays
public void WriteInt32(int value)
{
    var bytes = BitConverter.GetBytes(value);  // ALLOCATION!
    Array.Reverse(bytes);                       // ALLOCATION!
    _stream.Write(bytes);
}
```

### Async Patterns

```csharp
// CORRECT: ValueTask for potentially-synchronous operations
public async ValueTask<ProduceResult> ProduceAsync(Message<TKey, TValue> message,
    CancellationToken cancellationToken = default)
{
    // ...
    await _channel.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);
    return await workItem.CompletionSource.Task.ConfigureAwait(false);
}

// WRONG: Missing ConfigureAwait
await _channel.Writer.WriteAsync(workItem, cancellationToken);
```

### Thread-Safety with Channels

```csharp
// CORRECT: Channel-based work distribution (lock-free)
private readonly Channel<ProduceWorkItem<TKey, TValue>> _channel =
    Channel.CreateUnbounded<ProduceWorkItem<TKey, TValue>>();

// WRONG: Locks in hot paths
lock (_lock)
{
    _pendingWork.Add(workItem);
}
```

### Builder Pattern for Configuration

```csharp
// CORRECT: Fluent builder
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithClientId("my-producer")
    .WithAcks(Acks.All)
    .Build();

// Options classes use init-only properties
public sealed class ProducerOptions
{
    public required string BootstrapServers { get; init; }
    public Acks Acks { get; init; } = Acks.Leader;
}
```

### Error Handling

All Kafka-specific exceptions inherit from `KafkaException`:
- `ProduceException` - Production failures with topic/partition context
- `ConsumeException` - Consumption failures
- `GroupException` - Consumer group coordination errors
- `AuthenticationException` / `AuthorizationException` - Security failures

Use the `IsRetriable` property to determine if an operation can be retried.

## Testing Patterns

### Unit Tests (TUnit)

```csharp
public class SerializerTests
{
    [Test]
    public async Task StringSerializer_RoundTrip_PreservesValue()
    {
        var serializer = Serializers.String;
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize("test", buffer);
        var result = Serializers.String.Deserialize(buffer.WrittenSpan);

        await Assert.That(result).IsEqualTo("test");
    }
}
```

### Integration Tests (Testcontainers)

```csharp
[ClassDataSource<KafkaContainerDataSource>]
public class ProducerTests(KafkaContainer kafka)
{
    [Test]
    public async Task Producer_SendMessage_Succeeds()
    {
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.GetBootstrapAddress())
            .Build();

        var result = await producer.ProduceAsync("test-topic", "key", "value");

        await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
    }
}
```

### Benchmarks

```csharp
[MemoryDiagnoser]
public class MemoryBenchmarks
{
    [Benchmark]
    public void WriteThousandInt32s()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        for (int i = 0; i < 1000; i++)
            writer.WriteInt32(i);
    }
}
```

## Decision Framework

When making changes, ask:

1. **Does it allocate?** Hot paths must be allocation-free. Use `[MemoryDiagnoser]` benchmarks to verify.
2. **Is it thread-safe?** Producer/Consumer are used concurrently. Prefer channels and concurrent collections over locks.
3. **Does it have tests?** Unit tests for logic, integration tests for Kafka behavior, benchmarks for performance.
4. **Does it use modern C#?** Nullable reference types, pattern matching, init properties, records where appropriate.
5. **Is the API consistent?** Follow existing patterns: builders for configuration, interfaces for contracts, sealed classes for implementations.

## Architecture Notes

### Networking Layer

- Uses `System.IO.Pipelines` for high-performance I/O
- `MultiplexedConnection` handles concurrent requests over single TCP connection
- `ConnectionPool` manages connections per broker with `ConcurrentDictionary`

### Protocol Layer

- `KafkaProtocolWriter` / `KafkaProtocolReader` are ref structs for zero-allocation
- All protocol messages implement `IKafkaRequest` / `IKafkaResponse`
- Version negotiation happens during connection establishment

### Serialization

- Built-in serializers: `Serializers.String`, `Serializers.Int32`, `Serializers.Bytes`, etc.
- Custom serializers implement `ISerializer<T>` / `IDeserializer<T>`
- Schema Registry integration available via `Dekaf.SchemaRegistry`

### Compression

- Pluggable via `ICompressionCodec` interface
- Register codecs: `CompressionCodecRegistry.Register(new ZstdCodec())`
- Batch-level compression for efficiency
