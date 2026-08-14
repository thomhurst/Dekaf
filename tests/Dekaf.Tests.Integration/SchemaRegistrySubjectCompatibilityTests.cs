using System.Buffers;
using Avro.Generic;
using Confluent.Kafka;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Integration.Protos;
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
