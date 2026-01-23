using System.Buffers;
using System.Buffers.Binary;
using Avro;
using Avro.Generic;
using Avro.IO;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class AvroSerializerTests
{
    private const string SimpleRecordSchema = """
        {
            "type": "record",
            "name": "SimpleRecord",
            "namespace": "test",
            "fields": [
                { "name": "id", "type": "int" },
                { "name": "name", "type": "string" }
            ]
        }
        """;

    private static SerializationContext CreateContext(string topic = "test-topic", bool isKey = false) =>
        new()
        {
            Topic = topic,
            Component = isKey ? SerializationComponent.Key : SerializationComponent.Value
        };

    [Test]
    public async Task Serializer_SerializesGenericRecord_WithWireFormat()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = Schema.Parse(SimpleRecordSchema) as RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 42);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Act
        serializer.Serialize(record, buffer, context);

        // Assert
        var data = buffer.WrittenSpan;

        // Verify wire format: [magic byte] [4-byte schema ID] [Avro payload]
        await Assert.That(data.Length).IsGreaterThan(5);
        await Assert.That(data[0]).IsEqualTo((byte)0x00); // Magic byte

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(data.Slice(1, 4));
        await Assert.That(schemaId).IsGreaterThan(0);
    }

    [Test]
    public async Task Deserializer_DeserializesGenericRecord_FromWireFormat()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();

        // Pre-register schema
        var schemaObj = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        // Create wire format message manually
        var avroSchema = Avro.Schema.Parse(SimpleRecordSchema) as RecordSchema;
        var originalRecord = new GenericRecord(avroSchema!);
        originalRecord.Add("id", 42);
        originalRecord.Add("name", "test");

        // Serialize Avro payload
        using var ms = new MemoryStream();
        var encoder = new BinaryEncoder(ms);
        var writer = new GenericDatumWriter<GenericRecord>(avroSchema!);
        writer.Write(originalRecord, encoder);
        encoder.Flush();
        var avroPayload = ms.ToArray();

        // Create wire format
        var wireFormat = new byte[1 + 4 + avroPayload.Length];
        wireFormat[0] = 0x00; // Magic byte
        BinaryPrimitives.WriteInt32BigEndian(wireFormat.AsSpan(1, 4), schemaId);
        avroPayload.CopyTo(wireFormat.AsSpan(5));

        var context = CreateContext();

        // Act
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(wireFormat), context);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That((int)result["id"]!).IsEqualTo(42);
        await Assert.That((string)result["name"]!).IsEqualTo("test");
    }

    [Test]
    public async Task Serializer_RoundTrips_GenericRecord()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var schema = Schema.Parse(SimpleRecordSchema) as RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 123);
        record.Add("name", "round trip test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Act
        serializer.Serialize(record, buffer, context);
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        // Assert
        await Assert.That((int)result["id"]!).IsEqualTo(123);
        await Assert.That((string)result["name"]!).IsEqualTo("round trip test");
    }

    [Test]
    public async Task Serializer_CachesSchemaId_ForSameSubject()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = Schema.Parse(SimpleRecordSchema) as RecordSchema;
        var record1 = new GenericRecord(schema!);
        record1.Add("id", 1);
        record1.Add("name", "first");

        var record2 = new GenericRecord(schema!);
        record2.Add("id", 2);
        record2.Add("name", "second");

        var buffer1 = new ArrayBufferWriter<byte>();
        var buffer2 = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Act
        serializer.Serialize(record1, buffer1, context);
        serializer.Serialize(record2, buffer2, context);

        // Assert - both should have same schema ID
        var schemaId1 = BinaryPrimitives.ReadInt32BigEndian(buffer1.WrittenSpan.Slice(1, 4));
        var schemaId2 = BinaryPrimitives.ReadInt32BigEndian(buffer2.WrittenSpan.Slice(1, 4));
        await Assert.That(schemaId1).IsEqualTo(schemaId2);
    }

    [Test]
    public async Task Serializer_UsesTopicNameStrategy_ByDefault()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = Schema.Parse(SimpleRecordSchema) as RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic");

        // Act
        serializer.Serialize(record, buffer, context);

        // Assert - schema should be registered under "my-topic-value"
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("my-topic-value");
    }

    [Test]
    public async Task Serializer_UsesKeySubject_ForKeyComponent()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = Schema.Parse(SimpleRecordSchema) as RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic", isKey: true);

        // Act
        serializer.Serialize(record, buffer, context);

        // Assert - schema should be registered under "my-topic-key"
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("my-topic-key");
    }

    [Test]
    public async Task Deserializer_ThrowsOnInvalidMagicByte()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var invalidData = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x00 }; // Wrong magic byte
        var context = CreateContext();

        // Act & Assert
        await Assert.That(() => deserializer.Deserialize(new ReadOnlySequence<byte>(invalidData), context))
            .Throws<InvalidOperationException>()
            .WithMessage(x => x.Message.Contains("magic byte"));
    }

    [Test]
    public async Task Deserializer_ThrowsOnTooShortData()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var shortData = new byte[] { 0x00, 0x01, 0x02 }; // Less than 5 bytes
        var context = CreateContext();

        // Act & Assert
        await Assert.That(() => deserializer.Deserialize(new ReadOnlySequence<byte>(shortData), context))
            .Throws<InvalidOperationException>()
            .WithMessage(x => x.Message.Contains("too short"));
    }

    [Test]
    public async Task Config_AutoRegisterSchemas_DefaultsToTrue()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.AutoRegisterSchemas).IsTrue();
    }

    [Test]
    public async Task Config_SubjectNameStrategy_DefaultsToTopicName()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.SubjectNameStrategy).IsEqualTo(SubjectNameStrategy.TopicName);
    }

    [Test]
    public async Task Config_UseLatestVersion_DefaultsToFalse()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.UseLatestVersion).IsFalse();
    }
}
