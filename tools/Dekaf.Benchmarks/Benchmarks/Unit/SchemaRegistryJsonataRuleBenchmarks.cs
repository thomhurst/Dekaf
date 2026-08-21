using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Jsonata;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards no-rule and disabled-rule allocations while reporting enabled JSONata cost separately.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class SchemaRegistryJsonataRuleBenchmarks
{
    private static readonly byte[] Payload = """{"left":20,"right":22}"""u8.ToArray();

    private readonly SchemaRegistryRuleExecutor _executor =
        new([new JsonataSchemaRegistryRuleHandler()]);
    private readonly SchemaRegistryRuleContext _withoutRules = CreateContext(rule: null);
    private readonly SchemaRegistryRuleContext _disabledRule = CreateContext(CreateRule(disabled: true));
    private readonly SchemaRegistryRuleContext _activeRule = CreateContext(CreateRule(disabled: false));

    [GlobalSetup]
    public void WarmEnabledPath() => _ = ActiveJsonataRule();

    [Benchmark(Baseline = true)]
    public ReadOnlyMemory<byte> NoJsonataRule() =>
        _executor.TransformSerializedPayload(Payload, _withoutRules);

    [Benchmark]
    public ReadOnlyMemory<byte> DisabledJsonataRule() =>
        _executor.TransformSerializedPayload(Payload, _disabledRule);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveJsonataRule() =>
        _executor.TransformSerializedPayload(Payload, _activeRule);

    private static SchemaRegistryRuleContext CreateContext(SchemaRule? rule) =>
        new()
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value,
            SchemaId = 1,
            Subject = "benchmark-topic-value",
            Schema = new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = "{}",
                RuleSet = rule is null
                    ? null
                    : new SchemaRuleSet
                    {
                        DomainRules = [rule],
                        HasFixedRuleCollections = true
                    }
            },
            PayloadFormat = SchemaRegistryPayloadFormat.Json
        };

    private static SchemaRule CreateRule(bool disabled) =>
        new()
        {
            Name = "jsonata-benchmark",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Write,
            Type = JsonataSchemaRegistryRuleHandler.RuleType,
            Expr = "$merge([$, {'sum': left + right}])",
            Disabled = disabled
        };
}
