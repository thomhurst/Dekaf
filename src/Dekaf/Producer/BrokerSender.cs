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
/// for same-partition batches.
///
/// Per-partition ordering during retries uses a mute set (aligned with the Java Kafka
/// producer): when a batch enters retry, its partition is muted so no newer batches
/// can be sent until the retry completes. Retry batches (IsRetry=true) unmute the
/// partition when coalesced, ensuring they are sent before any waiting batches.
///
/// In-flight request limiting uses a non-blocking counter (aligned with Java Kafka's
/// InFlightRequests.canSendMore()): the send loop checks capacity before sending
/// and waits for a TaskCompletionSource signal when at max. No SemaphoreSlim.
///
/// All writes go through the send loop — there are no out-of-loop write paths.
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
    private readonly Action<ReadyBatch>? _rerouteBatch;
    private readonly Action<TopicPartition, long, DateTimeOffset, int, Exception?>? _onAcknowledgement;
    private readonly Func<short, CancellationToken, ValueTask<(long ProducerId, short ProducerEpoch)>>? _bumpEpoch;
    private readonly Func<short>? _getCurrentEpoch;
    private readonly ILogger? _logger;

    private readonly Channel<ReadyBatch> _batchChannel;
    private readonly Task _sendLoopTask;
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentDictionary<Task, byte> _inFlightResponses = new();

    // Non-blocking in-flight request limiter (replaces SemaphoreSlim).
    // Aligned with Java Kafka's InFlightRequests.canSendMore(): the send loop checks
    // capacity before sending, and waits for a signal when at max. No blocking primitives.
    // _inFlightCount is only incremented by the single-threaded send loop;
    // decremented by HandleResponseAsync on the thread pool via Interlocked.
    private readonly int _maxInFlight;
    private int _inFlightCount;
    private TaskCompletionSource? _inFlightSlotAvailable;

    // Per-producer shared API version (read via volatile, written via Interlocked)
    private readonly Func<int> _getProduceApiVersion;
    private readonly Action<int> _setProduceApiVersion;

    // Transaction support
    private readonly Func<bool> _isTransactional;
    private readonly Func<TopicPartition, CancellationToken, ValueTask>? _ensurePartitionInTransaction;

    // Muted partitions: partitions with a retry in progress. Prevents newer batches from
    // being sent while a retry is in-flight, maintaining per-partition ordering.
    // Aligned with the Java Kafka producer's mute mechanism (RecordAccumulator.muted).
    // Uses ConcurrentDictionary as a thread-safe HashSet (written by HandleResponseAsync/
    // ScheduleRetryAsync on thread pool, read by the single-threaded send loop).
    private readonly ConcurrentDictionary<TopicPartition, byte> _mutedPartitions = new();

    // Signalled when a partition is unmuted so the send loop can wake up,
    // without polling (avoids busy-wait when all carry-over partitions are muted).
    private TaskCompletionSource? _unmuteSignal;

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

        _maxInFlight = options.MaxInFlightRequestsPerConnection;

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
            // If the batch is a retry, unmute its partition.
            if (batch.IsRetry)
            {
                batch.IsRetry = false;
                UnmutePartition(batch.TopicPartition);
            }
            CompleteInflightEntry(batch);
            batch.Fail(new ObjectDisposedException(nameof(BrokerSender)));
            CleanupBatch(batch);
        }
    }

    /// <summary>
    /// Main send loop: coalesces batches by partition and sends pipelined requests.
    /// Single-threaded: coalescing is deterministically FIFO.
    ///
    /// Inspired by Java Kafka's Sender: instead of draining all batches into a fixed-size
    /// buffer (which can starve channel reads when carry-over fills the buffer), carry-over
    /// and channel are scanned directly during coalescing. The channel is ALWAYS scanned
    /// after carry-over, ensuring retry batches (which unmute partitions) can always reach
    /// the coalescer — eliminating the carry-over starvation livelock.
    ///
    /// Batches for muted partitions (retry in progress) or same-partition collisions are
    /// carried over to the next iteration. Retry batches (IsRetry=true) unmute their
    /// partition and are coalesced immediately, ensuring they go before any waiting batches.
    /// </summary>
    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        var channelReader = _batchChannel.Reader;
        // Max partitions per coalesced request — limits channel reads per iteration.
        var maxCoalesce = _options.MaxInFlightRequestsPerConnection * 4;

        // Reusable collections for coalescing (single-threaded, no contention)
        var coalescedPartitions = new HashSet<TopicPartition>();

        // Persistent carry-over list across iterations. Kept LOCAL instead of re-enqueued to the
        // channel to preserve batch ordering. Re-enqueueing puts batches at the back of the channel,
        // allowing new batches to jump ahead and be sent out of order.
        List<ReadyBatch>? pendingCarryOver = null;

        try
        {
            while (true)
            {
                // Must check cancellation at every iteration — when carry-over batches exist
                // with muted partitions (coalescedCount==0), the WhenAny path below completes
                // without propagating the OCE from the cancelled token.
                cancellationToken.ThrowIfCancellationRequested();

                // Wait for data: carry-over or channel
                if (pendingCarryOver is not { Count: > 0 })
                {
                    if (!await channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                        break;
                }

                // Register signals BEFORE coalescing checks. This prevents a race where
                // a signal fires between our state checks and the wait, which would miss
                // the notification. In the fast path, signals are harmlessly orphaned.
                var inFlightSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _inFlightSlotAvailable, inFlightSignal);
                var unmuteSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _unmuteSignal, unmuteSignal);

                // Coalesce: at most one batch per partition per request.
                // Carry-over first (preserves ordering), then channel (prevents starvation).
                coalescedPartitions.Clear();
                List<ReadyBatch>? newCarryOver = null;
                var coalescedCount = 0;
                var coalescedBatches = ArrayPool<ReadyBatch>.Shared.Rent(maxCoalesce);

                // Scan carry-over: retry batches unmute and coalesce; muted/duplicate carry over.
                var hadCarryOver = pendingCarryOver is { Count: > 0 };
                if (hadCarryOver)
                {
                    for (var i = 0; i < pendingCarryOver!.Count; i++)
                    {
                        CoalesceBatch(pendingCarryOver[i], coalescedBatches, ref coalescedCount,
                            coalescedPartitions, ref newCarryOver);
                    }

                    pendingCarryOver = null;
                }

                // Scan channel (non-blocking). Two cases:
                // 1. No carry-over: normal fast path — read freely for maximum throughput.
                // 2. Carry-over was all muted (coalescedCount==0): read to find retry batches
                //    that unmute partitions — this prevents the starvation livelock.
                // When carry-over produced a coalesced batch, skip channel reads to prevent
                // carry-over growth. Carry-over drains by 1 per iteration; reading more from
                // the channel (duplicate-partition for single-partition workloads) would cause
                // unbounded growth and O(n²) scanning.
                if (!hadCarryOver || coalescedCount == 0)
                {
                    var channelReads = 0;
                    while (channelReads < maxCoalesce && channelReader.TryRead(out var channelBatch))
                    {
                        channelReads++;
                        CoalesceBatch(channelBatch, coalescedBatches, ref coalescedCount,
                            coalescedPartitions, ref newCarryOver);
                    }
                }

                // Send or wait
                if (coalescedCount > 0)
                {
                    // Wait in place for in-flight capacity if needed.
                    // Double-check pattern: register signal, re-check condition, then wait.
                    while (Volatile.Read(ref _inFlightCount) >= _maxInFlight)
                    {
                        var waitSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        Volatile.Write(ref _inFlightSlotAvailable, waitSignal);

                        if (Volatile.Read(ref _inFlightCount) >= _maxInFlight)
                            await waitSignal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    Interlocked.Increment(ref _inFlightCount);
                    await SendCoalescedAsync(
                        coalescedBatches, coalescedCount, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ArrayPool<ReadyBatch>.Shared.Return(coalescedBatches, clearArray: true);

                    // All batches are for muted partitions. Wait for either:
                    // 1. A partition to be unmuted (retry completed), or
                    // 2. New channel data arrives (may contain retry batch with IsRetry=true).
                    // 3. An in-flight slot opens.
                    var channelWaitTask = channelReader.WaitToReadAsync(cancellationToken).AsTask();
                    await Task.WhenAny(unmuteSignal.Task, inFlightSignal.Task, channelWaitTask).ConfigureAwait(false);
                }

                // Set carry-over for next iteration
                pendingCarryOver = newCarryOver;
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
            // Fail any carry-over batches that couldn't be sent.
            // Must complete inflight entries so successors in WaitForPredecessorAsync don't hang.
            if (pendingCarryOver is { Count: > 0 })
            {
                foreach (var batch in pendingCarryOver)
                {
                    CompleteInflightEntry(batch);
                    try { batch.Fail(new ObjectDisposedException(nameof(BrokerSender))); }
                    catch { /* Observe */ }
                    CleanupBatch(batch);
                }
            }

            // Drain any remaining batches from channel and fail them
            while (channelReader.TryRead(out var remaining))
            {
                if (remaining.IsRetry)
                {
                    remaining.IsRetry = false;
                    _mutedPartitions.TryRemove(remaining.TopicPartition, out _);
                }
                CompleteInflightEntry(remaining);
                try { remaining.Fail(new ObjectDisposedException(nameof(BrokerSender))); }
                catch { /* Observe */ }
                CleanupBatch(remaining);
            }
        }
    }

    /// <summary>
    /// Processes a single batch for coalescing. Retry batches unmute their partition and are
    /// coalesced ahead of normal batches. Muted or duplicate-partition batches are carried over.
    /// Extracted to avoid duplicating the logic for carry-over and channel sources.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CoalesceBatch(
        ReadyBatch batch,
        ReadyBatch[] coalescedBatches,
        ref int coalescedCount,
        HashSet<TopicPartition> coalescedPartitions,
        ref List<ReadyBatch>? newCarryOver)
    {
        if (batch.IsRetry)
        {
            // Retry batch: unmute the partition and coalesce it ahead of newer batches.
            batch.IsRetry = false;
            _mutedPartitions.TryRemove(batch.TopicPartition, out _);

            if (!coalescedPartitions.Add(batch.TopicPartition))
            {
                // Same partition already in this request (shouldn't happen — only one
                // retry per partition at a time). Carry over as safety net.
                newCarryOver ??= [];
                newCarryOver.Add(batch);
                return;
            }

            coalescedBatches[coalescedCount] = batch;
            coalescedCount++;
            return;
        }

        // Normal batch: skip muted partitions (retry in progress)
        if (_mutedPartitions.ContainsKey(batch.TopicPartition))
        {
            newCarryOver ??= [];
            newCarryOver.Add(batch);
            return;
        }

        // Ensure at most one batch per partition per coalesced request
        if (!coalescedPartitions.Add(batch.TopicPartition))
        {
            newCarryOver ??= [];
            newCarryOver.Add(batch);
            return;
        }

        coalescedBatches[coalescedCount] = batch;
        coalescedCount++;
    }

    /// <summary>
    /// Sends coalesced batches (one per partition) as a single ProduceRequest.
    /// The in-flight count was already incremented by the send loop before calling this method.
    /// </summary>
    private async ValueTask SendCoalescedAsync(
        ReadyBatch[] batches,
        int count,
        CancellationToken cancellationToken)
    {
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
                ReleaseInFlightSlot();
                for (var i = 0; i < count; i++)
                {
                    CleanupBatch(batches[i]);
                }

                ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
                return;
            }

            // Pipelined send: write request, get response task
            var responseTask = connection.SendPipelinedAsync<ProduceRequest, ProduceResponse>(
                request, (short)apiVersion, cancellationToken);

            // Fire-and-forget response handling — tracked in _inFlightResponses for clean shutdown.
            // We track the handler task (not the response task) so DisposeAsync waits for the
            // full response processing + retry lifecycle, not just the network response.
            var handlerTask = HandleResponseAsync(
                responseTask, batches, count, requestStartTime, cancellationToken);
            _inFlightResponses.TryAdd(handlerTask, 0);
            _ = handlerTask.ContinueWith(static (t, state) =>
            {
                ((ConcurrentDictionary<Task, byte>)state!).TryRemove(t, out _);
            }, _inFlightResponses, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            // Send failed — release everything
            ReleaseInFlightSlot();
            for (var i = 0; i < count; i++)
            {
                FailAndCleanupBatch(batches[i], ex);
            }

            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
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
        int count,
        long requestStartTime,
        CancellationToken cancellationToken)
    {
        var inFlightReleased = false;
        List<Task>? retryTasks = null;

        try
        {
            var response = await responseTask.ConfigureAwait(false);

            // Response received — release in-flight slot immediately so the send loop
            // can pipeline new requests while we process results.
            inFlightReleased = true;
            ReleaseInFlightSlot();

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
                    // No response — retriable. Mute the partition to prevent newer batches
                    // from being sent while the retry is in progress.
                    _logger?.LogWarning(
                        "[BrokerSender] No response for {Topic}-{Partition}",
                        expectedTopic, expectedPartition);
                    _mutedPartitions.TryAdd(batch.TopicPartition, 0);
                    retryTasks ??= [];
                    retryTasks.Add(ScheduleRetryAsync(batch, ErrorCode.NetworkException, cancellationToken));
                    batches[i] = null!; // Mark as handed off
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
                        // Mute the partition to prevent newer batches from jumping ahead.
                        _mutedPartitions.TryAdd(batch.TopicPartition, 0);
                        retryTasks ??= [];
                        retryTasks.Add(ScheduleRetryAsync(batch, partitionResponse.ErrorCode, cancellationToken));
                        batches[i] = null!;
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
                    CleanupBatch(batch);
                    batches[i] = null!;
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
                CleanupBatch(batch);
                batches[i] = null!;
            }
        }
        catch (Exception ex)
        {
            // Entire send failed — fail all remaining batches.
            // Unmute any partitions that were muted but whose retry wasn't started.
            _logger?.LogError(ex, "BrokerSender[{BrokerId}] response handling failed", _brokerId);

            for (var i = 0; i < count; i++)
            {
                if (batches[i] is null) continue; // Already handled
                _mutedPartitions.TryRemove(batches[i].TopicPartition, out _);
                FailAndCleanupBatch(batches[i], ex);
            }
        }
        finally
        {
            if (!inFlightReleased)
                ReleaseInFlightSlot();
            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);

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
    /// Schedules a retry for a batch: performs backoff/OOSN coordination, then re-enqueues the
    /// batch to the appropriate BrokerSender (self or rerouted). The partition is muted by the
    /// caller (HandleResponseAsync) before this method is invoked.
    /// This ensures retries go through the send loop's single-threaded path, preserving wire-order.
    /// Fire-and-forget from HandleResponseAsync — tracked in _inFlightResponses for clean shutdown.
    /// </summary>
    private async Task ScheduleRetryAsync(
        ReadyBatch batch,
        ErrorCode errorCode,
        CancellationToken cancellationToken)
    {
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
                UnmutePartition(batch.TopicPartition);
                FailAndCleanupBatch(batch,
                    new KafkaException(ErrorCode.LeaderNotAvailable,
                        $"No leader available for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}"));
                return;
            }

            if (leader.NodeId == _brokerId)
            {
                // Same broker — mark as retry and re-enqueue. The send loop will unmute the
                // partition when it coalesces this batch, ensuring it goes before newer batches.
                batch.IsRetry = true;
                Enqueue(batch);
            }
            else if (_rerouteBatch is not null)
            {
                // Leader moved to different broker — unmute at this broker and reroute.
                UnmutePartition(batch.TopicPartition);
                _rerouteBatch(batch);
            }
            else
            {
                // No reroute callback — mark as retry and re-enqueue to self (best effort)
                batch.IsRetry = true;
                Enqueue(batch);
            }
        }
        catch (Exception ex)
        {
            UnmutePartition(batch.TopicPartition);
            CompleteInflightEntry(batch);
            _logger?.LogError(ex, "[BrokerSender] Retry scheduling failed for {Topic}-{Partition}",
                batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            try { batch.Fail(ex); }
            catch { /* Observe */ }
            try { _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
                batch.CompletionSourcesCount, ex); }
            catch { /* Observe - must not prevent cleanup */ }
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

    /// <summary>
    /// Removes a partition from the muted set and signals the send loop.
    /// Called when a retry completes (reroute, permanent failure, or catch block).
    /// Idempotent: safe to call even if the partition was not muted.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UnmutePartition(TopicPartition tp)
    {
        _mutedPartitions.TryRemove(tp, out _);
        Interlocked.Exchange(ref _unmuteSignal, null)?.TrySetResult();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseInFlightSlot()
    {
        Interlocked.Decrement(ref _inFlightCount);
        Interlocked.Exchange(ref _inFlightSlotAvailable, null)?.TrySetResult();
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

        // Cancel CTS FIRST so WaitToReadAsync and
        // in-flight HandleResponseAsync/ScheduleRetryAsync calls are interrupted promptly.
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
    }
}
