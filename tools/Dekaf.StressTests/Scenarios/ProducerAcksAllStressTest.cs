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
        var messageValue = new string('x', options.MessageSizeBytes);
        var throughput = new ThroughputTracker();
        // Fire-and-forget messages have no awaiter; the error metric is the only signal
        // that an accepted message failed delivery.
        using var deliveryErrorListener = new DekafDeliveryErrorListener(throughput);
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
            .WithConnectionsPerBroker(options.ConnectionsPerBroker);

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
            "Dekaf acks-all producer",
            throughput,
            cancellationToken);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Dekaf acks-all producer stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        StressTestHelpers.LogResourceUsage("Initial");

        throughput.Start();
        using var watchdog = options.ProgressWatchdog.Track(
            throughput,
            Client,
            Name,
            () => StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options));
        var messageIndex = 0L;
        var progress = new PeriodicProgressReporter(throughput);

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);
        var resourceMonitorTask = StressTestHelpers.RunResourceMonitorAsync(cts.Token);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                if (messageIndex % StressTestHelpers.LatencySampleInterval == 0)
                {
                    StressTestHelpers.SampleDeliveryLatency(producer, options.Topic, StressTestHelpers.GetKey(messageIndex), messageValue, latency, throughput, messageIndex);
                }
                else
                {
                    await producer.FireAsync(options.Topic, StressTestHelpers.GetKey(messageIndex), messageValue);
                }
                throughput.RecordMessage(options.MessageSizeBytes);
                messageIndex++;

                // Yield periodically to avoid starving other tasks
                if (messageIndex % 100_000 == 0)
                {
                    await Task.Yield();
                    progress.RecordMessage();
                }
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                throughput.RecordError(ex, "Produce loop", messageIndex);
            }
        }

        Console.WriteLine($"  Flushing remaining messages...");
        await StressTestHelpers.FlushWithTimeoutAsync(producer, throughput);

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
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Throughput = throughput.GetSnapshot(),
            DeliveredMessages = delivered,
            Latency = latency.GetSnapshot(),
            GcStats = gcStats.ToSnapshot(),
            CpuTimeSeconds = throughput.CpuTimeSeconds,
            ProducerDeliveryDiagnostics = producerDiagnostics
        };
    }
}
