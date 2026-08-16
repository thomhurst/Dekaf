using System.Buffers;
using Avro.Generic;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using Dekaf.Serialization.Routing;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class RoutingSerdeIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    private const string EventV1Schema = """
        {
            "type": "record",
            "name": "RoutedEvent",
            "namespace": "dekaf.tests",
            "fields": [
                { "name": "id", "type": "long" },
                { "name": "name", "type": "string" }
            ]
        }
        """;

    private const string EventV2Schema = """
        {
            "type": "record",
            "name": "RoutedEvent",
            "namespace": "dekaf.tests",
            "fields": [
                { "name": "id", "type": "long" },
                { "name": "name", "type": "string" },
                { "name": "category", "type": "string", "default": "general" }
            ]
        }
        """;

    [Test]
    public async Task SchemaIdRouter_ConsumesMultipleVersionsAndProvidesRawKey()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registry = new SchemaRegistryClient(new SchemaRegistryConfig { Url = testInfra.RegistryUrl });
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registry);

        var v1Record = CreateRecord(EventV1Schema, 1, "one");
        var v2Record = CreateRecord(EventV2Schema, 2, "two", "priority");
        var v1SchemaId = await serializer.WarmupAsync(topic, v1Record);
        var v2SchemaId = await serializer.WarmupAsync(topic, v2Record);
        await deserializer.WarmupAsync(v1SchemaId);
        await deserializer.WarmupAsync(v2SchemaId);
        var v1Bytes = Serialize(serializer, topic, v1Record);
        var v2Bytes = Serialize(serializer, topic, v2Record);

        var v1Route = new AvroRouteDeserializer<V1Event>(deserializer, static (record, key) =>
            new V1Event((long)record["id"]!, key));
        var v2Route = new AvroRouteDeserializer<V2Event>(deserializer, static (record, key) =>
            new V2Event((long)record["id"]!, (string)record["category"]!, key));
        var incompleteRouter = new SchemaIdRoutingDeserializer<RoutedEvent>()
            .Register(v1SchemaId, v1Route)
            .Freeze();
        var router = new SchemaIdRoutingDeserializer<RoutedEvent>()
            .Register(v1SchemaId, v1Route)
            .Register(v2SchemaId, v2Route)
            .Freeze();

        await Assert.That(() => incompleteRouter.Deserialize(v2Bytes, ValueContext(topic)))
            .Throws<SerializationException>();

        await using (var producer = await Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("routing-serde-producer")
            .BuildAsync())
        {
            await producer.ProduceAsync(topic, "key-v1", v1Bytes);
            await producer.ProduceAsync(topic, "key-v2", v2Bytes);
            await producer.FlushWithTimeoutAsync();
        }

        await using var consumer = await Kafka.CreateConsumer<string, RoutedEvent>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("routing-serde-consumer")
            .WithGroupId($"routing-serde-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(router)
            .BuildAsync();
        consumer.Subscribe(topic);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var events = new List<RoutedEvent>(2);
        await foreach (var message in consumer.ConsumeAsync(timeout.Token))
        {
            events.Add(message.Value);
            if (events.Count == 2)
                break;
        }

        await Assert.That(v1SchemaId).IsNotEqualTo(v2SchemaId);
        await Assert.That(events[0]).IsTypeOf<V1Event>();
        await Assert.That(events[1]).IsTypeOf<V2Event>();
        await Assert.That(events[0].Key).IsEqualTo("key-v1");
        await Assert.That(events[1].Key).IsEqualTo("key-v2");
        await Assert.That(((V2Event)events[1]).Category).IsEqualTo("priority");
    }

    private static GenericRecord CreateRecord(
        string schemaJson,
        long id,
        string name,
        string? category = null)
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(schemaJson);
        var record = new GenericRecord(schema);
        record.Add("id", id);
        record.Add("name", name);
        if (category is not null)
            record.Add("category", category);
        return record;
    }

    private static byte[] Serialize(
        AvroSchemaRegistrySerializer<GenericRecord> serializer,
        string topic,
        GenericRecord record)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var context = ValueContext(topic);
        serializer.Serialize(record, ref buffer, context);
        return buffer.WrittenSpan.ToArray();
    }

    private static SerializationContext ValueContext(string topic) => new()
    {
        Topic = topic,
        Component = SerializationComponent.Value
    };

    private abstract record RoutedEvent(long Id, string Key);
    private sealed record V1Event(long Id, string Key) : RoutedEvent(Id, Key);
    private sealed record V2Event(long Id, string Category, string Key) : RoutedEvent(Id, Key);

    private sealed class AvroRouteDeserializer<TEvent>(
        AvroSchemaRegistryDeserializer<GenericRecord> inner,
        Func<GenericRecord, string, TEvent> create) : IDeserializer<TEvent>
        where TEvent : RoutedEvent
    {
        public TEvent Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            var record = inner.Deserialize(data, context);
            var key = Serializers.String.Deserialize(context.KeyData, context);
            return create(record, key);
        }
    }
}
