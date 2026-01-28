using Dekaf.Serialization;

namespace Dekaf.Producer;

/// <summary>
/// A producer bound to a specific topic that wraps an <see cref="IKafkaProducer{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// <para>This implementation delegates all operations to the underlying producer, embedding the
/// topic in each call. When <c>ownsProducer</c> is true (created via builder), disposal disposes
/// the underlying producer. When false (created via <see cref="IKafkaProducer{TKey, TValue}.ForTopic"/>),
/// the underlying producer is left intact.</para>
/// </remarks>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
internal sealed class TopicProducer<TKey, TValue> : ITopicProducer<TKey, TValue>
{
    private readonly IKafkaProducer<TKey, TValue> _producer;
    private readonly bool _ownsProducer;
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new topic producer.
    /// </summary>
    /// <param name="producer">The underlying producer to delegate to.</param>
    /// <param name="topic">The topic to bind to.</param>
    /// <param name="ownsProducer">If true, this topic producer will dispose the underlying producer on disposal.</param>
    internal TopicProducer(IKafkaProducer<TKey, TValue> producer, string topic, bool ownsProducer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(topic);

        _producer = producer;
        Topic = topic;
        _ownsProducer = ownsProducer;
    }

    /// <inheritdoc />
    public string Topic { get; }

    /// <inheritdoc />
    public ValueTask<RecordMetadata> ProduceAsync(
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _producer.ProduceAsync(Topic, key, value, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<RecordMetadata> ProduceAsync(
        TKey? key,
        TValue value,
        Headers headers,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _producer.ProduceAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = Topic,
            Key = key,
            Value = value,
            Headers = headers
        }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<RecordMetadata> ProduceAsync(
        int partition,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _producer.ProduceAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = Topic,
            Partition = partition,
            Key = key,
            Value = value
        }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<RecordMetadata> ProduceAsync(
        TopicProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        return _producer.ProduceAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = Topic,
            Key = message.Key,
            Value = message.Value,
            Headers = message.Headers,
            Partition = message.Partition,
            Timestamp = message.Timestamp
        }, cancellationToken);
    }

    /// <inheritdoc />
    public void Send(TKey? key, TValue value)
    {
        ThrowIfDisposed();
        _producer.Send(Topic, key, value);
    }

    /// <inheritdoc />
    public void Send(TKey? key, TValue value, Headers headers)
    {
        ThrowIfDisposed();
        _producer.Send(new ProducerMessage<TKey, TValue>
        {
            Topic = Topic,
            Key = key,
            Value = value,
            Headers = headers
        });
    }

    /// <inheritdoc />
    public void Send(TKey? key, TValue value, Action<RecordMetadata, Exception?> deliveryHandler)
    {
        ThrowIfDisposed();
        _producer.Send(new ProducerMessage<TKey, TValue>
        {
            Topic = Topic,
            Key = key,
            Value = value
        }, deliveryHandler);
    }

    /// <inheritdoc />
    public Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<(TKey? Key, TValue Value)> messages,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(messages);
        return _producer.ProduceAllAsync(Topic, messages, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<TopicProducerMessage<TKey, TValue>> messages,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(messages);

        // Convert TopicProducerMessage to ProducerMessage with embedded topic
        var producerMessages = messages.Select(m => new ProducerMessage<TKey, TValue>
        {
            Topic = Topic,
            Key = m.Key,
            Value = m.Value,
            Headers = m.Headers,
            Partition = m.Partition,
            Timestamp = m.Timestamp
        });

        return await _producer.ProduceAllAsync(producerMessages, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _producer.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_ownsProducer)
        {
            await _producer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TopicProducer<TKey, TValue>));
    }
}
