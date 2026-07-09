using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Scenarios;

internal static class StressTestHelpers
{
    /// <summary>
    /// Producer scenarios sample true end-to-end delivery latency on 1 in N messages.
    /// Fire-and-forget latency only measures buffer-append time, which says nothing
    /// about broker round-trips; sampling keeps the measurement honest without
    /// stalling the produce loop.
    /// </summary>
    internal const int LatencySampleInterval = 1000;

    /// <summary>
    /// Explicit producer buffer bound, mirrored on the Confluent side
    /// (<see cref="ConfluentStressTestHelpers.QueueBufferingMaxKbytes"/>). Without an
    /// explicit value, Dekaf auto-tunes BufferMemory to a share of total system RAM
    /// (multi-GB on CI runners), which lets fire-and-forget appends run far ahead of
    /// what the broker ever accepts and makes accepted-message counts meaningless as
    /// a throughput proxy.
    /// </summary>
    internal const ulong ProducerBufferMemoryBytes = 512UL * 1024 * 1024;

    /// <summary>
    /// Shared with <see cref="ConfluentStressTestHelpers"/> so both clients drain the
    /// same warmup target — a mismatch would corrupt one side's start-offset arithmetic
    /// and surface as a phantom shortfall on that client only.
    /// </summary>
    internal const int ProducerWarmupMessageCount = 1000;

    /// <summary>
    /// Throughput sampler tick. The stall detector in Program.CheckForFailures converts
    /// its seconds threshold using this, so changing the tick rescales nothing silently.
    /// </summary>
    internal const int SamplerIntervalSeconds = 1;

    internal static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);
    private static readonly TimeSpan ProducerEndOffsetCatchUpTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProducerEndOffsetCatchUpDelay = TimeSpan.FromMilliseconds(500);

    private static string[] CreatePreAllocatedKeys(int count)
    {
        var keys = new string[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = $"key-{i}";
        }
        return keys;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetKey(long index) => PreAllocatedKeys[index % PreAllocatedKeys.Length];

    /// <summary>
    /// Histogram for sampled delivery latency. The sample includes time queued in the
    /// client's send buffer, which under saturation is seconds deep — so the range must
    /// be much wider than the LatencyTracker default (coarser buckets keep it at ~2.4 MB).
    /// </summary>
    internal static LatencyTracker CreateDeliveryLatencyTracker() =>
        new(maxValueMs: 30_000, bucketWidthUs: 100);

    /// <summary>
    /// Queries the high watermark of each partition, giving the fixed end offsets that
    /// consumer scenarios replay against.
    /// </summary>
    internal static async Task<long[]> QueryEndOffsetsAsync<TKey, TValue>(
        IKafkaConsumer<TKey, TValue> consumer,
        string topic,
        int partitionCount,
        CancellationToken cancellationToken)
    {
        var endOffsets = new long[partitionCount];
        for (var p = 0; p < partitionCount; p++)
        {
            var watermarks = await consumer
                .QueryWatermarkOffsetsAsync(new TopicPartition(topic, p), cancellationToken)
                .ConfigureAwait(false);
            endOffsets[p] = watermarks.High;
        }
        return endOffsets;
    }

    /// <summary>
    /// Sums the end offsets across all partitions of <paramref name="topic"/> via a
    /// short-lived Dekaf admin client (one batched ListOffsets request, no consumer
    /// group). Producer scenarios snapshot this before and after the run: the delta is
    /// the broker-confirmed delivered count, independent of how many appends the client
    /// accepted into its local buffer. Returns null on failure (e.g. broker unresponsive
    /// after the run); <see cref="ComputeDelivered"/> escalates that to a scenario
    /// failure so a dead broker can never masquerade as a throughput result.
    /// </summary>
    internal static async Task<long?> QueryTotalEndOffsetAsync(string bootstrapServers, string topic, int partitionCount)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var adminClient = Kafka.CreateAdminClient()
                .WithBootstrapServers(bootstrapServers)
                .WithClientId("stress-watermark-query")
                .Build();

            var specs = Enumerable.Range(0, partitionCount)
                .Select(p => new TopicPartitionOffsetSpec
                {
                    TopicPartition = new TopicPartition(topic, p),
                    Spec = OffsetSpec.Latest
                });

            var offsets = await adminClient.ListOffsetsAsync(specs, cancellationToken: cts.Token).ConfigureAwait(false);
            if (offsets.Count != partitionCount)
            {
                throw new InvalidOperationException(
                    $"end offset query returned {offsets.Count:N0} of {partitionCount:N0} partitions");
            }

            return offsets.Values.Sum(static info => info.Offset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: end offset query failed: {ex.Message}");
            return null;
        }
    }

    internal static async Task<long?> WarmUpProducerAndQueryStartOffsetAsync(
        IKafkaProducer<string, string> producer,
        StressTestOptions options,
        string producerName,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        var warmupStartOffset = await QueryTotalEndOffsetAsync(
            options.BootstrapServers,
            options.Topic,
            options.Partitions).ConfigureAwait(false);

        Console.WriteLine($"  Warming up {producerName}...");
        // First produce uses ProduceAsync to prime the topic metadata cache asynchronously.
        // Send() would block the thread via FetchTopicMetadataSync (.GetAwaiter().GetResult())
        // which hangs if the broker is slow to respond to new topic metadata requests.
        await producer.ProduceAsync(options.Topic, "warmup", "warmup", cancellationToken).ConfigureAwait(false);
        for (var i = 1; i < ProducerWarmupMessageCount; i++)
        {
            await producer.FireAsync(options.Topic, "warmup", "warmup").ConfigureAwait(false);
        }

        await producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        // A stale start snapshot is a correctness hazard, not just noise: warmup messages
        // landing after the snapshot would inflate the run's delivered count and mask real
        // loss, so a catch-up timeout here is recorded as an error via the tracker.
        return await QueryTotalEndOffsetAfterProducerDrainAsync(
            options.BootstrapServers,
            options.Topic,
            options.Partitions,
            warmupStartOffset,
            ProducerWarmupMessageCount,
            throughput,
            "Warmup drain").ConfigureAwait(false);
    }

    /// <summary>
    /// Producer flush waits for broker responses, but replicated topic watermarks can
    /// trail leader-ack writes briefly. Wait for the post-run watermark snapshot to
    /// reach the accepted-message target before returning the final snapshot.
    /// </summary>
    internal static Task<long?> QueryTotalEndOffsetAfterProducerDrainAsync(
        string bootstrapServers,
        string topic,
        int partitionCount,
        long? startOffset,
        long acceptedMessages,
        ThroughputTracker throughput,
        string operation)
        => WaitForEndOffsetCatchUpAsync(
            () => QueryTotalEndOffsetAsync(bootstrapServers, topic, partitionCount),
            startOffset,
            acceptedMessages,
            throughput,
            operation);

    /// <summary>
    /// Client-agnostic catch-up loop shared by the Dekaf and Confluent scenarios; each
    /// side supplies its own watermark query so Confluent runs stay Dekaf-free. A catch-up
    /// timeout is recorded as an error on <paramref name="throughput"/> because a lagging
    /// snapshot either masks loss (warmup) or is loss (post-run).
    /// </summary>
    internal static async Task<long?> WaitForEndOffsetCatchUpAsync(
        Func<Task<long?>> queryTotalEndOffset,
        long? startOffset,
        long acceptedMessages,
        ThroughputTracker throughput,
        string operation)
    {
        if (startOffset is not { } start || acceptedMessages <= 0)
        {
            return await queryTotalEndOffset().ConfigureAwait(false);
        }

        var expectedEndOffset = start + acceptedMessages;
        var deadline = DateTime.UtcNow + ProducerEndOffsetCatchUpTimeout;
        var loggedWait = false;

        while (true)
        {
            var endOffset = await queryTotalEndOffset().ConfigureAwait(false);
            if (endOffset is null || endOffset >= expectedEndOffset)
            {
                if (loggedWait && endOffset is not null)
                {
                    Console.WriteLine($"  End offsets caught up: observed {endOffset:N0}, target {expectedEndOffset:N0}");
                }

                return endOffset;
            }

            var lag = expectedEndOffset - endOffset.Value;
            if (DateTime.UtcNow >= deadline)
            {
                var message =
                    $"end offsets still lag accepted messages after " +
                    $"{ProducerEndOffsetCatchUpTimeout.TotalSeconds:N0}s: observed {endOffset:N0}, " +
                    $"target {expectedEndOffset:N0}, lag {lag:N0}";
                Console.WriteLine($"  Error: {message}");
                throughput.RecordError("EndOffsetCatchUpTimeout", message, operation);
                return endOffset;
            }

            if (!loggedWait)
            {
                Console.WriteLine(
                    $"  Waiting for broker end offsets to catch up: observed {endOffset:N0}, " +
                    $"target {expectedEndOffset:N0}, lag {lag:N0}");
                loggedWait = true;
            }

            await Task.Delay(ProducerEndOffsetCatchUpDelay, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Flushes the producer, recording a timeout as an error: a flush that cannot drain
    /// in 30 seconds against a healthy broker means the producer is stuck, and any
    /// still-buffered messages will surface as undelivered loss.
    /// </summary>
    internal static async Task FlushWithTimeoutAsync<TKey, TValue>(
        IKafkaProducer<TKey, TValue> producer,
        ThroughputTracker throughput)
    {
        try
        {
            await producer.FlushAsync(CancellationToken.None).AsTask()
                .WaitAsync(OperationTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            var message = $"Flush did not complete within {OperationTimeout.TotalSeconds:N0} seconds";
            Console.WriteLine($"  Error: {message}");
            throughput.RecordError("FlushTimeout", message, "FlushAsync");
        }
    }

    /// <summary>
    /// Disposes the producer, recording a timeout as an error — a hung dispose is a
    /// shutdown bug even when every message was already delivered.
    /// </summary>
    internal static Task DisposeWithTimeoutAsync(
        IKafkaProducer<string, string> producer,
        ThroughputTracker throughput) =>
        DisposeWithTimeoutAsync(producer, throughput, OperationTimeout);

    internal static async Task DisposeWithTimeoutAsync(
        IKafkaProducer<string, string> producer,
        ThroughputTracker throughput,
        TimeSpan timeout)
    {
        try
        {
            await producer.DisposeAsync().AsTask()
                .WaitAsync(timeout, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"  Producer disposed successfully");
        }
        catch (TimeoutException)
        {
            var message = $"Dispose did not complete within {timeout.TotalSeconds:N0} seconds";
            Console.WriteLine($"  Error: {message}");
            throughput.RecordError("DisposeTimeout", message, "DisposeAsync");
        }
    }

    /// <summary>
    /// Computes the delivered-message count from before/after end-offset snapshots and
    /// logs it against the client-accepted count so the gap (buffered-but-never-delivered
    /// messages) is visible in run output. Throws when either snapshot is missing: that
    /// means the broker was unreachable, the producer result is meaningless, and the
    /// scenario must fail loudly instead of letting reporting fall back to the
    /// client-side append rate (which once published a dead broker's buffer churn as
    /// headline throughput).
    /// </summary>
    internal static long ComputeDelivered(long? startOffset, long? endOffset, ThroughputTracker throughput)
    {
        if (startOffset is not { } start || endOffset is not { } end)
        {
            throw new InvalidOperationException(
                "Broker-confirmed delivered count unavailable (end-offset query failed — broker likely " +
                "unhealthy or dead). Failing the scenario rather than reporting the client-side append rate.");
        }

        var delivered = end - start;
        var elapsedSeconds = throughput.Elapsed.TotalSeconds;
        var accepted = throughput.MessageCount;
        var rate = elapsedSeconds > 0 ? delivered / elapsedSeconds : 0;
        Console.WriteLine($"  Delivered (broker-confirmed): {delivered:N0} messages, {rate:N0} msg/sec " +
            $"({(accepted > 0 ? delivered * 100.0 / accepted : 0):F1}% of {accepted:N0} accepted)");
        return delivered;
    }

    internal static void ConfigureProducerDeliveryDiagnostics(
        ProducerBuilder<string, string> builder,
        StressTestOptions options)
    {
        if (options.EnableProducerDeliveryDiagnostics)
            builder.WithDeliveryDiagnostics();
    }

    internal static ProducerDeliveryDiagnosticsSnapshot? CaptureProducerDeliveryDiagnostics(
        IKafkaProducer<string, string> producer,
        StressTestOptions options)
    {
        if (!options.EnableProducerDeliveryDiagnostics)
            return null;

        if (producer is not IProducerDiagnostics diagnostics)
            return null;

        var snapshot = diagnostics.GetDeliveryDiagnosticsSnapshot();
        if (snapshot.Batches.Count > 0)
        {
            Console.WriteLine($"  Captured producer delivery diagnostics: " +
                $"inFlight={snapshot.InFlightBatchCount:N0}, batches={snapshot.Batches.Count:N0}");
        }

        return snapshot;
    }

    /// <summary>
    /// Produces one message via ProduceAsync and records the full delivery round-trip
    /// into <paramref name="latency"/> when it completes. The append (including
    /// BufferMemory backpressure) runs synchronously like FireAsync; only the wait
    /// for the broker acknowledgment is fire-and-forget, so the produce loop is not
    /// stalled by the delivery round-trip.
    /// </summary>
    internal static void SampleDeliveryLatency(
        IKafkaProducer<string, string> producer,
        string topic,
        string key,
        string value,
        LatencyTracker latency,
        ThroughputTracker throughput,
        long? messageIndex = null)
    {
        _ = SampleAsync();

        async Task SampleAsync()
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                await producer.ProduceAsync(topic, key, value).ConfigureAwait(false);
                latency.RecordTicks(Stopwatch.GetTimestamp() - start);
            }
            catch (Exception ex)
            {
                // Detail only: the failure count is already captured via the producer's
                // messaging.client.sent.errors metric (DekafDeliveryErrorListener), and
                // this message was accepted into MessageCount before delivery failed.
                throughput.RecordDeliveryErrorDetail(ex, "SampleDeliveryLatency", messageIndex);
            }
        }
    }

    internal static async Task RunSamplerAsync(ThroughputTracker throughput, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(SamplerIntervalSeconds), cancellationToken).ConfigureAwait(false);
                throughput.TakeSample();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal static async Task RunResourceMonitorAsync(CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                LogResourceUsage("Monitor", process);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal static void LogResourceUsage(string label, Process? process = null)
    {
        process ??= Process.GetCurrentProcess();
        // Refresh is essential: Process caches metrics at creation time, so without
        // this call the properties below would return stale values.
        process.Refresh();

        var workingSet = process.WorkingSet64 / (1024.0 * 1024.0);
        var privateMemory = process.PrivateMemorySize64 / (1024.0 * 1024.0);
        var gcHeap = GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0);
        var threadCount = process.Threads.Count;
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);

        Console.WriteLine($"  [{DateTime.UtcNow:HH:mm:ss}] {label} Resources: " +
            $"WorkingSet={workingSet:F1}MB, Private={privateMemory:F1}MB, GCHeap={gcHeap:F1}MB, " +
            $"Threads={threadCount}, GC=[{gen0}/{gen1}/{gen2}]");
    }
}
