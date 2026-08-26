using System.Buffers;
using System.Collections.ObjectModel;
using System.Text;
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
/// Measures Avro cache preparation and prepared GenericRecord/SpecificRecord serialization paths.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[MedianColumn]
[MaxColumn]
public class AvroSchemaRegistrySerializerBenchmarks
{
    private const int DistinctSchemaCount = 1024;
    private const int EquivalentOverflowSchemaCount = 1024;
    private const string IntRecordSchema =
        """
        {
          "type": "record",
          "name": "IntBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{ "name": "id", "type": "int" }]
        }
        """;

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

    private const string NullableIntArraySchema =
        """
        {
          "type": "record",
          "name": "NullableIntArrayBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{ "name": "values", "type": { "type": "array", "items": ["null", "int"] } }]
        }
        """;

    private const string ScalarUnionSchema =
        """
        {
          "type": "record",
          "name": "ScalarUnionBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{ "name": "value", "type": ["null", "int", "string"] }]
        }
        """;

    private const string NullableStringArraySchema =
        """
        {
          "type": "record",
          "name": "NullableStringArrayBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{ "name": "values", "type": { "type": "array", "items": ["null", "string"] } }]
        }
        """;

    private const string DictionaryMapSchema =
        """
        {
          "type": "record",
          "name": "DictionaryMapBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{ "name": "values", "type": { "type": "map", "values": "int" } }]
        }
        """;

    private const string ConditionalLogicalUnionSchema =
        """
        {
          "type": "record",
          "name": "ConditionalLogicalUnionBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{
            "name": "value",
            "type": [
              { "type": "bytes", "logicalType": "dekaf-benchmark-string-bytes" },
              "string"
            ]
          }]
        }
        """;

    private const string ConditionalValueLogicalStringArraySchema =
        """
        {
          "type": "record",
          "name": "ConditionalValueLogicalStringArrayBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{
            "name": "values",
            "type": {
              "type": "array",
              "items": [
                { "type": "bytes", "logicalType": "dekaf-benchmark-int-bytes" },
                "string"
              ]
            }
          }]
        }
        """;

    private const string NestedRecordArraySchema =
        """
        {
          "type": "record",
          "name": "NestedRecordArrayBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{
            "name": "values",
            "type": {
              "type": "array",
              "items": {
                "type": "record",
                "name": "NestedBenchmarkValue",
                "fields": [{ "name": "id", "type": "int" }]
              }
            }
          }]
        }
        """;

    private AvroSchemaRegistrySerializer<GenericRecord> _serializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _missSerializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _equivalentOverflowSerializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _alternatingOverflowSerializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _overflowSerializer = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _alternatingGenericSerializer = null!;
    private BenchmarkSchemaRegistryClient _equivalentOverflowClient = null!;
    private BenchmarkSchemaRegistryClient _alternatingOverflowClient = null!;
    private BenchmarkSchemaRegistryClient _missClient = null!;
    private GenericRecord[] _distinctRecords = null!;
    private GenericRecord[] _equivalentRecords = null!;
    private GenericRecord[] _equivalentOverflowRecords = null!;
    private GenericRecord[] _alternatingOverflowRecords = null!;
    private GenericRecord[] _overflowRecords = null!;
    private AvroSchemaRegistrySerializer<SpecificBenchmarkRecord> _specificSerializer = null!;
    private GenericRecord _intRecord = null!;
    private GenericRecord _nullableIntArrayRecord = null!;
    private GenericRecord _nullableIntCollectionRecord = null!;
    private GenericRecord _nullableStringCollectionRecord = null!;
    private GenericRecord _scalarUnionRecord = null!;
    private GenericRecord _dictionaryMapRecord = null!;
    private GenericRecord _conditionalLogicalUnionRecord = null!;
    private GenericRecord _conditionalValueLogicalStringArrayRecord = null!;
    private GenericRecord _nestedRecordCollectionRecord = null!;
    private GenericRecord _nestedRecordListRecord = null!;
    private GenericRecord[] _variableSizeRecords = null!;
    private SpecificBenchmarkRecord _specificRecord = null!;
    private ArrayBufferWriter<byte> _serializeBuffer = null!;
    private ExactSizeBufferWriter _exactSizeSerializeBuffer = null!;
    private GenericRecord _stableRecord = null!;
    private GenericRecord[] _alternatingGenericRecords = null!;
    private ArrayBufferWriter<byte> _genericBuffer = null!;
    private SerializationContext _context;
    private int _overflowRecordIndex;
    private int _recordIndex;

    [GlobalSetup]
    public void Setup()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new BenchmarkStringBytesLogicalType());
        Avro.Util.LogicalTypeFactory.Instance.Register(new BenchmarkIntBytesLogicalType());
        _serializer = new AvroSchemaRegistrySerializer<GenericRecord>(new BenchmarkSchemaRegistryClient());
        _overflowSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            new BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { MaxCachedSchemas = 1 });
        _specificSerializer = new AvroSchemaRegistrySerializer<SpecificBenchmarkRecord>(
            new BenchmarkSchemaRegistryClient());
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
        _alternatingGenericSerializer.Serialize(_alternatingGenericRecords[0], ref _genericBuffer, _context);
        _genericBuffer.ResetWrittenCount();
        _alternatingGenericSerializer.Serialize(_alternatingGenericRecords[1], ref _genericBuffer, _context);

        _intRecord = CreateIntRecord();
        _nullableIntArrayRecord = CreateNullableIntArrayRecord();
        _nullableIntCollectionRecord = CreateNullableIntCollectionRecord();
        _nullableStringCollectionRecord = CreateNullableStringCollectionRecord();
        _scalarUnionRecord = CreateScalarUnionRecord();
        _dictionaryMapRecord = CreateDictionaryMapRecord();
        _conditionalLogicalUnionRecord = CreateConditionalLogicalUnionRecord();
        _conditionalValueLogicalStringArrayRecord = CreateConditionalValueLogicalStringArrayRecord();
        (_nestedRecordCollectionRecord, _nestedRecordListRecord) = CreateNestedRecordCollectionRecords();
        _variableSizeRecords =
        [
            CreateRecord(1, "small"),
            CreateRecord(2, new string('x', 4096))
        ];
        _specificRecord = new SpecificBenchmarkRecord { Id = 42, Name = "benchmark" };
        _serializeBuffer = new ArrayBufferWriter<byte>();
        _exactSizeSerializeBuffer = new ExactSizeBufferWriter(8192);
        _serializer.Serialize(_stableRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_intRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nullableIntArrayRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nullableStringCollectionRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_scalarUnionRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_dictionaryMapRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_conditionalLogicalUnionRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_conditionalValueLogicalStringArrayRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nestedRecordCollectionRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nestedRecordListRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _specificSerializer.Serialize(_specificRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_variableSizeRecords[1], ref _exactSizeSerializeBuffer, _context);
        _exactSizeSerializeBuffer.Reset();
        _serializer.Serialize(_variableSizeRecords[0], ref _exactSizeSerializeBuffer, _context);
        _exactSizeSerializeBuffer.Reset();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serializer.DisposeAsync().GetAwaiter().GetResult();
        _overflowSerializer.DisposeAsync().GetAwaiter().GetResult();
        _alternatingGenericSerializer.DisposeAsync().GetAwaiter().GetResult();
        _specificSerializer.DisposeAsync().GetAwaiter().GetResult();
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
        OperationsPerInvoke = DistinctSchemaCount,
        Description = "Prepare first-seen generic Avro schema beyond strong cache")]
    [InvocationCount(1)]
    public void PrepareDistinctSchemaMisses()
    {
        for (var i = 0; i < _distinctRecords.Length; i++)
            _missSerializer.PrepareAsync(_distinctRecords[i], _context).GetAwaiter().GetResult();
    }

    [Benchmark(Description = "Serialize prepared int-only generic Avro record")]
    public void SerializeIntRecord()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_intRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize prepared int + string generic Avro record")]
    public void SerializeIntStringRecord()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_stableRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize nullable-int array generic Avro record")]
    public void SerializeNullableIntArrayRecord()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nullableIntArrayRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize nullable-int Collection<T> generic Avro record")]
    public void SerializeNullableIntCollectionRecord()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nullableIntCollectionRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize scalar-union generic Avro record")]
    public void SerializeScalarUnionRecord()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_scalarUnionRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize nullable-string non-generic collection")]
    public void SerializeNullableStringCollectionRecord()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nullableStringCollectionRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize dictionary-map generic Avro record")]
    public void SerializeDictionaryMapRecord()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_dictionaryMapRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize conditional-logical union fallback")]
    public void SerializeConditionalLogicalUnionFallback()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_conditionalLogicalUnionRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize value-logical union string list")]
    public void SerializeConditionalValueLogicalStringArray()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_conditionalValueLogicalStringArrayRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize non-generic nested-record collection")]
    public void SerializeNestedRecordCollection()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nestedRecordCollectionRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize generic nested-record list")]
    public void SerializeNestedRecordList()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nestedRecordListRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize prepared SpecificRecord (int + string)")]
    public void SerializeSpecificRecord()
    {
        _serializeBuffer.ResetWrittenCount();
        _specificSerializer.Serialize(_specificRecord, ref _serializeBuffer, _context);
    }

    [Benchmark(Description = "Serialize alternating small/large generic Avro records")]
    public void SerializeAlternatingSizeRecords()
    {
        _exactSizeSerializeBuffer.Reset();
        _recordIndex ^= 1;
        _serializer.Serialize(_variableSizeRecords[_recordIndex], ref _exactSizeSerializeBuffer, _context);
    }

    private static GenericRecord CreateRecord(int id, string name = "benchmark")
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(RecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", id);
        record.Add("name", name);
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

    private static GenericRecord CreateIntRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(IntRecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", 42);
        return record;
    }

    private sealed class ExactSizeBufferWriter(int capacity) : IBufferWriter<byte>
    {
        private readonly byte[] _buffer = new byte[capacity];
        private int _written;

        public void Advance(int count) => _written += count;

        public Memory<byte> GetMemory(int sizeHint = 0) =>
            _buffer.AsMemory(_written, Math.Max(1, sizeHint));

        public Span<byte> GetSpan(int sizeHint = 0) =>
            _buffer.AsSpan(_written, Math.Max(1, sizeHint));

        internal void Reset() => _written = 0;
    }

    private static GenericRecord CreateNullableIntArrayRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NullableIntArraySchema);
        var record = new GenericRecord(schema);
        record.Add("values", new int?[] { int.MinValue, null, -1, 0, 1, null, int.MaxValue });
        return record;
    }

    private static GenericRecord CreateNullableIntCollectionRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NullableIntArraySchema);
        var record = new GenericRecord(schema);
        record.Add(
            "values",
            new Collection<int?>([int.MinValue, null, -1, 0, 1, null, int.MaxValue]));
        return record;
    }

    private static GenericRecord CreateScalarUnionRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(ScalarUnionSchema);
        var record = new GenericRecord(schema);
        record.Add("value", 42);
        return record;
    }

    private static GenericRecord CreateNullableStringCollectionRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NullableStringArraySchema);
        var record = new GenericRecord(schema);
        record.Add("values", new NonGenericStringCollection(["first", null, "second"]));
        return record;
    }

    private static GenericRecord CreateDictionaryMapRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(DictionaryMapSchema);
        var record = new GenericRecord(schema);
        record.Add("values", new Dictionary<string, object>
        {
            ["first"] = 1,
            ["second"] = 2,
            ["third"] = 3,
            ["fourth"] = 4
        });
        return record;
    }

    private static GenericRecord CreateConditionalLogicalUnionRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(ConditionalLogicalUnionSchema);
        var record = new GenericRecord(schema);
        record.Add("value", "primitive-value");
        return record;
    }

    private static GenericRecord CreateConditionalValueLogicalStringArrayRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(ConditionalValueLogicalStringArraySchema);
        var record = new GenericRecord(schema);
        record.Add("values", new List<string> { "first", "second" });
        return record;
    }

    private static (GenericRecord Collection, GenericRecord List) CreateNestedRecordCollectionRecords()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NestedRecordArraySchema);
        var arraySchema = (Avro.ArraySchema)schema.Fields[0].Schema;
        var itemSchema = (Avro.RecordSchema)arraySchema.ItemSchema;
        var first = new GenericRecord(itemSchema);
        first.Add("id", 1);
        var second = new GenericRecord(itemSchema);
        second.Add("id", 2);
        var collectionRecord = new GenericRecord(schema);
        collectionRecord.Add("values", new NonGenericRecordCollection([first, second]));
        var listRecord = new GenericRecord(schema);
        listRecord.Add("values", new List<GenericRecord> { first, second });
        return (collectionRecord, listRecord);
    }

    private sealed class NonGenericRecordCollection(IList<GenericRecord> values)
        : Collection<GenericRecord>(values);

    private sealed class NonGenericStringCollection(IList<string?> values)
        : Collection<string?>(values);

    private sealed class BenchmarkStringBytesLogicalType()
        : Avro.Util.LogicalType("dekaf-benchmark-string-bytes")
    {
        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            Encoding.UTF8.GetBytes((string)logicalValue);

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            Encoding.UTF8.GetString((byte[])baseValue);

        public override Type GetCSharpType(bool nullible) => typeof(string);

        public override bool IsInstanceOfLogicalType(object logicalValue) =>
            logicalValue is string value && value.StartsWith("logical-", StringComparison.Ordinal);
    }

    private sealed class BenchmarkIntBytesLogicalType()
        : Avro.Util.LogicalType("dekaf-benchmark-int-bytes")
    {
        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            BitConverter.GetBytes((int)logicalValue);

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            BitConverter.ToInt32((byte[])baseValue);

        public override Type GetCSharpType(bool nullible) => typeof(int);

        public override bool IsInstanceOfLogicalType(object logicalValue) => logicalValue is int;
    }

    private sealed class SpecificBenchmarkRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(RecordSchema);

        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public AvroSchema Schema => _SCHEMA;

        public object Get(int fieldPos) => fieldPos switch
        {
            0 => Id,
            1 => Name,
            _ => throw new ArgumentOutOfRangeException(nameof(fieldPos))
        };

        public void Put(int fieldPos, object fieldValue) =>
            throw new NotSupportedException();
    }

    internal sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient
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

        public Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            RegistrySchema schema,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegisteredSchema
            {
                Id = Math.Max(1, Volatile.Read(ref _registrationCount)),
                Guid = "89791762-2336-4186-9674-299b90a802e2",
                Subject = subject,
                Version = 1,
                Schema = schema
            });

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
