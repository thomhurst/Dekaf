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
    /// <remarks>
    /// Use this only to permanently ignore messages. Filtered messages are already consumed, so
    /// any commit that follows (including the default periodic auto-commit) advances past them
    /// and they are never redelivered to the consumer group. Skipped messages are dropped, not
    /// held back — do not use a stateful or time-based predicate to defer processing; pause
    /// consumption or park messages on a retry topic instead.
    /// </remarks>
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
    /// Filters consumed messages and invokes an asynchronous callback for each filtered message.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="source">The source async enumerable of consume results.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="onFiltered">The callback invoked for messages that do not match the predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of consume results that match the predicate.</returns>
    /// <remarks>
    /// The callback completes before the next source message is requested. This allows manual-commit
    /// consumers to park a filtered message on a retry or dead-letter topic before processing and
    /// committing later offsets. This method does not suspend periodic auto-commit; use
    /// <see cref="OffsetCommitMode.Manual"/> when the callback must complete before any commit can
    /// advance past the filtered message. Callback failures stop enumeration.
    /// </remarks>
    public static async IAsyncEnumerable<ConsumeResult<TKey, TValue>> Where<TKey, TValue>(
        this IAsyncEnumerable<ConsumeResult<TKey, TValue>> source,
        Func<ConsumeResult<TKey, TValue>, bool> predicate,
        Func<ConsumeResult<TKey, TValue>, ValueTask> onFiltered,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(onFiltered);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                yield return item;
                continue;
            }

            await onFiltered(item).ConfigureAwait(false);
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
    /// <remarks>
    /// This method allocates a new List for each batch. This is acceptable for consumer-side
    /// processing which is not in the zero-allocation hot path (protocol serialization).
    /// </remarks>
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
    /// <remarks>
    /// The skipped prefix is consumed and dropped; once a commit advances past it the skip is
    /// permanent for the consumer group. To start from a known position without discarding
    /// messages, seek to the desired offset instead.
    /// </remarks>
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
    /// <remarks>
    /// The first message that fails the predicate is consumed but never yielded, and its offset
    /// may be committed like any skipped message — a restarted consumer resumes after it. Do not
    /// rely on the boundary message being redelivered.
    /// </remarks>
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
