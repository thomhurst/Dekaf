using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConfluentProducerIdempotentStressTest : IStressTestScenario
{
    public string Name => "producer-idempotent";
    public string Client => "Confluent";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var latency = StressTestHelpers.CreateDeliveryLatencyTracker();
        var startedAt = DateTime.UtcNow;

        var config = new ConfluentKafka.ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-producer-idempotent-confluent",
            EnableIdempotence = true,
            Acks = ConfluentKafka.Acks.All,
            LingerMs = options.LingerMs,
            BatchSize = options.BatchSize,
            QueueBufferingMaxKbytes = ConfluentStressTestHelpers.QueueBufferingMaxKbytes,
            QueueBufferingMaxMessages = ConfluentStressTestHelpers.QueueBufferingMaxMessages,
            CompressionType = options.Compression switch
            {
                "lz4" => ConfluentKafka.CompressionType.Lz4,
                "snappy" => ConfluentKafka.CompressionType.Snappy,
                "zstd" => ConfluentKafka.CompressionType.Zstd,
                _ => ConfluentKafka.CompressionType.None
            }
        };

        using var producer = new ConfluentKafka.ProducerBuilder<string, string>(config).Build();

        var startOffset = await ConfluentStressTestHelpers.WarmUpProducerAndQueryStartOffsetAsync(
            producer,
            options,
            "Confluent idempotent producer",
            throughput,
            cancellationToken);

        var workload = await ProducerWorkload.RunAsync(
            producer, options, throughput, latency, TimeSpan.FromMinutes(options.DurationMinutes),
            awaitDelivery: false, cancellationToken).ConfigureAwait(false);

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        StressTestHelpers.LogResourceUsage("Final");

        var endOffset = await ConfluentStressTestHelpers.QueryTotalEndOffsetAfterProducerDrainAsync(
            options, startOffset, throughput.MessageCount, throughput, "Post-run drain").ConfigureAwait(false);
        var delivered = StressTestHelpers.ComputeDelivered(startOffset, endOffset, throughput);

        return new StressTestResult
        {
            Scenario = Name,
            Client = Client,
            DurationMinutes = options.DurationMinutes,
            BrokerCount = options.BrokerCount,
            MessageSizeBytes = options.MessageSizeBytes,
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Throughput = throughput.GetSnapshot(),
            DeliveredMessages = delivered,
            Idempotent = true,
            Latency = latency.GetSnapshot(),
            GcStats = workload.Gc,
            CpuTimeSeconds = throughput.CpuTimeSeconds
        };
    }
}
