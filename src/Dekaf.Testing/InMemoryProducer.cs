using System.Buffers;
using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.Testing;

internal interface IInMemoryPreparedTransaction
{
    bool IsCompleted { get; }
    bool IsPrepared { get; }
    PreparedTransactionState PreparedState { get; }

    ValueTask CompletePreparedAsync(
        IInMemoryTransactionRecoveryContext recoveryContext,
        bool committed,
        CancellationToken cancellationToken);
}

internal interface IInMemoryTransactionRecoveryContext
{
    void ThrowIfFatalTransactionError();
    ValueTask ApplyFaultAsync(KafkaFaultScope scope, CancellationToken cancellationToken);
    FatalTransactionException CaptureFatalTransactionException(FatalTransactionException exception);
}

/// <summary>
/// In-memory <see cref="IKafkaProducer{TKey,TValue}"/> backed by an <see cref="InMemoryKafkaCluster"/>.
/// </summary>
public sealed class InMemoryProducer<TKey, TValue> :
    IKafkaProducer<TKey, TValue>,
    IInMemoryTransactionRecoveryContext
{
    internal Action? TransactionCompletionPublishedTestHook;

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
    private readonly long _producerId;
    private InMemoryTransaction? _activeTransaction;
    private FatalTransactionException? _fatalTransactionException;
    private bool _preparedRecoveryEnabled;
    private bool _preparedRecoveryInProgress;
    private TaskCompletionSource? _preparedRecoveryCompletion;
    private TaskCompletionSource? _disposeCompletion;
    private bool _disposeStarted;
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
        _producerId = _cluster.AllocateProducerId();
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
        ThrowIfFatalTransactionError();

        lock (_transactionGate)
        {
            ThrowIfDisposingOrDisposed();
            if (_preparedRecoveryInProgress)
                throw new InvalidOperationException("A prepared transaction recovery is already in progress.");
            if (_activeTransaction is { IsCompleted: false })
                throw new InvalidOperationException("A transaction is already active.");

            var transaction = new InMemoryTransaction(this);
            Volatile.Write(ref _activeTransaction, transaction);
            return transaction;
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
        Volatile.Write(ref _preparedRecoveryEnabled, keepPreparedTransaction);
    }

    public async ValueTask CompletePreparedTransactionAsync(
        PreparedTransactionState preparedState,
        bool committed,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ThrowIfFatalTransactionError();

        IInMemoryPreparedTransaction transaction;
        TaskCompletionSource recoveryCompletion;
        lock (_transactionGate)
        {
            ThrowIfDisposingOrDisposed();
            if (_preparedRecoveryInProgress)
                throw new InvalidOperationException("A prepared transaction recovery is already in progress.");

            transaction = _activeTransaction!;
            if (transaction is { IsCompleted: false } &&
                (!transaction.IsPrepared || transaction.PreparedState != preparedState))
            {
                throw new InvalidOperationException(
                    "Cannot recover a prepared transaction while another transaction is active.");
            }

            if (transaction is null || !transaction.IsPrepared || transaction.PreparedState != preparedState)
            {
                if (!Volatile.Read(ref _preparedRecoveryEnabled) ||
                    _cluster.GetPreparedTransaction(preparedState) is not { } recovered)
                {
                    throw new InvalidOperationException(
                        "There is no matching active or recoverable prepared transaction.");
                }

                transaction = recovered;
            }

            if (!transaction.IsPrepared || transaction.PreparedState != preparedState)
                throw new InvalidOperationException("The prepared transaction state does not match the active transaction.");

            Volatile.Write(ref _preparedRecoveryInProgress, true);
            recoveryCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _preparedRecoveryCompletion = recoveryCompletion;
        }

        try
        {
            await transaction.CompletePreparedAsync(this, committed, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_transactionGate)
            {
                Volatile.Write(ref _preparedRecoveryInProgress, false);
                recoveryCompletion.TrySetResult();
                _preparedRecoveryCompletion = null;
            }
        }
    }

    public ITopicProducer<TKey, TValue> ForTopic(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ThrowIfDisposed();
        return new InMemoryTopicProducer(this, topic);
    }

    public ValueTask DisposeAsync()
    {
        InMemoryTransaction? transaction;
        Task? preparedRecovery;
        TaskCompletionSource disposeCompletion;
        lock (_transactionGate)
        {
            if (_disposeCompletion is not null)
                return new ValueTask(_disposeCompletion.Task);

            Volatile.Write(ref _disposeStarted, true);
            disposeCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeCompletion = disposeCompletion;
            transaction = _activeTransaction;
            preparedRecovery = _preparedRecoveryCompletion?.Task;
        }

        _ = DisposeCoreAsync(transaction, preparedRecovery, disposeCompletion);
        return new ValueTask(disposeCompletion.Task);
    }

    private async Task DisposeCoreAsync(
        InMemoryTransaction? transaction,
        Task? preparedRecovery,
        TaskCompletionSource disposeCompletion)
    {
        try
        {
            if (preparedRecovery is not null)
                await preparedRecovery.ConfigureAwait(false);

            if (transaction is { IsCompleted: false, IsPrepared: false })
                await transaction.DisposeAsync().ConfigureAwait(false);

            Volatile.Write(ref _disposed, true);
            disposeCompletion.TrySetResult();
        }
        catch (Exception exception)
        {
            disposeCompletion.TrySetException(exception);
        }
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
        if (transactionMarker is null)
            ThrowIfTransactionActive();

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

    private ValueTask<T> ObserveFatalAsync<T>(ValueTask<T> operation) =>
        operation.IsCompletedSuccessfully
            ? operation
            : ObserveFatalSlowAsync(operation);

    private async ValueTask<T> ObserveFatalSlowAsync<T>(ValueTask<T> operation)
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

    void IInMemoryTransactionRecoveryContext.ThrowIfFatalTransactionError() =>
        ThrowIfFatalTransactionError();

    ValueTask IInMemoryTransactionRecoveryContext.ApplyFaultAsync(
        KafkaFaultScope scope,
        CancellationToken cancellationToken) =>
        ApplyFaultAsync(scope, cancellationToken);

    FatalTransactionException IInMemoryTransactionRecoveryContext.CaptureFatalTransactionException(
        FatalTransactionException exception) =>
        CaptureFatalTransactionException(exception);

    private void ThrowIfFatalTransactionError()
    {
        if (Volatile.Read(ref _fatalTransactionException) is { } exception)
            throw exception;
    }

    private void CompleteTransaction(InMemoryTransaction transaction)
    {
        lock (_transactionGate)
        {
            transaction.PublishCompleted();
            Volatile.Read(ref TransactionCompletionPublishedTestHook)?.Invoke();
            if (ReferenceEquals(_activeTransaction, transaction))
                Volatile.Write(ref _activeTransaction, null);
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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);
    }

    private void ThrowIfDisposingOrDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposeStarted) || Volatile.Read(ref _disposed),
            this);
    }

    private void ThrowIfTransactionActive()
    {
        ThrowIfDisposingOrDisposed();
        if (Volatile.Read(ref _activeTransaction) is { IsCompleted: false } ||
            Volatile.Read(ref _preparedRecoveryInProgress))
        {
            throw new InvalidOperationException(
                "Cannot produce outside the active transaction; use the transaction handle.");
        }
    }

    private sealed class InMemoryTransaction :
        ITransaction<TKey, TValue>,
        IInMemoryPreparedTransaction
    {
        private const long MutationCountMask = uint.MaxValue;
        private readonly InMemoryProducer<TKey, TValue> _producer;
        private readonly object _completionGate = new();
        private readonly object _pendingOffsetsGate = new();
        private readonly Dictionary<string, PendingGroupOffsets> _pendingOffsets = new(StringComparer.Ordinal);
        private TaskCompletionSource? _completionAttempt;
        private long _lifecycle;
        private AbortableTransactionException? _abortableException;
        private bool _prepared;

        public InMemoryTransaction(InMemoryProducer<TKey, TValue> producer)
        {
            _producer = producer;
            TransactionMarker = InMemoryKafkaCluster.CreateTransactionMarker();
        }

        public InMemoryTransactionMarker TransactionMarker { get; }

        public bool IsCompleted => GetState(Volatile.Read(ref _lifecycle)) == TransactionLifecycleState.Completed;

        public bool IsPrepared => Volatile.Read(ref _prepared) ||
            GetState(Volatile.Read(ref _lifecycle)) == TransactionLifecycleState.Preparing;

        public PreparedTransactionState PreparedState => Volatile.Read(ref _prepared)
            ? new PreparedTransactionState(_producer._producerId, 0)
            : PreparedTransactionState.Empty;

        public ValueTask<RecordMetadata> ProduceAsync(
            ProducerMessage<TKey, TValue> message,
            CancellationToken cancellationToken = default)
        {
            if (!TryEnterMutation("Cannot produce", out var admissionException))
                return ValueTask.FromException<RecordMetadata>(admissionException);

            try
            {
                var operation = _producer.ProduceTransactionAsync(this, message, cancellationToken);
                if (!operation.IsCompletedSuccessfully)
                    return CompleteProduceAsync(operation);

                ExitMutation();
                return operation;
            }
            catch (AbortableTransactionException exception)
            {
                MarkAbortable(exception);
                ExitMutation();
                return FaultedProduce(exception);
            }
            catch (OperationCanceledException exception)
            {
                ExitMutation();
                return CanceledProduce(exception, cancellationToken);
            }
            catch (Exception exception)
            {
                ExitMutation();
                return FaultedProduce(exception);
            }
        }

        public ValueTask<RecordMetadata> ProduceAsync(
            string topic,
            TKey? key,
            TValue value,
            CancellationToken cancellationToken = default)
        {
            if (!TryEnterMutation("Cannot produce", out var admissionException))
                return ValueTask.FromException<RecordMetadata>(admissionException);

            try
            {
                var operation = _producer.ProduceTransactionAsync(this, topic, key, value, cancellationToken);
                if (!operation.IsCompletedSuccessfully)
                    return CompleteProduceAsync(operation);

                ExitMutation();
                return operation;
            }
            catch (AbortableTransactionException exception)
            {
                MarkAbortable(exception);
                ExitMutation();
                return FaultedProduce(exception);
            }
            catch (OperationCanceledException exception)
            {
                ExitMutation();
                return CanceledProduce(exception, cancellationToken);
            }
            catch (Exception exception)
            {
                ExitMutation();
                return FaultedProduce(exception);
            }
        }

        private static ValueTask<RecordMetadata> CanceledProduce(
            OperationCanceledException exception,
            CancellationToken cancellationToken)
        {
            var canceledToken = exception.CancellationToken.IsCancellationRequested
                ? exception.CancellationToken
                : cancellationToken.IsCancellationRequested
                    ? cancellationToken
                    : new CancellationToken(canceled: true);
            return new ValueTask<RecordMetadata>(Task.FromCanceled<RecordMetadata>(canceledToken));
        }

        private static ValueTask<RecordMetadata> FaultedProduce(Exception exception) =>
            new(Task.FromException<RecordMetadata>(exception));

        private async ValueTask<RecordMetadata> CompleteProduceAsync(
            ValueTask<RecordMetadata> operation)
        {
            try
            {
                return await operation.ConfigureAwait(false);
            }
            catch (AbortableTransactionException exception)
            {
                MarkAbortable(exception);
                throw;
            }
            finally
            {
                ExitMutation();
            }
        }

        public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            var previousState = EnterCompletion("Cannot commit transaction", allowAbortable: false);
            try
            {
                await _producer.ApplyFaultAsync(
                    new KafkaFaultScope(KafkaFaultOperation.CommitTransaction),
                    cancellationToken).ConfigureAwait(false);

                Complete(committed: true);
            }
            catch (FatalTransactionException exception)
            {
                RestoreAfterFailedCompletion(previousState);
                throw _producer.CaptureFatalTransactionException(exception);
            }
            catch (AbortableTransactionException exception)
            {
                _abortableException = exception;
                RestoreAfterFailedCompletion(TransactionLifecycleState.Abortable);
                throw;
            }
            catch
            {
                RestoreAfterFailedCompletion(previousState);
                throw;
            }
        }

        public ValueTask<PreparedTransactionState> PrepareAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TransitionToPrepared();
            try
            {
                Volatile.Write(ref _prepared, true);
                _producer._cluster.RegisterPreparedTransaction(PreparedState, this);
                Volatile.Write(ref _lifecycle, Pack(TransactionLifecycleState.Prepared));
                return ValueTask.FromResult(PreparedState);
            }
            catch
            {
                Volatile.Write(ref _prepared, false);
                Volatile.Write(ref _lifecycle, Pack(TransactionLifecycleState.Active));
                throw;
            }
        }

        public async ValueTask AbortAsync(CancellationToken cancellationToken = default)
        {
            var previousState = EnterCompletion("Cannot abort transaction", allowAbortable: true);
            try
            {
                await _producer.ApplyFaultAsync(
                    new KafkaFaultScope(KafkaFaultOperation.AbortTransaction),
                    cancellationToken).ConfigureAwait(false);

                Complete(committed: false);
            }
            catch (AbortableTransactionException exception)
            {
                _abortableException = exception;
                RestoreAfterFailedCompletion(TransactionLifecycleState.Abortable);
                throw;
            }
            catch
            {
                RestoreAfterFailedCompletion(previousState);
                throw;
            }
        }

        public async ValueTask SendOffsetsToTransactionAsync(
            IEnumerable<TopicPartitionOffset> offsets,
            string consumerGroupId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(offsets);
            ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroupId);
            var snapshot = offsets.ToArray();
            EnterMutation("Cannot send offsets to transaction");
            try
            {
                await _producer.ApplyFaultAsync(
                    new KafkaFaultScope(
                        KafkaFaultOperation.SendOffsetsToTransaction,
                        groupId: consumerGroupId),
                    cancellationToken).ConfigureAwait(false);

                StageOffsets(consumerGroupId, snapshot, metadata: null);
            }
            catch (AbortableTransactionException exception)
            {
                MarkAbortable(exception);
                throw;
            }
            finally
            {
                ExitMutation();
            }
        }

        public async ValueTask SendOffsetsToTransactionAsync(
            IEnumerable<TopicPartitionOffset> offsets,
            ConsumerGroupMetadata consumerGroupMetadata,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(offsets);
            ArgumentNullException.ThrowIfNull(consumerGroupMetadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroupMetadata.GroupId);
            var snapshot = offsets.ToArray();
            EnterMutation("Cannot send offsets to transaction");
            try
            {
                await _producer.ApplyFaultAsync(
                    new KafkaFaultScope(
                        KafkaFaultOperation.SendOffsetsToTransaction,
                        groupId: consumerGroupMetadata.GroupId),
                    cancellationToken).ConfigureAwait(false);

                StageOffsets(consumerGroupMetadata.GroupId, snapshot, consumerGroupMetadata);
            }
            catch (AbortableTransactionException exception)
            {
                MarkAbortable(exception);
                throw;
            }
            finally
            {
                ExitMutation();
            }
        }

        public async ValueTask DisposeAsync()
        {
            while (!IsCompleted && !IsPrepared && !Volatile.Read(ref _producer._disposed))
            {
                // Fault plans accept arbitrary exceptions. Disposal is best-effort and must release the
                // producer slot after any injected abort failure without a generic catch clause.
                await AbortAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                if (TryEnterDisposalCompletion(out var ownedCompletion))
                {
                    Complete(committed: false);
                    return;
                }

                if (ownedCompletion is null)
                    return;

                await ownedCompletion.ConfigureAwait(false);
            }
        }

        public async ValueTask CompletePreparedAsync(
            IInMemoryTransactionRecoveryContext recoveryContext,
            bool committed,
            CancellationToken cancellationToken)
        {
            recoveryContext.ThrowIfFatalTransactionError();
            var previousState = EnterCompletion(
                "Cannot complete prepared transaction",
                allowAbortable: !committed,
                recoveryContext);
            if (previousState != TransactionLifecycleState.Prepared &&
                previousState != TransactionLifecycleState.Abortable)
            {
                RestoreAfterFailedCompletion(previousState);
                throw new InvalidOperationException("Transaction is not prepared.");
            }

            try
            {
                await recoveryContext.ApplyFaultAsync(
                    new KafkaFaultScope(
                        committed
                            ? KafkaFaultOperation.CommitTransaction
                            : KafkaFaultOperation.AbortTransaction),
                    cancellationToken).ConfigureAwait(false);

                Complete(committed);
            }
            catch (FatalTransactionException exception)
            {
                RestoreAfterFailedCompletion(previousState);
                throw recoveryContext.CaptureFatalTransactionException(exception);
            }
            catch (AbortableTransactionException exception)
            {
                _abortableException = exception;
                RestoreAfterFailedCompletion(TransactionLifecycleState.Abortable);
                throw;
            }
            catch
            {
                RestoreAfterFailedCompletion(previousState);
                throw;
            }
        }

        private PendingGroupOffsets GetOrAddPendingOffsets(string groupId)
        {
            if (_pendingOffsets.TryGetValue(groupId, out var pending))
                return pending;

            pending = new PendingGroupOffsets();
            _pendingOffsets.Add(groupId, pending);
            return pending;
        }

        private void StageOffsets(
            string groupId,
            TopicPartitionOffset[] offsets,
            ConsumerGroupMetadata? metadata)
        {
            lock (_pendingOffsetsGate)
            {
                EnsureMutationActive("Cannot send offsets to transaction");
                var pending = GetOrAddPendingOffsets(groupId);
                if (metadata is not null)
                    pending.MetadataSnapshots.Add(metadata);
                pending.Offsets.AddRange(offsets);
            }
        }

        private void Complete(bool committed)
        {
            lock (_pendingOffsetsGate)
            {
                _producer._cluster.CompleteTransaction(
                    TransactionMarker,
                    committed,
                    _pendingOffsets.Select(static item => CreatePendingOffsets(item)),
                    PreparedState,
                    this);
                _pendingOffsets.Clear();
            }

            _producer.CompleteTransaction(this);
        }

        private void EnterMutation(string operation)
        {
            if (!TryEnterMutation(operation, out var exception))
                throw exception;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryEnterMutation(string operation, out Exception exception)
        {
            if (Volatile.Read(ref _producer._disposeStarted) || Volatile.Read(ref _producer._disposed))
            {
                exception = new ObjectDisposedException(_producer.GetType().FullName);
                return false;
            }

            if (Volatile.Read(ref _producer._fatalTransactionException) is { } fatalException)
            {
                exception = fatalException;
                return false;
            }

            while (true)
            {
                var lifecycle = Volatile.Read(ref _lifecycle);
                var state = GetState(lifecycle);
                if (state != TransactionLifecycleState.Active)
                {
                    exception = ExceptionForState(operation, state);
                    return false;
                }
                if ((uint)lifecycle == uint.MaxValue)
                {
                    exception = new InvalidOperationException(
                        $"{operation}: too many concurrent transaction operations.");
                    return false;
                }

                if (Interlocked.CompareExchange(ref _lifecycle, lifecycle + 1, lifecycle) == lifecycle)
                {
                    exception = null!;
                    return true;
                }
            }
        }

        private void ExitMutation() => Interlocked.Decrement(ref _lifecycle);

        private void TransitionToPrepared()
        {
            _producer.ThrowIfDisposingOrDisposed();
            _producer.ThrowIfFatalTransactionError();

            while (true)
            {
                var lifecycle = Volatile.Read(ref _lifecycle);
                var state = GetState(lifecycle);
                if (state != TransactionLifecycleState.Active)
                    ThrowForState("Cannot prepare transaction", state);
                if ((lifecycle & MutationCountMask) != 0)
                {
                    throw new InvalidOperationException(
                        "Cannot prepare transaction while transaction operations are in progress.");
                }

                if (Interlocked.CompareExchange(
                        ref _lifecycle,
                        Pack(TransactionLifecycleState.Preparing),
                        lifecycle) == lifecycle)
                {
                    return;
                }
            }
        }

        private void EnsureMutationActive(string operation)
        {
            var state = GetState(Volatile.Read(ref _lifecycle));
            if (state != TransactionLifecycleState.Active)
                ThrowForState(operation, state);
        }

        private TransactionLifecycleState EnterCompletion(
            string operation,
            bool allowAbortable,
            IInMemoryTransactionRecoveryContext? recoveryContext = null)
        {
            if (recoveryContext is null)
            {
                _producer.ThrowIfDisposed();
                _producer.ThrowIfFatalTransactionError();
            }
            else
            {
                recoveryContext.ThrowIfFatalTransactionError();
            }

            lock (_completionGate)
            {
                while (true)
                {
                    var lifecycle = Volatile.Read(ref _lifecycle);
                    var state = GetState(lifecycle);
                    if (state is not (TransactionLifecycleState.Active or TransactionLifecycleState.Prepared) &&
                        !(allowAbortable && state == TransactionLifecycleState.Abortable))
                    {
                        ThrowForState(operation, state);
                    }

                    if ((lifecycle & MutationCountMask) != 0)
                    {
                        throw new InvalidOperationException(
                            $"{operation} while transaction operations are in progress.");
                    }

                    if (Interlocked.CompareExchange(
                            ref _lifecycle,
                            Pack(TransactionLifecycleState.Completing),
                            lifecycle) == lifecycle)
                    {
                        _completionAttempt = new TaskCompletionSource(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        return state;
                    }
                }
            }
        }

        private void RestoreAfterFailedCompletion(TransactionLifecycleState state)
        {
            TaskCompletionSource? completion;
            lock (_completionGate)
            {
                Volatile.Write(ref _lifecycle, Pack(state));
                completion = _completionAttempt;
                _completionAttempt = null;
            }

            completion?.TrySetResult();
        }

        private bool TryEnterDisposalCompletion(out Task? ownedCompletion)
        {
            lock (_completionGate)
            {
                while (true)
                {
                    var lifecycle = Volatile.Read(ref _lifecycle);
                    var state = GetState(lifecycle);
                    if (state == TransactionLifecycleState.Completing)
                    {
                        ownedCompletion = _completionAttempt!.Task;
                        return false;
                    }

                    if (state is not (TransactionLifecycleState.Active or TransactionLifecycleState.Abortable))
                    {
                        ownedCompletion = null;
                        return false;
                    }

                    if (Interlocked.CompareExchange(
                            ref _lifecycle,
                            Pack(TransactionLifecycleState.Completing) | (lifecycle & MutationCountMask),
                            lifecycle) == lifecycle)
                    {
                        _completionAttempt = new TaskCompletionSource(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        ownedCompletion = null;
                        return true;
                    }
                }
            }
        }

        internal void PublishCompleted()
        {
            lock (_completionGate)
            {
                SetStatePreservingMutationCount(TransactionLifecycleState.Completed);
                var completion = _completionAttempt;
                _completionAttempt = null;
                completion?.TrySetResult();
            }
        }

        private void MarkAbortable(AbortableTransactionException exception)
        {
            _abortableException = exception;
            while (true)
            {
                var lifecycle = Volatile.Read(ref _lifecycle);
                if (GetState(lifecycle) != TransactionLifecycleState.Active)
                    return;

                var abortable = Pack(TransactionLifecycleState.Abortable) | (lifecycle & MutationCountMask);
                if (Interlocked.CompareExchange(ref _lifecycle, abortable, lifecycle) == lifecycle)
                    return;
            }
        }

        private void SetStatePreservingMutationCount(TransactionLifecycleState state)
        {
            while (true)
            {
                var lifecycle = Volatile.Read(ref _lifecycle);
                if (GetState(lifecycle) == state)
                    return;

                var updated = Pack(state) | (lifecycle & MutationCountMask);
                if (Interlocked.CompareExchange(ref _lifecycle, updated, lifecycle) == lifecycle)
                    return;
            }
        }

        private void ThrowForState(string operation, TransactionLifecycleState state)
            => throw ExceptionForState(operation, state);

        private Exception ExceptionForState(string operation, TransactionLifecycleState state)
        {
            if (state == TransactionLifecycleState.Abortable && _abortableException is { } exception)
                return exception;

            return new InvalidOperationException(state switch
            {
                TransactionLifecycleState.Prepared =>
                    $"{operation}: transaction is prepared; only commit or abort is permitted.",
                TransactionLifecycleState.Preparing =>
                    $"{operation}: transaction preparation is already in progress.",
                TransactionLifecycleState.Completing =>
                    $"{operation}: transaction completion is already in progress.",
                TransactionLifecycleState.Completed =>
                    $"{operation}: transaction is already completed.",
                TransactionLifecycleState.Abortable =>
                    $"{operation}: transaction has an abortable error and must be aborted.",
                _ => $"{operation}: transaction is not active."
            });
        }

        private static long Pack(TransactionLifecycleState state) => (long)state << 32;

        private static TransactionLifecycleState GetState(long lifecycle) =>
            (TransactionLifecycleState)(lifecycle >> 32);

        private static (
            string GroupId,
            IReadOnlyList<ConsumerGroupMetadata> MetadataSnapshots,
            IReadOnlyList<TopicPartitionOffset> Offsets) CreatePendingOffsets(
                KeyValuePair<string, PendingGroupOffsets> item) =>
            (item.Key, item.Value.MetadataSnapshots, item.Value.Offsets);

        private sealed class PendingGroupOffsets
        {
            public List<TopicPartitionOffset> Offsets { get; } = [];
            public List<ConsumerGroupMetadata> MetadataSnapshots { get; } = [];
        }

        private enum TransactionLifecycleState : byte
        {
            Active,
            Preparing,
            Prepared,
            Abortable,
            Completing,
            Completed
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
