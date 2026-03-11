using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Statistics;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

/// <summary>
/// Per-broker sender that serializes all writes to a single broker connection.
/// A single-threaded send loop drains events from a unified channel, coalesces batches into
/// ProduceRequests, and sends them via pipelined writes. This guarantees wire-order
/// for same-partition batches.
///
/// All wake-up sources (new batches, response completions, partition unmutes) flow through
/// a single <see cref="_eventChannel"/> — like Java Kafka's Sender.poll() model. Response
/// tasks complete and signal the channel via lightweight <c>ContinueWith</c> wake-ups;
/// the send loop then polls <c>_pendingResponses</c> for completed tasks (like main's
/// ProcessCompletedResponses). This avoids cross-thread reference sharing of batches arrays.
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
/// In-flight request limiting uses <see cref="_pendingResponses"/> count (exclusively owned
/// by the send loop) as the in-flight measure. Completed responses are processed by polling
/// <c>_pendingResponses</c> for completed tasks on each iteration.
///
/// All writes go through the send loop — there are no out-of-loop write paths.
/// </summary>
/// <remarks>
/// <para><b>Thread model:</b></para>
/// <para>
/// The core of BrokerSender is the <see cref="SendLoopAsync"/> method, which runs on a single
/// dedicated thread (Task). This send loop owns and exclusively accesses the following mutable state:
/// <see cref="_pendingResponses"/>, <see cref="_sendFailedRetries"/>, carry-over batch lists,
/// coalesced batch arrays, and <see cref="_pinnedConnection"/>. No locks are needed for these
/// because they are only ever touched by the send loop thread.
/// </para>
/// <para>
/// Response completion is detected by polling <c>_pendingResponses</c> for completed tasks
/// (checking <c>ResponseTask.IsCompleted</c>). <c>ContinueWith</c> callbacks write lightweight
/// <see cref="SendLoopEvent.ResponseReady"/> signals to wake up the send loop when responses
/// arrive. In-flight capacity is measured by <c>_pendingResponses.Count</c>.
/// </para>
/// <para>
/// External threads (producer callers) interact with BrokerSender only through the unbounded
/// <see cref="_eventChannel"/> (lock-free channel). Backpressure is provided by BufferMemory,
/// not channel bounding.
/// </para>
/// <para><b>Epoch bump synchronization:</b></para>
/// <para>
/// When a response handler (running inline in <see cref="ProcessCompletedResponses"/>) encounters
/// an <c>OutOfOrderSequenceNumber</c>, <c>InvalidProducerEpoch</c>, or <c>UnknownProducerId</c>
/// error, it signals the need for an epoch bump by writing the stale epoch value into
/// <see cref="_epochBumpRequestedForEpoch"/> via <c>Interlocked.CompareExchange</c> (CAS from -1
/// to the stale epoch). The send loop checks this field before coalescing, using
/// <c>Volatile.Read</c> and, if set, performs the epoch bump before coalescing any batches.
/// After a successful bump, the flag is cleared via <c>Interlocked.CompareExchange</c> (CAS from
/// stale epoch back to -1). If a new epoch error arrives concurrently, the CAS fails and the flag
/// remains set for the next iteration.
/// </para>
/// <para>
/// Memory ordering for <see cref="_epochBumpRequestedForEpoch"/>: All accesses use
/// <c>Interlocked</c> operations (CompareExchange) or <c>Volatile.Read</c>, which provide
/// acquire/release semantics on x86/x64 and explicit memory barriers on ARM.
/// </para>
/// </remarks>
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

    private readonly Channel<SendLoopEvent> _eventChannel;
    private readonly CancellationTokenSourcePool _pollTimeoutCtsPool = new();
    private readonly Task _sendLoopTask;
    private readonly CancellationTokenSource _cts;

    // Pending responses: send-loop owned (single-threaded). Entries are added when a pipelined
    // request is sent and removed by ProcessCompletedResponses when the response task completes.
    // SweepPendingResponseTimeouts is a safety net for the edge case where response tasks hang
    // past the delivery timeout.
    private readonly record struct PendingResponse(
        Task<ProduceResponse> ResponseTask,
        ReadyBatch[] Batches,
        int Count,
        long RequestStartTime);

    private enum SendLoopEventType : byte
    {
        NewBatch,
        ResponseReady, // Lightweight signal: a response task completed, poll _pendingResponses
        Unmute
    }

    [StructLayout(LayoutKind.Auto)]
    private struct SendLoopEvent
    {
        public SendLoopEventType Type;
        public ReadyBatch? Batch;

        public static SendLoopEvent NewBatch(ReadyBatch batch) => new()
        {
            Type = SendLoopEventType.NewBatch,
            Batch = batch
        };

        public static SendLoopEvent ResponseReady() => new()
        {
            Type = SendLoopEventType.ResponseReady
        };

        public static SendLoopEvent Unmute() => new()
        {
            Type = SendLoopEventType.Unmute
        };
    }

    private readonly List<PendingResponse> _pendingResponses = new();

    // Batches that failed during SendCoalescedAsync (connection error, etc.) and need retry.
    // Set by the catch block, consumed by the send loop at the top of each iteration.
    // Single-threaded: only accessed by the send loop.
    private readonly List<ReadyBatch> _sendFailedRetries = new();

    // Non-blocking in-flight request limiter. The send loop uses _pendingResponses.Count
    // (which it exclusively owns) as the in-flight measure. No cross-thread signaling needed.
    private readonly int _maxInFlight;

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
    // Exclusively owned by the single-threaded send loop — no concurrent access.
    private readonly HashSet<TopicPartition> _mutedPartitions = new();

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

        _eventChannel = Channel.CreateUnbounded<SendLoopEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _maxInFlight = options.MaxInFlightRequestsPerConnection;

        // Only mute at send time when limited to 1 in-flight request.
        // Idempotent producers with maxInFlight > 1 rely on sequence numbers for ordering.
        _muteOnSend = _maxInFlight <= 1;

        _cts = new CancellationTokenSource();
        _sendLoopTask = SendLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Returns true if the send loop is still running. When false, this BrokerSender
    /// should be replaced — its send loop has exited and it can no longer process batches.
    /// </summary>
    internal bool IsAlive => !_sendLoopTask.IsCompleted;

    /// <summary>
    /// Enqueues a batch for sending to this broker.
    /// TryWrite on the unbounded event channel always succeeds unless the channel is completed
    /// (send loop exited). BufferMemory provides the backpressure — the channel does not need bounding.
    /// </summary>
    public ValueTask EnqueueAsync(ReadyBatch batch, CancellationToken cancellationToken)
    {
        if (_eventChannel.Writer.TryWrite(SendLoopEvent.NewBatch(batch)))
            return ValueTask.CompletedTask;

        FailEnqueuedBatch(batch);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Synchronous enqueue for the reroute callback (called from BrokerSender's send loop
    /// when a retry discovers the leader moved). TryWrite on unbounded channel always succeeds
    /// unless the channel is completed.
    /// </summary>
    public void Enqueue(ReadyBatch batch)
    {
        if (!_eventChannel.Writer.TryWrite(SendLoopEvent.NewBatch(batch)))
            FailEnqueuedBatch(batch);
    }

    private void FailEnqueuedBatch(ReadyBatch batch)
    {
#if DEBUG
        batch.DebugLastTransition = (int)BatchTransition.FailEnqueuedBatch;
        batch.DebugLastBrokerId = _brokerId;
        Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} FailEnqueuedBatch broker={_brokerId}");
#endif
        if (batch.IsRetry)
        {
            batch.IsRetry = false;
            UnmutePartition(batch.TopicPartition);
        }
        FailAndCleanupBatch(batch, new ObjectDisposedException(nameof(BrokerSender)));
    }

    /// <summary>
    /// Main send loop: drains events from the unified channel, coalesces batches by partition,
    /// and sends pipelined requests. Single-threaded: coalescing is deterministically FIFO.
    ///
    /// All wake-up sources (new batches, response completions, partition unmutes) flow through
    /// <see cref="_eventChannel"/>. The loop drains all available events, processes responses
    /// inline, then waits on a single <c>WaitToReadAsync</c> with a computed timeout — like
    /// Java Kafka's Sender.poll() model.
    /// </summary>
    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        var eventReader = _eventChannel.Reader;
        var maxCoalesce = _options.MaxInFlightRequestsPerConnection * 4;

        var coalescedPartitions = new HashSet<TopicPartition>();
        var carryOverA = new List<ReadyBatch>();
        var carryOverB = new List<ReadyBatch>();
        var pendingCarryOver = carryOverA;

        ReadyBatch[]? coalescedBatches = null;
        var coalescedCount = 0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                LogSendLoopIteration(_brokerId, pendingCarryOver.Count, _pendingResponses.Count);

                // ── 1. Poll pending responses (like Java's client.poll()) ──
                // Signal events (ResponseReady, Unmute) may have woken us up — processing
                // completed responses here handles them. Batch events stay in the channel
                // and are read lazily during coalescing (step 5) to avoid O(n²) carry-over growth.
                ProcessCompletedResponses(pendingCarryOver, cancellationToken);

                // ── 2. Pick up send-failed retries ──
                if (_sendFailedRetries.Count > 0)
                {
                    pendingCarryOver.AddRange(_sendFailedRetries);
                    _sendFailedRetries.Clear();
                }

                // ── 3. Sweep delivery timeouts ──
                SweepPendingResponseTimeouts(pendingCarryOver, cancellationToken);

                // ── 4. Epoch bump ──
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

                        Interlocked.CompareExchange(ref _epochBumpRequestedForEpoch, -1, staleEpoch);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogEpochBumpFailed(ex, staleEpoch);
                    }
                }

                // ── 5. Coalesce ──
                coalescedPartitions.Clear();
                var newCarryOver = pendingCarryOver == carryOverA ? carryOverB : carryOverA;
                newCarryOver.Clear();
                coalescedCount = 0;
                coalescedBatches = ArrayPool<ReadyBatch>.Shared.Rent(maxCoalesce);

                var hadCarryOver = pendingCarryOver.Count > 0;
                if (hadCarryOver)
                {
                    for (var i = 0; i < pendingCarryOver.Count; i++)
                        CoalesceBatch(pendingCarryOver[i], coalescedBatches, ref coalescedCount,
                            coalescedPartitions, newCarryOver);
                    pendingCarryOver.Clear();
                }

                // Read from the event channel lazily during coalescing (like main reads
                // from its bounded batch channel). Non-batch events are consumed as signals
                // only. When carry-over produced a coalesced batch, read at most 1 new batch
                // from the channel to prevent carry-over growth while still draining gradually.
                // Without this limit, duplicate-partition batches (single-partition workloads)
                // would cause unbounded carry-over growth and O(n²) scanning.
                {
                    var channelReadLimit = (hadCarryOver && coalescedCount > 0) ? 1 : maxCoalesce;
                    var channelReads = 0;
                    while (channelReads < channelReadLimit && eventReader.TryRead(out var evt))
                    {
                        if (evt.Type == SendLoopEventType.NewBatch)
                        {
                            channelReads++;
                            CoalesceBatch(evt.Batch!, coalescedBatches, ref coalescedCount,
                                coalescedPartitions, newCarryOver);
                        }
                        // ResponseReady/Unmute: consumed as wake-up signals, no data to process
                    }
                }

                if (newCarryOver.Count > 0)
                    SweepExpiredCarryOver(newCarryOver);

                // ── 6. Send or wait ──
                if (coalescedCount > 0)
                {
                    if (_pendingResponses.Count >= _maxInFlight)
                        LogWaitingForInFlightCapacity(_brokerId, _pendingResponses.Count, _maxInFlight);

                    while (_pendingResponses.Count >= _maxInFlight)
                    {
                        // Drain any new events (new batches go to carry-over)
                        while (eventReader.TryRead(out var evt))
                        {
                            if (evt.Type == SendLoopEventType.NewBatch)
                                newCarryOver.Add(evt.Batch!);
                        }

                        // Poll for completed responses to free in-flight slots
                        ProcessCompletedResponses(newCarryOver, cancellationToken);

                        if (_pendingResponses.Count >= _maxInFlight)
                        {
                            await eventReader.WaitToReadAsync(cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }

                    // If a response processed during the in-flight wait triggered an epoch
                    // bump request, we must NOT send the already-coalesced batches — they have
                    // stale epoch/sequences. Move them back to carry-over and loop back so the
                    // epoch bump (step 4) runs before re-coalescing.
                    if (Volatile.Read(ref _epochBumpRequestedForEpoch) >= 0)
                    {
                        for (var i = 0; i < coalescedCount; i++)
                            newCarryOver.Add(coalescedBatches[i]);
                        ArrayPool<ReadyBatch>.Shared.Return(coalescedBatches, clearArray: true);
                        coalescedBatches = null;
                        coalescedCount = 0;
                        pendingCarryOver = newCarryOver;
                        continue;
                    }

                    LogSendingCoalesced(_brokerId, coalescedCount);
                    var batchesToSend = coalescedBatches;
                    var countToSend = coalescedCount;
                    coalescedBatches = null;
                    coalescedCount = 0;
                    await SendCoalescedAsync(batchesToSend, countToSend, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    ArrayPool<ReadyBatch>.Shared.Return(coalescedBatches, clearArray: true);
                    coalescedBatches = null;
                    coalescedCount = 0;
                }

                // ── 7. Compute timeout and wait ──
                pendingCarryOver = newCarryOver;

                if (pendingCarryOver.Count == 0 && _pendingResponses.Count == 0)
                {
                    if (!await eventReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                        break;
                }
                else if (!eventReader.TryPeek(out _))
                {
                    var timeoutMs = ComputeNextWakeupMs(pendingCarryOver);
                    if (timeoutMs > 0)
                    {
                        using var timeoutCts = _pollTimeoutCtsPool.Rent();
                        timeoutCts.CancelAfter(timeoutMs);
                        using var reg = cancellationToken.CanBeCanceled
                            ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), timeoutCts)
                            : default;
                        try
                        {
                            await eventReader.WaitToReadAsync(timeoutCts.Token)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ChannelClosedException) { }
        catch (Exception ex) { LogSendLoopFailed(ex, _brokerId); }
        finally
        {
            _eventChannel.Writer.TryComplete();

            var disposedException = new ObjectDisposedException(nameof(BrokerSender));

            for (var i = 0; i < _sendFailedRetries.Count; i++)
                FailAndCleanupBatch(_sendFailedRetries[i], disposedException);
            _sendFailedRetries.Clear();

            for (var i = 0; i < _pendingResponses.Count; i++)
            {
                var pr = _pendingResponses[i];
                for (var j = 0; j < pr.Count; j++)
                    if (pr.Batches[j] is not null)
                        FailAndCleanupBatch(pr.Batches[j], disposedException);
                ArrayPool<ReadyBatch>.Shared.Return(pr.Batches, clearArray: true);
            }
            _pendingResponses.Clear();

            FailCarryOverBatches(carryOverA);
            FailCarryOverBatches(carryOverB);

            if (coalescedBatches is not null)
            {
                for (var i = 0; i < coalescedCount; i++)
                    FailAndCleanupBatch(coalescedBatches[i], disposedException);
                ArrayPool<ReadyBatch>.Shared.Return(coalescedBatches, clearArray: true);
            }

            // Drain remaining events — only NewBatch events carry batches that need cleanup.
            // ResponseReady and Unmute events are lightweight signals with no data.
            while (eventReader.TryRead(out var evt))
            {
                if (evt.Type == SendLoopEventType.NewBatch)
                    FailAndCleanupBatch(evt.Batch!, disposedException);
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
#if DEBUG
                    batch.DebugLastTransition = (int)BatchTransition.Rerouted;
                    batch.DebugLastBrokerId = _brokerId;
                    Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} Rerouted from broker={_brokerId} to broker={leader.NodeId}");
#endif
                    _rerouteBatch(batch);
                    return;
                }
            }

            // Retry batch: unmute the partition and coalesce it ahead of newer batches.
            batch.IsRetry = false;
            batch.RetryNotBefore = 0;
            _mutedPartitions.Remove(batch.TopicPartition);

            if (!coalescedPartitions.Add(batch.TopicPartition))
            {
                // Same partition already in this request (shouldn't happen — only one
                // retry per partition at a time). Carry over as safety net.
                newCarryOver.Add(batch);
                return;
            }

            coalescedBatches[coalescedCount] = batch;
            coalescedCount++;
#if DEBUG
            batch.DebugLastTransition = (int)BatchTransition.Coalesced;
            batch.DebugLastBrokerId = _brokerId;
            Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} Coalesced (retry) broker={_brokerId}");
#endif
            return;
        }

        // Normal batch: skip muted partitions (retry in progress)
        if (_mutedPartitions.Contains(batch.TopicPartition))
        {
            LogPartitionMuted(_brokerId, batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            newCarryOver.Add(batch);
            return;
        }

        // Ensure at most one batch per partition per coalesced request
        if (!coalescedPartitions.Add(batch.TopicPartition))
        {
            newCarryOver.Add(batch);
            return;
        }

        coalescedBatches[coalescedCount] = batch;
        coalescedCount++;
#if DEBUG
        batch.DebugLastTransition = (int)BatchTransition.Coalesced;
        batch.DebugLastBrokerId = _brokerId;
        Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} Coalesced (normal) broker={_brokerId}");
#endif
    }

    /// <summary>
    /// Polls _pendingResponses for completed response tasks and processes them inline.
    /// Equivalent to Java's Sender processing completed sends inside NetworkClient.poll().
    /// Uses compact-in-place pattern to avoid list allocation during removal.
    /// </summary>
    private void ProcessCompletedResponses(
        List<ReadyBatch> pendingCarryOver,
        CancellationToken cancellationToken)
    {
        for (var i = _pendingResponses.Count - 1; i >= 0; i--)
        {
            var pending = _pendingResponses[i];
            if (!pending.ResponseTask.IsCompleted)
                continue;

            // Remove using swap-with-last (O(1))
            _pendingResponses[i] = _pendingResponses[^1];
            _pendingResponses.RemoveAt(_pendingResponses.Count - 1);

            var task = pending.ResponseTask;
            var batches = pending.Batches;
            var count = pending.Count;
            var requestStartTime = pending.RequestStartTime;

            if (task.IsFaulted || task.IsCanceled)
            {
                var ex = task.Exception?.InnerException ?? new OperationCanceledException();
                LogResponseFailed(ex, _brokerId);
                _pinnedConnection = null;

                for (var j = 0; j < count; j++)
                {
                    if (batches[j] is not null)
                    {
                        try
                        {
                            HandleRetriableBatch(batches[j], ErrorCode.NetworkException,
                                pendingCarryOver, cancellationToken);
                        }
                        catch (Exception batchEx)
                        {
                            try { FailAndCleanupBatch(batches[j], ex); }
                            catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                            LogBatchCleanupStepFailed(batchEx, _brokerId);
                        }
                    }
                }

                ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
                continue;
            }

            var response = task.Result;
            var elapsedTicks = Stopwatch.GetTimestamp() - requestStartTime;
            var latencyMs = (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
            _statisticsCollector.RecordResponseReceived(latencyMs);

            Dictionary<(string Topic, int Partition), ProduceResponsePartitionData>? responseLookup = null;
            foreach (var topicResp in response.Responses)
            {
                foreach (var partResp in topicResp.PartitionResponses)
                {
                    responseLookup ??= new Dictionary<(string, int), ProduceResponsePartitionData>();
                    responseLookup[(topicResp.Name, partResp.Index)] = partResp;
                }
            }

            ProcessResponseBatches(batches, count, responseLookup, requestStartTime,
                pendingCarryOver, cancellationToken);

            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
        }
    }

    /// <summary>
    /// Processes per-partition results for a completed response. Extracted from the success path
    /// of the former ProcessCompletedResponses — handles response lookup, error codes, retries,
    /// epoch bump flagging, acknowledgement callbacks, and batch cleanup.
    /// </summary>
    private void ProcessResponseBatches(
        ReadyBatch[] batches,
        int count,
        Dictionary<(string Topic, int Partition), ProduceResponsePartitionData>? responseLookup,
        long requestStartTime,
        List<ReadyBatch> pendingCarryOver,
        CancellationToken cancellationToken)
    {
        for (var j = 0; j < count; j++)
        {
            var batch = batches[j];
            if (batch is null)
                continue;
#if DEBUG
            batch.DebugLastTransition = (int)BatchTransition.ProcessingResponse;
            Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} ProcessingResponse broker={_brokerId}");
#endif
            try
            {
                var expectedTopic = batch.TopicPartition.Topic;
                var expectedPartition = batch.TopicPartition.Partition;

                ProduceResponsePartitionData? partitionResponse = null;
                responseLookup?.TryGetValue((expectedTopic, expectedPartition), out partitionResponse);

                if (partitionResponse is null)
                {
                    LogNoResponseForPartition(expectedTopic, expectedPartition);
                    HandleRetriableBatch(batch, ErrorCode.NetworkException,
                        pendingCarryOver, cancellationToken);
                    batches[j] = null!;
                    continue;
                }

                if (partitionResponse.ErrorCode == ErrorCode.DuplicateSequenceNumber)
                {
                    LogDuplicateSequenceNumber(expectedTopic, expectedPartition, partitionResponse.BaseOffset);
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
                        batches[j] = null!;
                        continue;
                    }

                    var failureException = new KafkaException(partitionResponse.ErrorCode,
                        $"Produce failed: {partitionResponse.ErrorCode}");
                    UnmutePartition(batch.TopicPartition);
                    try { CompleteInflightEntry(batch); }
                    catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                    _statisticsCollector.RecordBatchFailed(expectedTopic, expectedPartition,
                        batch.CompletionSourcesCount);
                    try
                    {
                        batch.Fail(failureException);
#if DEBUG
                        batch.DebugLastTransition = (int)BatchTransition.FailCalled;
                        Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} FailCalled (non-retriable) broker={_brokerId} error={partitionResponse.ErrorCode}");
#endif
                    }
                    catch (Exception failEx) { LogBatchCleanupStepFailed(failEx, _brokerId); }
                    try
                    {
                        _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
                            batch.CompletionSourcesCount, failureException);
                    }
                    catch (Exception ackEx) { LogBatchCleanupStepFailed(ackEx, _brokerId); }
                    CleanupBatch(batch);
                    batches[j] = null!;
                    continue;
                }

                UnmutePartition(batch.TopicPartition);
                LogBatchCompleted(_brokerId, expectedTopic, expectedPartition, partitionResponse.BaseOffset);
                _statisticsCollector.RecordBatchDelivered(expectedTopic, expectedPartition,
                    batch.CompletionSourcesCount);
                var timestamp = partitionResponse.LogAppendTimeMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(partitionResponse.LogAppendTimeMs)
                    : DateTimeOffset.UtcNow;
                try { CompleteInflightEntry(batch); }
                catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                batch.CompleteSend(partitionResponse.BaseOffset, timestamp);
#if DEBUG
                batch.DebugLastTransition = (int)BatchTransition.CompleteSendCalled;
                Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} CompleteSendCalled broker={_brokerId} offset={partitionResponse.BaseOffset}");
#endif
                try
                {
                    _onAcknowledgement?.Invoke(batch.TopicPartition,
                        partitionResponse.BaseOffset, timestamp,
                        batch.CompletionSourcesCount, null);
                }
                catch (Exception ackEx) { LogBatchCleanupStepFailed(ackEx, _brokerId); }
                CleanupBatch(batch);
                batches[j] = null!;
            }
            catch (Exception batchEx)
            {
                try { FailAndCleanupBatch(batch, new InvalidOperationException(
                    "Unexpected exception during response processing", batchEx)); }
                catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                batches[j] = null!;
            }
        }
    }

    /// <summary>
    /// Sweeps _pendingResponses for delivery timeouts. Entries normally get removed by
    /// ProcessResponseEvent when the ContinueWith fires; this sweep is a safety net for
    /// the edge case where response tasks hang past the delivery timeout.
    /// </summary>
    private void SweepPendingResponseTimeouts(
        List<ReadyBatch> pendingCarryOver,
        CancellationToken cancellationToken)
    {
        if (_pendingResponses.Count == 0) return;

        var now = Stopwatch.GetTimestamp();
        var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks;

        for (var i = _pendingResponses.Count - 1; i >= 0; i--)
        {
            var pending = _pendingResponses[i];

            if (now < pending.RequestStartTime + deliveryTimeoutTicks)
                continue;

            var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs);
            var anyBatchRemaining = false;
            for (var j = 0; j < pending.Count; j++)
            {
                var batch = pending.Batches[j];
                if (batch is not null)
                {
                    var deadline = batch.StopwatchCreatedTicks + deliveryTimeoutTicks;
                    if (now >= deadline)
                    {
                        UnmutePartition(batch.TopicPartition);
                        var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks);
                        try
                        {
                            FailAndCleanupBatch(batch, new KafkaTimeoutException(
                                TimeoutKind.Delivery, elapsed, configured,
                                $"Delivery timeout exceeded while awaiting response for {batch.TopicPartition}"));
                        }
                        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                        pending.Batches[j] = null!;
                    }
                    else
                    {
                        anyBatchRemaining = true;
                    }
                }
            }

            if (!anyBatchRemaining)
            {
                ArrayPool<ReadyBatch>.Shared.Return(pending.Batches, clearArray: true);
                _pendingResponses[i] = _pendingResponses[^1];
                _pendingResponses.RemoveAt(_pendingResponses.Count - 1);
            }
        }
    }

    /// <summary>
    /// Computes the next poll timeout in milliseconds. Returns the minimum of:
    /// - Earliest delivery deadline from _pendingResponses
    /// - Earliest retry backoff from carry-over batches
    /// - Earliest delivery deadline from carry-over batches
    /// Returns 0 if a deadline has already passed (immediate re-loop).
    /// Returns int.MaxValue if nothing is pending.
    /// </summary>
    private int ComputeNextWakeupMs(List<ReadyBatch> carryOver)
    {
        var now = Stopwatch.GetTimestamp();
        var earliestTicks = long.MaxValue;

        for (var i = 0; i < _pendingResponses.Count; i++)
        {
            var deadline = _pendingResponses[i].RequestStartTime + _options.DeliveryTimeoutTicks;
            if (deadline < earliestTicks)
                earliestTicks = deadline;
        }

        for (var i = 0; i < carryOver.Count; i++)
        {
            if (carryOver[i].RetryNotBefore > 0 && carryOver[i].RetryNotBefore < earliestTicks)
                earliestTicks = carryOver[i].RetryNotBefore;

            var deadlineTicks = carryOver[i].StopwatchCreatedTicks + _options.DeliveryTimeoutTicks;
            if (deadlineTicks < earliestTicks)
                earliestTicks = deadlineTicks;
        }

        if (earliestTicks >= long.MaxValue)
            return int.MaxValue;

        var delayTicks = earliestTicks - now;
        if (delayTicks <= 0)
            return 0;

        return Math.Max(1, (int)(delayTicks * 1000.0 / Stopwatch.Frequency));
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
            _options.DeliveryTimeoutTicks;

        if (Stopwatch.GetTimestamp() >= deliveryDeadlineTicks)
        {
            LogDeliveryTimeoutExceeded(_brokerId, batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            UnmutePartition(batch.TopicPartition);
            var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks);
            var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs);
            var ex = new KafkaTimeoutException(
                TimeoutKind.Delivery,
                elapsed,
                configured,
                $"Delivery timeout exceeded for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}" +
                (errorCode != ErrorCode.None ? $" (last error: {errorCode})" : ""));
            FailAndCleanupBatch(batch, ex);
            return;
        }

        // Mute partition so no newer batches overtake the retry (ordering guarantee).
        _mutedPartitions.Add(batch.TopicPartition);

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
            try { CompleteInflightEntry(batch); }
            catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }

            // Apply backoff to prevent tight retry loops if the epoch bump doesn't
            // resolve the error (e.g., broker keeps rejecting with OOSN).
            batch.RetryNotBefore = Stopwatch.GetTimestamp() +
                _options.RetryBackoffTicks;
        }
        else if (isEpochBumpError && _bumpEpoch is null
            && batch.InflightEntry is not null
            && _inflightTracker is not null)
        {
            // Transactional producer — no epoch bump
            LogOosnTransactionalReenqueue(batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                batch.RecordBatch.BaseSequence);

            try { CompleteInflightEntry(batch); }
            catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
            batch.InflightEntry = null;
        }
        else
        {
            LogRetriableErrorWithBackoff(errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                _options.RetryBackoffMs);

            // Fire-and-forget metadata refresh for leader changes.
            // Observe exceptions to prevent UnobservedTaskException on GC.
            _ = ObserveMetadataRefreshAsync(batch.TopicPartition.Topic, cancellationToken);

            // Set backoff via RetryNotBefore instead of Task.Delay
            batch.RetryNotBefore = Stopwatch.GetTimestamp() +
                _options.RetryBackoffTicks;
        }

        _statisticsCollector.RecordRetry();
        Diagnostics.DekafMetrics.Retries.Add(1,
            new System.Diagnostics.TagList { { Diagnostics.DekafDiagnostics.MessagingDestinationName, batch.TopicPartition.Topic } });

        // Add to carry-over — deterministic order since we process responses forward (FIFO)
        batch.IsRetry = true;
        pendingCarryOver.Add(batch);
#if DEBUG
        batch.DebugLastTransition = (int)BatchTransition.AddedToCarryOver;
        batch.DebugLastBrokerId = _brokerId;
        Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} AddedToCarryOver broker={_brokerId}");
#endif
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
#if DEBUG
                    batch.DebugLastTransition = (int)BatchTransition.InflightRegistered;
                    Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} InflightRegistered broker={_brokerId}");
#endif
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
#if DEBUG
                    batch.DebugLastTransition = (int)BatchTransition.CompleteSendCalled;
                    Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} CompleteSendCalled (fire-and-forget) broker={_brokerId}");
#endif
                    try { _onAcknowledgement?.Invoke(batch.TopicPartition, -1, fireAndForgetTimestamp, batch.CompletionSourcesCount, null); }
                    catch (Exception ackEx) { LogBatchCleanupStepFailed(ackEx, _brokerId); }
                }

                // Release everything synchronously (no pipelined response — fire-and-forget
                // doesn't add to _pendingResponses, so no in-flight slot to release)
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
#if DEBUG
                    batches[i].DebugLastTransition = (int)BatchTransition.MemoryReleased;
                    Debug.WriteLine($"[BatchTrack] {batches[i].TopicPartition} MemoryReleased broker={_brokerId}");
#endif
                }
            }

            var pendingResponse = new PendingResponse(responseTask, batches, count, requestStartTime);
            _pendingResponses.Add(pendingResponse);

            // Signal the send loop to wake up and poll when the response arrives.
            // Only a lightweight signal — actual processing happens in ProcessCompletedResponses.
            _ = responseTask.ContinueWith(static (_, state) =>
            {
                ((ChannelWriter<SendLoopEvent>)state!).TryWrite(SendLoopEvent.ResponseReady());
            }, _eventChannel.Writer,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
#if DEBUG
            for (var i = 0; i < count; i++)
            {
                batches[i].DebugLastTransition = (int)BatchTransition.AddedToPendingResponses;
                Debug.WriteLine($"[BatchTrack] {batches[i].TopicPartition} AddedToPendingResponses broker={_brokerId}");
            }
#endif

            // Mute partitions at send time when limited to 1 in-flight request.
            // This ensures at most one batch per partition in-flight across all requests.
            // When maxInFlight > 1, sequence numbers guarantee ordering instead,
            // and retry-time muting handles error recovery.
            if (_muteOnSend)
            {
                for (var i = 0; i < count; i++)
                    _mutedPartitions.Add(batches[i].TopicPartition);
            }

            LogPipelinedSend(_brokerId, count, _pendingResponses.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown — fail batches permanently (no point retrying)
            for (var i = 0; i < count; i++)
                FailAndCleanupBatch(batches[i], new ObjectDisposedException(nameof(BrokerSender)));

            ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
        }
        catch (Exception ex)
        {
            // Send failed (connection error, etc.) — retry batches instead of permanently failing.
            // Aligned with Java Kafka's Sender: transient failures cause reenqueue for retry.
            _pinnedConnection = null; // Invalidate broken connection
            LogResponseFailed(ex, _brokerId);

            for (var i = 0; i < count; i++)
            {
                var batch = batches[i];
                try
                {
                    // Buffer memory stays reserved during retry — the batch still holds
                    // physical memory (arena buffer). Release happens in CleanupBatch when
                    // the batch finally completes (success or permanent failure).

                    // Check delivery deadline before retrying
                    var deliveryDeadlineTicks = batch.StopwatchCreatedTicks +
                        _options.DeliveryTimeoutTicks;

                    if (Stopwatch.GetTimestamp() >= deliveryDeadlineTicks)
                    {
                        UnmutePartition(batch.TopicPartition);
                        var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks);
                        var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs);
                        FailAndCleanupBatch(batch, new KafkaTimeoutException(
                            TimeoutKind.Delivery,
                            elapsed,
                            configured,
                            $"Delivery timeout exceeded for {batch.TopicPartition}"));
                    }
                    else
                    {
                        // Mute partition (ordering guarantee) and queue for retry.
                        _mutedPartitions.Add(batch.TopicPartition);
                        batch.IsRetry = true;
                        batch.RetryNotBefore = Stopwatch.GetTimestamp() +
                            _options.RetryBackoffTicks;
                        _sendFailedRetries.Add(batch);
#if DEBUG
                        batch.DebugLastTransition = (int)BatchTransition.AddedToSendFailedRetries;
                        batch.DebugLastBrokerId = _brokerId;
                        Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} AddedToSendFailedRetries broker={_brokerId}");
#endif
                        _statisticsCollector.RecordRetry();
                    }
                }
                catch (Exception batchEx)
                {
                    // Per-batch exception must not skip remaining batches.
                    // Fall back to permanent failure for this batch.
                    LogBatchCleanupStepFailed(batchEx, _brokerId);
                    try { FailAndCleanupBatch(batch, ex); }
                    catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
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
        _mutedPartitions.Remove(tp);
        _eventChannel.Writer.TryWrite(SendLoopEvent.Unmute());
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
                _options.DeliveryTimeoutTicks;

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
                var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks);
                var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs);
                FailAndCleanupBatch(batch, new KafkaTimeoutException(
                    TimeoutKind.Delivery,
                    elapsed,
                    configured,
                    $"Delivery timeout exceeded for {batch.TopicPartition}"));
                carryOver.RemoveAt(i);
            }
        }
    }

    private void FailCarryOverBatches(List<ReadyBatch> carryOver)
    {
        var disposedException = new ObjectDisposedException(nameof(BrokerSender));
        for (var i = 0; i < carryOver.Count; i++)
            FailAndCleanupBatch(carryOver[i], disposedException);
    }

    private void FailAndCleanupBatch(ReadyBatch batch, Exception ex)
    {
#if DEBUG
        batch.DebugLastTransition = (int)BatchTransition.FailCalled;
        batch.DebugLastBrokerId = _brokerId;
        Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} FailAndCleanupBatch broker={_brokerId} ex={ex.GetType().Name}");
#endif
        // Every operation is wrapped in try/catch to guarantee we reach CleanupBatch.
        // If CompleteInflightEntry throws, batch.Fail must still run to resolve completion sources.
        // If batch.Fail throws, CleanupBatch must still run to release memory and return the batch.
        // A single unwrapped throw here causes the caller's loop to skip remaining batches,
        // leaking their completion sources and causing producer hangs (deadlocks).
        try { CompleteInflightEntry(batch); }
        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
        try { _statisticsCollector.RecordBatchFailed(
            batch.TopicPartition.Topic,
            batch.TopicPartition.Partition,
            batch.CompletionSourcesCount); }
        catch (Exception statsEx) { LogBatchCleanupStepFailed(statsEx, _brokerId); }
        try { batch.Fail(ex); }
        catch (Exception failEx) { LogBatchCleanupStepFailed(failEx, _brokerId); }
        try { _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
            batch.CompletionSourcesCount, ex); }
        catch (Exception ackEx) { LogBatchCleanupStepFailed(ackEx, _brokerId); }
        try { CleanupBatch(batch); }
        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CleanupBatch(ReadyBatch batch)
    {
#if DEBUG
        batch.DebugLastTransition = (int)BatchTransition.CleanupBatchCalled;
        Debug.WriteLine($"[BatchTrack] {batch.TopicPartition} CleanupBatch broker={_brokerId}");
#endif
        // Release buffer memory at batch completion (matching Java's RecordAccumulator.deallocate()).
        // This is the primary release path: memory is held throughout the entire pipeline
        // (append → drain → send → response) to provide accurate end-to-end backpressure.
        if (!batch.MemoryReleased)
        {
            _accumulator.ReleaseMemory(batch.DataSize);
            batch.MemoryReleased = true;
        }
        // Remove from tracking and decrement in-flight counter. OnBatchExitsPipeline uses
        // TryRemove as an atomic guard — if another path already removed this batch
        // (e.g., SweepExpiredInFlightBatches), it returns false but we still return
        // the batch to the pool since the sweep defers pool return to BrokerSender.
        _accumulator.OnBatchExitsPipeline(batch);

        // ReturnReadyBatch is idempotent (atomic _returnedToPool flag), so this is safe
        // even if ForceFailAllInFlightBatches races during disposal.
        try { _accumulator.ReturnReadyBatch(batch); }
        catch (Exception returnEx) { LogBatchCleanupStepFailed(returnEx, _brokerId); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        LogDisposing(_brokerId);

        _eventChannel.Writer.TryComplete();

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
        catch (Exception ex)
        {
            LogBatchCleanupStepFailed(ex, _brokerId);
        }

        if (_pendingResponses.Count > 0)
        {
            LogFailingPendingResponses(_brokerId, _pendingResponses.Count);

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
        _pollTimeoutCtsPool.Clear();
    }

    private async Task ObserveMetadataRefreshAsync(string topic, CancellationToken cancellationToken)
    {
        try
        {
            await _metadataManager.RefreshMetadataAsync([topic], cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort refresh — failures are already logged by MetadataManager
        }
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender[{BrokerId}] non-fatal exception during batch cleanup step (suppressed)")]
    private partial void LogBatchCleanupStepFailed(Exception exception, int brokerId);

    #endregion
}
