using Avro.Generic;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[NotInParallel]
[ClassDataSource<KafkaWithAssociationSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaIdentityFramingIntegrationTests(
    KafkaWithAssociationSchemaRegistryContainer testInfra)
{
    private const string AvroSchemaText = """
        {
            "type": "record",
            "name": "IdentityRecord",
            "namespace": "test",
            "fields": [
                { "name": "id", "type": "int" },
                { "name": "name", "type": "string" }
            ]
        }
        """;

    private const string JsonSchemaText = """
        {
            "type": "object",
            "properties": {
                "OrderId": { "type": "integer" },
                "CustomerName": { "type": "string" },
                "Amount": { "type": "number" }
            },
            "required": ["OrderId", "CustomerName", "Amount"]
        }
        """;

    [Test]
    public async Task Avro_ExplicitIdHeaderFraming_RoundTripsWithoutCallerHeaders()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registry = CreateRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            $"{topic}-value",
            new Schema { SchemaType = SchemaType.Avro, SchemaString = AvroSchemaText });
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registry,
            new AvroSerializerConfig
            {
                UseSchemaId = schemaId,
                AutoRegisterSchemas = false,
                SchemaIdStrategy = SchemaIdSerializerStrategy.Header
            });
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header
            });
        var schema = (Avro.RecordSchema)AvroSchema.Parse(AvroSchemaText);
        var record = new GenericRecord(schema);
        record.Add("id", 84);
        record.Add("name", "Header Integration");

        await using var producer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(serializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.ProduceAsync(topic, "header", record);

        await using var consumer = await Kafka.CreateConsumer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"avro-header-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That((int)message.Value["id"]!).IsEqualTo(84);
            await AssertIdentityHeaderAsync(message.Headers);
            return;
        }

        Assert.Fail("No Avro Schema Registry record was consumed.");
    }

    [Test]
    public async Task Json_ExplicitIdHeaderFraming_RoundTripsWithoutCallerHeaders()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registry = CreateRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            $"{topic}-value",
            new Schema { SchemaType = SchemaType.Json, SchemaString = JsonSchemaText });
        var order = new TestOrder
        {
            OrderId = 5005,
            CustomerName = "Header",
            Amount = 15.75m
        };

        await using var producer = await Kafka.CreateProducer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseJsonSchemaRegistry(
                registry,
                JsonSchemaText,
                JsonSchemaRegistryIntegrationJsonContext.Default.TestOrder,
                new JsonSchemaSerializerConfig
                {
                    UseSchemaId = schemaId,
                    AutoRegisterSchemas = false,
                    SchemaIdStrategy = SchemaIdSerializerStrategy.Header
                })
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.ProduceAsync(topic, "header", order);

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-header-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(
                registry,
                JsonSchemaRegistryIntegrationJsonContext.Default.TestOrder,
                new SchemaRegistryDeserializerConfig
                {
                    SchemaIdStrategy = SchemaIdDeserializerStrategy.Header
                })
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That(message.Value.OrderId).IsEqualTo(order.OrderId);
            await AssertIdentityHeaderAsync(message.Headers);
            return;
        }

        Assert.Fail("No JSON Schema Registry record was consumed.");
    }

    [Test]
    public async Task AvroPoco_GuidWarmup_AllowsSynchronousBatchConsumption()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registry = CreateRegistryClient();
        var context = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Value
        };
        await using var serializer = BatchIdentityRecord.CreateAvroSerializer(
            registry,
            new AvroSerializerConfig
            {
                SchemaIdStrategy = SchemaIdSerializerStrategy.Header
            });
        var schemaId = await serializer.WarmupAsync(topic);
        var registered = await registry.GetSchemaBySubjectAsync(
            $"{topic}-value",
            "latest");
        await Assert.That(registered.Id).IsEqualTo(schemaId);
        var schemaGuid = Guid.Parse(registered.Guid!);
        await using var deserializer = BatchIdentityRecord.CreateAvroDeserializer(
            registry,
            new AvroDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header
            });
        await deserializer.WarmupAsync(schemaGuid, context);

        await using var producer = await Kafka.CreateProducer<string, BatchIdentityRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(serializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.ProduceAsync(
            topic,
            "batch",
            new BatchIdentityRecord { Id = 91, Name = "batch-guid" });

        await using var consumer = await Kafka.CreateConsumer<string, BatchIdentityRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"avro-poco-batch-header-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var batches = consumer.ConsumeBatchAsync(cancellation.Token).GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        var consumed = batches.Current.Single();
        await Assert.That(consumed.Value.Id).IsEqualTo(91);
        await Assert.That(consumed.Value.Name).IsEqualTo("batch-guid");
    }

    private SchemaRegistryClient CreateRegistryClient() => new(new SchemaRegistryConfig
    {
        Url = testInfra.RegistryUrl
    });

    private static async Task AssertIdentityHeaderAsync(IReadOnlyList<Dekaf.Serialization.Header> headers)
    {
        await Assert.That(headers.Count).IsGreaterThan(0);
        Dekaf.Serialization.Header? identityHeader = null;
        for (var index = headers.Count - 1; index >= 0; index--)
        {
            if (headers[index].Key == SchemaIdentityHeaderNames.Value)
            {
                identityHeader = headers[index];
                break;
            }
        }

        await Assert.That(identityHeader).IsNotNull();
        await Assert.That(identityHeader!.Value.Value.Length).IsEqualTo(17);
    }
}

[AvroRecord(Name = "BatchIdentityRecord", Namespace = "Dekaf.Tests")]
internal sealed partial class BatchIdentityRecord
{
    [AvroField(Name = "id", Order = 0)]
    public int Id { get; init; }

    [AvroField(Name = "name", Order = 1)]
    public required string Name { get; init; }
}
