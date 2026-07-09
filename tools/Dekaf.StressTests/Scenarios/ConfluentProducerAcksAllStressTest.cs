using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConfluentProducerAcksAllStressTest : IStressTestScenario
{
    public string Name => "producer-acks-all";
    public string Client => "Confluent";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var messageValue = new string('x', options.MessageSizeBytes);
        var throughput = new ThroughputTracker();
        var latency = StressTestHelpers.CreateDeliveryLatencyTracker();
        var startedAt = DateTime.UtcNow;

        var config = new ConfluentKafka.ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-producer-acks-all-confluent",
            // Must match ProducerAcksAllStressTest for an apples-to-apples comparison
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
            "Confluent acks-all producer",
            throughput);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Confluent acks-all producer stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        StressTestHelpers.LogResourceUsage("Initial");

        throughput.Start();
        var messageIndex = 0L;
        var progress = new PeriodicProgressReporter(throughput);

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);
        var resourceMonitorTask = StressTestHelpers.RunResourceMonitorAsync(cts.Token);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var message = new ConfluentKafka.Message<string, string>
                {
                    Key = StressTestHelpers.GetKey(messageIndex),
                    Value = messageValue
                };

                if (messageIndex % StressTestHelpers.LatencySampleInterval == 0)
                {
                    ConfluentStressTestHelpers.SampleDeliveryLatency(producer, options.Topic, message, latency, throughput, cts.Token, messageIndex);
                }
                else
                {
                    ConfluentStressTestHelpers.ProduceWithBackpressure(producer, options.Topic, message, null, cts.Token);
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

        ConfluentStressTestHelpers.FlushWithTimeout(producer, throughput);
        throughput.Stop();
        gcStats.Capture();

        // Queried after the final flush — and outside the measurement window, so a slow
        // query against a degraded broker can't skew elapsed/CPU stats — so the delta
        // reflects what the broker actually accepted rather than what librdkafka's local
        // queue absorbed. The drain wait absorbs follower high-watermark lag before the
        // always-fatal shortfall check.
        var endOffset = await ConfluentStressTestHelpers.QueryTotalEndOffsetAfterProducerDrainAsync(
            options,
            startOffset,
            throughput.MessageCount,
            throughput,
            "Post-run drain");

        try { await samplerTask.ConfigureAwait(false); } catch { }
        try { await resourceMonitorTask.ConfigureAwait(false); } catch { }

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        StressTestHelpers.LogResourceUsage("Final");

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
            CpuTimeSeconds = throughput.CpuTimeSeconds
        };
    }
}
