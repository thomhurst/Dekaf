using System.Collections.Concurrent;
using System.Net.Sockets;
using Dekaf.Admin;
using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Shared Kafka container for integration tests.
/// Shared across all tests in the session via ClassDataSource(Shared = SharedType.PerTestSession).
/// Subclasses can customize the container via <see cref="ConfigureBuilder"/> and
/// <see cref="OnAfterInitializeAsync"/>, and override admin client creation via
/// <see cref="CreateAdminClient"/>.
/// </summary>
public abstract class KafkaTestContainer : IAsyncInitializer, IAsyncDisposable
{
    private KafkaContainer? _container;
    protected KafkaContainer? ContainerInstance => _container;
    private KafkaContainer Container => _container ??= ConfigureBuilder(
        new KafkaBuilder(ContainerName)
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m")     // Limit JVM heap for CI runners
            .WithEnvironment("KAFKA_LOG_RETENTION_MS", "30000")           // Delete segments after 30s
            .WithEnvironment("KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS", "10000") // Check every 10s
            .WithEnvironment("KAFKA_LOG_SEGMENT_BYTES", "1048576")        // 1MB segments for faster rotation
            .WithEnvironment("KAFKA_LOG_CLEANUP_POLICY", "delete"))
        .Build();

    private string _bootstrapServers = string.Empty;
    private readonly ConcurrentDictionary<string, byte> _createdTopics = new();

    public abstract string ContainerName { get; }
    public abstract int Version { get; }

    /// <summary>
    /// The Kafka bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers => _bootstrapServers;

    /// <summary>
    /// Hook to customize the <see cref="KafkaBuilder"/> before the container is built.
    /// Subclasses can override to add environment variables (e.g., SASL configuration).
    /// </summary>
    protected virtual KafkaBuilder ConfigureBuilder(KafkaBuilder builder) => builder;

    /// <summary>
    /// Hook called after the container is started and ready for TCP connections.
    /// Subclasses can override for post-startup setup (e.g., creating SCRAM credentials).
    /// </summary>
    protected virtual ValueTask OnAfterInitializeAsync() => default;

    /// <summary>
    /// Creates an admin client configured for this container.
    /// Subclasses can override to add authentication (e.g., SASL credentials).
    /// </summary>
    protected virtual IAdminClient CreateAdminClient()
    {
        return Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .Build();
    }

    public async Task InitializeAsync()
    {
        Console.WriteLine("[KafkaTestContainer] Starting Kafka container via Testcontainers...");

        await Container.StartAsync().ConfigureAwait(false);

        var rawAddress = Container.GetBootstrapAddress();
        // GetBootstrapAddress() may return "plaintext://host:port/" - extract just host:port
        _bootstrapServers = ExtractHostPort(rawAddress);

        Console.WriteLine($"[KafkaTestContainer] Kafka started at {_bootstrapServers}");

        await WaitForKafkaAsync().ConfigureAwait(false);
        await OnAfterInitializeAsync().ConfigureAwait(false);
    }

    protected static string ExtractHostPort(string address)
    {
        // Handle format like "plaintext://127.0.0.1:9092/" or just "127.0.0.1:9092"
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return $"{uri.Host}:{uri.Port}";
        }
        // Already in host:port format
        return address.TrimEnd('/');
    }

    private async Task WaitForKafkaAsync()
    {
        Console.WriteLine("[KafkaTestContainer] Waiting for Kafka to be ready...");
        const int maxAttempts = 30;

        // Parse host and port from host:port format
        var colonIndex = _bootstrapServers.LastIndexOf(':');
        var host = _bootstrapServers[..colonIndex];
        var port = int.Parse(_bootstrapServers[(colonIndex + 1)..]);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port).ConfigureAwait(false);
                Console.WriteLine("[KafkaTestContainer] Kafka is accepting connections");
                // Give it a moment to fully initialize
                await Task.Delay(2000).ConfigureAwait(false);
                return;
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts");
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
        // Use TryAdd for thread-safe check-and-add operation
        // If already exists, TryAdd returns false and we skip creation
        if (!_createdTopics.TryAdd(topicName, 0))
        {
            Console.WriteLine($"[KafkaTestContainer] Topic '{topicName}' already created");
            return;
        }

        Console.WriteLine($"[KafkaTestContainer] Creating topic '{topicName}' with {partitions} partition(s)...");

        await CreateTopicViaAdminClientAsync(topicName, partitions, replicationFactor).ConfigureAwait(false);

        // Poll until topic metadata propagates rather than using a fixed delay
        await WaitForTopicReadyAsync(topicName).ConfigureAwait(false);
        Console.WriteLine($"[KafkaTestContainer] Topic '{topicName}' created");
    }

    private async Task CreateTopicViaAdminClientAsync(string topicName, int partitions, int replicationFactor)
    {
        await using var adminClient = CreateAdminClient();

        await adminClient.CreateTopicsAsync([
            new NewTopic
            {
                Name = topicName,
                NumPartitions = partitions,
                ReplicationFactor = (short)replicationFactor
            }
        ]).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls until the specified topic appears in the broker's topic list.
    /// </summary>
    public async Task WaitForTopicReadyAsync(string topicName, int maxAttempts = 30)
    {
        await using var adminClient = CreateAdminClient();

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var topics = await adminClient.ListTopicsAsync().ConfigureAwait(false);
                if (topics.Any(t => t.Name == topicName))
                {
                    return;
                }
            }
            catch
            {
                // Metadata not yet available, retry
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Topic '{topicName}' not visible in metadata after {maxAttempts} attempts");
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
