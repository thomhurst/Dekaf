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
                EncodingRules = []
            }
        });
    private readonly SchemaRegistryRuleContext _activeDomainRule = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
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

    [GlobalSetup]
    public void WarmHandlerContextPool()
    {
        _executor.TransformSerializedPayload(_payload, _activeDomainRule);
        _multipleHandlerExecutor.TransformSerializedPayload(_payload, _activeDomainRule);
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
}
