using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ProducerAcksAllStressTest : IStressTestScenario
{
    public string Name => "producer-acks-all";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var latency = StressTestHelpers.CreateDeliveryLatencyTracker();
        var startedAt = DateTime.UtcNow;

        var builder = Kafka.CreateProducer<string, string>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-producer-acks-all-dekaf")
            .WithIdempotence(false)
            // Must match ConfluentProducerAcksAllStressTest for an apples-to-apples comparison
            .WithAcks(Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .WithBufferMemory(StressTestHelpers.ProducerBufferMemoryBytes)
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

        var startOffset = await StressTestHelpers.WarmUpProducerAndQueryStartOffsetAsync(
            producer,
            options,
            this,
            throughput,
            latency,
            cancellationToken);

        // Warmup cycles own their error counters. Observe measurement through disposal.
        using var deliveryErrorListener = new DekafDeliveryErrorListener(throughput, options.Topic);
        var workload = await ProducerWorkload.RunAsync(
            producer, options, Client, Name, throughput, latency, TimeSpan.FromMinutes(options.DurationMinutes),
            awaitDelivery: false, cancellationToken).ConfigureAwait(false);

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        StressTestHelpers.LogResourceUsage("Final");
        var producerDiagnostics = StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options);

        Console.WriteLine($"  Disposing producer...");
        await StressTestHelpers.DisposeWithTimeoutAsync(producer, throughput);

        // Queried after dispose so all delivery attempts (including the final flush)
        // have finished — the delta is what the broker actually accepted.
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
