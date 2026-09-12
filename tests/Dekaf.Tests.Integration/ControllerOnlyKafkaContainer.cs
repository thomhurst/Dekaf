using System.Net;
using System.Net.Sockets;
using System.Text;
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
        // Kafka 4.0's Docker launcher rejects KAFKA_ADVERTISED_LISTENERS on controllers.
        // Configure Kafka directly so discovery returns the endpoint reachable from the host.
        var config = $"""
            process.roles=controller
            node.id=1
            controller.quorum.voters=1@localhost:{_hostPort}
            controller.listener.names=CONTROLLER
            listeners=CONTROLLER://0.0.0.0:{_hostPort}
            advertised.listeners=CONTROLLER://{BootstrapControllers}
            listener.security.protocol.map=CONTROLLER:PLAINTEXT
            log.dirs=/tmp/controller-logs
            """;

        _container = new ContainerBuilder($"apache/kafka:{KafkaContainerDefault.ImageTag}")
            .WithName($"dekaf-controller-only-{Guid.NewGuid():N}")
            .WithPortBinding(_hostPort, _hostPort)
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx384m -Xms384m")
            .WithResourceMapping(Encoding.UTF8.GetBytes(config), "/tmp/controller.properties")
            .WithEntrypoint("/bin/bash")
            .WithCommand("-ec", "/opt/kafka/bin/kafka-storage.sh format -t MkU3OEVBNTcwNTJENDM2Qk -c /tmp/controller.properties; exec /opt/kafka/bin/kafka-server-start.sh /tmp/controller.properties")
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
