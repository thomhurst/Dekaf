using System.Buffers;
using System.Buffers.Binary;
using Avro.Generic;
using Avro.IO;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

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

    private static byte[] SerializeAvroRecord(GenericRecord record, Avro.RecordSchema schema)
    {
        using var ms = new MemoryStream();
        var encoder = new BinaryEncoder(ms);
        var writer = new GenericDatumWriter<GenericRecord>(schema);
        writer.Write(record, encoder);
        encoder.Flush();
        return ms.ToArray();
    }

    [Test]
    public async Task Serializer_SerializesGenericRecord_WithWireFormat()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 42);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert
        var data = buffer.WrittenMemory;

        // Verify wire format: [magic byte] [4-byte schema ID] [Avro payload]
        await Assert.That(data.Length).IsGreaterThan(5);
        await Assert.That(data.Span[0]).IsEqualTo((byte)0x00); // Magic byte

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(data.Span.Slice(1, 4));
        await Assert.That(schemaId).IsGreaterThan(0);
    }

    [Test]
    public async Task Deserializer_DeserializesGenericRecord_FromWireFormat()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();

        // Pre-register schema
        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        // Warm up the deserializer cache with the schema
        await deserializer.WarmupAsync(schemaId);

        // Create wire format message manually
        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var originalRecord = new GenericRecord(avroSchema!);
        originalRecord.Add("id", 42);
        originalRecord.Add("name", "test");

        // Serialize Avro payload
        var avroPayload = SerializeAvroRecord(originalRecord, avroSchema!);

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
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 123);
        record.Add("name", "round trip test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Act
        serializer.Serialize(record, ref buffer, context);
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        // Assert
        await Assert.That((int)result["id"]!).IsEqualTo(123);
        await Assert.That((string)result["name"]!).IsEqualTo("round trip test");
    }

    [Test]
    public async Task Serializer_CachesSchemaId_ForSameSubject()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
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
        serializer.Serialize(record1, ref buffer1, context);
        serializer.Serialize(record2, ref buffer2, context);

        // Assert - both should have same schema ID
        var schemaId1 = BinaryPrimitives.ReadInt32BigEndian(buffer1.WrittenSpan.Slice(1, 4));
        var schemaId2 = BinaryPrimitives.ReadInt32BigEndian(buffer2.WrittenSpan.Slice(1, 4));
        await Assert.That(schemaId1).IsEqualTo(schemaId2);
    }

    [Test]
    public async Task Serializer_UsesTopicNameStrategy_ByDefault()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic");

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert - schema should be registered under "my-topic-value"
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("my-topic-value");
    }

    [Test]
    public async Task Serializer_UsesKeySubject_ForKeyComponent()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic", isKey: true);

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert - schema should be registered under "my-topic-key"
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("my-topic-key");
    }

    [Test]
    public async Task Deserializer_ThrowsOnInvalidMagicByte()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var invalidData = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x00 }; // Wrong magic byte
        var context = CreateContext();

        // Act & Assert
        await Assert.That(() => deserializer.Deserialize(new ReadOnlySequence<byte>(invalidData), context))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("magic byte");
    }

    [Test]
    public async Task Deserializer_ThrowsOnTooShortData()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var shortData = new byte[] { 0x00, 0x01, 0x02 }; // Less than 5 bytes
        var context = CreateContext();

        // Act & Assert
        await Assert.That(() => deserializer.Deserialize(new ReadOnlySequence<byte>(shortData), context))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("too short");
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

    [Test]
    public async Task Serializer_WarmupAsync_PreCachesSchemaId()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "warmup");

        // Act - warm up the cache
        var warmupSchemaId = await serializer.WarmupAsync("warmup-topic", record, isKey: false);

        // Serialize using the warmed-up cache
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("warmup-topic");
        serializer.Serialize(record, ref buffer, context);

        // Assert - the schema ID from warmup should match the one used in serialization
        var serializedSchemaId = BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4));
        await Assert.That(serializedSchemaId).IsEqualTo(warmupSchemaId);
    }

    [Test]
    public async Task Deserializer_WarmupAsync_PreCachesSchema()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();

        // Pre-register schema
        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        // Act - warm up the cache
        var warmedSchema = await deserializer.WarmupAsync(schemaId);

        // Assert - schema was fetched and cached
        await Assert.That(warmedSchema).IsNotNull();
        await Assert.That(warmedSchema.Fullname).IsEqualTo("test.SimpleRecord");

        // Create wire format message to verify deserialization works with cached schema
        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var originalRecord = new GenericRecord(avroSchema!);
        originalRecord.Add("id", 99);
        originalRecord.Add("name", "cached");

        using var ms = new MemoryStream();
        var encoder = new BinaryEncoder(ms);
        var writer = new GenericDatumWriter<GenericRecord>(avroSchema!);
        writer.Write(originalRecord, encoder);
        encoder.Flush();
        var avroPayload = ms.ToArray();

        var wireFormat = new byte[1 + 4 + avroPayload.Length];
        wireFormat[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireFormat.AsSpan(1, 4), schemaId);
        avroPayload.CopyTo(wireFormat.AsSpan(5));

        var context = CreateContext();

        // Deserialize using the warmed-up cache
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(wireFormat), context);

        await Assert.That((int)result["id"]!).IsEqualTo(99);
        await Assert.That((string)result["name"]!).IsEqualTo("cached");
    }
}
