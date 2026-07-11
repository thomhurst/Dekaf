using System.Diagnostics;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class SoakStressTest : IStressTestScenario
{
    private const string WarmupKey = "__dekaf_soak_warmup__";
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ConsumerCatchUpTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ProducerPacingInterval = TimeSpan.FromMilliseconds(100);
    private const double MinimumPacingRateRatio = 0.95;

    public string Name => "soak";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var messageValue = new string('x', options.MessageSizeBytes);
        var throughput = new ThroughputTracker();
        var consumerThroughput = new ThroughputTracker();
        var warmupSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var measurementActive = 0;
        var consumedMessages = 0L;

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("soak-consumer-dekaf")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithCachedStringValues()
            .BuildAsync(cancellationToken);
        consumer.Assign(
            Enumerable.Range(0, options.Partitions)
                .Select(partition => new TopicPartition(options.Topic, partition))
                .ToArray());

        using var consumerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var consumerTask = ConsumeAsync(
            consumer,
            throughput,
            warmupSeen,
            () => Volatile.Read(ref measurementActive) != 0,
            () =>
            {
                Interlocked.Increment(ref consumedMessages);
                consumerThroughput.RecordMessage(options.MessageSizeBytes);
            },
            consumerCts.Token);

        var producerBuilder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("soak-producer-dekaf")
            .WithIdempotence(true)
            .WithAcks(Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .WithBufferMemory(StressTestHelpers.ProducerBufferMemoryBytes)
            .WithConnectionsPerBroker(options.ConnectionsPerBroker);

        _ = options.Compression switch
        {
            "lz4" => producerBuilder.UseLz4Compression(),
            "snappy" => producerBuilder.UseSnappyCompression(),
            "zstd" => producerBuilder.UseZstdCompression(),
            _ => producerBuilder
        };

        StressTestHelpers.ConfigureProducerDeliveryDiagnostics(producerBuilder, options);
        var producer = await producerBuilder.BuildAsync(cancellationToken);
        var producerDisposeAttempted = false;

        try
        {
            await producer.ProduceAsync(options.Topic, WarmupKey, messageValue, cancellationToken).ConfigureAwait(false);
            await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await warmupSeen.Task.WaitAsync(WarmupTimeout, cancellationToken).ConfigureAwait(false);

            var startOffset = await StressTestHelpers.QueryTotalEndOffsetAsync(
                options.BootstrapServers,
                options.Topic,
                options.Partitions).ConfigureAwait(false);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var gcStats = new GcStats();
            using var measurementCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            measurementCts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));
            var monitor = new ResourceTrendMonitor(
                () => throughput.MessageCount,
                () => Interlocked.Read(ref consumedMessages),
                TimeSpan.FromSeconds(options.ResourceSampleIntervalSeconds));

            var startedAt = DateTime.UtcNow;
            Console.WriteLine(
                $"  Running mixed Dekaf soak for {options.DurationMinutes:N0} minutes at " +
                $"{options.SoakMessagesPerSecond:N0} msg/s...");
            Console.WriteLine(
                $"  Resource samples every {options.ResourceSampleIntervalSeconds:N0}s; " +
                $"trend warmup {options.ResourceTrendThresholds.WarmupMinutes:N0}m");

            throughput.Start();
            consumerThroughput.Start();
            Volatile.Write(ref measurementActive, 1);
            var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, measurementCts.Token);
            var monitorTask = monitor.RunAsync(measurementCts.Token);

            using var consumerProgressWatchdog = options.ProgressWatchdog.CreateSibling();
            using (var measurementWatchdog = TrackMeasurementProgress(
                       options.ProgressWatchdog,
                       throughput,
                       () => StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options),
                       () => StressTestHelpers.CaptureConsumerDiagnostics(consumer)))
            using (var consumerMeasurementWatchdog = TrackConsumerMeasurementProgress(
                       consumerProgressWatchdog,
                       consumerThroughput,
                       () => StressTestHelpers.CaptureConsumerDiagnostics(consumer)))
            {
                await RunPacedProducerAsync(
                    producer,
                    options,
                    messageValue,
                    throughput,
                    measurementCts.Token).ConfigureAwait(false);
            }

            try
            {
                await producer.FlushAsync(CancellationToken.None)
                    .AsTask()
                    .WaitAsync(FlushTimeout, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throughput.RecordError(ex, "Final producer flush");
            }

            throughput.Stop();
            gcStats.Capture();

            var acceptedMessages = throughput.MessageCount;
            var acceptedRate = throughput.GetAverageMessagesPerSecond();
            if (!MeetsMinimumPacingRate(acceptedRate, options.SoakMessagesPerSecond))
            {
                var minimumRate = options.SoakMessagesPerSecond * MinimumPacingRateRatio;
                throughput.RecordError(
                    "SoakTargetRateMiss",
                    $"Accepted rate {acceptedRate:N0} msg/s was below the required {minimumRate:N0} msg/s.",
                    "Producer pacing");
            }

            await samplerTask.ConfigureAwait(false);
            await monitorTask.ConfigureAwait(false);

            var completedAt = DateTime.UtcNow;
            var producerDiagnostics = StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options);
            producerDisposeAttempted = true;
            await StressTestHelpers.DisposeWithTimeoutAsync(producer, throughput).ConfigureAwait(false);

            var endOffset = await StressTestHelpers.QueryTotalEndOffsetAfterProducerDrainAsync(
                options.BootstrapServers,
                options.Topic,
                options.Partitions,
                startOffset,
                acceptedMessages,
                throughput,
                "Post-run drain").ConfigureAwait(false);
            var deliveredMessages = StressTestHelpers.ComputeDelivered(startOffset, endOffset, throughput);

            if (!IsBrokerDeliveryExact(acceptedMessages, deliveredMessages))
            {
                throughput.RecordError(
                    "BrokerDeliveryCountMismatch",
                    $"Mixed soak accepted {acceptedMessages:N0} messages but broker end offsets advanced by {deliveredMessages:N0}.",
                    "Broker delivery verification");
            }

            await WaitForConsumerCatchUpAsync(
                () => Interlocked.Read(ref consumedMessages),
                deliveredMessages,
                consumerTask,
                cancellationToken).ConfigureAwait(false);

            var finalConsumedMessages = Interlocked.Read(ref consumedMessages);
            if (finalConsumedMessages != deliveredMessages)
            {
                throughput.RecordError(
                    "ConsumerCountMismatch",
                    $"Mixed soak consumed {finalConsumedMessages:N0} of {deliveredMessages:N0} broker-delivered messages.",
                    "Consumer catch-up");
            }

            consumerCts.Cancel();
            await consumerTask.ConfigureAwait(false);

            var resourceTrend = monitor.GetSnapshot(options.ResourceTrendThresholds);
            PrintTrendAnalysis(resourceTrend.Analysis);

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
                DeliveredMessages = deliveredMessages,
                ConsumedMessages = finalConsumedMessages,
                Idempotent = true,
                Latency = null,
                GcStats = gcStats.ToSnapshot(),
                CpuTimeSeconds = throughput.CpuTimeSeconds,
                ProducerDeliveryDiagnostics = producerDiagnostics,
                ResourceTrend = resourceTrend
            };
        }
        finally
        {
            Volatile.Write(ref measurementActive, 0);
            consumerCts.Cancel();
            await consumerTask.ConfigureAwait(false);

            if (!producerDisposeAttempted)
            {
                await StressTestHelpers.DisposeWithTimeoutAsync(producer, throughput).ConfigureAwait(false);
            }
        }
    }

    internal IDisposable TrackMeasurementProgress(
        ProgressWatchdog watchdog,
        ThroughputTracker throughput,
        Func<ProducerDeliveryDiagnosticsSnapshot?> captureProducerDiagnostics,
        Func<ConsumerDiagnosticSnapshot?>? captureConsumerDiagnostics = null) =>
        watchdog.Track(
            throughput,
            Client,
            Name,
            captureProducerDiagnostics,
            captureConsumerDiagnostics);

    internal IDisposable TrackConsumerMeasurementProgress(
        ProgressWatchdog watchdog,
        ThroughputTracker throughput,
        Func<ConsumerDiagnosticSnapshot?> captureConsumerDiagnostics) =>
        watchdog.Track(
            throughput,
            Client,
            $"{Name}-consumer",
            captureConsumerDiagnostics: captureConsumerDiagnostics);

    internal static bool IsBrokerDeliveryExact(long acceptedMessages, long deliveredMessages) =>
        acceptedMessages == deliveredMessages;

    internal static bool MeetsMinimumPacingRate(double acceptedRate, int targetMessagesPerSecond) =>
        acceptedRate >= targetMessagesPerSecond * MinimumPacingRateRatio;

    private static async Task ConsumeAsync(
        IKafkaConsumer<string, string> consumer,
        ThroughputTracker throughput,
        TaskCompletionSource warmupSeen,
        Func<bool> measurementActive,
        Action recordConsumed,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var record in consumer.ConsumeAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(record.Key, WarmupKey, StringComparison.Ordinal))
                {
                    warmupSeen.TrySetResult();
                    continue;
                }

                if (measurementActive())
                {
                    recordConsumed();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when measurement and catch-up complete.
        }
        catch (Exception ex)
        {
            warmupSeen.TrySetException(ex);
            throughput.RecordError(ex, "Mixed soak consume loop");
        }
    }

    private static async Task RunPacedProducerAsync(
        IKafkaProducer<string, string> producer,
        StressTestOptions options,
        string value,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var attemptedMessages = 0L;
        var nextPacingTick = ProducerPacingInterval;
        var maximumBurst = Math.Max(1, options.SoakMessagesPerSecond / 2);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var delay = nextPacingTick - stopwatch.Elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                var expectedMessages = (long)(stopwatch.Elapsed.TotalSeconds * options.SoakMessagesPerSecond);
                var messagesThisTick = Math.Min(expectedMessages - attemptedMessages, maximumBurst);
                for (var i = 0L; i < messagesThisTick; i++)
                {
                    var messageIndex = attemptedMessages++;
                    try
                    {
                        await producer.FireAsync(
                            options.Topic,
                            StressTestHelpers.GetKey(messageIndex),
                            value).ConfigureAwait(false);
                        throughput.RecordMessage(options.MessageSizeBytes);
                    }
                    catch (Exception ex)
                    {
                        throughput.RecordError(ex, "Mixed soak produce loop", messageIndex);
                    }
                }

                nextPacingTick += ProducerPacingInterval;
                if (nextPacingTick < stopwatch.Elapsed - ProducerPacingInterval)
                {
                    nextPacingTick = stopwatch.Elapsed + ProducerPacingInterval;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when duration expires.
        }
    }

    private static async Task WaitForConsumerCatchUpAsync(
        Func<long> consumedCount,
        long deliveredMessages,
        Task consumerTask,
        CancellationToken cancellationToken)
    {
        var timeout = Stopwatch.StartNew();
        while (consumedCount() < deliveredMessages && timeout.Elapsed < ConsumerCatchUpTimeout)
        {
            if (consumerTask.IsCompleted)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void PrintTrendAnalysis(ResourceTrendAnalysis analysis)
    {
        Console.WriteLine(
            $"  Post-warmup slopes ({analysis.SampleCount:N0} samples): " +
            $"WorkingSet={analysis.WorkingSetSlopeMibPerHour:N2}MiB/h, " +
            $"GCHeap={analysis.GcHeapSlopeMibPerHour:N2}MiB/h, " +
            $"LOH={analysis.LohSlopeMibPerHour:N2}MiB/h, " +
            $"Produced={analysis.ProducedThroughputSlopePercentPerHour:N2}%/h, " +
            $"Consumed={analysis.ConsumedThroughputSlopePercentPerHour:N2}%/h");

        foreach (var failure in analysis.Failures)
        {
            Console.WriteLine($"  TREND FAILURE: {failure}");
        }
    }
}
