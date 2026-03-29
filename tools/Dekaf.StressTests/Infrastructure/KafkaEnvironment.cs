using Dekaf.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Kafka;

namespace Dekaf.StressTests.Infrastructure;

/// <summary>
/// Manages Kafka environment for stress tests.
/// Supports external Kafka (via KAFKA_BOOTSTRAP_SERVERS env var), single-broker Testcontainers,
/// and multi-broker KRaft clusters.
/// </summary>
internal sealed class KafkaEnvironment : IAsyncDisposable
{
    // Retention tuned to balance two constraints:
    // 1. Original 5s retention was too aggressive — caused constant "offset out of range" resets
    // 2. Long time-based retention (e.g. 30 min) would retain ~360 GB at high throughput,
    //    exhausting disk/memory on CI runners
    // Solution: byte-based retention (128 MB/partition × 6 partitions ≈ 768 MB max disk) is
    // the primary bound. Time-based retention (5 min) is a secondary backstop. Small 16 MB
    // segments let Kafka reclaim space incrementally as the consumer keeps up.
    private static readonly Dictionary<string, string> RetentionConfig = new()
    {
        ["KAFKA_LOG_RETENTION_MS"] = "300000",
        ["KAFKA_LOG_RETENTION_BYTES"] = "134217728",
        ["KAFKA_LOG_SEGMENT_BYTES"] = "16777216",
        ["KAFKA_LOG_SEGMENT_DELETE_DELAY_MS"] = "1000",
        ["KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS"] = "10000",
        ["KAFKA_LOG_CLEANUP_POLICY"] = "delete",
    };

    public string BootstrapServers { get; }
    private readonly KafkaContainer? _container;
    private readonly List<IContainer>? _clusterContainers;
    private readonly INetwork? _network;

    private KafkaEnvironment(string bootstrapServers, KafkaContainer? container)
    {
        BootstrapServers = bootstrapServers;
        _container = container;
    }

    private KafkaEnvironment(string bootstrapServers, List<IContainer> clusterContainers, INetwork network)
    {
        BootstrapServers = bootstrapServers;
        _clusterContainers = clusterContainers;
        _network = network;
    }

    public static async Task<KafkaEnvironment> CreateAsync(int brokerCount = 1)
    {
        var externalBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        if (!string.IsNullOrEmpty(externalBootstrap))
        {
            Console.WriteLine($"Using external Kafka at {externalBootstrap}");
            return new KafkaEnvironment(externalBootstrap, null);
        }

        if (brokerCount > 1)
        {
            return await CreateMultiBrokerAsync(brokerCount).ConfigureAwait(false);
        }

        return await CreateSingleBrokerAsync().ConfigureAwait(false);
    }

    private static async Task<KafkaEnvironment> CreateSingleBrokerAsync()
    {
        Console.WriteLine("Starting Kafka container via Testcontainers...");
        var builder = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .WithPortBinding(9092, true);

        foreach (var (key, value) in RetentionConfig)
        {
            builder = builder.WithEnvironment(key, value);
        }

        var container = builder.Build();

        await container.StartAsync().ConfigureAwait(false);

        var rawAddress = container.GetBootstrapAddress();
        var bootstrapServers = rawAddress;
        if (Uri.TryCreate(rawAddress, UriKind.Absolute, out var uri))
        {
            bootstrapServers = $"{uri.Host}:{uri.Port}";
        }

        Console.WriteLine($"Kafka started at {bootstrapServers}");
        await WaitForKafkaAsync(bootstrapServers).ConfigureAwait(false);

        return new KafkaEnvironment(bootstrapServers, container);
    }

    private static async Task<KafkaEnvironment> CreateMultiBrokerAsync(int brokerCount)
    {
        Console.WriteLine($"Starting {brokerCount}-broker KRaft cluster via Testcontainers...");

        var network = new NetworkBuilder()
            .WithName($"kafka-stress-{Guid.NewGuid():N}")
            .Build();

        await network.CreateAsync().ConfigureAwait(false);

        // Build controller quorum voters string: 1@kafka-1:9093,2@kafka-2:9093,...
        var quorumVoters = string.Join(",",
            Enumerable.Range(1, brokerCount).Select(id => $"{id}@kafka-{id}:9093"));

        var containers = new List<IContainer>(brokerCount);
        var startTasks = new List<Task>(brokerCount);

        for (var i = 1; i <= brokerCount; i++)
        {
            var nodeId = i;
            var hostname = $"kafka-{nodeId}";
            var externalPort = 29091 + nodeId; // 29092, 29093, 29094

            var containerBuilder = new ContainerBuilder("apache/kafka:4.1.0")
                .WithName($"{hostname}-{Guid.NewGuid():N}")
                .WithHostname(hostname)
                .WithNetwork(network)
                .WithNetworkAliases(hostname)
                .WithPortBinding(externalPort, 29092)
                .WithEnvironment("KAFKA_NODE_ID", nodeId.ToString())
                .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
                .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", quorumVoters)
                // All KRaft nodes must share the same cluster ID to form a quorum
                .WithEnvironment("CLUSTER_ID", "MkU3OEVBNTcwNTJENDM2Qg")
                .WithEnvironment("KAFKA_LISTENERS", $"PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093,EXTERNAL://0.0.0.0:29092")
                .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", $"PLAINTEXT://{hostname}:9092,EXTERNAL://localhost:{externalPort}")
                .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT,EXTERNAL:PLAINTEXT")
                .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT")
                .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
                .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", Math.Min(brokerCount, 3).ToString());

            foreach (var (key, value) in RetentionConfig)
            {
                containerBuilder = containerBuilder.WithEnvironment(key, value);
            }

            var container = containerBuilder.Build();

            containers.Add(container);
            startTasks.Add(container.StartAsync());
        }

        try
        {
            await Task.WhenAll(startTasks).ConfigureAwait(false);
        }
        catch
        {
            // Clean up already-started containers and network on partial startup failure
            foreach (var c in containers)
            {
                try { await c.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            await network.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        // Bootstrap servers = all external ports
        var bootstrapServers = string.Join(",",
            Enumerable.Range(1, brokerCount).Select(id => $"localhost:{29091 + id}"));

        Console.WriteLine($"KRaft cluster started at {bootstrapServers}");
        await WaitForKafkaAsync(bootstrapServers, maxAttempts: 60).ConfigureAwait(false);

        return new KafkaEnvironment(bootstrapServers, containers, network);
    }

    private static async Task WaitForKafkaAsync(string bootstrapServers, int maxAttempts = 30)
    {
        Console.WriteLine("Waiting for Kafka to be ready...");

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await using var producer = await Kafka.CreateProducer<string, string>()
                    .WithBootstrapServers(bootstrapServers)
                    .WithClientId("kafka-ready-check")
                    .WithAcks(Producer.Acks.Leader)
                    .BuildAsync();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await producer.ProduceAsync(new Producer.ProducerMessage<string, string>
                {
                    Topic = "__kafka_ready_check",
                    Key = "check",
                    Value = "check"
                }, cts.Token).ConfigureAwait(false);

                Console.WriteLine("Kafka is ready");
                return;
            }
            catch
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts");
    }

    public async Task CreateTopicAsync(string topic, int partitions, int replicationFactor = 1)
    {
        if (_container is not null)
        {
            await ExecCreateTopicAsync(_container, "kafka-topics", "localhost:9092",
                topic, partitions, replicationFactor).ConfigureAwait(false);
            return;
        }

        if (_clusterContainers is { Count: > 0 })
        {
            await ExecCreateTopicAsync(_clusterContainers[0], "/opt/kafka/bin/kafka-topics.sh", "kafka-1:9092",
                topic, partitions, replicationFactor).ConfigureAwait(false);
            return;
        }

        await AdminCreateTopicAsync(BootstrapServers, topic, partitions, replicationFactor).ConfigureAwait(false);
    }

    private static async Task AdminCreateTopicAsync(
        string bootstrapServers, string topic, int partitions, int replicationFactor)
    {
        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("stress-test-admin")
            .Build();

        try
        {
            await adminClient.CreateTopicsAsync([
                new NewTopic
                {
                    Name = topic,
                    NumPartitions = partitions,
                    ReplicationFactor = (short)replicationFactor
                }
            ]).ConfigureAwait(false);

            Console.WriteLine($"Created topic via Dekaf admin API: {topic} (partitions={partitions}, replication={replicationFactor})");
        }
        catch (Dekaf.Errors.KafkaException ex) when (ex.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAlreadyExists)
        {
            Console.WriteLine($"Topic {topic} already exists");
        }
    }

    private static async Task ExecCreateTopicAsync(
        IContainer container, string executablePath, string bootstrapServer,
        string topic, int partitions, int replicationFactor)
    {
        var result = await container.ExecAsync([
            executablePath,
            "--bootstrap-server", bootstrapServer,
            "--create",
            "--topic", topic,
            "--partitions", partitions.ToString(),
            "--replication-factor", replicationFactor.ToString(),
            "--if-not-exists"
        ]).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            Console.WriteLine($"Warning: Topic creation returned exit code {result.ExitCode}: {result.Stderr}");
            return;
        }

        Console.WriteLine($"Created topic: {topic} (partitions={partitions}, replication={replicationFactor})");
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            Console.WriteLine("Stopping Kafka container...");
            await _container.DisposeAsync().ConfigureAwait(false);
        }

        if (_clusterContainers is not null)
        {
            Console.WriteLine($"Stopping {_clusterContainers.Count}-broker cluster...");
            var disposeTasks = _clusterContainers.Select(async c =>
            {
                try
                {
                    await c.DisposeAsync().ConfigureAwait(false);
                    return (Exception?)null;
                }
                catch (Exception ex) { return ex; }
            });

            var exceptions = (await Task.WhenAll(disposeTasks).ConfigureAwait(false))
                .Where(ex => ex is not null)
                .ToList();

            if (_network is not null)
            {
                try { await _network.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { exceptions.Add(ex); }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException("One or more cluster resources failed to stop", exceptions!);
            }
        }
    }
}
