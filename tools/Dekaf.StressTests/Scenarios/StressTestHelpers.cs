using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Scenarios;

internal static class StressTestHelpers
{
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
    /// Yield frequency for the background producer. On low-CPU machines (4 or fewer processors),
    /// yield more frequently to avoid starving the consumer and heartbeat threads.
    /// This prevents the Dekaf background producer from dominating CPU and GC pressure,
    /// which unfairly penalizes consumer throughput measurements vs Confluent (whose native
    /// librdkafka producer has no .NET GC impact).
    ///
    /// The threshold of 4 CPUs was chosen because the stress test runs a producer, consumer,
    /// heartbeat timer, and Kafka broker concurrently - at least 4 active threads. With 4 or
    /// fewer processors there is no spare capacity, so the producer must yield aggressively.
    /// GitHub Actions runners typically have 2-4 vCPUs, making this the common CI case.
    /// </summary>
    private static readonly int BackgroundProducerYieldInterval =
        Environment.ProcessorCount <= 4 ? 1_000 : 10_000;

    /// <summary>
    /// Brief pause between yield batches on CPU-constrained machines to let the consumer
    /// and heartbeat threads make progress. On well-provisioned machines, no delay is added.
    /// </summary>
    private static readonly int BackgroundProducerThrottleMs =
        Environment.ProcessorCount <= 4 ? 10 : 0;

    internal static async Task RunBackgroundProducerAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        string messageValue,
        CancellationToken cancellationToken)
    {
        var messageIndex = 0L;
        var yieldInterval = BackgroundProducerYieldInterval;
        var throttleMs = BackgroundProducerThrottleMs;

        await Task.Yield();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await producer.FireAsync(topic, GetKey(messageIndex), messageValue).ConfigureAwait(false);
                messageIndex++;

                // Yield periodically to avoid starving other tasks.
                // On low-CPU machines, yield more frequently and add a brief pause
                // to reduce GC pressure from the .NET producer competing with the consumer.
                if (messageIndex % yieldInterval == 0)
                {
                    if (throttleMs > 0)
                        await Task.Delay(throttleMs, cancellationToken).ConfigureAwait(false);
                    else
                        await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore producer errors in the feeder
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
        var lastAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                var currentAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
                var allocRateMBps = (currentAllocatedBytes - lastAllocatedBytes) / 5.0 / (1024.0 * 1024.0);
                lastAllocatedBytes = currentAllocatedBytes;
                LogResourceUsage("Monitor", process, allocRateMBps);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal static void LogResourceUsage(string label, Process? process = null, double? allocRateMBps = null)
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

        var allocSuffix = allocRateMBps.HasValue ? $", AllocRate={allocRateMBps.Value:F1}MB/s" : "";
        Console.WriteLine($"  [{DateTime.UtcNow:HH:mm:ss}] {label} Resources: " +
            $"WorkingSet={workingSet:F1}MB, Private={privateMemory:F1}MB, GCHeap={gcHeap:F1}MB, " +
            $"Threads={threadCount}, GC=[{gen0}/{gen1}/{gen2}]{allocSuffix}");
    }
}
