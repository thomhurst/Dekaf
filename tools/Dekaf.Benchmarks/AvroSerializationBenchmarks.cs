using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using Avro;
using Avro.Generic;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks for Avro serialization with Schema Registry integration.
/// These benchmarks use a mock schema registry client to isolate serialization performance.
/// </summary>
/// <remarks>
/// After warmup, these benchmarks demonstrate zero-allocation behavior for the serialization
/// hot path. The first call for a new subject may allocate due to schema caching, but
/// subsequent calls reuse the cached schema ID.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class AvroSerializationBenchmarks
{
    private static readonly int[] SingleVersionArray = [1];

    private AvroSchemaRegistrySerializer<GenericRecord> _serializer = null!;
    private AvroSchemaRegistryDeserializer<GenericRecord> _deserializer = null!;
    private MockSchemaRegistryClient _mockClient = null!;
    private ArrayBufferWriter<byte> _buffer = null!;
    private GenericRecord _smallRecord = null!;
    private GenericRecord _largeRecord = null!;
    private byte[] _serializedSmallRecord = null!;
    private byte[] _serializedLargeRecord = null!;
    private SerializationContext _context;

    private static readonly RecordSchema SmallSchema = (RecordSchema)AvroSchema.Parse(@"
    {
        ""type"": ""record"",
        ""name"": ""SmallMessage"",
        ""namespace"": ""Dekaf.Benchmarks"",
        ""fields"": [
            { ""name"": ""id"", ""type"": ""long"" },
            { ""name"": ""name"", ""type"": ""string"" },
            { ""name"": ""active"", ""type"": ""boolean"" }
        ]
    }");

    private static readonly RecordSchema LargeSchema = (RecordSchema)AvroSchema.Parse(@"
    {
        ""type"": ""record"",
        ""name"": ""LargeMessage"",
        ""namespace"": ""Dekaf.Benchmarks"",
        ""fields"": [
            { ""name"": ""id"", ""type"": ""long"" },
            { ""name"": ""timestamp"", ""type"": ""long"" },
            { ""name"": ""eventType"", ""type"": ""string"" },
            { ""name"": ""payload"", ""type"": ""string"" },
            { ""name"": ""source"", ""type"": ""string"" },
            { ""name"": ""destination"", ""type"": ""string"" },
            { ""name"": ""correlationId"", ""type"": ""string"" },
            { ""name"": ""priority"", ""type"": ""int"" },
            { ""name"": ""retryCount"", ""type"": ""int"" },
            { ""name"": ""metadata"", ""type"": { ""type"": ""map"", ""values"": ""string"" } }
        ]
    }");

    [GlobalSetup]
    public void Setup()
    {
        _mockClient = new MockSchemaRegistryClient();
        _serializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            _mockClient,
            new AvroSerializerConfig { AutoRegisterSchemas = true });
        _deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(_mockClient);
        _buffer = new ArrayBufferWriter<byte>(4096);
        _context = new SerializationContext { Topic = "benchmark-topic", Component = SerializationComponent.Value };

        // Create test records
        _smallRecord = new GenericRecord(SmallSchema);
        _smallRecord.Add("id", 12345L);
        _smallRecord.Add("name", "test-message");
        _smallRecord.Add("active", true);

        _largeRecord = new GenericRecord(LargeSchema);
        _largeRecord.Add("id", 98765L);
        _largeRecord.Add("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _largeRecord.Add("eventType", "OrderCreated");
        _largeRecord.Add("payload", new string('x', 500)); // 500 char payload
        _largeRecord.Add("source", "order-service");
        _largeRecord.Add("destination", "fulfillment-service");
        _largeRecord.Add("correlationId", Guid.NewGuid().ToString());
        _largeRecord.Add("priority", 5);
        _largeRecord.Add("retryCount", 0);
        _largeRecord.Add("metadata", new Dictionary<string, object>
        {
            ["region"] = "us-east-1",
            ["environment"] = "production",
            ["version"] = "1.0.0"
        });

        // Warmup serializers to populate schema cache (ensures subsequent calls are allocation-free)
        _serializer.WarmupAsync("benchmark-topic", _smallRecord, isKey: false).GetAwaiter().GetResult();
        _serializer.WarmupAsync("benchmark-topic", _largeRecord, isKey: false).GetAwaiter().GetResult();

        // Pre-serialize records for deserialization benchmarks
        _serializer.Serialize(_smallRecord, _buffer, _context);
        _serializedSmallRecord = _buffer.WrittenSpan.ToArray();
        _buffer.Clear();

        _serializer.Serialize(_largeRecord, _buffer, _context);
        _serializedLargeRecord = _buffer.WrittenSpan.ToArray();
        _buffer.Clear();

        // Warmup deserializers with schema IDs
        var smallSchemaId = BinaryPrimitives.ReadInt32BigEndian(_serializedSmallRecord.AsSpan(1, 4));
        var largeSchemaId = BinaryPrimitives.ReadInt32BigEndian(_serializedLargeRecord.AsSpan(1, 4));
        _deserializer.WarmupAsync(smallSchemaId).GetAwaiter().GetResult();
        _deserializer.WarmupAsync(largeSchemaId).GetAwaiter().GetResult();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _buffer.Clear();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serializer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _deserializer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _mockClient.Dispose();
    }

    // ===== Serialization Benchmarks =====

    [Benchmark(Description = "Avro Serialize: Small Record (3 fields)")]
    public void SerializeSmallRecord()
    {
        _serializer.Serialize(_smallRecord, _buffer, _context);
    }

    [Benchmark(Description = "Avro Serialize: Large Record (10 fields, 500 char payload)")]
    public void SerializeLargeRecord()
    {
        _serializer.Serialize(_largeRecord, _buffer, _context);
    }

    // ===== Deserialization Benchmarks =====

    [Benchmark(Description = "Avro Deserialize: Small Record (3 fields)")]
    public GenericRecord DeserializeSmallRecord()
    {
        return _deserializer.Deserialize(new ReadOnlySequence<byte>(_serializedSmallRecord), _context);
    }

    [Benchmark(Description = "Avro Deserialize: Large Record (10 fields, 500 char payload)")]
    public GenericRecord DeserializeLargeRecord()
    {
        return _deserializer.Deserialize(new ReadOnlySequence<byte>(_serializedLargeRecord), _context);
    }

    // ===== Round-trip Benchmarks =====

    [Benchmark(Description = "Avro Round-trip: Small Record")]
    public GenericRecord RoundTripSmallRecord()
    {
        _serializer.Serialize(_smallRecord, _buffer, _context);
        var result = _deserializer.Deserialize(new ReadOnlySequence<byte>(_buffer.WrittenSpan.ToArray()), _context);
        _buffer.Clear();
        return result;
    }

    /// <summary>
    /// Mock Schema Registry client for benchmarking without network overhead.
    /// All schema operations are performed in-memory with thread-safe caching.
    /// </summary>
    private sealed class MockSchemaRegistryClient : ISchemaRegistryClient
    {
        private int _nextSchemaId = 1;
        private readonly ConcurrentDictionary<string, int> _subjectToId = new();
        private readonly ConcurrentDictionary<int, RegistrySchema> _idToSchema = new();
        private readonly ConcurrentDictionary<string, RegisteredSchema> _subjectToRegistered = new();

        public Task<int> RegisterSchemaAsync(string subject, RegistrySchema schema, CancellationToken cancellationToken = default)
        {
            var id = _subjectToId.GetOrAdd(subject, _ =>
            {
                var newId = Interlocked.Increment(ref _nextSchemaId);
                _idToSchema[newId] = schema;
                _subjectToRegistered[subject] = new RegisteredSchema
                {
                    Id = newId,
                    Subject = subject,
                    Version = 1,
                    Schema = schema
                };
                return newId;
            });
            return Task.FromResult(id);
        }

        public Task<RegistrySchema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
        {
            if (_idToSchema.TryGetValue(id, out var schema))
                return Task.FromResult(schema);
            throw new InvalidOperationException($"Schema with ID {id} not found");
        }

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default)
        {
            if (_subjectToRegistered.TryGetValue(subject, out var registered))
                return Task.FromResult(registered);
            throw new InvalidOperationException($"Subject {subject} not found");
        }

        public Task<int> GetOrRegisterSchemaAsync(string subject, RegistrySchema schema, CancellationToken cancellationToken = default)
        {
            return RegisterSchemaAsync(subject, schema, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(_subjectToId.Keys.ToList());
        }

        public Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<int>>(SingleVersionArray);
        }

        public Task<bool> IsCompatibleAsync(string subject, RegistrySchema schema, string version = "latest", CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default)
        {
            _subjectToId.TryRemove(subject, out _);
            _subjectToRegistered.TryRemove(subject, out _);
            return Task.FromResult<IReadOnlyList<int>>(SingleVersionArray);
        }

        public void Dispose()
        {
            // Nothing to dispose in mock
        }
    }
}
