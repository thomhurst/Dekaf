using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for schema evolution and compatibility checking via Schema Registry.
/// </summary>
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaEvolutionTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task BackwardCompatible_AddOptionalField_IsCompatible()
    {
        var subject = $"compat-backward-{Guid.NewGuid():N}-value";

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        // Register v1 schema
        var v1Schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = """
                {
                    "type": "record",
                    "name": "User",
                    "namespace": "test.compat",
                    "fields": [
                        { "name": "id", "type": "int" },
                        { "name": "name", "type": "string" }
                    ]
                }
                """
        };

        await registryClient.RegisterSchemaAsync(subject, v1Schema);

        // v2 adds an optional field with default - backward compatible
        var v2Schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = """
                {
                    "type": "record",
                    "name": "User",
                    "namespace": "test.compat",
                    "fields": [
                        { "name": "id", "type": "int" },
                        { "name": "name", "type": "string" },
                        { "name": "email", "type": ["null", "string"], "default": null }
                    ]
                }
                """
        };

        var isCompatible = await registryClient.IsCompatibleAsync(subject, v2Schema);
        await Assert.That(isCompatible).IsTrue();
    }

    [Test]
    public async Task IncompatibleChange_ChangeFieldType_IsNotCompatible()
    {
        var subject = $"compat-incompat-{Guid.NewGuid():N}-value";

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        // Register v1 schema
        var v1Schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = """
                {
                    "type": "record",
                    "name": "Event",
                    "namespace": "test.compat",
                    "fields": [
                        { "name": "id", "type": "int" },
                        { "name": "name", "type": "string" }
                    ]
                }
                """
        };

        await registryClient.RegisterSchemaAsync(subject, v1Schema);

        // v2 changes "name" from string to int - incompatible
        var v2Schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = """
                {
                    "type": "record",
                    "name": "Event",
                    "namespace": "test.compat",
                    "fields": [
                        { "name": "id", "type": "int" },
                        { "name": "name", "type": "int" }
                    ]
                }
                """
        };

        var isCompatible = await registryClient.IsCompatibleAsync(subject, v2Schema);
        await Assert.That(isCompatible).IsFalse();
    }

    [Test]
    public async Task MultipleVersions_RegisterTwoVersions_BothTracked()
    {
        var subject = $"compat-versions-{Guid.NewGuid():N}-value";

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        // Register v1
        var v1Schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = """
                {
                    "type": "record",
                    "name": "Metric",
                    "namespace": "test.compat",
                    "fields": [
                        { "name": "name", "type": "string" },
                        { "name": "value", "type": "double" }
                    ]
                }
                """
        };

        await registryClient.RegisterSchemaAsync(subject, v1Schema);

        // Register v2
        var v2Schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = """
                {
                    "type": "record",
                    "name": "Metric",
                    "namespace": "test.compat",
                    "fields": [
                        { "name": "name", "type": "string" },
                        { "name": "value", "type": "double" },
                        { "name": "unit", "type": ["null", "string"], "default": null }
                    ]
                }
                """
        };

        await registryClient.RegisterSchemaAsync(subject, v2Schema);

        var versions = await registryClient.GetVersionsAsync(subject);
        await Assert.That(versions).Count().IsEqualTo(2);
        await Assert.That(versions).Contains(1);
        await Assert.That(versions).Contains(2);
    }

    [Test]
    public async Task GetByVersion_SpecificVersion_ReturnsCorrectSchema()
    {
        var subject = $"compat-getver-{Guid.NewGuid():N}-value";

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        // Register v1
        var v1Schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = """
                {
                    "type": "record",
                    "name": "Config",
                    "namespace": "test.compat",
                    "fields": [
                        { "name": "key", "type": "string" }
                    ]
                }
                """
        };

        await registryClient.RegisterSchemaAsync(subject, v1Schema);

        // Register v2
        var v2Schema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = """
                {
                    "type": "record",
                    "name": "Config",
                    "namespace": "test.compat",
                    "fields": [
                        { "name": "key", "type": "string" },
                        { "name": "value", "type": ["null", "string"], "default": null }
                    ]
                }
                """
        };

        await registryClient.RegisterSchemaAsync(subject, v2Schema);

        // Get v1
        var registered1 = await registryClient.GetSchemaBySubjectAsync(subject, "1");
        await Assert.That(registered1.Version).IsEqualTo(1);
        await Assert.That(registered1.Schema.SchemaString).Contains("\"key\"");
        await Assert.That(registered1.Schema.SchemaString).DoesNotContain("\"value\"");

        // Get latest (v2)
        var registeredLatest = await registryClient.GetSchemaBySubjectAsync(subject, "latest");
        await Assert.That(registeredLatest.Version).IsEqualTo(2);
        await Assert.That(registeredLatest.Schema.SchemaString).Contains("\"value\"");
    }
}
