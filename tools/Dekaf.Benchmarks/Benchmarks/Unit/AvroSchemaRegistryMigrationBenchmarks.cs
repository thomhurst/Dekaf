using System.Buffers.Binary;
using Avro.IO;
using Avro.Specific;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards specific-record schema migration field mapping and allocation overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class AvroSchemaRegistryMigrationBenchmarks
{
    internal const string GeneratedSchema = """
        {
          "type": "record",
          "name": "MigrationSpecificBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks.Benchmarks.Unit",
          "fields": [
            { "name": "name", "type": "string" },
            { "name": "age", "type": "int" }
          ]
        }
        """;

    private const string ReorderedLatestSchema = """
        {
          "type": "record",
          "name": "MigrationSpecificBenchmarkRecord",
          "namespace": "Dekaf.Benchmarks.Benchmarks.Unit",
          "fields": [
            { "name": "age", "type": "int" },
            { "name": "name", "type": "string" }
          ]
        }
        """;

    private static readonly SerializationContext Context = new()
    {
        Topic = "benchmark-topic",
        Component = SerializationComponent.Value
    };

    private AvroSchemaRegistryDeserializer<MigrationSpecificBenchmarkRecord> _baseline = null!;
    private AvroSchemaRegistryDeserializer<MigrationSpecificBenchmarkRecord> _latest = null!;
    private byte[] _wire = null!;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new MigrationBenchmarkRegistryClient();
        _baseline = new AvroSchemaRegistryDeserializer<MigrationSpecificBenchmarkRecord>(registry);
        _latest = new AvroSchemaRegistryDeserializer<MigrationSpecificBenchmarkRecord>(
            registry,
            new AvroDeserializerConfig { UseLatestVersion = true });
        _wire = CreateWirePayload();

        _ = Baseline();
        _ = UseLatestVersion();
    }

    [Benchmark(Baseline = true)]
    public MigrationSpecificBenchmarkRecord Baseline() => _baseline.Deserialize(_wire, Context);

    [Benchmark]
    public MigrationSpecificBenchmarkRecord UseLatestVersion() => _latest.Deserialize(_wire, Context);

    private static byte[] CreateWirePayload()
    {
        using var stream = new MemoryStream();
        var record = new MigrationSpecificBenchmarkRecord { Name = "Ada", Age = 36 };
        var writer = new SpecificDefaultWriter(record.Schema);
        writer.Write(record.Schema, record, new BinaryEncoder(stream));
        var payload = stream.ToArray();
        var wire = new byte[payload.Length + 5];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), 1);
        payload.CopyTo(wire.AsSpan(5));
        return wire;
    }

    private sealed class MigrationBenchmarkRegistryClient : ISchemaRegistryClient, ISchemaRegistryCache
    {
        private static readonly RegistrySchema WriterSchema = new()
        {
            SchemaType = SchemaType.Avro,
            SchemaString = GeneratedSchema
        };
        private static readonly RegistrySchema ReaderSchema = new()
        {
            SchemaType = SchemaType.Avro,
            SchemaString = ReorderedLatestSchema
        };
        private static readonly RegisteredSchema Writer = new()
        {
            Id = 1,
            Subject = "benchmark-topic-value",
            Version = 1,
            Schema = WriterSchema
        };
        private static readonly RegisteredSchema Reader = new()
        {
            Id = 2,
            Subject = "benchmark-topic-value",
            Version = 2,
            Schema = ReaderSchema
        };

        public Task<RegistrySchema> GetSchemaAsync(
            int id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(id == 1 ? WriterSchema : ReaderSchema);

        public Task<RegistrySchema> GetSchemaAsync(
            int id,
            string subject,
            CancellationToken cancellationToken = default) =>
            GetSchemaAsync(id, cancellationToken);

        public bool TryGetCachedSchema(int id, out RegistrySchema schema)
        {
            schema = id == 1 ? WriterSchema : ReaderSchema;
            return true;
        }

        public bool TryGetCachedSchema(int id, string subject, out RegistrySchema schema) =>
            TryGetCachedSchema(id, out schema);

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => Task.FromResult(Reader);

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version,
            bool ignoreDeletedSchemas,
            CancellationToken cancellationToken = default) => Task.FromResult(Reader);

        public Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            RegistrySchema schema,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) => Task.FromResult(Writer);

        public Task<int> RegisterSchemaAsync(
            string subject,
            RegistrySchema schema,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            RegistrySchema schema,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

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

        public void Dispose()
        {
        }
    }
}

public sealed class MigrationSpecificBenchmarkRecord : ISpecificRecord
{
    public static readonly AvroSchema _SCHEMA =
        AvroSchema.Parse(AvroSchemaRegistryMigrationBenchmarks.GeneratedSchema);

    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public AvroSchema Schema => _SCHEMA;

    public object Get(int fieldPos) => fieldPos switch
    {
        0 => Name,
        1 => Age,
        _ => throw new ArgumentOutOfRangeException(nameof(fieldPos))
    };

    public void Put(int fieldPos, object fieldValue)
    {
        switch (fieldPos)
        {
            case 0:
                Name = (string)fieldValue;
                break;
            case 1:
                Age = (int)fieldValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fieldPos));
        }
    }
}
