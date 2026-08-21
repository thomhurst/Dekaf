using System.Buffers.Binary;
using Avro.Generic;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Integration.Protos;
using Google.Protobuf;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Integration;

[ClassDataSource<KafkaWithAssociationSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
[Category("Serialization")]
public sealed class SchemaRegistryAssociationIntegrationTests(KafkaWithAssociationSchemaRegistryContainer testInfra)
{
    private const string AvroRecordSchema = """
        {
            "type": "record",
            "name": "AssociatedOrder",
            "namespace": "dekaf.associations",
            "fields": [{ "name": "id", "type": "int" }]
        }
        """;

    [Test]
    public async Task Association_CreateListDelete_RoundTripsAgainstSchemaRegistry()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var resourceName = $"orders-{suffix}";
        var resourceNamespace = $"cluster-{suffix}";
        var resourceId = $"{resourceNamespace}:{resourceName}";
        var subject = $"{resourceName}-value";
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var schemaId = await client.RegisterSchemaAsync(subject, new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{\"type\":\"object\"}"
        });
        var request = new AssociationCreateOrUpdateRequest
        {
            ResourceName = resourceName,
            ResourceNamespace = resourceNamespace,
            ResourceId = resourceId,
            ResourceType = "topic",
            Associations =
            [
                new AssociationCreateOrUpdateInfo
                {
                    Subject = subject,
                    AssociationType = "value",
                    Lifecycle = "WEAK",
                    Frozen = false
                }
            ]
        };

        var created = await client.CreateAssociationAsync(request);
        try
        {
            var found = await client.GetAssociationsByResourceNameAsync(
                resourceName,
                resourceNamespace: "-",
                resourceType: "topic",
                associationTypes: ["value"],
                lifecycle: "WEAK");

            await Assert.That(created.ResourceId).IsEqualTo(resourceId);
            await Assert.That(created.Associations).Count().IsEqualTo(1);
            await Assert.That(found).Count().IsEqualTo(1);
            await Assert.That(found[0].Subject).IsEqualTo(subject);

            await using var serializer = new SchemaRegistrySerializer<int>(
                client,
                static (_, _) => { },
                static () => new Schema
                {
                    SchemaType = SchemaType.Json,
                    SchemaString = "{\"type\":\"object\"}"
                },
                SubjectNameStrategy.AssociatedName);
            var resolved = await serializer.PrepareAsync(resourceName, 42);
            await Assert.That(resolved.Subject).IsEqualTo(subject);

            var ruleExecutor = new CapturingRuleExecutor();
            await using var deserializer = SchemaRegistryDeserializer.Create<int>(
                client,
                static (_, _) => 42,
                new SchemaRegistryDeserializerConfig
                {
                    SubjectNameStrategy = SubjectNameStrategy.AssociatedName
                },
                ruleExecutor: ruleExecutor);
            var preparer = (IAsyncDeserializerPreparer<int>)deserializer;
            var context = new SerializationContext
            {
                Topic = resourceName,
                Component = SerializationComponent.Value
            };
            var data = new byte[5];
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(1), schemaId);

            await Assert.That(preparer.TryDeserialize(data, context, out _)).IsFalse();
            await preparer.PrepareAsync(data, context);
            await Assert.That(preparer.TryDeserialize(data, context, out var value)).IsTrue();
            await Assert.That(value).IsEqualTo(42);
            await Assert.That(ruleExecutor.Subject).IsEqualTo(subject);
        }
        finally
        {
            await client.DeleteAssociationsAsync(
                resourceId,
                resourceType: "topic",
                associationTypes: ["value"]);
        }

        var remaining = await client.GetAssociationsByResourceNameAsync(resourceName, resourceNamespace);

        await Assert.That(remaining).IsEmpty();
    }

    [Test]
    public async Task AssociatedName_AllFormatSerializers_PrepareAgainstSchemaRegistry()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var avroTopic = $"associated-avro-{suffix}";
        var jsonTopic = $"associated-json-{suffix}";
        var protobufTopic = $"associated-protobuf-{suffix}";
        var avroSubject = $"{avroTopic}-governed";
        var jsonSubject = $"{jsonTopic}-governed";
        var protobufSubject = $"{protobufTopic}-governed";
        await RegisterAssociatedSchemaAsync(
            client,
            avroTopic,
            avroSubject,
            new Schema { SchemaType = SchemaType.Avro, SchemaString = AvroRecordSchema });
        await RegisterAssociatedSchemaAsync(
            client,
            jsonTopic,
            jsonSubject,
            new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"integer\"}}}"
            });
        await RegisterAssociatedSchemaAsync(
            client,
            protobufTopic,
            protobufSubject,
            new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = TestPerson.Descriptor.File.ToProto().ToByteString().ToBase64()
            });

        try
        {
            var avroSchema = (Avro.RecordSchema)AvroSchema.Parse(AvroRecordSchema);
            var avroValue = new GenericRecord(avroSchema);
            avroValue.Add("id", 42);
            await using var avro = new AvroSchemaRegistrySerializer<GenericRecord>(
                client,
                new AvroSerializerConfig
                {
                    SubjectNameStrategy = SubjectNameStrategy.AssociatedName,
                    AutoRegisterSchemas = false,
                    UseLatestVersion = true
                });
            await using var json = new JsonSchemaRegistrySerializer<AssociatedJsonRecord>(
                client,
                "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"integer\"}}}",
                subjectNameStrategy: SubjectNameStrategy.AssociatedName,
                autoRegisterSchemas: false);
            await using var protobuf = new ProtobufSchemaRegistrySerializer<TestPerson>(
                client,
                new ProtobufSerializerConfig
                {
                    SubjectNameStrategy = SubjectNameStrategy.AssociatedName,
                    AutoRegisterSchemas = false,
                    UseLatestVersion = true
                });

            var avroPrepared = await avro.PrepareAsync(avroTopic, avroValue);
            var jsonPrepared = await json.PrepareAsync(jsonTopic, new AssociatedJsonRecord { Id = 42 });
            var protobufPrepared = await protobuf.PrepareAsync(
                protobufTopic,
                new TestPerson { Id = 42, Name = "Associated" });

            await Assert.That(avroPrepared.Subject).IsEqualTo(avroSubject);
            await Assert.That(jsonPrepared.Subject).IsEqualTo(jsonSubject);
            await Assert.That(protobufPrepared.Subject).IsEqualTo(protobufSubject);
        }
        finally
        {
            await DeleteAssociationAsync(client, avroTopic);
            await DeleteAssociationAsync(client, jsonTopic);
            await DeleteAssociationAsync(client, protobufTopic);
        }
    }

    private static async Task RegisterAssociatedSchemaAsync(
        ISchemaRegistryClient client,
        string topic,
        string subject,
        Schema schema)
    {
        _ = await client.RegisterSchemaAsync(subject, schema);
        _ = await client.CreateAssociationAsync(new AssociationCreateOrUpdateRequest
        {
            ResourceName = topic,
            ResourceNamespace = AssociatedNameStrategy.NamespaceWildcard,
            ResourceId = topic,
            ResourceType = "topic",
            Associations =
            [
                new AssociationCreateOrUpdateInfo
                {
                    Subject = subject,
                    AssociationType = "value",
                    Lifecycle = "WEAK"
                }
            ]
        });
    }

    private static Task DeleteAssociationAsync(ISchemaRegistryClient client, string topic) =>
        client.DeleteAssociationsAsync(topic, "topic", ["value"]);

    private sealed class AssociatedJsonRecord
    {
        public int Id { get; init; }
    }

    private sealed class CapturingRuleExecutor : ISchemaRegistryRuleExecutor
    {
        internal string? Subject { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            Subject = context.Subject;
            return payload;
        }
    }
}
