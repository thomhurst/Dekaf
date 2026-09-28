using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Dekaf.Tests.Aspire.Hosting;

public class DekafSchemaRegistryResourceTests
{
    [Test]
    public async Task AddDekafSchemaRegistry_AddsRegistryContainerThatWaitsForKafka()
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddDekafKafka("messaging");
        var registry = builder.AddDekafSchemaRegistry("schema-registry", kafka, port: 18081);

        var endpoint = registry.Resource.Annotations.OfType<EndpointAnnotation>().Single();
        var image = registry.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        var waits = registry.Resource.Annotations.OfType<WaitAnnotation>().Select(w => w.Resource).ToArray();

        await Assert.That(endpoint.Name).IsEqualTo("http");
        await Assert.That(endpoint.TargetPort).IsEqualTo(8081);
        await Assert.That(endpoint.Port).IsEqualTo(18081);
        await Assert.That($"{image.Registry}/{image.Image}:{image.Tag}").IsEqualTo("docker.io/confluentinc/cp-schema-registry:8.2.0");
        await Assert.That(waits).Contains(kafka.Resource);
        await Assert.That(registry.Resource.Annotations.OfType<HealthCheckAnnotation>().Any()).IsTrue();
    }

    [Test]
    public async Task PublishMode_UsesTheKafkaContainerNetworkListener()
    {
        var builder = DistributedApplication.CreateBuilder(["--operation", "publish"]);
        var kafka = builder.AddDekafKafka("messaging");
        var registry = builder.AddDekafSchemaRegistry("schema-registry", kafka);

        var environment = await ResourceEnvironment.GetAsync(registry.Resource, DistributedApplicationOperation.Publish);

        await Assert.That(environment["SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS"])
            .IsEqualTo("PLAINTEXT://{messaging.bindings.internal.host}:{messaging.bindings.internal.port}");
        await Assert.That(environment["SCHEMA_REGISTRY_LISTENERS"])
            .IsEqualTo("{schema-registry.bindings.http.scheme}://0.0.0.0:{schema-registry.bindings.http.targetPort}");
        await Assert.That(environment["SCHEMA_REGISTRY_HOST_NAME"]).IsEqualTo("{schema-registry.bindings.http.host}");
        await Assert.That(environment["SCHEMA_REGISTRY_KAFKASTORE_TOPIC_REPLICATION_FACTOR"]).IsEqualTo("1");
    }

    [Test]
    public async Task ConnectionString_IsTheRegistryUrl()
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddDekafKafka("messaging");
        var registry = builder.AddDekafSchemaRegistry("schema-registry", kafka)
            .WithEndpoint("http", e => e.AllocatedEndpoint = new(e, "localhost", 18081));

        var connectionString = await registry.Resource.ConnectionStringExpression.GetValueAsync(default);
        var properties = ((IResourceWithConnectionString)registry.Resource).GetConnectionProperties().ToDictionary(p => p.Key, p => p.Value);

        await Assert.That(connectionString).IsEqualTo("http://localhost:18081");
        await Assert.That(properties.Keys).IsEquivalentTo(["Host", "Port", "Uri"]);
        await Assert.That(await properties["Uri"].GetValueAsync(default)).IsEqualTo("http://localhost:18081");
    }

    [Test]
    public async Task Arguments_AreValidated()
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddDekafKafka("messaging");

        await Assert.That(() => builder.AddDekafSchemaRegistry("registry", null!)).Throws<ArgumentNullException>();
    }
}
