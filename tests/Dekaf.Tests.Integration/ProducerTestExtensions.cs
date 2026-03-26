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
    /// the partition and its producer state tracking. Relies on the producer's built-in retry
    /// logic (DeliveryTimeoutMs) rather than external timeouts, which race with internal retries.
    /// </summary>
    public static async Task WarmUpAllPartitionsAsync(
        this IKafkaProducer<string, string> producer, string topic, int partitions)
    {
        for (var p = 0; p < partitions; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "warmup", Value = "warmup", Partition = p
            });
        }
    }
}
