using System.Buffers;
using Avro.Generic;
using Avro.Specific;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures Avro preparation and serialization for stable and equivalent schema instances.
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
    private AvroSchemaRegistrySerializer<GenericRecord> _alternatingOverflowSerializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _overflowSerializer = null!;
    private AvroSchemaRegistrySerializer<BenchmarkSpecificRecord> _specificOverflowSerializer = null!;
    private AvroSchemaRegistrySerializer<BenchmarkSpecificRecord> _alternatingSpecificSerializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _alternatingGenericSerializer = null!;
    private BenchmarkSchemaRegistryClient _equivalentOverflowClient = null!;
    private BenchmarkSchemaRegistryClient _alternatingOverflowClient = null!;
    private BenchmarkSchemaRegistryClient _missClient = null!;
    private BenchmarkSchemaRegistryClient _specificOverflowClient = null!;
    private GenericRecord[] _distinctRecords = null!;
    private GenericRecord[] _equivalentRecords = null!;
    private GenericRecord[] _equivalentOverflowRecords = null!;
    private GenericRecord[] _alternatingOverflowRecords = null!;
    private GenericRecord[] _overflowRecords = null!;
    private GenericRecord _stableRecord = null!;
    private BenchmarkSpecificRecord[] _specificOverflowRecords = null!;
    private BenchmarkSpecificRecord[] _alternatingSpecificRecords = null!;
    private GenericRecord[] _alternatingGenericRecords = null!;
    private ArrayBufferWriter<byte> _specificBuffer = null!;
    private ArrayBufferWriter<byte> _genericBuffer = null!;
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

        _alternatingOverflowRecords = new GenericRecord[EquivalentOverflowSchemaCount];
        for (var i = 0; i < _alternatingOverflowRecords.Length; i++)
            _alternatingOverflowRecords[i] = CreateAlternatingOverflowRecord(i);

        _distinctRecords = new GenericRecord[DistinctSchemaCount];
        for (var i = 0; i < _distinctRecords.Length; i++)
            _distinctRecords[i] = CreateDistinctRecord(i);

        _overflowRecords =
        [
            CreateDistinctRecord(DistinctSchemaCount),
            CreateDistinctRecord(DistinctSchemaCount + 1)
        ];

        _specificOverflowRecords = new BenchmarkSpecificRecord[EquivalentOverflowSchemaCount];
        for (var i = 0; i < _specificOverflowRecords.Length; i++)
            _specificOverflowRecords[i] = BenchmarkSpecificRecord.CreateEquivalent();
        _specificBuffer = new ArrayBufferWriter<byte>();

        _alternatingSpecificRecords =
        [
            BenchmarkSpecificRecord.CreateLookupHeavy("AlternatingSpecificA"),
            BenchmarkSpecificRecord.CreateLookupHeavy("AlternatingSpecificB")
        ];
        _alternatingSpecificSerializer = new AvroSchemaRegistrySerializer<BenchmarkSpecificRecord>(
            new BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { MaxCachedSchemas = 1 });
        _alternatingGenericRecords =
        [
            CreateLookupHeavyGenericRecord("AlternatingGenericA"),
            CreateLookupHeavyGenericRecord("AlternatingGenericB")
        ];
        _alternatingGenericSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            new BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { MaxCachedSchemas = 2 });
        _genericBuffer = new ArrayBufferWriter<byte>();

        var buffer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(_stableRecord, ref buffer, _context);
        _overflowSerializer.PrepareAsync(_overflowRecords[0], _context).GetAwaiter().GetResult();
        _overflowSerializer.PrepareAsync(_overflowRecords[1], _context).GetAwaiter().GetResult();
        _specificBuffer.ResetWrittenCount();
        _alternatingSpecificSerializer.Serialize(_alternatingSpecificRecords[0], ref _specificBuffer, _context);
        _specificBuffer.ResetWrittenCount();
        _alternatingSpecificSerializer.Serialize(_alternatingSpecificRecords[1], ref _specificBuffer, _context);
        _alternatingGenericSerializer.Serialize(_alternatingGenericRecords[0], ref _genericBuffer, _context);
        _genericBuffer.ResetWrittenCount();
        _alternatingGenericSerializer.Serialize(_alternatingGenericRecords[1], ref _genericBuffer, _context);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serializer.DisposeAsync().GetAwaiter().GetResult();
        _overflowSerializer.DisposeAsync().GetAwaiter().GetResult();
        _alternatingSpecificSerializer.DisposeAsync().GetAwaiter().GetResult();
        _alternatingGenericSerializer.DisposeAsync().GetAwaiter().GetResult();
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

    [IterationSetup(Target = nameof(PrepareAlternatingOverflowSchemas))]
    public void SetupAlternatingOverflowSchemas()
    {
        _alternatingOverflowClient = new BenchmarkSchemaRegistryClient();
        _alternatingOverflowSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            _alternatingOverflowClient,
            new AvroSerializerConfig { MaxCachedSchemas = 1 });
        _alternatingOverflowSerializer.PrepareAsync(_stableRecord, _context).GetAwaiter().GetResult();
        _alternatingOverflowSerializer.PrepareAsync(_alternatingOverflowRecords[0], _context)
            .GetAwaiter().GetResult();
        _alternatingOverflowSerializer.PrepareAsync(_alternatingOverflowRecords[1], _context)
            .GetAwaiter().GetResult();
        _alternatingOverflowSerializer.PrepareAsync(_alternatingOverflowRecords[2], _context)
            .GetAwaiter().GetResult();
    }

    [IterationSetup(Target = nameof(SerializeEquivalentSpecificOverflowSchemas))]
    public void SetupEquivalentSpecificOverflowSchemas()
    {
        _specificOverflowClient = new BenchmarkSchemaRegistryClient();
        _specificOverflowSerializer = new AvroSchemaRegistrySerializer<BenchmarkSpecificRecord>(
            _specificOverflowClient,
            new AvroSerializerConfig { MaxCachedSchemas = 1 });
        _specificBuffer.ResetWrittenCount();
        _specificOverflowSerializer.Serialize(
            BenchmarkSpecificRecord.CreateDistinct(),
            ref _specificBuffer,
            _context);
        _specificBuffer.ResetWrittenCount();
        _specificOverflowSerializer.Serialize(_specificOverflowRecords[0], ref _specificBuffer, _context);
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

    [IterationCleanup(Target = nameof(PrepareAlternatingOverflowSchemas))]
    public void CleanupAlternatingOverflowSchemas()
    {
        _alternatingOverflowSerializer.DisposeAsync().GetAwaiter().GetResult();
        if (_alternatingOverflowClient.RegistrationCount != 4)
        {
            throw new InvalidOperationException(
                $"Expected 4 registrations, but observed " +
                $"{_alternatingOverflowClient.RegistrationCount}.");
        }
    }

    [IterationCleanup(Target = nameof(SerializeEquivalentSpecificOverflowSchemas))]
    public void CleanupEquivalentSpecificOverflowSchemas()
    {
        _specificOverflowSerializer.DisposeAsync().GetAwaiter().GetResult();
        if (_specificOverflowClient.RegistrationCount != 2)
        {
            throw new InvalidOperationException(
                $"Expected 2 registrations, but observed " +
                $"{_specificOverflowClient.RegistrationCount}.");
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
        OperationsPerInvoke = EquivalentOverflowSchemaCount - 3,
        Description = "Prepare three rotating logical schemas beyond strong cache")]
    [InvocationCount(1)]
    public void PrepareAlternatingOverflowSchemas()
    {
        for (var i = 3; i < _alternatingOverflowRecords.Length; i++)
        {
            _alternatingOverflowSerializer.PrepareAsync(_alternatingOverflowRecords[i], _context)
                .GetAwaiter().GetResult();
        }
    }

    [Benchmark(
        OperationsPerInvoke = EquivalentOverflowSchemaCount - 1,
        Description = "Serialize newly parsed equivalent specific schema beyond strong cache")]
    [InvocationCount(1)]
    public void SerializeEquivalentSpecificOverflowSchemas()
    {
        for (var i = 1; i < _specificOverflowRecords.Length; i++)
        {
            _specificBuffer.ResetWrittenCount();
            _specificOverflowSerializer.Serialize(_specificOverflowRecords[i], ref _specificBuffer, _context);
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

    [Benchmark(
        OperationsPerInvoke = EquivalentOverflowSchemaCount,
        Description = "Serialize alternating exact specific schemas")]
    [InvocationCount(1)]
    public void SerializeAlternatingSpecificSchemas()
    {
        for (var i = 0; i < EquivalentOverflowSchemaCount; i++)
        {
            _specificBuffer.ResetWrittenCount();
            _alternatingSpecificSerializer.Serialize(
                _alternatingSpecificRecords[i & 1],
                ref _specificBuffer,
                _context);
        }
    }

    [Benchmark(
        OperationsPerInvoke = EquivalentOverflowSchemaCount,
        Description = "Serialize alternating exact generic schemas")]
    [InvocationCount(1)]
    public void SerializeAlternatingGenericSchemas()
    {
        for (var i = 0; i < EquivalentOverflowSchemaCount; i++)
        {
            _genericBuffer.ResetWrittenCount();
            _alternatingGenericSerializer.Serialize(
                _alternatingGenericRecords[i & 1],
                ref _genericBuffer,
                _context);
        }
    }

    private static GenericRecord CreateLookupHeavyGenericRecord(string name)
    {
        var properties = string.Join(",", Enumerable.Range(0, 64).Select(
            static index => $"\"property{index}\":\"value{index}\""));
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
              "type": "record",
              "name": "{{name}}",
              "namespace": "Dekaf.Benchmarks",
              "fields": [],
              {{properties}}
            }
            """);
        return new GenericRecord(schema);
    }

    private static GenericRecord CreateAlternatingOverflowRecord(int id)
    {
        var recordName = (id % 3) switch
        {
            0 => "RotatingOverflowA",
            1 => "RotatingOverflowB",
            _ => "RotatingOverflowC"
        };
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
              "type": "record",
              "name": "{{recordName}}",
              "namespace": "Dekaf.Benchmarks",
              "fields": [{ "name": "id", "type": "int" }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("id", id);
        return record;
    }

    private sealed class BenchmarkSpecificRecord(AvroSchema schema) : ISpecificRecord
    {
        public AvroSchema Schema { get; } = schema;

        internal static BenchmarkSpecificRecord CreateDistinct() => new(
            AvroSchema.Parse(
                """
                {
                  "type": "record",
                  "name": "RetainedSpecificRecord",
                  "namespace": "Dekaf.Benchmarks",
                  "fields": []
                }
                """));

        internal static BenchmarkSpecificRecord CreateEquivalent() => new(
            AvroSchema.Parse(
                """
                {
                  "type": "record",
                  "name": "EquivalentSpecificRecord",
                  "namespace": "Dekaf.Benchmarks",
                  "fields": []
                }
                """));

        internal static BenchmarkSpecificRecord CreateLookupHeavy(string name)
        {
            var properties = string.Join(",", Enumerable.Range(0, 64).Select(
                static index => $"\"property{index}\":\"value{index}\""));
            return new BenchmarkSpecificRecord(AvroSchema.Parse(
                $$"""
                {
                  "type": "record",
                  "name": "{{name}}",
                  "namespace": "Dekaf.Benchmarks",
                  "fields": [],
                  {{properties}}
                }
                """));
        }

        public object Get(int fieldPos) => throw new ArgumentOutOfRangeException(nameof(fieldPos));

        public void Put(int fieldPos, object fieldValue) =>
            throw new ArgumentOutOfRangeException(nameof(fieldPos));
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
