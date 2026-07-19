using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

public sealed class ControllerOnlyKafkaContainer : IAsyncInitializer, IAsyncDisposable
{
    private readonly int _hostPort = GetFreeTcpPort();
    private IContainer? _container;

    public string BootstrapControllers => $"127.0.0.1:{_hostPort}";

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder($"apache/kafka:{KafkaContainerDefault.ImageTag}")
            .WithName($"dekaf-controller-only-{Guid.NewGuid():N}")
            .WithPortBinding(_hostPort, _hostPort)
            .WithEnvironment("CLUSTER_ID", "MkU3OEVBNTcwNTJENDM2Qk")
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "controller")
            .WithEnvironment("KAFKA_LISTENERS", $"CONTROLLER://0.0.0.0:{_hostPort}")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "CONTROLLER:PLAINTEXT")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", $"1@localhost:{_hostPort}")
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx384m -Xms384m")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(_hostPort))
            .Build();

        await _container.StartAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
