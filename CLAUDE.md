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
- **BufferMemory Enforces Strict Limits**: Producer `BufferMemory` setting enforces limits across all append paths (both slow path and arena fast path) to prevent unbounded growth. Exceeding this limit blocks `ProduceAsync` until space is available via backpressure.

## Performance and Correctness Guidelines

### Deadlock Prevention

**Always consider deadlock potential when adding synchronization:**

- **ConfigureAwait(false) is mandatory** - Missing it causes deadlocks in consumer applications with synchronization contexts
- **Never block async code** - Don't use `.Result` or `.Wait()` on Tasks in library code
- **Channel-based patterns** - Prefer channels over locks for coordination between threads
- **Disposal coordination** - Use `CancellationTokenSource` and `ManualResetEventSlim` for prompt shutdown signaling

**Hot path discipline:**
- Methods marked `[MethodImpl(MethodImplOptions.AggressiveInlining)]` are performance-critical
- **NEVER add O(n) operations to hot paths** - This includes dictionary enumeration, collection scans, or cleanup loops
- Hot path = message serialization, batch append, channel writes
- If a 10-minute test hang occurs, suspect O(n) operations on hot path

### Memory Leak Prevention

**Track resource lifecycles carefully:**

- **Auto-cleanup for long-running resources** - Use `ContinueWith` or background threads to clean up completed tasks/references
- **Per-batch vs per-message matters** - 1000x difference in allocation cost. Batches contain ~1000 messages.
- **ConcurrentDictionary growth** - Dictionaries tracking async operations must remove completed entries to prevent unbounded growth
- **Unobserved task exceptions** - Always observe task exceptions via `await`, `ContinueWith`, or `TaskScheduler.UnobservedTaskException`

**Example - Proper task tracking with auto-cleanup:**
```csharp
// GOOD: Auto-removes when complete (one allocation per batch, not per message)
private void TrackDeliveryTask(ReadyBatch readyBatch)
{
    var task = readyBatch.DeliveryTask;
    _inFlightDeliveryTasks.TryAdd(task, 0);

    if (!task.IsCompleted)
    {
        _ = task.ContinueWith(static (t, state) =>
        {
            var dict = (ConcurrentDictionary<Task, byte>)state!;
            dict.TryRemove(t, out _);
        }, _inFlightDeliveryTasks,
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
    }
}

// BAD: Memory leak - completed tasks never removed
private void TrackDeliveryTask(ReadyBatch readyBatch)
{
    _inFlightDeliveryTasks.TryAdd(readyBatch.DeliveryTask, 0);
    // No cleanup = unbounded growth in long-running apps
}
```

### Allocation Cost Analysis

**Understand per-message vs per-batch costs:**

- **Per-message allocations** are expensive at high throughput (millions/sec)
- **Per-batch allocations** are acceptable - amortized over ~1000 messages
- Batch = 16KB default = ~1000 small messages
- 100 bytes per batch = 0.1 bytes per message amortized

**When reviewing allocation-related changes:**
- Ask: "Is this per message or per batch?"
- Per-batch allocations (even 100s of bytes) are usually acceptable
- Per-message allocations in hot path must be zero

### Testing Implications

**CI test hangs indicate serious problems:**
- 10-minute timeout = hot path performance issue or deadlock
- Check for O(n) operations added to hot paths
- Check for missing `ConfigureAwait(false)`
- Check for blocking on async operations

**Unobserved task exceptions in CI:**
- Indicates background tasks not properly awaited during disposal
- `DisposeAsync()` must wait for all in-flight work with `try-catch` to observe exceptions
- Use `ConcurrentDictionary<Task, byte>` to track all async operations that need coordinated disposal

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
  Dekaf.Serialization.Json/ # JSON serialization
  Dekaf.Extensions.*/       # DI and Hosting integrations
  Dekaf.SchemaRegistry/     # Confluent Schema Registry base
  Dekaf.SchemaRegistry.Avro/     # Avro serialization with Schema Registry
  Dekaf.SchemaRegistry.Protobuf/ # Protobuf serialization with Schema Registry
tests/
  Dekaf.Tests.Unit/         # Unit tests (TUnit)
  Dekaf.Tests.Integration/  # Integration tests (TUnit + Testcontainers)
tools/
  Dekaf.Benchmarks/         # BenchmarkDotNet benchmarks
  Dekaf.StressTests/        # Long-running stress tests
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

# Run stress tests (requires Docker)
dotnet run --project tools/Dekaf.StressTests --configuration Release -- \
  --duration 15 \
  --message-size 1000 \
  --scenario all \
  --client all
```

### Test Filtering (TUnit)

TUnit uses `--treenode-filter` with the syntax `/<Assembly>/<Namespace>/<Class>/<Test>`. Use `*` as wildcard.

```bash
# Run all tests in a specific class
./Dekaf.Tests.Unit --treenode-filter /*/*/SerializerTests/*

# Run a specific test by name
./Dekaf.Tests.Unit --treenode-filter /*/*/*/StringSerializer_RoundTrip_PreservesValue

# Run tests matching a pattern
./Dekaf.Tests.Unit --treenode-filter /*/*/Producer*/*
```

**Operators:**
- `=` exact match: `/*/*/*[Category=Unit]`
- `!=` exclude: `/*/*/*[Category!=Slow]`
- `&` AND: `/*/*/*[Category=Unit]&[Priority=High]`
- `|` OR (requires parentheses): `(/*/*/ClassA/*)|(/*/*/ClassB/*)`

**Common mistakes to avoid:**
- Do NOT use `--filter` (that's for VSTest, not Microsoft.Testing.Platform)
- Do NOT use `dotnet test --filter` syntax like `FullyQualifiedName~Pattern`
- The path segments are `/<Assembly>/<Namespace>/<Class>/<Test>` - use `*` to skip segments

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

### Producer Cancellation Semantics

**ProduceAsync:** Cancellation works at ALL phases, but with different effects:

**Before message is appended (prevents delivery):**
- ✅ Entry point: Token checked immediately
- ✅ Metadata lookup: Network operations respect cancellation
- ✅ Channel write (slow path): Can be cancelled before worker processes
- ✅ Memory reservation: Blocking on BufferMemory respects cancellation

**After message is appended (stops wait, delivery continues):**
- ✅ Caller's await throws `OperationCanceledException`
- ✅ Message delivery continues in background (no data loss)
- ✅ Allows callers to implement timeouts without blocking indefinitely

**FlushAsync:** Can be cancelled throughout the wait. Cancelling stops the caller from waiting, but batches continue sending in background.

**Send (Fire-and-Forget):** Never uses cancellation tokens. Use `FlushAsync(cancellationToken)` if you need cancellable waiting for delivery.

```csharp
// CORRECT: Cancellation before append - message NOT sent
using var cts = new CancellationTokenSource();
cts.Cancel(); // Cancel immediately
await producer.ProduceAsync(message, cts.Token).ConfigureAwait(false); // Throws OperationCanceledException

// CORRECT: Cancellation after append - stops wait but message IS delivered
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // 5-second timeout
try
{
    var metadata = await producer.ProduceAsync(message, cts.Token).ConfigureAwait(false);
    // Success - got delivery confirmation within 5s
}
catch (OperationCanceledException)
{
    // Timeout - but message will still be delivered to Kafka in background
    // This is useful for scenarios where you want to "fire and forget with best-effort wait"
}

// CORRECT: FlushAsync cancellation
await producer.FlushAsync(cts.Token).ConfigureAwait(false); // Can cancel wait, batches continue sending
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
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
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
using Dekaf;

[ClassDataSource<KafkaContainerDataSource>]
public class ProducerTests(KafkaContainer kafka)
{
    [Test]
    public async Task Producer_SendMessage_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
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
