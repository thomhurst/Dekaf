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
    private KafkaContainer? _kafkaContainer;
    private IContainer? _schemaRegistryContainer;
    private INetwork? _network;
    private bool _externalKafka;
    private bool _externalRegistry;
    private string _bootstrapServers = string.Empty;
    private string _registryUrl = string.Empty;
    private readonly HashSet<string> _createdTopics = [];

    /// <summary>
    /// The Kafka bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers => _bootstrapServers;

    /// <summary>
    /// The Schema Registry URL.
    /// </summary>
    public string RegistryUrl => _registryUrl;

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
            await WaitForServicesAsync().ConfigureAwait(false);
            return;
        }

        Console.WriteLine("[KafkaWithSchemaRegistry] Creating Docker network...");

        // Create a shared network for the containers
        _network = new NetworkBuilder()
            .WithName($"kafka-sr-network-{Guid.NewGuid():N}")
            .Build();
        await _network.CreateAsync().ConfigureAwait(false);

        Console.WriteLine("[KafkaWithSchemaRegistry] Starting Kafka container...");

        // Start Kafka with network alias
        _kafkaContainer = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .WithNetwork(_network)
            .WithNetworkAliases("kafka")
            .Build();

        await _kafkaContainer.StartAsync().ConfigureAwait(false);
        _bootstrapServers = ExtractHostPort(_kafkaContainer.GetBootstrapAddress());
        Console.WriteLine($"[KafkaWithSchemaRegistry] Kafka started at {_bootstrapServers}");

        Console.WriteLine("[KafkaWithSchemaRegistry] Starting Schema Registry container...");

        // Start Schema Registry connected to Kafka via network
        _schemaRegistryContainer = new ContainerBuilder("confluentinc/cp-schema-registry:7.5.0")
            .WithNetwork(_network)
            .WithNetworkAliases("schema-registry")
            .WithPortBinding(8081, true)
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", "kafka:9092")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8081")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/subjects").ForPort(8081)))
            .Build();

        await _schemaRegistryContainer.StartAsync().ConfigureAwait(false);

        var port = _schemaRegistryContainer.GetMappedPublicPort(8081);
        _registryUrl = $"http://localhost:{port}";
        Console.WriteLine($"[KafkaWithSchemaRegistry] Schema Registry started at {_registryUrl}");

        await WaitForServicesAsync().ConfigureAwait(false);
    }

    private static string ExtractHostPort(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return $"{uri.Host}:{uri.Port}";
        }
        return address.TrimEnd('/');
    }

    private async Task WaitForServicesAsync()
    {
        Console.WriteLine("[KafkaWithSchemaRegistry] Waiting for services to be ready...");

        // Wait for Schema Registry
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
    public async Task<string> CreateTestTopicAsync(int partitions = 1)
    {
        var topicName = $"test-topic-{Guid.NewGuid():N}";
        await CreateTopicAsync(topicName, partitions).ConfigureAwait(false);
        return topicName;
    }

    /// <summary>
    /// Creates a topic with the specified name.
    /// </summary>
    public async Task CreateTopicAsync(string topicName, int partitions = 1, int replicationFactor = 1)
    {
        if (_createdTopics.Contains(topicName))
        {
            return;
        }

        Console.WriteLine($"[KafkaWithSchemaRegistry] Creating topic '{topicName}' with {partitions} partition(s)...");

        if (_kafkaContainer is not null)
        {
            // Use docker exec directly to avoid Docker.DotNet JSON parsing issues
            // that occur with certain Docker daemon versions (e.g., 29.x).
            var containerId = _kafkaContainer.Id;
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec {containerId} kafka-topics --bootstrap-server localhost:9093 --create --topic {topicName} --partitions {partitions} --replication-factor {replicationFactor} --if-not-exists",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.Start();
            await process.WaitForExitAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"[KafkaWithSchemaRegistry] Warning: Failed to create topic: {error}");
            }
        }

        _createdTopics.Add(topicName);
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
