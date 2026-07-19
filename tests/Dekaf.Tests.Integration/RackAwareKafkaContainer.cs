using System.Net;
using System.Net.Sockets;
using Dekaf.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

public sealed class RackAwareKafkaContainer : IAsyncInitializer, IAsyncDisposable
{
    private static readonly string Image = $"apache/kafka:{KafkaContainerDefault.ImageTag}";
    private const int InternalBrokerPort = 9092;
    private const int ControllerPort = 9093;
    private const int ExternalBrokerPort = 19092;

    private string _resourceSuffix = string.Empty;
    private int[] _hostPorts = [];
    private IContainer[] _brokers = [];
    private INetwork? _network;

    public string BootstrapServers => string.Join(",", _hostPorts.Select(port => $"127.0.0.1:{port}"));

    public async Task InitializeAsync()
    {
        await ContainerStartupRetry.RunAsync(
            StartClusterAttemptAsync,
            DisposeClusterAttemptAsync,
            ContainerStartupRetry.IsKnownTransient).ConfigureAwait(false);
    }

    private async Task StartClusterAttemptAsync()
    {
        _resourceSuffix = Guid.NewGuid().ToString("N")[..12];
        _hostPorts = GetFreeTcpPorts(3);
        _brokers = new IContainer[3];
        _network = new NetworkBuilder()
            .WithName($"dekaf-rack-{_resourceSuffix}")
            .Build();

        await _network.CreateAsync().ConfigureAwait(false);

        _brokers[0] = CreateBroker(nodeId: 1, rack: "rack-b", externalPort: _hostPorts[0]);
        _brokers[1] = CreateBroker(nodeId: 2, rack: "rack-a", externalPort: _hostPorts[1]);
        _brokers[2] = CreateBroker(nodeId: 3, rack: "rack-a", externalPort: _hostPorts[2]);

        await Task.WhenAll(_brokers.Select(broker => broker.StartAsync())).ConfigureAwait(false);
        await WaitForClusterAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeClusterAttemptAsync().ConfigureAwait(false);
    }

    private async ValueTask DisposeClusterAttemptAsync()
    {
        var brokers = _brokers;
        var network = _network;
        _brokers = [];
        _network = null;
        _hostPorts = [];

        Exception? firstError = null;
        foreach (var broker in brokers.AsEnumerable().Reverse())
        {
            try
            {
                if (broker is not null)
                    await broker.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                firstError ??= exception;
            }
        }

        try
        {
            if (network is not null)
                await network.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstError ??= exception;
        }

        if (firstError is not null)
            throw firstError;
    }

    public IAdminClient CreateAdminClient()
    {
        return Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
    }

    public async Task<string> CreateTopicWithRemoteLeaderAndLocalFollowerAsync()
    {
        var topic = $"rack-aware-{Guid.NewGuid():N}";
        await using var admin = CreateAdminClient();

        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topic,
                NumPartitions = -1,
                ReplicationFactor = -1,
                ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>>
                {
                    [0] = [1, 2]
                },
                Configs = new Dictionary<string, string>
                {
                    ["min.insync.replicas"] = "1"
                }
            }
        ]).ConfigureAwait(false);

        await WaitForTopicAssignmentAsync(admin, topic).ConfigureAwait(false);
        return topic;
    }

    public async Task<string> CreateReplicatedTopicAsync()
    {
        var topic = $"replicated-{Guid.NewGuid():N}";
        await using var admin = CreateAdminClient();

        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topic,
                NumPartitions = -1,
                ReplicationFactor = -1,
                ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>>
                {
                    [0] = [1, 2, 3]
                },
                Configs = new Dictionary<string, string>
                {
                    ["min.insync.replicas"] = "2"
                }
            }
        ]).ConfigureAwait(false);

        await WaitForTopicAssignmentAsync(admin, topic, [1, 2, 3]).ConfigureAwait(false);
        return topic;
    }

    public async Task<string> CreateUncleanElectionTopicAsync()
    {
        var topic = $"unclean-election-{Guid.NewGuid():N}";
        await using var admin = CreateAdminClient();

        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topic,
                NumPartitions = -1,
                ReplicationFactor = -1,
                ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>>
                {
                    [0] = [1, 2]
                },
                Configs = new Dictionary<string, string>
                {
                    ["min.insync.replicas"] = "1",
                    ["unclean.leader.election.enable"] = "true"
                }
            }
        ]).ConfigureAwait(false);

        await WaitForTopicAssignmentAsync(admin, topic).ConfigureAwait(false);
        return topic;
    }

    public async Task<int> GetPartitionLeaderIdAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        await using var admin = CreateAdminClient();
        var descriptions = await admin.DescribeTopicsAsync([topic], cancellationToken).ConfigureAwait(false);
        return descriptions[topic].Partitions.Single().LeaderId;
    }

    public Task StopBrokerAsync(int nodeId, CancellationToken cancellationToken = default) =>
        GetBroker(nodeId).StopAsync(cancellationToken);

    public async Task StartBrokerAsync(int nodeId, CancellationToken cancellationToken = default)
    {
        await StartBrokerWithoutClusterWaitAsync(nodeId, cancellationToken).ConfigureAwait(false);

        await WaitForClusterAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StartBrokerWithoutClusterWaitAsync(
        int nodeId,
        CancellationToken cancellationToken = default)
    {
        var broker = GetBroker(nodeId);
        if (broker.State != TestcontainersStates.Running)
            await broker.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StartBrokersAsync(
        IReadOnlyCollection<int> nodeIds,
        CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(nodeIds.Select(nodeId =>
            StartBrokerWithoutClusterWaitAsync(nodeId, cancellationToken))).ConfigureAwait(false);
        await WaitForClusterAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> WaitForPartitionLeaderChangeAsync(
        string topic,
        int previousLeaderId,
        CancellationToken cancellationToken = default)
    {
        return await PollUntilAsync(
            token => GetPartitionLeaderIdAsync(topic, token),
            leaderId => leaderId >= 0 && leaderId != previousLeaderId,
            maxAttempts: 90,
            delay: TimeSpan.FromMilliseconds(500),
            timeoutMessage: $"Topic '{topic}' did not elect a new leader after broker {previousLeaderId} stopped.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task WaitForInSyncReplicasAsync(
        string topic,
        int expectedCount,
        CancellationToken cancellationToken = default)
    {
        _ = await PollUntilAsync(
            async token =>
            {
                await using var admin = CreateAdminClient();
                var descriptions = await admin.DescribeTopicsAsync([topic], token).ConfigureAwait(false);
                return descriptions[topic].Partitions.Single().IsrNodes.Count;
            },
            isComplete: count => count == expectedCount,
            maxAttempts: 120,
            delay: TimeSpan.FromMilliseconds(500),
            timeoutMessage: $"Topic '{topic}' did not restore {expectedCount} in-sync replicas.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private IContainer CreateBroker(int nodeId, string rack, int externalPort)
    {
        var brokerAlias = BrokerAlias(nodeId);

        return new ContainerBuilder(Image)
            .WithName($"dekaf-rack-{_resourceSuffix}-{brokerAlias}")
            .WithHostname(brokerAlias)
            .WithNetwork(_network!)
            .WithNetworkAliases(brokerAlias)
            .WithPortBinding(externalPort, ExternalBrokerPort)
            .WithEnvironment(CreateBrokerEnvironment(nodeId, rack, externalPort))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(ExternalBrokerPort))
            .Build();
    }

    private static IReadOnlyDictionary<string, string> CreateBrokerEnvironment(int nodeId, string rack, int externalPort)
    {
        return new Dictionary<string, string>
        {
            ["KAFKA_CLUSTER_ID"] = "MkU3OEVBNTcwNTJENDM2Qk",
            ["KAFKA_NODE_ID"] = nodeId.ToString(),
            ["KAFKA_PROCESS_ROLES"] = "broker,controller",
            ["KAFKA_CONTROLLER_QUORUM_VOTERS"] = string.Join(",", Enumerable.Range(1, 3)
                .Select(id => $"{id}@{BrokerAlias(id)}:{ControllerPort}")),
            ["KAFKA_LISTENERS"] =
                $"PLAINTEXT://0.0.0.0:{InternalBrokerPort},CONTROLLER://0.0.0.0:{ControllerPort},EXTERNAL://0.0.0.0:{ExternalBrokerPort}",
            ["KAFKA_ADVERTISED_LISTENERS"] =
                $"PLAINTEXT://{BrokerAlias(nodeId)}:{InternalBrokerPort},EXTERNAL://127.0.0.1:{externalPort}",
            ["KAFKA_LISTENER_SECURITY_PROTOCOL_MAP"] = "PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT,EXTERNAL:PLAINTEXT",
            ["KAFKA_INTER_BROKER_LISTENER_NAME"] = "PLAINTEXT",
            ["KAFKA_CONTROLLER_LISTENER_NAMES"] = "CONTROLLER",
            ["KAFKA_BROKER_RACK"] = rack,
            ["KAFKA_REPLICA_SELECTOR_CLASS"] = "org.apache.kafka.common.replica.RackAwareReplicaSelector",
            ["KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR"] = "3",
            ["KAFKA_OFFSETS_TOPIC_NUM_PARTITIONS"] = "3",
            ["KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR"] = "3",
            ["KAFKA_TRANSACTION_STATE_LOG_MIN_ISR"] = "2",
            ["KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS"] = "0",
            ["KAFKA_AUTO_CREATE_TOPICS_ENABLE"] = "false",
            ["KAFKA_LOG_RETENTION_MS"] = "30000",
            ["KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS"] = "10000",
            ["KAFKA_LOG_SEGMENT_BYTES"] = "1048576",
            ["KAFKA_LOG_CLEANUP_POLICY"] = "delete",
            ["KAFKA_HEAP_OPTS"] = "-Xmx384m -Xms384m"
        };
    }

    private async Task WaitForClusterAsync(CancellationToken cancellationToken = default)
    {
        _ = await PollUntilAsync(
            async token =>
            {
                await using var admin = CreateAdminClient();
                var cluster = await admin.DescribeClusterAsync(token).ConfigureAwait(false);
                return cluster.Nodes.Count;
            },
            isComplete: nodeCount => nodeCount == 3,
            maxAttempts: 90,
            delay: TimeSpan.FromSeconds(1),
            timeoutMessage: "Rack-aware Kafka cluster was not ready after 90s.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForTopicAssignmentAsync(
        IAdminClient admin,
        string topic,
        CancellationToken cancellationToken = default)
    {
        await WaitForTopicAssignmentAsync(admin, topic, [1, 2], cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForTopicAssignmentAsync(
        IAdminClient admin,
        string topic,
        IReadOnlyList<int> expectedReplicas,
        CancellationToken cancellationToken = default)
    {
        _ = await PollUntilAsync(
            async token =>
            {
                var descriptions = await admin.DescribeTopicsAsync([topic], token).ConfigureAwait(false);
                var partition = descriptions[topic].Partitions.Single();
                return partition.LeaderId == 1
                    && partition.ReplicaNodes.SequenceEqual(expectedReplicas)
                    && expectedReplicas.All(partition.IsrNodes.Contains);
            },
            isComplete: static assigned => assigned,
            maxAttempts: 60,
            delay: TimeSpan.FromMilliseconds(500),
            timeoutMessage: $"Topic '{topic}' did not get expected rack-aware assignment.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> PollUntilAsync<T>(
        Func<CancellationToken, Task<T>> probeAsync,
        Func<T, bool> isComplete,
        int maxAttempts,
        TimeSpan delay,
        string timeoutMessage,
        CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await probeAsync(cancellationToken).ConfigureAwait(false);
                if (isComplete(result))
                    return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        if (lastError is null)
            throw new InvalidOperationException(timeoutMessage);

        throw new InvalidOperationException($"{timeoutMessage} Last error: {lastError.Message}", lastError);
    }

    private IContainer GetBroker(int nodeId)
    {
        if (nodeId < 1 || nodeId > _brokers.Length)
            throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "Broker node ID must be between 1 and 3.");

        return _brokers[nodeId - 1]
            ?? throw new InvalidOperationException($"Broker {nodeId} has not been initialized.");
    }

    private static int[] GetFreeTcpPorts(int count)
    {
        var listeners = new TcpListener[count];
        try
        {
            for (var i = 0; i < count; i++)
            {
                listeners[i] = new TcpListener(IPAddress.Loopback, 0);
                listeners[i].Start();
            }

            return listeners
                .Select(listener => ((IPEndPoint)listener.LocalEndpoint).Port)
                .ToArray();
        }
        finally
        {
            foreach (var listener in listeners)
                listener?.Stop();
        }
    }

    private static string BrokerAlias(int nodeId) => $"broker-{nodeId}";
}
