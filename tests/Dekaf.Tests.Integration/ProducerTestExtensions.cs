using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Extension methods for producer test helpers.
/// </summary>
internal static class ProducerTestExtensions
{
    /// <summary>
    /// Flushes the producer with a bounded timeout to prevent indefinite hangs on slow CI runners.
    /// </summary>
    public static async ValueTask FlushWithTimeoutAsync<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer, int timeoutSeconds = 30)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        await producer.FlushAsync(cts.Token);
    }

    /// <summary>
    /// Flushes the topic producer with a bounded timeout to prevent indefinite hangs on slow CI runners.
    /// </summary>
    public static async ValueTask FlushWithTimeoutAsync<TKey, TValue>(
        this ITopicProducer<TKey, TValue> producer, int timeoutSeconds = 30)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        await producer.FlushAsync(cts.Token);
    }

    /// <summary>
    /// Produces a warmup message to each partition to ensure the broker has fully initialized
    /// the partition and its producer state tracking. Uses a per-message timeout to fail fast
    /// if a produce hangs (e.g., due to a transient connection death on CI), and retries once.
    /// </summary>
    public static async Task WarmUpAllPartitionsAsync(
        this IKafkaProducer<string, string> producer, string topic, int partitions, int timeoutSeconds = 30)
    {
        for (var p = 0; p < partitions; p++)
        {
            var message = new ProducerMessage<string, string>
            {
                Topic = topic, Key = "warmup", Value = "warmup", Partition = p
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await producer.ProduceAsync(message, cts.Token);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                // First attempt timed out — connection may have died and self-healed.
                // Retry once with a fresh timeout.
                Console.WriteLine($"  [warmup] partition {p} timed out, retrying...");
                using var retryCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                await producer.ProduceAsync(message, retryCts.Token);
                Console.WriteLine($"  [warmup] partition {p} retry succeeded");
            }
        }
    }
}
