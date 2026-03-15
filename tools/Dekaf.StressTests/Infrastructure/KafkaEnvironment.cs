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
        var container = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .WithPortBinding(9092, true)
            // Aggressive retention limits to prevent disk filling during stress tests
            // Limits: 64MB per partition, 5-second retention, 1-second cleanup checks
            .WithEnvironment("KAFKA_LOG_RETENTION_MS", "5000")                    // 5 seconds
            .WithEnvironment("KAFKA_LOG_RETENTION_BYTES", "67108864")             // 64MB
            .WithEnvironment("KAFKA_LOG_SEGMENT_BYTES", "16777216")               // 16MB segments
            .WithEnvironment("KAFKA_LOG_SEGMENT_DELETE_DELAY_MS", "100")          // 100ms delete delay
            .WithEnvironment("KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS", "1000")     // 1 second check interval
            .WithEnvironment("KAFKA_LOG_CLEANUP_POLICY", "delete")
            .Build();

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

            var container = new ContainerBuilder("apache/kafka:latest")
                .WithName($"{hostname}-{Guid.NewGuid():N}")
                .WithHostname(hostname)
                .WithNetwork(network)
                .WithNetworkAliases(hostname)
                .WithPortBinding(externalPort, 29092)
                .WithEnvironment("KAFKA_NODE_ID", nodeId.ToString())
                .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
                .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", quorumVoters)
                .WithEnvironment("KAFKA_LISTENERS", $"PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093,EXTERNAL://0.0.0.0:29092")
                .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", $"PLAINTEXT://{hostname}:9092,EXTERNAL://localhost:{externalPort}")
                .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT,EXTERNAL:PLAINTEXT")
                .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT")
                .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
                .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", Math.Min(brokerCount, 3).ToString())
                // Aggressive retention limits to prevent disk filling during stress tests
                .WithEnvironment("KAFKA_LOG_RETENTION_MS", "5000")
                .WithEnvironment("KAFKA_LOG_RETENTION_BYTES", "67108864")
                .WithEnvironment("KAFKA_LOG_SEGMENT_BYTES", "16777216")
                .WithEnvironment("KAFKA_LOG_SEGMENT_DELETE_DELAY_MS", "100")
                .WithEnvironment("KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS", "1000")
                .WithEnvironment("KAFKA_LOG_CLEANUP_POLICY", "delete")
                .Build();

            containers.Add(container);
            startTasks.Add(container.StartAsync());
        }

        await Task.WhenAll(startTasks).ConfigureAwait(false);

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
            // Single-broker: cp-kafka uses kafka-topics on PATH
            var result = await _container.ExecAsync([
                "kafka-topics",
                "--bootstrap-server", "localhost:9092",
                "--create",
                "--topic", topic,
                "--partitions", partitions.ToString(),
                "--replication-factor", replicationFactor.ToString(),
                "--if-not-exists"
            ]).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                Console.WriteLine($"Created topic: {topic} (partitions={partitions}, replication={replicationFactor})");
            }
            else
            {
                Console.WriteLine($"Warning: Topic creation returned exit code {result.ExitCode}: {result.Stderr}");
            }

            await Task.Delay(1000).ConfigureAwait(false);
            return;
        }

        if (_clusterContainers is { Count: > 0 })
        {
            // Multi-broker: apache/kafka uses /opt/kafka/bin/kafka-topics.sh
            // Use internal bootstrap address via the first broker
            var firstContainer = _clusterContainers[0];
            var result = await firstContainer.ExecAsync([
                "/opt/kafka/bin/kafka-topics.sh",
                "--bootstrap-server", "kafka-1:9092",
                "--create",
                "--topic", topic,
                "--partitions", partitions.ToString(),
                "--replication-factor", replicationFactor.ToString(),
                "--if-not-exists"
            ]).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                Console.WriteLine($"Created topic: {topic} (partitions={partitions}, replication={replicationFactor})");
            }
            else
            {
                Console.WriteLine($"Warning: Topic creation returned exit code {result.ExitCode}: {result.Stderr}");
            }

            await Task.Delay(1000).ConfigureAwait(false);
            return;
        }

        Console.WriteLine($"Using external Kafka - assuming topic {topic} exists or will be auto-created");
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
            foreach (var container in _clusterContainers)
            {
                await container.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (_network is not null)
        {
            await _network.DisposeAsync().ConfigureAwait(false);
        }
    }
}
