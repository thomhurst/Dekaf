using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Dekaf.Admin;
using Dekaf.Extensions.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dekaf.Tests.Aspire.Hosting;

public class DekafKafkaResourceTests
{
    [Test]
    public async Task AddDekafKafka_AddsBrokerContainerAndEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddDekafKafka("messaging", port: 19092);

        var endpoints = kafka.Resource.Annotations.OfType<EndpointAnnotation>().ToDictionary(e => e.Name);
        var image = kafka.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();

        await Assert.That(endpoints.Keys).IsEquivalentTo(["tcp", "internal"]);
        await Assert.That(endpoints["tcp"].TargetPort).IsEqualTo(9092);
        await Assert.That(endpoints["tcp"].Port).IsEqualTo(19092);
        await Assert.That(endpoints["tcp"].Protocol).IsEqualTo(ProtocolType.Tcp);
        await Assert.That(endpoints["internal"].TargetPort).IsEqualTo(9093);
        await Assert.That($"{image.Registry}/{image.Image}:{image.Tag}").IsEqualTo("docker.io/apache/kafka:4.3.1");
    }

    [Test]
    public async Task ConnectionString_IsTheHostBootstrapServer()
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddDekafKafka("messaging").WithEndpoint("tcp", e => e.AllocatedEndpoint = new(e, "localhost", 19092));

        var connectionString = await kafka.Resource.ConnectionStringExpression.GetValueAsync(default);
        var properties = ((IResourceWithConnectionString)kafka.Resource).GetConnectionProperties().ToDictionary(p => p.Key, p => p.Value);

        await Assert.That(connectionString).IsEqualTo("localhost:19092");
        await Assert.That(kafka.Resource.ConnectionStringExpression.ValueExpression).IsEqualTo("{messaging.bindings.tcp.host}:{messaging.bindings.tcp.port}");
        await Assert.That(properties.Keys).IsEquivalentTo(["Host", "Port"]);
        await Assert.That(await properties["Port"].GetValueAsync(default)).IsEqualTo("19092");
    }

    [Test]
    public async Task RunMode_ConfiguresSingleNodeKRaftWithShareGroups()
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddDekafKafka("messaging").WithEndpoint("tcp", e => e.AllocatedEndpoint = new(e, "localhost", 19092));

        var environment = await ResourceEnvironment.GetAsync(kafka.Resource, DistributedApplicationOperation.Run);

        await Assert.That(environment["KAFKA_PROCESS_ROLES"]).IsEqualTo("broker,controller");
        await Assert.That(environment["KAFKA_ADVERTISED_LISTENERS"])
            .IsEqualTo("PLAINTEXT://localhost:29092,PLAINTEXT_HOST://localhost:19092,PLAINTEXT_INTERNAL://messaging:9093");
        await Assert.That(environment["KAFKA_GROUP_SHARE_ENABLE"]).IsEqualTo("true");
        await Assert.That(environment["KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS"]).IsEqualTo("classic,consumer,share");
        await Assert.That(environment["KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR"]).IsEqualTo("1");
        await Assert.That(environment["KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR"]).IsEqualTo("1");
        await Assert.That(environment.ContainsKey("KAFKA_LOG_DIRS")).IsFalse();
    }

    [Test]
    public async Task PublishMode_AdvertisesDeferredEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder(["--operation", "publish"]);
        var kafka = builder.AddDekafKafka("messaging");

        var environment = await ResourceEnvironment.GetAsync(kafka.Resource, DistributedApplicationOperation.Publish);

        await Assert.That(environment["KAFKA_ADVERTISED_LISTENERS"]).IsEqualTo(
            "PLAINTEXT://localhost:29092,PLAINTEXT_HOST://{messaging.bindings.tcp.host}:{messaging.bindings.tcp.port},PLAINTEXT_INTERNAL://{messaging.bindings.internal.host}:{messaging.bindings.internal.port}");
    }

    [Test]
    public async Task WithDataVolumeAndBindMount_PersistTheLogDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();
        var volume = builder.AddDekafKafka("volume").WithDataVolume("kafka-data");
        var bind = builder.AddDekafKafka("bind").WithDataBindMount("data");

        var volumeMount = volume.Resource.Annotations.OfType<ContainerMountAnnotation>().Single();
        var bindMount = bind.Resource.Annotations.OfType<ContainerMountAnnotation>().Single();
        var environment = await ResourceEnvironment.GetAsync(volume.Resource, DistributedApplicationOperation.Publish);

        await Assert.That(volumeMount.Source).IsEqualTo("kafka-data");
        await Assert.That(volumeMount.Target).IsEqualTo("/var/lib/kafka/data");
        await Assert.That(volumeMount.Type).IsEqualTo(ContainerMountType.Volume);
        await Assert.That(bindMount.Type).IsEqualTo(ContainerMountType.BindMount);
        await Assert.That(bindMount.IsReadOnly).IsFalse();
        await Assert.That(volumeMount.IsReadOnly).IsFalse();
        await Assert.That(environment["KAFKA_LOG_DIRS"]).IsEqualTo("/var/lib/kafka/data");
    }

    [Test]
    public async Task WithKafkaUI_AddsOneContainerForAllBrokers()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddDekafKafka("first").WithKafkaUI(ui => ui.WithHostPort(18080));
        builder.AddDekafKafka("second").WithKafkaUI();

        var ui = builder.Resources.OfType<DekafKafkaUIContainerResource>().Single();
        var endpoint = ui.Annotations.OfType<EndpointAnnotation>().Single();

        await Assert.That(ui.Name).IsEqualTo("kafka-ui");
        await Assert.That(endpoint.TargetPort).IsEqualTo(8080);
        await Assert.That(endpoint.Port).IsEqualTo(18080);
    }

    [Test]
    public async Task HealthCheck_UsesADekafAdminClientPerBroker()
    {
        var builder = DistributedApplication.CreateBuilder();
        var first = builder.AddDekafKafka("first").WithEndpoint("tcp", e => e.AllocatedEndpoint = new(e, "localhost", 19092));
        var second = builder.AddDekafKafka("second").WithEndpoint("tcp", e => e.AllocatedEndpoint = new(e, "localhost", 19093));

        using var app = builder.Build();
        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Where(r => r.Name.EndsWith("_check", StringComparison.Ordinal)).ToArray();
        await builder.Eventing.PublishAsync(new ConnectionStringAvailableEvent(first.Resource, app.Services));
        await builder.Eventing.PublishAsync(new ConnectionStringAvailableEvent(second.Resource, app.Services));

        await Assert.That(registrations.Select(r => r.Name)).IsEquivalentTo(["first_check", "second_check"]);
        await Assert.That(registrations[0].Factory(app.Services)).IsTypeOf<DekafBrokerHealthCheck>();
        await Assert.That(app.Services.GetRequiredKeyedService<IAdminClient>("first_check"))
            .IsNotSameReferenceAs(app.Services.GetRequiredKeyedService<IAdminClient>("second_check"));
        await Assert.That(first.Resource.Annotations.OfType<HealthCheckAnnotation>().Single().Key).IsEqualTo("first_check");
    }
}
