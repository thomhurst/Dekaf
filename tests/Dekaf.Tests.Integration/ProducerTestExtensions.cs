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
    /// Produces a message with a bounded timeout to prevent indefinite hangs on CI.
    /// When the bootstrap connection dies from idle timeout, ProduceAsync without a
    /// CancellationToken can hang forever. This wrapper ensures a fast failure.
    /// </summary>
    public static async Task<RecordMetadata> ProduceWithTimeoutAsync<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer,
        ProducerMessage<TKey, TValue> message,
        int timeoutSeconds = 90)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        return await producer.ProduceAsync(message, cts.Token);
    }

    /// <summary>
    /// Produces a message with retry-on-timeout resilience for slow CI runners.
    /// On first timeout, retries once with a fresh timeout — the connection may have
    /// died and self-healed. Returns null only if both attempts time out.
    /// Callers should NOT retry themselves — cancellation after append still delivers
    /// the message in background, so a caller retry would produce duplicates.
    /// </summary>
    public static async Task<RecordMetadata?> TryProduceWithTimeoutAsync<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer,
        ProducerMessage<TKey, TValue> message,
        int timeoutSeconds = 90)
    {
        try
        {
            return await producer.ProduceWithTimeoutAsync(message, timeoutSeconds);
        }
        catch (OperationCanceledException)
        {
            // First attempt timed out — retry once with a fresh timeout.
            try
            {
                Console.WriteLine("  [produce] timed out, retrying...");
                return await producer.ProduceWithTimeoutAsync(message, timeoutSeconds);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("  [produce] retry also timed out, skipping");
                return null;
            }
        }
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

        // Flush to ensure all warmup messages are fully delivered before the test starts.
        // Without this, warmup batches may still be in-flight when test produces begin,
        // adding to connection load and causing receive timeouts on slow CI runners.
        await producer.FlushWithTimeoutAsync(timeoutSeconds);
    }
}
