using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using ConfluentContext = Confluent.Kafka.SerializationContext;
using DekafRegistryClient = Dekaf.SchemaRegistry.SchemaRegistryClient;
using DekafRegistryConfig = Dekaf.SchemaRegistry.SchemaRegistryConfig;
using DekafSerializationContext = Dekaf.Serialization.SerializationContext;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class AvroPocoInteropIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task GeneratedPoco_InteroperatesWithConfluentGenericRecordBothDirections()
    {
        var topic = $"avro-poco-interop-{Guid.NewGuid():N}";
        using var dekafRegistry = new DekafRegistryClient(new DekafRegistryConfig { Url = testInfra.RegistryUrl });
        using var dekafConsumerRegistry = new DekafRegistryClient(
            new DekafRegistryConfig { Url = testInfra.RegistryUrl });
        using var confluentRegistry = new CachedSchemaRegistryClient(
            new Confluent.SchemaRegistry.SchemaRegistryConfig { Url = testInfra.RegistryUrl });
        await using var dekafSerializer = PocoInteropRecord.CreateAvroSerializer(dekafRegistry);
        await using var dekafDeserializer = PocoInteropRecord.CreateAvroDeserializer(dekafConsumerRegistry);
        var confluentSerializer = new AvroSerializer<GenericRecord>(confluentRegistry);
        var confluentDeserializer = new AvroDeserializer<GenericRecord>(confluentRegistry);
        var dekafContext = new DekafSerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Value
        };
        var confluentContext = new ConfluentContext(MessageComponentType.Value, topic);

        var poco = new PocoInteropRecord { Id = 42, Name = "Dekaf", Note = "to-confluent", Amount = -123.45m };
        await dekafSerializer.WarmupAsync(topic);
        var buffer = new ArrayBufferWriter<byte>();
        dekafSerializer.Serialize(poco, ref buffer, dekafContext);

        var confluentValue = await confluentDeserializer.DeserializeAsync(
            buffer.WrittenMemory,
            isNull: false,
            confluentContext);
        await Assert.That((int)confluentValue["id"]!).IsEqualTo(poco.Id);
        await Assert.That((string)confluentValue["name"]!).IsEqualTo(poco.Name);
        await Assert.That((string)confluentValue["note"]!).IsEqualTo(poco.Note);
        var confluentAmount = (Avro.AvroDecimal)confluentValue["amount"]!;
        await Assert.That(confluentAmount.UnscaledValue).IsEqualTo(new BigInteger(-12345));
        await Assert.That(confluentAmount.Scale).IsEqualTo(2);

        var schema = (Avro.RecordSchema)AvroSchema.Parse(PocoInteropRecord.AvroCodec.SchemaJson);
        var generic = new GenericRecord(schema);
        generic.Add("id", 84);
        generic.Add("name", "Confluent");
        generic.Add("note", "to-dekaf");
        generic.Add("amount", new Avro.AvroDecimal(new BigInteger(7890), 2));
        var confluentBytes = await confluentSerializer.SerializeAsync(generic, confluentContext);

        var confluentSchemaId = BinaryPrimitives.ReadInt32BigEndian(confluentBytes.AsSpan(1, 4));
        await dekafDeserializer.WarmupAsync(confluentSchemaId);
        var dekafValue = dekafDeserializer.Deserialize(confluentBytes, dekafContext);
        await Assert.That(dekafValue.Id).IsEqualTo(84);
        await Assert.That(dekafValue.Name).IsEqualTo("Confluent");
        await Assert.That(dekafValue.Note).IsEqualTo("to-dekaf");
        await Assert.That(dekafValue.Amount).IsEqualTo(78.90m);
    }
}

[AvroRecord(Name = "PocoInteropRecord", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoInteropRecord
{
    [AvroField(Name = "id", Order = 0)]
    public int Id { get; init; }

    [AvroField(Name = "name", Order = 1)]
    public required string Name { get; init; }

    [AvroField(Name = "note", Order = 2, DefaultJson = "null")]
    public string? Note { get; init; }

    [AvroField(Name = "amount", Order = 3, Precision = 8, Scale = 2)]
    public decimal Amount { get; init; }
}
