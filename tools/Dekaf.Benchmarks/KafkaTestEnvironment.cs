using Testcontainers.Kafka;

namespace Dekaf.Benchmarks;

/// <summary>
/// Manages a Kafka container for benchmarking.
/// </summary>
public sealed class KafkaTestEnvironment : IAsyncDisposable
{
    private KafkaContainer? _container;
    private bool _disposed;

    public string BootstrapServers { get; private set; } = string.Empty;

    public static async Task<KafkaTestEnvironment> CreateAsync()
    {
        var env = new KafkaTestEnvironment();
        await env.StartAsync();
        return env;
    }

    private async Task StartAsync()
    {
        Console.WriteLine("Starting Kafka container...");

        _container = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .WithPortBinding(9092, true)
            .Build();

        await _container.StartAsync();

        BootstrapServers = _container.GetBootstrapAddress();
        Console.WriteLine($"Kafka started at {BootstrapServers}");

        // Wait for Kafka to be fully ready
        await Task.Delay(2000);
    }

    public async Task CreateTopicAsync(string topic, int partitions = 1)
    {
        if (_container is null)
            throw new InvalidOperationException("Container not started");

        var result = await _container.ExecAsync([
            "kafka-topics",
            "--bootstrap-server", "localhost:9092",
            "--create",
            "--topic", topic,
            "--partitions", partitions.ToString(),
            "--replication-factor", "1"
        ]);

        if (result.ExitCode != 0)
        {
            Console.WriteLine($"Warning: Failed to create topic {topic}: {result.Stderr}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_container is not null)
        {
            Console.WriteLine("Stopping Kafka container...");
            await _container.DisposeAsync();
        }
    }
}
