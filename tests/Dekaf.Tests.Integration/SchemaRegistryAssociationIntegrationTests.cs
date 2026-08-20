using Dekaf.SchemaRegistry;

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
        await client.RegisterSchemaAsync(subject, new Schema
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
}
