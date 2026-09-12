using System.Text;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
[ClassDataSource<DynamicQuorumKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class DynamicQuorumIntegrationTests(DynamicQuorumKafkaContainer kafka)
{
    [Test]
    public async Task AddAndRemoveVoter_ChangesRealControllerMembership(CancellationToken cancellationToken)
    {
        await using var admin = new AdminClientBuilder().WithBootstrapControllers(kafka.BootstrapControllers).Build();
        var observed = await TestWait.WaitForConditionAsync(
            async () => await admin.DescribeMetadataQuorumAsync(cancellationToken),
            quorum => quorum.Observers.Any(replica => replica.ReplicaId == 2 && replica.ReplicaDirectoryId is not null),
            description: "second controller joins as an observer");
        var observer = observed.Observers.Single(replica => replica.ReplicaId == 2);
        await Assert.That(observed.CurrentVoters.Select(replica => replica.ReplicaId)).IsEquivalentTo([1]);
        var directoryId = observer.ReplicaDirectoryId!.Value;

        await admin.AddRaftVoterAsync(2, directoryId,
            [new RaftVoterEndpoint { Name = "CONTROLLER", Host = "controller-2", Port = 9093 }], cancellationToken: cancellationToken);
        var expanded = await TestWait.WaitForConditionAsync(
            async () => await admin.DescribeMetadataQuorumAsync(cancellationToken),
            quorum => quorum.CurrentVoters.Count == 2, description: "AddRaftVoter is committed");
        await Assert.That(expanded.CurrentVoters.Single(replica => replica.ReplicaId == 2).ReplicaDirectoryId).IsEqualTo(directoryId);

        await admin.RemoveRaftVoterAsync(2, directoryId, cancellationToken: cancellationToken);
        await TestWait.WaitForConditionAsync(
            async () => await admin.DescribeMetadataQuorumAsync(cancellationToken),
            quorum => quorum.CurrentVoters.Count == 1 && quorum.CurrentVoters[0].ReplicaId == 1,
            description: "RemoveRaftVoter is committed");
    }
}

public sealed class DynamicQuorumKafkaContainer : IAsyncInitializer, IAsyncDisposable
{
    private readonly INetwork _network = new NetworkBuilder().Build();
    private IContainer? _leader;
    private IContainer? _observer;
    private const int ExternalPort = 9094;
    public string BootstrapControllers => _leader is { } leader
        ? $"{leader.Hostname}:{leader.GetMappedPublicPort(ExternalPort)}"
        : throw new InvalidOperationException("Controller has not started.");

    public async Task InitializeAsync()
    {
        await _network.CreateAsync();
        _leader = CreateController(1);
        _observer = CreateController(2);
        await _leader.StartAsync();
        await _observer.StartAsync();
    }

    private IContainer CreateController(int nodeId)
    {
        var format = nodeId == 1 ? "--standalone" : "--no-initial-controllers";
        return new ContainerBuilder($"apache/kafka:{KafkaContainerDefault.ImageTag}")
            .WithNetwork(_network).WithNetworkAliases($"controller-{nodeId}")
            .WithPortBinding(ExternalPort, true)
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx384m -Xms384m")
            .WithEntrypoint("/bin/bash")
            .WithCommand("-ec", $"while [ ! -f /tmp/controller.ready ]; do sleep 0.1; done; /opt/kafka/bin/kafka-storage.sh format -t MkU3OEVBNTcwNTJENDM2Qk -c /tmp/controller.properties {format}; exec /opt/kafka/bin/kafka-server-start.sh /tmp/controller.properties")
            .WithStartupCallback(async (container, cancellationToken) =>
            {
                // Docker owns the host port before Kafka advertises it. Signal readiness
                // only after the complete configuration has been copied into the container.
                var config = $"""
                    process.roles=controller
                    node.id={nodeId}
                    controller.quorum.bootstrap.servers=controller-1:9093
                    controller.listener.names=CONTROLLER,EXTERNAL
                    listeners=CONTROLLER://0.0.0.0:9093,EXTERNAL://0.0.0.0:{ExternalPort}
                    advertised.listeners=CONTROLLER://controller-{nodeId}:9093,EXTERNAL://{container.Hostname}:{container.GetMappedPublicPort(ExternalPort)}
                    listener.security.protocol.map=CONTROLLER:PLAINTEXT,EXTERNAL:PLAINTEXT
                    log.dirs=/tmp/quorum
                    """;
                await container.CopyAsync(Encoding.UTF8.GetBytes(config), "/tmp/controller.properties", ct: cancellationToken);
                await container.CopyAsync(Array.Empty<byte>(), "/tmp/controller.ready", ct: cancellationToken);
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(9093)).Build();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_observer is not null)
                await _observer.DisposeAsync();
        }
        finally
        {
            try
            {
                if (_leader is not null)
                    await _leader.DisposeAsync();
            }
            finally { await _network.DisposeAsync(); }
        }
    }
}

[Category("Admin")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class BrokerUnregistrationIntegrationTests(RackAwareKafkaContainer kafka)
{
    [Test]
    public async Task UnregisterStoppedBroker_RemovesRegistrationAndAllowsRestart(CancellationToken cancellationToken)
    {
        await using var admin = kafka.CreateAdminClient();
        await kafka.StopBrokerAsync(3, cancellationToken);
        try
        {
            await TestWait.WaitForConditionAsync(
                async () => await admin.DescribeClusterAsync(new DescribeClusterOptions(), cancellationToken),
                cluster => cluster.Nodes.All(broker => broker.NodeId != 3),
                description: "stopped broker is fenced");
            await admin.UnregisterBrokerAsync(3, cancellationToken);
            var exception = await Assert.That(async () => await admin.UnregisterBrokerAsync(3, cancellationToken))
                .Throws<KafkaException>();
            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.BrokerIdNotRegistered);
        }
        finally
        {
            await kafka.StartBrokerAsync(3, cancellationToken);
        }
        await TestWait.WaitForConditionAsync(
            async () => await admin.DescribeClusterAsync(new DescribeClusterOptions(), cancellationToken),
            cluster => cluster.Nodes.Any(broker => broker.NodeId == 3), description: "restarted broker registers again");
    }
}
