using Dekaf.Consumer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConsumerStressTest : IStressTestScenario
{
    public string Name => "consumer";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;

        // The topic is pre-seeded by Program.SeedConsumerTopicAsync. The consumer re-reads
        // that fixed data set in a loop (seek to beginning when all partitions are drained).
        // A live feeder would compete with the consumer for CPU and cap throughput at the
        // feeder's rate, measuring the feeder instead of the consumer.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-dekaf")
            .WithGroupId($"stress-group-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            // No WithCachedStringValues(): the seeded topic repeats one identical value, so
            // Dekaf's string cache would hit 100% and skip the per-message UTF-8 decode +
            // allocation Confluent always pays — inflating both the throughput ratio and
            // the Alloc/msg comparison. The head-to-head must deserialize like-for-like.
            .ForHighThroughput()
            .BuildAsync(cancellationToken);

        consumer.Subscribe(options.Topic);

        var partitions = Enumerable.Range(0, options.Partitions)
            .Select(p => new TopicPartition(options.Topic, p))
            .ToArray();

        var endOffsets = await StressTestHelpers.QueryEndOffsetsAsync(consumer, options.Topic, options.Partitions, cancellationToken);
        var replay = new PartitionReplayTracker(endOffsets);

        Console.WriteLine($"  Consuming pre-seeded topic in a loop ({endOffsets.Sum():N0} messages per pass)");

        // GC baseline before consumer measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));
        // Dekaf-only diagnostics active inside the measured window (per-fetch MeterListener +
        // 1-minute snapshot sampler). Accepted overhead: it slightly inflates Dekaf's own
        // CPU/msg and alloc/msg vs Confluent (works against Dekaf, never for it) and the
        // fetch-path visibility has repeatedly been what made stress regressions diagnosable.
        using var consumerDiagnostics = new ConsumerFetchDiagnosticsTracker(options.Topic);
        consumerDiagnostics.Start(StressTestHelpers.CaptureConsumerDiagnostics(consumer)!);

        Console.WriteLine($"  Running Dekaf consumer stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        StressTestHelpers.LogResourceUsage("Initial");

        throughput.Start();
        using var watchdog = options.ProgressWatchdog.Track(
            throughput,
            Client,
            Name,
            captureConsumerDiagnostics: () => StressTestHelpers.CaptureConsumerDiagnostics(consumer));
        var progress = new PeriodicProgressReporter(throughput);

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);
        var resourceMonitorTask = StressTestHelpers.RunResourceMonitorAsync(cts.Token);
        var consumerDiagnosticsTask = consumerDiagnostics.RunSamplerAsync(
            () => StressTestHelpers.CaptureConsumerDiagnostics(consumer),
            cts.Token);

        try
        {
            await foreach (var record in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
            {
                throughput.RecordMessage(record.Value?.Length ?? 0);
                progress.RecordMessage();

                if (replay.RecordConsumed(record.Partition, record.Offset))
                {
                    consumer.Positions.SeekToBeginning(partitions);
                }
            }
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            // Expected — duration timer expired
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Consumer error: {ex}");
            throughput.RecordError(ex, "Consume loop");
        }

        throughput.Stop();
        gcStats.Capture();

        try { await samplerTask.ConfigureAwait(false); } catch { }
        try { await resourceMonitorTask.ConfigureAwait(false); } catch { }
        try { await consumerDiagnosticsTask.ConfigureAwait(false); } catch { }
        consumerDiagnostics.TryTakeSample(() => StressTestHelpers.CaptureConsumerDiagnostics(consumer));

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        StressTestHelpers.LogResourceUsage("Final");

        return new StressTestResult
        {
            Scenario = Name,
            Client = Client,
            DurationMinutes = options.DurationMinutes,
            BrokerCount = options.BrokerCount,
            MessageSizeBytes = options.MessageSizeBytes,
            ConsumerSeedBatchSizeBytes = options.ConsumerSeedBatchSizeBytes,
            ConsumerConnectionsPerBroker = StressTestOptions.HighThroughputConsumerConnectionsPerBroker,
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Throughput = throughput.GetSnapshot(),
            Latency = null,
            GcStats = gcStats.ToSnapshot(),
            CpuTimeSeconds = throughput.CpuTimeSeconds,
            ConsumerFetchDiagnostics = consumerDiagnostics.GetSnapshot()
        };
    }
}
