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
/// Measures prepared GenericRecord and SpecificRecord serialization paths.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroSchemaRegistrySerializerBenchmarks
{
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

    private const string ConditionalLogicalStructuralUnionSchema =
        """
        {
          "type": "record",
          "name": "ConditionalLogicalStructuralUnionBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{
            "name": "value",
            "type": [
              { "type": "bytes", "logicalType": "dekaf-benchmark-int-list-bytes" },
              { "type": "array", "items": "int" }
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
    private AvroSchemaRegistrySerializer<SpecificBenchmarkRecord> _specificSerializer = null!;
    private GenericRecord[] _equivalentRecords = null!;
    private GenericRecord _intRecord = null!;
    private GenericRecord _nullableIntArrayRecord = null!;
    private GenericRecord _nullableIntCollectionRecord = null!;
    private GenericRecord _nullableStringCollectionRecord = null!;
    private GenericRecord _scalarUnionRecord = null!;
    private GenericRecord _dictionaryMapRecord = null!;
    private GenericRecord _conditionalLogicalUnionRecord = null!;
    private GenericRecord _conditionalLogicalStructuralUnionRecord = null!;
    private GenericRecord _conditionalValueLogicalStringArrayRecord = null!;
    private GenericRecord _nestedRecordCollectionRecord = null!;
    private GenericRecord _nestedRecordListRecord = null!;
    private SpecificBenchmarkRecord _specificRecord = null!;
    private ArrayBufferWriter<byte> _serializeBuffer = null!;
    private GenericRecord _stableRecord = null!;
    private SerializationContext _context;
    private int _recordIndex;

    [GlobalSetup]
    public void Setup()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new BenchmarkStringBytesLogicalType());
        Avro.Util.LogicalTypeFactory.Instance.Register(new BenchmarkIntListBytesLogicalType());
        Avro.Util.LogicalTypeFactory.Instance.Register(new BenchmarkIntBytesLogicalType());
        _serializer = new AvroSchemaRegistrySerializer<GenericRecord>(new BenchmarkSchemaRegistryClient());
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
        _intRecord = CreateIntRecord();
        _nullableIntArrayRecord = CreateNullableIntArrayRecord();
        _nullableIntCollectionRecord = CreateNullableIntCollectionRecord();
        _nullableStringCollectionRecord = CreateNullableStringCollectionRecord();
        _scalarUnionRecord = CreateScalarUnionRecord();
        _dictionaryMapRecord = CreateDictionaryMapRecord();
        _conditionalLogicalUnionRecord = CreateConditionalLogicalUnionRecord();
        _conditionalLogicalStructuralUnionRecord = CreateConditionalLogicalStructuralUnionRecord();
        _conditionalValueLogicalStringArrayRecord = CreateConditionalValueLogicalStringArrayRecord();
        (_nestedRecordCollectionRecord, _nestedRecordListRecord) = CreateNestedRecordCollectionRecords();
        _specificRecord = new SpecificBenchmarkRecord { Id = 42, Name = "benchmark" };
        _serializeBuffer = new ArrayBufferWriter<byte>();
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
        _serializer.Serialize(_conditionalLogicalStructuralUnionRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_conditionalValueLogicalStringArrayRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nestedRecordCollectionRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nestedRecordListRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _specificSerializer.Serialize(_specificRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _serializer.DisposeAsync().ConfigureAwait(false);
        await _specificSerializer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true, Description = "Prepare stable generic Avro schema")]
    public ValueTask PrepareStableSchema() => _serializer.PrepareAsync(_stableRecord, _context);

    [Benchmark(Description = "Prepare equivalent generic Avro schema instance")]
    public ValueTask PrepareEquivalentSchema()
    {
        _recordIndex = (_recordIndex + 1) & (_equivalentRecords.Length - 1);
        return _serializer.PrepareAsync(_equivalentRecords[_recordIndex], _context);
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

    [Benchmark(Description = "Serialize conditional-logical structural union fallback")]
    public void SerializeConditionalLogicalStructuralUnionFallback()
    {
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_conditionalLogicalStructuralUnionRecord, ref _serializeBuffer, _context);
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

    private static GenericRecord CreateRecord(int id)
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(RecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", id);
        record.Add("name", "benchmark");
        return record;
    }

    private static GenericRecord CreateIntRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(IntRecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", 42);
        return record;
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

    private static GenericRecord CreateConditionalLogicalStructuralUnionRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(ConditionalLogicalStructuralUnionSchema);
        var record = new GenericRecord(schema);
        record.Add("value", new List<int> { 1, 2, 3, 4 });
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

    private sealed class BenchmarkIntListBytesLogicalType()
        : Avro.Util.LogicalType("dekaf-benchmark-int-list-bytes")
    {
        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            new byte[] { (byte)((IList<int>)logicalValue).Count };

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            throw new NotSupportedException();

        public override Type GetCSharpType(bool nullible) => typeof(IList<int>);

        public override bool IsInstanceOfLogicalType(object logicalValue) =>
            logicalValue is IList<int> { Count: > 0 } values && values[0] < 0;
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
            CancellationToken cancellationToken = default) => Task.FromResult(1);

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
