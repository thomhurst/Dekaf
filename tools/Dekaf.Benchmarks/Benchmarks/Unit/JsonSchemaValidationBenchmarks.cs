using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Json;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures disabled and enabled JSON Schema validation steady-state costs.
/// Registration and validator compilation are completed during setup.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class JsonSchemaValidationBenchmarks
{
    private const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "id": { "type": "integer" },
            "name": { "type": "string" }
          },
          "required": ["id", "name"]
        }
        """;

    private ArrayBufferWriter<byte> _disabledDestination = new(256);
    private ArrayBufferWriter<byte> _enabledDestination = new(256);
    private readonly BenchmarkPayload _value = new(7, "benchmark");
    private JsonSchemaRegistrySerializer<BenchmarkPayload> _disabledSerializer = null!;
    private JsonSchemaRegistrySerializer<BenchmarkPayload> _enabledSerializer = null!;
    private JsonSchemaRegistryDeserializer<BenchmarkPayload> _disabledDeserializer = null!;
    private JsonSchemaRegistryDeserializer<BenchmarkPayload> _enabledDeserializer = null!;
    private JsonSchemaNetValidatorFactory _validatorFactory = null!;
    private Schema _validatorSchema = null!;
    private ReadOnlyMemory<byte> _wirePayload;
    private SerializationContext _context;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new BenchmarkSchemaRegistryClient();
        _validatorFactory = new JsonSchemaNetValidatorFactory(registry);
        _validatorSchema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = JsonSchema
        };
        var validation = new JsonSchemaValidationOptions
        {
            ValidatorFactory = _validatorFactory
        };
        _context = new SerializationContext
        {
            Topic = "json-validation-benchmark",
            Component = SerializationComponent.Value
        };
        _disabledSerializer = new JsonSchemaRegistrySerializer<BenchmarkPayload>(registry, JsonSchema);
        _enabledSerializer = new JsonSchemaRegistrySerializer<BenchmarkPayload>(
            registry,
            JsonSchema,
            validation);
        _disabledDeserializer = new JsonSchemaRegistryDeserializer<BenchmarkPayload>(registry);
        _enabledDeserializer = new JsonSchemaRegistryDeserializer<BenchmarkPayload>(registry, validation);

        _disabledSerializer.Serialize(_value, ref _disabledDestination, _context);
        _wirePayload = _disabledDestination.WrittenMemory.ToArray();
        _disabledDestination.Clear();
        _enabledSerializer.Serialize(_value, ref _enabledDestination, _context);
        _enabledDestination.Clear();
        _ = _disabledDeserializer.Deserialize(_wirePayload, _context);
        _ = _enabledDeserializer.Deserialize(_wirePayload, _context);
        _ = _validatorFactory.GetOrCreate(_validatorSchema);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _disabledSerializer.DisposeAsync();
        await _enabledSerializer.DisposeAsync();
        await _disabledDeserializer.DisposeAsync();
        await _enabledDeserializer.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public void SerializeValidationDisabled()
    {
        _disabledDestination.Clear();
        _disabledSerializer.Serialize(_value, ref _disabledDestination, _context);
    }

    [Benchmark]
    public void SerializeValidationEnabled()
    {
        _enabledDestination.Clear();
        _enabledSerializer.Serialize(_value, ref _enabledDestination, _context);
    }

    [Benchmark]
    public BenchmarkPayload DeserializeValidationDisabled() =>
        _disabledDeserializer.Deserialize(_wirePayload, _context);

    [Benchmark]
    public BenchmarkPayload DeserializeValidationEnabled() =>
        _enabledDeserializer.Deserialize(_wirePayload, _context);

    [Benchmark]
    public IJsonSchemaValidator GetCachedValidator() =>
        _validatorFactory.GetOrCreate(_validatorSchema);

    public sealed record BenchmarkPayload(int Id, string Name);

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient, ISchemaRegistryCache
    {
        private Schema? _schema;

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default)
        {
            _schema = schema;
            return Task.FromResult(1);
        }

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_schema!);

        public bool TryGetCachedSchema(int id, out Schema schema)
        {
            schema = _schema!;
            return schema is not null;
        }

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => RegisterSchemaAsync(subject, schema, cancellationToken);

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
