using System.Buffers;
using Avro.Generic;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the producer's generic Avro preparation path for stable and equivalent schema instances.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[MedianColumn]
[MaxColumn]
public class AvroSchemaRegistrySerializerBenchmarks
{
    private const int DistinctSchemaCount = 1024;
    private const int EquivalentOverflowSchemaCount = 1024;
    private const string RecordSchema =
        """
        {
          "type": "record",
          "name": "BenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [
            { "name": "id", "type": "int" },
            { "name": "name", "type": "string" }
          ]
        }
        """;

    private AvroSchemaRegistrySerializer<GenericRecord> _serializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _missSerializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _equivalentOverflowSerializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _overflowSerializer = null!;
    private BenchmarkSchemaRegistryClient _equivalentOverflowClient = null!;
    private BenchmarkSchemaRegistryClient _missClient = null!;
    private GenericRecord[] _distinctRecords = null!;
    private GenericRecord[] _equivalentRecords = null!;
    private GenericRecord[] _equivalentOverflowRecords = null!;
    private GenericRecord[] _overflowRecords = null!;
    private GenericRecord _stableRecord = null!;
    private SerializationContext _context;
    private int _overflowRecordIndex;
    private int _recordIndex;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = new AvroSchemaRegistrySerializer<GenericRecord>(new BenchmarkSchemaRegistryClient());
        _overflowSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            new BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { MaxCachedSchemas = 1 });
        _context = new SerializationContext
        {
            Topic = "avro-benchmark",
            Component = SerializationComponent.Value
        };

        _equivalentRecords = new GenericRecord[256];
        for (var i = 0; i < _equivalentRecords.Length; i++)
            _equivalentRecords[i] = CreateRecord(i);

        _stableRecord = _equivalentRecords[0];
        _equivalentOverflowRecords = new GenericRecord[EquivalentOverflowSchemaCount];
        for (var i = 0; i < _equivalentOverflowRecords.Length; i++)
            _equivalentOverflowRecords[i] = CreateEquivalentOverflowRecord(i);

        _distinctRecords = new GenericRecord[DistinctSchemaCount];
        for (var i = 0; i < _distinctRecords.Length; i++)
            _distinctRecords[i] = CreateDistinctRecord(i);

        _overflowRecords =
        [
            CreateDistinctRecord(DistinctSchemaCount),
            CreateDistinctRecord(DistinctSchemaCount + 1)
        ];

        var buffer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(_stableRecord, ref buffer, _context);
        _overflowSerializer.PrepareAsync(_overflowRecords[0], _context).GetAwaiter().GetResult();
        _overflowSerializer.PrepareAsync(_overflowRecords[1], _context).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serializer.DisposeAsync().GetAwaiter().GetResult();
        _overflowSerializer.DisposeAsync().GetAwaiter().GetResult();
    }

    [IterationSetup(Target = nameof(PrepareDistinctSchemaMisses))]
    public void SetupDistinctSchemaMisses()
    {
        _missClient = new BenchmarkSchemaRegistryClient();
        _missSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            _missClient,
            new AvroSerializerConfig { MaxCachedSchemas = 64 });
    }

    [IterationSetup(Target = nameof(PrepareEquivalentOverflowSchemas))]
    public void SetupEquivalentOverflowSchemas()
    {
        _equivalentOverflowClient = new BenchmarkSchemaRegistryClient();
        _equivalentOverflowSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            _equivalentOverflowClient,
            new AvroSerializerConfig { MaxCachedSchemas = 1 });
        _equivalentOverflowSerializer.PrepareAsync(_stableRecord, _context).GetAwaiter().GetResult();
        _equivalentOverflowSerializer.PrepareAsync(_equivalentOverflowRecords[0], _context)
            .GetAwaiter().GetResult();
    }

    [IterationCleanup(Target = nameof(PrepareEquivalentOverflowSchemas))]
    public void CleanupEquivalentOverflowSchemas()
    {
        _equivalentOverflowSerializer.DisposeAsync().GetAwaiter().GetResult();
        if (_equivalentOverflowClient.RegistrationCount != 2)
        {
            throw new InvalidOperationException(
                $"Expected 2 registrations, but observed " +
                $"{_equivalentOverflowClient.RegistrationCount}.");
        }
    }

    [IterationCleanup(Target = nameof(PrepareDistinctSchemaMisses))]
    public void CleanupDistinctSchemaMisses()
    {
        _missSerializer.DisposeAsync().GetAwaiter().GetResult();
        if (_missClient.RegistrationCount != DistinctSchemaCount)
        {
            throw new InvalidOperationException(
                $"Expected {DistinctSchemaCount} registrations, but observed {_missClient.RegistrationCount}.");
        }
    }

    [Benchmark(Baseline = true, Description = "Prepare stable generic Avro schema")]
    public ValueTask PrepareStableSchema() => _serializer.PrepareAsync(_stableRecord, _context);

    [Benchmark(Description = "Prepare equivalent generic Avro schema instance")]
    public ValueTask PrepareEquivalentSchema()
    {
        _recordIndex = (_recordIndex + 1) & (_equivalentRecords.Length - 1);
        return _serializer.PrepareAsync(_equivalentRecords[_recordIndex], _context);
    }

    [Benchmark(Description = "Prepare generic Avro schema beyond strong cache")]
    public ValueTask PrepareWeakOverflowSchema()
    {
        _overflowRecordIndex = (_overflowRecordIndex + 1) & 1;
        return _overflowSerializer.PrepareAsync(_overflowRecords[_overflowRecordIndex], _context);
    }

    [Benchmark(
        OperationsPerInvoke = EquivalentOverflowSchemaCount - 1,
        Description = "Prepare newly parsed equivalent schema beyond strong cache")]
    [InvocationCount(1)]
    public void PrepareEquivalentOverflowSchemas()
    {
        for (var i = 1; i < _equivalentOverflowRecords.Length; i++)
        {
            _equivalentOverflowSerializer.PrepareAsync(_equivalentOverflowRecords[i], _context)
                .GetAwaiter().GetResult();
        }
    }

    [Benchmark(
        OperationsPerInvoke = DistinctSchemaCount,
        Description = "Prepare first-seen generic Avro schema beyond strong cache")]
    [InvocationCount(1)]
    public void PrepareDistinctSchemaMisses()
    {
        for (var i = 0; i < _distinctRecords.Length; i++)
            _missSerializer.PrepareAsync(_distinctRecords[i], _context).GetAwaiter().GetResult();
    }

    private static GenericRecord CreateRecord(int id)
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(RecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", id);
        record.Add("name", "benchmark");
        return record;
    }

    private static GenericRecord CreateDistinctRecord(int id)
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
              "type": "record",
              "name": "BenchmarkRecord{{id}}",
              "namespace": "Dekaf.Benchmarks",
              "fields": [{ "name": "id", "type": "int" }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("id", id);
        return record;
    }

    private static GenericRecord CreateEquivalentOverflowRecord(int id)
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            """
            {
              "type": "record",
              "name": "EquivalentOverflowRecord",
              "namespace": "Dekaf.Benchmarks",
              "fields": [{ "name": "id", "type": "int" }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("id", id);
        return record;
    }

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient
    {
        private int _registrationCount;

        internal int RegistrationCount => Volatile.Read(ref _registrationCount);

        public Task<int> RegisterSchemaAsync(
            string subject,
            RegistrySchema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<RegistrySchema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            RegistrySchema schema,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Interlocked.Increment(ref _registrationCount));

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            RegistrySchema schema,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose() { }
    }
}
