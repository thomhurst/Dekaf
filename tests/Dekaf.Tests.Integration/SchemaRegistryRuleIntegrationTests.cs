using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Serialization;
using Avro.Generic;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Integration.Protos;
using Google.Protobuf;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryRuleIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task RegisteredDomainRules_ProduceAndConsume_RoundTrip()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var calls = new ConcurrentQueue<string>();
        var ruleExecutor = new SchemaRegistryRuleExecutor(
        [
            new CelSchemaRegistryRuleHandler(),
            new XorRuleHandler("DOMAIN-XOR", 0x25, calls)
        ]);
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "string", "title": "DomainRulePayload" }""",
            RuleSet = new SchemaRuleSet
            {
                DomainRules =
                [
                    new SchemaRule
                    {
                        Name = "subject-condition",
                        Kind = SchemaRuleKind.Condition,
                        Mode = SchemaRuleMode.WriteRead,
                        Type = "CEL",
                        Expr = "subject == \"DomainRulePayload\""
                    },
                    CreateRule("domain-xor", "DOMAIN-XOR")
                ]
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
            subjectNameStrategy: SubjectNameStrategy.RecordName,
            ruleExecutor: ruleExecutor);
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registryClient,
            static (ReadOnlyMemory<byte> payload, Schema _) => Encoding.UTF8.GetString(payload.Span),
            new SchemaRegistryDeserializerConfig
            {
                SubjectNameStrategy = SubjectNameStrategy.RecordName
            },
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

        var registered = await registryClient.GetSchemaBySubjectAsync("DomainRulePayload");
        var registeredById = await registryClient.GetSchemaAsync(registered.Id);
        await Assert.That(consumed).IsEqualTo("domain-rule-payload");
        await Assert.That(registeredById.RuleSet!.DomainRules).Count().IsEqualTo(2);
        await Assert.That(calls).IsEquivalentTo([
            "Write:domain-xor",
            "Read:domain-xor"
        ]);
    }

    [Test]
    public async Task RegisteredWriteRules_RegistryResolutionModes_JsonAvroAndProtobufExecute()
    {
        var jsonTopic = await testInfra.CreateTestTopicAsync();
        var avroTopic = await testInfra.CreateTestTopicAsync();
        var protobufTopic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var calls = new ConcurrentQueue<string>();
        var ruleExecutor = new SchemaRegistryRuleExecutor(
        [
            new XorRuleHandler("DOMAIN-XOR", 0x25, calls)
        ]);

        const string jsonSchemaText = """{ "type": "string", "title": "JsonWriteRule" }""";
        await registryClient.RegisterSchemaAsync(
            $"{jsonTopic}-value",
            CreateSchema(SchemaType.Json, jsonSchemaText));
        await using var jsonSerializer = new JsonSchemaRegistrySerializer<string>(
            registryClient,
            jsonSchemaText,
            SchemaRegistryRuleJsonContext.Default.String,
            autoRegisterSchemas: false,
            ruleExecutor: ruleExecutor);

        var jsonOutput = new ArrayBufferWriter<byte>();
        jsonSerializer.Serialize("json-payload", ref jsonOutput, CreateContext(jsonTopic));

        const string avroSchemaText = """
            {
              "type": "record",
              "name": "AvroWriteRule",
              "namespace": "Dekaf.Tests.Integration",
              "fields": [{ "name": "value", "type": "string" }]
            }
            """;
        var avroSchema = (Avro.RecordSchema)AvroSchema.Parse(avroSchemaText);
        await registryClient.RegisterSchemaAsync(
            $"{avroTopic}-value",
            CreateSchema(SchemaType.Avro, avroSchema.ToString()));
        await using var avroSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registryClient,
            new AvroSerializerConfig
            {
                AutoRegisterSchemas = false,
                RuleExecutor = ruleExecutor
            });
        var record = new GenericRecord(avroSchema);
        record.Add("value", "avro-payload");

        await using var avroProducer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(avroSerializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await avroProducer.ProduceAsync(new ProducerMessage<string, GenericRecord>
        {
            Topic = avroTopic,
            Key = "key",
            Value = record
        });

        await using var latestAvroSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registryClient,
            new AvroSerializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = ruleExecutor
            });
        var latestAvroOutput = new ArrayBufferWriter<byte>();
        latestAvroSerializer.Serialize(record, ref latestAvroOutput, CreateContext(avroTopic));

        await registryClient.RegisterSchemaAsync(
            $"{protobufTopic}-value",
            CreateSchema(
                SchemaType.Protobuf,
                SchemaRuleMessage.Descriptor.File.SerializedData.ToBase64()));
        await using var protobufSerializer = new ProtobufSchemaRegistrySerializer<SchemaRuleMessage>(
            registryClient,
            new ProtobufSerializerConfig
            {
                RuleExecutor = ruleExecutor
            });
        var protobufOutput = new ArrayBufferWriter<byte>();
        protobufSerializer.Serialize(
            new SchemaRuleMessage { Value = "protobuf-payload" },
            ref protobufOutput,
            CreateContext(protobufTopic));

        await Assert.That(calls).IsEquivalentTo([
            "Write:domain-xor",
            "Write:domain-xor",
            "Write:domain-xor",
            "Write:domain-xor"
        ]);
    }

    private static Schema CreateSchema(SchemaType schemaType, string schemaString) =>
        new()
        {
            SchemaType = schemaType,
            SchemaString = schemaString,
            RuleSet = new SchemaRuleSet
            {
                DomainRules = [CreateRule("domain-xor", "DOMAIN-XOR")]
            }
        };

    private static SerializationContext CreateContext(string topic) =>
        new()
        {
            Topic = topic,
            Component = SerializationComponent.Value
        };

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

[JsonSerializable(typeof(string))]
internal sealed partial class SchemaRegistryRuleJsonContext : JsonSerializerContext;
