using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistrySubjectDeletionIntegrationTests(KafkaWithSchemaRegistryContainer infrastructure)
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task DeleteThenReregister_RestoresSubject(bool normalize, bool permanent)
    {
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig { Url = infrastructure.RegistryUrl });
        using var observer = new SchemaRegistryClient(new SchemaRegistryConfig { Url = infrastructure.RegistryUrl });
        var subject = $"subject-deletion-{Guid.NewGuid():N}";
        var schema = new Schema
        {
            SchemaString = $$"""{"type":"record","name":"DeletionRecord_{{Guid.NewGuid():N}}","fields":[{"name":"value","type":"string"}]}""",
            SchemaType = SchemaType.Avro
        };
        var originalId = await client.RegisterSchemaAsync(subject, schema, normalize);

        await client.DeleteSubjectAsync(subject);
        if (permanent)
            await client.DeleteSubjectAsync(subject, permanent: true);
        await Assert.ThrowsAsync<SchemaRegistryException>(() => observer.GetSchemaBySubjectAsync(subject));

        var restoredId = await client.RegisterSchemaAsync(subject, schema, normalize);
        var restored = await observer.GetSchemaBySubjectAsync(subject);

        await Assert.That(restored.Subject).IsEqualTo(subject);
        await Assert.That(restored.Id).IsEqualTo(restoredId);
        await Assert.That(Avro.Schema.Parse(restored.Schema.SchemaString)).IsEqualTo(Avro.Schema.Parse(schema.SchemaString));
        if (!permanent)
            await Assert.That(restoredId).IsEqualTo(originalId);
    }
}
