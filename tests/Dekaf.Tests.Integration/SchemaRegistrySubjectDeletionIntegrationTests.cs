using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[Timeout(120_000)]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistrySubjectDeletionIntegrationTests(KafkaWithSchemaRegistryContainer infrastructure)
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task DeleteThenReregister_RestoresSubject(bool normalize, bool permanent, CancellationToken cancellationToken)
    {
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig { Url = infrastructure.RegistryUrl });
        using var observer = new SchemaRegistryClient(new SchemaRegistryConfig { Url = infrastructure.RegistryUrl });
        var subject = $"subject-deletion-{Guid.NewGuid():N}";
        var schema = new Schema
        {
            SchemaString = $$"""{"type":"record","name":"DeletionRecord_{{Guid.NewGuid():N}}","fields":[{"name":"value","type":"string"}]}""",
            SchemaType = SchemaType.Avro
        };
        var originalId = await client.RegisterSchemaAsync(subject, schema, normalize, cancellationToken);

        await client.DeleteSubjectAsync(subject, cancellationToken: cancellationToken);
        if (permanent)
            await client.DeleteSubjectAsync(subject, permanent: true, cancellationToken: cancellationToken);
        await Assert.ThrowsAsync<SchemaRegistryException>(() => observer.GetSchemaBySubjectAsync(subject));

        var restoredId = await client.RegisterSchemaAsync(subject, schema, normalize, cancellationToken);
        var restored = await observer.GetSchemaBySubjectAsync(subject, cancellationToken: cancellationToken);

        await Assert.That(restored.Subject).IsEqualTo(subject);
        await Assert.That(restored.Id).IsEqualTo(restoredId);
        await Assert.That(Avro.Schema.Parse(restored.Schema.SchemaString)).IsEqualTo(Avro.Schema.Parse(schema.SchemaString));
        if (!permanent)
            await Assert.That(restoredId).IsEqualTo(originalId);
    }
}
