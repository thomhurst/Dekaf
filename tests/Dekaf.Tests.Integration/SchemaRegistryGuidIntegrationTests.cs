using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[NotInParallel]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryGuidIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task GuidLookup_WhenRegistryExposesGuid_ReturnsRegisteredSchema()
    {
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var subject = $"guid-lookup-{Guid.NewGuid():N}-value";
        var expected = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "title": "GuidLookup",
                  "type": "object",
                  "properties": { "id": { "type": "integer" } }
                }
                """
        };

        await client.RegisterSchemaAsync(subject, expected);
        var registered = await client.GetSchemaBySubjectAsync(subject);

        if (registered.Guid is not { } guid)
        {
            Skip.Test("The configured Schema Registry version does not expose schema GUIDs.");
            return;
        }

        using var uncachedClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl,
            MaxCachedSchemas = 0
        });
        var actual = await uncachedClient.GetSchemaByGuidAsync(guid);

        await Assert.That(actual.SchemaType).IsEqualTo(expected.SchemaType);
        await Assert.That(actual.SchemaString).IsEqualTo(expected.SchemaString);
    }
}
