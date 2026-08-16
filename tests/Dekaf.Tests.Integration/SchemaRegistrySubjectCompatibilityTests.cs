using System.Buffers;
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
    [Arguments(false)]
    [Arguments(true)]
    public async Task Avro_ConfluentRegisteredSubject_CanBeLookedUpByDekaf(bool topicRecord)
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
        var registered = await dekafRegistry.GetSchemaBySubjectAsync(expectedSubject);
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
    public async Task Protobuf_ConfluentRegisteredSubject_CanBeLookedUpByDekaf()
    {
        var topic = $"protobuf-subject-compat-{Guid.NewGuid():N}";
        using var confluentRegistry = CreateConfluentClient();
        var confluentSerializer = new Confluent.SchemaRegistry.Serdes.ProtobufSerializer<TestPerson>(
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
        var registered = await dekafRegistry.GetSchemaBySubjectAsync(expectedSubject);
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
    public async Task Protobuf_DekafRegisteredReferences_CanBeConsumedByConfluent()
    {
        var topic = $"proto-reference-{Guid.NewGuid():N}";
        using var registryClient = CreateDekafClient();
        var versionOne = SchemaReferenceCommon.Descriptor.File.ToProto();
        versionOne.MessageType[0].Field.RemoveAt(1);
        await registryClient.RegisterSchemaAsync("Protos/reference_common.proto", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = versionOne.ToByteString().ToBase64()
        });

        await using var serializer = new ProtobufSchemaRegistrySerializer<SchemaReferenceEnvelope>(registryClient);
        var destination = new ArrayBufferWriter<byte>();
        var value = new SchemaReferenceEnvelope
        {
            Id = "envelope-1",
            Common = new SchemaReferenceCommon { Name = "shared", Revision = 2 }
        };
        serializer.Serialize(value, ref destination, CreateDekafContext(topic));

        var dependency = await registryClient.GetSchemaBySubjectAsync("Protos/reference_common.proto");
        var root = await registryClient.GetSchemaBySubjectAsync($"{topic}-value");
        await Assert.That(dependency.Version).IsEqualTo(2);
        await Assert.That(root.Schema.References).Count().IsEqualTo(1);
        await Assert.That(root.Schema.References![0].Name).IsEqualTo("Protos/reference_common.proto");
        await Assert.That(root.Schema.References[0].Version).IsEqualTo(2);

        using var confluentRegistry = CreateConfluentClient();
        var confluentDeserializer =
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
    public async Task Json_ConfluentRegisteredSubject_CanBeLookedUpByDekaf()
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
        var subjects = await dekafRegistry.GetAllSubjectsAsync();
        var subject = subjects.Single(static candidate => candidate.Contains(nameof(CompatibilityJsonRecord), StringComparison.Ordinal));
        var registered = await dekafRegistry.GetSchemaBySubjectAsync(subject);

        await using var dekafSerializer = new JsonSchemaRegistrySerializer<CompatibilityJsonRecord>(
            dekafRegistry,
            registered.Schema.SchemaString,
            subjectNameStrategy: DekafSubjectNameStrategy.RecordName,
            autoRegisterSchemas: false);
        var destination = new ArrayBufferWriter<byte>();
        dekafSerializer.Serialize(value, ref destination, CreateDekafContext(topic));
    }

    [Test]
    public async Task GenericSerializer_UsesConfluentAvroRuntimeSchemaSubject()
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
        var registered = await dekafRegistry.GetSchemaBySubjectAsync("dekaf.compatibility.CompatibilityRecord");
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

    private sealed class CompatibilityJsonRecord
    {
        public int Id { get; init; }
    }
}
