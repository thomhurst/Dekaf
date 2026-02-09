using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Statistics;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

/// <summary>
/// Per-broker sender that serializes all writes to a single broker connection.
/// A single-threaded send loop drains batches from a channel, coalesces them into
/// ProduceRequests, and sends them via pipelined writes. This guarantees wire-order
/// for same-partition batches and enables N in-flight batches per partition for
/// idempotent producers.
///
/// All writes go through the send loop — there are no out-of-loop write paths.
/// Retries re-enqueue to the channel after backoff, and leader changes reroute
/// to the correct BrokerSender via the rerouteBatch callback.
/// </summary>
internal sealed class BrokerSender : IAsyncDisposable
{
    private readonly int _brokerId;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly RecordAccumulator _accumulator;
    private readonly ProducerOptions _options;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly PartitionInflightTracker? _inflightTracker;
    private readonly ProducerStatisticsCollector _statisticsCollector;
    private readonly ConcurrentDictionary<TopicPartition, SemaphoreSlim> _partitionSendGates;
    private readonly Func<SemaphoreSlim> _createPartitionGate;
    private readonly Action<ReadyBatch>? _rerouteBatch;
    private readonly Action<TopicPartition, long, DateTimeOffset, int, Exception?>? _onAcknowledgement;
    private readonly ILogger? _logger;

    private readonly Channel<ReadyBatch> _batchChannel;
    private readonly Task _sendLoopTask;
    private readonly CancellationTokenSource _cts;
    private readonly SemaphoreSlim _inFlightSemaphore;
    private readonly ConcurrentDictionary<Task, byte> _inFlightResponses = new();

    // Per-producer shared API version (read via volatile, written via Interlocked)
    private readonly Func<int> _getProduceApiVersion;
    private readonly Action<int> _setProduceApiVersion;

    // Transaction support
    private readonly Func<bool> _isTransactional;
    private readonly Func<TopicPartition, CancellationToken, ValueTask>? _ensurePartitionInTransaction;

    private volatile bool _disposed;

    public BrokerSender(
        int brokerId,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        RecordAccumulator accumulator,
        ProducerOptions options,
        CompressionCodecRegistry compressionCodecs,
        PartitionInflightTracker? inflightTracker,
        ProducerStatisticsCollector statisticsCollector,
        ConcurrentDictionary<TopicPartition, SemaphoreSlim> partitionSendGates,
        Func<SemaphoreSlim> createPartitionGate,
        Func<int> getProduceApiVersion,
        Action<int> setProduceApiVersion,
        Func<bool> isTransactional,
        Func<TopicPartition, CancellationToken, ValueTask>? ensurePartitionInTransaction,
        Action<ReadyBatch>? rerouteBatch,
        Action<TopicPartition, long, DateTimeOffset, int, Exception?>? onAcknowledgement,
        ILogger? logger)
    {
        _brokerId = brokerId;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _accumulator = accumulator;
        _options = options;
        _compressionCodecs = compressionCodecs;
        _inflightTracker = inflightTracker;
        _statisticsCollector = statisticsCollector;
        _partitionSendGates = partitionSendGates;
        _createPartitionGate = createPartitionGate;
        _getProduceApiVersion = getProduceApiVersion;
        _setProduceApiVersion = setProduceApiVersion;
        _isTransactional = isTransactional;
        _ensurePartitionInTransaction = ensurePartitionInTransaction;
        _rerouteBatch = rerouteBatch;
        _onAcknowledgement = onAcknowledgement;
        _logger = logger;

        _batchChannel = Channel.CreateUnbounded<ReadyBatch>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _inFlightSemaphore = new SemaphoreSlim(
            options.MaxInFlightRequestsPerConnection,
            options.MaxInFlightRequestsPerConnection);

        _cts = new CancellationTokenSource();
        _sendLoopTask = SendLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Enqueues a batch for sending to this broker.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(ReadyBatch batch)
    {
        if (!_batchChannel.Writer.TryWrite(batch))
        {
            // Channel is completed (disposal in progress) — fail the batch
            batch.Fail(new ObjectDisposedException(nameof(BrokerSender)));
            CleanupBatch(batch);
        }
    }

    /// <summary>
    /// Main send loop: drains batches from channel, coalesces by partition, and sends pipelined requests.
    /// Single-threaded: partition gate acquisition is deterministically FIFO.
    ///
    /// Batches that cannot be sent (gate busy or same-partition collision) are carried over
    /// to the next iteration by re-enqueuing them to the channel.
    /// </summary>
    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        var channelReader = _batchChannel.Reader;
        // Max batches per drain = MaxInFlight * 4 (same as old SenderLoopAsync)
        var maxDrain = _options.MaxInFlightRequestsPerConnection * 4;
        var drainBuffer = ArrayPool<ReadyBatch>.Shared.Rent(maxDrain);

        // Reusable collections for coalescing (single-threaded, no contention)
        var coalescedPartitions = new HashSet<TopicPartition>();
        List<ReadyBatch>? carryOver = null;

        try
        {
            while (await channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // Drain all currently-available batches
                var drainCount = 0;
                while (drainCount < maxDrain && channelReader.TryRead(out var batch))
                {
                    drainBuffer[drainCount++] = batch;
                }

                if (drainCount == 0)
                    continue;

                // Coalesce: at most one batch per partition per request
                coalescedPartitions.Clear();
                carryOver?.Clear();

                var coalescedCount = 0;
                var coalescedBatches = ArrayPool<ReadyBatch>.Shared.Rent(drainCount);
                var coalescedGates = ArrayPool<SemaphoreSlim>.Shared.Rent(drainCount);

                for (var i = 0; i < drainCount; i++)
                {
                    var batch = drainBuffer[i];

                    // Ensure at most one batch per partition per coalesced request
                    if (!coalescedPartitions.Add(batch.TopicPartition))
                    {
                        carryOver ??= [];
                        carryOver.Add(batch);
                        continue;
                    }

                    // Acquire partition gate (single-threaded → deterministically FIFO)
                    var partitionGate = _partitionSendGates.GetOrAdd(
                        batch.TopicPartition, _ => _createPartitionGate());

                    // Try non-blocking acquire first for coalescing
                    if (!partitionGate.Wait(0, CancellationToken.None))
                    {
                        // Gate busy — carry over to next iteration
                        coalescedPartitions.Remove(batch.TopicPartition);
                        carryOver ??= [];
                        carryOver.Add(batch);
                        continue;
                    }

                    coalescedBatches[coalescedCount] = batch;
                    coalescedGates[coalescedCount] = partitionGate;
                    coalescedCount++;
                }

                // Send coalesced batches
                if (coalescedCount > 0)
                {
                    await SendCoalescedAsync(
                        coalescedBatches, coalescedGates, coalescedCount, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ArrayPool<ReadyBatch>.Shared.Return(coalescedBatches, clearArray: true);
                    ArrayPool<SemaphoreSlim>.Shared.Return(coalescedGates, clearArray: true);
                }

                // Re-enqueue carry-over batches to the channel so WaitToReadAsync wakes naturally.
                // If nothing was coalesced (all gates busy), yield to let response handlers
                // release gates before the next iteration — prevents a tight spin loop.
                if (carryOver is { Count: > 0 })
                {
                    if (coalescedCount == 0)
                    {
                        await Task.Yield();
                    }

                    foreach (var batch in carryOver)
                    {
                        Enqueue(batch);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (ChannelClosedException)
        {
            // Channel completed — normal during disposal
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "BrokerSender[{BrokerId}] send loop failed", _brokerId);
        }
        finally
        {
            ArrayPool<ReadyBatch>.Shared.Return(drainBuffer, clearArray: true);

            // Fail any carry-over batches that couldn't be re-enqueued
            if (carryOver is { Count: > 0 })
            {
                foreach (var batch in carryOver)
                {
                    try { batch.Fail(new ObjectDisposedException(nameof(BrokerSender))); }
                    catch { /* Observe */ }
                    CleanupBatch(batch);
                }
            }

            // Drain any remaining batches from channel and fail them
            while (channelReader.TryRead(out var remaining))
            {
                try { remaining.Fail(new ObjectDisposedException(nameof(BrokerSender))); }
                catch { /* Observe */ }
                CleanupBatch(remaining);
            }
        }
    }

    /// <summary>
    /// Sends coalesced batches (one per partition) as a single ProduceRequest.
    /// Gates are pre-acquired by the caller.
    /// </summary>
    private async ValueTask SendCoalescedAsync(
        ReadyBatch[] batches,
        SemaphoreSlim[] gates,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            // Acquire in-flight semaphore (limits pipelined requests per broker)
            await _inFlightSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Failed to acquire — release gates and fail batches
            for (var i = 0; i < count; i++)
            {
                ReleaseGate(gates[i]);
                FailAndCleanupBatch(batches[i], new OperationCanceledException(cancellationToken));
            }

            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
            ArrayPool<SemaphoreSlim>.Shared.Return(gates, clearArray: true);
            return;
        }

        try
        {
            var connection = await _connectionPool.GetConnectionAsync(_brokerId, cancellationToken)
                .ConfigureAwait(false);

            var apiVersion = EnsureApiVersion();

            // Register new partitions in transaction if needed
            if (_isTransactional() && _ensurePartitionInTransaction is not null)
            {
                for (var i = 0; i < count; i++)
                {
                    await _ensurePartitionInTransaction(batches[i].TopicPartition, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            // Build coalesced ProduceRequest
            var request = BuildProduceRequest(batches, count);

            var requestStartTime = Stopwatch.GetTimestamp();
            _statisticsCollector.RecordRequestSent();

            // Handle Acks.None (fire-and-forget)
            if (_options.Acks == Acks.None)
            {
                await connection.SendFireAndForgetAsync<ProduceRequest, ProduceResponse>(
                    request, (short)apiVersion, cancellationToken).ConfigureAwait(false);

                var fireAndForgetTimestamp = DateTimeOffset.UtcNow;
                for (var i = 0; i < count; i++)
                {
                    var batch = batches[i];
                    _statisticsCollector.RecordBatchDelivered(
                        batch.TopicPartition.Topic,
                        batch.TopicPartition.Partition,
                        batch.CompletionSourcesCount);
                    CompleteInflightEntry(batch);
                    batch.CompleteSend(-1, fireAndForgetTimestamp);
                    _onAcknowledgement?.Invoke(batch.TopicPartition, -1, fireAndForgetTimestamp, batch.CompletionSourcesCount, null);
                }

                // Release everything synchronously (no pipelined response)
                ReleaseInFlightSemaphore();
                for (var i = 0; i < count; i++)
                {
                    ReleaseGate(gates[i]);
                    CleanupBatch(batches[i]);
                }

                ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
                ArrayPool<SemaphoreSlim>.Shared.Return(gates, clearArray: true);
                return;
            }

            // Pipelined send: write request, get response task
            var responseTask = connection.SendPipelinedAsync<ProduceRequest, ProduceResponse>(
                request, (short)apiVersion, cancellationToken);

            // Fire-and-forget response handling — HandleResponseAsync self-registers
            // in _inFlightResponses for clean shutdown (zero allocation on success path)
            _ = HandleResponseAsync(
                responseTask, batches, gates, count, requestStartTime, cancellationToken);
        }
        catch (Exception ex)
        {
            // Send failed — release everything
            ReleaseInFlightSemaphore();
            for (var i = 0; i < count; i++)
            {
                ReleaseGate(gates[i]);
                FailAndCleanupBatch(batches[i], ex);
            }

            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
            ArrayPool<SemaphoreSlim>.Shared.Return(gates, clearArray: true);
        }
    }

    /// <summary>
    /// Handles the response from a pipelined send.
    /// Processes per-partition results, schedules retries for retriable errors, releases resources.
    /// Self-registers in _inFlightResponses for clean shutdown. Awaits any spawned retry tasks
    /// before unregistering, so DisposeAsync sees this task as running until all retries complete.
    /// Zero extra allocations on the success path.
    /// </summary>
    private async Task HandleResponseAsync(
        Task<ProduceResponse> responseTask,
        ReadyBatch[] batches,
        SemaphoreSlim[] gates,
        int count,
        long requestStartTime,
        CancellationToken cancellationToken)
    {
        // Self-register for clean shutdown tracking
        var thisTask = responseTask as Task; // Use responseTask as the tracking key (already allocated)
        _inFlightResponses.TryAdd(thisTask, 0);

        var inFlightReleased = false;
        List<Task>? retryTasks = null;

        try
        {
            var response = await responseTask.ConfigureAwait(false);

            // Response received — release in-flight semaphore immediately so the send loop
            // can pipeline new requests while we process results.
            inFlightReleased = true;
            ReleaseInFlightSemaphore();

            var elapsedTicks = Stopwatch.GetTimestamp() - requestStartTime;
            var latencyMs = (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
            _statisticsCollector.RecordResponseReceived(latencyMs);

            // Build response lookup
            Dictionary<(string Topic, int Partition), ProduceResponsePartitionData>? responseLookup = null;
            foreach (var topicResp in response.Responses)
            {
                foreach (var partResp in topicResp.PartitionResponses)
                {
                    responseLookup ??= new Dictionary<(string, int), ProduceResponsePartitionData>();
                    responseLookup[(topicResp.Name, partResp.Index)] = partResp;
                }
            }

            // Process each batch
            for (var i = 0; i < count; i++)
            {
                var batch = batches[i];
                var expectedTopic = batch.TopicPartition.Topic;
                var expectedPartition = batch.TopicPartition.Partition;

                ProduceResponsePartitionData? partitionResponse = null;
                responseLookup?.TryGetValue((expectedTopic, expectedPartition), out partitionResponse);

                if (partitionResponse is null)
                {
                    // No response — retriable
                    _logger?.LogWarning(
                        "[BrokerSender] No response for {Topic}-{Partition}",
                        expectedTopic, expectedPartition);
                    retryTasks ??= [];
                    retryTasks.Add(ScheduleRetryAsync(batch, gates[i], ErrorCode.NetworkException, cancellationToken));
                    batches[i] = null!; // Mark as handed off
                    gates[i] = null!;
                    continue;
                }

                if (partitionResponse.ErrorCode == ErrorCode.DuplicateSequenceNumber)
                {
                    _logger?.LogDebug(
                        "[BrokerSender] DuplicateSequenceNumber for {Topic}-{Partition} at offset {Offset}",
                        expectedTopic, expectedPartition, partitionResponse.BaseOffset);
                    // Treat as success — fall through
                }
                else if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    if (partitionResponse.ErrorCode.IsRetriable())
                    {
                        retryTasks ??= [];
                        retryTasks.Add(ScheduleRetryAsync(batch, gates[i], partitionResponse.ErrorCode, cancellationToken));
                        batches[i] = null!;
                        gates[i] = null!;
                        continue;
                    }

                    // Non-retriable error
                    CompleteInflightEntry(batch);
                    _statisticsCollector.RecordBatchFailed(expectedTopic, expectedPartition, batch.CompletionSourcesCount);
                    try { batch.Fail(new KafkaException(partitionResponse.ErrorCode, $"Produce failed: {partitionResponse.ErrorCode}")); }
                    catch { /* Observe */ }
                    _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow, batch.CompletionSourcesCount,
                        new KafkaException(partitionResponse.ErrorCode, $"Produce failed: {partitionResponse.ErrorCode}"));
                    ReleaseGate(gates[i]);
                    CleanupBatch(batch);
                    batches[i] = null!;
                    gates[i] = null!;
                    continue;
                }

                // Success
                _statisticsCollector.RecordBatchDelivered(expectedTopic, expectedPartition, batch.CompletionSourcesCount);
                var timestamp = partitionResponse.LogAppendTimeMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(partitionResponse.LogAppendTimeMs)
                    : DateTimeOffset.UtcNow;
                CompleteInflightEntry(batch);
                batch.CompleteSend(partitionResponse.BaseOffset, timestamp);
                _onAcknowledgement?.Invoke(batch.TopicPartition, partitionResponse.BaseOffset, timestamp, batch.CompletionSourcesCount, null);
                ReleaseGate(gates[i]);
                CleanupBatch(batch);
                batches[i] = null!;
                gates[i] = null!;
            }
        }
        catch (Exception ex)
        {
            // Entire send failed — fail all remaining batches
            _logger?.LogError(ex, "BrokerSender[{BrokerId}] response handling failed", _brokerId);

            for (var i = 0; i < count; i++)
            {
                if (batches[i] is null) continue; // Already handled
                FailAndCleanupBatch(batches[i], ex);
                if (gates[i] is not null)
                    ReleaseGate(gates[i]);
            }
        }
        finally
        {
            if (!inFlightReleased)
                ReleaseInFlightSemaphore();
            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
            ArrayPool<SemaphoreSlim>.Shared.Return(gates, clearArray: true);

            // Await any spawned retry tasks before unregistering, so DisposeAsync
            // sees this handler as running until all its retries complete
            if (retryTasks is { Count: > 0 })
            {
                try { await Task.WhenAll(retryTasks).ConfigureAwait(false); }
                catch { /* Exceptions observed by ScheduleRetryAsync */ }
            }

            _inFlightResponses.TryRemove(thisTask, out _);
        }
    }

    /// <summary>
    /// Schedules a retry for a batch: performs backoff/OOSN coordination, releases the partition
    /// gate, then re-enqueues the batch to the appropriate BrokerSender (self or rerouted).
    /// This ensures retries go through the send loop's single-threaded path, preserving wire-order.
    /// Fire-and-forget from HandleResponseAsync — tracked in _inFlightResponses for clean shutdown.
    /// </summary>
    private async Task ScheduleRetryAsync(
        ReadyBatch batch,
        SemaphoreSlim partitionGate,
        ErrorCode errorCode,
        CancellationToken cancellationToken)
    {
        var gateReleased = false;

        try
        {
            var deliveryDeadlineTicks = Stopwatch.GetTimestamp() +
                (long)(_options.DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0));
            var backoffMs = _options.RetryBackoffMs;

            cancellationToken.ThrowIfCancellationRequested();

            if (Stopwatch.GetTimestamp() >= deliveryDeadlineTicks)
            {
                throw new TimeoutException(
                    $"Delivery timeout exceeded for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}" +
                    (errorCode != ErrorCode.None ? $" (last error: {errorCode})" : ""));
            }

            // Coordinated retry for OutOfOrderSequenceNumber
            if (errorCode == ErrorCode.OutOfOrderSequenceNumber
                && batch.InflightEntry is { } inflightEntry
                && _inflightTracker is not null)
            {
                _logger?.LogDebug(
                    "[BrokerSender] OOSN for {Topic}-{Partition} seq={Seq}, waiting for predecessor",
                    batch.TopicPartition.Topic, batch.TopicPartition.Partition, inflightEntry.BaseSequence);

                try
                {
                    await _inflightTracker.WaitForPredecessorAsync(inflightEntry, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception predecessorEx) when (predecessorEx is not OperationCanceledException)
                {
                    _logger?.LogDebug(predecessorEx,
                        "[BrokerSender] Predecessor failed for {Topic}-{Partition}, retrying",
                        batch.TopicPartition.Topic, batch.TopicPartition.Partition);
                }
            }
            else
            {
                _logger?.LogDebug(
                    "[BrokerSender] Retriable error {ErrorCode} for {Topic}-{Partition}, retrying after {BackoffMs}ms",
                    errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition, backoffMs);

                // Refresh metadata for leader changes
                _ = _metadataManager.RefreshMetadataAsync([batch.TopicPartition.Topic], cancellationToken);

                await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
            }

            _statisticsCollector.RecordRetry();

            // Release partition gate before re-enqueue so the send loop can acquire it
            gateReleased = true;
            ReleaseGate(partitionGate);

            // Re-resolve leader — may have changed
            var leader = await _metadataManager.GetPartitionLeaderAsync(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                cancellationToken).ConfigureAwait(false);

            if (leader is null)
            {
                FailAndCleanupBatch(batch,
                    new KafkaException(ErrorCode.LeaderNotAvailable,
                        $"No leader available for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}"));
                return;
            }

            if (leader.NodeId == _brokerId)
            {
                // Same broker — re-enqueue to own channel
                Enqueue(batch);
            }
            else if (_rerouteBatch is not null)
            {
                // Leader moved to different broker — reroute
                _rerouteBatch(batch);
            }
            else
            {
                // No reroute callback — re-enqueue to self (best effort)
                Enqueue(batch);
            }
        }
        catch (Exception ex)
        {
            CompleteInflightEntry(batch);
            _logger?.LogError(ex, "[BrokerSender] Retry scheduling failed for {Topic}-{Partition}",
                batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            try { batch.Fail(ex); }
            catch { /* Observe */ }
            _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
                batch.CompletionSourcesCount, ex);
            if (!gateReleased)
                ReleaseGate(partitionGate);
            CleanupBatch(batch);
        }
    }

    /// <summary>
    /// Builds a coalesced ProduceRequest for multiple batches (one per partition).
    /// </summary>
    private ProduceRequest BuildProduceRequest(ReadyBatch[] batches, int count)
    {
        // Group by topic (O(n²) for small n, avoids dictionary allocation)
        var topicCount = 0;
        for (var i = 0; i < count; i++)
        {
            var isFirst = true;
            for (var j = 0; j < i; j++)
            {
                if (batches[j].TopicPartition.Topic == batches[i].TopicPartition.Topic)
                {
                    isFirst = false;
                    break;
                }
            }
            if (isFirst) topicCount++;
        }

        var topicDataArray = new ProduceRequestTopicData[topicCount];
        var topicIdx = 0;

        for (var i = 0; i < count; i++)
        {
            var alreadyProcessed = false;
            for (var j = 0; j < i; j++)
            {
                if (batches[j].TopicPartition.Topic == batches[i].TopicPartition.Topic)
                {
                    alreadyProcessed = true;
                    break;
                }
            }
            if (alreadyProcessed) continue;

            var topicName = batches[i].TopicPartition.Topic;
            var partCount = 0;
            for (var j = i; j < count; j++)
            {
                if (batches[j].TopicPartition.Topic == topicName) partCount++;
            }

            var partitionDataArray = new ProduceRequestPartitionData[partCount];
            var partIdx = 0;
            for (var j = i; j < count; j++)
            {
                if (batches[j].TopicPartition.Topic != topicName) continue;
                partitionDataArray[partIdx++] = new ProduceRequestPartitionData
                {
                    Index = batches[j].TopicPartition.Partition,
                    Records = [batches[j].RecordBatch],
                    Compression = _options.CompressionType,
                    CompressionCodecs = _compressionCodecs
                };
            }

            topicDataArray[topicIdx++] = new ProduceRequestTopicData
            {
                Name = topicName,
                PartitionData = partitionDataArray
            };
        }

        return new ProduceRequest
        {
            Acks = (short)_options.Acks,
            TimeoutMs = _options.RequestTimeoutMs,
            TransactionalId = _options.TransactionalId,
            TopicData = topicDataArray
        };
    }

    /// <summary>
    /// Ensures API version is negotiated. Thread-safe initialization.
    /// </summary>
    private int EnsureApiVersion()
    {
        var version = _getProduceApiVersion();
        if (version < 0)
        {
            version = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.Produce,
                ProduceRequest.LowestSupportedVersion,
                ProduceRequest.HighestSupportedVersion);
            _setProduceApiVersion(version);
            version = _getProduceApiVersion();
        }
        return version;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CompleteInflightEntry(ReadyBatch batch)
    {
        if (batch.InflightEntry is { } entry)
        {
            _inflightTracker?.Complete(entry);
            batch.InflightEntry = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReleaseGate(SemaphoreSlim gate)
    {
        try { gate.Release(); }
        catch (ObjectDisposedException) { /* Disposal race */ }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseInFlightSemaphore()
    {
        try { _inFlightSemaphore.Release(); }
        catch (ObjectDisposedException) { /* Disposal race */ }
    }

    private void FailAndCleanupBatch(ReadyBatch batch, Exception ex)
    {
        CompleteInflightEntry(batch);
        _statisticsCollector.RecordBatchFailed(
            batch.TopicPartition.Topic,
            batch.TopicPartition.Partition,
            batch.CompletionSourcesCount);
        try { batch.Fail(ex); }
        catch { /* Observe */ }
        _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
            batch.CompletionSourcesCount, ex);
        CleanupBatch(batch);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CleanupBatch(ReadyBatch batch)
    {
        _accumulator.ReturnReadyBatch(batch);
        _accumulator.OnBatchExitsPipeline();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Complete channel — send loop will drain remaining batches
        _batchChannel.Writer.Complete();

        // Wait for send loop to finish draining
        try
        {
            await _sendLoopTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
        catch
        {
            // Timeout — cancel to force exit
            await _cts.CancelAsync().ConfigureAwait(false);
            try { await _sendLoopTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch { /* Best effort */ }
        }

        // Wait for in-flight responses
        var inFlightTasks = _inFlightResponses.Keys.ToArray();
        if (inFlightTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(inFlightTasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Best effort — some responses may not arrive during shutdown
            }
        }

        _cts.Dispose();
        _inFlightSemaphore.Dispose();
    }
}
