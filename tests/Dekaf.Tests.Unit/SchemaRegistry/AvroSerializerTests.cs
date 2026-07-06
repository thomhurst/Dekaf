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

// Apache.Avro's Schema.Parse and PreresolvingDatumReader have thread-safety issues
// when multiple tests parse the same schema concurrently. Serialize Avro tests.
[NotInParallel("AvroSerialization")]
public sealed class AvroSerializerTests
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

    private sealed class CapturingRuleExecutor(
        byte[]? serializedPayload = null,
        byte[]? deserializedPayload = null) : ISchemaRegistryRuleExecutor
    {
        public SchemaRegistryRuleContext? SerializeContext { get; private set; }
        public SchemaRegistryRuleContext? DeserializeContext { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            SerializeContext = context;
            return serializedPayload ?? payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            DeserializeContext = context;
            return deserializedPayload ?? payload;
        }
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
    public async Task Serializer_RuleExecutor_TransformsAvroPayload()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 42);
        record.Add("name", "plain");

        var replacement = new GenericRecord(schema!);
        replacement.Add("id", 99);
        replacement.Add("name", "encrypted");
        var replacementPayload = SerializeAvroRecord(replacement, schema!);
        var executor = new CapturingRuleExecutor(serializedPayload: replacementPayload);
        var config = new AvroSerializerConfig { RuleExecutor = executor };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer, CreateContext());

        await Assert.That(buffer.WrittenSpan.Slice(5).ToArray()).IsEquivalentTo(replacementPayload);
        await Assert.That(executor.SerializeContext).IsNotNull();
        await Assert.That(executor.SerializeContext!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Avro);
        await Assert.That(executor.SerializeContext.Subject).IsEqualTo("test-topic-value");
        await Assert.That(executor.SerializeContext.SchemaId).IsGreaterThan(0);
        await Assert.That(executor.SerializeContext.Schema).IsNotNull();
        await Assert.That(executor.SerializeContext.Schema!.SchemaType).IsEqualTo(SchemaType.Avro);
        await Assert.That(executor.SerializeContext.Schema.SchemaString).IsEqualTo(schema!.ToString());
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

        // Serialize a record using the Avro library for correct binary encoding
        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(avroSchema!);
        record.Add("id", 42);
        record.Add("name", "test");
        var avroPayload = SerializeAvroRecord(record, avroSchema!);

        // Create wire format
        var wireFormat = new byte[1 + 4 + avroPayload.Length];
        wireFormat[0] = 0x00; // Magic byte
        BinaryPrimitives.WriteInt32BigEndian(wireFormat.AsSpan(1, 4), schemaId);
        avroPayload.CopyTo(wireFormat.AsSpan(5));

        var context = CreateContext();

        // Act
        var result = deserializer.Deserialize(wireFormat, context);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That((int)result["id"]!).IsEqualTo(42);
        await Assert.That((string)result["name"]!).IsEqualTo("test");
    }

    [Test]
    public async Task Deserializer_RuleExecutor_TransformsAvroPayload()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var replacement = new GenericRecord(avroSchema!);
        replacement.Add("id", 7);
        replacement.Add("name", "plain");
        var replacementPayload = SerializeAvroRecord(replacement, avroSchema!);
        var wireFormat = CreateWireFormat(schemaId, "encrypted"u8.ToArray());
        var executor = new CapturingRuleExecutor(deserializedPayload: replacementPayload);
        var config = new AvroDeserializerConfig { RuleExecutor = executor };
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry, config);

        var result = deserializer.Deserialize(wireFormat, CreateContext());

        await Assert.That((int)result["id"]!).IsEqualTo(7);
        await Assert.That((string)result["name"]!).IsEqualTo("plain");
        await Assert.That(executor.DeserializeContext).IsNotNull();
        await Assert.That(executor.DeserializeContext!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Avro);
        await Assert.That(executor.DeserializeContext.SchemaId).IsEqualTo(schemaId);
        await Assert.That(executor.DeserializeContext.Schema).IsSameReferenceAs(schemaObj);
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
        var result = deserializer.Deserialize(buffer.WrittenMemory, context);

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
    public async Task Serializer_CachesGenericDatumWriter_ForSameSchema()
    {
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

        serializer.Serialize(record1, ref buffer1, context);
        serializer.Serialize(record2, ref buffer2, context);

        await Assert.That(serializer.CachedGenericWriterCount).IsEqualTo(1);
        await Assert.That(serializer.CachedSpecificWriterCount).IsEqualTo(0);
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
        await Assert.That(() => deserializer.Deserialize(invalidData, context))
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
        await Assert.That(() => deserializer.Deserialize(shortData, context))
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

        // Construct wire format manually using the known schemaId to avoid
        // non-determinism from a second serializer interacting with the mock registry
        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var originalRecord = new GenericRecord(avroSchema!);
        originalRecord.Add("id", 42);
        originalRecord.Add("name", "warmup-test");

        var avroPayload = SerializeAvroRecord(originalRecord, avroSchema!);
        var wireFormat = new byte[1 + 4 + avroPayload.Length];
        wireFormat[0] = 0x00; // Magic byte
        BinaryPrimitives.WriteInt32BigEndian(wireFormat.AsSpan(1, 4), schemaId);
        avroPayload.CopyTo(wireFormat.AsSpan(5));

        // Deserialize using the warmed-up cache
        var desContext = CreateContext();
        var result = deserializer.Deserialize(wireFormat, desContext);

        // Verify deserialization worked correctly
        await Assert.That((int)result["id"]!).IsEqualTo(42);
        await Assert.That((string)result["name"]!).IsEqualTo("warmup-test");
    }

    [Test]
    public async Task Serializer_WarmupAsync_RetriesAfterTransientSchemaIdFailure()
    {
        using var schemaRegistry = new MockSchemaRegistryClient
        {
            GetOrRegisterSchemaFailuresRemaining = 1
        };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "retry");

        await Assert.That(async () => await serializer.WarmupAsync("retry-topic", record))
            .Throws<SchemaRegistryException>();

        var schemaId = await serializer.WarmupAsync("retry-topic", record);

        await Assert.That(schemaId).IsGreaterThan(0);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Serializer_WarmupAsync_DoesNotBindSharedFetchToFirstCallerCancellation()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        schemaRegistry.BlockNextGetOrRegisterSchema();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "shared-cancellation");

        using var firstWaiterCts = new CancellationTokenSource();
        var firstWaiter = serializer.WarmupAsync("shared-topic", record, cancellationToken: firstWaiterCts.Token);
        await schemaRegistry.WaitForBlockedGetOrRegisterSchemaAsync(TimeSpan.FromSeconds(2));

        var secondWaiter = serializer.WarmupAsync("shared-topic", record);
        try
        {
            firstWaiterCts.Cancel();

            await Assert.That(async () => await firstWaiter).Throws<OperationCanceledException>();
        }
        finally
        {
            schemaRegistry.ReleaseBlockedGetOrRegisterSchema();
        }

        var schemaId = await secondWaiter.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.That(schemaId).IsGreaterThan(0);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Deserializer_WarmupAsync_RetriesAfterTransientSchemaFetchFailure()
    {
        using var schemaRegistry = new MockSchemaRegistryClient
        {
            GetSchemaFailuresRemaining = 1
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("retry-topic-value", new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        });
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        await Assert.That(async () => await deserializer.WarmupAsync(schemaId))
            .Throws<SchemaRegistryException>();

        var schema = await deserializer.WarmupAsync(schemaId);

        await Assert.That(schema.Fullname).IsEqualTo("test.SimpleRecord");
        await Assert.That(schemaRegistry.GetSchemaCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Deserializer_CachesGenericDatumReader_ForSameSchemaPair()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();

        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);
        await deserializer.WarmupAsync(schemaId);

        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record1 = new GenericRecord(avroSchema!);
        record1.Add("id", 1);
        record1.Add("name", "first");

        var record2 = new GenericRecord(avroSchema!);
        record2.Add("id", 2);
        record2.Add("name", "second");

        var wireFormat1 = CreateWireFormat(schemaId, SerializeAvroRecord(record1, avroSchema!));
        var wireFormat2 = CreateWireFormat(schemaId, SerializeAvroRecord(record2, avroSchema!));
        var context = CreateContext();

        deserializer.Deserialize(wireFormat1, context);
        deserializer.Deserialize(wireFormat2, context);

        await Assert.That(deserializer.CachedGenericReaderCount).IsEqualTo(1);
        await Assert.That(deserializer.CachedSpecificReaderCount).IsEqualTo(0);
    }

    private static byte[] CreateWireFormat(int schemaId, byte[] avroPayload)
    {
        var wireFormat = new byte[1 + 4 + avroPayload.Length];
        wireFormat[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireFormat.AsSpan(1, 4), schemaId);
        avroPayload.CopyTo(wireFormat.AsSpan(5));
        return wireFormat;
    }
}
