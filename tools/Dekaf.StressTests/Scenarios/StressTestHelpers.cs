using System.Diagnostics;
using System.Runtime.CompilerServices;
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
            return offsets.Values.Sum(static info => info.Offset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: end offset query failed: {ex.Message}");
            return null;
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
        ThroughputTracker throughput)
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
            catch
            {
                throughput.RecordError();
            }
        }
    }

    internal static async Task RunSamplerAsync(ThroughputTracker throughput, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
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
