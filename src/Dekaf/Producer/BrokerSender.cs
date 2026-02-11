using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Statistics;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

/// <summary>
/// Per-broker sender that serializes all writes to a single broker connection.
/// A single-threaded send loop drains batches from a channel, coalesces them into
/// ProduceRequests, and sends them via pipelined writes. This guarantees wire-order
/// for same-partition batches. Partition gates (capacity=1) ensure at most one batch
/// per partition is in-flight at a time, preventing OutOfOrderSequenceNumber cascades.
///
/// All writes go through the send loop — there are no out-of-loop write paths.
/// Same-broker retries keep their partition gate held (IsRetry flag) so newer batches
/// cannot jump ahead. Leader-change retries release the gate and reroute to the
/// correct BrokerSender via the rerouteBatch callback.
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
    private readonly Func<short, CancellationToken, ValueTask<(long ProducerId, short ProducerEpoch)>>? _bumpEpoch;
    private readonly Func<short>? _getCurrentEpoch;
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

    // Signalled by ReleaseGate so the send loop can wake up when a partition gate becomes free,
    // without acquiring the gate itself (avoids phantom-waiter accumulation on the SemaphoreSlim).
    private TaskCompletionSource? _gateReleasedSignal;

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
        Func<short, CancellationToken, ValueTask<(long ProducerId, short ProducerEpoch)>>? bumpEpoch,
        Func<short>? getCurrentEpoch,
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
        _bumpEpoch = bumpEpoch;
        _getCurrentEpoch = getCurrentEpoch;
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
            // Channel is completed (disposal in progress) — fail the batch.
            // Must complete inflight entry so successors in WaitForPredecessorAsync don't hang.
            // If the batch is a retry, it still holds its partition gate — release it.
            if (batch.IsRetry)
            {
                batch.IsRetry = false;
                var gate = _partitionSendGates.GetOrAdd(
                    batch.TopicPartition, _ => _createPartitionGate());
                ReleaseGate(gate);
            }
            CompleteInflightEntry(batch);
            batch.Fail(new ObjectDisposedException(nameof(BrokerSender)));
            CleanupBatch(batch);
        }
    }

    /// <summary>
    /// Main send loop: drains batches from channel, coalesces by partition, and sends pipelined requests.
    /// Single-threaded: partition gate acquisition is deterministically FIFO.
    ///
    /// Batches that cannot be sent (gate busy or same-partition collision) are carried over
    /// to the next iteration in a persistent local list. Retry batches (IsRetry=true) already
    /// hold their partition gate and skip gate acquisition, ensuring they are sent before
    /// newer batches for the same partition.
    /// </summary>
    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        var channelReader = _batchChannel.Reader;
        // Max batches per drain = MaxInFlight * 4 (same as old SenderLoopAsync)
        var maxDrain = _options.MaxInFlightRequestsPerConnection * 4;
        var drainBuffer = ArrayPool<ReadyBatch>.Shared.Rent(maxDrain);

        // Reusable collections for coalescing (single-threaded, no contention)
        var coalescedPartitions = new HashSet<TopicPartition>();

        // Persistent carry-over list across iterations. Kept LOCAL instead of re-enqueued to the
        // channel to preserve batch ordering. Re-enqueueing puts batches at the back of the channel,
        // allowing new batches from SenderLoopAsync to jump ahead and be sent out of order.
        // This causes ordering violations for non-idempotent producers and OOSN errors + hangs
        // for idempotent producers.
        List<ReadyBatch>? pendingCarryOver = null;

        try
        {
            while (true)
            {
                // Must check cancellation at every iteration — when carry-over batches exist
                // with busy gates (coalescedCount==0), the WhenAny path below completes without
                // propagating the OCE from the cancelled token. Without this check the loop
                // spins indefinitely, preventing process exit after disposal.
                cancellationToken.ThrowIfCancellationRequested();

                // Phase 1: Fill drain buffer — carry-over first (ordering), then channel
                var drainCount = 0;

                if (pendingCarryOver is { Count: > 0 })
                {
                    // Carry-over batches from previous iteration get priority to maintain ordering
                    var take = Math.Min(pendingCarryOver.Count, maxDrain);
                    for (var i = 0; i < take; i++)
                    {
                        drainBuffer[drainCount++] = pendingCarryOver[i];
                    }
                    pendingCarryOver.RemoveRange(0, take);
                }

                if (drainCount == 0)
                {
                    // No carry-over: wait for channel data
                    if (!await channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                        break;
                }

                // Fill remaining drain capacity from channel
                while (drainCount < maxDrain && channelReader.TryRead(out var channelBatch))
                {
                    drainBuffer[drainCount++] = channelBatch;
                }

                if (drainCount == 0)
                    continue;

                // Register gate-release signal BEFORE Phase 2 gate checks. This prevents a
                // race where ReleaseGate fires between our gate check (Phase 2) and the wait
                // (Phase 3), which would miss the notification and deadlock.
                // The signal is only consumed when coalescedCount == 0 (all gates busy).
                // In the common case (coalescedCount > 0), the signal is harmlessly orphaned.
                var gateSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _gateReleasedSignal, gateSignal);

                // Phase 2: Coalesce — at most one batch per partition per request
                coalescedPartitions.Clear();
                List<ReadyBatch>? newCarryOver = null;

                var coalescedCount = 0;
                var coalescedBatches = ArrayPool<ReadyBatch>.Shared.Rent(drainCount);
                var coalescedGates = ArrayPool<SemaphoreSlim>.Shared.Rent(drainCount);

                for (var i = 0; i < drainCount; i++)
                {
                    var batch = drainBuffer[i];

                    // Ensure at most one batch per partition per coalesced request
                    if (!coalescedPartitions.Add(batch.TopicPartition))
                    {
                        newCarryOver ??= [];
                        newCarryOver.Add(batch);
                        continue;
                    }

                    var partitionGate = _partitionSendGates.GetOrAdd(
                        batch.TopicPartition, _ => _createPartitionGate());

                    if (batch.IsRetry)
                    {
                        // Retry batch already holds its partition gate from the previous send.
                        // Skip gate acquisition — this prevents newer batches from jumping ahead
                        // during the retry cycle, which would cause OOSN cascades.
                        batch.IsRetry = false;
                        coalescedBatches[coalescedCount] = batch;
                        coalescedGates[coalescedCount] = partitionGate;
                        coalescedCount++;
                        continue;
                    }

                    // Try non-blocking acquire for normal batches
                    if (!partitionGate.Wait(0, CancellationToken.None))
                    {
                        // Gate busy — carry over to next iteration
                        coalescedPartitions.Remove(batch.TopicPartition);
                        newCarryOver ??= [];
                        newCarryOver.Add(batch);
                        continue;
                    }

                    coalescedBatches[coalescedCount] = batch;
                    coalescedGates[coalescedCount] = partitionGate;
                    coalescedCount++;
                }

                // Phase 3: Send coalesced batches
                if (coalescedCount > 0)
                {
                    await SendCoalescedAsync(
                        coalescedBatches, coalescedGates, coalescedCount, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ArrayPool<ReadyBatch>.Shared.Return(coalescedBatches, clearArray: true);
                    ArrayPool<SemaphoreSlim>.Shared.Return(coalescedGates, clearArray: true);

                    // All carry-over batches have busy gates. Wait for either:
                    // 1. A gate to free up (ReleaseGate sets _gateReleasedSignal), or
                    // 2. New channel data arrives (retry batch with pre-acquired gate).
                    //
                    // We use a TaskCompletionSource signal instead of SemaphoreSlim.WaitAsync
                    // to avoid acquiring the gate. WaitAsync caused a livelock: each iteration
                    // queued a pending waiter on the semaphore, and when the gate was finally
                    // released, each Release() fed a pending waiter instead of incrementing the
                    // count — keeping the gate permanently at 0.
                    var channelWaitTask = channelReader.WaitToReadAsync(cancellationToken).AsTask();
                    await Task.WhenAny(gateSignal.Task, channelWaitTask).ConfigureAwait(false);
                }

                // Merge new carry-over into persistent list for next iteration
                if (newCarryOver is { Count: > 0 })
                {
                    if (pendingCarryOver is { Count: > 0 })
                    {
                        // Prepend remaining old carry-over (already at front) before new carry-over
                        // to maintain overall ordering
                        pendingCarryOver.AddRange(newCarryOver);
                    }
                    else
                    {
                        pendingCarryOver = newCarryOver;
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

            // Fail any carry-over batches that couldn't be sent.
            // Must complete inflight entries so successors in WaitForPredecessorAsync don't hang.
            // Retry batches still hold their partition gate — release it.
            if (pendingCarryOver is { Count: > 0 })
            {
                foreach (var batch in pendingCarryOver)
                {
                    ReleaseRetryGateIfNeeded(batch);
                    CompleteInflightEntry(batch);
                    try { batch.Fail(new ObjectDisposedException(nameof(BrokerSender))); }
                    catch { /* Observe */ }
                    CleanupBatch(batch);
                }
            }

            // Drain any remaining batches from channel and fail them
            while (channelReader.TryRead(out var remaining))
            {
                ReleaseRetryGateIfNeeded(remaining);
                CompleteInflightEntry(remaining);
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
            // Lazy epoch check: batches sealed in the channel before an epoch bump have stale
            // producer epoch/sequence. Rewrite them before sending. The epoch comparison is a
            // short compare (essentially free) — the rewrite branch only executes during recovery.
            if (_getCurrentEpoch is not null)
            {
                var currentEpoch = _getCurrentEpoch();
                for (var i = 0; i < count; i++)
                {
                    var batch = batches[i];
                    var batchEpoch = batch.RecordBatch.ProducerEpoch;

                    if (batchEpoch >= 0 && batchEpoch != currentEpoch)
                    {
                        // Stale batch — rewrite with current epoch and fresh sequence
                        CompleteInflightEntry(batch);

                        var tp = batch.TopicPartition;
                        var recordCount = batch.RecordBatch.Records.Count;
                        var newSeq = _accumulator.GetAndIncrementSequence(tp, recordCount);
                        // Read producerId from accumulator (updated atomically with epoch)
                        var currentPid = _accumulator.ProducerId;
                        batch.RewriteRecordBatch(
                            batch.RecordBatch.WithProducerState(currentPid, currentEpoch, newSeq));

                        // Re-register with inflight tracker
                        if (_inflightTracker is not null)
                        {
                            batch.InflightEntry = _inflightTracker.Register(tp, newSeq, recordCount);
                        }
                    }
                }
            }

            // Register fresh batches with inflight tracker at send time (not drain time).
            // Retry batches already have entries from ScheduleRetryAsync; stale batches were
            // re-registered above. Only fresh batches (InflightEntry == null) need registration.
            //
            // Registration at send time prevents a deadlock where queued-but-not-sent batches
            // appear as predecessors of retry batches in WaitForPredecessorAsync but can never
            // complete because the retry batch holds the partition gate.
            if (_inflightTracker is not null)
            {
                for (var i = 0; i < count; i++)
                {
                    var batch = batches[i];
                    if (batch.InflightEntry is null && batch.RecordBatch.BaseSequence >= 0)
                    {
                        batch.InflightEntry = _inflightTracker.Register(
                            batch.TopicPartition,
                            batch.RecordBatch.BaseSequence,
                            batch.CompletionSourcesCount);
                    }
                }
            }

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
                    try { _onAcknowledgement?.Invoke(batch.TopicPartition, -1, fireAndForgetTimestamp, batch.CompletionSourcesCount, null); }
                    catch { /* Observe - must not prevent cleanup */ }
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

            // Fire-and-forget response handling — tracked in _inFlightResponses for clean shutdown.
            // We track the handler task (not the response task) so DisposeAsync waits for the
            // full response processing + retry lifecycle, not just the network response.
            var handlerTask = HandleResponseAsync(
                responseTask, batches, gates, count, requestStartTime, cancellationToken);
            _inFlightResponses.TryAdd(handlerTask, 0);
            _ = handlerTask.ContinueWith(static (t, state) =>
            {
                ((ConcurrentDictionary<Task, byte>)state!).TryRemove(t, out _);
            }, _inFlightResponses, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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
    /// Tracked in _inFlightResponses by SendCoalescedAsync for clean shutdown. Awaits any spawned
    /// retry tasks so DisposeAsync sees this task as running until all retries complete.
    /// </summary>
    private async Task HandleResponseAsync(
        Task<ProduceResponse> responseTask,
        ReadyBatch[] batches,
        SemaphoreSlim[] gates,
        int count,
        long requestStartTime,
        CancellationToken cancellationToken)
    {
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
                    if (partitionResponse.ErrorCode.IsRetriable()
                        || partitionResponse.ErrorCode == ErrorCode.OutOfOrderSequenceNumber
                        || partitionResponse.ErrorCode == ErrorCode.InvalidProducerEpoch
                        || partitionResponse.ErrorCode == ErrorCode.UnknownProducerId)
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
                    try { _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow, batch.CompletionSourcesCount,
                        new KafkaException(partitionResponse.ErrorCode, $"Produce failed: {partitionResponse.ErrorCode}")); }
                    catch { /* Observe - must not prevent cleanup */ }
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
                try { _onAcknowledgement?.Invoke(batch.TopicPartition, partitionResponse.BaseOffset, timestamp, batch.CompletionSourcesCount, null); }
                catch { /* Observe - must not prevent cleanup */ }
                ReleaseGate(gates[i]);
                CleanupBatch(batch);
                batches[i] = null!;
                gates[i] = null!;
            }
        }
        catch (Exception ex)
        {
            // Entire send failed — fail all remaining batches.
            // Release gates BEFORE cleanup to prevent gate leaks if FailAndCleanupBatch throws.
            _logger?.LogError(ex, "BrokerSender[{BrokerId}] response handling failed", _brokerId);

            for (var i = 0; i < count; i++)
            {
                if (batches[i] is null) continue; // Already handled
                if (gates[i] is not null)
                    ReleaseGate(gates[i]);
                FailAndCleanupBatch(batches[i], ex);
            }
        }
        finally
        {
            if (!inFlightReleased)
                ReleaseInFlightSemaphore();
            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
            ArrayPool<SemaphoreSlim>.Shared.Return(gates, clearArray: true);

            // Await any spawned retry tasks so this Task stays alive until all retries complete.
            // The ContinueWith in SendCoalescedAsync removes us from _inFlightResponses when done.
            if (retryTasks is { Count: > 0 })
            {
                try { await Task.WhenAll(retryTasks).ConfigureAwait(false); }
                catch { /* Exceptions observed by ScheduleRetryAsync */ }
            }
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
            // Use the batch's creation time as the anchor for the delivery deadline.
            // This prevents infinite retries — each retry checks against the same absolute deadline.
            var deliveryDeadlineTicks = batch.StopwatchCreatedTicks +
                (long)(_options.DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0));
            var backoffMs = _options.RetryBackoffMs;

            cancellationToken.ThrowIfCancellationRequested();

            if (Stopwatch.GetTimestamp() >= deliveryDeadlineTicks)
            {
                throw new TimeoutException(
                    $"Delivery timeout exceeded for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}" +
                    (errorCode != ErrorCode.None ? $" (last error: {errorCode})" : ""));
            }

            // Epoch bump recovery for OOSN/InvalidProducerEpoch/UnknownProducerId
            var isEpochBumpError = errorCode is ErrorCode.OutOfOrderSequenceNumber
                or ErrorCode.InvalidProducerEpoch or ErrorCode.UnknownProducerId;

            if (isEpochBumpError && _bumpEpoch is not null
                && batch.InflightEntry is { } inflightEntry
                && _inflightTracker is not null)
            {
                var isHead = _inflightTracker.IsHeadOfLine(inflightEntry);

                if (isHead)
                {
                    _logger?.LogDebug(
                        "[BrokerSender] {ErrorCode} for {Topic}-{Partition} seq={Seq}, bumping epoch",
                        errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                        inflightEntry.BaseSequence);

                    // Bump epoch — serialized across all partitions by _transactionLock
                    var (newPid, newEpoch) = await _bumpEpoch(
                        batch.RecordBatch.ProducerEpoch, cancellationToken).ConfigureAwait(false);

                    // Remove old inflight entry (stale sequence)
                    CompleteInflightEntry(batch);

                    // Assign new sequence and rewrite the record batch
                    var tp = batch.TopicPartition;
                    var recordCount = batch.RecordBatch.Records.Count;
                    var newSeq = _accumulator.GetAndIncrementSequence(tp, recordCount);
                    batch.RewriteRecordBatch(
                        batch.RecordBatch.WithProducerState(newPid, newEpoch, newSeq));

                    // Re-register with inflight tracker under new sequence
                    batch.InflightEntry = _inflightTracker.Register(tp, newSeq, recordCount);
                }
                else
                {
                    // Not head-of-line — wait for predecessor (OOSN caused by predecessor failure)
                    _logger?.LogDebug(
                        "[BrokerSender] {ErrorCode} for {Topic}-{Partition} seq={Seq}, waiting for predecessor",
                        errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                        inflightEntry.BaseSequence);

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
            }
            else if (isEpochBumpError && _bumpEpoch is null
                && batch.InflightEntry is { } txnInflightEntry
                && _inflightTracker is not null)
            {
                // Transactional producer — no epoch bump, wait for predecessor (existing behavior)
                _logger?.LogDebug(
                    "[BrokerSender] OOSN for {Topic}-{Partition} seq={Seq}, waiting for predecessor (transactional)",
                    batch.TopicPartition.Topic, batch.TopicPartition.Partition, txnInflightEntry.BaseSequence);

                try
                {
                    await _inflightTracker.WaitForPredecessorAsync(txnInflightEntry, cancellationToken)
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

            // Re-resolve leader — may have changed
            var leader = await _metadataManager.GetPartitionLeaderAsync(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                cancellationToken).ConfigureAwait(false);

            if (leader is null)
            {
                gateReleased = true;
                ReleaseGate(partitionGate);
                FailAndCleanupBatch(batch,
                    new KafkaException(ErrorCode.LeaderNotAvailable,
                        $"No leader available for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}"));
                return;
            }

            if (leader.NodeId == _brokerId)
            {
                // Same broker — keep the partition gate held and mark as retry.
                // The send loop will skip gate acquisition for retry batches, preventing
                // newer batches from jumping ahead and causing OOSN cascades.
                // The gate is released normally when the batch succeeds or finally fails.
                batch.IsRetry = true;
                gateReleased = true; // Gate ownership transferred to the retry batch
                Enqueue(batch);
            }
            else if (_rerouteBatch is not null)
            {
                // Leader moved to different broker — release gate and reroute.
                // The new BrokerSender will acquire the gate when processing the batch.
                gateReleased = true;
                ReleaseGate(partitionGate);
                _rerouteBatch(batch);
            }
            else
            {
                // No reroute callback — re-enqueue to self with gate held (best effort)
                batch.IsRetry = true;
                gateReleased = true; // Gate ownership transferred to the retry batch
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
            try { _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
                batch.CompletionSourcesCount, ex); }
            catch { /* Observe - must not prevent cleanup */ }
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
    private void ReleaseGate(SemaphoreSlim gate)
    {
        try { gate.Release(); }
        catch (ObjectDisposedException) { /* Disposal race */ }

        // Wake the send loop if it's waiting for a gate to become available.
        Interlocked.Exchange(ref _gateReleasedSignal, null)?.TrySetResult();
    }

    /// <summary>
    /// Releases the partition gate for a retry batch that still holds it.
    /// Called during cleanup (finally block, Enqueue failure) to prevent gate leaks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseRetryGateIfNeeded(ReadyBatch batch)
    {
        if (!batch.IsRetry) return;
        batch.IsRetry = false;
        var gate = _partitionSendGates.GetOrAdd(
            batch.TopicPartition, _ => _createPartitionGate());
        ReleaseGate(gate);
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
        try { _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
            batch.CompletionSourcesCount, ex); }
        catch { /* Observe - must not prevent CleanupBatch */ }
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

        // Complete channel — send loop will see channel completed and exit
        _batchChannel.Writer.Complete();

        // Cancel CTS FIRST so WaitToReadAsync, WaitAsync(cancellationToken), and
        // in-flight HandleResponseAsync/ScheduleRetryAsync calls are interrupted promptly.
        // Previously this happened AFTER the 10s send loop wait, which meant the send loop
        // could block on semaphore/gate waits with no way to be interrupted.
        await _cts.CancelAsync().ConfigureAwait(false);

        // Wait for send loop to finish (should exit quickly now that CTS is cancelled)
        try
        {
            await _sendLoopTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Best effort — send loop didn't exit in time
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
