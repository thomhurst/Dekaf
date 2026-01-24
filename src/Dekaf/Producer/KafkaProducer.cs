using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.Statistics;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

/// <summary>
/// Kafka producer implementation.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class KafkaProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>
{
    private readonly ProducerOptions _options;
    private readonly ISerializer<TKey> _keySerializer;
    private readonly ISerializer<TValue> _valueSerializer;
    private readonly IPartitioner _partitioner;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly RecordAccumulator _accumulator;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly ILogger<KafkaProducer<TKey, TValue>>? _logger;

    private readonly CancellationTokenSource _senderCts;
    private readonly Task _senderTask;
    private readonly Task _lingerTask;

    // Channel-based worker pool for thread-safe produce operations
    private readonly Channel<ProduceWorkItem<TKey, TValue>> _workChannel;
    private readonly Task[] _workerTasks;
    private readonly int _workerCount;

    private volatile short _produceApiVersion = -1;
    private volatile bool _disposed;

    // Statistics collection
    private readonly ProducerStatisticsCollector _statisticsCollector = new();
    private readonly StatisticsEmitter<ProducerStatistics>? _statisticsEmitter;

    // Thread-local reusable buffers for serialization to avoid per-message allocations
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_keySerializationBuffer;
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_valueSerializationBuffer;

    public KafkaProducer(
        ProducerOptions options,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options;
        _keySerializer = keySerializer;
        _valueSerializer = valueSerializer;
        _logger = loggerFactory?.CreateLogger<KafkaProducer<TKey, TValue>>();

        _partitioner = options.Partitioner switch
        {
            PartitionerType.Sticky => new StickyPartitioner(),
            PartitionerType.RoundRobin => new RoundRobinPartitioner(),
            _ => new DefaultPartitioner()
        };

        _connectionPool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions
            {
                UseTls = options.UseTls,
                TlsConfig = options.TlsConfig,
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword,
                GssapiConfig = options.GssapiConfig,
                OAuthBearerConfig = options.OAuthBearerConfig,
                OAuthBearerTokenProvider = options.OAuthBearerTokenProvider,
                SendBufferSize = options.SocketSendBufferBytes,
                ReceiveBufferSize = options.SocketReceiveBufferBytes
            },
            loggerFactory);

        _metadataManager = new MetadataManager(
            _connectionPool,
            options.BootstrapServers,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        _accumulator = new RecordAccumulator(options);
        _compressionCodecs = new CompressionCodecRegistry();

        _senderCts = new CancellationTokenSource();
        _senderTask = SenderLoopAsync(_senderCts.Token);
        _lingerTask = LingerLoopAsync(_senderCts.Token);

        // Set up worker pool for thread-safe produce operations
        _workerCount = Environment.ProcessorCount;
        _workChannel = Channel.CreateBounded<ProduceWorkItem<TKey, TValue>>(
            new BoundedChannelOptions(_workerCount * 16)
            {
                SingleReader = false,   // Multiple workers read
                SingleWriter = false,   // Multiple callers write
                FullMode = BoundedChannelFullMode.Wait
            });

        // Start worker tasks
        _workerTasks = new Task[_workerCount];
        for (var i = 0; i < _workerCount; i++)
        {
            _workerTasks[i] = ProcessWorkAsync(_senderCts.Token);
        }

        // Start statistics emitter if configured
        if (options.StatisticsInterval.HasValue &&
            options.StatisticsInterval.Value > TimeSpan.Zero &&
            options.StatisticsHandler is not null)
        {
            _statisticsEmitter = new StatisticsEmitter<ProducerStatistics>(
                options.StatisticsInterval.Value,
                CollectStatistics,
                options.StatisticsHandler);
        }
    }

    private ProducerStatistics CollectStatistics()
    {
        var (messagesProduced, messagesDelivered, messagesFailed, bytesProduced,
            requestsSent, responsesReceived, retries, avgLatencyMs) = _statisticsCollector.GetGlobalStats();

        return new ProducerStatistics
        {
            Timestamp = DateTimeOffset.UtcNow,
            MessagesProduced = messagesProduced,
            MessagesDelivered = messagesDelivered,
            MessagesFailed = messagesFailed,
            BytesProduced = bytesProduced,
            QueuedMessages = (int)(messagesProduced - messagesDelivered - messagesFailed),
            AccumulatorMemoryUsed = _accumulator.UsedMemory,
            AccumulatorMemoryLimit = _options.BufferMemory,
            RequestsSent = requestsSent,
            ResponsesReceived = responsesReceived,
            Retries = retries,
            AvgRequestLatencyMs = avgLatencyMs,
            Topics = _statisticsCollector.GetTopicStatistics()
        };
    }

    public async ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        // Create work item with completion source
        var completion = new TaskCompletionSource<RecordMetadata>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var workItem = new ProduceWorkItem<TKey, TValue>(message, completion, cancellationToken);

        // Write to channel (backpressure if full)
        await _workChannel.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);

        // Await the result
        return await completion.Task.ConfigureAwait(false);
    }

    private async Task ProcessWorkAsync(CancellationToken cancellationToken)
    {
        await foreach (var work in _workChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                // ProduceInternalAsync adds the completion to the batch
                // The batch will complete it when sent - no need to set result here
                await ProduceInternalAsync(work.Message, work.Completion, work.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (work.CancellationToken.IsCancellationRequested)
            {
                work.Completion.TrySetCanceled(work.CancellationToken);
            }
            catch (Exception ex)
            {
                work.Completion.TrySetException(ex);
            }
        }
    }

    private async ValueTask ProduceInternalAsync(
        ProducerMessage<TKey, TValue> message,
        TaskCompletionSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        // Ensure metadata is initialized
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Get topic metadata
        var topicInfo = await _metadataManager.GetTopicMetadataAsync(message.Topic, cancellationToken)
            .ConfigureAwait(false);

        if (topicInfo is null)
        {
            throw new InvalidOperationException($"Topic '{message.Topic}' not found");
        }

        if (topicInfo.PartitionCount == 0)
        {
            throw new InvalidOperationException($"Topic '{message.Topic}' has no partitions. Error code: {topicInfo.ErrorCode}");
        }

        // Serialize key and value to pooled memory (returned to pool when batch completes)
        var keyIsNull = message.Key is null;
        var key = keyIsNull ? PooledMemory.Null : SerializeKeyToPooled(message.Key!, message.Topic, message.Headers);
        var value = SerializeValueToPooled(message.Value, message.Topic, message.Headers);

        // Determine partition
        var partition = message.Partition
            ?? _partitioner.Partition(message.Topic, key.Span, keyIsNull, topicInfo.PartitionCount);

        // Get timestamp
        var timestamp = message.Timestamp ?? DateTimeOffset.UtcNow;
        var timestampMs = timestamp.ToUnixTimeMilliseconds();

        // Convert headers with minimal allocations
        IReadOnlyList<RecordHeader>? recordHeaders = null;
        RecordHeader[]? pooledHeaderArray = null;
        if (message.Headers is not null && message.Headers.Count > 0)
        {
            recordHeaders = ConvertHeaders(message.Headers, out pooledHeaderArray);
        }

        // Append to accumulator - passes completion through to batch
        // The batch will complete the TCS when sent, so we don't await anything here
        var result = await _accumulator.AppendAsync(
            new TopicPartition(message.Topic, partition),
            timestampMs,
            key,
            value,
            recordHeaders,
            pooledHeaderArray,
            completion,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            // Return pooled array before throwing to avoid resource leak
            if (pooledHeaderArray is not null)
            {
                ArrayPool<RecordHeader>.Shared.Return(pooledHeaderArray);
            }
            throw new InvalidOperationException("Failed to append record");
        }

        // Track message produced (key + value bytes)
        var messageBytes = key.Length + value.Length;
        _statisticsCollector.RecordMessageProduced(message.Topic, partition, messageBytes);

        // No await here - completion will be set by the batch when it's sent
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        return ProduceAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Key = key,
            Value = value
        }, cancellationToken);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await _accumulator.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ITransaction<TKey, TValue> BeginTransaction()
    {
        if (string.IsNullOrEmpty(_options.TransactionalId))
        {
            throw new InvalidOperationException("Producer is not transactional. Set TransactionalId in options.");
        }

        return new Transaction<TKey, TValue>(this);
    }

    public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement transaction initialization
        throw new NotImplementedException();
    }

    private async Task SenderLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var batch in _accumulator.GetReadyBatchesAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await SendBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send batch to {Topic}-{Partition}",
                    batch.TopicPartition.Topic, batch.TopicPartition.Partition);

                // Track batch failure (if not already tracked in SendBatchAsync)
                // This handles cases where the exception occurred before SendBatchAsync could track it
                _statisticsCollector.RecordBatchFailed(
                    batch.TopicPartition.Topic,
                    batch.TopicPartition.Partition,
                    batch.CompletionSources.Count);

                batch.Fail(ex);
            }
        }
    }

    private async Task LingerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.LingerMs > 0 ? _options.LingerMs : 100, cancellationToken)
                    .ConfigureAwait(false);
                await _accumulator.ExpireLingerAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in linger loop");
            }
        }
    }

    private async Task SendBatchAsync(ReadyBatch batch, CancellationToken cancellationToken)
    {
        var leader = await _metadataManager.GetPartitionLeaderAsync(
            batch.TopicPartition.Topic,
            batch.TopicPartition.Partition,
            cancellationToken).ConfigureAwait(false);

        if (leader is null)
        {
            throw new InvalidOperationException(
                $"No leader for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}");
        }

        var connection = await _connectionPool.GetConnectionAsync(leader.NodeId, cancellationToken)
            .ConfigureAwait(false);

        // Ensure API version is negotiated
        if (_produceApiVersion < 0)
        {
            _produceApiVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.Produce,
                ProduceRequest.LowestSupportedVersion,
                ProduceRequest.HighestSupportedVersion);
        }

        var request = new ProduceRequest
        {
            Acks = (short)_options.Acks,
            TimeoutMs = _options.RequestTimeoutMs,
            TransactionalId = _options.TransactionalId,
            TopicData =
            [
                new ProduceRequestTopicData
                {
                    Name = batch.TopicPartition.Topic,
                    PartitionData =
                    [
                        new ProduceRequestPartitionData
                        {
                            Index = batch.TopicPartition.Partition,
                            Records = [batch.RecordBatch],
                            Compression = _options.CompressionType
                        }
                    ]
                }
            ]
        };

        var messageCount = batch.CompletionSources.Count;
        var requestStartTime = DateTimeOffset.UtcNow;

        // Track request sent
        _statisticsCollector.RecordRequestSent();

        // Handle Acks.None (fire-and-forget) - broker doesn't send response
        if (_options.Acks == Acks.None)
        {
            await connection.SendFireAndForgetAsync<ProduceRequest, ProduceResponse>(
                request,
                _produceApiVersion,
                cancellationToken).ConfigureAwait(false);

            // Track batch delivered (fire-and-forget assumes success)
            _statisticsCollector.RecordBatchDelivered(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                messageCount);

            // Complete with synthetic metadata since we don't get a response
            // Offset is unknown (-1) for fire-and-forget
            batch.Complete(-1, DateTimeOffset.UtcNow);
            return;
        }

        var response = await connection.SendAsync<ProduceRequest, ProduceResponse>(
            request,
            _produceApiVersion,
            cancellationToken).ConfigureAwait(false);

        // Track response received with latency
        var latencyMs = (long)(DateTimeOffset.UtcNow - requestStartTime).TotalMilliseconds;
        _statisticsCollector.RecordResponseReceived(latencyMs);

        // Process response
        var topicResponse = response.Responses.FirstOrDefault(t => t.Name == batch.TopicPartition.Topic);
        var partitionResponse = topicResponse?.PartitionResponses
            .FirstOrDefault(p => p.Index == batch.TopicPartition.Partition);

        if (partitionResponse is null)
        {
            // Track batch failed
            _statisticsCollector.RecordBatchFailed(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                messageCount);
            throw new InvalidOperationException("No response for partition");
        }

        if (partitionResponse.ErrorCode != ErrorCode.None)
        {
            // Track batch failed
            _statisticsCollector.RecordBatchFailed(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                messageCount);
            throw new KafkaException(partitionResponse.ErrorCode,
                $"Produce failed: {partitionResponse.ErrorCode}");
        }

        // Track batch delivered
        _statisticsCollector.RecordBatchDelivered(
            batch.TopicPartition.Topic,
            batch.TopicPartition.Partition,
            messageCount);

        var timestamp = partitionResponse.LogAppendTimeMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(partitionResponse.LogAppendTimeMs)
            : DateTimeOffset.UtcNow;

        batch.Complete(partitionResponse.BaseOffset, timestamp);
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArrayBufferWriter<byte> GetKeySerializationBuffer()
    {
        var buffer = t_keySerializationBuffer;
        if (buffer is null)
        {
            buffer = new ArrayBufferWriter<byte>(256);
            t_keySerializationBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArrayBufferWriter<byte> GetValueSerializationBuffer()
    {
        var buffer = t_valueSerializationBuffer;
        if (buffer is null)
        {
            buffer = new ArrayBufferWriter<byte>(256);
            t_valueSerializationBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    /// <summary>
    /// Serializes the key into pooled memory that will be returned to the pool when the batch completes.
    /// </summary>
    private PooledMemory SerializeKeyToPooled(TKey key, string topic, Headers? headers)
    {
        var buffer = GetKeySerializationBuffer();
        var context = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Key,
            Headers = headers
        };
        _keySerializer.Serialize(key, buffer, context);

        // Rent from pool and copy serialized data
        var length = buffer.WrittenCount;
        var array = ArrayPool<byte>.Shared.Rent(length);
        buffer.WrittenSpan.CopyTo(array);
        return new PooledMemory(array, length);
    }

    /// <summary>
    /// Serializes the value into pooled memory that will be returned to the pool when the batch completes.
    /// </summary>
    private PooledMemory SerializeValueToPooled(TValue value, string topic, Headers? headers)
    {
        var buffer = GetValueSerializationBuffer();
        var context = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Value,
            Headers = headers
        };
        _valueSerializer.Serialize(value, buffer, context);

        // Rent from pool and copy serialized data
        var length = buffer.WrittenCount;
        var array = ArrayPool<byte>.Shared.Rent(length);
        buffer.WrittenSpan.CopyTo(array);
        return new PooledMemory(array, length);
    }

    /// <summary>
    /// Converts Headers to RecordHeaders with minimal allocations.
    /// Allocates an array directly instead of using List to avoid the List wrapper allocation.
    /// For small header counts (<=16), allocates a small array on the heap.
    /// For larger counts (>16), uses ArrayPool to avoid allocating large arrays that could pressure GC.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IReadOnlyList<RecordHeader> ConvertHeaders(Headers headers, out RecordHeader[]? pooledArray)
    {
        const int PoolingThreshold = 16;
        var count = headers.Count;
        pooledArray = null;

        RecordHeader[] result;
        if (count <= PoolingThreshold)
        {
            // Small header count: allocate array directly (one allocation vs List's two allocations)
            result = new RecordHeader[count];
        }
        else
        {
            // Large header count: rent from pool to avoid large array allocations
            result = ArrayPool<RecordHeader>.Shared.Rent(count);
            pooledArray = result; // Track for returning to pool later
        }

        var index = 0;
        foreach (var h in headers)
        {
            result[index++] = new RecordHeader
            {
                Key = h.Key,
                Value = h.Value,
                IsValueNull = h.IsValueNull
            };
        }

        // If pooled, wrap in HeaderListWrapper to expose only the valid portion
        if (pooledArray is not null)
        {
            return new HeaderListWrapper(result, count);
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Graceful shutdown: flush all pending messages to Kafka before closing.
        // CloseTimeoutMs controls how long to wait (0 = no timeout, wait indefinitely).
        var hasTimeout = _options.CloseTimeoutMs > 0;
        using var shutdownCts = hasTimeout
            ? new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.CloseTimeoutMs))
            : new CancellationTokenSource();
        var gracefulShutdown = true;

        try
        {
            // 1. Complete the work channel (no more writes accepted)
            _workChannel.Writer.Complete();

            // 2. Wait for workers to drain - they'll process remaining items and exit
            //    when the channel is empty and completed
            if (hasTimeout)
                await Task.WhenAll(_workerTasks).WaitAsync(shutdownCts.Token).ConfigureAwait(false);
            else
                await Task.WhenAll(_workerTasks).ConfigureAwait(false);

            // 3. Flush accumulator and complete its channel - sender will process remaining batches
            await _accumulator.CloseAsync(shutdownCts.Token).ConfigureAwait(false);

            // 4. Cancel linger loop (no longer needed) but let sender finish
            _senderCts.Cancel();

            // 5. Wait for sender to drain remaining batches
            if (hasTimeout)
                await _senderTask.WaitAsync(shutdownCts.Token).ConfigureAwait(false);
            else
                await _senderTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown timed out - fall back to forceful shutdown
            gracefulShutdown = false;
            _logger?.LogWarning("Graceful shutdown timed out after {Timeout}ms, forcing disposal", _options.CloseTimeoutMs);
        }
        catch
        {
            gracefulShutdown = false;
        }

        if (!gracefulShutdown)
        {
            // Forceful shutdown: cancel everything and fail pending batches
            _senderCts.Cancel();

            try
            {
                await Task.WhenAll(_workerTasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore
            }
        }

        // Wait for linger task to exit (it should be quick after cancellation)
        try
        {
            await _lingerTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch
        {
            // Ignore
        }

        _senderCts.Dispose();

        // Dispose statistics emitter
        if (_statisticsEmitter is not null)
        {
            await _statisticsEmitter.DisposeAsync().ConfigureAwait(false);
        }

        // Dispose accumulator - this will fail any remaining batches if graceful shutdown failed
        await _accumulator.DisposeAsync().ConfigureAwait(false);

        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Work item for the producer worker pool.
/// </summary>
internal readonly struct ProduceWorkItem<TKey, TValue>
{
    public readonly ProducerMessage<TKey, TValue> Message;
    public readonly TaskCompletionSource<RecordMetadata> Completion;
    public readonly CancellationToken CancellationToken;

    public ProduceWorkItem(
        ProducerMessage<TKey, TValue> message,
        TaskCompletionSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        Message = message;
        Completion = completion;
        CancellationToken = cancellationToken;
    }
}

/// <summary>
/// Transaction implementation.
/// </summary>
internal sealed class Transaction<TKey, TValue> : ITransaction<TKey, TValue>
{
    private readonly KafkaProducer<TKey, TValue> _producer;
    private bool _committed;
    private bool _aborted;

    public Transaction(KafkaProducer<TKey, TValue> producer)
    {
        _producer = producer;
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        return _producer.ProduceAsync(message, cancellationToken);
    }

    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _committed = true;
        // TODO: Implement EndTxn
        return ValueTask.CompletedTask;
    }

    public ValueTask AbortAsync(CancellationToken cancellationToken = default)
    {
        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _aborted = true;
        // TODO: Implement EndTxn
        return ValueTask.CompletedTask;
    }

    public ValueTask SendOffsetsToTransactionAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        string consumerGroupId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement TxnOffsetCommit
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        if (!_committed && !_aborted)
        {
            // Abort on dispose if not completed
            return AbortAsync();
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Zero-allocation wrapper around a pooled array that implements IReadOnlyList.
/// The array is returned to the pool when the wrapper is no longer needed.
/// This struct is used to avoid List allocations in the producer hot path.
/// </summary>
internal readonly struct HeaderListWrapper : IReadOnlyList<RecordHeader>
{
    private readonly RecordHeader[] _array;
    private readonly int _count;

    public HeaderListWrapper(RecordHeader[] array, int count)
    {
        _array = array;
        _count = count;
    }

    public RecordHeader this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _array[index];
        }
    }

    public int Count => _count;

    public Enumerator GetEnumerator() => new(_array, _count);

    IEnumerator<RecordHeader> IEnumerable<RecordHeader>.GetEnumerator() => GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<RecordHeader>
    {
        private readonly RecordHeader[] _array;
        private readonly int _count;
        private int _index;

        public Enumerator(RecordHeader[] array, int count)
        {
            _array = array;
            _count = count;
            _index = -1;
        }

        public RecordHeader Current => _array[_index];

        object System.Collections.IEnumerator.Current => Current;

        public bool MoveNext() => ++_index < _count;

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}
