using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[NotInParallel]
[Timeout(120_000)]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryCompatibilityConfigurationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task CompatibilityConfiguration_GlobalAndSubjectPoliciesRoundTripIndependently(CancellationToken cancellationToken)
    {
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var originalGlobal = await client.GetCompatibilityAsync(cancellationToken: cancellationToken);

        try
        {
            var globalAcknowledged = await client.UpdateCompatibilityAsync(SchemaCompatibilityLevel.BackwardTransitive, cancellationToken: cancellationToken);
            await Assert.That(globalAcknowledged).IsEqualTo(SchemaCompatibilityLevel.BackwardTransitive);
            await Assert.That(await client.GetCompatibilityAsync(cancellationToken: cancellationToken))
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
            }, cancellationToken);

            var subjectAcknowledged = await client.UpdateCompatibilityAsync(SchemaCompatibilityLevel.FullTransitive, subject, cancellationToken);

            await Assert.That(subjectAcknowledged).IsEqualTo(SchemaCompatibilityLevel.FullTransitive);
            await Assert.That(await client.GetCompatibilityAsync(subject, cancellationToken))
                .IsEqualTo(SchemaCompatibilityLevel.FullTransitive);
            await Assert.That(await client.GetCompatibilityAsync(cancellationToken: cancellationToken))
                .IsEqualTo(SchemaCompatibilityLevel.BackwardTransitive);
        }
        finally
        {
            await client.UpdateCompatibilityAsync(originalGlobal, cancellationToken: cancellationToken);
        }
    }
}
