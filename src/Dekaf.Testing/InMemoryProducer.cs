using System.Buffers;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.Testing;

/// <summary>
/// In-memory <see cref="IKafkaProducer{TKey,TValue}"/> backed by an <see cref="InMemoryKafkaCluster"/>.
/// </summary>
public sealed class InMemoryProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>
{
    private readonly InMemoryKafkaCluster _cluster;
    private readonly ISerializer<TKey> _keySerializer;
    private readonly ISerializer<TValue> _valueSerializer;
    private bool _disposed;

    public InMemoryProducer(InMemoryKafkaCluster cluster)
        : this(
            cluster,
            InMemorySerdeResolver.Serializer<TKey>(),
            InMemorySerdeResolver.Serializer<TValue>())
    {
    }

    public InMemoryProducer(
        InMemoryKafkaCluster cluster,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _keySerializer = keySerializer ?? throw new ArgumentNullException(nameof(keySerializer));
        _valueSerializer = valueSerializer ?? throw new ArgumentNullException(nameof(valueSerializer));
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfDisposed();

        return ProduceCoreAsync(
            message.Topic,
            message.Partition,
            message.Key,
            message.Value,
            message.Headers,
            message.Timestamp ?? DateTimeOffset.UtcNow,
            cancellationToken);
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return ProduceCoreAsync(topic, partition: null, key, value, headers: null, DateTimeOffset.UtcNow, cancellationToken);
    }

    public ValueTask FireAsync(ProducerMessage<TKey, TValue> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfDisposed();

        return FireAndForgetCoreAsync(
            message.Topic,
            message.Partition,
            message.Key,
            message.Value,
            message.Headers,
            message.Timestamp ?? DateTimeOffset.UtcNow);
    }

    public ValueTask FireAsync(string topic, TKey? key, TValue value)
    {
        ThrowIfDisposed();
        return FireAndForgetCoreAsync(topic, partition: null, key, value, headers: null, DateTimeOffset.UtcNow);
    }

    public async ValueTask FireAsync(
        ProducerMessage<TKey, TValue> message,
        Action<RecordMetadata, Exception?> deliveryHandler)
    {
        ArgumentNullException.ThrowIfNull(deliveryHandler);

        try
        {
            var metadata = await ProduceAsync(message).ConfigureAwait(false);
            deliveryHandler(metadata, null);
        }
        catch (Exception ex)
        {
            deliveryHandler(default, ex);
        }
    }

    public async Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<ProducerMessage<TKey, TValue>> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var result = new List<RecordMetadata>();
        foreach (var message in messages)
            result.Add(await ProduceAsync(message, cancellationToken).ConfigureAwait(false));

        return result.ToArray();
    }

    public async Task<RecordMetadata[]> ProduceAllAsync(
        string topic,
        IEnumerable<(TKey? Key, TValue Value)> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(messages);

        var result = new List<RecordMetadata>();
        foreach (var (key, value) in messages)
            result.Add(await ProduceAsync(topic, key, value, cancellationToken).ConfigureAwait(false));

        return result.ToArray();
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public ValueTask PurgeAsync(PurgeOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if ((options & PurgeOptions.All) == PurgeOptions.None)
            return ValueTask.CompletedTask;

        return ValueTask.CompletedTask;
    }

    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        ThrowIfDisposed();
    }

    public void UnregisterMetricFromSubscription(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ThrowIfDisposed();
    }

    public ITransaction<TKey, TValue> BeginTransaction()
    {
        ThrowIfDisposed();
        throw new NotSupportedException("In-memory producer transactions are not supported.");
    }

    public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public ITopicProducer<TKey, TValue> ForTopic(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ThrowIfDisposed();
        return new InMemoryTopicProducer(this, topic);
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private ValueTask<RecordMetadata> ProduceCoreAsync(
        string topic,
        int? partition,
        TKey? key,
        TValue value,
        Headers? headers,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var keyBytes = Serialize(_keySerializer, key, topic, SerializationComponent.Key, headers, out var isKeyNull);
        var valueBytes = Serialize(_valueSerializer, value, topic, SerializationComponent.Value, headers, out var isValueNull);

        return _cluster.AppendAsync(
            topic,
            partition,
            keyBytes,
            isKeyNull,
            valueBytes,
            isValueNull,
            headers?.ToList(),
            timestamp,
            cancellationToken);
    }

    private async ValueTask FireAndForgetCoreAsync(
        string topic,
        int? partition,
        TKey? key,
        TValue value,
        Headers? headers,
        DateTimeOffset timestamp)
    {
        try
        {
            await ProduceCoreAsync(topic, partition, key, value, headers, timestamp, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
            // Matches IKafkaProducer fire-and-forget delivery semantics: failures are not surfaced.
        }
    }

    private static byte[] Serialize<T>(
        ISerializer<T> serializer,
        T? value,
        string topic,
        SerializationComponent component,
        Headers? headers,
        out bool isNull)
    {
        if (value is null)
        {
            isNull = true;
            return [];
        }

        var writer = new ArrayBufferWriter<byte>();
        var context = new SerializationContext
        {
            Topic = topic,
            Component = component,
            Headers = headers,
            IsNull = false
        };

        serializer.Serialize(value, ref writer, context);
        isNull = false;
        return writer.WrittenSpan.ToArray();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class InMemoryTopicProducer : ITopicProducer<TKey, TValue>
    {
        private readonly InMemoryProducer<TKey, TValue> _producer;

        public InMemoryTopicProducer(InMemoryProducer<TKey, TValue> producer, string topic)
        {
            _producer = producer;
            Topic = topic;
        }

        public string Topic { get; }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
            _producer.InitializeAsync(cancellationToken);

        public ValueTask<RecordMetadata> ProduceAsync(
            TKey? key,
            TValue value,
            CancellationToken cancellationToken = default) =>
            _producer.ProduceAsync(Topic, key, value, cancellationToken);

        public ValueTask<RecordMetadata> ProduceAsync(
            TKey? key,
            TValue value,
            Headers headers,
            CancellationToken cancellationToken = default) =>
            _producer.ProduceAsync(
                new ProducerMessage<TKey, TValue>
                {
                    Topic = Topic,
                    Key = key,
                    Value = value,
                    Headers = headers
                },
                cancellationToken);

        public ValueTask<RecordMetadata> ProduceAsync(
            int partition,
            TKey? key,
            TValue value,
            CancellationToken cancellationToken = default) =>
            _producer.ProduceAsync(
                new ProducerMessage<TKey, TValue>
                {
                    Topic = Topic,
                    Partition = partition,
                    Key = key,
                    Value = value
                },
                cancellationToken);

        public ValueTask<RecordMetadata> ProduceAsync(
            TopicProducerMessage<TKey, TValue> message,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            return _producer.ProduceAsync(
                new ProducerMessage<TKey, TValue>
                {
                    Topic = Topic,
                    Partition = message.Partition,
                    Key = message.Key,
                    Value = message.Value,
                    Headers = message.Headers,
                    Timestamp = message.Timestamp
                },
                cancellationToken);
        }

        public ValueTask FireAsync(TKey? key, TValue value) =>
            _producer.FireAsync(Topic, key, value);

        public ValueTask FireAsync(TKey? key, TValue value, Headers headers) =>
            _producer.FireAsync(
                new ProducerMessage<TKey, TValue>
                {
                    Topic = Topic,
                    Key = key,
                    Value = value,
                    Headers = headers
                });

        public ValueTask FireAsync(
            TKey? key,
            TValue value,
            Action<RecordMetadata, Exception?> deliveryHandler) =>
            _producer.FireAsync(
                new ProducerMessage<TKey, TValue>
                {
                    Topic = Topic,
                    Key = key,
                    Value = value
                },
                deliveryHandler);

        public Task<RecordMetadata[]> ProduceAllAsync(
            IEnumerable<(TKey? Key, TValue Value)> messages,
            CancellationToken cancellationToken = default) =>
            _producer.ProduceAllAsync(Topic, messages, cancellationToken);

        public Task<RecordMetadata[]> ProduceAllAsync(
            IEnumerable<TopicProducerMessage<TKey, TValue>> messages,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(messages);

            var producerMessages = messages.Select(message => new ProducerMessage<TKey, TValue>
            {
                Topic = Topic,
                Partition = message.Partition,
                Key = message.Key,
                Value = message.Value,
                Headers = message.Headers,
                Timestamp = message.Timestamp
            });

            return _producer.ProduceAllAsync(producerMessages, cancellationToken);
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
            _producer.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
