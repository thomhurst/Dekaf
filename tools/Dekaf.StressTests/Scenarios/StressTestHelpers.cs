using System.Diagnostics;
using System.Runtime.CompilerServices;
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
