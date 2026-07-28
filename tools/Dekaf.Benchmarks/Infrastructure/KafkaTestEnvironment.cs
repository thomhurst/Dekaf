using System.Text;
using DotNet.Testcontainers.Configurations;
using Testcontainers.Kafka;

namespace Dekaf.Benchmarks.Infrastructure;

/// <summary>
/// Manages a Kafka container for benchmarking.
/// Uses KAFKA_BOOTSTRAP_SERVERS env var if set (for CI), otherwise starts Testcontainers.
/// </summary>
public sealed class KafkaTestEnvironment : IAsyncDisposable
{
    private const string KafkaImage = "apache/kafka:4.3.1";

    // Testcontainers.Kafka emits a trailing comma in KAFKA_ADVERTISED_LISTENERS
    // when no extra listener is configured. Kafka 4.2+ rejects that empty value.
    private static readonly byte[] RunWrapperScript = Encoding.UTF8.GetBytes(
        "#!/bin/bash\n" +
        ". /etc/kafka/docker/bash-config\n" +
        "export KAFKA_ADVERTISED_LISTENERS=$(echo \"$KAFKA_ADVERTISED_LISTENERS\" | sed 's/,$//')\n" +
        ". /etc/kafka/docker/configureDefaults\n" +
        ". /etc/kafka/docker/configure\n" +
        ". /etc/kafka/docker/launch\n");

    private KafkaContainer? _container;
    private bool _disposed;
    private bool _externalKafka;

    public string BootstrapServers { get; private set; } = string.Empty;

    public static async Task<KafkaTestEnvironment> CreateAsync()
    {
        var env = new KafkaTestEnvironment();
        await env.StartAsync().ConfigureAwait(false);
        return env;
    }

    private async Task StartAsync()
    {
        var externalBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        if (!string.IsNullOrEmpty(externalBootstrap))
        {
            BootstrapServers = externalBootstrap;
            _externalKafka = true;
            Console.WriteLine($"Using external Kafka at {BootstrapServers}");

            await WaitForKafkaAsync().ConfigureAwait(false);
            return;
        }

        Console.WriteLine("Starting Kafka container via Testcontainers...");

        _container = new KafkaBuilder(KafkaImage)
            .WithPortBinding(9092, true)
            .WithResourceMapping(
                RunWrapperScript,
                "/etc/kafka/docker/run",
                0,
                0,
                UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute |
                UnixFileModes.GroupRead | UnixFileModes.GroupExecute |
                UnixFileModes.OtherRead | UnixFileModes.OtherExecute)
            .Build();

        await _container.StartAsync().ConfigureAwait(false);

        BootstrapServers = BootstrapServerList.Parse(_container.GetBootstrapAddress()).Normalized;
        Console.WriteLine($"Kafka started at {BootstrapServers}");

        await WaitForKafkaAsync().ConfigureAwait(false);
    }

    private async Task WaitForKafkaAsync()
    {
        Console.WriteLine("Waiting for Kafka to be ready...");
        var maxAttempts = 30;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            try
            {
                using var adminClient = new Confluent.Kafka.AdminClientBuilder(
                    new Confluent.Kafka.AdminClientConfig { BootstrapServers = BootstrapServers })
                    .Build();

                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                if (metadata.Brokers.Count > 0)
                {
                    Console.WriteLine($"Kafka is ready with {metadata.Brokers.Count} broker(s)");
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            attempt++;
            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts");
    }

    public async Task CreateTopicAsync(string topic, int partitions = 1)
    {
        using var adminClient = new Confluent.Kafka.AdminClientBuilder(
            new Confluent.Kafka.AdminClientConfig { BootstrapServers = BootstrapServers })
            .Build();

        try
        {
            await adminClient.CreateTopicsAsync([
                new Confluent.Kafka.Admin.TopicSpecification
                {
                    Name = topic,
                    NumPartitions = partitions,
                    ReplicationFactor = 1
                }
            ]).ConfigureAwait(false);
        }
        catch (Confluent.Kafka.Admin.CreateTopicsException ex)
        {
            if (!ex.Message.Contains("already exists"))
            {
                Console.WriteLine($"Warning: Failed to create topic {topic}: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_externalKafka)
        {
            Console.WriteLine("Using external Kafka - skipping container cleanup");
            return;
        }

        if (_container is not null)
        {
            Console.WriteLine("Stopping Kafka container...");
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
