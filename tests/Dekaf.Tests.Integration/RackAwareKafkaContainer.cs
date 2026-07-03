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
    private const string Image = "apache/kafka:4.2.0";
    private const int InternalBrokerPort = 9092;
    private const int ControllerPort = 9093;
    private const int ExternalBrokerPort = 19092;

    private readonly string _resourceSuffix = Guid.NewGuid().ToString("N")[..12];
    private readonly int[] _hostPorts = GetFreeTcpPorts(3);
    private readonly IContainer[] _brokers = new IContainer[3];
    private INetwork? _network;

    public string BootstrapServers => string.Join(",", _hostPorts.Select(port => $"127.0.0.1:{port}"));

    public async Task InitializeAsync()
    {
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
        foreach (var broker in _brokers.Reverse())
        {
            if (broker is not null)
                await broker.DisposeAsync().ConfigureAwait(false);
        }

        if (_network is not null)
            await _network.DisposeAsync().ConfigureAwait(false);
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

    private async Task WaitForClusterAsync()
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 90; attempt++)
        {
            try
            {
                await using var admin = CreateAdminClient();
                var cluster = await admin.DescribeClusterAsync().ConfigureAwait(false);
                if (cluster.Nodes.Count == 3)
                    return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Rack-aware Kafka cluster was not ready after 90s. Last error: {lastError?.Message}");
    }

    private static async Task WaitForTopicAssignmentAsync(IAdminClient admin, string topic)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var descriptions = await admin.DescribeTopicsAsync([topic]).ConfigureAwait(false);
            var partition = descriptions[topic].Partitions.Single();

            if (partition.LeaderId == 1
                && partition.ReplicaNodes.SequenceEqual([1, 2])
                && partition.IsrNodes.Contains(2))
            {
                return;
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Topic '{topic}' did not get expected rack-aware assignment.");
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
