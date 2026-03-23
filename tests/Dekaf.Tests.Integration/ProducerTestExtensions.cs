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
        this IKafkaProducer<TKey, TValue> producer, int seconds = 30)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        await producer.FlushAsync(cts.Token);
    }

    /// <summary>
    /// Flushes the topic producer with a bounded timeout to prevent indefinite hangs on slow CI runners.
    /// </summary>
    public static async ValueTask FlushWithTimeoutAsync<TKey, TValue>(
        this ITopicProducer<TKey, TValue> producer, int seconds = 30)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        await producer.FlushAsync(cts.Token);
    }
}
