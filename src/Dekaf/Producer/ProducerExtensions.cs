using Dekaf.Serialization;

namespace Dekaf.Producer;

/// <summary>
/// Extension methods for producer convenience operations.
/// </summary>
public static class ProducerExtensions
{
    /// <summary>
    /// Gets the latest accepted partition metadata for a topic.
    /// </summary>
    /// <remarks>
    /// Dekaf's built-in producer completes cache hits synchronously. Cache misses perform a
    /// cancellable metadata refresh bounded by the producer's configured maximum blocking time.
    /// Use an admin client when broader topic configuration or authorized-operation details are
    /// required.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// The producer does not implement <see cref="IProducerMetadata"/>.
    /// </exception>
    public static ValueTask<IReadOnlyList<ProducerPartitionMetadata>> GetPartitionsForAsync<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer,
        string topic,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(producer);

        return producer is IProducerMetadata metadata
            ? metadata.GetPartitionsForAsync(topic, cancellationToken)
            : throw new NotSupportedException(
                $"Producer type '{producer.GetType().FullName}' does not support topic partition metadata queries.");
    }

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

        if (producer is IProducerFastPath<TKey, TValue> fastPath)
        {
            return fastPath.ProduceAsync(topic, key, value, headers, partition: null, timestamp: null, cancellationToken);
        }

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

        if (producer is IProducerFastPath<TKey, TValue> fastPath)
        {
            return fastPath.ProduceAsync(topic, key, value, headers: null, partition: partition, timestamp: null, cancellationToken);
        }

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
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(topic);

        return producer.FireAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Key = key,
            Value = value,
            Headers = headers
        });
    }

    /// <summary>
    /// Produces a message with optional headers, partition and timestamp without waiting for
    /// acknowledgment (fire-and-forget with async backpressure), without creating a
    /// <see cref="ProducerMessage{TKey, TValue}"/> object.
    /// </summary>
    /// <remarks>
    /// <para>With Dekaf's built-in producer, a send on cached topic metadata completes synchronously
    /// and allocates nothing when the buffer has space. Interceptors, async serializers and a
    /// metadata miss take the same paths as the <see cref="ProducerMessage{TKey, TValue}"/> overloads.
    /// Other producer implementations receive a <see cref="ProducerMessage{TKey, TValue}"/>.</para>
    ///
    /// <para>With <paramref name="deliveryHandler"/>, this behaves like
    /// <see cref="IKafkaProducer{TKey, TValue}.FireAsync(ProducerMessage{TKey, TValue}, Action{RecordMetadata, Exception})"/>:
    /// every failure, including a buffer timeout or a transaction that cannot produce, goes to the handler.
    /// Without it, this behaves like <see cref="IKafkaProducer{TKey, TValue}.FireAsync(ProducerMessage{TKey, TValue})"/>,
    /// except that it does not start a publish activity.</para>
    /// </remarks>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="producer">The producer to use.</param>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="headers">The message headers, or null for none.</param>
    /// <param name="partition">The partition to produce to, or null to use the partitioner.</param>
    /// <param name="timestamp">The record timestamp, or null to use the current time.</param>
    /// <param name="deliveryHandler">Callback invoked when delivery completes, or null to log delivery errors.
    /// The exception parameter is null on success.</param>
    public static ValueTask FireAsync<TKey, TValue>(
        this IKafkaProducer<TKey, TValue> producer,
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        Action<RecordMetadata, Exception?>? deliveryHandler)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(topic);

        if (producer is IProducerFastPath<TKey, TValue> fastPath)
        {
            return fastPath.FireAsync(topic, key, value, headers, partition, timestamp, deliveryHandler);
        }

        var message = new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Key = key,
            Value = value,
            Headers = headers,
            Partition = partition,
            Timestamp = timestamp
        };
        return deliveryHandler is null
            ? producer.FireAsync(message)
            : producer.FireAsync(message, deliveryHandler);
    }
}
