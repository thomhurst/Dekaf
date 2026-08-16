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
    private GenericRecord[] _distinctRecords = null!;
    private GenericRecord[] _equivalentRecords = null!;
    private GenericRecord _stableRecord = null!;
    private SerializationContext _context;
    private int _recordIndex;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = new AvroSchemaRegistrySerializer<GenericRecord>(new BenchmarkSchemaRegistryClient());
        _context = new SerializationContext
        {
            Topic = "avro-benchmark",
            Component = SerializationComponent.Value
        };

        _equivalentRecords = new GenericRecord[256];
        for (var i = 0; i < _equivalentRecords.Length; i++)
            _equivalentRecords[i] = CreateRecord(i);

        _stableRecord = _equivalentRecords[0];
        _distinctRecords = new GenericRecord[64];
        for (var i = 0; i < _distinctRecords.Length; i++)
            _distinctRecords[i] = CreateDistinctRecord(i);

        var buffer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(_stableRecord, ref buffer, _context);
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _serializer.DisposeAsync();

    [Benchmark(Baseline = true, Description = "Prepare stable generic Avro schema")]
    public ValueTask PrepareStableSchema() => _serializer.PrepareAsync(_stableRecord, _context);

    [Benchmark(Description = "Prepare equivalent generic Avro schema instance")]
    public ValueTask PrepareEquivalentSchema()
    {
        _recordIndex = (_recordIndex + 1) & (_equivalentRecords.Length - 1);
        return _serializer.PrepareAsync(_equivalentRecords[_recordIndex], _context);
    }

    [Benchmark(OperationsPerInvoke = 64, Description = "Prepare first-seen generic Avro schema")]
    public void PrepareDistinctSchemaMisses()
    {
        var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(new BenchmarkSchemaRegistryClient());
        for (var i = 0; i < _distinctRecords.Length; i++)
            serializer.PrepareAsync(_distinctRecords[i], _context).GetAwaiter().GetResult();
        serializer.DisposeAsync().GetAwaiter().GetResult();
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

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient
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
