using System.Buffers;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.Testing;

/// <summary>
/// In-memory <see cref="IKafkaProducer{TKey,TValue}"/> backed by an <see cref="InMemoryKafkaCluster"/>.
/// </summary>
public sealed class InMemoryProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>
{
    private static long s_nextProducerId;
    private readonly InMemoryKafkaCluster _cluster;
    private readonly ISerializer<TKey> _keySerializer;
    private readonly ISerializer<TValue> _valueSerializer;
    // Non-null when the caller configured an IAsyncSerializer for that component. The matching
    // synchronous slot then holds a throwing placeholder, mirroring KafkaProducer: reaching it
    // means a produce path missed the asynchronous divert and must fail loudly.
    private readonly IAsyncSerializer<TKey>? _asyncKeySerializer;
    private readonly IAsyncSerializer<TValue>? _asyncValueSerializer;
    private readonly bool _hasAsyncSerializers;
    private readonly object _transactionGate = new();
    private readonly long _producerId = Interlocked.Increment(ref s_nextProducerId);
    private InMemoryTransaction? _activeTransaction;
    private FatalTransactionException? _fatalTransactionException;
    private bool _preparedRecoveryEnabled;
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
        : this(
            cluster,
            InMemorySerdeResolver.Required(keySerializer, nameof(keySerializer)),
            InMemorySerdeResolver.Required(valueSerializer, nameof(valueSerializer)),
            asyncKeySerializer: null,
            asyncValueSerializer: null)
    {
    }

    /// <summary>
    /// Creates a producer that awaits <see cref="IAsyncSerializer{T}"/> for both components.
    /// </summary>
    public InMemoryProducer(
        InMemoryKafkaCluster cluster,
        IAsyncSerializer<TKey> keySerializer,
        IAsyncSerializer<TValue> valueSerializer)
        : this(
            cluster,
            keySerializer: null,
            valueSerializer: null,
            InMemorySerdeResolver.Required(keySerializer, nameof(keySerializer)),
            InMemorySerdeResolver.Required(valueSerializer, nameof(valueSerializer)))
    {
    }

    /// <summary>
    /// Creates a producer with a synchronous key serializer and an asynchronous value serializer.
    /// </summary>
    public InMemoryProducer(
        InMemoryKafkaCluster cluster,
        ISerializer<TKey> keySerializer,
        IAsyncSerializer<TValue> valueSerializer)
        : this(
            cluster,
            InMemorySerdeResolver.Required(keySerializer, nameof(keySerializer)),
            valueSerializer: null,
            asyncKeySerializer: null,
            InMemorySerdeResolver.Required(valueSerializer, nameof(valueSerializer)))
    {
    }

    /// <summary>
    /// Creates a producer with an asynchronous key serializer and a synchronous value serializer.
    /// </summary>
    public InMemoryProducer(
        InMemoryKafkaCluster cluster,
        IAsyncSerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer)
        : this(
            cluster,
            keySerializer: null,
            InMemorySerdeResolver.Required(valueSerializer, nameof(valueSerializer)),
            InMemorySerdeResolver.Required(keySerializer, nameof(keySerializer)),
            asyncValueSerializer: null)
    {
    }

    private InMemoryProducer(
        InMemoryKafkaCluster cluster,
        ISerializer<TKey>? keySerializer,
        ISerializer<TValue>? valueSerializer,
        IAsyncSerializer<TKey>? asyncKeySerializer,
        IAsyncSerializer<TValue>? asyncValueSerializer)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _asyncKeySerializer = asyncKeySerializer;
        _asyncValueSerializer = asyncValueSerializer;
        _keySerializer = asyncKeySerializer is null
            ? keySerializer!
            : AsyncOnlySerializerPlaceholder<TKey>.Instance;
        _valueSerializer = asyncValueSerializer is null
            ? valueSerializer!
            : AsyncOnlySerializerPlaceholder<TValue>.Instance;
        _hasAsyncSerializers = asyncKeySerializer is not null || asyncValueSerializer is not null;
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
        ThrowIfFatalTransactionError();

        lock (_transactionGate)
        {
            if (_activeTransaction is { IsCompleted: false })
                throw new InvalidOperationException("A transaction is already active.");

            return _activeTransaction = new InMemoryTransaction(this);
        }
    }

    public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default) =>
        InitTransactionsAsync(keepPreparedTransaction: false, cancellationToken);

    public async ValueTask InitTransactionsAsync(bool keepPreparedTransaction, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ThrowIfFatalTransactionError();
        await ApplyFaultAsync(
            new KafkaFaultScope(KafkaFaultOperation.InitializeTransactions),
            cancellationToken).ConfigureAwait(false);
        _preparedRecoveryEnabled = keepPreparedTransaction;
    }

    public async ValueTask CompletePreparedTransactionAsync(
        PreparedTransactionState preparedState,
        bool committed,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ThrowIfFatalTransactionError();

        InMemoryTransaction? transaction;
        lock (_transactionGate)
            transaction = _activeTransaction;

        if (transaction is null || !transaction.IsPrepared || transaction.PreparedState != preparedState)
        {
            if (!_preparedRecoveryEnabled ||
                _cluster.GetPreparedTransaction(preparedState) is not InMemoryTransaction recovered)
            {
                throw new InvalidOperationException(
                    "There is no matching active or recoverable prepared transaction.");
            }

            transaction = recovered;
        }

        if (!transaction.IsPrepared || transaction.PreparedState != preparedState)
            throw new InvalidOperationException("The prepared transaction state does not match the active transaction.");

        await transaction.CompletePreparedAsync(this, committed, cancellationToken).ConfigureAwait(false);
    }

    public ITopicProducer<TKey, TValue> ForTopic(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ThrowIfDisposed();
        return new InMemoryTopicProducer(this, topic);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        InMemoryTransaction? transaction;
        lock (_transactionGate)
            transaction = _activeTransaction;

        if (transaction is { IsCompleted: false, IsPrepared: false })
            await transaction.DisposeAsync().ConfigureAwait(false);

        _disposed = true;
    }

    private ValueTask<RecordMetadata> ProduceCoreAsync(
        string topic,
        int? partition,
        TKey? key,
        TValue value,
        Headers? headers,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken) =>
        ProduceCoreAsync(
            topic,
            partition,
            key,
            value,
            headers,
            timestamp,
            KafkaFaultOperation.Produce,
            transactionMarker: null,
            cancellationToken);

    private ValueTask<RecordMetadata> ProduceCoreAsync(
        string topic,
        int? partition,
        TKey? key,
        TValue value,
        Headers? headers,
        DateTimeOffset timestamp,
        KafkaFaultOperation faultOperation,
        InMemoryTransactionMarker? transactionMarker,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ThrowIfFatalTransactionError();

        if (_hasAsyncSerializers)
        {
            return ProduceWithAsyncSerializersAsync(
                topic,
                partition,
                key,
                value,
                headers,
                timestamp,
                faultOperation,
                transactionMarker,
                cancellationToken);
        }

        var keyBytes = Serialize(_keySerializer, key, topic, SerializationComponent.Key, headers, out var isKeyNull);
        var valueBytes = Serialize(_valueSerializer, value, topic, SerializationComponent.Value, headers, out var isValueNull);

        return ObserveFatalAsync(_cluster.AppendAsync(
            topic,
            partition,
            keyBytes,
            isKeyNull,
            valueBytes,
            isValueNull,
            headers?.ToList(),
            timestamp,
            cancellationToken,
            faultOperation,
            transactionMarker));
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

    /// <summary>
    /// Produce path used when at least one component has an <see cref="IAsyncSerializer{T}"/>.
    /// Components without one still encode through their synchronous serializer, so mixed
    /// configurations behave exactly like the production producer.
    /// </summary>
    private async ValueTask<RecordMetadata> ProduceWithAsyncSerializersAsync(
        string topic,
        int? partition,
        TKey? key,
        TValue value,
        Headers? headers,
        DateTimeOffset timestamp,
        KafkaFaultOperation faultOperation,
        InMemoryTransactionMarker? transactionMarker,
        CancellationToken cancellationToken)
    {
        // Null components skip their serializer entirely, matching the synchronous path.
        var isKeyNull = key is null;
        byte[] keyBytes = isKeyNull
            ? []
            : await SerializeAsync(
                _asyncKeySerializer,
                _keySerializer,
                key!,
                topic,
                SerializationComponent.Key,
                headers,
                cancellationToken).ConfigureAwait(false);

        var isValueNull = value is null;
        byte[] valueBytes = isValueNull
            ? []
            : await SerializeAsync(
                _asyncValueSerializer,
                _valueSerializer,
                value,
                topic,
                SerializationComponent.Value,
                headers,
                cancellationToken).ConfigureAwait(false);

        return await ObserveFatalAsync(_cluster.AppendAsync(
            topic,
            partition,
            keyBytes,
            isKeyNull,
            valueBytes,
            isValueNull,
            headers?.ToList(),
            timestamp,
            cancellationToken,
            faultOperation,
            transactionMarker)).ConfigureAwait(false);
    }

    private ValueTask<RecordMetadata> ProduceTransactionAsync(
        InMemoryTransaction transaction,
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return ProduceCoreAsync(
            message.Topic,
            message.Partition,
            message.Key,
            message.Value,
            message.Headers,
            message.Timestamp ?? DateTimeOffset.UtcNow,
            KafkaFaultOperation.TransactionProduce,
            transaction.TransactionMarker,
            cancellationToken);
    }

    private ValueTask<RecordMetadata> ProduceTransactionAsync(
        InMemoryTransaction transaction,
        string topic,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken) =>
        ProduceCoreAsync(
            topic,
            partition: null,
            key,
            value,
            headers: null,
            DateTimeOffset.UtcNow,
            KafkaFaultOperation.TransactionProduce,
            transaction.TransactionMarker,
            cancellationToken);

    private async ValueTask ApplyFaultAsync(KafkaFaultScope scope, CancellationToken cancellationToken)
    {
        try
        {
            await _cluster.FaultPlan.ApplyAsync(scope, cancellationToken).ConfigureAwait(false);
        }
        catch (FatalTransactionException exception)
        {
            throw CaptureFatalTransactionException(exception);
        }
    }

    private async ValueTask<T> ObserveFatalAsync<T>(ValueTask<T> operation)
    {
        try
        {
            return await operation.ConfigureAwait(false);
        }
        catch (FatalTransactionException exception)
        {
            throw CaptureFatalTransactionException(exception);
        }
    }

    private FatalTransactionException CaptureFatalTransactionException(FatalTransactionException exception) =>
        Interlocked.CompareExchange(ref _fatalTransactionException, exception, null) ?? exception;

    private void ThrowIfFatalTransactionError()
    {
        if (Volatile.Read(ref _fatalTransactionException) is { } exception)
            throw exception;
    }

    private void CompleteTransaction(InMemoryTransaction transaction)
    {
        lock (_transactionGate)
        {
            if (ReferenceEquals(_activeTransaction, transaction))
                _activeTransaction = null;
        }
    }

    /// <summary>
    /// Encodes one non-null component, awaiting <paramref name="asyncSerializer"/> when the caller
    /// configured one and falling back to the synchronous serializer otherwise, so mixed
    /// configurations encode each component with its own serializer.
    /// </summary>
    private static async ValueTask<byte[]> SerializeAsync<T>(
        IAsyncSerializer<T>? asyncSerializer,
        ISerializer<T> serializer,
        T value,
        string topic,
        SerializationComponent component,
        Headers? headers,
        CancellationToken cancellationToken)
    {
        var writer = new ArrayBufferWriter<byte>();
        var context = new SerializationContext
        {
            Topic = topic,
            Component = component,
            Headers = headers,
            IsNull = false
        };

        if (asyncSerializer is null)
            serializer.Serialize(value, ref writer, context);
        else
            await asyncSerializer.SerializeAsync(value, writer, context, cancellationToken).ConfigureAwait(false);

        return writer.WrittenSpan.ToArray();
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

    private sealed class InMemoryTransaction : ITransaction<TKey, TValue>
    {
        private readonly InMemoryProducer<TKey, TValue> _producer;
        private readonly Dictionary<string, PendingGroupOffsets> _pendingOffsets = new(StringComparer.Ordinal);
        private bool _completed;
        private bool _prepared;

        public InMemoryTransaction(InMemoryProducer<TKey, TValue> producer)
        {
            _producer = producer;
            TransactionMarker = InMemoryKafkaCluster.CreateTransactionMarker();
        }

        public InMemoryTransactionMarker TransactionMarker { get; }

        public bool IsCompleted => _completed;

        public bool IsPrepared => _prepared;

        public PreparedTransactionState PreparedState => _prepared
            ? new PreparedTransactionState(_producer._producerId, 0)
            : PreparedTransactionState.Empty;

        public async ValueTask<RecordMetadata> ProduceAsync(
            ProducerMessage<TKey, TValue> message,
            CancellationToken cancellationToken = default)
        {
            ThrowIfCannotMutate("Cannot produce");
            return await _producer.ProduceTransactionAsync(this, message, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<RecordMetadata> ProduceAsync(
            string topic,
            TKey? key,
            TValue value,
            CancellationToken cancellationToken = default)
        {
            ThrowIfCannotMutate("Cannot produce");
            return await _producer.ProduceTransactionAsync(this, topic, key, value, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfCompleted("Cannot commit transaction");

            await _producer.ApplyFaultAsync(
                new KafkaFaultScope(KafkaFaultOperation.CommitTransaction),
                cancellationToken).ConfigureAwait(false);

            Complete(committed: true);
        }

        public ValueTask<PreparedTransactionState> PrepareAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfCannotMutate("Cannot prepare transaction");
            _prepared = true;
            _producer._cluster.RegisterPreparedTransaction(PreparedState, this);
            return ValueTask.FromResult(PreparedState);
        }

        public async ValueTask AbortAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfCompleted("Cannot abort transaction");

            await _producer.ApplyFaultAsync(
                new KafkaFaultScope(KafkaFaultOperation.AbortTransaction),
                cancellationToken).ConfigureAwait(false);

            Complete(committed: false);
        }

        public async ValueTask SendOffsetsToTransactionAsync(
            IEnumerable<TopicPartitionOffset> offsets,
            string consumerGroupId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(offsets);
            ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroupId);
            ThrowIfCannotMutate("Cannot send offsets to transaction");
            var snapshot = offsets.ToArray();

            await _producer.ApplyFaultAsync(
                new KafkaFaultScope(
                    KafkaFaultOperation.SendOffsetsToTransaction,
                    groupId: consumerGroupId),
                cancellationToken).ConfigureAwait(false);

            var pending = GetOrAddPendingOffsets(consumerGroupId);
            pending.Offsets.AddRange(snapshot);
        }

        public async ValueTask SendOffsetsToTransactionAsync(
            IEnumerable<TopicPartitionOffset> offsets,
            ConsumerGroupMetadata consumerGroupMetadata,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(offsets);
            ArgumentNullException.ThrowIfNull(consumerGroupMetadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroupMetadata.GroupId);
            ThrowIfCannotMutate("Cannot send offsets to transaction");
            var snapshot = offsets.ToArray();

            await _producer.ApplyFaultAsync(
                new KafkaFaultScope(
                    KafkaFaultOperation.SendOffsetsToTransaction,
                    groupId: consumerGroupMetadata.GroupId),
                cancellationToken).ConfigureAwait(false);

            var pending = GetOrAddPendingOffsets(consumerGroupMetadata.GroupId);
            pending.Metadata = consumerGroupMetadata;
            pending.Offsets.AddRange(snapshot);
        }

        public async ValueTask DisposeAsync()
        {
            if (_completed || _producer._disposed)
                return;

            try
            {
                await AbortAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort cleanup mirrors the production transaction adapter.
                Complete(committed: false);
            }
        }

        public async ValueTask CompletePreparedAsync(
            InMemoryProducer<TKey, TValue> recoveringProducer,
            bool committed,
            CancellationToken cancellationToken)
        {
            ThrowIfCompleted("Cannot complete prepared transaction", allowDisposedProducer: true);
            if (!_prepared)
                throw new InvalidOperationException("Transaction is not prepared.");

            await recoveringProducer.ApplyFaultAsync(
                new KafkaFaultScope(
                    committed
                        ? KafkaFaultOperation.CommitTransaction
                        : KafkaFaultOperation.AbortTransaction),
                cancellationToken).ConfigureAwait(false);

            Complete(committed);
        }

        private void ThrowIfCannotMutate(string operation)
        {
            ThrowIfCompleted(operation);
            if (_prepared)
                throw new InvalidOperationException("Transaction is prepared; only commit or abort is permitted.");
        }

        private void ThrowIfCompleted(string operation, bool allowDisposedProducer = false)
        {
            if (!allowDisposedProducer)
                _producer.ThrowIfDisposed();
            _producer.ThrowIfFatalTransactionError();
            if (_completed)
                throw new InvalidOperationException($"{operation}: transaction is already completed.");
        }

        private PendingGroupOffsets GetOrAddPendingOffsets(string groupId)
        {
            if (_pendingOffsets.TryGetValue(groupId, out var pending))
                return pending;

            pending = new PendingGroupOffsets();
            _pendingOffsets.Add(groupId, pending);
            return pending;
        }

        private void Complete(bool committed)
        {
            _producer._cluster.CompleteTransaction(
                TransactionMarker,
                committed,
                _pendingOffsets.Select(static item =>
                    (item.Key, item.Value.Metadata, (IReadOnlyList<TopicPartitionOffset>)item.Value.Offsets)),
                PreparedState,
                this);
            _pendingOffsets.Clear();
            _completed = true;
            _producer.CompleteTransaction(this);
        }

        private sealed class PendingGroupOffsets
        {
            public List<TopicPartitionOffset> Offsets { get; } = [];
            public ConsumerGroupMetadata? Metadata { get; set; }
        }
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
