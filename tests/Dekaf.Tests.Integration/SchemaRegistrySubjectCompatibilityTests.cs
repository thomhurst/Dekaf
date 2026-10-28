using System.Buffers;
using System.Buffers.Binary;
using Avro.Generic;
using Confluent.Kafka;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Integration.Protos;
using Google.Protobuf;
using AvroSchema = Avro.Schema;
using ConfluentSubjectNameStrategy = Confluent.SchemaRegistry.SubjectNameStrategy;
using DekafSubjectNameStrategy = Dekaf.SchemaRegistry.SubjectNameStrategy;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[Timeout(120_000)]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistrySubjectCompatibilityTests(KafkaWithSchemaRegistryContainer testInfra)
{
    private const string AvroRecordSchema = """
        {
            "type": "record",
            "name": "CompatibilityRecord",
            "namespace": "dekaf.compatibility",
            "fields": [
                { "name": "id", "type": "int" }
            ]
        }
        """;

    [Test]
    public async Task Preparation_AllSerializers_ResolvedIdMatchesWireFormat(CancellationToken cancellationToken)
    {
        var prefix = $"prepare-{Guid.NewGuid():N}";
        using var registry = CreateDekafClient();

        var genericSchema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{\"type\":\"integer\"}"
        };
        await using var generic = new SchemaRegistrySerializer<int>(
            registry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32BigEndian(span, value);
                writer.Advance(sizeof(int));
            },
            () => genericSchema);
        await AssertPreparedIdMatchesWireFormat(
            generic,
            await generic.PrepareAsync($"{prefix}-generic", 42, cancellationToken: cancellationToken),
            42,
            $"{prefix}-generic");

        var avroSchema = (Avro.RecordSchema)AvroSchema.Parse(AvroRecordSchema);
        var avroValue = new GenericRecord(avroSchema);
        avroValue.Add("id", 42);
        await using var avro = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await AssertPreparedIdMatchesWireFormat(
            avro,
            await avro.PrepareAsync($"{prefix}-avro", avroValue, cancellationToken: cancellationToken),
            avroValue,
            $"{prefix}-avro");

        var protobufValue = new TestPerson { Id = 42, Name = "Prepared" };
        await using var protobuf = new ProtobufSchemaRegistrySerializer<TestPerson>(registry);
        await AssertPreparedIdMatchesWireFormat(
            protobuf,
            await protobuf.PrepareAsync($"{prefix}-protobuf", protobufValue, cancellationToken: cancellationToken),
            protobufValue,
            $"{prefix}-protobuf");

        var jsonValue = new CompatibilityJsonRecord { Id = 42 };
        await using var json = new JsonSchemaRegistrySerializer<CompatibilityJsonRecord>(
            registry,
            "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"integer\"}}}");
        await AssertPreparedIdMatchesWireFormat(
            json,
            await json.PrepareAsync($"{prefix}-json", jsonValue, cancellationToken: cancellationToken),
            jsonValue,
            $"{prefix}-json");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Avro_ConfluentRegisteredSubject_CanBeLookedUpByDekaf(bool topicRecord, CancellationToken cancellationToken)
    {
        var topic = $"avro-subject-compat-{Guid.NewGuid():N}";
        using var confluentRegistry = CreateConfluentClient();
        var confluentSerializer = new Confluent.SchemaRegistry.Serdes.AvroSerializer<GenericRecord>(
            confluentRegistry,
            new Confluent.SchemaRegistry.Serdes.AvroSerializerConfig
            {
                SubjectNameStrategy = topicRecord
                    ? ConfluentSubjectNameStrategy.TopicRecord
                    : ConfluentSubjectNameStrategy.Record
            });
        var schema = (Avro.RecordSchema)AvroSchema.Parse(AvroRecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", 42);

        await confluentSerializer.SerializeAsync(
            record,
            new Confluent.Kafka.SerializationContext(MessageComponentType.Value, topic));

        using var dekafRegistry = CreateDekafClient();
        var expectedSubject = topicRecord
            ? $"{topic}-dekaf.compatibility.CompatibilityRecord"
            : "dekaf.compatibility.CompatibilityRecord";
        var registered = await dekafRegistry.GetSchemaBySubjectAsync(expectedSubject, cancellationToken: cancellationToken);
        await Assert.That(registered.Subject).IsEqualTo(expectedSubject);

        await using var dekafSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            dekafRegistry,
            new AvroSerializerConfig
            {
                SubjectNameStrategy = topicRecord
                    ? DekafSubjectNameStrategy.TopicRecordName
                    : DekafSubjectNameStrategy.RecordName,
                AutoRegisterSchemas = false
            });
        var destination = new ArrayBufferWriter<byte>();
        dekafSerializer.Serialize(record, ref destination, CreateDekafContext(topic));
    }

    [Test]
    public async Task Protobuf_ConfluentRegisteredSubject_CanBeLookedUpByDekaf(CancellationToken cancellationToken)
    {
        var topic = $"protobuf-subject-compat-{Guid.NewGuid():N}";
        using var confluentRegistry = CreateConfluentClient();
        Confluent.Kafka.IAsyncSerializer<TestPerson> confluentSerializer =
            new Confluent.SchemaRegistry.Serdes.ProtobufSerializer<TestPerson>(
                confluentRegistry,
                new Confluent.SchemaRegistry.Serdes.ProtobufSerializerConfig
                {
                    SubjectNameStrategy = ConfluentSubjectNameStrategy.Record
                });
        var value = new TestPerson { Id = 42, Name = "Compatibility", Email = "compat@example.com" };

        await confluentSerializer.SerializeAsync(
            value,
            new Confluent.Kafka.SerializationContext(MessageComponentType.Value, topic));

        using var dekafRegistry = CreateDekafClient();
        var expectedSubject = TestPerson.Descriptor.FullName;
        var registered = await dekafRegistry.GetSchemaBySubjectAsync(expectedSubject, cancellationToken: cancellationToken);
        await Assert.That(registered.Subject).IsEqualTo(expectedSubject);

        await using var dekafSerializer = new ProtobufSchemaRegistrySerializer<TestPerson>(
            dekafRegistry,
            new ProtobufSerializerConfig
            {
                SubjectNameStrategy = DekafSubjectNameStrategy.RecordName,
                AutoRegisterSchemas = false
            });
        var destination = new ArrayBufferWriter<byte>();
        dekafSerializer.Serialize(value, ref destination, CreateDekafContext(topic));
    }

    [Test]
    public async Task Protobuf_UseLatestVersion_ReorderedSchema_WritesIndexesOfRegisteredMessage(
        CancellationToken cancellationToken)
    {
        var topic = $"proto-latest-indexes-{Guid.NewGuid():N}";
        var subject = $"{topic}-value";
        var reordered = TestPerson.Descriptor.File.ToProto();
        reordered.MessageType.Insert(0, new Google.Protobuf.Reflection.DescriptorProto { Name = "Envelope" });
        using var registryClient = CreateDekafClient();
        var schemaId = await registryClient.RegisterSchemaAsync(subject, new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = reordered.ToByteString().ToBase64()
        }, cancellationToken);
        var latest = await registryClient.GetSchemaBySubjectAsync(subject, cancellationToken: cancellationToken);
        await Assert.That(latest.Schema.SchemaString).Contains("message Envelope");

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestPerson>(
            registryClient,
            new ProtobufSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true });
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(new TestPerson { Id = 7, Name = "Latest" }, ref destination, CreateDekafContext(topic));

        // Zigzag varints: one index, value 1, so TestPerson is the second message of the registered schema.
        var written = destination.WrittenMemory.ToArray();
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(written.AsSpan(1, 4))).IsEqualTo(schemaId);
        await Assert.That(written[5..7]).IsEquivalentTo(new byte[] { 2, 2 });
    }

    [Test]
    public async Task Protobuf_DekafRegisteredReferences_CanBeConsumedByConfluent(CancellationToken cancellationToken)
    {
        var topic = $"proto-reference-{Guid.NewGuid():N}";
        using var registryClient = CreateDekafClient();
        var versionOne = SchemaReferenceCommon.Descriptor.File.ToProto();
        versionOne.MessageType[0].Field.RemoveAt(1);
        await registryClient.RegisterSchemaAsync("Protos/reference_common.proto", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = versionOne.ToByteString().ToBase64()
        }, cancellationToken);

        await using var serializer = new ProtobufSchemaRegistrySerializer<SchemaReferenceEnvelope>(registryClient);
        var destination = new ArrayBufferWriter<byte>();
        var value = new SchemaReferenceEnvelope
        {
            Id = "envelope-1",
            Common = new SchemaReferenceCommon { Name = "shared", Revision = 2 }
        };
        serializer.Serialize(value, ref destination, CreateDekafContext(topic));

        var dependency = await registryClient.GetSchemaBySubjectAsync("Protos/reference_common.proto", cancellationToken: cancellationToken);
        var root = await registryClient.GetSchemaBySubjectAsync($"{topic}-value", cancellationToken: cancellationToken);
        await Assert.That(dependency.Version).IsEqualTo(2);
        await Assert.That(root.Schema.References).Count().IsEqualTo(1);
        await Assert.That(root.Schema.References![0].Name).IsEqualTo("Protos/reference_common.proto");
        await Assert.That(root.Schema.References[0].Version).IsEqualTo(2);

        using var confluentRegistry = CreateConfluentClient();
        Confluent.Kafka.IAsyncDeserializer<SchemaReferenceEnvelope> confluentDeserializer =
            new Confluent.SchemaRegistry.Serdes.ProtobufDeserializer<SchemaReferenceEnvelope>(confluentRegistry);
        var roundTrip = await confluentDeserializer.DeserializeAsync(
            destination.WrittenMemory,
            isNull: false,
            new Confluent.Kafka.SerializationContext(MessageComponentType.Value, topic));

        await Assert.That(roundTrip.Id).IsEqualTo(value.Id);
        await Assert.That(roundTrip.Common.Name).IsEqualTo(value.Common.Name);
        await Assert.That(roundTrip.Common.Revision).IsEqualTo(value.Common.Revision);
    }

    [Test]
    public async Task Json_ConfluentRegisteredSubject_CanBeLookedUpByDekaf(CancellationToken cancellationToken)
    {
        var topic = $"json-subject-compat-{Guid.NewGuid():N}";
        using var confluentRegistry = CreateConfluentClient();
        var confluentSerializer = new Confluent.SchemaRegistry.Serdes.JsonSerializer<CompatibilityJsonRecord>(
            confluentRegistry,
            new Confluent.SchemaRegistry.Serdes.JsonSerializerConfig
            {
                SubjectNameStrategy = ConfluentSubjectNameStrategy.Record
            });
        var value = new CompatibilityJsonRecord { Id = 42 };

        await confluentSerializer.SerializeAsync(
            value,
            new Confluent.Kafka.SerializationContext(MessageComponentType.Value, topic));

        using var dekafRegistry = CreateDekafClient();
        var subjects = await dekafRegistry.GetAllSubjectsAsync(cancellationToken);
        var subject = subjects.Single(static candidate => candidate.Contains(nameof(CompatibilityJsonRecord), StringComparison.Ordinal));
        var registered = await dekafRegistry.GetSchemaBySubjectAsync(subject, cancellationToken: cancellationToken);

        await using var dekafSerializer = new JsonSchemaRegistrySerializer<CompatibilityJsonRecord>(
            dekafRegistry,
            registered.Schema.SchemaString,
            subjectNameStrategy: DekafSubjectNameStrategy.RecordName,
            autoRegisterSchemas: false);
        var destination = new ArrayBufferWriter<byte>();
        dekafSerializer.Serialize(value, ref destination, CreateDekafContext(topic));
    }

    [Test]
    public async Task GenericSerializer_UsesConfluentAvroRuntimeSchemaSubject(CancellationToken cancellationToken)
    {
        var topic = $"generic-subject-compat-{Guid.NewGuid():N}";
        using var confluentRegistry = CreateConfluentClient();
        var confluentSerializer = new Confluent.SchemaRegistry.Serdes.AvroSerializer<GenericRecord>(
            confluentRegistry,
            new Confluent.SchemaRegistry.Serdes.AvroSerializerConfig
            {
                SubjectNameStrategy = ConfluentSubjectNameStrategy.Record
            });
        var schema = (Avro.RecordSchema)AvroSchema.Parse(AvroRecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", 42);
        await confluentSerializer.SerializeAsync(
            record,
            new Confluent.Kafka.SerializationContext(MessageComponentType.Value, topic));

        using var dekafRegistry = CreateDekafClient();
        var registered = await dekafRegistry.GetSchemaBySubjectAsync("dekaf.compatibility.CompatibilityRecord", cancellationToken: cancellationToken);
        await using var dekafSerializer = new SchemaRegistrySerializer<GenericRecord>(
            dekafRegistry,
            static (_, writer) => writer.Advance(0),
            _ => registered.Schema,
            DekafSubjectNameStrategy.RecordName,
            autoRegisterSchemas: false);
        var destination = new ArrayBufferWriter<byte>();
        dekafSerializer.Serialize(record, ref destination, CreateDekafContext(topic));
    }

    private Confluent.SchemaRegistry.CachedSchemaRegistryClient CreateConfluentClient() =>
        new(new Confluent.SchemaRegistry.SchemaRegistryConfig { Url = testInfra.RegistryUrl });

    private SchemaRegistryClient CreateDekafClient() =>
        new(new SchemaRegistryConfig { Url = testInfra.RegistryUrl });

    private static Dekaf.Serialization.SerializationContext CreateDekafContext(string topic) =>
        new()
        {
            Topic = topic,
            Component = SerializationComponent.Value
        };

    private static async Task AssertPreparedIdMatchesWireFormat<T>(
        Dekaf.Serialization.ISerializer<T> serializer,
        ResolvedSchemaContext resolved,
        T value,
        string topic)
    {
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(value, ref destination, CreateDekafContext(topic));

        await Assert.That(resolved.Subject).IsEqualTo($"{topic}-value");
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan.Slice(1, 4)))
            .IsEqualTo(resolved.SchemaId);
    }

    private sealed class CompatibilityJsonRecord
    {
        public int Id { get; init; }
    }
}
