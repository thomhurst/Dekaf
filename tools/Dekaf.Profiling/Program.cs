using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Producer;
using Testcontainers.Kafka;
using DekafLib = Dekaf;

namespace Dekaf.Profiling;

/// <summary>
/// Dedicated profiling app for Dekaf producers and consumers against real Kafka.
///
/// Usage:
///   dotnet run -c Release -- [scenario] [duration_seconds] [message_size] [batch_size]
///
/// Scenarios:
///   producer-fire-forget  - Fire-and-forget producer (no acks waiting)
///   producer-acked        - Producer with acks (awaits each message)
///   producer-batch        - Batch producer (fires batch, awaits all)
///   consumer              - Consumer consuming messages
///   roundtrip             - End-to-end produce then consume
///   all                   - Run all scenarios sequentially
///
/// Environment Variables:
///   KAFKA_BOOTSTRAP_SERVERS - Use external Kafka instead of Testcontainers
///
/// Examples:
///   dotnet run -c Release -- producer-fire-forget 30 1000 1000
///   dotnet run -c Release -- all 15
///
/// Profiling:
///   dotnet-trace collect --profile dotnet-sampled-thread-time -- \
///     ./bin/Release/net10.0/Dekaf.Profiling producer-fire-forget 30
///
///   dotnet-counters monitor -p [PID] --counters System.Runtime
/// </summary>
public static class Program
{
    private const string Topic = "profiling-topic";

    public static async Task<int> Main(string[] args)
    {
        var scenario = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
        var durationSeconds = args.Length > 1 ? int.Parse(args[1]) : 15;
        var messageSize = args.Length > 2 ? int.Parse(args[2]) : 1000;
        var batchSize = args.Length > 3 ? int.Parse(args[3]) : 100;

        Console.WriteLine("Dekaf Profiling App (Real Kafka)");
        Console.WriteLine($"PID: {Environment.ProcessId}");
        Console.WriteLine($"Scenario: {scenario}");
        Console.WriteLine($"Duration: {durationSeconds}s");
        Console.WriteLine($"Message Size: {messageSize} bytes");
        Console.WriteLine($"Batch Size: {batchSize}");
        Console.WriteLine(new string('-', 50));

        // Start Kafka
        await using var kafka = await StartKafkaAsync();
        Console.WriteLine($"Kafka ready at: {kafka.BootstrapServers}");

        // Create topic
        await kafka.CreateTopicAsync(Topic, partitions: 3).ConfigureAwait(false);

        // Give time to attach profiler
        Console.WriteLine("\nStarting in 2 seconds (attach profiler now)...");
        await Task.Delay(2000).ConfigureAwait(false);

        var scenarios = scenario switch
        {
            "all" => new[] { "producer-fire-forget", "producer-acked", "producer-batch", "consumer", "roundtrip" },
            _ => new[] { scenario }
        };

        foreach (var s in scenarios)
        {
            Console.WriteLine($"\n=== Running: {s} ===");

            switch (s)
            {
                case "producer-fire-forget":
                    await RunProducerFireAndForgetAsync(kafka.BootstrapServers, durationSeconds, messageSize, batchSize).ConfigureAwait(false);
                    break;
                case "producer-acked":
                    await RunProducerAckedAsync(kafka.BootstrapServers, durationSeconds, messageSize).ConfigureAwait(false);
                    break;
                case "producer-batch":
                    await RunProducerBatchAsync(kafka.BootstrapServers, durationSeconds, messageSize, batchSize).ConfigureAwait(false);
                    break;
                case "consumer":
                    await RunConsumerAsync(kafka.BootstrapServers, durationSeconds, messageSize, batchSize).ConfigureAwait(false);
                    break;
                case "roundtrip":
                    await RunRoundtripAsync(kafka, durationSeconds, messageSize, batchSize).ConfigureAwait(false);
                    break;
                default:
                    Console.WriteLine($"Unknown scenario: {s}");
                    PrintUsage();
                    return 1;
            }

            // Force GC between scenarios
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Console.WriteLine("\nProfiling complete.");
        return 0;
    }

    private static async Task RunProducerFireAndForgetAsync(string bootstrapServers, int durationSeconds, int messageSize, int batchSize)
    {
        var messageValue = new string('x', messageSize);

        await using var producer = DekafLib.Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("profiling-producer")
            .WithAcks(Acks.Leader)
            .WithLingerMs(5)
            .WithBatchSize(16384)
            .Build();

        // Warmup
        for (var i = 0; i < 100; i++)
        {
            producer.Produce(Topic, "warmup", "warmup");
        }
        await producer.FlushAsync().ConfigureAwait(false);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var count = 0L;
        var gcBefore = GC.CollectionCount(0);

        while (!cts.Token.IsCancellationRequested)
        {
            for (var i = 0; i < batchSize && !cts.Token.IsCancellationRequested; i++)
            {
                producer.Produce(Topic, $"key-{count % 10000}", messageValue);
                count++;
            }
        }

        // Flush remaining
        await producer.FlushAsync().ConfigureAwait(false);

        sw.Stop();
        var gcAfter = GC.CollectionCount(0);

        Console.WriteLine($"  Messages sent: {count:N0}");
        Console.WriteLine($"  Throughput: {count / sw.Elapsed.TotalSeconds:N0} msg/sec");
        Console.WriteLine($"  Gen0 GCs: {gcAfter - gcBefore}");
    }

    private static async Task RunProducerAckedAsync(string bootstrapServers, int durationSeconds, int messageSize)
    {
        var messageValue = new string('x', messageSize);

        await using var producer = DekafLib.Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("profiling-producer-acked")
            .WithAcks(Acks.All)
            .WithLingerMs(1)
            .WithBatchSize(16384)
            .Build();

        // Warmup
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "warmup",
                Value = "warmup"
            }).ConfigureAwait(false);
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var count = 0L;
        var gcBefore = GC.CollectionCount(0);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = Topic,
                    Key = $"key-{count % 10000}",
                    Value = messageValue
                }, cts.Token).ConfigureAwait(false);
                count++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        sw.Stop();
        var gcAfter = GC.CollectionCount(0);

        Console.WriteLine($"  Messages sent: {count:N0}");
        Console.WriteLine($"  Throughput: {count / sw.Elapsed.TotalSeconds:N0} msg/sec");
        Console.WriteLine($"  Gen0 GCs: {gcAfter - gcBefore}");
    }

    private static async Task RunProducerBatchAsync(string bootstrapServers, int durationSeconds, int messageSize, int batchSize)
    {
        var messageValue = new string('x', messageSize);

        await using var producer = DekafLib.Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("profiling-producer-batch")
            .WithAcks(Acks.Leader)
            .WithLingerMs(5)
            .WithBatchSize(16384)
            .Build();

        // Warmup
        for (var i = 0; i < 100; i++)
        {
            producer.Produce(Topic, "warmup", "warmup");
        }
        await producer.FlushAsync().ConfigureAwait(false);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var count = 0L;
        var gcBefore = GC.CollectionCount(0);

        while (!cts.Token.IsCancellationRequested)
        {
            // Fire batch of messages
            var tasks = new Task<RecordMetadata>[batchSize];
            for (var i = 0; i < batchSize; i++)
            {
                tasks[i] = producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = Topic,
                    Key = $"key-{(count + i) % 10000}",
                    Value = messageValue
                }).AsTask();
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
                count += batchSize;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        sw.Stop();
        var gcAfter = GC.CollectionCount(0);

        Console.WriteLine($"  Messages sent: {count:N0}");
        Console.WriteLine($"  Throughput: {count / sw.Elapsed.TotalSeconds:N0} msg/sec");
        Console.WriteLine($"  Gen0 GCs: {gcAfter - gcBefore}");
    }

    private static async Task RunConsumerAsync(string bootstrapServers, int durationSeconds, int messageSize, int batchSize)
    {
        // First, seed some messages to consume
        Console.WriteLine("  Seeding messages for consumer test...");
        var messageValue = new string('x', messageSize);
        var messagesToSeed = batchSize * 100; // Seed plenty of messages

        await using (var producer = DekafLib.Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("profiling-seeder")
            .WithAcks(Acks.Leader)
            .WithLingerMs(1)
            .Build())
        {
            for (var i = 0; i < messagesToSeed; i++)
            {
                producer.Produce(Topic, $"key-{i}", messageValue);
            }
            await producer.FlushAsync().ConfigureAwait(false);
        }
        Console.WriteLine($"  Seeded {messagesToSeed:N0} messages");

        // Now consume
        await using var consumer = DekafLib.Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("profiling-consumer")
            .WithGroupId($"profiling-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(Topic);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var count = 0L;
        var gcBefore = GC.CollectionCount(0);

        try
        {
            await foreach (var result in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
            {
                count++;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        sw.Stop();
        var gcAfter = GC.CollectionCount(0);

        Console.WriteLine($"  Messages consumed: {count:N0}");
        Console.WriteLine($"  Throughput: {count / sw.Elapsed.TotalSeconds:N0} msg/sec");
        Console.WriteLine($"  Gen0 GCs: {gcAfter - gcBefore}");
    }

    private static async Task RunRoundtripAsync(KafkaEnvironment kafka, int durationSeconds, int messageSize, int batchSize)
    {
        var messageValue = new string('x', messageSize);
        var roundtripTopic = $"profiling-roundtrip-{Guid.NewGuid():N}";

        await kafka.CreateTopicAsync(roundtripTopic, partitions: 1).ConfigureAwait(false);

        await using var producer = DekafLib.Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("profiling-roundtrip-producer")
            .WithAcks(Acks.Leader)
            .WithLingerMs(1)
            .Build();

        await using var consumer = DekafLib.Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("profiling-roundtrip-consumer")
            .WithGroupId($"profiling-roundtrip-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(roundtripTopic);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var produced = 0L;
        var consumed = 0L;
        var gcBefore = GC.CollectionCount(0);

        // Start consumer in background
        var consumerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var result in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
                {
                    Interlocked.Increment(ref consumed);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }, cts.Token);

        // Produce messages
        while (!cts.Token.IsCancellationRequested)
        {
            for (var i = 0; i < batchSize && !cts.Token.IsCancellationRequested; i++)
            {
                producer.Produce(roundtripTopic, $"key-{produced % 10000}", messageValue);
                produced++;
            }

            // Small delay to let consumer catch up
            try
            {
                await Task.Delay(1, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Flush and wait for consumer
        await producer.FlushAsync().ConfigureAwait(false);
        try { await consumerTask.ConfigureAwait(false); } catch { }

        sw.Stop();
        var gcAfter = GC.CollectionCount(0);

        Console.WriteLine($"  Messages produced: {produced:N0}");
        Console.WriteLine($"  Messages consumed: {consumed:N0}");
        Console.WriteLine($"  Producer throughput: {produced / sw.Elapsed.TotalSeconds:N0} msg/sec");
        Console.WriteLine($"  Consumer throughput: {consumed / sw.Elapsed.TotalSeconds:N0} msg/sec");
        Console.WriteLine($"  Gen0 GCs: {gcAfter - gcBefore}");
    }

    private static async Task<KafkaEnvironment> StartKafkaAsync()
    {
        // Check for external Kafka
        var externalBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        if (!string.IsNullOrEmpty(externalBootstrap))
        {
            Console.WriteLine($"Using external Kafka at {externalBootstrap}");
            return new KafkaEnvironment(externalBootstrap, null);
        }

        Console.WriteLine("Starting Kafka container via Testcontainers...");
        var container = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .WithPortBinding(9092, true)
            .Build();

        await container.StartAsync().ConfigureAwait(false);

        // GetBootstrapAddress returns format like "plaintext://127.0.0.1:9092/"
        // Dekaf expects just "127.0.0.1:9092"
        var rawAddress = container.GetBootstrapAddress();
        var bootstrapServers = rawAddress;
        if (Uri.TryCreate(rawAddress, UriKind.Absolute, out var uri))
        {
            bootstrapServers = $"{uri.Host}:{uri.Port}";
        }
        Console.WriteLine($"Kafka started at {bootstrapServers}");

        // Wait for Kafka to be ready by attempting to connect with a producer
        await WaitForKafkaAsync(bootstrapServers).ConfigureAwait(false);

        return new KafkaEnvironment(bootstrapServers, container);
    }

    private static async Task WaitForKafkaAsync(string bootstrapServers)
    {
        Console.WriteLine($"Waiting for Kafka to be ready at {bootstrapServers}...");
        var maxAttempts = 30;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                // Try to create a producer and send a test message
                await using var producer = DekafLib.Dekaf.CreateProducer<string, string>()
                    .WithBootstrapServers(bootstrapServers)
                    .WithClientId("kafka-ready-check")
                    .WithAcks(Acks.Leader)
                    .Build();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = "__kafka_ready_check",
                    Key = "check",
                    Value = "check"
                }, cts.Token).ConfigureAwait(false);

                Console.WriteLine("Kafka is ready");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Attempt {attempt + 1}/{maxAttempts}: {ex.GetType().Name}: {ex.Message}");
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""

            Usage: dotnet run -c Release -- [scenario] [duration_seconds] [message_size] [batch_size]

            Scenarios:
              producer-fire-forget  - Fire-and-forget producer (no acks waiting)
              producer-acked        - Producer with acks (awaits each message)
              producer-batch        - Batch producer (fires batch, awaits all)
              consumer              - Consumer consuming messages
              roundtrip             - End-to-end produce then consume
              all                   - Run all scenarios sequentially

            Environment Variables:
              KAFKA_BOOTSTRAP_SERVERS - Use external Kafka instead of Testcontainers

            Examples:
              dotnet run -c Release -- producer-fire-forget 30 1000 1000
              dotnet run -c Release -- all 15
            """);
    }

    private sealed class KafkaEnvironment : IAsyncDisposable
    {
        public string BootstrapServers { get; }
        private readonly KafkaContainer? _container;

        public KafkaEnvironment(string bootstrapServers, KafkaContainer? container)
        {
            BootstrapServers = bootstrapServers;
            _container = container;
        }

        public async Task CreateTopicAsync(string topic, int partitions)
        {
            if (_container is null)
            {
                // External Kafka - rely on auto-create or assume topic exists
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
}
