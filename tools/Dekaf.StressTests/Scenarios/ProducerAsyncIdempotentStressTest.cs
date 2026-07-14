using System.Diagnostics;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ProducerAsyncIdempotentStressTest : IStressTestScenario
{
    public string Name => "producer-async-idempotent";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var messageValue = new string('x', options.MessageSizeBytes);
        var throughput = new ThroughputTracker();
        var latency = new LatencyTracker();
        var startedAt = DateTime.UtcNow;

        var builder = Kafka.CreateProducer<string, string>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-producer-async-idempotent-dekaf")
            .WithIdempotence(true)
            .WithAcks(Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .WithConnectionsPerBroker(options.ConnectionsPerBroker)
            // Confluent uses exactly the configured connection count. Pin Dekaf too so adaptive
            // scale-up under backpressure (1 -> 3 connections/broker) cannot leak into the
            // like-for-like baseline; the multi-connection pass measures that separately.
            .WithoutAdaptiveConnections()
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
            "Dekaf async idempotent producer",
            throughput,
            cancellationToken);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Dekaf async idempotent producer stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        StressTestHelpers.LogResourceUsage("Initial");

        throughput.Start();
        using var watchdog = options.ProgressWatchdog.Track(
            throughput,
            Client,
            Name,
            () => StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options));
        var messageIndex = 0L;
        var lastStatusTime = DateTime.UtcNow;
        var lastStatusMessageCount = 0L;

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);
        var resourceMonitorTask = StressTestHelpers.RunResourceMonitorAsync(cts.Token);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var start = Stopwatch.GetTimestamp();
                await producer.ProduceAsync(options.Topic, StressTestHelpers.GetKey(messageIndex), messageValue, cts.Token).ConfigureAwait(false);
                latency.RecordTicks(Stopwatch.GetTimestamp() - start);
                throughput.RecordMessage(options.MessageSizeBytes);
                messageIndex++;

                // Report status periodically - lower threshold than fire-and-forget since throughput is lower
                if (messageIndex % 10_000 == 0)
                {
                    var now = DateTime.UtcNow;
                    if ((now - lastStatusTime).TotalSeconds >= 10)
                    {
                        var elapsedSinceLastStatus = (now - lastStatusTime).TotalSeconds;
                        var messagesSinceLastStatus = messageIndex - lastStatusMessageCount;
                        var instantaneousMsgSec = messagesSinceLastStatus / elapsedSinceLastStatus;
                        Console.WriteLine($"  [{now:HH:mm:ss}] Progress: {messageIndex:N0} messages | instant: {instantaneousMsgSec:N0} msg/sec | avg: {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
                        lastStatusTime = now;
                        lastStatusMessageCount = messageIndex;
                    }
                }
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                throughput.RecordError(ex, "ProduceAsync loop", messageIndex);
            }
        }

        throughput.Stop();
        gcStats.Capture();

        try { await samplerTask.ConfigureAwait(false); } catch { }
        try { await resourceMonitorTask.ConfigureAwait(false); } catch { }

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
            Idempotent = true,
            Latency = latency.GetSnapshot(),
            GcStats = gcStats.ToSnapshot(),
            CpuTimeSeconds = throughput.CpuTimeSeconds,
            ProducerDeliveryDiagnostics = producerDiagnostics
        };
    }
}
