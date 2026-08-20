using System.Buffers;
using System.Buffers.Binary;
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

    private const string InlineRulesJsonSchema = """
        {
          "type": "object",
          "confluent:rules": [
            { "name": "valid", "expr": "this.id > 0 && this.name.startsWith('bench')" }
          ],
          "properties": {
            "id": { "type": "integer" },
            "name": {
              "type": "string",
              "confluent:rules": [{ "name": "name", "expr": "size(this) > 0" }]
            }
          },
          "required": ["id", "name"]
        }
        """;

    private ArrayBufferWriter<byte> _disabledDestination = new(256);
    private ArrayBufferWriter<byte> _enabledDestination = new(256);
    private ArrayBufferWriter<byte> _inlineRulesDestination = new(256);
    private readonly BenchmarkPayload _value = new(7, "benchmark");
    private JsonSchemaRegistrySerializer<BenchmarkPayload> _disabledSerializer = null!;
    private JsonSchemaRegistrySerializer<BenchmarkPayload> _enabledSerializer = null!;
    private JsonSchemaRegistrySerializer<BenchmarkPayload> _inlineRulesSerializer = null!;
    private JsonSchemaRegistryDeserializer<BenchmarkPayload> _disabledDeserializer = null!;
    private JsonSchemaRegistryDeserializer<BenchmarkPayload> _enabledDeserializer = null!;
    private JsonSchemaRegistryDeserializer<BenchmarkPayload> _inlineRulesDeserializer = null!;
    private StreamingJsonSchemaValidatorFactory _validatorFactory = null!;
    private Schema _validatorSchema = null!;
    private ReadOnlyMemory<byte> _wirePayload;
    private ReadOnlyMemory<byte> _alternateWirePayload;
    private ReadOnlyMemory<byte> _inlineRulesWirePayload;
    private ReadOnlyMemory<byte> _inlineRulesJsonPayload;
    private IJsonSchemaValidator _inlineRulesValidator = null!;
    private SerializationContext _context;
    private int _alternateSchemaIndex;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new BenchmarkSchemaRegistryClient();
        _validatorFactory = new StreamingJsonSchemaValidatorFactory(registry);
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
            jsonOptions: null,
            validationOptions: validation);
        _disabledDeserializer = new JsonSchemaRegistryDeserializer<BenchmarkPayload>(registry);
        _enabledDeserializer = new JsonSchemaRegistryDeserializer<BenchmarkPayload>(
            registry,
            jsonOptions: null,
            validationOptions: validation);

        var inlineRulesRegistry = new BenchmarkSchemaRegistryClient();
        var inlineRulesFactory = new StreamingJsonSchemaValidatorFactory(inlineRulesRegistry);
        var inlineRulesValidation = new JsonSchemaValidationOptions
        {
            ValidatorFactory = inlineRulesFactory,
            Mode = JsonSchemaValidationMode.None,
            ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
        };
        _inlineRulesSerializer = new JsonSchemaRegistrySerializer<BenchmarkPayload>(
            inlineRulesRegistry,
            InlineRulesJsonSchema,
            jsonOptions: null,
            validationOptions: inlineRulesValidation);
        _inlineRulesDeserializer = new JsonSchemaRegistryDeserializer<BenchmarkPayload>(
            inlineRulesRegistry,
            jsonOptions: null,
            validationOptions: inlineRulesValidation);

        _disabledSerializer.Serialize(_value, ref _disabledDestination, _context);
        _wirePayload = _disabledDestination.WrittenMemory.ToArray();
        var alternateWirePayload = _wirePayload.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(alternateWirePayload.AsSpan(1, 4), 2);
        _alternateWirePayload = alternateWirePayload;
        registry.AddSchema(2, new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = JsonSchema
        });
        _disabledDestination.Clear();
        _enabledSerializer.Serialize(_value, ref _enabledDestination, _context);
        _enabledDestination.Clear();
        _inlineRulesSerializer.Serialize(_value, ref _inlineRulesDestination, _context);
        _inlineRulesWirePayload = _inlineRulesDestination.WrittenMemory.ToArray();
        _inlineRulesJsonPayload = _inlineRulesWirePayload[5..];
        _inlineRulesValidator = inlineRulesFactory.GetOrCreate(inlineRulesRegistry.GetSchema(1));
        _inlineRulesValidator.ValidateRules(_inlineRulesJsonPayload, 1, failFast: false);
        _inlineRulesDestination.Clear();
        _ = _disabledDeserializer.Deserialize(_wirePayload, _context);
        _ = _enabledDeserializer.Deserialize(_wirePayload, _context);
        _ = _enabledDeserializer.Deserialize(_alternateWirePayload, _context);
        _ = _inlineRulesDeserializer.Deserialize(_inlineRulesWirePayload, _context);
        _ = _validatorFactory.GetOrCreate(_validatorSchema);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _disabledSerializer.DisposeAsync();
        await _enabledSerializer.DisposeAsync();
        await _inlineRulesSerializer.DisposeAsync();
        await _disabledDeserializer.DisposeAsync();
        await _enabledDeserializer.DisposeAsync();
        await _inlineRulesDeserializer.DisposeAsync();
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
    public void SerializeInlineRulesEnabled()
    {
        _inlineRulesDestination.Clear();
        _inlineRulesSerializer.Serialize(_value, ref _inlineRulesDestination, _context);
    }

    [Benchmark]
    public BenchmarkPayload DeserializeValidationDisabled() =>
        _disabledDeserializer.Deserialize(_wirePayload, _context);

    [Benchmark]
    public BenchmarkPayload DeserializeValidationEnabled() =>
        _enabledDeserializer.Deserialize(_wirePayload, _context);

    [Benchmark]
    public BenchmarkPayload DeserializeInlineRulesEnabled() =>
        _inlineRulesDeserializer.Deserialize(_inlineRulesWirePayload, _context);

    [Benchmark]
    public void ValidateInlineRules() =>
        _inlineRulesValidator.ValidateRules(_inlineRulesJsonPayload, 1, failFast: false);

    [Benchmark]
    public BenchmarkPayload DeserializeValidationEnabledAlternatingSchemas()
    {
        var payload = (_alternateSchemaIndex++ & 1) == 0
            ? _wirePayload
            : _alternateWirePayload;
        return _enabledDeserializer.Deserialize(payload, _context);
    }

    [Benchmark]
    public IJsonSchemaValidator GetCachedValidator() =>
        _validatorFactory.GetOrCreate(_validatorSchema);

    public sealed record BenchmarkPayload(int Id, string Name);

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient, ISchemaRegistryCache
    {
        private readonly Dictionary<int, Schema> _schemas = [];

        public void AddSchema(int id, Schema schema) => _schemas[id] = schema;

        public Schema GetSchema(int id) => _schemas[id];

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default)
        {
            _schemas[1] = schema;
            return Task.FromResult(1);
        }

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_schemas[id]);

        public bool TryGetCachedSchema(int id, out Schema schema) =>
            _schemas.TryGetValue(id, out schema!);

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
