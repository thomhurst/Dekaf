using Avro.Generic;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Integration.Protos;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryTombstoneIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task AvroDeserializer_ConsumesTombstoneFromCompactedTopic()
    {
        using var registry = CreateRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registry);

        await AssertTombstoneAsync(deserializer);
    }

    [Test]
    public async Task ProtobufDeserializer_ConsumesTombstoneFromCompactedTopic()
    {
        using var registry = CreateRegistryClient();
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestPerson>(registry);

        await AssertTombstoneAsync(deserializer);
    }

    [Test]
    public async Task JsonSchemaDeserializer_ConsumesTombstoneFromCompactedTopic()
    {
        using var registry = CreateRegistryClient();
        await using var deserializer = new JsonSchemaRegistryDeserializer<TestOrder>(
            registry,
            JsonSchemaRegistryIntegrationJsonContext.Default.TestOrder);

        await AssertTombstoneAsync(deserializer);
    }

    private SchemaRegistryClient CreateRegistryClient() =>
        new(new SchemaRegistryConfig { Url = testInfra.RegistryUrl });

    private async Task AssertTombstoneAsync<T>(IDeserializer<T> deserializer)
        where T : class
    {
        var topic = await testInfra.CreateTestTopicAsync(
            configs: new Dictionary<string, string> { ["cleanup.policy"] = "compact" });

        await using var producer = await Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(Serializers.NullableString)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "deleted-key",
            Value = null
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, T>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsNull();
    }
}
