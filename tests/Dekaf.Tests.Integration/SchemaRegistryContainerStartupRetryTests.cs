using System.Net;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[Timeout(180_000)]
public sealed class SchemaRegistryContainerStartupRetryTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InitializeAsync_TransientStartupFailure_ReplacesAndRemovesFailedResources(
        bool failSchemaRegistry,
        CancellationToken cancellationToken)
    {
        await using var fixture = new FailingStartupContainer(failSchemaRegistry);

        await fixture.InitializeAsync();

        await Assert.That(fixture.KafkaAttempts).IsEqualTo(2);
        await Assert.That(fixture.FailedContainerId).IsNotNull();
        using var docker = fixture.DockerEndpoint.GetDockerClientBuilder(Guid.NewGuid()).Build();
        var inspectContainer = async () =>
            await docker.Containers.InspectContainerAsync(fixture.FailedContainerId!, cancellationToken);
        var containerError = await Assert.That(inspectContainer).Throws<DockerApiException>();
        await Assert.That(containerError!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var inspectNetwork = async () =>
            await docker.Networks.InspectNetworkAsync(fixture.FailedNetworkName!, cancellationToken: cancellationToken);
        var networkError = await Assert.That(inspectNetwork).Throws<DockerApiException>();
        await Assert.That(networkError!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        using var http = new HttpClient();
        using var response = await http.GetAsync($"{fixture.RegistryUrl}/subjects", cancellationToken);
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }

    private sealed class FailingStartupContainer(bool failSchemaRegistry) : KafkaWithSchemaRegistryContainer
    {
        private int _registryAttempts;
        public int KafkaAttempts { get; private set; }
        public string? FailedContainerId { get; private set; }
        public string? FailedNetworkName { get; private set; }
        public IDockerEndpointAuthenticationConfiguration DockerEndpoint { get; } =
            TestcontainersSettings.OS.DockerEndpointAuthConfig;

        protected override KafkaBuilder ConfigureKafkaBuilder(KafkaBuilder builder) =>
            builder.WithCommand(KafkaTestContainer.StartupCommandOverride);

        protected override async Task StartKafkaContainerAsync(KafkaContainer container, CancellationToken cancellationToken)
        {
            KafkaAttempts++;
            await base.StartKafkaContainerAsync(container, cancellationToken);
            if (!failSchemaRegistry && KafkaAttempts == 1)
                await FailStartupAsync(container, cancellationToken);
        }

        protected override async Task StartSchemaRegistryContainerAsync(IContainer container, CancellationToken cancellationToken)
        {
            _registryAttempts++;
            await base.StartSchemaRegistryContainerAsync(container, cancellationToken);
            if (failSchemaRegistry && _registryAttempts == 1)
                await FailStartupAsync(container, cancellationToken);
        }

        private async Task FailStartupAsync(IContainer container, CancellationToken cancellationToken)
        {
            FailedContainerId = container.Id;
            using var docker = DockerEndpoint.GetDockerClientBuilder(Guid.NewGuid()).Build();
            var details = await docker.Containers.InspectContainerAsync(container.Id, cancellationToken);
            FailedNetworkName = details.NetworkSettings!.Networks!.Keys.Single();
            // Inject the observed race after resource creation, so cleanup is exercised too.
            throw new ContainerNotRunningException(
                container.Id,
                string.Empty,
                "/bin/sh: /testcontainers.sh: Text file busy",
                126,
                new InvalidOperationException("injected startup failure"));
        }
    }
}
