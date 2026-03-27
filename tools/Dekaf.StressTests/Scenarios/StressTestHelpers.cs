using System.Runtime.CompilerServices;
using Dekaf.Consumer;
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

    internal static async Task RunBackgroundProducerAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        string messageValue,
        CancellationToken cancellationToken)
    {
        var messageIndex = 0L;

        await Task.Yield();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await producer.FireAsync(topic, GetKey(messageIndex), messageValue).ConfigureAwait(false);
                messageIndex++;

                // Yield periodically to avoid starving other tasks
                if (messageIndex % 10_000 == 0)
                {
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

    /// <summary>
    /// Runs a consume-then-retry loop that records message sizes to the throughput tracker.
    /// On transient errors, logs and retries after a brief backoff. The backoff respects
    /// <paramref name="cancellationToken"/> so it does not delay test shutdown.
    /// </summary>
    internal static async Task RunConsumeLoopAsync<TKey, TValue>(
        IKafkaConsumer<TKey, TValue> consumer,
        Func<ConsumeResult<TKey, TValue>, int> getMessageSize,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var record in consumer.ConsumeAsync(cancellationToken).ConfigureAwait(false))
                {
                    throughput.RecordMessage(getMessageSize(record));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected — duration timer expired
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Consumer error: {ex.GetType().Name}: {ex.Message}");
                throughput.RecordError();

                // Brief delay to prevent tight error loops on persistent failures.
                // Uses the cancellation token so shutdown is not delayed by the backoff.
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
