using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards the Schema Registry rule executor's no-rules and active-rule paths.
/// </summary>
[MemoryDiagnoser]
public class SchemaRegistryRuleExecutorBenchmarks
{
    private readonly byte[] _payload = "benchmark-payload"u8.ToArray();
    private readonly SchemaRegistryRuleExecutor _executor = new([PassThroughRuleHandler.Instance]);
    private readonly SchemaRegistryRuleExecutor _celExecutor = new([new CelSchemaRegistryRuleHandler()]);
    private readonly IJsonSchemaValidator _validator = NoOpJsonSchemaValidator.Instance;
    private readonly SchemaRegistryRuleExecutor _multipleHandlerExecutor = new(
    [
        new PassThroughRuleHandler("A"),
        new PassThroughRuleHandler("B"),
        new PassThroughRuleHandler("C"),
        PassThroughRuleHandler.Instance
    ]);
    private readonly SchemaRegistryRuleContext _schemaWithoutRules = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}"
        });
    private readonly SchemaRegistryRuleContext _emptyRuleSet = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                DomainRules = [],
                EncodingRules = [],
                HasFixedRuleCollections = true
            }
        });
    private readonly SchemaRegistryRuleContext _activeDomainRule = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                HasFixedRuleCollections = true,
                DomainRules =
                [
                    new SchemaRule
                    {
                        Name = "benchmark-rule",
                        Kind = SchemaRuleKind.Transform,
                        Mode = SchemaRuleMode.Write,
                        Type = PassThroughRuleHandler.RuleType
                    }
                ]
            }
        });
    private readonly SchemaRegistryRuleContext _activeDomainRuleWithInactiveRules = CreateContext(
        CreatePassThroughSchema(inactiveRuleCount: 32));
    private readonly SchemaRegistryRuleContext _activeDomainAndEncodingRules = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                HasFixedRuleCollections = true,
                DomainRules = [CreatePassThroughRule("domain-rule")],
                EncodingRules = [CreatePassThroughRule("encoding-rule")]
            }
        });
    private readonly SchemaRegistryRuleContext _activeCelCondition = CreateContext(
        CreateCelSchema(
            SchemaRuleKind.Condition,
            "contains(message, \"mark\") && payload == \"benchmark-payload\" && format == \"Json\""));
    private readonly SchemaRegistryRuleContext _activeCelTransform = CreateContext(
        CreateCelSchema(SchemaRuleKind.Transform, "metadata(\"replacement\")"));

    [GlobalSetup]
    public void WarmHandlerContextPool()
    {
        _executor.TransformSerializedPayload(_payload, _activeDomainRule);
        _executor.TransformSerializedPayload(_payload, _activeDomainRuleWithInactiveRules);
        _executor.TransformSerializedPayload(_payload, _activeDomainAndEncodingRules, _validator, schemaId: 1);
        _multipleHandlerExecutor.TransformSerializedPayload(_payload, _activeDomainRule);
        _celExecutor.TransformSerializedPayload(_payload, _activeCelCondition);
        _celExecutor.TransformSerializedPayload(_payload, _activeCelTransform);
    }

    [Benchmark(Baseline = true)]
    public ReadOnlyMemory<byte> SchemaWithoutRules() =>
        _executor.TransformSerializedPayload(_payload, _schemaWithoutRules);

    [Benchmark]
    public ReadOnlyMemory<byte> EmptyRuleSet() =>
        _executor.TransformSerializedPayload(_payload, _emptyRuleSet);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveDomainRule() =>
        _executor.TransformSerializedPayload(_payload, _activeDomainRule);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveDomainRuleMultipleHandlers() =>
        _multipleHandlerExecutor.TransformSerializedPayload(_payload, _activeDomainRule);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveDomainRuleAfterInactiveRules() =>
        _executor.TransformSerializedPayload(_payload, _activeDomainRuleWithInactiveRules);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveDomainAndEncodingRulesWithValidation() =>
        _executor.TransformSerializedPayload(_payload, _activeDomainAndEncodingRules, _validator, schemaId: 1);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveCelDomainCondition() =>
        _celExecutor.TransformSerializedPayload(_payload, _activeCelCondition);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveCelDomainTransform() =>
        _celExecutor.TransformSerializedPayload(_payload, _activeCelTransform);

    private static Schema CreateCelSchema(SchemaRuleKind kind, string expression) =>
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["replacement"] = "rewritten-payload"
                }
            },
            RuleSet = new SchemaRuleSet
            {
                HasFixedRuleCollections = true,
                DomainRules =
                [
                    new SchemaRule
                    {
                        Name = "benchmark-cel-rule",
                        Kind = kind,
                        Mode = SchemaRuleMode.Write,
                        Type = "CEL",
                        Expr = expression
                    }
                ]
            }
        };

    private static Schema CreatePassThroughSchema(int inactiveRuleCount)
    {
        var rules = new SchemaRule[inactiveRuleCount + 1];
        for (var i = 0; i < inactiveRuleCount; i++)
        {
            rules[i] = new SchemaRule
            {
                Name = "inactive-rule",
                Kind = SchemaRuleKind.Transform,
                Mode = SchemaRuleMode.Write,
                Type = "MISSING",
                Disabled = true
            };
        }

        rules[^1] = new SchemaRule
        {
            Name = "benchmark-rule",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Write,
            Type = PassThroughRuleHandler.RuleType
        };

        return new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                DomainRules = rules,
                HasFixedRuleCollections = true
            }
        };
    }

    private static SchemaRule CreatePassThroughRule(string name) =>
        new()
        {
            Name = name,
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Write,
            Type = PassThroughRuleHandler.RuleType
        };

    private static SchemaRegistryRuleContext CreateContext(Schema schema) =>
        new()
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value,
            SchemaId = 1,
            Subject = "benchmark-topic-value",
            Schema = schema,
            PayloadFormat = SchemaRegistryPayloadFormat.Json
        };

    private sealed class PassThroughRuleHandler : ISchemaRegistryRuleHandler
    {
        public const string RuleType = "BENCHMARK";

        public static PassThroughRuleHandler Instance { get; } = new(RuleType);

        public PassThroughRuleHandler(string type) => Type = type;

        public string Type { get; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => payload;
    }

    private sealed class NoOpJsonSchemaValidator : IJsonSchemaValidator
    {
        public static NoOpJsonSchemaValidator Instance { get; } = new();

        public void Validate(ReadOnlySpan<byte> payload, int schemaId)
        {
        }
    }
}
