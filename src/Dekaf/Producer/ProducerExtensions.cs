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
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(topic);

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
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(topic);

        return producer.ProduceAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Partition = partition,
            Key = key,
            Value = value
        }, cancellationToken);
    }

    /// <summary>
    /// Sends a message with headers without waiting for acknowledgment (fire-and-forget).
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="producer">The producer to use.</param>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="headers">The message headers.</param>
    public static void Send<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer,
        string topic,
        TKey? key,
        TValue value,
        Headers headers)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(topic);

        producer.Send(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Key = key,
            Value = value,
            Headers = headers
        });
    }
}
