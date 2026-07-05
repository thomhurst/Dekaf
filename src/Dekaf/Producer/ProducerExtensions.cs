using Dekaf.Serialization;

namespace Dekaf.Producer;

/// <summary>
/// Extension methods for producer convenience operations.
/// </summary>
public static class ProducerExtensions
{
    /// <summary>
    /// Produces a message with headers without creating a ProducerMessage object.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="producer">The producer to use.</param>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="headers">The message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The record metadata for the produced message.</returns>
    public static ValueTask<RecordMetadata> ProduceAsync<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer,
        string topic,
        TKey? key,
        TValue value,
        Headers headers,
        CancellationToken cancellationToken = default)
    {
        CompatibilityThrowHelpers.ThrowIfNull(producer);
        CompatibilityThrowHelpers.ThrowIfNull(topic);

        if (producer is IProducerFastPath<TKey, TValue> fastPath)
            return fastPath.ProduceAsync(topic, key, value, headers, partition: null, timestamp: null, cancellationToken);

        return producer.ProduceAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Key = key,
            Value = value,
            Headers = headers
        }, cancellationToken);
    }

    /// <summary>
    /// Produces a message to a specific partition without creating a ProducerMessage object.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="producer">The producer to use.</param>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="partition">The partition to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The record metadata for the produced message.</returns>
    public static ValueTask<RecordMetadata> ProduceAsync<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer,
        string topic,
        int partition,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        CompatibilityThrowHelpers.ThrowIfNull(producer);
        CompatibilityThrowHelpers.ThrowIfNull(topic);

        if (producer is IProducerFastPath<TKey, TValue> fastPath)
            return fastPath.ProduceAsync(topic, key, value, headers: null, partition: partition, timestamp: null, cancellationToken);

        return producer.ProduceAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Partition = partition,
            Key = key,
            Value = value
        }, cancellationToken);
    }

    /// <summary>
    /// Produces a message with headers without waiting for acknowledgment (fire-and-forget with async backpressure).
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="producer">The producer to use.</param>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="headers">The message headers.</param>
    public static ValueTask FireAsync<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer,
        string topic,
        TKey? key,
        TValue value,
        Headers headers)
    {
        CompatibilityThrowHelpers.ThrowIfNull(producer);
        CompatibilityThrowHelpers.ThrowIfNull(topic);

        return producer.FireAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Key = key,
            Value = value,
            Headers = headers
        });
    }
}
