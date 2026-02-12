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
/// Epoch recovery for OutOfOrderSequenceNumber uses the Java Kafka Sender pattern:
/// response handlers signal a flag, and the single-threaded send loop bumps the epoch
/// before the next send. This eliminates all races between concurrent handlers.
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

    // Pending responses: send-loop owned (single-threaded). Responses are processed inline
    // in the send loop (like Java's NetworkClient.poll()), eliminating concurrent response
    // handler races that caused non-deterministic retry ordering.
    private readonly record struct PendingResponse(
        Task<ProduceResponse> ResponseTask,
        ReadyBatch[] Batches,
        int Count,
        long RequestStartTime);

    private readonly List<PendingResponse> _pendingResponses = new();
    private TaskCompletionSource? _responseReadySignal;

    // Non-blocking in-flight request limiter (replaces SemaphoreSlim).
    // Aligned with Java Kafka's InFlightRequests.canSendMore(): the send loop checks
    // capacity before sending, and waits for a signal when at max. No blocking primitives.
    // _inFlightCount is only incremented by the single-threaded send loop;
    // decremented by ProcessCompletedResponses in the send loop via Interlocked.
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
    // Written and read by the single-threaded send loop (ProcessCompletedResponses/CoalesceBatch).
    private readonly ConcurrentDictionary<TopicPartition, byte> _mutedPartitions = new();

    // Signalled when a partition is unmuted so the send loop can wake up,
    // without polling (avoids busy-wait when all carry-over partitions are muted).
    private TaskCompletionSource? _unmuteSignal;

    // Epoch bump recovery flag (Java Kafka Sender pattern): set by response handlers
    // when OutOfOrderSequenceNumber is received. The single-threaded send loop checks
    // this before coalescing and bumps the epoch if needed, eliminating all races
    // between concurrent response handlers. Value is the stale epoch (-1 = no bump needed).
    private int _epochBumpRequestedForEpoch = -1;

    // Pinned connection: the send loop reuses a single connection to preserve wire order.
    // With multiple connections per broker (round-robin pool), requests on different
    // TCP connections can arrive at the broker out of order, causing OOSN. By pinning
    // one connection, all produce requests are pipelined on the same TCP stream.
    private IKafkaConnection? _pinnedConnection;

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
        // Retry batches from ProcessCompletedResponses are added here in deterministic order.
        List<ReadyBatch>? pendingCarryOver = null;

        try
        {
            while (true)
            {
                // Must check cancellation at every iteration — when carry-over batches exist
                // with muted partitions (coalescedCount==0), the WhenAny path below completes
                // without propagating the OCE from the cancelled token.
                cancellationToken.ThrowIfCancellationRequested();

                // Process completed responses inline (like Java's NetworkClient.poll()).
                // Retry batches are added to pendingCarryOver in deterministic send order.
                ProcessCompletedResponses(ref pendingCarryOver, cancellationToken);

                // Wait for data: carry-over, channel, or pending responses
                if (pendingCarryOver is not { Count: > 0 })
                {
                    if (_pendingResponses.Count == 0)
                    {
                        // Nothing pending — block on channel
                        if (!await channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                            break;
                    }
                    else
                    {
                        // Responses pending — wait for channel OR response completion
                        var responseSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        Volatile.Write(ref _responseReadySignal, responseSignal);

                        // Double-check after signal registration to avoid missed wake-up
                        var anyCompleted = false;
                        for (var i = 0; i < _pendingResponses.Count; i++)
                        {
                            if (_pendingResponses[i].ResponseTask.IsCompleted)
                            {
                                anyCompleted = true;
                                break;
                            }
                        }

                        if (!anyCompleted)
                        {
                            var channelWait = channelReader.WaitToReadAsync(cancellationToken).AsTask();
                            await Task.WhenAny(channelWait, responseSignal.Task).ConfigureAwait(false);
                        }
                    }
                }

                // Register signals BEFORE coalescing checks. This prevents a race where
                // a signal fires between our state checks and the wait, which would miss
                // the notification. In the fast path, signals are harmlessly orphaned.
                var inFlightSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _inFlightSlotAvailable, inFlightSignal);
                var unmuteSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _unmuteSignal, unmuteSignal);

                // Epoch bump recovery (Java Kafka Sender pattern): if ProcessCompletedResponses
                // flagged an epoch-bump error, bump the epoch here in the single-threaded
                // send loop. This guarantees the bump completes BEFORE any batch is sent.
                // SendCoalescedAsync's stale-epoch check then re-sequences all batches.
                var staleEpoch = Volatile.Read(ref _epochBumpRequestedForEpoch);
                if (staleEpoch >= 0 && _bumpEpoch is not null)
                {
                    try
                    {
                        var currentEpoch = _getCurrentEpoch?.Invoke() ?? -1;
                        if (currentEpoch >= 0 && currentEpoch <= (short)staleEpoch)
                        {
                            await _bumpEpoch((short)staleEpoch, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        // Clear only the epoch we attempted. If a new epoch was requested
                        // concurrently, the CAS fails and the flag stays set for next iteration.
                        Interlocked.CompareExchange(ref _epochBumpRequestedForEpoch, -1, staleEpoch);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex,
                            "[BrokerSender] Epoch bump failed for stale epoch {Epoch}, will retry next iteration",
                            staleEpoch);
                        // Flag stays set — retry next iteration.
                    }
                }

                // Coalesce: at most one batch per partition per request.
                // Carry-over first (preserves ordering), then channel (prevents starvation).
                coalescedPartitions.Clear();
                List<ReadyBatch>? newCarryOver = null;
                var coalescedCount = 0;
                var coalescedBatches = ArrayPool<ReadyBatch>.Shared.Rent(maxCoalesce);

                // Scan carry-over: retry batches unmute and coalesce; muted/duplicate carry over.
                // No sorting needed — ProcessCompletedResponses adds retry batches in send order.
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
                // 2. Carry-over was all muted (coalescedCount==0): read to find sendable batches
                //    — this prevents the starvation livelock.
                // When carry-over produced a coalesced batch, skip channel reads to prevent
                // carry-over growth. Carry-over drains by 1 per iteration; reading more from
                // the channel (duplicate-partition for single-partition workloads) would cause
                // unbounded growth and O(n²) scanning.
                // No sorting needed — retry batches no longer come through the channel.
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

                    // All batches are for muted partitions or in backoff. Wait for either:
                    // 1. A response completes (may produce carry-over retry batches), or
                    // 2. New channel data arrives, or
                    // 3. An in-flight slot opens, or
                    // 4. Retry backoff elapses (earliest RetryNotBefore from carry-over).
                    var waitTasks = new List<Task>(4);
                    waitTasks.Add(channelReader.WaitToReadAsync(cancellationToken).AsTask());
                    waitTasks.Add(inFlightSignal.Task);

                    if (_pendingResponses.Count > 0)
                    {
                        var responseSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        Volatile.Write(ref _responseReadySignal, responseSignal);

                        var anyCompleted = false;
                        for (var i = 0; i < _pendingResponses.Count; i++)
                        {
                            if (_pendingResponses[i].ResponseTask.IsCompleted)
                            {
                                anyCompleted = true;
                                break;
                            }
                        }

                        if (!anyCompleted)
                            waitTasks.Add(responseSignal.Task);
                    }

                    // Calculate earliest backoff from carry-over
                    if (newCarryOver is { Count: > 0 })
                    {
                        var earliestBackoff = long.MaxValue;
                        for (var i = 0; i < newCarryOver.Count; i++)
                        {
                            if (newCarryOver[i].RetryNotBefore > 0 && newCarryOver[i].RetryNotBefore < earliestBackoff)
                                earliestBackoff = newCarryOver[i].RetryNotBefore;
                        }

                        if (earliestBackoff < long.MaxValue)
                        {
                            var delayTicks = earliestBackoff - Stopwatch.GetTimestamp();
                            if (delayTicks > 0)
                            {
                                var delayMs = (int)(delayTicks * 1000.0 / Stopwatch.Frequency);
                                waitTasks.Add(Task.Delay(Math.Max(1, delayMs), cancellationToken));
                            }
                            // else: backoff already elapsed, will be processed next iteration
                        }
                    }

                    await Task.WhenAny(waitTasks).ConfigureAwait(false);
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
            // Check backoff — carry over if backoff hasn't elapsed
            if (batch.RetryNotBefore > 0 && Stopwatch.GetTimestamp() < batch.RetryNotBefore)
            {
                newCarryOver ??= [];
                newCarryOver.Add(batch);
                return;
            }

            // Check if leader changed (synchronous cache check — no async needed)
            if (_rerouteBatch is not null)
            {
                var leader = _metadataManager.TryGetCachedPartitionLeader(
                    batch.TopicPartition.Topic, batch.TopicPartition.Partition);
                if (leader is not null && leader.NodeId != _brokerId)
                {
                    // Leader moved to different broker — unmute and reroute
                    batch.IsRetry = false;
                    batch.RetryNotBefore = 0;
                    UnmutePartition(batch.TopicPartition);
                    _rerouteBatch(batch);
                    return;
                }
            }

            // Retry batch: unmute the partition and coalesce it ahead of newer batches.
            batch.IsRetry = false;
            batch.RetryNotBefore = 0;
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
    /// Processes completed response tasks inline in the send loop (like Java's NetworkClient.poll()).
    /// Iterates _pendingResponses forward (preserves send order for retry batches) and compacts
    /// in-place. Retry batches are added to pendingCarryOver in deterministic order — no concurrent
    /// re-enqueue race possible since this runs in the single-threaded send loop.
    /// </summary>
    private void ProcessCompletedResponses(ref List<ReadyBatch>? pendingCarryOver,
        CancellationToken cancellationToken)
    {
        if (_pendingResponses.Count == 0) return;

        var writeIndex = 0;
        for (var i = 0; i < _pendingResponses.Count; i++)
        {
            var pending = _pendingResponses[i];
            if (!pending.ResponseTask.IsCompleted)
            {
                // Not yet complete — keep in list (compact)
                _pendingResponses[writeIndex++] = pending;
                continue;
            }

            // Response complete — release in-flight slot immediately
            ReleaseInFlightSlot();

            if (pending.ResponseTask.IsFaulted || pending.ResponseTask.IsCanceled)
            {
                // Entire request failed — fail all batches
                var ex = pending.ResponseTask.Exception?.InnerException
                    ?? new OperationCanceledException();
                _logger?.LogError(ex, "BrokerSender[{BrokerId}] response failed", _brokerId);

                for (var j = 0; j < pending.Count; j++)
                {
                    if (pending.Batches[j] is not null)
                        FailAndCleanupBatch(pending.Batches[j], ex);
                }

                ArrayPool<ReadyBatch>.Shared.Return(pending.Batches, clearArray: true);
                continue;
            }

            // Success — process per-partition results
            var response = pending.ResponseTask.Result;
            var elapsedTicks = Stopwatch.GetTimestamp() - pending.RequestStartTime;
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
            for (var j = 0; j < pending.Count; j++)
            {
                var batch = pending.Batches[j];
                var expectedTopic = batch.TopicPartition.Topic;
                var expectedPartition = batch.TopicPartition.Partition;

                ProduceResponsePartitionData? partitionResponse = null;
                responseLookup?.TryGetValue((expectedTopic, expectedPartition), out partitionResponse);

                if (partitionResponse is null)
                {
                    // No response — treat as retriable
                    _logger?.LogWarning(
                        "[BrokerSender] No response for {Topic}-{Partition}",
                        expectedTopic, expectedPartition);
                    HandleRetriableBatch(batch, ErrorCode.NetworkException,
                        ref pendingCarryOver, cancellationToken);
                    pending.Batches[j] = null!;
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
                        _logger?.LogDebug(
                            "Retriable error {ErrorCode} for {Topic}-{Partition} seq={Seq} count={Count} epoch={Epoch} pid={Pid}",
                            partitionResponse.ErrorCode, expectedTopic, expectedPartition,
                            batch.RecordBatch.BaseSequence, batch.RecordBatch.Records.Count,
                            batch.RecordBatch.ProducerEpoch, batch.RecordBatch.ProducerId);

                        HandleRetriableBatch(batch, partitionResponse.ErrorCode,
                            ref pendingCarryOver, cancellationToken);
                        pending.Batches[j] = null!;
                        continue;
                    }

                    // Non-retriable error
                    CompleteInflightEntry(batch);
                    _statisticsCollector.RecordBatchFailed(expectedTopic, expectedPartition,
                        batch.CompletionSourcesCount);
                    try
                    {
                        batch.Fail(new KafkaException(partitionResponse.ErrorCode,
                            $"Produce failed: {partitionResponse.ErrorCode}"));
                    }
                    catch { /* Observe */ }
                    try
                    {
                        _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
                            batch.CompletionSourcesCount,
                            new KafkaException(partitionResponse.ErrorCode,
                                $"Produce failed: {partitionResponse.ErrorCode}"));
                    }
                    catch { /* Observe - must not prevent cleanup */ }
                    CleanupBatch(batch);
                    pending.Batches[j] = null!;
                    continue;
                }

                // Success
                _statisticsCollector.RecordBatchDelivered(expectedTopic, expectedPartition,
                    batch.CompletionSourcesCount);
                var timestamp = partitionResponse.LogAppendTimeMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(partitionResponse.LogAppendTimeMs)
                    : DateTimeOffset.UtcNow;
                CompleteInflightEntry(batch);
                batch.CompleteSend(partitionResponse.BaseOffset, timestamp);
                try
                {
                    _onAcknowledgement?.Invoke(batch.TopicPartition,
                        partitionResponse.BaseOffset, timestamp,
                        batch.CompletionSourcesCount, null);
                }
                catch { /* Observe - must not prevent cleanup */ }
                CleanupBatch(batch);
                pending.Batches[j] = null!;
            }

            ArrayPool<ReadyBatch>.Shared.Return(pending.Batches, clearArray: true);
        }

        // Compact the list
        _pendingResponses.RemoveRange(writeIndex, _pendingResponses.Count - writeIndex);
    }

    /// <summary>
    /// Handles a single batch that received a retriable error. Checks delivery timeout,
    /// mutes the partition, signals epoch bump if needed, sets backoff, and adds to carry-over.
    /// Called inline from ProcessCompletedResponses in the single-threaded send loop.
    /// </summary>
    private void HandleRetriableBatch(ReadyBatch batch, ErrorCode errorCode,
        ref List<ReadyBatch>? pendingCarryOver, CancellationToken cancellationToken)
    {
        // Check delivery deadline
        var deliveryDeadlineTicks = batch.StopwatchCreatedTicks +
            (long)(_options.DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0));

        if (Stopwatch.GetTimestamp() >= deliveryDeadlineTicks)
        {
            var ex = new TimeoutException(
                $"Delivery timeout exceeded for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}" +
                (errorCode != ErrorCode.None ? $" (last error: {errorCode})" : ""));
            FailAndCleanupBatch(batch, ex);
            return;
        }

        // Mute partition
        _mutedPartitions.TryAdd(batch.TopicPartition, 0);

        var isEpochBumpError = errorCode is ErrorCode.OutOfOrderSequenceNumber
            or ErrorCode.InvalidProducerEpoch or ErrorCode.UnknownProducerId;

        if (isEpochBumpError && _bumpEpoch is not null)
        {
            _logger?.LogDebug(
                "[BrokerSender] {ErrorCode} for {Topic}-{Partition} seq={Seq}, signaling epoch bump to send loop",
                errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                batch.RecordBatch.BaseSequence);

            // Signal the send loop to bump the epoch
            Interlocked.CompareExchange(ref _epochBumpRequestedForEpoch,
                (int)batch.RecordBatch.ProducerEpoch, -1);

            // Complete inflight entry — will be re-registered after epoch bump
            CompleteInflightEntry(batch);
        }
        else if (isEpochBumpError && _bumpEpoch is null
            && batch.InflightEntry is not null
            && _inflightTracker is not null)
        {
            // Transactional producer — no epoch bump
            _logger?.LogDebug(
                "[BrokerSender] OOSN for {Topic}-{Partition} seq={Seq}, re-enqueueing (transactional)",
                batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                batch.RecordBatch.BaseSequence);

            CompleteInflightEntry(batch);
            batch.InflightEntry = null;
        }
        else
        {
            _logger?.LogDebug(
                "[BrokerSender] Retriable error {ErrorCode} for {Topic}-{Partition}, retrying after {BackoffMs}ms",
                errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                _options.RetryBackoffMs);

            // Fire-and-forget metadata refresh for leader changes
            _ = _metadataManager.RefreshMetadataAsync(
                [batch.TopicPartition.Topic], cancellationToken);

            // Set backoff via RetryNotBefore instead of Task.Delay
            batch.RetryNotBefore = Stopwatch.GetTimestamp() +
                (long)(_options.RetryBackoffMs * (Stopwatch.Frequency / 1000.0));
        }

        _statisticsCollector.RecordRetry();

        // Add to carry-over — deterministic order since we process responses forward (FIFO)
        batch.IsRetry = true;
        pendingCarryOver ??= [];
        pendingCarryOver.Add(batch);
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
            // Assign sequences at send time (Java Kafka Sender pattern).
            // All sequence assignment happens here in the single-threaded send loop,
            // eliminating the race between the accumulator's seal thread and the send
            // loop during epoch bump recovery. Previously, sequences were assigned during
            // PartitionBatch.Seal() on the producer thread, which raced with
            // ResetSequenceNumbers() called inside BumpEpochAsync — both threads called
            // GetAndIncrementSequence on the same shared counter, causing sequence
            // conflicts that led to OutOfOrderSequenceNumber errors.
            {
                var currentEpoch = _getCurrentEpoch?.Invoke() ?? (short)-1;
                var currentPid = currentEpoch >= 0 ? _accumulator.ProducerId : -1L;

                for (var i = 0; i < count; i++)
                {
                    var batch = batches[i];
                    if (batch.RecordBatch.ProducerId < 0)
                        continue; // Non-idempotent batch — no sequence tracking

                    var tp = batch.TopicPartition;
                    var recordCount = batch.RecordBatch.Records.Count;
                    var isStaleEpoch = currentEpoch >= 0
                        && batch.RecordBatch.ProducerEpoch >= 0
                        && batch.RecordBatch.ProducerEpoch != currentEpoch;

                    if (isStaleEpoch)
                    {
                        // Stale epoch: complete old inflight, assign fresh sequence, update epoch/PID
                        CompleteInflightEntry(batch);
                        var newSeq = _accumulator.GetAndIncrementSequence(tp, recordCount);
                        batch.RecordBatch.ProducerId = currentPid;
                        batch.RecordBatch.ProducerEpoch = currentEpoch;
                        batch.RecordBatch.BaseSequence = newSeq;

                        // Re-register with inflight tracker
                        if (_inflightTracker is not null)
                        {
                            batch.InflightEntry = _inflightTracker.Register(tp, newSeq, recordCount);
                        }
                    }
                    else if (batch.RecordBatch.BaseSequence < 0)
                    {
                        // Fresh batch: assign sequence (epoch/PID are already correct)
                        var newSeq = _accumulator.GetAndIncrementSequence(tp, recordCount);
                        batch.RecordBatch.BaseSequence = newSeq;
                    }
                    else
                    {
                        // Retry batch with correct epoch — keeps its original sequence
                    }
                }
            }

            // Register fresh batches with inflight tracker at send time (not drain time).
            // Stale batches were re-registered above. Retry batches with correct epoch
            // keep their existing inflight entries. Only batches without entries need registration.
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

            var connection = await GetPinnedConnectionAsync(cancellationToken)
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

            // Pipelined send: write request, get response task.
            // Add to _pendingResponses for the send loop to process inline (like Java's client.poll()).
            // No fire-and-forget — responses are processed in the single-threaded send loop,
            // making retry ordering deterministic by construction.
            var responseTask = connection.SendPipelinedAsync<ProduceRequest, ProduceResponse>(
                request, (short)apiVersion, cancellationToken);

            _pendingResponses.Add(new PendingResponse(responseTask, batches, count, requestStartTime));

            // Signal the send loop when the response completes so it can wake up from WaitToReadAsync.
            var self = this;
            _ = responseTask.ContinueWith(static (_, state) =>
                Volatile.Read(ref ((BrokerSender)state!)._responseReadySignal)?.TrySetResult(),
                self, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
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

    /// <summary>
    /// Returns the pinned connection for this BrokerSender. The send loop reuses a single
    /// connection to ensure all produce requests are pipelined on the same TCP stream.
    /// With round-robin connection pools (ConnectionsPerBroker > 1), using different connections
    /// per request causes the broker to process requests out of order, leading to OOSN.
    /// </summary>
    private async ValueTask<IKafkaConnection> GetPinnedConnectionAsync(CancellationToken cancellationToken)
    {
        var conn = _pinnedConnection;
        if (conn is not null && conn.IsConnected)
            return conn;

        conn = await _connectionPool.GetConnectionAsync(_brokerId, cancellationToken)
            .ConfigureAwait(false);
        _pinnedConnection = conn;
        return conn;
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

        // Cancel CTS FIRST so WaitToReadAsync is interrupted promptly.
        await _cts.CancelAsync().ConfigureAwait(false);

        // Wait for send loop to finish (should exit quickly now that CTS is cancelled).
        // The send loop owns _pendingResponses — it will process remaining responses
        // during its final iteration(s) before exiting.
        try
        {
            await _sendLoopTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Best effort — send loop didn't exit in time
        }

        // Fail any remaining pending responses that the send loop didn't process
        if (_pendingResponses.Count > 0)
        {
            var responseTasks = new Task[_pendingResponses.Count];
            for (var i = 0; i < _pendingResponses.Count; i++)
                responseTasks[i] = _pendingResponses[i].ResponseTask;
            try
            {
                await Task.WhenAll(responseTasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch { /* Best effort */ }

            foreach (var pr in _pendingResponses)
            {
                for (var j = 0; j < pr.Count; j++)
                {
                    if (pr.Batches[j] is not null)
                        FailAndCleanupBatch(pr.Batches[j], new ObjectDisposedException(nameof(BrokerSender)));
                }

                ArrayPool<ReadyBatch>.Shared.Return(pr.Batches, clearArray: true);
            }

            _pendingResponses.Clear();
        }

        _cts.Dispose();
    }
}
