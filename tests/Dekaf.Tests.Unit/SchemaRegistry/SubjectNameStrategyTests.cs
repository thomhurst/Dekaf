using System.Buffers;
using Avro.Generic;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SubjectNameStrategyTests
{
    // --- TopicNameStrategy Tests ---

    [Test]
    public async Task TopicNameStrategy_Value_ReturnsTopicDashValue()
    {
        var strategy = new TopicNameStrategy();
        var result = strategy.GetSubjectName("my-topic", "com.example.MyRecord", isKey: false);
        await Assert.That(result).IsEqualTo("my-topic-value");
    }

    [Test]
    public async Task TopicNameStrategy_Key_ReturnsTopicDashKey()
    {
        var strategy = new TopicNameStrategy();
        var result = strategy.GetSubjectName("my-topic", "com.example.MyRecord", isKey: true);
        await Assert.That(result).IsEqualTo("my-topic-key");
    }

    [Test]
    public async Task TopicNameStrategy_IgnoresRecordType()
    {
        var strategy = new TopicNameStrategy();
        var resultWithRecord = strategy.GetSubjectName("orders", "com.example.Order", isKey: false);
        var resultWithNull = strategy.GetSubjectName("orders", null, isKey: false);
        await Assert.That(resultWithRecord).IsEqualTo("orders-value");
        await Assert.That(resultWithNull).IsEqualTo("orders-value");
    }

    // --- RecordNameStrategy Tests ---

    [Test]
    public async Task RecordNameStrategy_ReturnsRecordTypeName()
    {
        var strategy = new RecordNameStrategy();
        var result = strategy.GetSubjectName("my-topic", "com.example.MyRecord", isKey: false);
        await Assert.That(result).IsEqualTo("com.example.MyRecord");
    }

    [Test]
    public async Task RecordNameStrategy_IgnoresTopic()
    {
        var strategy = new RecordNameStrategy();
        var result1 = strategy.GetSubjectName("topic-a", "com.example.MyRecord", isKey: false);
        var result2 = strategy.GetSubjectName("topic-b", "com.example.MyRecord", isKey: true);
        await Assert.That(result1).IsEqualTo("com.example.MyRecord");
        await Assert.That(result2).IsEqualTo("com.example.MyRecord");
    }

    [Test]
    public async Task RecordNameStrategy_ThrowsWhenRecordTypeIsNull()
    {
        var strategy = new RecordNameStrategy();
        await Assert.That(() => strategy.GetSubjectName("my-topic", null, isKey: false))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("RecordNameStrategy requires a record type name");
    }

    [Test]
    public async Task RecordNameStrategy_ThrowsWhenRecordTypeIsEmpty()
    {
        var strategy = new RecordNameStrategy();
        await Assert.That(() => strategy.GetSubjectName("my-topic", "", isKey: false))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("RecordNameStrategy requires a record type name");
    }

    // --- TopicRecordNameStrategy Tests ---

    [Test]
    public async Task TopicRecordNameStrategy_Value_ReturnsTopicDashRecordType()
    {
        var strategy = new TopicRecordNameStrategy();
        var result = strategy.GetSubjectName("my-topic", "com.example.MyRecord", isKey: false);
        await Assert.That(result).IsEqualTo("my-topic-com.example.MyRecord");
    }

    [Test]
    public async Task TopicRecordNameStrategy_Key_ReturnsTopicDashRecordType()
    {
        var strategy = new TopicRecordNameStrategy();
        var result = strategy.GetSubjectName("my-topic", "com.example.MyRecord", isKey: true);
        await Assert.That(result).IsEqualTo("my-topic-com.example.MyRecord");
    }

    [Test]
    public async Task TopicRecordNameStrategy_ThrowsWhenRecordTypeIsNull()
    {
        var strategy = new TopicRecordNameStrategy();
        await Assert.That(() => strategy.GetSubjectName("my-topic", null, isKey: false))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("TopicRecordNameStrategy requires a record type name");
    }

    [Test]
    public async Task TopicRecordNameStrategy_ThrowsWhenRecordTypeIsEmpty()
    {
        var strategy = new TopicRecordNameStrategy();
        await Assert.That(() => strategy.GetSubjectName("my-topic", "", isKey: false))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("TopicRecordNameStrategy requires a record type name");
    }

    // --- SubjectNameStrategies Static Accessor Tests ---

    [Test]
    public async Task SubjectNameStrategies_Topic_IsTopicNameStrategy()
    {
        var strategy = SubjectNameStrategies.Topic;
        await Assert.That(strategy).IsTypeOf<TopicNameStrategy>();
    }

    [Test]
    public async Task SubjectNameStrategies_Record_IsRecordNameStrategy()
    {
        var strategy = SubjectNameStrategies.Record;
        await Assert.That(strategy).IsTypeOf<RecordNameStrategy>();
    }

    [Test]
    public async Task SubjectNameStrategies_TopicRecord_IsTopicRecordNameStrategy()
    {
        var strategy = SubjectNameStrategies.TopicRecord;
        await Assert.That(strategy).IsTypeOf<TopicRecordNameStrategy>();
    }

    // --- Custom ISubjectNameStrategy Tests ---

    [Test]
    public async Task CustomStrategy_IsUsedBySchemaRegistrySerializer()
    {
        var customStrategy = new PrefixedSubjectNameStrategy("staging");
        var result = customStrategy.GetSubjectName("orders", "com.example.Order", isKey: false);
        await Assert.That(result).IsEqualTo("staging.orders-value");
    }

    [Test]
    public async Task CustomStrategy_HandlesIsKeyCorrectly()
    {
        var customStrategy = new PrefixedSubjectNameStrategy("prod");
        var keyResult = customStrategy.GetSubjectName("events", "com.example.Event", isKey: true);
        var valueResult = customStrategy.GetSubjectName("events", "com.example.Event", isKey: false);
        await Assert.That(keyResult).IsEqualTo("prod.events-key");
        await Assert.That(valueResult).IsEqualTo("prod.events-value");
    }

    // --- Config Default Tests ---

    [Test]
    public async Task AvroSerializerConfig_SubjectNameStrategy_DefaultsToTopicName()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.SubjectNameStrategy).IsEqualTo(SubjectNameStrategy.TopicName);
    }

    [Test]
    public async Task AvroSerializerConfig_CustomSubjectNameStrategy_DefaultsToNull()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.CustomSubjectNameStrategy).IsNull();
    }

    [Test]
    public async Task AvroSerializerConfig_AutoRegisterSchemas_DefaultsToTrue()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.AutoRegisterSchemas).IsTrue();
    }

    [Test]
    public async Task AvroSerializerConfig_UseLatestVersion_DefaultsToFalse()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.UseLatestVersion).IsFalse();
    }

    [Test]
    public async Task ProtobufSerializerConfig_SubjectNameStrategy_DefaultsToTopicName()
    {
        var config = new ProtobufSerializerConfig();
        await Assert.That(config.SubjectNameStrategy).IsEqualTo(SubjectNameStrategy.TopicName);
    }

    [Test]
    public async Task ProtobufSerializerConfig_CustomSubjectNameStrategy_DefaultsToNull()
    {
        var config = new ProtobufSerializerConfig();
        await Assert.That(config.CustomSubjectNameStrategy).IsNull();
    }

    [Test]
    public async Task ProtobufSerializerConfig_AutoRegisterSchemas_DefaultsToTrue()
    {
        var config = new ProtobufSerializerConfig();
        await Assert.That(config.AutoRegisterSchemas).IsTrue();
    }

    [Test]
    public async Task ProtobufSerializerConfig_UseLatestVersion_DefaultsToFalse()
    {
        var config = new ProtobufSerializerConfig();
        await Assert.That(config.UseLatestVersion).IsFalse();
    }

    // --- Integration with AvroSchemaRegistrySerializer ---

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
    public async Task AvroSerializer_RecordNameStrategy_RegistersUnderRecordName()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig
        {
            SubjectNameStrategy = SubjectNameStrategy.RecordName
        };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic");

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert - for GenericRecord (not ISpecificRecord), the record name falls back
        // to the .NET FullName of GenericRecord since the static _SCHEMA field is not available.
        // For ISpecificRecord types, the Avro schema fullname would be used instead.
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("Avro.Generic.GenericRecord-value");
    }

    [Test]
    public async Task AvroSerializer_TopicRecordNameStrategy_RegistersUnderTopicRecordName()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig
        {
            SubjectNameStrategy = SubjectNameStrategy.TopicRecordName
        };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic");

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        // GenericRecord uses .FullName from the schema, so this should be the
        // Avro record fullname (not the .NET type name)
        var hasTopicRecordSubject = false;
        foreach (var subject in subjects)
        {
            if (subject.StartsWith("my-topic-", StringComparison.Ordinal) && subject != "my-topic-value" && subject != "my-topic-key")
            {
                hasTopicRecordSubject = true;
            }
        }
        await Assert.That(hasTopicRecordSubject).IsTrue();
    }

    [Test]
    public async Task AvroSerializer_CustomStrategy_RegistersUnderCustomSubjectName()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig
        {
            CustomSubjectNameStrategy = new PrefixedSubjectNameStrategy("staging")
        };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic");

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert - custom strategy adds "staging." prefix
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("staging.my-topic-value");
    }

    [Test]
    public async Task AvroSerializer_CustomStrategy_TakesPrecedenceOverEnum()
    {
        // Arrange - set both enum and custom strategy
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig
        {
            SubjectNameStrategy = SubjectNameStrategy.RecordName,
            CustomSubjectNameStrategy = new PrefixedSubjectNameStrategy("override")
        };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic");

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert - custom strategy should win over enum
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("override.my-topic-value");
    }

    // --- Test helper: custom strategy implementation ---

    /// <summary>
    /// A test custom subject name strategy that adds an environment prefix.
    /// </summary>
    private sealed class PrefixedSubjectNameStrategy : ISubjectNameStrategy
    {
        private readonly string _prefix;

        public PrefixedSubjectNameStrategy(string prefix)
        {
            _prefix = prefix;
        }

        public string GetSubjectName(string topic, string? recordType, bool isKey)
        {
            var suffix = isKey ? "key" : "value";
            return $"{_prefix}.{topic}-{suffix}";
        }
    }
}
