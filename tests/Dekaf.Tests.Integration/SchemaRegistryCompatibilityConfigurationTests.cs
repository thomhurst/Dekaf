using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[NotInParallel]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryCompatibilityConfigurationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task CompatibilityConfiguration_GlobalAndSubjectPoliciesRoundTripIndependently()
    {
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var originalGlobal = await client.GetCompatibilityAsync();

        try
        {
            var globalAcknowledged = await client.UpdateCompatibilityAsync(
                SchemaCompatibilityLevel.BackwardTransitive);
            await Assert.That(globalAcknowledged).IsEqualTo(SchemaCompatibilityLevel.BackwardTransitive);
            await Assert.That(await client.GetCompatibilityAsync())
                .IsEqualTo(SchemaCompatibilityLevel.BackwardTransitive);

            var subject = $"compatibility-{Guid.NewGuid():N}-value";
            await client.RegisterSchemaAsync(subject, new Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString = $$"""
                    {
                      "type": "record",
                      "name": "Compatibility{{Guid.NewGuid():N}}",
                      "fields": [ { "name": "id", "type": "long" } ]
                    }
                    """
            });

            var subjectAcknowledged = await client.UpdateCompatibilityAsync(
                SchemaCompatibilityLevel.FullTransitive,
                subject);

            await Assert.That(subjectAcknowledged).IsEqualTo(SchemaCompatibilityLevel.FullTransitive);
            await Assert.That(await client.GetCompatibilityAsync(subject))
                .IsEqualTo(SchemaCompatibilityLevel.FullTransitive);
            await Assert.That(await client.GetCompatibilityAsync())
                .IsEqualTo(SchemaCompatibilityLevel.BackwardTransitive);
        }
        finally
        {
            await client.UpdateCompatibilityAsync(originalGlobal);
        }
    }
}
