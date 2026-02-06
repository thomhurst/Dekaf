using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Pre-allocated keys to eliminate string interpolation allocations in hot paths.
    /// Using 10,000 keys provides good distribution without excessive memory usage.
    /// </summary>
    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);

    private static string[] CreatePreAllocatedKeys(int count)
    {
        var keys = new string[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = $"key-{i}";
        }
        return keys;
    }

    /// <summary>
    /// Gets a pre-allocated key using modulo indexing for zero-allocation key selection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetKey(long index) => PreAllocatedKeys[index % PreAllocatedKeys.Length];

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

        await using var producer = Kafka.CreateProducer<string, string>()
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

        // Force GC and capture baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var count = 0L;
        var gcStats = new GcStats();

        while (!cts.Token.IsCancellationRequested)
        {
            for (var i = 0; i < batchSize && !cts.Token.IsCancellationRequested; i++)
            {
                // Use pre-allocated key to avoid string interpolation allocation
                producer.Produce(Topic, GetKey(count), messageValue);
                count++;
            }
        }

        // Flush remaining
        await producer.FlushAsync().ConfigureAwait(false);

        sw.Stop();
        gcStats.Capture();

        PrintResults("Messages sent", count, sw.Elapsed, gcStats);
    }

    private static async Task RunProducerAckedAsync(string bootstrapServers, int durationSeconds, int messageSize)
    {
        var messageValue = new string('x', messageSize);

        await using var producer = Kafka.CreateProducer<string, string>()
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

        // Force GC and capture baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var count = 0L;
        var gcStats = new GcStats();

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = Topic,
                    Key = GetKey(count),
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
        gcStats.Capture();

        PrintResults("Messages sent", count, sw.Elapsed, gcStats);
    }

    private static async Task RunProducerBatchAsync(string bootstrapServers, int durationSeconds, int messageSize, int batchSize)
    {
        var messageValue = new string('x', messageSize);

        await using var producer = Kafka.CreateProducer<string, string>()
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

        // Force GC and capture baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Pre-allocate task array to avoid per-batch allocation
        var tasks = new Task<RecordMetadata>[batchSize];

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var count = 0L;
        var gcStats = new GcStats();

        while (!cts.Token.IsCancellationRequested)
        {
            // Fire batch of messages using pre-allocated keys
            for (var i = 0; i < batchSize; i++)
            {
                tasks[i] = producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = Topic,
                    Key = GetKey(count + i),
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
        gcStats.Capture();

        PrintResults("Messages sent", count, sw.Elapsed, gcStats);
    }

    private static async Task RunConsumerAsync(string bootstrapServers, int durationSeconds, int messageSize, int batchSize)
    {
        // First, seed some messages to consume
        Console.WriteLine("  Seeding messages for consumer test...");
        var messageValue = new string('x', messageSize);
        var messagesToSeed = batchSize * 100; // Seed plenty of messages

        await using (var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("profiling-seeder")
            .WithAcks(Acks.Leader)
            .WithLingerMs(1)
            .Build())
        {
            for (var i = 0; i < messagesToSeed; i++)
            {
                producer.Produce(Topic, GetKey(i), messageValue);
            }
            await producer.FlushAsync().ConfigureAwait(false);
        }
        Console.WriteLine($"  Seeded {messagesToSeed:N0} messages");

        // Now consume
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("profiling-consumer")
            .WithGroupId($"profiling-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(Topic);

        // Force GC and capture baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var count = 0L;
        var gcStats = new GcStats();

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
        gcStats.Capture();

        PrintResults("Messages consumed", count, sw.Elapsed, gcStats);
    }

    private static async Task RunRoundtripAsync(KafkaEnvironment kafka, int durationSeconds, int messageSize, int batchSize)
    {
        var messageValue = new string('x', messageSize);
        var roundtripTopic = $"profiling-roundtrip-{Guid.NewGuid():N}";

        await kafka.CreateTopicAsync(roundtripTopic, partitions: 1).ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("profiling-roundtrip-producer")
            .WithAcks(Acks.Leader)
            .WithLingerMs(1)
            .Build();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("profiling-roundtrip-consumer")
            .WithGroupId($"profiling-roundtrip-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(roundtripTopic);

        // Force GC and capture baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var sw = Stopwatch.StartNew();
        var produced = 0L;
        var consumed = 0L;
        var gcStats = new GcStats();

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

        // Produce messages using pre-allocated keys
        while (!cts.Token.IsCancellationRequested)
        {
            for (var i = 0; i < batchSize && !cts.Token.IsCancellationRequested; i++)
            {
                producer.Produce(roundtripTopic, GetKey(produced), messageValue);
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
        gcStats.Capture();

        Console.WriteLine($"  Messages produced: {produced:N0}");
        Console.WriteLine($"  Messages consumed: {consumed:N0}");
        Console.WriteLine($"  Producer throughput: {produced / sw.Elapsed.TotalSeconds:N0} msg/sec");
        Console.WriteLine($"  Consumer throughput: {consumed / sw.Elapsed.TotalSeconds:N0} msg/sec");
        PrintGcStats(gcStats);
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
                await using var producer = Kafka.CreateProducer<string, string>()
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

    private static void PrintResults(string label, long count, TimeSpan elapsed, GcStats gcStats)
    {
        Console.WriteLine($"  {label}: {count:N0}");
        Console.WriteLine($"  Throughput: {count / elapsed.TotalSeconds:N0} msg/sec");
        PrintGcStats(gcStats);
    }

    private static void PrintGcStats(GcStats gcStats)
    {
        Console.WriteLine($"  Gen0 GCs: {gcStats.Gen0}");
        Console.WriteLine($"  Gen1 GCs: {gcStats.Gen1}");
        Console.WriteLine($"  Gen2 GCs: {gcStats.Gen2}");
        Console.WriteLine($"  Allocated: {FormatBytes(gcStats.AllocatedBytes)}");
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F2} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F2} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    /// <summary>
    /// Tracks GC collection counts across all generations.
    /// Create before the operation, then call Capture() after to calculate deltas.
    /// </summary>
    private struct GcStats
    {
        private readonly int _gen0Before;
        private readonly int _gen1Before;
        private readonly int _gen2Before;
        private readonly long _allocatedBefore;

        public int Gen0 { get; private set; }
        public int Gen1 { get; private set; }
        public int Gen2 { get; private set; }
        public long AllocatedBytes { get; private set; }

        public GcStats()
        {
            _gen0Before = GC.CollectionCount(0);
            _gen1Before = GC.CollectionCount(1);
            _gen2Before = GC.CollectionCount(2);
            _allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
            Gen0 = Gen1 = Gen2 = 0;
            AllocatedBytes = 0;
        }

        public void Capture()
        {
            Gen0 = GC.CollectionCount(0) - _gen0Before;
            Gen1 = GC.CollectionCount(1) - _gen1Before;
            Gen2 = GC.CollectionCount(2) - _gen2Before;
            AllocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - _allocatedBefore;
        }
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
