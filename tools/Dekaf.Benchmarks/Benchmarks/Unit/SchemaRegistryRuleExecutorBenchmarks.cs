using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards the Schema Registry rule executor's no-rules fast path.
/// </summary>
[MemoryDiagnoser]
public class SchemaRegistryRuleExecutorBenchmarks
{
    private readonly byte[] _payload = "benchmark-payload"u8.ToArray();
    private readonly SchemaRegistryRuleExecutor _executor = new([]);
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

    [Benchmark(Baseline = true)]
    public ReadOnlyMemory<byte> SchemaWithoutRules() =>
        _executor.TransformSerializedPayload(_payload, _schemaWithoutRules);

    [Benchmark]
    public ReadOnlyMemory<byte> EmptyRuleSet() =>
        _executor.TransformSerializedPayload(_payload, _emptyRuleSet);

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
}
