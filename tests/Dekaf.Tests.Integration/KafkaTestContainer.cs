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
public class KafkaTestContainer : IAsyncInitializer, IAsyncDisposable
{
    private KafkaContainer? _container;
    private bool _externalKafka;
    private string _bootstrapServers = string.Empty;
    private readonly HashSet<string> _createdTopics = [];

    /// <summary>
    /// The Kafka bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers => _bootstrapServers;

    public async Task InitializeAsync()
    {
        // Check for external Kafka (CI environment)
        var externalBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        if (!string.IsNullOrEmpty(externalBootstrap))
        {
            _bootstrapServers = externalBootstrap;
            _externalKafka = true;
            Console.WriteLine($"[KafkaTestContainer] Using external Kafka at {_bootstrapServers}");
            await WaitForKafkaAsync();
            return;
        }

        Console.WriteLine("[KafkaTestContainer] Starting Kafka container via Testcontainers...");
        // Use KafkaBuilder without custom port binding - it handles listeners correctly
        _container = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .Build();

        await _container.StartAsync();
        var rawAddress = _container.GetBootstrapAddress();
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
        if (_createdTopics.Contains(topicName))
        {
            Console.WriteLine($"[KafkaTestContainer] Topic '{topicName}' already created");
            return;
        }

        Console.WriteLine($"[KafkaTestContainer] Creating topic '{topicName}' with {partitions} partition(s)...");

        if (_externalKafka)
        {
            await CreateTopicViaDockerExecAsync(topicName, partitions, replicationFactor);
        }
        else if (_container is not null)
        {
            await CreateTopicViaTestcontainersAsync(topicName, partitions, replicationFactor);
        }

        _createdTopics.Add(topicName);

        // Wait for topic metadata to propagate
        // In containerized environments, metadata propagation can be slow
        await Task.Delay(3000);
        Console.WriteLine($"[KafkaTestContainer] Topic '{topicName}' created");
    }

    private static async Task CreateTopicViaDockerExecAsync(string topicName, int partitions, int replicationFactor)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --create --topic {topicName} --partitions {partitions} --replication-factor {replicationFactor} --if-not-exists",
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

    private async Task CreateTopicViaTestcontainersAsync(string topicName, int partitions, int replicationFactor)
    {
        // Use docker exec directly to avoid Docker.DotNet JSON parsing issues
        // that occur with certain Docker daemon versions.
        // The internal broker listener (BROKER://localhost:9093) is used inside the container.
        var containerId = _container!.Id;
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
        if (_externalKafka)
        {
            GC.SuppressFinalize(this);
            return;
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
