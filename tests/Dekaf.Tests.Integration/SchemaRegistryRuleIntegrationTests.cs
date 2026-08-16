using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryRuleIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task RegisteredDomainAndEncodingRules_ProduceAndConsume_RoundTrip()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var calls = new ConcurrentQueue<string>();
        var ruleExecutor = new SchemaRegistryRuleExecutor(
        [
            new XorRuleHandler("DOMAIN-XOR", 0x25, calls),
            new XorRuleHandler("ENCODING-XOR", 0x5A, calls)
        ]);
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "string" }""",
            RuleSet = new SchemaRuleSet
            {
                DomainRules = [CreateRule("domain-xor", "DOMAIN-XOR")],
                EncodingRules = [CreateRule("encoding-xor", "ENCODING-XOR")]
            }
        };

        await using var serializer = new SchemaRegistrySerializer<string>(
            registryClient,
            static (value, writer) =>
            {
                var byteCount = Encoding.UTF8.GetByteCount(value);
                Encoding.UTF8.GetBytes(value, writer.GetSpan(byteCount));
                writer.Advance(byteCount);
            },
            () => schema,
            ruleExecutor: ruleExecutor);
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registryClient,
            static (ReadOnlyMemory<byte> payload, Schema _) => Encoding.UTF8.GetString(payload.Span),
            ruleExecutor: ruleExecutor);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(serializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "domain-rule-payload"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"schema-rule-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        string? consumed = null;
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            consumed = message.Value;
            break;
        }

        var registered = await registryClient.GetSchemaBySubjectAsync($"{topic}-value");
        var registeredById = await registryClient.GetSchemaAsync(registered.Id);
        await Assert.That(consumed).IsEqualTo("domain-rule-payload");
        await Assert.That(registeredById.RuleSet!.DomainRules).Count().IsEqualTo(1);
        await Assert.That(registeredById.RuleSet.EncodingRules).Count().IsEqualTo(1);
        await Assert.That(calls).IsEquivalentTo([
            "Write:domain-xor",
            "Write:encoding-xor",
            "Read:encoding-xor",
            "Read:domain-xor"
        ]);
    }

    private static SchemaRule CreateRule(string name, string type) =>
        new()
        {
            Name = name,
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.WriteRead,
            Type = type
        };

    private sealed class XorRuleHandler(
        string type,
        byte mask,
        ConcurrentQueue<string> calls) : ISchemaRegistryRuleHandler
    {
        public string Type => type;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        private ReadOnlyMemory<byte> Transform(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Enqueue($"{context.Direction}:{context.Rule.Name}");
            var result = payload.ToArray();
            for (var i = 0; i < result.Length; i++)
                result[i] ^= mask;

            return result;
        }
    }
}
