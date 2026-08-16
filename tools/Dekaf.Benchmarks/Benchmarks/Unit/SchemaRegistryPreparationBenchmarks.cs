using System.Buffers;
using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Protects generic and JSON Schema Registry cached preparation and serialization.
/// Registry resolution is completed during setup.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class SchemaRegistryPreparationBenchmarks
{
    private const int DistinctDataContractCount = 128;
    private readonly ArrayBufferWriter<byte> _genericDestination = new(64);
    private readonly ArrayBufferWriter<byte> _jsonDestination = new(128);
    private readonly SchemaResolutionCache<int> _equivalentDataContractCache = new(maxCachedEntries: 1);
    private readonly SchemaResolutionCache<int> _distinctDataContractCache = new();
    private readonly SchemaResolutionCache<int> _referencedSchemaCache = new();
    private SchemaRegistrySerializer<int> _genericSerializer = null!;
    private SchemaRegistrySerializer<int> _genericOverflowSerializer = null!;
    private JsonSchemaRegistrySerializer<BenchmarkPayload> _jsonSerializer = null!;
    private SerializationContext _context;
    private SerializationContext _overflowContextA;
    private SerializationContext _overflowContextB;
    private SerializationContext _overflowContextC;
    private int _overflowContextIndex;
    private int _equivalentDataContractIndex;
    private int _distinctDataContractIndex;
    private BenchmarkPayload _jsonValue = null!;
    private Schema _dataContractSchemaA = null!;
    private Schema _dataContractSchemaB = null!;
    private Schema[] _distinctDataContractSchemas = null!;
    private Schema _referencedSchema = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var registry = new BenchmarkSchemaRegistryClient();
        var genericSchema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{\"type\":\"integer\"}"
        };
        _genericSerializer = new SchemaRegistrySerializer<int>(
            registry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32BigEndian(span, value);
                writer.Advance(sizeof(int));
            },
            () => genericSchema);
        _genericOverflowSerializer = new SchemaRegistrySerializer<int>(
            registry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32BigEndian(span, value);
                writer.Advance(sizeof(int));
            },
            static _ => CreateDataContractSchema(),
            subjectNameStrategy: SubjectNameStrategy.RecordName);
        _jsonSerializer = new JsonSchemaRegistrySerializer<BenchmarkPayload>(
            registry,
            "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"integer\"}}}");
        _jsonValue = new BenchmarkPayload { Id = 42 };
        _dataContractSchemaA = CreateDataContractSchema();
        _dataContractSchemaB = CreateDataContractSchema();
        _distinctDataContractSchemas = new Schema[DistinctDataContractCount];
        for (var index = 0; index < _distinctDataContractSchemas.Length; index++)
        {
            var schema = CreateDataContractSchema($"owner-{index}");
            _distinctDataContractSchemas[index] = schema;
            await _distinctDataContractCache.ResolveAsync(
                "data-contract-value",
                schema,
                index,
                static (schemaId, _, _) => Task.FromResult(schemaId),
                CancellationToken.None).ConfigureAwait(false);
        }
        _referencedSchema = CreateReferencedSchema();
        _context = new SerializationContext
        {
            Topic = "schema-preparation-benchmark",
            Component = SerializationComponent.Value
        };
        _overflowContextA = new SerializationContext
        {
            Topic = "schema-preparation-overflow-a",
            Component = SerializationComponent.Value
        };
        _overflowContextB = new SerializationContext
        {
            Topic = "schema-preparation-overflow-b",
            Component = SerializationComponent.Value
        };
        _overflowContextC = new SerializationContext
        {
            Topic = "schema-preparation-overflow-c",
            Component = SerializationComponent.Value
        };

        await _genericSerializer.PrepareAsync(42, _context).ConfigureAwait(false);
        await _jsonSerializer.PrepareAsync(_jsonValue, _context).ConfigureAwait(false);
        for (var index = 0; index < SubjectSchemaIdCache.MaxCachedEntries; index++)
        {
            await _genericOverflowSerializer.PrepareAsync(
                42,
                new SerializationContext
                {
                    Topic = $"schema-preparation-seed-{index}",
                    Component = SerializationComponent.Value
                }).ConfigureAwait(false);
        }
        await _genericOverflowSerializer.PrepareAsync(42, _overflowContextA).ConfigureAwait(false);
        await _genericOverflowSerializer.PrepareAsync(42, _overflowContextB).ConfigureAwait(false);
        await _genericOverflowSerializer.PrepareAsync(42, _overflowContextC).ConfigureAwait(false);
        await _equivalentDataContractCache.ResolveAsync(
            "data-contract-value",
            _dataContractSchemaA,
            0,
            static (_, _, _) => Task.FromResult(1),
            CancellationToken.None).ConfigureAwait(false);
        await _referencedSchemaCache.ResolveAsync(
            "referenced-value",
            _referencedSchema,
            0,
            static (_, _, _) => Task.FromResult(1),
            CancellationToken.None).ConfigureAwait(false);
        var genericDestination = _genericDestination;
        _genericSerializer.Serialize(42, ref genericDestination, _context);
        var jsonDestination = _jsonDestination;
        _jsonSerializer.Serialize(_jsonValue, ref jsonDestination, _context);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _genericSerializer.DisposeAsync().ConfigureAwait(false);
        await _genericOverflowSerializer.DisposeAsync().ConfigureAwait(false);
        await _jsonSerializer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public ValueTask PrepareGenericCached() => _genericSerializer.PrepareAsync(42, _context);

    [Benchmark]
    public ValueTask PrepareGenericAlternatingAfterSubjectCacheTurnover()
    {
        var context = (_overflowContextIndex++ & 1) == 0
            ? _overflowContextA
            : _overflowContextB;
        return _genericOverflowSerializer.PrepareAsync(42, context);
    }

    [Benchmark]
    public ValueTask PrepareGenericThreeWayAfterSubjectCacheTurnover()
    {
        var context = (_overflowContextIndex++ % 3) switch
        {
            0 => _overflowContextA,
            1 => _overflowContextB,
            _ => _overflowContextC
        };
        return _genericOverflowSerializer.PrepareAsync(42, context);
    }

    [Benchmark]
    public ValueTask<int> ResolveEquivalentDataContractSchema()
    {
        var schema = (_equivalentDataContractIndex++ & 1) == 0
            ? _dataContractSchemaA
            : _dataContractSchemaB;
        return _equivalentDataContractCache.ResolveAsync(
            "data-contract-value",
            schema,
            0,
            static (_, _, _) => Task.FromResult(1),
            CancellationToken.None);
    }

    [Benchmark]
    public ValueTask<int> ResolveDistinctDataContractSchema()
    {
        var schema = _distinctDataContractSchemas[
            _distinctDataContractIndex++ & (DistinctDataContractCount - 1)];
        return _distinctDataContractCache.ResolveAsync(
            "data-contract-value",
            schema,
            0,
            static (_, _, _) => Task.FromResult(0),
            CancellationToken.None);
    }

    [Benchmark]
    public ValueTask<int> ResolveReferencedSchemaCached() =>
        _referencedSchemaCache.ResolveAsync(
            "referenced-value",
            _referencedSchema,
            0,
            static (_, _, _) => Task.FromResult(1),
            CancellationToken.None);

    [Benchmark]
    public void SerializeGenericPrepared()
    {
        _genericDestination.Clear();
        var destination = _genericDestination;
        _genericSerializer.Serialize(42, ref destination, _context);
    }

    [Benchmark]
    public ValueTask PrepareJsonCached() => _jsonSerializer.PrepareAsync(_jsonValue, _context);

    [Benchmark]
    public void SerializeJsonPrepared()
    {
        _jsonDestination.Clear();
        var destination = _jsonDestination;
        _jsonSerializer.Serialize(_jsonValue, ref destination, _context);
    }

    private sealed class BenchmarkPayload
    {
        public int Id { get; init; }
    }

    private static Schema CreateDataContractSchema(string owner = "payments") =>
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["$.id"] = new HashSet<string>(["PII"], StringComparer.Ordinal)
                },
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["owner"] = owner
                },
                Sensitive = new HashSet<string>(["owner"], StringComparer.Ordinal)
            },
            RuleSet = new SchemaRuleSet
            {
                EncodingRules =
                [
                    new SchemaRule
                    {
                        Name = "encrypt-id",
                        Kind = SchemaRuleKind.Transform,
                        Mode = SchemaRuleMode.WriteRead,
                        Type = "ENCRYPT",
                        Tags = new HashSet<string>(["PII"], StringComparer.Ordinal),
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["encrypt.kek.name"] = "orders-kek"
                        }
                    }
                ]
            }
        };

    private static Schema CreateReferencedSchema()
    {
        var references = new SchemaReference[32];
        for (var index = 0; index < references.Length; index++)
        {
            references[index] = new SchemaReference
            {
                Name = $"dependency-{index}.proto",
                Subject = $"dependency-{index}.proto",
                Version = index + 1
            };
        }

        return new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = "root",
            References = references
        };
    }

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient
    {
        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
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
