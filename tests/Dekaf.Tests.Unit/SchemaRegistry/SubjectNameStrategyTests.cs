using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
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
    public async Task AvroSerializerConfig_MaxCachedSchemas_DefaultsToOneThousand()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.MaxCachedSchemas).IsEqualTo(1000);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task AvroSerializer_MaxCachedSchemasMustBePositive(int maxCachedSchemas)
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig { MaxCachedSchemas = maxCachedSchemas };
        var create = () => new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        await Assert.That(create).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AvroSerializerConfig_UseLatestVersion_DefaultsToFalse()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.UseLatestVersion).IsFalse();
    }

    [Test]
    public async Task AvroSerializerConfig_UseLegacySubjectNames_DefaultsToFalse()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.UseLegacySubjectNames).IsFalse();
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

    [Test]
    public async Task ProtobufSerializerConfig_UseLegacySubjectNames_DefaultsToFalse()
    {
        var config = new ProtobufSerializerConfig();
        await Assert.That(config.UseLegacySubjectNames).IsFalse();
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

    private const string AlternateRecordSchema = """
        {
            "type": "record",
            "name": "AlternateRecord",
            "namespace": "test",
            "fields": [
                { "name": "id", "type": "int" }
            ]
        }
        """;

    private const string RecursiveRecordSchema = """
        {
            "type": "record",
            "name": "Node",
            "namespace": "test",
            "fields": [
                { "name": "value", "type": "int" },
                { "name": "next", "type": ["null", "test.Node"], "default": null }
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

        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("test.SimpleRecord");
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

        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("my-topic-test.SimpleRecord");
    }

    [Test]
    public async Task AvroSerializer_LegacyRecordNameStrategy_RetainsSuffix()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig
        {
            SubjectNameStrategy = SubjectNameStrategy.RecordName,
            UseLegacySubjectNames = true
        };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var schema = (Avro.RecordSchema)AvroSchema.Parse(SimpleRecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer, CreateContext("my-topic"));

        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("test.SimpleRecord-value");
    }

    [Test]
    [Arguments(SubjectNameStrategy.TopicName)]
    [Arguments(SubjectNameStrategy.RecordName)]
    [Arguments(SubjectNameStrategy.TopicRecordName)]
    public async Task AvroSerializer_RuntimeSchemasOnSameTopic_UseDistinctSchemaIds(
        SubjectNameStrategy subjectNameStrategy)
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig { SubjectNameStrategy = subjectNameStrategy };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var firstSchema = (Avro.RecordSchema)AvroSchema.Parse(SimpleRecordSchema);
        var firstRecord = new GenericRecord(firstSchema);
        firstRecord.Add("id", 1);
        firstRecord.Add("name", "first");

        var secondSchema = (Avro.RecordSchema)AvroSchema.Parse(AlternateRecordSchema);
        var secondRecord = new GenericRecord(secondSchema);
        secondRecord.Add("id", 2);

        var firstBuffer = new ArrayBufferWriter<byte>();
        var secondBuffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("shared-topic");
        serializer.Serialize(firstRecord, ref firstBuffer, context);
        serializer.Serialize(secondRecord, ref secondBuffer, context);

        var firstId = BinaryPrimitives.ReadInt32BigEndian(firstBuffer.WrittenSpan.Slice(1, 4));
        var secondId = BinaryPrimitives.ReadInt32BigEndian(secondBuffer.WrittenSpan.Slice(1, 4));
        await Assert.That(secondId).IsNotEqualTo(firstId);
    }

    [Test]
    public async Task AvroSerializer_ConcurrentRuntimeSchemas_KeepSchemaAndCachePaired()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig { SubjectNameStrategy = SubjectNameStrategy.RecordName };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var firstSchema = (Avro.RecordSchema)AvroSchema.Parse(SimpleRecordSchema);
        var firstRecord = new GenericRecord(firstSchema);
        firstRecord.Add("id", 1);
        firstRecord.Add("name", "first");

        var secondSchema = (Avro.RecordSchema)AvroSchema.Parse(AlternateRecordSchema);
        var secondRecord = new GenericRecord(secondSchema);
        secondRecord.Add("id", 2);

        var context = CreateContext("shared-topic");
        var firstId = await serializer.WarmupAsync(context.Topic, firstRecord);
        var secondId = await serializer.WarmupAsync(context.Topic, secondRecord);
        await Assert.That(secondId).IsNotEqualTo(firstId);
        var mismatches = 0;
        using var start = new Barrier(2);

        Task RunAsync(GenericRecord record, int expectedId) => Task.Factory.StartNew(() =>
        {
            var buffer = new ArrayBufferWriter<byte>();
            start.SignalAndWait();
            for (var i = 0; i < 10_000; i++)
            {
                buffer.ResetWrittenCount();
                serializer.Serialize(record, ref buffer, context);
                var actualId = BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4));
                if (actualId != expectedId)
                    Interlocked.Increment(ref mismatches);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        await Task.WhenAll(
            RunAsync(firstRecord, firstId),
            RunAsync(secondRecord, secondId));

        await Assert.That(mismatches).IsEqualTo(0);
    }

    [Test]
    public async Task AvroSerializer_EquivalentRuntimeSchemaInstances_ReuseSubjectCache()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var context = CreateContext("shared-topic");
        var records = new GenericRecord[100];

        for (var i = 0; i < records.Length; i++)
        {
            var schema = (Avro.RecordSchema)AvroSchema.Parse(SimpleRecordSchema);
            var record = new GenericRecord(schema);
            record.Add("id", i);
            record.Add("name", "equivalent");
            records[i] = record;
        }

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(records[0], ref buffer, context);
        buffer.ResetWrittenCount();
        serializer.Serialize(records[1], ref buffer, context);

        var prepareBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 2; i < records.Length; i++)
            serializer.PrepareAsync(records[i], context).GetAwaiter().GetResult();
        var prepareAllocated = GC.GetAllocatedBytesForCurrentThread() - prepareBefore;

        var stableBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 2; i < records.Length; i++)
        {
            buffer.ResetWrittenCount();
            serializer.Serialize(records[0], ref buffer, context);
        }
        var stableAllocated = GC.GetAllocatedBytesForCurrentThread() - stableBefore;

        var equivalentBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 2; i < records.Length; i++)
        {
            buffer.ResetWrittenCount();
            serializer.Serialize(records[i], ref buffer, context);
        }
        var equivalentAllocated = GC.GetAllocatedBytesForCurrentThread() - equivalentBefore;

        await Assert.That(serializer.CachedDynamicSubjectSchemaCount).IsEqualTo(1);
        await Assert.That(serializer.CachedGenericWriterCount).IsEqualTo(1);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
        await Assert.That(prepareAllocated).IsEqualTo(0);
        await Assert.That(equivalentAllocated).IsEqualTo(stableAllocated);
    }

    [Test]
    public async Task AvroSerializer_EquivalentRecursiveSchemas_ReuseSubjectCache()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var context = CreateContext("recursive-topic");
        var buffer = new ArrayBufferWriter<byte>();

        for (var i = 0; i < 2; i++)
        {
            var schema = (Avro.RecordSchema)AvroSchema.Parse(RecursiveRecordSchema);
            var record = new GenericRecord(schema);
            record.Add("value", i);
            record.Add("next", null);
            buffer.ResetWrittenCount();
            serializer.Serialize(record, ref buffer, context);
        }

        await Assert.That(serializer.CachedDynamicSubjectSchemaCount).IsEqualTo(1);
        await Assert.That(serializer.CachedGenericWriterCount).IsEqualTo(1);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task AvroSerializer_RuntimeSchemaCaches_StayWithinConfiguredBound()
    {
        const int maxCachedSchemas = 4;
        using var schemaRegistry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig { MaxCachedSchemas = maxCachedSchemas };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);
        var context = CreateContext("bounded-topic");
        var buffer = new ArrayBufferWriter<byte>();
        GenericRecord? firstRecord = null;
        var firstSchemaId = 0;

        for (var i = 0; i < 10; i++)
        {
            var schema = (Avro.RecordSchema)AvroSchema.Parse(
                $$"""
                {
                  "type": "record",
                  "name": "BoundedRecord{{i}}",
                  "namespace": "test",
                  "fields": [{ "name": "id", "type": "int" }]
                }
                """);
            var record = new GenericRecord(schema);
            record.Add("id", i);
            firstRecord ??= record;

            buffer.ResetWrittenCount();
            serializer.Serialize(record, ref buffer, context);
            var schemaId = BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4));
            if (i == 0)
                firstSchemaId = schemaId;
        }

        await Assert.That(serializer.CachedDynamicSubjectSchemaCount).IsLessThanOrEqualTo(maxCachedSchemas);
        await Assert.That(serializer.CachedGenericWriterCount).IsLessThanOrEqualTo(maxCachedSchemas);
        await Assert.That(serializer.CachedSchemaIdCount).IsEqualTo(maxCachedSchemas);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(10);

        buffer.ResetWrittenCount();
        serializer.Serialize(firstRecord!, ref buffer, context);

        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4)))
            .IsEqualTo(firstSchemaId);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(10);
    }

    [Test]
    public async Task AvroSerializer_RuntimeSchemaCache_DoesNotRetainRecords()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var recordReference = SerializeTransientRecord(serializer, CreateContext("retention-topic"));

        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        await Assert.That(recordReference.TryGetTarget(out _)).IsFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<GenericRecord> SerializeTransientRecord(
        AvroSchemaRegistrySerializer<GenericRecord> serializer,
        SerializationContext context)
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(SimpleRecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", 1);
        record.Add("name", "transient");
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer, context);
        return new WeakReference<GenericRecord>(record);
    }

    [Test]
    public async Task JsonSerializer_RecordNameStrategy_UsesSchemaTitle()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            schemaRegistry,
            """{ "title": "com.example.JsonRecord", "type": "string" }""",
            subjectNameStrategy: SubjectNameStrategy.RecordName);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("value", ref buffer, CreateContext("my-topic"));

        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("com.example.JsonRecord");
    }

    [Test]
    public async Task JsonSerializer_LegacyTopicRecordNameStrategy_RetainsSuffix()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            schemaRegistry,
            """{ "title": "com.example.JsonRecord", "type": "string" }""",
            subjectNameStrategy: SubjectNameStrategy.TopicRecordName,
            useLegacySubjectNames: true);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("value", ref buffer, CreateContext("my-topic", isKey: true));

        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("my-topic-com.example.JsonRecord-key");
    }

    [Test]
    public async Task GenericSerializer_RecordNameStrategy_UsesRuntimeSchemaName()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var requestedSubjects = new List<string>();
        var schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        await using var serializer = new SchemaRegistrySerializer<string>(
            schemaRegistry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(value.Length);
                for (var i = 0; i < value.Length; i++)
                    span[i] = (byte)value[i];
                writer.Advance(value.Length);
            },
            subject =>
            {
                requestedSubjects.Add(subject);
                return schema;
            },
            SubjectNameStrategy.RecordName);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("value", ref buffer, CreateContext("my-topic"));

        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("test.SimpleRecord");
        await Assert.That(requestedSubjects).Count().IsEqualTo(2);
        await Assert.That(requestedSubjects).IsEquivalentTo(["System.String", "test.SimpleRecord"]);
    }

    [Test]
    public async Task GenericSerializer_SubjectIndependentSchemaFactory_ResolvesSchemaOnce()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var calls = 0;
        var schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        await using var serializer = new SchemaRegistrySerializer<string>(
            schemaRegistry,
            static (_, writer) => writer.Advance(0),
            () =>
            {
                calls++;
                return schema;
            },
            SubjectNameStrategy.RecordName);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("value", ref buffer, CreateContext("my-topic"));

        await Assert.That(calls).IsEqualTo(1);
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("test.SimpleRecord");
    }

    [Test]
    public async Task GenericSerializer_LegacyRecordNameStrategy_RetainsSuffix()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        await using var serializer = new SchemaRegistrySerializer<string>(
            schemaRegistry,
            static (value, writer) =>
            {
                writer.GetSpan(1)[0] = (byte)value[0];
                writer.Advance(1);
            },
            _ => schema,
            useLegacySubjectNames: true,
            subjectNameStrategy: SubjectNameStrategy.RecordName);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("value", ref buffer, CreateContext("my-topic", isKey: true));

        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("test.SimpleRecord-key");
    }

    [Test]
    public async Task UnknownEnumStrategy_PreservesTopicNameFallback()
    {
        var subject = SubjectNameResolver.GetSubjectName(
            (SubjectNameStrategy)int.MaxValue,
            "my-topic",
            "com.example.Record",
            isKey: false,
            useLegacySubjectNames: false);

        await Assert.That(subject).IsEqualTo("my-topic-value");
    }

    [Test]
    public async Task JsonBooleanSchema_RecordNameFallsBackToClrType()
    {
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "true"
        };

        var recordName = SubjectNameResolver.GetRecordName(schema, "com.example.Record");

        await Assert.That(recordName).IsEqualTo("com.example.Record");
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
