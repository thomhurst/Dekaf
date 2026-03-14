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
    /// <summary>
    /// Timeout for SendCoalescedAsync which only does TCP write + response task wiring.
    /// Longer than this indicates a broken/stale connection.
    /// </summary>
    private const int SendCoalescedTimeoutMs = 5000;

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
    private readonly Func<short, IReadOnlyCollection<TopicPartition>, (long ProducerId, short ProducerEpoch)>? _bumpEpoch;
    private readonly Func<short>? _getCurrentEpoch;
    private readonly ILogger _logger;

    private readonly Channel<SendLoopEvent> _eventChannel;
    private readonly Task _sendLoopTask;
    private readonly CancellationTokenSource _cts;

    // Pending responses: send-loop owned (single-threaded). Entries are added when a pipelined
    // request is sent and removed by ProcessCompletedResponses when the response task completes.
    // HandleTimedOutRequests (Java pattern) checks request timeout centrally and fails all
    // pending responses on timeout, invalidating the connection.
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
    private readonly struct SendLoopEvent
    {
        public readonly SendLoopEventType Type;
        public readonly ReadyBatch? Batch;

        private SendLoopEvent(SendLoopEventType type, ReadyBatch? batch = null)
        {
            Type = type;
            Batch = batch;
        }

        public static SendLoopEvent NewBatch(ReadyBatch batch) => new(SendLoopEventType.NewBatch, batch);

        public static SendLoopEvent ResponseReady() => new(SendLoopEventType.ResponseReady);

        public static SendLoopEvent Unmute() => new(SendLoopEventType.Unmute);
    }

    /// <summary>
    /// Per-partition carry-over queues matching Java Kafka's per-partition Deque design.
    /// Retries go to the FRONT (AddFirst), new batches to the BACK (Add).
    /// Guarantees per-partition FIFO ordering during coalescing.
    /// Single-threaded: only accessed by the send loop.
    /// </summary>
    private sealed class PartitionCarryOver
    {
        private readonly Dictionary<TopicPartition, LinkedList<ReadyBatch>> _partitions = new();
        private int _count;

        public int Count => _count;

        private LinkedList<ReadyBatch> GetOrCreateQueue(TopicPartition tp)
        {
            if (!_partitions.TryGetValue(tp, out var queue))
            {
                queue = new LinkedList<ReadyBatch>();
                _partitions[tp] = queue;
            }
            return queue;
        }

        /// <summary>Add to back of partition queue (normal carry-over, new batches).</summary>
        public void Add(ReadyBatch batch)
        {
            GetOrCreateQueue(batch.TopicPartition).AddLast(batch);
            _count++;
        }

        /// <summary>
        /// Add to front of partition queue (retries, epoch bump — older batches first).
        /// Matches Java's Deque.addFirst() for reenqueue.
        /// </summary>
        public void AddFirst(ReadyBatch batch)
        {
            GetOrCreateQueue(batch.TopicPartition).AddFirst(batch);
            _count++;
        }

        /// <summary>
        /// Add after existing retry batches but before non-retry batches.
        /// Used by HandleRetriableBatch where multiple responses may add retries
        /// for the same partition — preserves FIFO among retries while keeping
        /// them ahead of newer non-retry carry-over batches.
        /// </summary>
        public void AddAfterRetries(ReadyBatch batch)
        {
            var queue = GetOrCreateQueue(batch.TopicPartition);

            var node = queue.First;
            while (node is not null && node.Value.IsRetry)
                node = node.Next;

            if (node is not null)
                queue.AddBefore(node, batch);
            else
                queue.AddLast(batch);

            _count++;
        }

        /// <summary>
        /// Drains all batches to the destination in per-partition FIFO order, then clears.
        /// Used before coalescing to iterate in deterministic per-partition order.
        /// </summary>
        public void DrainTo(List<ReadyBatch> destination)
        {
            foreach (var kvp in _partitions)
            {
                foreach (var batch in kvp.Value)
                    destination.Add(batch);
                kvp.Value.Clear();
            }
            _count = 0;
        }

        public void Clear()
        {
            foreach (var kvp in _partitions)
                kvp.Value.Clear();
            _count = 0;
        }

        /// <summary>Iterates all batches across all partitions (for fail/cleanup/wakeup).</summary>
        public void ForEach(Action<ReadyBatch> action)
        {
            foreach (var kvp in _partitions)
                foreach (var batch in kvp.Value)
                    action(batch);
        }

        /// <summary>
        /// Sweeps expired batches matching the predicate. Calls onRemoved for each,
        /// then removes it from its partition queue.
        /// </summary>
        public void SweepWhere(Func<ReadyBatch, bool> shouldRemove, Action<ReadyBatch> onRemoved)
        {
            foreach (var kvp in _partitions)
            {
                var queue = kvp.Value;
                var node = queue.First;
                while (node is not null)
                {
                    var next = node.Next;
                    if (shouldRemove(node.Value))
                    {
                        onRemoved(node.Value);
                        queue.Remove(node);
                        _count--;
                    }
                    node = next;
                }
            }
        }
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

    // Partitions that triggered OOSN/InvalidProducerEpoch and need sequence reset
    // during the next epoch bump (Java-style per-partition reset, KIP-360).
    // Single-threaded send loop — no locks needed.
    private readonly HashSet<TopicPartition> _partitionsNeedingSequenceReset = new();

    // Pinned connection: the send loop reuses a single connection to preserve wire order.
    // With multiple connections per broker (round-robin pool), requests on different
    // TCP connections can arrive at the broker out of order, causing OOSN. By pinning
    // one connection, all produce requests are pipelined on the same TCP stream.
    private IKafkaConnection? _pinnedConnection;

    /// <summary>
    /// Signal-based completion notification: rotated TCS that gets signaled when any
    /// response task completes. Replaces Task.WhenAny polling to avoid per-wait
    /// continuation allocations for every in-flight task.
    /// </summary>
    private TaskCompletionSource _anyResponseCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
        Func<short, IReadOnlyCollection<TopicPartition>, (long ProducerId, short ProducerEpoch)>? bumpEpoch,
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
        var carryOver = new PartitionCarryOver();
        var drainList = new List<ReadyBatch>();

        var coalescedBatches = new ReadyBatch[maxCoalesce];
        var coalescedCount = 0;

        var sendTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                LogSendLoopIteration(_brokerId, carryOver.Count, _pendingResponses.Count);

                // ── 1. Poll pending responses (like Java's client.poll()) ──
                // Signal events (ResponseReady, Unmute) may have woken us up — processing
                // completed responses here handles them. Batch events stay in the channel
                // and are read lazily during coalescing (step 5) to avoid O(n²) carry-over growth.
                ProcessCompletedResponses(carryOver, cancellationToken);

                // ── 2. Pick up send-failed retries ──
                // Use AddFirst: retries are older batches that must go before any newer
                // carry-over batches for the same partition (Java's Deque.addFirst).
                if (_sendFailedRetries.Count > 0)
                {
                    for (var i = 0; i < _sendFailedRetries.Count; i++)
                        carryOver.AddFirst(_sendFailedRetries[i]);
                    _sendFailedRetries.Clear();
                }

                // ── 3. Handle timed-out requests (Java handleTimedOutRequests pattern) ──
                HandleTimedOutRequests(carryOver, cancellationToken);

                // ── 4. Epoch bump (Java-style client-side, KIP-360) ──
                // Synchronous: no network call, just epoch+1 + per-partition sequence reset.
                // The broker accepts the bumped epoch when it sees seq=0 for affected partitions.
                var staleEpoch = Volatile.Read(ref _epochBumpRequestedForEpoch);
                if (staleEpoch >= 0 && _bumpEpoch is not null)
                {
                    try
                    {
                        var currentEpoch = _getCurrentEpoch?.Invoke() ?? -1;
                        if (currentEpoch >= 0 && currentEpoch <= (short)staleEpoch)
                        {
                            _bumpEpoch((short)staleEpoch, _partitionsNeedingSequenceReset);
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
                    finally
                    {
                        _partitionsNeedingSequenceReset.Clear();
                    }
                }

                // ── 5. Coalesce ──
                coalescedPartitions.Clear();
                coalescedCount = 0;

                // Drain carry-over into temp list for iteration. Per-partition FIFO order
                // ensures oldest batch per partition is seen first (Java's Deque.pollFirst).
                drainList.Clear();
                var hadCarryOver = carryOver.Count > 0;
                if (hadCarryOver)
                    carryOver.DrainTo(drainList);

                for (var i = 0; i < drainList.Count; i++)
                    CoalesceBatch(drainList[i], coalescedBatches, ref coalescedCount,
                        coalescedPartitions, carryOver);

                // Read from the event channel lazily during coalescing (like main reads
                // from its bounded batch channel). Non-batch events are consumed as signals.
                // Carry-over budget: limit channel-read carry-overs so total carry-over
                // never exceeds maxCoalesce. This bounds carry-over growth while allowing
                // multi-partition workloads to fill all partition slots from the channel.
                // On main, the bounded channel (capacity 10) prevents this naturally;
                // the unbounded event channel needs an explicit budget instead.
                {
                    var carryOverBudget = maxCoalesce - carryOver.Count;
                    var channelReads = 0;
                    var channelCarryOvers = 0;
                    while (channelReads < maxCoalesce && eventReader.TryRead(out var evt))
                    {
                        if (evt.Type == SendLoopEventType.NewBatch)
                        {
                            var carryBefore = carryOver.Count;
                            channelReads++;
                            CoalesceBatch(evt.Batch!, coalescedBatches, ref coalescedCount,
                                coalescedPartitions, carryOver);
                            if (carryOver.Count > carryBefore)
                            {
                                channelCarryOvers++;
                                if (channelCarryOvers >= carryOverBudget)
                                    break;
                            }
                        }
                        // ResponseReady/Unmute: consumed as wake-up signals, no data to process
                    }
                }

                if (carryOver.Count > 0)
                    SweepExpiredCarryOver(carryOver);

                // ── 6. Send or wait ──
                var sentThisIteration = false;
                if (coalescedCount > 0)
                {
                    if (_pendingResponses.Count >= _maxInFlight)
                        LogWaitingForInFlightCapacity(_brokerId, _pendingResponses.Count, _maxInFlight);

                    while (_pendingResponses.Count >= _maxInFlight)
                    {
                        // Poll for completed responses to free in-flight slots.
                        ProcessCompletedResponses(carryOver, cancellationToken);

                        if (_pendingResponses.Count >= _maxInFlight)
                        {
                            // Handle timed-out requests (Java pattern) to free zombie entries.
                            // Without this, the send loop blocks forever when a response task
                            // never completes.
                            HandleTimedOutRequests(carryOver, cancellationToken);
                            if (_pendingResponses.Count < _maxInFlight)
                                break;

                            // Wait for any pending response to complete.
                            // Cannot use eventReader.WaitToReadAsync here — the channel may
                            // contain unread NewBatch events that cause immediate (synchronous)
                            // return, creating a spin loop that starves the thread pool and
                            // prevents I/O completion callbacks from running.
                            // Uses a signal-based TCS that gets signaled when any response
                            // completes, with a 100ms periodic wake-up to re-sweep delivery
                            // timeouts for zombie entries that expire while we're waiting.
                            try
                            {
                                await _anyResponseCompleted.Task
                                    .WaitAsync(TimeSpan.FromMilliseconds(100), cancellationToken)
                                    .ConfigureAwait(false);
                            }
                            catch (TimeoutException)
                            {
                                // Periodic wake-up — loop back to re-check responses and timeouts
                            }
                        }
                    }

                    // If a response processed during the in-flight wait muted a partition
                    // (retry scheduled), any coalesced batch for that partition must NOT be sent —
                    // it would arrive after the retry batch, violating ordering. Move muted
                    // batches back to carry-over so they're re-sent after the retry completes.
                    if (coalescedCount > 0)
                    {
                        var dst = 0;
                        for (var src = 0; src < coalescedCount; src++)
                        {
                            if (!coalescedBatches[src].IsRetry
                                && _mutedPartitions.Contains(coalescedBatches[src].TopicPartition))
                            {
                                // Normal batch whose partition was muted during the in-flight
                                // wait (a different batch for this partition triggered a retry).
                                // Must not send — it would arrive after the retry, violating order.
                                carryOver.AddAfterRetries(coalescedBatches[src]);
                            }
                            else
                            {
                                coalescedBatches[dst] = coalescedBatches[src];
                                dst++;
                            }
                        }

                        // Clear trailing slots so we don't hold stale references
                        for (var i = dst; i < coalescedCount; i++)
                            coalescedBatches[i] = null!;

                        coalescedCount = dst;
                    }

                    // If a response processed during the in-flight wait triggered an epoch
                    // bump request, we must NOT send the already-coalesced batches — they have
                    // stale epoch/sequences. Move them back to carry-over and loop back so the
                    // epoch bump (step 4) runs before re-coalescing.
                    if (coalescedCount > 0 && Volatile.Read(ref _epochBumpRequestedForEpoch) >= 0)
                    {
                        // Epoch bump pending — move coalesced batches back to carry-over.
                        // Retry batches use AddFirst (they're older and must go before everything).
                        // Normal batches use AddAfterRetries (they're newer and must go after
                        // all retry batches to preserve per-partition ordering). Without this
                        // distinction, AddFirst puts normal batches before retries, causing
                        // newer data to be sent before older retry data after the partition unmutes.
                        for (var i = 0; i < coalescedCount; i++)
                        {
                            if (coalescedBatches[i].IsRetry)
                                carryOver.AddFirst(coalescedBatches[i]);
                            else
                                carryOver.AddAfterRetries(coalescedBatches[i]);
                        }
                        Array.Clear(coalescedBatches, 0, coalescedCount);
                        coalescedCount = 0;
                        continue;
                    }

                    if (coalescedCount > 0)
                    {
                        // Finalize retry batches now that we know they will actually be sent
                        // (epoch bump check passed). Clear IsRetry and unmute partitions.
                        FinalizeCoalescedRetries(coalescedBatches, coalescedCount);

                        LogSendingCoalesced(_brokerId, coalescedCount);
                        for (var si = 0; si < coalescedCount; si++)
                            coalescedBatches[si].AppendDiag('S');
                        // Copy to a dedicated array for SendCoalescedAsync, which may store it
                        // in PendingResponse beyond this iteration. The pre-allocated scratch
                        // array stays with the loop for reuse.
                        var batchesToSend = new ReadyBatch[coalescedCount];
                        coalescedBatches.AsSpan(0, coalescedCount).CopyTo(batchesToSend);
                        var countToSend = coalescedCount;
                        Array.Clear(coalescedBatches, 0, coalescedCount);
                        coalescedCount = 0;
                        // Send timeout: if SendCoalescedAsync hangs (connection issues,
                        // stale sockets), the CTS cancellation triggers the Z path (retry/fail)
                        // so the send loop stays responsive for HandleTimedOutRequests.
                        // IMPORTANT: must await (not fire-and-forget) to preserve thread safety —
                        // SendCoalescedAsync accesses _pendingResponses and _sendFailedRetries
                        // which are not thread-safe.
                        {
                            if (!sendTimeoutCts.TryReset())
                            {
                                sendTimeoutCts.Dispose();
                                sendTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            }
                            sendTimeoutCts.CancelAfter(SendCoalescedTimeoutMs);
                            await SendCoalescedAsync(batchesToSend, countToSend, sendTimeoutCts.Token)
                                .ConfigureAwait(false);
                        }
                        sentThisIteration = true;
                    }
                    else
                    {
                        // coalescedCount dropped to 0 (all batches moved to carry-over for
                        // muted partitions). Nothing to send; array already cleared by
                        // the muting loop above.
                        coalescedCount = 0;
                    }
                }
                else
                {
                    // No batches were coalesced at all — nothing to clear.
                    coalescedCount = 0;
                }

                // ── 7. Compute timeout and wait ──
                if (carryOver.Count == 0 && _pendingResponses.Count == 0)
                {
                    // Fully idle — wait for any event (new batch, response, unmute).
                    if (!await eventReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                        break;
                }
                else if (carryOver.Count > 0 && sentThisIteration)
                {
                    // Carry-over exists and we sent batches — loop immediately to process
                    // responses and re-coalesce. Carry-over batches that were blocked by
                    // duplicate-partition will coalesce next time (coalescedPartitions is cleared).
                }
                else if (_pendingResponses.Count > 0)
                {
                    // Carry-over (possibly all muted) and/or pending responses — wait for
                    // any response to complete. Cannot use eventReader.WaitToReadAsync here
                    // because the channel may contain stale NewBatch events that cause
                    // immediate return, creating a spin loop when all carry-over is muted.
                    // Signal-based TCS bypasses the channel and wakes exactly when a
                    // response completes (which may unmute a partition).
                    try
                    {
                        await _anyResponseCompleted.Task
                            .WaitAsync(TimeSpan.FromMilliseconds(100), cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // Periodic wake-up — loop back to re-check and sweep timeouts
                    }
                }
                else if (carryOver.Count > 0)
                {
                    // Carry-over exists but no pending responses (e.g. retry backoff waiting).
                    // Timed wait — loop back after earliest retry backoff or delivery deadline.
                    var wakeupMs = ComputeNextWakeupMs(carryOver);
                    if (wakeupMs > 0)
                    {
                        await Task.Delay(Math.Min(wakeupMs, 100), cancellationToken).ConfigureAwait(false);
                    }
                }
                else if (!eventReader.TryPeek(out _))
                {
                    // No carry-over, no pending responses, no events — wait for new batch.
                    if (!await eventReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        catch (ChannelClosedException) { }
        catch (Exception ex) { LogSendLoopFailed(ex, _brokerId); }
        finally
        {
            sendTimeoutCts.Dispose();
            _eventChannel.Writer.TryComplete();

            var disposedException = new ObjectDisposedException(nameof(BrokerSender));

            for (var i = 0; i < _sendFailedRetries.Count; i++)
            {
                try { FailAndCleanupBatch(_sendFailedRetries[i], disposedException); }
                catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
            }
            _sendFailedRetries.Clear();

            for (var i = 0; i < _pendingResponses.Count; i++)
            {
                var pr = _pendingResponses[i];
                for (var j = 0; j < pr.Count; j++)
                {
                    if (pr.Batches[j] is not null)
                    {
                        try { FailAndCleanupBatch(pr.Batches[j], disposedException); }
                        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                    }
                }
                Array.Clear(pr.Batches);
            }
            _pendingResponses.Clear();

            FailCarryOverBatches(carryOver);

            for (var i = 0; i < coalescedCount; i++)
            {
                try { FailAndCleanupBatch(coalescedBatches[i], disposedException); }
                catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
            }
            Array.Clear(coalescedBatches, 0, coalescedCount);

            // Drain remaining events — only NewBatch events carry batches that need cleanup.
            // ResponseReady and Unmute events are lightweight signals with no data.
            while (eventReader.TryRead(out var evt))
            {
                if (evt.Type == SendLoopEventType.NewBatch)
                {
                    try { FailAndCleanupBatch(evt.Batch!, disposedException); }
                    catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                }
            }

            // Unmute all partitions this BrokerSender had muted in the accumulator.
            // Without this, partitions stay permanently muted after BrokerSender disposal,
            // preventing Ready()/Drain() from ever picking up queued batches for those
            // partitions. This causes orphaned batch timeouts on slow CI runners where
            // transient connection errors kill the send loop while retries are pending.
            foreach (var tp in _mutedPartitions)
                _accumulator.UnmutePartition(tp);
            _mutedPartitions.Clear();
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
        PartitionCarryOver carryOver)
    {
        if (batch.IsRetry)
        {
            // Check delivery deadline before re-sending. Without this, a retrying batch
            // can loop past HandleRetriableBatch's deadline check indefinitely when the
            // broker keeps returning fast retriable errors — the batch is coalesced/sent/
            // responded/retried in a tight loop, and SweepExpiredCarryOver never sees it
            // because it's already drained from carry-over by the time the sweep runs.
            var now = Stopwatch.GetTimestamp();
            if (now >= batch.StopwatchCreatedTicks + _options.DeliveryTimeoutTicks)
            {
                LogDeliveryTimeoutExceeded(_brokerId, batch.TopicPartition.Topic, batch.TopicPartition.Partition);
                UnmutePartition(batch.TopicPartition);
                var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks);
                var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs);
                FailAndCleanupBatch(batch, new KafkaTimeoutException(
                    TimeoutKind.Delivery,
                    elapsed,
                    configured,
                    $"Delivery timeout exceeded for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition} during retry coalesce"));
                return;
            }

            // Check backoff — carry over if backoff hasn't elapsed
            if (batch.RetryNotBefore > 0 && now < batch.RetryNotBefore)
            {
                carryOver.Add(batch);
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

            // Retry batch: coalesce it ahead of newer batches.
            // NOTE: Do NOT clear IsRetry or unmute here. These are deferred to
            // FinalizeCoalescedRetries() after the epoch bump check passes.
            // If an epoch bump is pending, coalesced batches are moved back to
            // carry-over — they must retain their retry state and mute status.
            batch.RetryNotBefore = 0;

            if (!coalescedPartitions.Add(batch.TopicPartition))
            {
                // Same partition already in this request (shouldn't happen — only one
                // retry per partition at a time). Carry over as safety net.
                carryOver.Add(batch);
                return;
            }

            batch.AppendDiag('C');
            coalescedBatches[coalescedCount] = batch;
            coalescedCount++;
            return;
        }

        // Normal batch: skip muted partitions (retry in progress)
        if (_mutedPartitions.Contains(batch.TopicPartition))
        {
            LogPartitionMuted(_brokerId, batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            batch.AppendDiag('O');
            carryOver.Add(batch);
            return;
        }

        // Ensure at most one batch per partition per coalesced request
        if (!coalescedPartitions.Add(batch.TopicPartition))
        {
            batch.AppendDiag('O');
            carryOver.Add(batch);
            return;
        }

        batch.AppendDiag('C');
        coalescedBatches[coalescedCount] = batch;
        coalescedCount++;
    }

    /// <summary>
    /// Finalizes retry batches that have been coalesced and are about to be sent.
    /// Called after the epoch bump check passes to ensure retry state is only cleared
    /// when the batch will actually be sent. If called during CoalesceBatch instead,
    /// an epoch bump check that moves batches back to carry-over would lose the retry
    /// state — causing the batch to be treated as a normal batch and sent out of order.
    /// </summary>
    private void FinalizeCoalescedRetries(ReadyBatch[] batches, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var batch = batches[i];
            if (batch.IsRetry)
            {
                batch.IsRetry = false;
                _mutedPartitions.Remove(batch.TopicPartition);
                _accumulator.UnmutePartition(batch.TopicPartition);
            }
        }
    }

    /// <summary>
    /// Polls _pendingResponses for completed response tasks and processes them inline.
    /// Equivalent to Java's Sender processing completed sends inside NetworkClient.poll().
    /// Uses compact-in-place pattern to avoid list allocation during removal.
    /// </summary>
    private void ProcessCompletedResponses(
        PartitionCarryOver carryOver,
        CancellationToken cancellationToken)
    {
        // CRITICAL: Process responses in FORWARD order (oldest request first).
        // With multi-inflight, R1 and R2 may both complete with errors for the same partition.
        // Forward iteration ensures R1's retry batches are added to carry-over before R2's,
        // preserving per-partition FIFO ordering during the next coalescing pass.
        // Reverse iteration (swap-with-last O(1) removal) would process R2 before R1,
        // causing the newer batch to be coalesced first and violating ordering.
        var writeIdx = 0;
        for (var i = 0; i < _pendingResponses.Count; i++)
        {
            var pending = _pendingResponses[i];
            if (!pending.ResponseTask.IsCompleted)
            {
                // Keep this entry — compact in-place
                _pendingResponses[writeIdx++] = pending;
                continue;
            }

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
                        batches[j].AppendDiag('P'); // Faulted/cancelled response processed
                        try
                        {
                            HandleRetriableBatch(batches[j], ErrorCode.NetworkException,
                                carryOver, cancellationToken);
                        }
                        catch (Exception batchEx)
                        {
                            try { FailAndCleanupBatch(batches[j], ex); }
                            catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                            LogBatchCleanupStepFailed(batchEx, _brokerId);
                        }
                    }
                }

                Array.Clear(batches);
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
                carryOver, cancellationToken);

            Array.Clear(batches);
        }

        // Compact: remove processed entries from the end
        if (writeIdx < _pendingResponses.Count)
            _pendingResponses.RemoveRange(writeIdx, _pendingResponses.Count - writeIdx);
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
        PartitionCarryOver carryOver,
        CancellationToken cancellationToken)
    {
        for (var j = 0; j < count; j++)
        {
            var batch = batches[j];
            if (batch is null)
                continue;
            try
            {
                batch.AppendDiag('R');
                var expectedTopic = batch.TopicPartition.Topic;
                var expectedPartition = batch.TopicPartition.Partition;

                ProduceResponsePartitionData? partitionResponse = null;
                responseLookup?.TryGetValue((expectedTopic, expectedPartition), out partitionResponse);

                if (partitionResponse is null)
                {
                    LogNoResponseForPartition(expectedTopic, expectedPartition);
                    HandleRetriableBatch(batch, ErrorCode.NetworkException,
                        carryOver, cancellationToken);
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
                            carryOver, cancellationToken);
                        batches[j] = null!;
                        continue;
                    }

                    var failureException = new KafkaException(partitionResponse.ErrorCode,
                        $"Produce failed: {partitionResponse.ErrorCode}");
                    if (_muteOnSend)
                        UnmutePartition(batch.TopicPartition);
                    try { CompleteInflightEntry(batch); }
                    catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                    _statisticsCollector.RecordBatchFailed(expectedTopic, expectedPartition,
                        batch.CompletionSourcesCount);
                    try
                    {
                        batch.Fail(failureException);
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

                // Only unmute on success when mute-on-send is active (maxInFlight <= 1).
                // With maxInFlight > 1, partitions are muted only on error (HandleRetriableBatch).
                // Unconditionally unmuting on success would prematurely clear the mute set by a
                // DIFFERENT failed batch for the same partition still pending retry. This allows
                // newer carry-over batches to skip ahead of the older retry batch, violating
                // per-partition FIFO ordering. CoalesceBatch already unmutes when processing the
                // retry batch, which is the correct unmute point for multi-inflight.
                // This matches Java Kafka's conservative muting: partitions stay muted until the
                // retry batch is actually re-sent, not when a sibling batch succeeds.
                if (_muteOnSend)
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
    /// Java-style request timeout handling (handleTimedOutRequests pattern).
    /// On every send loop iteration, checks if ANY pending response has exceeded the
    /// request timeout. If so, removes ALL entries from _pendingResponses and processes
    /// each batch — exactly like Java's NetworkClient.handleTimedOutRequests() which closes
    /// the connection and calls cancelInFlightRequests() for the node.
    /// <para/>
    /// Processing happens synchronously in the single-threaded send loop. Entries are
    /// removed from _pendingResponses BEFORE processing, so ProcessCompletedResponses
    /// cannot also process the same batches (eliminating the race condition).
    /// <para/>
    /// The connection is also invalidated (_pinnedConnection = null) so the next send
    /// establishes a fresh connection. Response tasks from the old connection are orphaned —
    /// they may eventually complete, but nobody polls them since the entries were removed.
    /// </summary>
    private void HandleTimedOutRequests(
        PartitionCarryOver carryOver,
        CancellationToken cancellationToken)
    {
        if (_pendingResponses.Count == 0) return;

        var now = Stopwatch.GetTimestamp();
        var requestTimeoutTicks = _options.RequestTimeoutTicks;

        // Check if ANY pending response has exceeded the request timeout.
        var hasTimedOut = false;
        for (var i = 0; i < _pendingResponses.Count; i++)
        {
            if (now >= _pendingResponses[i].RequestStartTime + requestTimeoutTicks)
            {
                hasTimedOut = true;
                break;
            }
        }

        if (!hasTimedOut)
            return;

        // Request timeout detected — invalidate connection and process ALL pending batches.
        // Must process ALL entries (not just the timed-out one) because they share the same
        // connection, which is now unreliable. This matches Java's behavior: when any request
        // times out, ALL in-flight requests for that node are failed.
        _pinnedConnection = null;
        LogRequestTimeoutDisconnection(_brokerId, _pendingResponses.Count);

        var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks;

        for (var i = 0; i < _pendingResponses.Count; i++)
        {
            var pending = _pendingResponses[i];
            for (var j = 0; j < pending.Count; j++)
            {
                var batch = pending.Batches[j];
                if (batch is null) continue;

                batch.AppendDiag('T'); // Timed-out request

                // Check delivery deadline: if exceeded, permanently fail the batch.
                // Otherwise, retry (like Java's handleProduceResponse for disconnected requests).
                if (now >= batch.StopwatchCreatedTicks + deliveryTimeoutTicks)
                {
                    UnmutePartition(batch.TopicPartition);
                    var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks);
                    var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs);
                    try
                    {
                        FailAndCleanupBatch(batch, new KafkaTimeoutException(
                            TimeoutKind.Delivery, elapsed, configured,
                            $"Delivery timeout exceeded while awaiting response for {batch.TopicPartition}"));
                    }
                    catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                }
                else
                {
                    try
                    {
                        HandleRetriableBatch(batch, ErrorCode.NetworkException,
                            carryOver, cancellationToken);
                    }
                    catch (Exception batchEx)
                    {
                        try { FailAndCleanupBatch(batch, batchEx); }
                        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                    }
                }
            }

            Array.Clear(pending.Batches);
        }

        // Remove all entries. Response tasks are orphaned — they'll eventually complete
        // (via CTS timeout or connection disposal) but nobody polls them.
        _pendingResponses.Clear();
    }

    /// <summary>
    /// Computes the next poll timeout in milliseconds. Returns the minimum of:
    /// - Earliest request timeout deadline from _pendingResponses
    /// - Earliest retry backoff from carry-over batches
    /// - Earliest delivery deadline from carry-over batches
    /// Returns 0 if a deadline has already passed (immediate re-loop).
    /// Returns int.MaxValue if nothing is pending.
    /// </summary>
    private int ComputeNextWakeupMs(PartitionCarryOver carryOver)
    {
        var now = Stopwatch.GetTimestamp();
        var earliestTicks = long.MaxValue;

        // Use request timeout for pending responses (Java handleTimedOutRequests pattern).
        for (var i = 0; i < _pendingResponses.Count; i++)
        {
            var deadline = _pendingResponses[i].RequestStartTime + _options.RequestTimeoutTicks;
            if (deadline < earliestTicks)
                earliestTicks = deadline;
        }

        var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks;
        carryOver.ForEach(batch =>
        {
            if (batch.RetryNotBefore > 0 && batch.RetryNotBefore < earliestTicks)
                earliestTicks = batch.RetryNotBefore;

            var deadlineTicks = batch.StopwatchCreatedTicks + deliveryTimeoutTicks;
            if (deadlineTicks < earliestTicks)
                earliestTicks = deadlineTicks;
        });

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
        PartitionCarryOver carryOver, CancellationToken cancellationToken)
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
        // Also mute in accumulator so Ready/Drain skips this partition — prevents the
        // sender loop from draining newer batches that would jump the retry queue.
        _mutedPartitions.Add(batch.TopicPartition);
        _accumulator.MutePartition(batch.TopicPartition);

        var isEpochBumpError = errorCode is ErrorCode.OutOfOrderSequenceNumber
            or ErrorCode.InvalidProducerEpoch or ErrorCode.UnknownProducerId;

        if (isEpochBumpError && _bumpEpoch is not null)
        {
            LogEpochBumpSignaled(errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition,
                batch.RecordBatch.BaseSequence);

            // Track which partition needs sequence reset (Java-style per-partition reset)
            _partitionsNeedingSequenceReset.Add(batch.TopicPartition);

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

        // Insert after existing retries but before non-retry carry-over batches.
        // When ProcessCompletedResponses processes multiple responses (R1, R2, ...)
        // in forward order, each response's retry must go AFTER earlier responses'
        // retries for the same partition to preserve FIFO ordering.
        batch.IsRetry = true;
        batch.AppendDiag('H'); // HandleRetriableBatch → carry-over
        carryOver.AddAfterRetries(batch);
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

            // Diagnostic: mark batches as having acquired a connection.
            // If orphan trace shows 'S' but no 'G' (Got connection), the hang is at
            // GetPinnedConnectionAsync (connection creation or write lock contention).
            for (var i = 0; i < count; i++)
                batches[i].AppendDiag('G');

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
                    catch (Exception ackEx) { LogBatchCleanupStepFailed(ackEx, _brokerId); }
                }

                // Release everything synchronously (no pipelined response — fire-and-forget
                // doesn't add to _pendingResponses, so no in-flight slot to release)
                for (var i = 0; i < count; i++)
                {
                    CleanupBatch(batches[i]);
                }

                Array.Clear(batches);
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

            var pendingResponse = new PendingResponse(responseTask, batches, count, requestStartTime);
            _pendingResponses.Add(pendingResponse);

            // Diagnostic: mark batches as successfully pipelined to _pendingResponses.
            // If an orphan trace shows 'S' but no 'W' (Wire), the batch never reached here.
            for (var i = 0; i < count; i++)
                batches[i].AppendDiag('W');

            // Signal the send loop to wake up and poll when the response arrives.
            // Only a lightweight signal — actual processing happens in ProcessCompletedResponses.
            // Two signal paths: (1) channel write for the main WaitToReadAsync path,
            // (2) TCS rotation for the direct response-wait paths (replaces Task.WhenAny).
            _ = responseTask.ContinueWith(static (_, state) =>
            {
                var (writer, sender) = ((ChannelWriter<SendLoopEvent>, BrokerSender))state!;
                writer.TryWrite(SendLoopEvent.ResponseReady());
                Interlocked.Exchange(ref sender._anyResponseCompleted,
                        new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                    .TrySetResult();
            }, (_eventChannel.Writer, this),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            // Mute partitions at send time when limited to 1 in-flight request.
            // This ensures at most one batch per partition in-flight across all requests.
            // When maxInFlight > 1, sequence numbers guarantee ordering instead,
            // and retry-time muting handles error recovery.
            if (_muteOnSend)
            {
                for (var i = 0; i < count; i++)
                {
                    _mutedPartitions.Add(batches[i].TopicPartition);
                    _accumulator.MutePartition(batches[i].TopicPartition);
                }
            }

            LogPipelinedSend(_brokerId, count, _pendingResponses.Count);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Shutdown — fail batches permanently (no point retrying).
            // Check _cts (BrokerSender lifetime token) instead of cancellationToken because
            // the parameter may be a linked timeout CTS. On timeout, we want retry (Z path),
            // not permanent failure — only true shutdown should permanently fail batches.
            for (var i = 0; i < count; i++)
                FailAndCleanupBatch(batches[i], new ObjectDisposedException(nameof(BrokerSender)));

            Array.Clear(batches);
        }
        catch (Exception ex)
        {
            // Send failed (connection error, timeout, etc.) — retry batches instead of permanently failing.
            // Aligned with Java Kafka's Sender: transient failures cause reenqueue for retry.
            _pinnedConnection = null; // Invalidate broken connection
            LogResponseFailed(ex, _brokerId);

            for (var i = 0; i < count; i++)
            {
                batches[i].AppendDiag('Z'); // Diagnostic: send failed, entering retry/fail path
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
                        _accumulator.MutePartition(batch.TopicPartition);
                        batch.IsRetry = true;
                        batch.RetryNotBefore = Stopwatch.GetTimestamp() +
                            _options.RetryBackoffTicks;
                        _sendFailedRetries.Add(batch);
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

            Array.Clear(batches);
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

        // Apply request timeout to prevent indefinite blocking during connection creation.
        // If GetConnectionAsync hangs (e.g., broker unreachable, TCP SYN drops), the entire
        // send loop stalls — preventing HandleTimedOutRequests from running on batches already
        // in _pendingResponses. The request timeout bounds this wait so the send loop can
        // progress and handle timeouts for other pending batches.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.RequestTimeoutMs));

        conn = await _connectionPool.GetConnectionAsync(_brokerId, timeoutCts.Token)
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
        _accumulator.UnmutePartition(tp); // Also unmute in accumulator so Ready/Drain can pick up new batches
        _eventChannel.Writer.TryWrite(SendLoopEvent.Unmute());
    }

    /// <summary>
    /// Sweeps carry-over for batches that have exceeded their delivery deadline.
    /// Prevents muted batches from sitting indefinitely while their partition's retry cycles.
    /// Called from the single-threaded send loop after coalescing.
    /// </summary>
    private void SweepExpiredCarryOver(PartitionCarryOver carryOver)
    {
        var now = Stopwatch.GetTimestamp();
        var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks;

        carryOver.SweepWhere(
            batch => now >= batch.StopwatchCreatedTicks + deliveryTimeoutTicks,
            batch =>
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
            });
    }

    private void FailCarryOverBatches(PartitionCarryOver carryOver)
    {
        var disposedException = new ObjectDisposedException(nameof(BrokerSender));
        carryOver.ForEach(batch => FailAndCleanupBatch(batch, disposedException));
    }

    private void FailAndCleanupBatch(ReadyBatch batch, Exception ex)
    {
        batch.AppendDiag('F');
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

                Array.Clear(pr.Batches);
            }

            _pendingResponses.Clear();
        }

        _cts.Dispose();
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender[{BrokerId}] request timeout: disconnecting and failing {PendingCount} pending responses (Java handleTimedOutRequests pattern)")]
    private partial void LogRequestTimeoutDisconnection(int brokerId, int pendingCount);

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
