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
/// Measures the producer's generic Avro path and the unchanged Apache SpecificRecord control.
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

    private AvroSchemaRegistrySerializer<GenericRecord> _serializer = null!;
    private AvroSchemaRegistrySerializer<SpecificBenchmarkRecord> _specificSerializer = null!;
    private GenericRecord[] _equivalentRecords = null!;
    private GenericRecord _intRecord = null!;
    private GenericRecord _nullableIntArrayRecord = null!;
    private GenericRecord _nullableIntCollectionRecord = null!;
    private GenericRecord _scalarUnionRecord = null!;
    private GenericRecord _dictionaryMapRecord = null!;
    private GenericRecord _conditionalLogicalUnionRecord = null!;
    private SpecificBenchmarkRecord _specificRecord = null!;
    private ArrayBufferWriter<byte> _serializeBuffer = null!;
    private GenericRecord _stableRecord = null!;
    private SerializationContext _context;
    private int _recordIndex;

    [GlobalSetup]
    public void Setup()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new BenchmarkStringBytesLogicalType());
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
        _scalarUnionRecord = CreateScalarUnionRecord();
        _dictionaryMapRecord = CreateDictionaryMapRecord();
        _conditionalLogicalUnionRecord = CreateConditionalLogicalUnionRecord();
        _specificRecord = new SpecificBenchmarkRecord { Id = 42, Name = "benchmark" };
        _serializeBuffer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(_stableRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_intRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_nullableIntArrayRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_scalarUnionRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_dictionaryMapRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _serializer.Serialize(_conditionalLogicalUnionRecord, ref _serializeBuffer, _context);
        _serializeBuffer.ResetWrittenCount();
        _specificSerializer.Serialize(_specificRecord, ref _serializeBuffer, _context);
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

    [Benchmark(Description = "Control: Apache writer prepared SpecificRecord")]
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

[MemoryDiagnoser(displayGenColumns: false)]
public class AvroMultipleConditionalUnionBenchmarks
{
    private const string SchemaText =
        """
        {
          "type": "record",
          "name": "MultipleConditionalUnionBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks",
          "fields": [{
            "name": "value",
            "type": [
              { "type": "bytes", "logicalType": "dekaf-benchmark-multi-string-bytes" },
              { "type": "string", "logicalType": "dekaf-benchmark-multi-string-text" }
            ]
          }]
        }
        """;

    private AvroSchemaRegistrySerializer<GenericRecord> _serializer = null!;
    private GenericRecord _record = null!;
    private ArrayBufferWriter<byte> _buffer = null!;
    private SerializationContext _context;

    [GlobalSetup]
    public void Setup()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new MultiStringBytesLogicalType());
        Avro.Util.LogicalTypeFactory.Instance.Register(new MultiStringTextLogicalType());
        _serializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        var schema = (Avro.RecordSchema)AvroSchema.Parse(SchemaText);
        _record = new GenericRecord(schema);
        _record.Add("value", "text-value");
        _buffer = new ArrayBufferWriter<byte>();
        _context = new SerializationContext
        {
            Topic = "avro-multiple-conditional-benchmark",
            Component = SerializationComponent.Value
        };
        _serializer.Serialize(_record, ref _buffer, _context);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _serializer.DisposeAsync().ConfigureAwait(false);

    [Benchmark(Description = "Serialize second custom-logical union branch")]
    public void SerializeSecondConditionalBranch()
    {
        _buffer.ResetWrittenCount();
        _serializer.Serialize(_record, ref _buffer, _context);
    }

    private sealed class MultiStringBytesLogicalType()
        : Avro.Util.LogicalType("dekaf-benchmark-multi-string-bytes")
    {
        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            Encoding.UTF8.GetBytes((string)logicalValue);

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            Encoding.UTF8.GetString((byte[])baseValue);

        public override Type GetCSharpType(bool nullible) => typeof(string);

        public override bool IsInstanceOfLogicalType(object logicalValue) =>
            logicalValue is string value && value.StartsWith("bytes-", StringComparison.Ordinal);
    }

    private sealed class MultiStringTextLogicalType()
        : Avro.Util.LogicalType("dekaf-benchmark-multi-string-text")
    {
        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) => logicalValue;

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) => baseValue;

        public override Type GetCSharpType(bool nullible) => typeof(string);

        public override bool IsInstanceOfLogicalType(object logicalValue) =>
            logicalValue is string value && value.StartsWith("text-", StringComparison.Ordinal);
    }
}
