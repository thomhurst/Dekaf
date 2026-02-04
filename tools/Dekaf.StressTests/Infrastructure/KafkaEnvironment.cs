using Testcontainers.Kafka;

namespace Dekaf.StressTests.Infrastructure;

/// <summary>
/// Manages Kafka environment for stress tests.
/// Supports both external Kafka (via KAFKA_BOOTSTRAP_SERVERS env var) and Testcontainers.
/// </summary>
internal sealed class KafkaEnvironment : IAsyncDisposable
{
    public string BootstrapServers { get; }
    private readonly KafkaContainer? _container;

    private KafkaEnvironment(string bootstrapServers, KafkaContainer? container)
    {
        BootstrapServers = bootstrapServers;
        _container = container;
    }

    public static async Task<KafkaEnvironment> CreateAsync()
    {
        var externalBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        if (!string.IsNullOrEmpty(externalBootstrap))
        {
            Console.WriteLine($"Using external Kafka at {externalBootstrap}");
            return new KafkaEnvironment(externalBootstrap, null);
        }

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

    private static async Task WaitForKafkaAsync(string bootstrapServers)
    {
        Console.WriteLine("Waiting for Kafka to be ready...");
        const int maxAttempts = 30;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await using var producer = Kafka.CreateProducer<string, string>()
                    .WithBootstrapServers(bootstrapServers)
                    .WithClientId("kafka-ready-check")
                    .WithAcks(Producer.Acks.Leader)
                    .Build();

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

    public async Task CreateTopicAsync(string topic, int partitions)
    {
        if (_container is null)
        {
            Console.WriteLine($"Using external Kafka - assuming topic {topic} exists or will be auto-created");
            return;
        }

        var result = await _container.ExecAsync([
            "kafka-topics",
            "--bootstrap-server", "localhost:9092",
            "--create",
            "--topic", topic,
            "--partitions", partitions.ToString(),
            "--replication-factor", "1",
            "--if-not-exists"
        ]).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            Console.WriteLine($"Created topic: {topic}");
        }
        else
        {
            Console.WriteLine($"Warning: Topic creation returned exit code {result.ExitCode}: {result.Stderr}");
        }

        await Task.Delay(1000).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            Console.WriteLine("Stopping Kafka container...");
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
