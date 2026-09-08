using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ProducerAsyncStressTest : IStressTestScenario
{
    public string Name => "producer-async";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var latency = new LatencyTracker();
        var startedAt = DateTime.UtcNow;

        var builder = Kafka.CreateProducer<string, string>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-producer-async-dekaf")
            .WithIdempotence(false)
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .WithConnectionsPerBroker(options.ConnectionsPerBroker)
            // Confluent uses exactly the configured connection count. Pin Dekaf too so adaptive
            // scale-up under backpressure (1 -> 3 connections/broker) cannot leak into the
            // like-for-like baseline; the multi-connection pass measures that separately.
            .WithStressConnectionPolicy(options)
            .WithDeliveryLatencyTarget(TimeSpan.FromMilliseconds(options.DeliveryLatencyTargetMs));

        _ = options.Compression switch
        {
            "lz4" => builder.UseLz4Compression(),
            "snappy" => builder.UseSnappyCompression(),
            "zstd" => builder.UseZstdCompression(),
            _ => builder
        };

        StressTestHelpers.ConfigureProducerDeliveryDiagnostics(builder, options);
        var producer = await builder.BuildAsync(cancellationToken);

        // Even though every ProduceAsync is awaited, the broker-confirmed end-offset
        // delta is still verified: a client bug that completes the delivery task
        // without the broker persisting the message would otherwise be invisible.
        var startOffset = await StressTestHelpers.WarmUpProducerAndQueryStartOffsetAsync(
            producer,
            options,
            "Dekaf async producer",
            throughput,
            cancellationToken,
            awaitDelivery: true);

        var workload = await ProducerWorkload.RunAsync(
            producer, options, throughput, latency, TimeSpan.FromMinutes(options.DurationMinutes),
            awaitDelivery: true, cancellationToken).ConfigureAwait(false);

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        StressTestHelpers.LogResourceUsage("Final");
        var producerDiagnostics = StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options);

        Console.WriteLine($"  Disposing producer...");
        await StressTestHelpers.DisposeWithTimeoutAsync(producer, throughput);

        // Queried after dispose so all delivery attempts have finished — the delta is
        // what the broker actually accepted.
        var endOffset = await StressTestHelpers.QueryTotalEndOffsetAfterProducerDrainAsync(
            options.BootstrapServers,
            options.Topic,
            options.Partitions,
            startOffset,
            throughput.MessageCount,
            throughput,
            "Post-run drain");
        var delivered = StressTestHelpers.ComputeDelivered(startOffset, endOffset, throughput);

        return new StressTestResult
        {
            Scenario = Name,
            Client = Client,
            DurationMinutes = options.DurationMinutes,
            BrokerCount = options.BrokerCount,
            MessageSizeBytes = options.MessageSizeBytes,
            DeliveryLatencyTargetMs = options.DeliveryLatencyTargetMs,
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Throughput = throughput.GetSnapshot(),
            DeliveredMessages = delivered,
            Latency = latency.GetSnapshot(),
            GcStats = workload.Gc,
            CpuTimeSeconds = throughput.CpuTimeSeconds,
            ProducerDeliveryDiagnostics = producerDiagnostics
        };
    }
}
