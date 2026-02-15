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
internal sealed partial class BrokerSender : IAsyncDisposable
{
    private readonly int _brokerId;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly RecordAccumulator _accumulator;
    private readonly ProducerOptions _options;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly PartitionInflightTracker _inflightTracker;
    private readonly ProducerStatisticsCollector _statisticsCollector;
    private readonly Action<ReadyBatch>? _rerouteBatch;
    private readonly Action<TopicPartition, long, DateTimeOffset, int, Exception?>? _onAcknowledgement;
    private readonly Func<short, CancellationToken, ValueTask<(long ProducerId, short ProducerEpoch)>>? _bumpEpoch;
    private readonly Func<short>? _getCurrentEpoch;
    private readonly ILogger _logger;

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

    // Batches that failed during SendCoalescedAsync (connection error, etc.) and need retry.
    // Set by the catch block, consumed by the send loop at the top of each iteration.
    // Single-threaded: only accessed by the send loop.
    private readonly List<ReadyBatch> _sendFailedRetries = new();

    // Non-blocking in-flight request limiter (replaces SemaphoreSlim).
    // Aligned with Java Kafka's InFlightRequests.canSendMore(): the send loop checks
    // capacity before sending, and waits for a signal when at max. No blocking primitives.
    // _inFlightCount is only incremented by the single-threaded send loop;
    // decremented by the ContinueWith callback on the response task via Interlocked.
    private readonly int _maxInFlight;
    private int _inFlightCount;
    private TaskCompletionSource? _inFlightSlotAvailable;

    // Per-producer shared API version (read via volatile, written via Interlocked)
    private readonly Func<int> _getProduceApiVersion;
    private readonly Action<int> _setProduceApiVersion;

    // Transaction support
    private readonly Func<bool> _isTransactional;
    private readonly Func<TopicPartition, CancellationToken, ValueTask>? _ensurePartitionInTransaction;

    // Send-time muting: when true, partitions are muted at send time (limiting to 1
    // in-flight batch per partition). When false, multiple in-flight batches per partition
    // are allowed, relying on sequence numbers for ordering instead of muting.
    // Only enabled when MaxInFlight <= 1; idempotent producers with MaxInFlight > 1
    // use sequence numbers to guarantee ordering without send-time muting.
    private readonly bool _muteOnSend;

    // Muted partitions: partitions with a retry in progress or limited to 1 in-flight
    // batch (when _muteOnSend). Prevents newer batches from being sent, maintaining
    // per-partition ordering. Aligned with Java Kafka producer's mute mechanism.
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
        PartitionInflightTracker inflightTracker,
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
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        // Bounded channel limits pipeline depth between SenderLoop (drain) and BrokerSender (send).
        // Inspired by Java Kafka's Sender which drains and sends in a single thread (no intermediate
        // buffer). Here we allow MaxInFlightRequestsPerConnection × 2 batches in the channel so the
        // send loop always has work ready, while bounding the total in-transit data to prevent
        // unbounded memory growth when production rate exceeds TCP drain rate.
        _batchChannel = Channel.CreateBounded<ReadyBatch>(new BoundedChannelOptions(
            options.MaxInFlightRequestsPerConnection * 2)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _maxInFlight = options.MaxInFlightRequestsPerConnection;

        // Only mute at send time when limited to 1 in-flight request.
        // Idempotent producers with maxInFlight > 1 rely on sequence numbers for ordering.
        _muteOnSend = _maxInFlight <= 1;

        _cts = new CancellationTokenSource();
        _sendLoopTask = SendLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Enqueues a batch for sending to this broker.
    /// Fast path: TryWrite succeeds when the bounded channel has capacity.
    /// Returns a ValueTask that completes asynchronously when the channel is full,
    /// providing backpressure to the SenderLoop and bounding pipeline depth.
    /// </summary>
    public ValueTask EnqueueAsync(ReadyBatch batch, CancellationToken cancellationToken)
    {
        if (_batchChannel.Writer.TryWrite(batch))
            return ValueTask.CompletedTask;

        // Channel is either full (backpressure) or completed (disposal).
        // Use async write which will wait for capacity or throw ChannelClosedException.
        return EnqueueSlowAsync(batch, cancellationToken);
    }

    private async ValueTask EnqueueSlowAsync(ReadyBatch batch, CancellationToken cancellationToken)
    {
        try
        {
            await _batchChannel.Writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            FailEnqueuedBatch(batch);
        }
    }

    /// <summary>
    /// Synchronous enqueue for the reroute callback (called from BrokerSender's send loop
    /// when a retry discovers the leader moved). Uses TryWrite; if the channel is full,
    /// falls back to a background WriteAsync (rare — only when reroute targets a busy broker).
    /// </summary>
    public void Enqueue(ReadyBatch batch)
    {
        if (_batchChannel.Writer.TryWrite(batch))
            return;

        if (_cts.IsCancellationRequested)
        {
            FailEnqueuedBatch(batch);
            return;
        }

        // Channel full (rare during reroute) — fire-and-forget async write.
        // The write completes as soon as the send loop drains one batch.
        _ = _batchChannel.Writer.WriteAsync(batch, _cts.Token).AsTask().ContinueWith(
            static (task, state) =>
            {
                var (sender, b) = ((BrokerSender, ReadyBatch))state!;
                try { sender.FailEnqueuedBatch(b); }
                catch { /* Observe - disposal may have already cleaned up */ }
            },
            (this, batch),
            CancellationToken.None,
            TaskContinuationOptions.NotOnRanToCompletion,
            TaskScheduler.Default);
    }

    private void FailEnqueuedBatch(ReadyBatch batch)
    {
        if (batch.IsRetry)
        {
            batch.IsRetry = false;
            UnmutePartition(batch.TopicPartition);
        }
        CompleteInflightEntry(batch);
        batch.Fail(new ObjectDisposedException(nameof(BrokerSender)));
        CleanupBatch(batch);
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

        // Two swappable carry-over lists — eliminates per-iteration List<ReadyBatch> allocations.
        // pendingCarryOver holds batches from the previous iteration; newCarryOver receives batches
        // that can't be coalesced in this iteration. At the end of each iteration they swap roles.
        var carryOverA = new List<ReadyBatch>();
        var carryOverB = new List<ReadyBatch>();
        var pendingCarryOver = carryOverA;
        var reusableWaitTasks = new List<Task>(4);

        try
        {
            while (true)
            {
                // Must check cancellation at every iteration — when carry-over batches exist
                // with muted partitions (coalescedCount==0), the WhenAny path below completes
                // without propagating the OCE from the cancelled token.
                cancellationToken.ThrowIfCancellationRequested();

                LogSendLoopIteration(_brokerId, pendingCarryOver.Count, _pendingResponses.Count);

                // Pick up batches that failed during SendCoalescedAsync (connection errors, etc.)
                // and need retry. These are treated as carry-over for the next coalescing pass.
                if (_sendFailedRetries.Count > 0)
                {
                    pendingCarryOver.AddRange(_sendFailedRetries);
                    _sendFailedRetries.Clear();
                }

                // Process completed responses inline (like Java's NetworkClient.poll()).
                // Retry batches are added to pendingCarryOver in deterministic send order.
                ProcessCompletedResponses(pendingCarryOver, cancellationToken);

                // Wait for data: carry-over, channel, or pending responses
                if (pendingCarryOver.Count == 0)
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
                            // Fast path: if channel already has data, skip WhenAny allocation.
                            // WaitToReadAsync completes synchronously when data is buffered.
                            var channelWaitVt = channelReader.WaitToReadAsync(cancellationToken);
                            if (!channelWaitVt.IsCompleted)
                            {
                                await Task.WhenAny(channelWaitVt.AsTask(), responseSignal.Task).ConfigureAwait(false);
                            }
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
                        LogEpochBumpFailed(ex, staleEpoch);
                        // Flag stays set — retry next iteration.
                    }
                }

                // Coalesce: at most one batch per partition per request.
                // Carry-over first (preserves ordering), then channel (prevents starvation).
                coalescedPartitions.Clear();
                var newCarryOver = pendingCarryOver == carryOverA ? carryOverB : carryOverA;
                newCarryOver.Clear();
                var coalescedCount = 0;
                var coalescedBatches = ArrayPool<ReadyBatch>.Shared.Rent(maxCoalesce);

                // Scan carry-over: retry batches unmute and coalesce; muted/duplicate carry over.
                // No sorting needed — ProcessCompletedResponses adds retry batches in send order.
                var hadCarryOver = pendingCarryOver.Count > 0;
                if (hadCarryOver)
                {
                    for (var i = 0; i < pendingCarryOver.Count; i++)
                    {
                        CoalesceBatch(pendingCarryOver[i], coalescedBatches, ref coalescedCount,
                            coalescedPartitions, newCarryOver);
                    }

                    // Clear after scanning — prevents double-cleanup if an exception
                    // occurs before the swap at the end of the iteration.
                    pendingCarryOver.Clear();
                }

                // Scan channel (non-blocking). Two cases:
                // 1. No carry-over: normal fast path — read freely for maximum throughput.
                // 2. Carry-over was all muted (coalescedCount==0): read to find sendable batches
                //    — this prevents the starvation livelock.
                // When carry-over produced a coalesced batch, read at most 1 from channel
                // to prevent carry-over growth while still draining the channel gradually.
                // Without this limit, duplicate-partition batches (single-partition workloads)
                // would cause unbounded carry-over growth and O(n²) scanning.
                // With the limit of 1, carry-over growth is bounded by channel capacity
                // (MaxInFlightRequestsPerConnection × 2) and drains naturally.
                // No sorting needed — retry batches no longer come through the channel.
                {
                    var channelReadLimit = (hadCarryOver && coalescedCount > 0) ? 1 : maxCoalesce;
                    var channelReads = 0;
                    while (channelReads < channelReadLimit && channelReader.TryRead(out var channelBatch))
                    {
                        channelReads++;
                        CoalesceBatch(channelBatch, coalescedBatches, ref coalescedCount,
                            coalescedPartitions, newCarryOver);
                    }
                }

                // Sweep carry-over for expired batches. This prevents muted batches
                // from sitting indefinitely while their partition's retry cycles, and
                // ensures channel batches that were read above are deadline-checked.
                if (newCarryOver.Count > 0)
                    SweepExpiredCarryOver(newCarryOver);

                // Send or wait
                if (coalescedCount > 0)
                {
                    // Wait in place for in-flight capacity if needed.
                    // Double-check pattern: register signal, re-check condition, then wait.
                    var currentInFlight = Volatile.Read(ref _inFlightCount);
                    if (currentInFlight >= _maxInFlight)
                        LogWaitingForInFlightCapacity(_brokerId, currentInFlight, _maxInFlight);

                    while (Volatile.Read(ref _inFlightCount) >= _maxInFlight)
                    {
                        var waitSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        Volatile.Write(ref _inFlightSlotAvailable, waitSignal);

                        if (Volatile.Read(ref _inFlightCount) >= _maxInFlight)
                            await waitSignal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    Interlocked.Increment(ref _inFlightCount);
                    LogSendingCoalesced(_brokerId, coalescedCount);
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
                    reusableWaitTasks.Clear();

                    // Fast path: if channel already has data, skip wait entirely
                    var channelWaitVt2 = channelReader.WaitToReadAsync(cancellationToken);
                    if (channelWaitVt2.IsCompleted)
                    {
                        pendingCarryOver = newCarryOver;
                        continue;
                    }

                    reusableWaitTasks.Add(channelWaitVt2.AsTask());
                    reusableWaitTasks.Add(inFlightSignal.Task);

                    if (_pendingResponses.Count > 0)
                    {
                        // If any response is already completed, go back to the top of the loop
                        // immediately so ProcessCompletedResponses can handle it. Without this,
                        // we'd wait on channelWait + inFlightSignal which may never fire.
                        var anyCompleted = false;
                        for (var i = 0; i < _pendingResponses.Count; i++)
                        {
                            if (_pendingResponses[i].ResponseTask.IsCompleted)
                            {
                                anyCompleted = true;
                                break;
                            }
                        }

                        if (anyCompleted)
                        {
                            pendingCarryOver = newCarryOver;
                            continue;
                        }

                        var responseSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        Volatile.Write(ref _responseReadySignal, responseSignal);
                        reusableWaitTasks.Add(responseSignal.Task);
                    }

                    // Calculate earliest backoff and delivery deadline from carry-over
                    if (newCarryOver.Count > 0)
                    {
                        var earliestBackoff = long.MaxValue;
                        var earliestDeadlineTicks = long.MaxValue;
                        var now = Stopwatch.GetTimestamp();

                        for (var i = 0; i < newCarryOver.Count; i++)
                        {
                            if (newCarryOver[i].RetryNotBefore > 0 && newCarryOver[i].RetryNotBefore < earliestBackoff)
                                earliestBackoff = newCarryOver[i].RetryNotBefore;

                            var deadlineTicks = newCarryOver[i].StopwatchCreatedTicks +
                                (long)(_options.DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0));
                            if (deadlineTicks < earliestDeadlineTicks)
                                earliestDeadlineTicks = deadlineTicks;
                        }

                        if (earliestBackoff < long.MaxValue)
                        {
                            var delayTicks = earliestBackoff - now;
                            if (delayTicks > 0)
                            {
                                var delayMs = (int)(delayTicks * 1000.0 / Stopwatch.Frequency);
                                reusableWaitTasks.Add(Task.Delay(Math.Max(1, delayMs), cancellationToken));
                            }
                            // else: backoff already elapsed, will be processed next iteration
                        }

                        // Delivery deadline timer — ensures the loop wakes to expire
                        // timed-out batches even when no other signals fire.
                        if (earliestDeadlineTicks < long.MaxValue)
                        {
                            var delayTicks = earliestDeadlineTicks - now;
                            if (delayTicks > 0)
                            {
                                var delayMs = (int)(delayTicks * 1000.0 / Stopwatch.Frequency);
                                reusableWaitTasks.Add(Task.Delay(Math.Max(1, delayMs), cancellationToken));
                            }
                            // else: deadline already passed, will be swept next iteration
                        }
                    }

                    await Task.WhenAny(reusableWaitTasks).ConfigureAwait(false);
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
            LogSendLoopFailed(ex, _brokerId);
        }
        finally
        {
            // Fail any carry-over batches that couldn't be sent.
            // Drain both swappable lists — if an exception occurred mid-iteration,
            // batches may be in either list depending on timing.
            FailCarryOverBatches(carryOverA);
            FailCarryOverBatches(carryOverB);

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
        List<ReadyBatch> newCarryOver)
    {
        if (batch.IsRetry)
        {
            // Check backoff — carry over if backoff hasn't elapsed
            if (batch.RetryNotBefore > 0 && Stopwatch.GetTimestamp() < batch.RetryNotBefore)
            {
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
                    LogRetryRerouted(_brokerId, batch.TopicPartition.Topic, batch.TopicPartition.Partition, leader.NodeId);
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
            LogPartitionMuted(_brokerId, batch.TopicPartition.Topic, batch.TopicPartition.Partition);
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
    private void ProcessCompletedResponses(List<ReadyBatch> pendingCarryOver,
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

            // In-flight slot already released by ContinueWith in SendCoalescedAsync.

            if (pending.ResponseTask.IsFaulted || pending.ResponseTask.IsCanceled)
            {
                // Entire request failed (connection drop, timeout, etc.) — retry all batches.
                // Aligned with Java Kafka's Sender: request-level failures cause reenqueue,
                // not permanent failure. Batches retry until DeliveryTimeoutMs expires.
                var ex = pending.ResponseTask.Exception?.InnerException
                    ?? new OperationCanceledException();
                LogResponseFailed(ex, _brokerId);

                // Invalidate the pinned connection since it likely failed
                _pinnedConnection = null;

                for (var j = 0; j < pending.Count; j++)
                {
                    if (pending.Batches[j] is not null)
                    {
                        HandleRetriableBatch(pending.Batches[j], ErrorCode.NetworkException,
                            pendingCarryOver, cancellationToken);
                    }
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
                    LogNoResponseForPartition(expectedTopic, expectedPartition);
                    HandleRetriableBatch(batch, ErrorCode.NetworkException,
                        pendingCarryOver, cancellationToken);
                    pending.Batches[j] = null!;
                    continue;
                }

                if (partitionResponse.ErrorCode == ErrorCode.DuplicateSequenceNumber)
                {
                    LogDuplicateSequenceNumber(expectedTopic, expectedPartition, partitionResponse.BaseOffset);
                    // Treat as success — fall through
                }
                else if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    if (partitionResponse.ErrorCode.IsRetriable()
                        || partitionResponse.ErrorCode == ErrorCode.OutOfOrderSequenceNumber
                        || partitionResponse.ErrorCode == ErrorCode.InvalidProducerEpoch
                        || partitionResponse.ErrorCode == ErrorCode.UnknownProducerId)
                    {
                        LogRetriableError(partitionResponse.ErrorCode, expectedTopic, expectedPartition,
                            batch.RecordBatch.BaseSequence, batch.RecordBatch.Records.Count,
                            batch.RecordBatch.ProducerEpoch, batch.RecordBatch.ProducerId);

                        HandleRetriableBatch(batch, partitionResponse.ErrorCode,
                            pendingCarryOver, cancellationToken);
                        pending.Batches[j] = null!;
                        continue;
                    }

                    // Non-retriable error — unmute so subsequent batches can proceed
                    UnmutePartition(batch.TopicPartition);
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

                // Success — unmute so the next batch for this partition can be sent
                UnmutePartition(batch.TopicPartition);
                LogBatchCompleted(_brokerId, expectedTopic, expectedPartition, partitionResponse.BaseOffset);
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
        List<ReadyBatch> pendingCarryOver, CancellationToken cancellationToken)
    {
        // Check delivery deadline
        var deliveryDeadlineTicks = batch.StopwatchCreatedTicks +
            (long)(_options.DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0));

        if (Stopwatch.GetTimestamp() >= deliveryDeadlineTicks)
        {
            LogDeliveryTimeoutExceeded(_brokerId, batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            UnmutePartition(batch.TopicPartition);
            var ex = new TimeoutException(
                $"Delivery timeout exceeded for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}" +
                (errorCode != ErrorCode.None ? $" (last error: {errorCode})" : ""));
            FailAndCleanupBatch(batch, ex);
            return;
        }

        // Mute partition so no newer batches overtake the retry (ordering guarantee).
        _mutedPartitions.TryAdd(batch.TopicPartition, 0);

        var isEpochBumpError = errorCode is ErrorCode.OutOfOrderSequenceNumber
            or ErrorCode.InvalidProducerEpoch or ErrorCode.UnknownProducerId;

        if (isEpochBumpError && _bumpEpoch is not null)
        {
            LogEpochBumpSignaled(errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition,
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
            LogOosnTransactionalReenqueue(batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                batch.RecordBatch.BaseSequence);

            CompleteInflightEntry(batch);
            batch.InflightEntry = null;
        }
        else
        {
            LogRetriableErrorWithBackoff(errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                _options.RetryBackoffMs);

            // Fire-and-forget metadata refresh for leader changes
            _ = _metadataManager.RefreshMetadataAsync(
                [batch.TopicPartition.Topic], cancellationToken);

            // Set backoff via RetryNotBefore instead of Task.Delay
            batch.RetryNotBefore = Stopwatch.GetTimestamp() +
                (long)(_options.RetryBackoffMs * (Stopwatch.Frequency / 1000.0));
        }

        _statisticsCollector.RecordRetry();
        Diagnostics.DekafMetrics.Retries.Add(1,
            new System.Diagnostics.TagList { { Diagnostics.DekafDiagnostics.MessagingDestinationName, batch.TopicPartition.Topic } });

        // Add to carry-over — deterministic order since we process responses forward (FIFO)
        batch.IsRetry = true;
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
                    var tp = batch.TopicPartition;
                    var recordCount = batch.RecordBatch.Records.Count;
                    var isStaleEpoch = currentEpoch >= 0
                        && batch.RecordBatch.ProducerEpoch >= 0
                        && batch.RecordBatch.ProducerEpoch != currentEpoch;

                    if (isStaleEpoch)
                    {
                        // Stale epoch: complete old inflight, assign fresh sequence, update epoch/PID
                        LogStaleEpochResequencing(_brokerId, tp.Topic, tp.Partition,
                            batch.RecordBatch.ProducerEpoch, currentEpoch);
                        CompleteInflightEntry(batch);
                        var newSeq = _accumulator.GetAndIncrementSequence(tp, recordCount);
                        batch.RecordBatch.ProducerId = currentPid;
                        batch.RecordBatch.ProducerEpoch = currentEpoch;
                        batch.RecordBatch.BaseSequence = newSeq;

                        // Re-register with inflight tracker
                        batch.InflightEntry = _inflightTracker.Register(tp, newSeq, recordCount);
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

            // Release buffer memory now that data is written to the TCP buffer.
            // The untracked gap (between release and response) is bounded by
            // MaxInFlightRequestsPerConnection × BatchSize (e.g. 5 × 1MB = 5MB).
            // This is safe because: (1) the kernel has a copy in the TCP send buffer,
            // (2) the gap is bounded and small, (3) it unblocks producers waiting on
            // BufferMemory without the unbounded growth caused by drain-time release.
            // CleanupBatch still releases for error paths where TCP send wasn't reached.
            for (var i = 0; i < count; i++)
            {
                if (!batches[i].MemoryReleased)
                {
                    _accumulator.ReleaseMemory(batches[i].DataSize);
                    batches[i].MemoryReleased = true;
                }
            }

            _pendingResponses.Add(new PendingResponse(responseTask, batches, count, requestStartTime));

            // Mute partitions at send time when limited to 1 in-flight request.
            // This ensures at most one batch per partition in-flight across all requests.
            // When maxInFlight > 1, sequence numbers guarantee ordering instead,
            // and retry-time muting handles error recovery.
            if (_muteOnSend)
            {
                for (var i = 0; i < count; i++)
                    _mutedPartitions.TryAdd(batches[i].TopicPartition, 0);
            }

            LogPipelinedSend(_brokerId, count, _pendingResponses.Count);

            // Release the in-flight slot and signal the send loop when the response completes.
            // The slot MUST be released here (not in ProcessCompletedResponses) because the send loop
            // may be waiting for a slot inside the loop body (while _inFlightCount >= _maxInFlight),
            // and ProcessCompletedResponses only runs at the top of the loop — causing a deadlock.
            var self = this;
            _ = responseTask.ContinueWith(static (_, state) =>
            {
                var sender = (BrokerSender)state!;
                sender.ReleaseInFlightSlot();
                Volatile.Read(ref sender._responseReadySignal)?.TrySetResult();
            }, self, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown — fail batches permanently (no point retrying)
            ReleaseInFlightSlot();
            for (var i = 0; i < count; i++)
                FailAndCleanupBatch(batches[i], new ObjectDisposedException(nameof(BrokerSender)));

            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
        }
        catch (Exception ex)
        {
            // Send failed (connection error, etc.) — retry batches instead of permanently failing.
            // Aligned with Java Kafka's Sender: transient failures cause reenqueue for retry.
            ReleaseInFlightSlot();
            _pinnedConnection = null; // Invalidate broken connection
            LogResponseFailed(ex, _brokerId);

            for (var i = 0; i < count; i++)
            {
                var batch = batches[i];

                // Buffer memory stays reserved during retry — the batch still holds
                // physical memory (arena buffer). Release happens in CleanupBatch when
                // the batch finally completes (success or permanent failure).

                // Check delivery deadline before retrying
                var deliveryDeadlineTicks = batch.StopwatchCreatedTicks +
                    (long)(_options.DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0));

                if (Stopwatch.GetTimestamp() >= deliveryDeadlineTicks)
                {
                    UnmutePartition(batch.TopicPartition);
                    FailAndCleanupBatch(batch, new TimeoutException(
                        $"Delivery timeout exceeded for {batch.TopicPartition}"));
                }
                else
                {
                    // Mute partition (ordering guarantee) and queue for retry.
                    _mutedPartitions.TryAdd(batch.TopicPartition, 0);
                    batch.IsRetry = true;
                    batch.RetryNotBefore = Stopwatch.GetTimestamp() +
                        (long)(_options.RetryBackoffMs * (Stopwatch.Frequency / 1000.0));
                    _sendFailedRetries.Add(batch);
                    _statisticsCollector.RecordRetry();
                }
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
            _inflightTracker.Complete(entry);
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

    /// <summary>
    /// Sweeps carry-over for batches that have exceeded their delivery deadline.
    /// Prevents muted batches from sitting indefinitely while their partition's retry cycles.
    /// Called from the single-threaded send loop after coalescing.
    /// </summary>
    private void SweepExpiredCarryOver(List<ReadyBatch> carryOver)
    {
        var now = Stopwatch.GetTimestamp();
        for (var i = carryOver.Count - 1; i >= 0; i--)
        {
            var batch = carryOver[i];
            var deliveryDeadlineTicks = batch.StopwatchCreatedTicks +
                (long)(_options.DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0));

            if (now >= deliveryDeadlineTicks)
            {
                // Unmute partition for retry batches (they caused the mute).
                // Non-retry muted batches: don't unmute — the retry batch for this
                // partition may still be in play and will unmute on its own expiry.
                if (batch.IsRetry)
                {
                    batch.IsRetry = false;
                    batch.RetryNotBefore = 0;
                    UnmutePartition(batch.TopicPartition);
                }

                LogDeliveryTimeoutExceeded(_brokerId, batch.TopicPartition.Topic,
                    batch.TopicPartition.Partition);
                FailAndCleanupBatch(batch, new TimeoutException(
                    $"Delivery timeout exceeded for {batch.TopicPartition}"));
                carryOver.RemoveAt(i);
            }
        }
    }

    private void FailCarryOverBatches(List<ReadyBatch> carryOver)
    {
        for (var i = 0; i < carryOver.Count; i++)
        {
            CompleteInflightEntry(carryOver[i]);
            try { carryOver[i].Fail(new ObjectDisposedException(nameof(BrokerSender))); }
            catch { /* Observe */ }
            CleanupBatch(carryOver[i]);
        }
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
        // Release buffer memory at batch completion (matching Java's RecordAccumulator.deallocate()).
        // This is the primary release path: memory is held throughout the entire pipeline
        // (append → drain → send → response) to provide accurate end-to-end backpressure.
        if (!batch.MemoryReleased)
        {
            _accumulator.ReleaseMemory(batch.DataSize);
            batch.MemoryReleased = true;
        }
        _accumulator.ReturnReadyBatch(batch);
        _accumulator.OnBatchExitsPipeline();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        LogDisposing(_brokerId);

        // Complete channel — send loop will see channel completed and exit
        _batchChannel.Writer.Complete();

        // Cancel CTS FIRST so WaitToReadAsync is interrupted promptly.
        await _cts.CancelAsync().ConfigureAwait(false);

        // Wait for send loop to finish (should exit quickly now that CTS is cancelled).
        // The send loop owns _pendingResponses — it will process remaining responses
        // during its final iteration(s) before exiting.
        LogWaitingForSendLoop(_brokerId);
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
            LogFailingPendingResponses(_brokerId, _pendingResponses.Count);
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

    #region Logging

    [LoggerMessage(Level = LogLevel.Warning, Message = "[BrokerSender] Epoch bump failed for stale epoch {Epoch}, will retry next iteration")]
    private partial void LogEpochBumpFailed(Exception ex, int epoch);

    [LoggerMessage(Level = LogLevel.Error, Message = "BrokerSender[{BrokerId}] send loop failed")]
    private partial void LogSendLoopFailed(Exception ex, int brokerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "BrokerSender[{BrokerId}] response failed")]
    private partial void LogResponseFailed(Exception ex, int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[BrokerSender] No response for {Topic}-{Partition}")]
    private partial void LogNoResponseForPartition(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] DuplicateSequenceNumber for {Topic}-{Partition} at offset {Offset}")]
    private partial void LogDuplicateSequenceNumber(string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retriable error {ErrorCode} for {Topic}-{Partition} seq={Seq} count={Count} epoch={Epoch} pid={Pid}")]
    private partial void LogRetriableError(ErrorCode errorCode, string topic, int partition, int seq, int count, short epoch, long pid);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] {ErrorCode} for {Topic}-{Partition} seq={Seq}, signaling epoch bump to send loop")]
    private partial void LogEpochBumpSignaled(ErrorCode errorCode, string topic, int partition, int seq);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] OOSN for {Topic}-{Partition} seq={Seq}, re-enqueueing (transactional)")]
    private partial void LogOosnTransactionalReenqueue(string topic, int partition, int seq);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] Retriable error {ErrorCode} for {Topic}-{Partition}, retrying after {BackoffMs}ms")]
    private partial void LogRetriableErrorWithBackoff(ErrorCode errorCode, string topic, int partition, int backoffMs);

    [LoggerMessage(Level = LogLevel.Trace, Message = "BrokerSender[{BrokerId}] send loop iteration: {CarryOverCount} carry-over, {PendingResponseCount} pending responses")]
    private partial void LogSendLoopIteration(int brokerId, int carryOverCount, int pendingResponseCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] sending coalesced request: {CoalescedCount} batches")]
    private partial void LogSendingCoalesced(int brokerId, int coalescedCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] waiting for in-flight capacity ({InFlightCount}/{MaxInFlight})")]
    private partial void LogWaitingForInFlightCapacity(int brokerId, int inFlightCount, int maxInFlight);

    [LoggerMessage(Level = LogLevel.Trace, Message = "BrokerSender[{BrokerId}] batch completed: {Topic}-{Partition} at offset {Offset}")]
    private partial void LogBatchCompleted(int brokerId, string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Trace, Message = "BrokerSender[{BrokerId}] partition {Topic}-{Partition} muted, carrying over")]
    private partial void LogPartitionMuted(int brokerId, string topic, int partition);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] retry batch rerouted: {Topic}-{Partition} leader changed to broker {NewLeader}")]
    private partial void LogRetryRerouted(int brokerId, string topic, int partition, int newLeader);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender[{BrokerId}] delivery timeout exceeded for {Topic}-{Partition}")]
    private partial void LogDeliveryTimeoutExceeded(int brokerId, string topic, int partition);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] re-sequencing batch {Topic}-{Partition}: stale epoch {StaleEpoch} -> current {CurrentEpoch}")]
    private partial void LogStaleEpochResequencing(int brokerId, string topic, int partition, short staleEpoch, short currentEpoch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] pipelined send: {BatchCount} batches, {PendingResponseCount} pending responses")]
    private partial void LogPipelinedSend(int brokerId, int batchCount, int pendingResponseCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] disposing: cancelling send loop")]
    private partial void LogDisposing(int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] waiting for send loop to finish")]
    private partial void LogWaitingForSendLoop(int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender[{BrokerId}] failing {RemainingCount} pending responses during disposal")]
    private partial void LogFailingPendingResponses(int brokerId, int remainingCount);

    #endregion
}
