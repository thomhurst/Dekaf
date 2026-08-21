using System.Buffers;
using System.Buffers.Binary;
using System.Text;
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

    private const string NestedInlineRulesJsonSchema = """
        {
          "properties": { "child": {
            "properties": { "child": {
              "properties": { "child": {
                "properties": { "child": {
                  "properties": { "child": {
                    "properties": { "child": {
                      "properties": { "child": {
                        "properties": { "child": {
                          "properties": { "value": {
                            "type": "integer",
                            "confluent:rules": [{ "name": "positive", "expr": "this > 0" }]
                          } }
                        } }
                      } }
                    } }
                  } }
                } }
              } }
            } }
          } }
        }
        """;

    private const string StructuralEqualityJsonSchema = """
        {
          "confluent:rules": [{
            "name": "equal",
            "expr": "this.left == this.right && this.values == this.expected"
          }]
        }
        """;

    private const string SiblingInlineRulesJsonSchema = """
        {
          "confluent:rules": [
            { "name": "a", "expr": "this.a > 0" },
            { "name": "b", "expr": "this.b > 0" },
            { "name": "c", "expr": "this.c > 0" },
            { "name": "d", "expr": "this.d > 0" },
            { "name": "e", "expr": "this.e > 0" },
            { "name": "f", "expr": "this.f > 0" },
            { "name": "g", "expr": "this.g > 0" },
            { "name": "h", "expr": "this.h > 0" }
          ]
        }
        """;

    private const string NestedMemberInlineRulesJsonSchema = """
        {
          "confluent:rules": [
            { "name": "a", "expr": "this.details.a > 0" },
            { "name": "b", "expr": "this.details.b > 0" },
            { "name": "c", "expr": "this.details.c > 0" },
            { "name": "d", "expr": "this.details.d > 0" },
            { "name": "e", "expr": "this.details.e > 0" },
            { "name": "f", "expr": "this.details.f > 0" },
            { "name": "g", "expr": "this.details.g > 0" },
            { "name": "h", "expr": "this.details.h > 0" }
          ]
        }
        """;

    private const string DuplicatePropertyInlineRulesJsonSchema = """
        {
          "properties": {
            "name": {
              "confluent:rules": [{ "name": "final-value", "expr": "this == 'ok'" }]
            }
          }
        }
        """;

    private const string MapSizeInlineRulesJsonSchema = """
        {
          "confluent:rules": [{ "name": "map-size", "expr": "size(this) == 1" }]
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
    private ReadOnlyMemory<byte> _nestedInlineRulesJsonPayload;
    private IJsonSchemaValidator _nestedInlineRulesValidator = null!;
    private ReadOnlyMemory<byte> _nestedCompositionInlineRulesJsonPayload;
    private IJsonSchemaValidator _nestedCompositionInlineRulesValidator = null!;
    private ReadOnlyMemory<byte> _shallowCompositionInlineRulesJsonPayload;
    private IJsonSchemaValidator _shallowCompositionInlineRulesValidator = null!;
    private ReadOnlyMemory<byte> _nestedAllOfInlineRulesJsonPayload;
    private IJsonSchemaValidator _nestedAllOfInlineRulesValidator = null!;
    private ReadOnlyMemory<byte> _ruleBearingAllOfChainJsonPayload;
    private IJsonSchemaValidator _ruleBearingAllOfChainValidator = null!;
    private ReadOnlyMemory<byte> _structuralEqualityJsonPayload;
    private ReadOnlyMemory<byte> _deepStructuralEqualityJsonPayload;
    private IJsonSchemaValidator _structuralEqualityValidator = null!;
    private ReadOnlyMemory<byte> _siblingInlineRulesJsonPayload;
    private IJsonSchemaValidator _siblingInlineRulesValidator = null!;
    private ReadOnlyMemory<byte> _nestedMemberInlineRulesJsonPayload;
    private ReadOnlyMemory<byte> _duplicateNestedMemberInlineRulesJsonPayload;
    private ReadOnlyMemory<byte> _manyDuplicateNestedMemberInlineRulesJsonPayload;
    private ReadOnlyMemory<byte> _manyDistinctDuplicateInlineRulesJsonPayload;
    private IJsonSchemaValidator _manyDistinctDuplicateInlineRulesValidator = null!;
    private IJsonSchemaValidator _nestedMemberInlineRulesValidator = null!;
    private ReadOnlyMemory<byte> _duplicatePropertyInlineRulesJsonPayload;
    private IJsonSchemaValidator _duplicatePropertyInlineRulesValidator = null!;
    private ReadOnlyMemory<byte> _duplicateMapSizeInlineRulesJsonPayload;
    private IJsonSchemaValidator _mapSizeInlineRulesValidator = null!;
    private ReadOnlyMemory<byte> _terminalPrefixInlineRulesJsonPayload;
    private IJsonSchemaValidator _terminalPrefixInlineRulesValidator = null!;
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
        _nestedInlineRulesJsonPayload =
            """{"child":{"child":{"child":{"child":{"child":{"child":{"child":{"child":{"value":7}}}}}}}}}"""u8
                .ToArray();
        _nestedInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = NestedInlineRulesJsonSchema
        });
        _nestedInlineRulesValidator.ValidateRules(_nestedInlineRulesJsonPayload, 2, failFast: false);
        var (nestedCompositionSchema, nestedCompositionPayload) = CreateNestedCompositionRule(depth: 12);
        _nestedCompositionInlineRulesJsonPayload = nestedCompositionPayload;
        _nestedCompositionInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = nestedCompositionSchema
        });
        _nestedCompositionInlineRulesValidator.ValidateRules(
            _nestedCompositionInlineRulesJsonPayload,
            7,
            failFast: false);
        var (shallowCompositionSchema, shallowCompositionPayload) = CreateNestedCompositionRule(depth: 1);
        _shallowCompositionInlineRulesJsonPayload = shallowCompositionPayload;
        _shallowCompositionInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = shallowCompositionSchema
        });
        _shallowCompositionInlineRulesValidator.ValidateRules(
            _shallowCompositionInlineRulesJsonPayload,
            8,
            failFast: false);
        var (nestedAllOfSchema, nestedAllOfPayload) = CreateNestedCompositionRule(depth: 12, "allOf");
        _nestedAllOfInlineRulesJsonPayload = nestedAllOfPayload;
        _nestedAllOfInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = nestedAllOfSchema
        });
        _nestedAllOfInlineRulesValidator.ValidateRules(
            _nestedAllOfInlineRulesJsonPayload,
            9,
            failFast: false);
        var (ruleBearingAllOfSchema, ruleBearingAllOfPayload) =
            CreateRuleBearingAllOfChain(depth: 24, itemCount: 256);
        _ruleBearingAllOfChainJsonPayload = ruleBearingAllOfPayload;
        _ruleBearingAllOfChainValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = ruleBearingAllOfSchema
        });
        _ruleBearingAllOfChainValidator.ValidateRules(
            _ruleBearingAllOfChainJsonPayload,
            10,
            failFast: false);
        _structuralEqualityJsonPayload =
            """{"left":{"id":1,"name":"bench"},"right":{"name":"bench","id":1.0},"values":[1,"a"],"expected":[1.0,"\u0061"]}"""u8
                .ToArray();
        _structuralEqualityValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = StructuralEqualityJsonSchema
        });
        _structuralEqualityValidator.ValidateRules(_structuralEqualityJsonPayload, 3, failFast: false);
        _deepStructuralEqualityJsonPayload = CreateDeepStructuralEqualityPayload(depth: 48);
        _structuralEqualityValidator.ValidateRules(
            _deepStructuralEqualityJsonPayload,
            3,
            failFast: false);
        _siblingInlineRulesJsonPayload =
            """{"a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7,"h":8}"""u8.ToArray();
        _siblingInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = SiblingInlineRulesJsonSchema
        });
        _siblingInlineRulesValidator.ValidateRules(_siblingInlineRulesJsonPayload, 4, failFast: false);
        _nestedMemberInlineRulesJsonPayload =
            """{"details":{"a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7,"h":8}}"""u8.ToArray();
        _nestedMemberInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = NestedMemberInlineRulesJsonSchema
        });
        _nestedMemberInlineRulesValidator.ValidateRules(
            _nestedMemberInlineRulesJsonPayload,
            5,
            failFast: false);
        _duplicateNestedMemberInlineRulesJsonPayload =
            """{"details":{"a":0,"b":0,"c":0,"d":0,"e":0,"f":0,"g":0,"h":0},"details":{"a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7,"h":8}}"""u8
                .ToArray();
        _nestedMemberInlineRulesValidator.ValidateRules(
            _duplicateNestedMemberInlineRulesJsonPayload,
            5,
            failFast: false);
        _manyDuplicateNestedMemberInlineRulesJsonPayload =
            CreateDuplicateNestedMemberPayload(duplicateCount: 32);
        _nestedMemberInlineRulesValidator.ValidateRules(
            _manyDuplicateNestedMemberInlineRulesJsonPayload,
            5,
            failFast: false);
        var (manyDistinctDuplicateSchema, manyDistinctDuplicatePayload) =
            CreateManyDistinctDuplicatePropertiesRule(propertyCount: 32);
        _manyDistinctDuplicateInlineRulesJsonPayload = manyDistinctDuplicatePayload;
        _manyDistinctDuplicateInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = manyDistinctDuplicateSchema
        });
        _manyDistinctDuplicateInlineRulesValidator.ValidateRules(
            _manyDistinctDuplicateInlineRulesJsonPayload,
            11,
            failFast: false);
        _duplicatePropertyInlineRulesJsonPayload =
            """{"name":"bad","name":"ok"}"""u8.ToArray();
        _duplicatePropertyInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = DuplicatePropertyInlineRulesJsonSchema
        });
        _duplicatePropertyInlineRulesValidator.ValidateRules(
            _duplicatePropertyInlineRulesJsonPayload,
            9,
            failFast: false);
        _duplicateMapSizeInlineRulesJsonPayload = """{"a":1,"a":2}"""u8.ToArray();
        _mapSizeInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = MapSizeInlineRulesJsonSchema
        });
        _mapSizeInlineRulesValidator.ValidateRules(
            _duplicateMapSizeInlineRulesJsonPayload,
            10,
            failFast: false);
        var (terminalPrefixSchema, terminalPrefixPayload) = CreateTerminalPrefixRule(depth: 32);
        _terminalPrefixInlineRulesJsonPayload = terminalPrefixPayload;
        _terminalPrefixInlineRulesValidator = inlineRulesFactory.GetOrCreate(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = terminalPrefixSchema
        });
        _terminalPrefixInlineRulesValidator.ValidateRules(
            _terminalPrefixInlineRulesJsonPayload,
            6,
            failFast: false);
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
    public void ValidateNestedInlineRules() =>
        _nestedInlineRulesValidator.ValidateRules(_nestedInlineRulesJsonPayload, 2, failFast: false);

    [Benchmark]
    public void ValidateNestedCompositionInlineRules() =>
        _nestedCompositionInlineRulesValidator.ValidateRules(
            _nestedCompositionInlineRulesJsonPayload,
            7,
            failFast: false);

    [Benchmark]
    public void ValidateShallowCompositionInlineRules() =>
        _shallowCompositionInlineRulesValidator.ValidateRules(
            _shallowCompositionInlineRulesJsonPayload,
            8,
            failFast: false);

    [Benchmark]
    public void ValidateNestedAllOfInlineRules() =>
        _nestedAllOfInlineRulesValidator.ValidateRules(
            _nestedAllOfInlineRulesJsonPayload,
            9,
            failFast: false);

    [Benchmark]
    public void ValidateRuleBearingAllOfChain() =>
        _ruleBearingAllOfChainValidator.ValidateRules(
            _ruleBearingAllOfChainJsonPayload,
            10,
            failFast: false);

    [Benchmark]
    public void ValidateStructuralEquality() =>
        _structuralEqualityValidator.ValidateRules(_structuralEqualityJsonPayload, 3, failFast: false);

    [Benchmark]
    public void ValidateDeepStructuralEquality() =>
        _structuralEqualityValidator.ValidateRules(
            _deepStructuralEqualityJsonPayload,
            3,
            failFast: false);

    [Benchmark]
    public void ValidateSiblingInlineRules() =>
        _siblingInlineRulesValidator.ValidateRules(_siblingInlineRulesJsonPayload, 4, failFast: false);

    [Benchmark]
    public void ValidateNestedMemberInlineRules() =>
        _nestedMemberInlineRulesValidator.ValidateRules(
            _nestedMemberInlineRulesJsonPayload,
            5,
            failFast: false);

    [Benchmark]
    public void ValidateDuplicateNestedMemberInlineRules() =>
        _nestedMemberInlineRulesValidator.ValidateRules(
            _duplicateNestedMemberInlineRulesJsonPayload,
            5,
            failFast: false);

    [Benchmark]
    public void ValidateManyDuplicateNestedMemberInlineRules() =>
        _nestedMemberInlineRulesValidator.ValidateRules(
            _manyDuplicateNestedMemberInlineRulesJsonPayload,
            5,
            failFast: false);

    [Benchmark]
    public void ValidateManyDistinctDuplicateInlineRules() =>
        _manyDistinctDuplicateInlineRulesValidator.ValidateRules(
            _manyDistinctDuplicateInlineRulesJsonPayload,
            11,
            failFast: false);

    [Benchmark]
    public void ValidateDuplicatePropertyFailFastInlineRules() =>
        _duplicatePropertyInlineRulesValidator.ValidateRules(
            _duplicatePropertyInlineRulesJsonPayload,
            9,
            failFast: true);

    [Benchmark]
    public void ValidateDuplicateMapSizeInlineRules() =>
        _mapSizeInlineRulesValidator.ValidateRules(
            _duplicateMapSizeInlineRulesJsonPayload,
            10,
            failFast: false);

    [Benchmark]
    public void ValidateTerminalPrefixInlineRules() =>
        _terminalPrefixInlineRulesValidator.ValidateRules(
            _terminalPrefixInlineRulesJsonPayload,
            6,
            failFast: false);

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

    private static byte[] CreateDeepStructuralEqualityPayload(int depth)
    {
        var json = new StringBuilder(depth * 24);
        json.Append("{\"left\":");
        AppendNestedValue(json, depth);
        json.Append(",\"right\":");
        AppendNestedValue(json, depth);
        json.Append(",\"values\":[],\"expected\":[]}");
        return Encoding.UTF8.GetBytes(json.ToString());
    }

    private static byte[] CreateDuplicateNestedMemberPayload(int duplicateCount)
    {
        const string invalid = "\"details\":{\"a\":0,\"b\":0,\"c\":0,\"d\":0,\"e\":0,\"f\":0,\"g\":0,\"h\":0},";
        var json = new StringBuilder(duplicateCount * invalid.Length + 80);
        json.Append('{');
        for (var index = 0; index < duplicateCount; index++)
            json.Append(invalid);
        json.Append("\"details\":{\"a\":1,\"b\":2,\"c\":3,\"d\":4,\"e\":5,\"f\":6,\"g\":7,\"h\":8}}");
        return Encoding.UTF8.GetBytes(json.ToString());
    }

    private static (string Schema, byte[] Payload) CreateManyDistinctDuplicatePropertiesRule(int propertyCount)
    {
        var schema = new StringBuilder(propertyCount * 96);
        var payload = new StringBuilder(propertyCount * 20);
        schema.Append("{\"properties\":{");
        payload.Append('{');
        for (var index = 0; index < propertyCount; index++)
        {
            if (index != 0)
            {
                schema.Append(',');
                payload.Append(',');
            }
            schema.Append("\"p").Append(index)
                .Append("\":{\"confluent:rules\":[{\"name\":\"p").Append(index)
                .Append("\",\"expr\":\"this == 1\"}]}");
            payload.Append("\"p").Append(index).Append("\":0");
        }
        schema.Append("}}");
        for (var index = 0; index < propertyCount; index++)
            payload.Append(",\"p").Append(index).Append("\":1");
        payload.Append('}');
        return (schema.ToString(), Encoding.UTF8.GetBytes(payload.ToString()));
    }

    private static void AppendNestedValue(StringBuilder json, int depth)
    {
        for (var index = 0; index < depth; index++)
            json.Append("{\"value\":");
        json.Append('1');
        json.Append('}', depth);
    }

    private static (string Schema, byte[] Payload) CreateTerminalPrefixRule(int depth)
    {
        var expression = new StringBuilder(depth * depth * 4);
        var path = new StringBuilder("this");
        var payload = new StringBuilder(depth * 12);
        for (var index = 0; index < depth; index++)
        {
            path.Append(".child");
            if (expression.Length != 0)
                expression.Append(" && ");
            expression.Append(path).Append(" != null");
            payload.Append("{\"child\":");
        }
        payload.Append("{}");
        payload.Append('}', depth);
        var schema = $$"""
            {
              "confluent:rules": [{ "name": "prefixes", "expr": "{{expression}}" }]
            }
            """;
        return (schema, Encoding.UTF8.GetBytes(payload.ToString()));
    }

    private static (string Schema, byte[] Payload) CreateNestedCompositionRule(
        int depth,
        string keyword = "anyOf")
    {
        var schema = new StringBuilder(depth * 96);
        var payload = new StringBuilder(depth * 12);
        for (var index = 0; index < depth; index++)
        {
            schema.Append("{\"").Append(keyword).Append(
                "\":[{\"type\":\"object\",\"required\":[\"child\"],\"properties\":{\"child\":");
            payload.Append("{\"child\":");
        }
        schema.Append(
            "{\"type\":\"integer\",\"confluent:rules\":[{\"name\":\"positive\",\"expr\":\"this > 0\"}]}");
        for (var index = 0; index < depth; index++)
            schema.Append("}}]}");
        payload.Append('1');
        payload.Append('}', depth);
        return (schema.ToString(), Encoding.UTF8.GetBytes(payload.ToString()));
    }

    private static (string Schema, byte[] Payload) CreateRuleBearingAllOfChain(
        int depth,
        int itemCount)
    {
        var schema = new StringBuilder(depth * 96);
        for (var index = 0; index < depth; index++)
        {
            schema.Append(
                "{\"confluent:rules\":[{\"name\":\"valid\",\"expr\":\"true\"}],\"allOf\":[");
        }
        schema.Append("{}");
        for (var index = 0; index < depth; index++)
            schema.Append("]}");

        var payload = new StringBuilder(itemCount * 4);
        payload.Append("{\"items\":[");
        for (var index = 0; index < itemCount; index++)
        {
            if (index != 0)
                payload.Append(',');
            payload.Append(index);
        }
        payload.Append("]}");
        return (schema.ToString(), Encoding.UTF8.GetBytes(payload.ToString()));
    }

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
