using System.Buffers.Binary;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[ClassDataSource<KafkaWithAssociationSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
[Category("Serialization")]
public sealed class SchemaRegistryAssociationIntegrationTests(KafkaWithAssociationSchemaRegistryContainer testInfra)
{
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
