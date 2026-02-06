using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Shared Kafka container for integration tests.
/// Uses KAFKA_BOOTSTRAP_SERVERS env var if set (for CI), otherwise starts Testcontainers.
/// Shared across all tests in the session via ClassDataSource(Shared = SharedType.PerTestSession).
/// </summary>
public abstract class KafkaTestContainer : IAsyncInitializer, IAsyncDisposable
{
    private KafkaContainer Container => field ??= new KafkaBuilder(ContainerName).Build();
    private string _bootstrapServers = string.Empty;
    private readonly ConcurrentDictionary<string, byte> _createdTopics = new();
    
    public abstract string ContainerName { get; }
    public abstract int Version { get; }

    /// <summary>
    /// The Kafka bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers => _bootstrapServers;

    public async Task InitializeAsync()
    {
        Console.WriteLine("[KafkaTestContainer] Starting Kafka container via Testcontainers...");

        await Container.StartAsync();
        
        var rawAddress = Container.GetBootstrapAddress();
        // GetBootstrapAddress() may return "plaintext://host:port/" - extract just host:port
        _bootstrapServers = ExtractHostPort(rawAddress);
        
        Console.WriteLine($"[KafkaTestContainer] Kafka started at {_bootstrapServers}");
        
        await WaitForKafkaAsync();
    }

    private static string ExtractHostPort(string address)
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
                await client.ConnectAsync(host, port);
                if (client.Connected)
                {
                    Console.WriteLine("[KafkaTestContainer] Kafka is accepting connections");
                    // Give it a moment to fully initialize
                    await Task.Delay(2000);
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts");
    }

    /// <summary>
    /// Creates a unique topic for a test and returns the topic name.
    /// </summary>
    public async Task<string> CreateTestTopicAsync(int partitions = 1)
    {
        var topicName = $"test-topic-{Guid.NewGuid():N}";
        await CreateTopicAsync(topicName, partitions);
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

        await CreateTopicViaTestcontainersAsync(topicName, partitions, replicationFactor);

        // Wait for topic metadata to propagate
        // In containerized environments, metadata propagation can be slow
        await Task.Delay(3000);
        Console.WriteLine($"[KafkaTestContainer] Topic '{topicName}' created");
    }

    private async Task CreateTopicViaTestcontainersAsync(string topicName, int partitions, int replicationFactor)
    {
        // Use docker exec directly to avoid Docker.DotNet JSON parsing issues
        // that occur with certain Docker daemon versions.
        // The internal broker listener (BROKER://localhost:9093) is used inside the container.
        var containerId = Container!.Id;
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"exec {containerId} kafka-topics --bootstrap-server localhost:9093 --create --topic {topicName} --partitions {partitions} --replication-factor {replicationFactor} --if-not-exists",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        var error = await process.StandardError.ReadToEndAsync();
        if (process.ExitCode != 0)
        {
            Console.WriteLine($"[KafkaTestContainer] Warning: Failed to create topic: {error}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
