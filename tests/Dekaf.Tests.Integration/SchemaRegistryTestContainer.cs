using System.Collections.Concurrent;
using System.Net.Sockets;
using Dekaf.Admin;
using Dekaf.Errors;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Combined Kafka and Schema Registry container for integration tests.
/// This creates both containers on the same Docker network so Schema Registry can connect to Kafka.
/// </summary>
public class KafkaWithSchemaRegistryContainer : IAsyncInitializer, IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, ContainerImageBootstrapCoordinator> ImageBootstrapCoordinators = new();

    private KafkaContainer? _kafkaContainer;
    private IContainer? _schemaRegistryContainer;
    private INetwork? _network;
    private bool _externalKafka;
    private bool _externalRegistry;
    private string _bootstrapServers = string.Empty;
    private string _registryUrl = string.Empty;
    private readonly ConcurrentDictionary<string, byte> _createdTopics = new();

    /// <summary>
    /// The Kafka bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers => _bootstrapServers;

    /// <summary>
    /// The Schema Registry URL.
    /// </summary>
    public string RegistryUrl => _registryUrl;

    protected virtual string SchemaRegistryImage => "confluentinc/cp-schema-registry:7.9.0";
    protected virtual string KafkaImage => "apache/kafka:4.0.2";

    protected virtual KafkaBuilder ConfigureKafkaBuilder(KafkaBuilder builder) => builder;

    public async Task InitializeAsync()
    {
        // Check for external Kafka (CI environment)
        var externalKafkaBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        var externalRegistryUrl = Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL");

        if (!string.IsNullOrEmpty(externalKafkaBootstrap) && !string.IsNullOrEmpty(externalRegistryUrl))
        {
            _bootstrapServers = externalKafkaBootstrap;
            _registryUrl = externalRegistryUrl;
            _externalKafka = true;
            _externalRegistry = true;
            Console.WriteLine($"[KafkaWithSchemaRegistry] Using external Kafka at {_bootstrapServers}");
            Console.WriteLine($"[KafkaWithSchemaRegistry] Using external Schema Registry at {_registryUrl}");
            await WaitForKafkaAsync().ConfigureAwait(false);
            await WaitForServicesAsync().ConfigureAwait(false);
            return;
        }

        // A complete first startup guarantees Testcontainers has pulled both images.
        var bootstrapCoordinator = ImageBootstrapCoordinators.GetOrAdd(
            SchemaRegistryImage,
            static _ => new ContainerImageBootstrapCoordinator());
        await bootstrapCoordinator.RunAsync(StartLocalContainersAsync).ConfigureAwait(false);
    }

    private async Task StartLocalContainersAsync()
    {
        Console.WriteLine("[KafkaWithSchemaRegistry] Creating Docker network...");

        // Create a shared network for the containers
        _network = new NetworkBuilder()
            .WithName($"kafka-sr-network-{Guid.NewGuid():N}")
            .Build();
        await _network.CreateAsync().ConfigureAwait(false);

        Console.WriteLine("[KafkaWithSchemaRegistry] Starting Kafka container...");

        // Start Kafka with network alias
        _kafkaContainer = ConfigureKafkaBuilder(new KafkaBuilder(KafkaImage)
            .WithNetwork(_network)
            .WithNetworkAliases("kafka")
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m")
            .WithEnvironment("KAFKA_LOG_RETENTION_MS", "30000")
            .WithEnvironment("KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS", "10000")
            .WithEnvironment("KAFKA_LOG_SEGMENT_BYTES", "1048576")
            .WithEnvironment("KAFKA_LOG_CLEANUP_POLICY", "delete"))
            .Build();

        await _kafkaContainer.StartAsync().ConfigureAwait(false);
        _bootstrapServers = _kafkaContainer.GetBootstrapAddress();
        Console.WriteLine($"[KafkaWithSchemaRegistry] Kafka started at {_bootstrapServers}");

        // Schema Registry can fail its initial Noop write if topic creation has completed
        // but the _schemas partition is not serving leader requests yet.
        using var readinessTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await PrepareSchemaStoreAsync(_bootstrapServers, readinessTimeout.Token).ConfigureAwait(false);

        Console.WriteLine("[KafkaWithSchemaRegistry] Starting Schema Registry container...");

        // Start Schema Registry connected to Kafka via network
        _schemaRegistryContainer = new ContainerBuilder(SchemaRegistryImage)
            .WithNetwork(_network)
            .WithNetworkAliases("schema-registry")
            .WithPortBinding(8081, true)
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", "kafka:9093")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8081")
            .WithEnvironment(
                "SCHEMA_REGISTRY_RESOURCE_EXTENSION_CLASS",
                "io.confluent.kafka.schemaregistry.rulehandler.RuleSetResourceExtension")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/subjects").ForPort(8081)))
            .Build();

        await _schemaRegistryContainer.StartAsync().ConfigureAwait(false);

        var port = _schemaRegistryContainer.GetMappedPublicPort(8081);
        _registryUrl = $"http://localhost:{port}";
        Console.WriteLine($"[KafkaWithSchemaRegistry] Schema Registry started at {_registryUrl}");

        await WaitForServicesAsync().ConfigureAwait(false);
    }

    internal static async Task PrepareSchemaStoreAsync(string bootstrapServers, CancellationToken cancellationToken)
    {
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(bootstrapServers)
            .Build();
        await admin.CreateTopicsAsync(
            [new NewTopic
            {
                Name = "_schemas",
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string> { ["cleanup.policy"] = "compact" }
            }], cancellationToken: cancellationToken).ConfigureAwait(false);

        var partition = new TopicPartition("_schemas", 0);
        TopicPartitionOffsetSpec[] offsets = [new() { TopicPartition = partition, Spec = OffsetSpec.Latest }];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Refresh routing before probing the partition's leader, rather than trusting
                // TCP readiness or only the controller's CreateTopics acknowledgement.
                await admin.DescribeTopicsAsync(["_schemas"], cancellationToken).ConfigureAwait(false);
                var result = await admin.ListOffsetsAsync(offsets, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (result.TryGetValue(partition, out var offset) && offset.Offset >= 0)
                    return;
            }
            catch (KafkaException exception) when (exception.IsRetriable)
            {
                Console.WriteLine($"[KafkaWithSchemaRegistry] Waiting for _schemas leader: {exception.Message}");
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitForKafkaAsync()
    {
        Console.WriteLine("[KafkaWithSchemaRegistry] Waiting for Kafka to be ready...");
        const int maxAttempts = 30;

        var endpoint = BootstrapServerList.Parse(_bootstrapServers);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(endpoint.Host, endpoint.Port).ConfigureAwait(false);
                if (client.Connected)
                {
                    Console.WriteLine("[KafkaWithSchemaRegistry] Kafka is accepting connections");
                    await Task.Delay(2000).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KafkaWithSchemaRegistry] TCP connect attempt {attempt + 1} failed: {ex.Message}");
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts at {_bootstrapServers}");
    }

    private async Task WaitForServicesAsync()
    {
        Console.WriteLine("[KafkaWithSchemaRegistry] Waiting for Schema Registry to be ready...");

        // Wait for Schema Registry HTTP endpoint
        const int maxAttempts = 30;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync($"{_registryUrl}/subjects").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[KafkaWithSchemaRegistry] Schema Registry is ready");
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Schema Registry not ready after {maxAttempts} attempts");
    }

    /// <summary>
    /// Creates a unique topic for a test and returns the topic name.
    /// </summary>
    public async Task<string> CreateTestTopicAsync(
        int partitions = 1,
        IReadOnlyDictionary<string, string>? configs = null)
    {
        var topicName = $"test-topic-{Guid.NewGuid():N}";
        await CreateTopicAsync(topicName, partitions, configs: configs).ConfigureAwait(false);
        return topicName;
    }

    /// <summary>
    /// Creates a topic with the specified name.
    /// </summary>
    public async Task CreateTopicAsync(
        string topicName,
        int partitions = 1,
        int replicationFactor = 1,
        IReadOnlyDictionary<string, string>? configs = null)
    {
        if (_createdTopics.ContainsKey(topicName))
        {
            return;
        }

        Console.WriteLine($"[KafkaWithSchemaRegistry] Creating topic '{topicName}' with {partitions} partition(s)...");

        if (!string.IsNullOrEmpty(_bootstrapServers))
        {
            await using var adminClient = Kafka.CreateAdminClient()
                .WithBootstrapServers(_bootstrapServers)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .Build();

            await adminClient.CreateTopicsAsync([
                new Admin.NewTopic
                {
                    Name = topicName,
                    NumPartitions = partitions,
                    ReplicationFactor = (short)replicationFactor,
                    Configs = configs
                }
            ]).ConfigureAwait(false);
        }

        _createdTopics.TryAdd(topicName, 0);
        await Task.Delay(500).ConfigureAwait(false);
        Console.WriteLine($"[KafkaWithSchemaRegistry] Topic '{topicName}' created");
    }

    public async ValueTask DisposeAsync()
    {
        if (_externalKafka && _externalRegistry)
        {
            GC.SuppressFinalize(this);
            return;
        }

        if (_schemaRegistryContainer is not null)
        {
            await _schemaRegistryContainer.DisposeAsync().ConfigureAwait(false);
        }

        if (_kafkaContainer is not null)
        {
            await _kafkaContainer.DisposeAsync().ConfigureAwait(false);
        }

        if (_network is not null)
        {
            await _network.DeleteAsync().ConfigureAwait(false);
            await _network.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}

public sealed class KafkaWithAssociationSchemaRegistryContainer : KafkaWithSchemaRegistryContainer
{
    protected override string SchemaRegistryImage => "confluentinc/cp-schema-registry:8.2.0";
    protected override string KafkaImage => $"apache/kafka:{KafkaContainerDefault.DefaultTag}";

    protected override KafkaBuilder ConfigureKafkaBuilder(KafkaBuilder builder) =>
        KafkaContainerDefault.ConfigureBuilderForVersion(
            builder,
            KafkaContainerDefault.DefaultTag);
}
