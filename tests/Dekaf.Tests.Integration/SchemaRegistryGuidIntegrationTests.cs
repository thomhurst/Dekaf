using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[NotInParallel]
[ClassDataSource<KafkaWithAssociationSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryGuidIntegrationTests(KafkaWithAssociationSchemaRegistryContainer testInfra)
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

        await Assert.That(registered.Guid).IsNotNull();

        using var uncachedClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl,
            MaxCachedSchemas = 0
        });
        var actual = await uncachedClient.GetSchemaByGuidAsync(registered.Guid!);

        await Assert.That(actual.SchemaType).IsEqualTo(registered.Schema.SchemaType);
        await Assert.That(actual.SchemaString).IsEqualTo(registered.Schema.SchemaString);
    }
}
