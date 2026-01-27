using System.Runtime.CompilerServices;

namespace Dekaf.Consumer;

/// <summary>
/// Extension methods for consuming messages with LINQ-like operators.
/// </summary>
public static class ConsumerExtensions
{
    /// <summary>
    /// Filters consumed messages based on a predicate.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="source">The source async enumerable of consume results.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of consume results that match the predicate.</returns>
    public static async IAsyncEnumerable<ConsumeResult<TKey, TValue>> Where<TKey, TValue>(
        this IAsyncEnumerable<ConsumeResult<TKey, TValue>> source,
        Func<ConsumeResult<TKey, TValue>, bool> predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Projects each consume result into a new form.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="source">The source async enumerable of consume results.</param>
    /// <param name="selector">The projection function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of projected results.</returns>
    public static async IAsyncEnumerable<TResult> Select<TKey, TValue, TResult>(
        this IAsyncEnumerable<ConsumeResult<TKey, TValue>> source,
        Func<ConsumeResult<TKey, TValue>, TResult> selector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return selector(item);
        }
    }

    /// <summary>
    /// Takes the first N consume results.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="source">The source async enumerable of consume results.</param>
    /// <param name="count">The number of results to take.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of the first N consume results.</returns>
    public static async IAsyncEnumerable<ConsumeResult<TKey, TValue>> Take<TKey, TValue>(
        this IAsyncEnumerable<ConsumeResult<TKey, TValue>> source,
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0)
        {
            yield break;
        }

        var taken = 0;
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
            if (++taken >= count)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Batches consume results into groups of the specified size.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="source">The source async enumerable of consume results.</param>
    /// <param name="batchSize">The size of each batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of batches.</returns>
    public static async IAsyncEnumerable<IReadOnlyList<ConsumeResult<TKey, TValue>>> Batch<TKey, TValue>(
        this IAsyncEnumerable<ConsumeResult<TKey, TValue>> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var batch = new List<ConsumeResult<TKey, TValue>>(batchSize);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<ConsumeResult<TKey, TValue>>(batchSize);
            }
        }

        // Yield any remaining items
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    /// <summary>
    /// Skips consume results until a predicate is satisfied, then yields all remaining results.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="source">The source async enumerable of consume results.</param>
    /// <param name="predicate">The predicate to check for each result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of consume results after the predicate is satisfied.</returns>
    public static async IAsyncEnumerable<ConsumeResult<TKey, TValue>> SkipWhile<TKey, TValue>(
        this IAsyncEnumerable<ConsumeResult<TKey, TValue>> source,
        Func<ConsumeResult<TKey, TValue>, bool> predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var skipping = true;
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (skipping && predicate(item))
            {
                continue;
            }

            skipping = false;
            yield return item;
        }
    }

    /// <summary>
    /// Takes consume results while a predicate is satisfied.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="source">The source async enumerable of consume results.</param>
    /// <param name="predicate">The predicate to check for each result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of consume results while the predicate is satisfied.</returns>
    public static async IAsyncEnumerable<ConsumeResult<TKey, TValue>> TakeWhile<TKey, TValue>(
        this IAsyncEnumerable<ConsumeResult<TKey, TValue>> source,
        Func<ConsumeResult<TKey, TValue>, bool> predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!predicate(item))
            {
                yield break;
            }

            yield return item;
        }
    }

    /// <summary>
    /// Processes each consumed message with the specified async action.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="consumer">The consumer to read from.</param>
    /// <param name="processor">The async processor function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when consumption is cancelled or the consumer is disposed.</returns>
    public static async Task ForEachAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        Func<ConsumeResult<TKey, TValue>, ValueTask> processor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(processor);

        await foreach (var result in consumer.ConsumeAsync(cancellationToken).ConfigureAwait(false))
        {
            await processor(result).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Processes each consumed message with the specified synchronous action.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="consumer">The consumer to read from.</param>
    /// <param name="processor">The processor action.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when consumption is cancelled or the consumer is disposed.</returns>
    public static async Task ForEachAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        Action<ConsumeResult<TKey, TValue>> processor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(processor);

        await foreach (var result in consumer.ConsumeAsync(cancellationToken).ConfigureAwait(false))
        {
            processor(result);
        }
    }
}
