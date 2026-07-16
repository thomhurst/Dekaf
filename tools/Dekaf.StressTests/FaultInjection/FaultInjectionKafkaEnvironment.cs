using Confluent.Kafka;
using Dekaf.Admin;
using Dekaf.StressTests.Infrastructure;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Toxiproxy;
using Toxiproxy.Net;
using Toxiproxy.Net.Toxics;
using ConfluentAdminClientBuilder = Confluent.Kafka.AdminClientBuilder;
using ConfluentAdminClient = Confluent.Kafka.IAdminClient;
using ConfluentKafkaException = Confluent.Kafka.KafkaException;

namespace Dekaf.StressTests.FaultInjection;

internal sealed class FaultInjectionKafkaEnvironment : IAsyncDisposable
{
    private const ushort InternalBrokerPort = 9092;
    private const ushort ControllerPort = 9093;
    private const ushort ExternalBrokerPort = 29092;
    private const string ToxiproxyImage = "ghcr.io/shopify/toxiproxy:2.12.0";
    private const string ClusterId = "MkU3OEVBNTcwNTJENDM2Qg";

    private readonly INetwork _network;
    private readonly ToxiproxyContainer _toxiproxyContainer;
    private readonly Connection _toxiproxyConnection;
    private readonly Client _toxiproxyClient;
    private readonly IReadOnlyList<Proxy> _proxies;
    private readonly IReadOnlyList<IContainer> _brokers;
    private readonly DockerClient _dockerClient;
    private bool _disposed;

    private FaultInjectionKafkaEnvironment(
        int brokerCount,
        string bootstrapServers,
        INetwork network,
        ToxiproxyContainer toxiproxyContainer,
        Connection toxiproxyConnection,
        Client toxiproxyClient,
        IReadOnlyList<Proxy> proxies,
        IReadOnlyList<IContainer> brokers,
        DockerClient dockerClient)
    {
        BrokerCount = brokerCount;
        BootstrapServers = bootstrapServers;
        _network = network;
        _toxiproxyContainer = toxiproxyContainer;
        _toxiproxyConnection = toxiproxyConnection;
        _toxiproxyClient = toxiproxyClient;
        _proxies = proxies;
        _brokers = brokers;
        _dockerClient = dockerClient;
    }

    internal int BrokerCount { get; }

    internal string BootstrapServers { get; }

    internal static async Task<FaultInjectionKafkaEnvironment> CreateAsync(
        int brokerCount,
        CancellationToken cancellationToken)
    {
        if (brokerCount is not 1 and not 3)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerCount), brokerCount,
                "Fault injection supports one or three brokers.");
        }

        var network = new NetworkBuilder()
            .WithName($"kafka-fault-{Guid.NewGuid():N}")
            .Build();
        await network.CreateAsync(cancellationToken).ConfigureAwait(false);

        ToxiproxyContainer? toxiproxyContainer = null;
        Connection? toxiproxyConnection = null;
        DockerClient? dockerClient = null;
        var brokers = new List<IContainer>(brokerCount);

        try
        {
            toxiproxyContainer = new ToxiproxyBuilder(ToxiproxyImage)
                .WithNetwork(network)
                .Build();
            await toxiproxyContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            toxiproxyConnection = new Connection(
                toxiproxyContainer.Hostname,
                toxiproxyContainer.GetMappedPublicPort(ToxiproxyBuilder.ToxiproxyControlPort));
            var toxiproxyClient = toxiproxyConnection.Client();
            var proxies = new List<Proxy>(brokerCount);
            var bootstrapEndpoints = new List<string>(brokerCount);

            for (var nodeId = 1; nodeId <= brokerCount; nodeId++)
            {
                var proxyPort = checked((ushort)(ToxiproxyBuilder.FirstProxiedPort + nodeId - 1));
                var proxy = await toxiproxyClient.AddAsync(new Proxy
                {
                    Name = $"kafka-{nodeId}",
                    Enabled = true,
                    Listen = $"0.0.0.0:{proxyPort}",
                    Upstream = $"kafka-{nodeId}:{ExternalBrokerPort}"
                }).WaitAsync(cancellationToken).ConfigureAwait(false);

                proxies.Add(proxy);
                bootstrapEndpoints.Add(
                    $"{toxiproxyContainer.Hostname}:{toxiproxyContainer.GetMappedPublicPort(proxyPort)}");
            }

            var advertisedHost = toxiproxyContainer.Hostname;
            var quorumVoters = string.Join(",",
                Enumerable.Range(1, brokerCount).Select(nodeId => $"{nodeId}@kafka-{nodeId}:{ControllerPort}"));
            var replicationFactor = Math.Min(3, brokerCount);
            var minInSyncReplicas = brokerCount == 1 ? 1 : 2;

            for (var nodeId = 1; nodeId <= brokerCount; nodeId++)
            {
                var hostname = $"kafka-{nodeId}";
                var proxyPort = checked((ushort)(ToxiproxyBuilder.FirstProxiedPort + nodeId - 1));
                var mappedProxyPort = toxiproxyContainer.GetMappedPublicPort(proxyPort);

                var broker = new ContainerBuilder(KafkaEnvironment.KafkaImage)
                    .WithName($"{hostname}-fault-{Guid.NewGuid():N}")
                    .WithHostname(hostname)
                    .WithNetwork(network)
                    .WithNetworkAliases(hostname)
                    .WithEnvironment("KAFKA_NODE_ID", nodeId.ToString())
                    .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
                    .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", quorumVoters)
                    .WithEnvironment("CLUSTER_ID", ClusterId)
                    .WithEnvironment("KAFKA_LISTENERS",
                        $"INTERNAL://0.0.0.0:{InternalBrokerPort},CONTROLLER://0.0.0.0:{ControllerPort},EXTERNAL://0.0.0.0:{ExternalBrokerPort}")
                    .WithEnvironment("KAFKA_ADVERTISED_LISTENERS",
                        $"INTERNAL://{hostname}:{InternalBrokerPort},EXTERNAL://{advertisedHost}:{mappedProxyPort}")
                    .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP",
                        "INTERNAL:PLAINTEXT,CONTROLLER:PLAINTEXT,EXTERNAL:PLAINTEXT")
                    .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "INTERNAL")
                    .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
                    .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", replicationFactor.ToString())
                    .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", replicationFactor.ToString())
                    .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", minInSyncReplicas.ToString())
                    .WithEnvironment("KAFKA_DEFAULT_REPLICATION_FACTOR", replicationFactor.ToString())
                    .WithEnvironment("KAFKA_MIN_INSYNC_REPLICAS", minInSyncReplicas.ToString())
                    .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
                    .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false")
                    .WithWaitStrategy(KafkaEnvironment.BrokerServingWaitStrategy())
                    .Build();

                brokers.Add(broker);
            }

            await Task.WhenAll(brokers.Select(broker => broker.StartAsync(cancellationToken)))
                .ConfigureAwait(false);

            var bootstrapServers = string.Join(",", bootstrapEndpoints);
            await WaitForClusterAsync(bootstrapServers, brokerCount, TimeSpan.FromMinutes(2), cancellationToken)
                .ConfigureAwait(false);

            dockerClient = new DockerClientBuilder().Build();
            Console.WriteLine($"Fault-injection Kafka cluster ready: {bootstrapServers}");
            return new FaultInjectionKafkaEnvironment(
                brokerCount,
                bootstrapServers,
                network,
                toxiproxyContainer,
                toxiproxyConnection,
                toxiproxyClient,
                proxies,
                brokers,
                dockerClient);
        }
        catch
        {
            dockerClient?.Dispose();
            toxiproxyConnection?.Dispose();
            foreach (var broker in brokers)
            {
                try { await broker.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            if (toxiproxyContainer is not null)
            {
                try { await toxiproxyContainer.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            await network.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task CreateTopicAsync(string topic, int partitionCount, CancellationToken cancellationToken)
    {
        var replicationFactor = (short)Math.Min(3, BrokerCount);
        await using var admin = Kafka.CreateAdminClient()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(BootstrapServers)
            .WithClientId("fault-injection-admin")
            .Build();

        await admin.CreateTopicsAsync(
        [
            new NewTopic
            {
                Name = topic,
                NumPartitions = partitionCount,
                ReplicationFactor = replicationFactor,
                Configs = new Dictionary<string, string>
                {
                    ["min.insync.replicas"] = BrokerCount == 1 ? "1" : "2",
                    ["retention.ms"] = "-1",
                    ["retention.bytes"] = "-1"
                }
            }
        ], cancellationToken: cancellationToken).ConfigureAwait(false);

        await WaitForTopicHealthyAsync(topic, cancellationToken).ConfigureAwait(false);
    }

    internal async Task ExecuteNetworkFaultAsync(
        FaultWindowKind kind,
        TimeSpan duration,
        Action faultActivated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(faultActivated);
        try
        {
            switch (kind)
            {
                case FaultWindowKind.ConnectionReset:
                    await AddToEveryProxyAsync(static () => new ResetPeerToxic
                    {
                        Name = "reset-upstream",
                        Stream = ToxicDirection.UpStream,
                        Toxicity = 1,
                        Attributes = { Timeout = 0 }
                    }, cancellationToken).ConfigureAwait(false);
                    await AddToEveryProxyAsync(static () => new ResetPeerToxic
                    {
                        Name = "reset-downstream",
                        Stream = ToxicDirection.DownStream,
                        Toxicity = 1,
                        Attributes = { Timeout = 0 }
                    }, cancellationToken).ConfigureAwait(false);
                    faultActivated();
                    await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
                    break;

                case FaultWindowKind.HalfOpen:
                    await AddToEveryProxyAsync(static () => new TimeoutToxic
                    {
                        Name = "half-open-upstream",
                        Stream = ToxicDirection.UpStream,
                        Toxicity = 1,
                        Attributes = { Timeout = 0 }
                    }, cancellationToken).ConfigureAwait(false);
                    faultActivated();
                    await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
                    break;

                case FaultWindowKind.SlowClose:
                    await AddToEveryProxyAsync(() => new SlowCloseToxic
                    {
                        Name = "slow-close-downstream",
                        Stream = ToxicDirection.DownStream,
                        Toxicity = 1,
                        Attributes = { Delay = checked((int)duration.TotalMilliseconds) }
                    }, cancellationToken).ConfigureAwait(false);
                    await SetAllProxiesEnabledAsync(false, cancellationToken).ConfigureAwait(false);
                    faultActivated();
                    await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
                    break;

                case FaultWindowKind.LatencyAndBandwidth:
                    foreach (var direction in new[] { ToxicDirection.UpStream, ToxicDirection.DownStream })
                    {
                        var suffix = direction == ToxicDirection.UpStream ? "upstream" : "downstream";
                        await AddToEveryProxyAsync(() => new LatencyToxic
                        {
                            Name = $"latency-{suffix}",
                            Stream = direction,
                            Toxicity = 1,
                            Attributes = { Latency = 300, Jitter = 100 }
                        }, cancellationToken).ConfigureAwait(false);
                        await AddToEveryProxyAsync(() => new BandwidthToxic
                        {
                            Name = $"bandwidth-{suffix}",
                            Stream = direction,
                            Toxicity = 1,
                            Attributes = { Rate = 128 }
                        }, cancellationToken).ConfigureAwait(false);
                    }

                    faultActivated();
                    await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a network fault.");
            }
        }
        finally
        {
            await ResetProxiesAsync().ConfigureAwait(false);
        }
    }

    internal async Task KillAndRestartBrokerAsync(
        int nodeId,
        string topic,
        TimeSpan downtime,
        Action faultActivated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(faultActivated);
        var broker = GetBroker(nodeId);
        Console.WriteLine($"  SIGKILL broker {nodeId}");
        await _dockerClient.Containers.KillContainerAsync(
            broker.Id,
            new ContainerKillParameters { Signal = "SIGKILL" },
            cancellationToken).ConfigureAwait(false);
        faultActivated();
        try
        {
            await Task.Delay(downtime, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Console.WriteLine($"  Restart broker {nodeId}");
            await RestoreBrokerAsync(broker, topic).ConfigureAwait(false);
        }
    }

    internal async Task ForceLeaderElectionAsync(
        string topic,
        TimeSpan downtime,
        Action faultActivated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(faultActivated);
        if (BrokerCount < 3)
        {
            throw new InvalidOperationException("Leader-election fault requires three brokers.");
        }

        var previousLeader = GetLeaderNodeId(topic, partition: 0);
        var broker = GetBroker(previousLeader);
        Console.WriteLine($"  Controlled stop of partition-0 leader broker {previousLeader}");
        await broker.StopAsync(cancellationToken).ConfigureAwait(false);
        faultActivated();
        try
        {
            await WaitForLeaderChangeAsync(topic, previousLeader, cancellationToken).ConfigureAwait(false);
            await Task.Delay(downtime, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Console.WriteLine($"  Restart former leader broker {previousLeader}");
            await RestoreBrokerAsync(broker, topic).ConfigureAwait(false);
        }
    }

    internal async Task RollingRestartAsync(
        string topic,
        TimeSpan downtime,
        Action faultActivated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(faultActivated);
        if (BrokerCount < 3)
        {
            throw new InvalidOperationException("Rolling restart requires three brokers.");
        }

        var activated = false;
        for (var nodeId = 1; nodeId <= BrokerCount; nodeId++)
        {
            var broker = GetBroker(nodeId);
            Console.WriteLine($"  Rolling stop broker {nodeId}");
            await broker.StopAsync(cancellationToken).ConfigureAwait(false);
            if (!activated)
            {
                activated = true;
                faultActivated();
            }

            try
            {
                await Task.Delay(downtime, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Console.WriteLine($"  Rolling start broker {nodeId}");
                await RestoreBrokerAsync(broker, topic).ConfigureAwait(false);
            }
        }
    }

    internal async Task WaitForTopicHealthyAsync(string topic, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        Exception? lastError = null;
        using var admin = CreateMetadataClient(BootstrapServers);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var metadata = admin.GetMetadata(topic, TimeSpan.FromSeconds(5));
                var topicMetadata = metadata.Topics.SingleOrDefault(candidate => candidate.Topic == topic);
                if (topicMetadata is not null
                    && !topicMetadata.Error.IsError
                    && topicMetadata.Partitions.Count > 0
                    && topicMetadata.Partitions.All(partition =>
                        !partition.Error.IsError
                        && partition.Leader > 0
                        && partition.InSyncReplicas.Length == Math.Min(3, BrokerCount)))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Topic {topic} did not become fully in-sync.", lastError);
    }

    private async Task AddToEveryProxyAsync<T>(Func<T> toxicFactory, CancellationToken cancellationToken)
        where T : ToxicBase
    {
        foreach (var proxy in _proxies)
        {
            await proxy.AddAsync(toxicFactory()).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SetAllProxiesEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        foreach (var proxy in _proxies)
        {
            proxy.Enabled = enabled;
            await proxy.UpdateAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ResetProxiesAsync()
    {
        await _toxiproxyClient.ResetAsync().ConfigureAwait(false);
        foreach (var proxy in _proxies)
        {
            proxy.Enabled = true;
        }
    }

    private async Task RestoreBrokerAsync(IContainer broker, string topic)
    {
        await broker.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await WaitForTopicHealthyAsync(topic, CancellationToken.None).ConfigureAwait(false);
    }

    private IContainer GetBroker(int nodeId)
    {
        if (nodeId < 1 || nodeId > _brokers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "Unknown broker node ID.");
        }

        return _brokers[nodeId - 1];
    }

    private int GetLeaderNodeId(string topic, int partition)
    {
        using var admin = CreateMetadataClient(BootstrapServers);
        return GetLeaderNodeId(admin, topic, partition);
    }

    private static int GetLeaderNodeId(ConfluentAdminClient admin, string topic, int partition)
    {
        var metadata = admin.GetMetadata(topic, TimeSpan.FromSeconds(10));
        var topicMetadata = metadata.Topics.Single(candidate => candidate.Topic == topic);
        return topicMetadata.Partitions.Single(candidate => candidate.PartitionId == partition).Leader;
    }

    private async Task WaitForLeaderChangeAsync(
        string topic,
        int previousLeader,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using var admin = CreateMetadataClient(BootstrapServers);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var leader = GetLeaderNodeId(admin, topic, partition: 0);
                if (leader > 0 && leader != previousLeader)
                {
                    Console.WriteLine($"  Partition-0 leader moved {previousLeader} -> {leader}");
                    return;
                }
            }
            catch (ConfluentKafkaException)
            {
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Partition-0 leader did not move away from broker {previousLeader}.");
    }

    private static async Task WaitForClusterAsync(
        string bootstrapServers,
        int expectedBrokerCount,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;
        using var admin = CreateMetadataClient(bootstrapServers);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var metadata = admin.GetMetadata(TimeSpan.FromSeconds(5));
                if (metadata.Brokers.Count >= expectedBrokerCount)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Kafka cluster did not expose {expectedBrokerCount} brokers through Toxiproxy.",
            lastError);
    }

    private static ConfluentAdminClient CreateMetadataClient(string bootstrapServers) =>
        new ConfluentAdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers,
            SocketTimeoutMs = 5_000
        }).Build();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var errors = new List<Exception>();
        try { await ResetProxiesAsync().ConfigureAwait(false); }
        catch (Exception ex) { errors.Add(ex); }
        foreach (var broker in _brokers)
        {
            try { await broker.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { errors.Add(ex); }
        }

        try { _dockerClient.Dispose(); }
        catch (Exception ex) { errors.Add(ex); }
        try { _toxiproxyConnection.Dispose(); }
        catch (Exception ex) { errors.Add(ex); }
        try { await _toxiproxyContainer.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { errors.Add(ex); }
        try { await _network.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { errors.Add(ex); }

        if (errors.Count > 0)
        {
            throw new AggregateException("Fault-injection environment cleanup failed.", errors);
        }
    }
}
