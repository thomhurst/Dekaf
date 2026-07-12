using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

internal readonly record struct TransactionPartitionEnrollmentResult(
    bool IsEnrolled,
    Exception? Error)
{
    public static TransactionPartitionEnrollmentResult Enrolled => new(true, null);
    public static TransactionPartitionEnrollmentResult Pending => new(false, null);
    public static TransactionPartitionEnrollmentResult Failed(Exception error) => new(false, error);
}

/// <summary>
/// Per-broker sender that serializes all writes to a single broker connection.
/// A single-threaded send loop drains events from a unified channel, coalesces batches into
/// ProduceRequests, and sends them via pipelined writes. This guarantees wire-order
/// for same-partition batches.
///
/// All wake-up sources (new batches, response completions, partition unmutes) flow through
/// a single <see cref="_eventChannel"/> — like Java Kafka's Sender.poll() model. Response
/// tasks complete and signal the channel via lightweight <c>UnsafeOnCompleted</c> wake-ups;
/// the send loop then polls <c>_pendingResponsesByConnection</c> for completed tasks (like main's
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
/// In-flight limiting uses both request count and encoded bytes. The byte ceiling keeps
/// acknowledged pipelines near broker BDP instead of filling the full admission buffer.
/// Completed responses are processed by polling <c>_pendingResponsesByConnection</c>.
///
/// All writes go through the send loop — there are no out-of-loop write paths.
/// </summary>
/// <remarks>
/// <para><b>Thread model:</b></para>
/// <para>
/// The core of BrokerSender is the <see cref="SendLoopAsync"/> method, which runs on a single
/// dedicated thread (Task). This send loop owns and exclusively accesses the following mutable state:
/// <see cref="_pendingResponsesByConnection"/>, <see cref="_sendFailedRetries"/>, carry-over batch lists,
/// coalesced batch arrays, and <see cref="_pinnedConnections"/>. No locks are needed for these
/// because they are only ever touched by the send loop thread.
/// </para>
/// <para>
/// Response completion is detected by polling <c>_pendingResponsesByConnection</c> for completed tasks
/// (checking <c>ResponseTask.IsCompleted</c>). <c>UnsafeOnCompleted</c> callbacks write lightweight
/// <see cref="SendLoopEvent.ResponseReady"/> signals to wake up the send loop when responses
/// arrive. In-flight capacity is measured by request count and encoded bytes.
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
    /// Longer than this indicates a broken/stale connection. Reduced from 5000ms to 1500ms
    /// to bound tail latency: a degraded connection previously held batches for up to 5s
    /// before timing out, contributing to p99+ spikes.
    /// </summary>
    private const int SendCoalescedTimeoutMs = 1500;

    private const int ResponsePollIntervalMs = 100;
    private const int BlockedBucketPollIntervalMs = 1;
    private static readonly TimeSpan DisposalDrainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Micro-linger: when coalesced batch count is at or below this threshold,
    /// briefly spin-wait for more batches before sending. Reduces per-request
    /// overhead in multi-broker setups with few partitions per broker.
    /// </summary>
    private const int MicroLingerBatchThreshold = 3;

    /// <summary>
    /// Maximum SpinWait iterations for the micro-linger. Bounds the spin cost
    /// regardless of channel activity. Higher values (20 vs 10) give the sender
    /// loop more time to enqueue all batches for this broker, improving coalescing
    /// in multi-broker setups where per-broker batch counts are low.
    /// </summary>
    private const int MicroLingerMaxSpins = 20;

    /// <summary>
    /// Maximum batches coalesced into one send-loop pass. The in-flight limit can be very
    /// high for non-idempotent producers, but coalescing capacity should stay bounded.
    /// </summary>
    private const int MaxCoalescedBatchesPerPass = 1024;
    // The historical multi-broker workload drains about 1 GB/s with roughly 30 ms broker
    // acknowledgement latency, making 32 MB a practical default BDP per connection. This
    // also prevents the 100-request non-idempotent count limit from turning BufferMemory
    // into an acknowledgement queue. Shared with the per-broker unacked-byte admission
    // budget so the pipeline ceiling and the admission ceiling stay consistent.
    private const int InFlightByteBudgetBatchMultiplier = BrokerUnackedByteBudget.CapBatchMultiplier;

    private static int s_instanceCounter;
    private readonly int _instanceId = Interlocked.Increment(ref s_instanceCounter);

    private readonly int _brokerId;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly RecordAccumulator _accumulator;
    private readonly ProducerOptions _options;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly PartitionInflightTracker _inflightTracker;
    private readonly Action<ReadyBatch, int>? _rerouteBatch;
    private readonly Action<TopicPartition, long, DateTimeOffset, int, Exception?>? _onAcknowledgement;
    private readonly Func<short, IReadOnlyCollection<TopicPartition>, (long ProducerId, short ProducerEpoch)>? _bumpEpoch;
    private readonly Func<short>? _getCurrentEpoch;
    private readonly ILogger _logger;
    private readonly Action<int>? _onBrokerThrottle;
    private readonly Action? _onBlockedBucketRequeued;
    private readonly Func<long> _getTimestamp;
    private readonly Func<int, CancellationToken, ValueTask> _delayForThrottle;
    private readonly TimeSpan _disposalDrainTimeout;

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
        int[] BatchGenerations,
        int Count,
        long EncodedBytes,
        long RequestStartTime)
    {
        /// <summary>
        /// Wraps a pipelined response with the batch-generation snapshot the send captured
        /// at send start (before its first await) — the generations detect batch object
        /// recycling between send and response processing. Ownership of the rented
        /// <paramref name="generations"/> array transfers to this PendingResponse;
        /// it is returned by <see cref="ReturnBatchesArray"/>.
        /// </summary>
        public static PendingResponse Create(
            Task<ProduceResponse> responseTask, ReadyBatch[] batches, int[] generations,
            int count, long encodedBytes, long requestStartTime)
            => new(responseTask, batches, generations, count, encodedBytes, requestStartTime);

        /// <summary>
        /// Returns true if the batch at index <paramref name="i"/> is the same incarnation
        /// that was present when this PendingResponse was created.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSameIncarnation(int i)
            => Batches[i] is not null && Batches[i].IsCurrentIncarnation(BatchGenerations[i]);

        /// <summary>
        /// Clears batch references and returns pooled arrays to <see cref="ArrayPool{T}.Shared"/>.
        /// Must be called exactly once per PendingResponse entry — not idempotent due to struct copy semantics.
        /// </summary>
        public void ReturnBatchesArray()
        {
            ArrayPool<int>.Shared.Return(BatchGenerations);
            ArrayPool<ReadyBatch>.Shared.Return(Batches, clearArray: true);
        }
    }

    internal struct AckedResponsePass
    {
        public long Bytes { get; private set; }
        public long EarliestRequestStart { get; private set; }

        public void Add(long bytes, long requestStart)
        {
            EarliestRequestStart = Bytes == 0
                ? requestStart
                : Math.Min(EarliestRequestStart, requestStart);
            Bytes += bytes;
        }
    }

    private enum SendLoopEventType : byte
    {
        NewBatch,
        ResponseReady, // Lightweight signal: a response task completed, poll _pendingResponsesByConnection
        Unmute,
        TransactionEnrollmentReady
    }

    private readonly struct BatchReference
    {
        public readonly ReadyBatch Batch;
        public readonly int Generation;

        public BatchReference(ReadyBatch batch, int generation)
        {
            Batch = batch;
            Generation = generation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCurrentIncarnation() => Batch.IsCurrentIncarnation(Generation);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct SendLoopEvent
    {
        public readonly SendLoopEventType Type;
        public readonly ReadyBatch? Batch;
        public readonly int BatchGeneration;
        public readonly Exception? EnrollmentError;

        private SendLoopEvent(
            SendLoopEventType type,
            ReadyBatch? batch = null,
            int batchGeneration = 0,
            Exception? enrollmentError = null)
        {
            Type = type;
            Batch = batch;
            BatchGeneration = batchGeneration;
            EnrollmentError = enrollmentError;
        }

        public static SendLoopEvent NewBatch(ReadyBatch batch) => NewBatch(batch, batch.Generation);

        public static SendLoopEvent NewBatch(ReadyBatch batch, int generation)
            => new(SendLoopEventType.NewBatch, batch, generation);

        public BatchReference GetBatchReference() => new(Batch!, BatchGeneration);

        public static SendLoopEvent ResponseReady() => new(SendLoopEventType.ResponseReady);

        public static SendLoopEvent Unmute() => new(SendLoopEventType.Unmute);

        public static SendLoopEvent TransactionEnrollmentReady(Exception? error) =>
            new(SendLoopEventType.TransactionEnrollmentReady, enrollmentError: error);
    }

    /// <summary>
    /// Pre-allocated bucket for grouping coalesced batches by connection affinity.
    /// Single-threaded: only accessed by the send loop.
    /// </summary>
    private struct ConnectionBucket
    {
        public ReadyBatch[] Batches;
        public int[] Generations;
        public int Count;

        public void Clear()
        {
            Array.Clear(Batches, 0, Count);
            Array.Clear(Generations, 0, Count);
            Count = 0;
        }

        public readonly bool HasBatches => Count > 0;
    }

    /// <summary>
    /// Per-partition carry-over queues matching Java Kafka's per-partition Deque design.
    /// Retries go to the FRONT (AddFirst), new batches to the BACK (Add).
    /// Guarantees per-partition FIFO ordering during coalescing.
    /// Single-threaded: only accessed by the send loop.
    /// </summary>
    /// <remarks>
    /// Uses List&lt;BatchReference&gt; instead of LinkedList&lt;BatchReference&gt; to avoid per-operation
    /// LinkedListNode heap allocations. Carry-over queues are typically small (1-3 items per
    /// partition), so List.Insert(0, item) O(n) shifts are negligible compared to the GC
    /// pressure from LinkedListNode allocations at high throughput.
    /// </remarks>
    private sealed class PartitionCarryOver
    {
        private readonly Dictionary<TopicPartition, List<BatchReference>> _partitions = new();
        private int _count;

        public int Count => _count;
        public Dictionary<TopicPartition, List<BatchReference>> Partitions => _partitions;

        private List<BatchReference> GetOrCreateQueue(TopicPartition tp)
        {
            if (!_partitions.TryGetValue(tp, out var queue))
            {
                queue = new List<BatchReference>(4);
                _partitions[tp] = queue;
            }
            return queue;
        }

        /// <summary>Add to back of partition queue (normal carry-over, new batches).</summary>
        public void Add(BatchReference batchRef)
        {
            if (batchRef.Batch.IsLoopExitRedelivery)
            {
                AddLoopExitRedelivery(batchRef);
                return;
            }

            GetOrCreateQueue(batchRef.Batch.TopicPartition).Add(batchRef);
            _count++;
        }

        /// <summary>
        /// Add to front of partition queue (retries, epoch bump — older batches first).
        /// Matches Java's Deque.addFirst() for reenqueue.
        /// </summary>
        public void AddFirst(BatchReference batchRef)
        {
            if (batchRef.Batch.IsLoopExitRedelivery)
            {
                AddLoopExitRedelivery(batchRef);
                return;
            }

            GetOrCreateQueue(batchRef.Batch.TopicPartition).Insert(0, batchRef);
            _count++;
        }

        /// <summary>
        /// Add after existing retry batches but before non-retry batches.
        /// Used by HandleRetriableBatch where multiple responses may add retries
        /// for the same partition — preserves FIFO among retries while keeping
        /// them ahead of newer non-retry carry-over batches.
        /// </summary>
        /// <remarks>
        /// The scan + insert is O(n^2) worst case, but acceptable given typical
        /// per-partition queue sizes of 1-3 items.
        /// </remarks>
        public void AddAfterRetries(BatchReference batchRef)
        {
            if (batchRef.Batch.IsLoopExitRedelivery)
            {
                AddLoopExitRedelivery(batchRef);
                return;
            }

            var queue = GetOrCreateQueue(batchRef.Batch.TopicPartition);

            var insertIdx = 0;
            while (insertIdx < queue.Count && queue[insertIdx].Batch.IsRetry)
                insertIdx++;

            queue.Insert(insertIdx, batchRef);
            _count++;
        }

        private void AddLoopExitRedelivery(BatchReference batchRef)
        {
            var queue = GetOrCreateQueue(batchRef.Batch.TopicPartition);
            var insertIdx = 0;
            while (insertIdx < queue.Count
                && queue[insertIdx].Batch.IsLoopExitRedelivery
                && queue[insertIdx].Batch.LoopExitRedeliveryOrder <= batchRef.Batch.LoopExitRedeliveryOrder)
                insertIdx++;

            queue.Insert(insertIdx, batchRef);
            _count++;
        }

        /// <summary>
        /// Drains all batches to the destination in per-partition FIFO order, then clears.
        /// Used before coalescing to iterate in deterministic per-partition order.
        /// </summary>
        public void DrainTo(List<BatchReference> destination)
        {
            foreach (var kvp in _partitions)
            {
                var queue = kvp.Value;
                for (var i = 0; i < queue.Count; i++)
                    destination.Add(queue[i]);
                queue.Clear();
            }
            _count = 0;
        }

        public void Clear()
        {
            foreach (var kvp in _partitions)
                kvp.Value.Clear();
            _count = 0;
        }

        public void RemoveAt(List<BatchReference> queue, int index)
        {
            queue.RemoveAt(index);
            _count--;
        }
    }

    // Per-connection pending responses. Each list tracks in-flight requests for one connection.
    // Single-threaded: only accessed by the send loop. Pre-sized at the scaling ceiling.
    private readonly List<PendingResponse>[] _pendingResponsesByConnection;

    // Batches that failed during SendCoalescedAsync (connection error, etc.) and need retry.
    // Must be thread-safe: with multi-connection sends, multiple Z handlers (catch blocks)
    // can Enqueue simultaneously from different thread-pool threads. ConcurrentQueue provides
    // FIFO ordering (matching the old List iteration order) and is optimized for the
    // cross-thread producer-consumer pattern used here.
    //
    // The send loop drains this queue through HandleRetriableBatch so connection failures
    // trigger the same metadata refresh and ordering logic as broker error responses.
    //
    // Each entry carries the batch's generation captured at send start. ReadyBatch objects
    // are pooled, so a reference sitting in this queue can be completed and recycled by
    // another owner before the send loop dequeues it. IsReturnedToPool alone cannot detect
    // that (the flag is reset on re-rent); the generation comparison can. Dequeuers must
    // validate with IsCurrentIncarnation before acting on the batch — acting on a recycled
    // entry re-sends or fails a batch now owned by a different partition, which is the
    // root cause of the PRODUCE framing-guard failures in issue #1570.
    private readonly ConcurrentQueue<(ReadyBatch Batch, int Generation)> _sendFailedRetries = new();

    // Batches recovered from an unexpectedly exited sender. These must run before both
    // send-failure retries and normal channel backlog on the replacement sender. Their
    // separate queue preserves LoopExitRedeliveryOrder while carry-over insertion keeps
    // every crash recovery ahead of ordinary retries and new work.
    private readonly ConcurrentQueue<(ReadyBatch Batch, int Generation)> _loopExitRedeliveries = new();

    // Partitions accepted while this sender is alive. Guarded with _loopExited so an
    // unexpected exit can install every accumulator barrier before replacement is visible.
    private readonly HashSet<TopicPartition> _enqueuedPartitions = [];
    private readonly System.Threading.Lock _loopExitRedeliveryLock = new();
    private long _nextLoopExitRedeliveryOrder;

    // Non-blocking in-flight request limiter. The send loop uses _totalPendingResponseCount
    // (which it exclusively owns) as the in-flight measure. No cross-thread signaling needed.
    private readonly int _maxInFlight;
    private int _totalMaxInFlight;

    // Per-producer shared API version (read via volatile, written via Interlocked)
    private readonly Func<int> _getProduceApiVersion;
    private readonly Action<int> _setProduceApiVersion;

    // Transaction support
    private readonly Func<bool> _isTransactional;
    private readonly Func<ReadyBatch[], int, Action<Exception?>, HashSet<TopicPartition>,
        TransactionPartitionEnrollmentResult>?
        _tryEnsurePartitionsInTransaction;
    private readonly Action<Exception?> _transactionEnrollmentCompleted;
    private readonly HashSet<TopicPartition> _transactionEnrollmentPendingPartitions = [];

    // Send-time muting limits each partition to 1 in-flight batch when producer sequence
    // numbers are unavailable, or when the configured request limit is already 1.
    // Idempotent producers with MaxInFlight > 1 rely on sequence numbers instead.
    private readonly bool _muteOnSend;

    // Muted partitions: partitions with a retry in progress or limited to 1 in-flight
    // batch by _muteOnSend. Prevents newer batches from being sent, maintaining
    // per-partition ordering. Aligned with Java Kafka producer's mute mechanism.
    // Uses ConcurrentDictionary as a concurrent set (byte value unused): with multi-connection
    // sends, concurrent Z handlers (catch blocks in SendCoalescedAsync) can write from
    // different threads. HashSet<T> is not thread-safe for concurrent writes.
    private readonly ConcurrentDictionary<TopicPartition, byte> _mutedPartitions = new();
    private int _mutedPartitionCount;
    private int _mutedPartitionHighWatermark;

    // Epoch bump recovery flag (Java Kafka Sender pattern): set by response handlers
    // when OutOfOrderSequenceNumber is received. The single-threaded send loop checks
    // this before coalescing and bumps the epoch if needed, eliminating all races
    // between concurrent response handlers. Value is the stale epoch (-1 = no bump needed).
    private int _epochBumpRequestedForEpoch = -1;

    // Partitions that triggered OOSN/InvalidProducerEpoch and need sequence reset
    // during the next epoch bump (Java-style per-partition reset, KIP-360).
    // Single-threaded send loop — no locks needed.
    private readonly HashSet<TopicPartition> _partitionsNeedingSequenceReset = new();

    // Partition-affined connections: each partition pins to _pinnedConnections[GetConnectionForPartition(topicPartition)].
    // For single-connection mode (_connectionCount == 1), degenerates to the original pinned behavior.
    // All producers use partition affinity (partition % N) for CPU cache locality.
    // Idempotent producers additionally require affinity for per-partition sequence ordering;
    // during scaling, _migratingPartitions overrides the modulo to preserve ordering.
    private readonly IKafkaConnection?[] _pinnedConnections;
    private int _connectionCount;
    private readonly bool _isIdempotent;

    // Adaptive connection scaling state (send-loop owned, single-threaded)
    private bool _adaptiveScalingEnabled;
    private readonly bool _canPhysicallyShrinkConnections;
    private bool _hasScaledConnectionGroup;
    private readonly int _minConnectionCount; // Initial ConnectionsPerBroker — never scale below this
    private readonly int _maxConnectionsPerBroker;
    private long _lastPressureSnapshot;
    private long _sendLoopPressureEvents;
    private long _lastSendLoopPressureSnapshot;
    private long _lastScaleTimeTicks;
    private Task<int>? _pendingScaleTask; // Background connection creation, polled by send loop
    private double _pendingScaleBufferUtilization;
    private long _pendingScaleBufferPressureDelta;
    private long _pendingScaleSendLoopPressureDelta;

    // Scale-down state (send-loop owned, single-threaded)
    private long _lowUtilizationStartTicks; // When low utilization was first detected (0 = not tracking)
    private long _lastMutedPartitionLoadTicks; // Last observed load-bearing muted width
    private Task<IKafkaConnection?>? _pendingShrinkTask; // Background shrink, polled by send loop
    private IKafkaConnection? _drainingConnection; // Connection being drained before disposal

    // A connection removed from the pool by scale-down, parked until its pending-response
    // list (slot _connectionCount — frozen while parked, because a parked connection blocks
    // all new scale events) is confirmed empty, then promoted to _drainingConnection for
    // disposal. The list is expected to already be empty when parked; a non-empty list
    // means a request slipped in against the width-reduction invariant and is logged.
    private IKafkaConnection? _retiringConnection;
    private readonly ConcurrentDictionary<Task, byte> _retirementDrainTasks = new();

    // Set at send-loop finally entry so IsAlive turns false before surviving batches are
    // reroute-redelivered — GetOrCreateBrokerSender must build a replacement, not return
    // this instance whose event channel is already completed.
    private volatile bool _loopExited;
    private volatile bool _redeliverAfterLoopExit;

    // Per-partition migration fencing for all acknowledged producers: during a scale-UP,
    // partitions whose connection assignment changes (partition % oldCount != partition % newCount)
    // are fenced here if they have in-flight batches on the old connection. The partition
    // continues routing to the old connection until its in-flight clears, then is removed
    // from this dictionary and routes via the new partition % _connectionCount.
    // Scale-down never fences (its drain gates guarantee no in-flight) and clears any
    // stale entries at initiation. Send-loop owned (single-threaded) — no synchronization needed.
    private readonly Dictionary<TopicPartition, int> _migratingPartitions = new();
    // Reused removal buffer keeps CompleteMigrations O(migrating) and allocation-free.
    // Capacity grows during scale-up, outside response completion processing.
    private readonly List<TopicPartition> _migrationRemovalBuffer = [];

    // Scaling thresholds
    // Also the per-connection unit of pressure for step estimation (step = delta / threshold),
    // so changing this value affects both trigger sensitivity and per-step magnitude.
    private const long ScalePressureDeltaThreshold = 100;
    private const long ScaleCooldownMs = 5_000;
    private const double ScaleUtilizationThreshold = 0.7;

    private const int MaxScaleStep = 3; // Cap per scale-up to avoid over-provisioning from a brief spike

    // Scale-down thresholds
    private const double ScaleDownUtilizationThreshold = 0.3; // Buffer utilization below which scale-down is considered
    private const double ScaleDownInFlightUtilizationThreshold = 0.3;
    private const long ScaleDownSustainedMs = 120_000; // 2 minutes of sustained low utilization required

    // Effective scaling timings: the constants above unless overridden via the internal
    // ProducerOptions test knobs (so tests can drive scale cycles without waiting minutes).
    // Readonly — set once in the constructor before the send loop starts.
    private readonly long _scaleCooldownMs;
    private readonly long _scaleDownSustainedMs;

    // Maintained counter for O(1) hot-path access. Incremented in SendCoalescedAsync
    // when a PendingResponse is added, decremented in ProcessCompletedResponses and
    // HandleTimedOutRequests when entries are removed. Must use Interlocked: with
    // multi-connection sends, concurrent SendCoalescedAsync calls increment from
    // different threads. Non-atomic ++ silently loses increments, causing the send
    // loop to undercount pending responses and enter idle wait prematurely.
    private int _totalPendingResponseCount;
    private long _totalPendingResponseBytes;
    private long _totalMaxInFlightBytes;
    private readonly long _maxInFlightBytesPerConnection;
    private readonly long[] _pendingResponseBytesByConnection;

    // Per-broker unacked-byte admission budget (owned by the accumulator, shared with the
    // producer's admission gate). This send loop is the single writer of its drain-rate
    // and minimum-RTT estimates (OnAcked) and cap (SetCap); null when the bound is disabled.
    private readonly BrokerUnackedByteBudget? _unackedBudget;

    // Snapshot of the budget's admission-block counter for scale-pressure deltas. The gate
    // keeps BufferUtilization near zero, so blocked admissions must feed scale-up directly.
    // Reset only on successful scale-up (like _lastPressureSnapshot) so pressure keeps
    // accumulating across failed grow attempts.
    private long _lastUnackedBlockSnapshot;

    // Recency tracking for the scale-down veto: the counter value last seen by the scale
    // check, and the monotonic-ms time of its last observed movement. Kept separate from
    // the scale-up snapshot above, whose reset-on-scale-up policy would otherwise pin the
    // veto forever on workloads that can never grow (at the ceiling or partition-capped).
    private long _lastUnackedBlockObserved;
    private long _lastUnackedBlockObservedMs;

    // Broker quota throttling is scoped per broker, not per connection. A positive
    // ProduceResponse.ThrottleTimeMs pauses every connection owned by this BrokerSender.
    // Zero is the common fast path and leaves this sentinel at zero.
    private long _brokerThrottleUntilTimestamp;

    // Tracks distinct partitions this broker has seen, used to skip MicroLinger when all
    // known partitions are already coalesced (e.g., single-partition topics).
    // Conservative: a brand-new partition not yet in _knownPartitions may miss one
    // coalescing opportunity on its first appearance — benign, picked up next iteration.
    // Single-threaded: only accessed by the send loop.
    private readonly HashSet<TopicPartition> _knownPartitions = [];

    /// <summary>
    /// Zero-allocation async auto-reset signal for response completion notification.
    /// Uses <see cref="ManualResetValueTaskSourceCore{TResult}"/> internally — no per-wait
    /// TaskNode allocations unlike <c>SemaphoreSlim.WaitAsync(CancellationToken)</c>.
    /// Timeout is handled by an internal reusable <see cref="Timer"/> (no per-call allocation).
    /// Single waiter (send loop), multiple signalers (response callbacks on thread pool).
    /// </summary>
    private readonly AsyncAutoResetSignal _anyResponseCompleted = new();

    /// <summary>
    /// Pre-allocated callback for response completion signaling.
    /// Caches a single <see cref="Action"/> delegate that is reused across all response tasks,
    /// eliminating the continuation <see cref="Task"/> allocation that <c>ContinueWith</c> would create.
    /// The <see cref="Action"/> is registered via <c>UnsafeOnCompleted</c> which has no return value
    /// (no Task allocation).
    /// </summary>
    /// <remarks>
    /// <c>UnsafeOnCompleted</c> is used instead of <c>OnCompleted</c> because it skips
    /// <see cref="System.Threading.ExecutionContext"/> capture/restore. This is safe because the
    /// callback only writes to a channel and signals an <see cref="AsyncAutoResetSignal"/> —
    /// no ambient context (e.g. <c>AsyncLocal</c>, <c>SecurityContext</c>) needs to flow.
    /// </remarks>
    private readonly Action _responseCompletionCallback;
    private int _disposed;

    public BrokerSender(
        int brokerId,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        RecordAccumulator accumulator,
        ProducerOptions options,
        CompressionCodecRegistry compressionCodecs,
        PartitionInflightTracker inflightTracker,
        Func<int> getProduceApiVersion,
        Action<int> setProduceApiVersion,
        Func<bool> isTransactional,
        Func<ReadyBatch[], int, Action<Exception?>, HashSet<TopicPartition>,
            TransactionPartitionEnrollmentResult>?
            tryEnsurePartitionsInTransaction,
        Func<short, IReadOnlyCollection<TopicPartition>, (long ProducerId, short ProducerEpoch)>? bumpEpoch,
        Func<short>? getCurrentEpoch,
        Action<ReadyBatch, int>? rerouteBatch,
        Action<TopicPartition, long, DateTimeOffset, int, Exception?>? onAcknowledgement,
        ILogger? logger,
        bool canPhysicallyShrinkConnections = true,
        Action<int>? onBrokerThrottle = null,
        Func<long>? getTimestamp = null,
        Func<int, CancellationToken, ValueTask>? delayForThrottle = null,
        Action? onBlockedBucketRequeued = null,
        BrokerUnackedByteBudget? unackedBudget = null,
        TimeSpan? disposalDrainTimeout = null)
    {
        _unackedBudget = unackedBudget;
        _brokerId = brokerId;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _accumulator = accumulator;
        _options = options;
        _compressionCodecs = compressionCodecs;
        _inflightTracker = inflightTracker;
        _getProduceApiVersion = getProduceApiVersion;
        _setProduceApiVersion = setProduceApiVersion;
        _isTransactional = isTransactional;
        _tryEnsurePartitionsInTransaction = tryEnsurePartitionsInTransaction;
        _bumpEpoch = bumpEpoch;
        _getCurrentEpoch = getCurrentEpoch;
        _rerouteBatch = rerouteBatch;
        _onAcknowledgement = onAcknowledgement;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _onBrokerThrottle = onBrokerThrottle;
        _getTimestamp = getTimestamp ?? Stopwatch.GetTimestamp;
        _delayForThrottle = delayForThrottle ?? DelayForThrottleAsync;
        _onBlockedBucketRequeued = onBlockedBucketRequeued;
        _disposalDrainTimeout = disposalDrainTimeout ?? DisposalDrainTimeout;

        _eventChannel = Channel.CreateUnbounded<SendLoopEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _transactionEnrollmentCompleted = error =>
            _eventChannel.Writer.TryWrite(SendLoopEvent.TransactionEnrollmentReady(error));

        _maxInFlight = options.MaxInFlightRequestsPerConnection;
        _isIdempotent = options.EnableIdempotence;

        // Non-idempotent batches have no sequence numbers, so concurrent requests for the
        // same partition can be appended out of order even when no retry occurs.
        _muteOnSend = !_isIdempotent || _maxInFlight <= 1;

        // All producers use partition affinity (partition % connectionCount) for CPU cache
        // locality. Idempotent producers additionally need affinity for sequence ordering.
        _connectionCount = options.ConnectionsPerBroker;
        _totalMaxInFlight = _connectionCount * _maxInFlight;
        _maxInFlightBytesPerConnection = GetInFlightByteBudget(connectionCount: 1);
        UpdateInFlightByteBudget(_connectionCount);

        // Transactions are excluded because coordinator requests require a single connection.
        // Acks.None is excluded because no broker acknowledgement establishes a safe boundary
        // for remapping a partition to another connection without risking reordered appends.
        _adaptiveScalingEnabled = options.EnableAdaptiveConnections
            && options.TransactionalId is null
            && options.Acks != Acks.None;
        _canPhysicallyShrinkConnections = canPhysicallyShrinkConnections;
        _minConnectionCount = options.ConnectionsPerBroker;
        _maxConnectionsPerBroker = options.MaxConnectionsPerBroker;
        _scaleCooldownMs = options.ScaleCooldownMsOverride ?? ScaleCooldownMs;
        _scaleDownSustainedMs = options.ScaleDownSustainedMsOverride ?? ScaleDownSustainedMs;

        // Per-connection arrays are allocated once at the adaptive-scaling ceiling and never
        // resized: scale events change only _connectionCount, and slots beyond it sit unused.
        // Response/timeout scans iterate the full array, so a slot that leaves the routing
        // width during a scale-down keeps having its in-flight requests polled to completion
        // instead of being dropped by an array truncation.
        var connectionCapacity = _adaptiveScalingEnabled
            ? Math.Max(_connectionCount, _maxConnectionsPerBroker)
            : _connectionCount;
        _pinnedConnections = new IKafkaConnection?[connectionCapacity];
        _pendingResponsesByConnection = new List<PendingResponse>[connectionCapacity];
        _pendingResponseBytesByConnection = new long[connectionCapacity];
        for (var i = 0; i < connectionCapacity; i++)
            _pendingResponsesByConnection[i] = new List<PendingResponse>();

        _responseCompletionCallback = () =>
        {
            _eventChannel.Writer.TryWrite(SendLoopEvent.ResponseReady());
            _anyResponseCompleted.Signal();
        };
        _cts = new CancellationTokenSource();
        _sendLoopTask = Task.Factory.StartNew(
            () => SendLoopAsync(_cts.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private static ValueTask DelayForThrottleAsync(int delayMs, CancellationToken cancellationToken) =>
        new(Task.Delay(delayMs, cancellationToken));

    /// <summary>
    /// Returns true if the send loop is still running. When false, this BrokerSender
    /// should be replaced — its send loop has exited and it can no longer process batches.
    /// </summary>
    internal bool IsAlive => !_loopExited && !_sendLoopTask.IsCompleted;

    /// <summary>
    /// Requests cancellation of this BrokerSender's send loop without waiting for it to exit.
    /// Called during forceful shutdown so all BrokerSender loops begin exiting concurrently
    /// before DisposeAsync awaits each one.
    /// Uses synchronous Cancel() because this is a fire-and-forget context where we cannot await.
    /// Safe to call multiple times: TryComplete and Cancel are both idempotent.
    /// </summary>
    /// <remarks>
    /// Wrapped in try/catch because <c>Cancel()</c> invokes registered callbacks synchronously.
    /// If a callback ever throws, the exception must not prevent cancellation of remaining senders
    /// in the caller's foreach loop.
    /// </remarks>
    internal void RequestCancellation()
    {
        _eventChannel.Writer.TryComplete();
        try { _cts.Cancel(); }
        catch (ObjectDisposedException) { /* CTS already disposed by a concurrent DisposeAsync */ }
    }

    /// <summary>
    /// Bulk enqueue for the sender loop: writes all batches to the event channel before the
    /// send loop can wake and read them, ensuring all batches are available for coalescing
    /// into a single ProduceRequest. This reduces per-request overhead in multi-broker setups
    /// where each broker receives only a few partitions per drain cycle.
    /// </summary>
    /// <remarks>
    /// Channel.TryWrite on unbounded channels always succeeds unless the channel is completed.
    /// Writing all events in a tight loop (no yields) maximizes the chance they are all present
    /// when the send loop reads from the channel.
    /// </remarks>
    public void EnqueueBulk(List<ReadyBatch> batches)
    {
        var writer = _eventChannel.Writer;
        var rejectedIndex = -1;
        var rerouteRejected = false;

        lock (_loopExitRedeliveryLock)
        {
            if (_loopExited)
            {
                rejectedIndex = 0;
                rerouteRejected = true;
            }
            else
            {
                for (var i = 0; i < batches.Count; i++)
                {
                    _enqueuedPartitions.Add(batches[i].TopicPartition);
                    if (!writer.TryWrite(SendLoopEvent.NewBatch(batches[i])))
                    {
                        rejectedIndex = i;
                        break;
                    }
                }
            }
        }

        if (rejectedIndex < 0)
            return;

        // Channel completion during disposal keeps the existing fail behavior. A batch that
        // raced an unexpected loop exit is handed to the replacement sender instead.
        for (var i = rejectedIndex; i < batches.Count; i++)
        {
            if (rerouteRejected)
                RerouteRejectedBatch(batches[i], batches[i].Generation);
            else
                FailEnqueuedBatch(batches[i], batches[i].Generation);
        }
    }

    /// <summary>
    /// Synchronous enqueue for the reroute callback (called from BrokerSender's send loop
    /// when a retry discovers the leader moved). TryWrite on unbounded channel always succeeds
    /// unless the channel is completed.
    /// </summary>
    public void Enqueue(ReadyBatch batch) => Enqueue(batch, batch.Generation);

    internal void Enqueue(ReadyBatch batch, int expectedGeneration)
    {
        if (!batch.TryAcquireResourcePin(expectedGeneration))
            return;

        var reroute = false;
        var enqueued = false;
        try
        {
            lock (_loopExitRedeliveryLock)
            {
                if (_loopExited)
                {
                    reroute = true;
                }
                else
                {
                    var topicPartition = batch.TopicPartition;
                    _enqueuedPartitions.Add(topicPartition);

                    if (batch.IsLoopExitRedelivery)
                    {
                        // Recovery pre-dates normal work already owned by this sender.
                        batch.LoopExitRedeliveryOrder = ++_nextLoopExitRedeliveryOrder;
                        _loopExitRedeliveries.Enqueue((batch, expectedGeneration));
                        _eventChannel.Writer.TryWrite(SendLoopEvent.Unmute());
                        enqueued = true;
                    }
                    else
                    {
                        enqueued = _eventChannel.Writer.TryWrite(
                            SendLoopEvent.NewBatch(batch, expectedGeneration));
                    }
                }
            }
        }
        finally
        {
            batch.ReleaseResourcePin();
        }

        if (reroute)
            RerouteRejectedBatch(batch, expectedGeneration);
        else if (!enqueued)
            FailEnqueuedBatch(batch, expectedGeneration);
    }

    private void RerouteRejectedBatch(ReadyBatch batch, int expectedGeneration)
    {
        if (_redeliverAfterLoopExit && _rerouteBatch is not null)
        {
            try
            {
                // KafkaProducer disposes a crashed sender as soon as its first survivor
                // creates the replacement. Callers that already captured this sender can
                // still race in afterward; they must follow that same replacement path.
                // Producer disposal is guarded by the reroute callback itself.
                _rerouteBatch(batch, expectedGeneration);
                return;
            }
            catch (Exception ex)
            {
                try { FailAndCleanupBatch(batch, expectedGeneration, ex); }
                catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                return;
            }
        }

        FailEnqueuedBatch(batch, expectedGeneration);
    }

    private void FailEnqueuedBatch(ReadyBatch batch, int expectedGeneration)
    {
        if (!batch.TryAcquireResourcePin(expectedGeneration))
            return;

        try
        {
            if (batch.IsRetry)
            {
                batch.IsRetry = false;
                UnmutePartition(batch.TopicPartition);
            }
        }
        finally
        {
            batch.ReleaseResourcePin();
        }

        FailAndCleanupBatch(batch, expectedGeneration,
            new ObjectDisposedException(nameof(BrokerSender)));
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
        var maxCoalesce = (int)Math.Clamp(
            (long)_options.MaxInFlightRequestsPerConnection * 4,
            4L,
            MaxCoalescedBatchesPerPass);

        var coalescedPartitions = new HashSet<TopicPartition>();
        var carryOver = new PartitionCarryOver();
        var drainList = new List<BatchReference>();

        var coalescedBatches = new ReadyBatch[maxCoalesce];
        var coalescedGenerations = new int[maxCoalesce];
        var coalescedCount = 0;
        // Sum individually framed batch sizes, matching RecordAccumulator.Drain's
        // conservative request budget across batches from separate drain passes.
        var coalescedRequestBudgetUsed = 0L;

        // Pre-allocate reusable response lookup dictionary to avoid per-response allocation.
        // Single-threaded: only accessed by the send loop, cleared after each use.
        var responseLookup = new Dictionary<(string Topic, int Partition), ProduceResponsePartitionData>();

        // Pre-allocate reusable ProduceRequest structures.
        // The send loop is single-threaded. Acks.None single-connection sends can be
        // one write ahead, so that path gets a second scratch slot before reuse.
        var requestScratch = new ProduceRequestScratch(_options, _compressionCodecs, maxCoalesce);
        var singleConnectionFireAndForgetScratches = _options.Acks == Acks.None
            ? new[] { requestScratch, new ProduceRequestScratch(_options, _compressionCodecs, maxCoalesce) }
            : Array.Empty<ProduceRequestScratch>();

        // Per-connection scratch and buckets for partition-affined multi-send.
        // Arrays are sized at the adaptive-scaling ceiling (matching the per-connection
        // field arrays) so scale events never resize them — entries beyond the current
        // connection count are created lazily on scale-up. When _connectionCount == 1,
        // only slot 0 is populated — no grouping overhead.
        var connectionCapacity = _pendingResponsesByConnection.Length;
        var scratches = new ProduceRequestScratch[connectionCapacity];
        scratches[0] = requestScratch; // Reuse the existing one for index 0
        for (var c = 1; c < _connectionCount; c++)
            scratches[c] = new ProduceRequestScratch(_options, _compressionCodecs, maxCoalesce);

        var connectionBuckets = new ConnectionBucket[connectionCapacity];
        for (var c = 0; c < _connectionCount; c++)
        {
            connectionBuckets[c].Batches = new ReadyBatch[maxCoalesce];
            connectionBuckets[c].Generations = new int[maxCoalesce];
        }

        var sendTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var singleConnectionFireAndForgetTimeoutCts = _options.Acks == Acks.None
            ? new[]
            {
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            }
            : Array.Empty<CancellationTokenSource>();
        var pendingSingleConnectionFireAndForgetSend = default(ValueTask);
        var hasPendingSingleConnectionFireAndForgetSend = false;
        var nextSingleConnectionFireAndForgetSlot = 0;

        // Pre-allocated array for parallel multi-connection sends. Each entry holds
        // a pending SendConnectionBucketAsync ValueTask so connections can flush concurrently
        // instead of sequentially (overlaps TCP FlushAsync waits across connections).
        var parallelSends = connectionCapacity > 1
            ? new ValueTask[connectionCapacity]
            : Array.Empty<ValueTask>();

        // Shared by all connection buckets dispatched in one send-loop iteration. Send
        // failures can complete concurrently, so PrepareDeferredNetworkRetry protects it.
        HashSet<string>? retryMetadataRefreshTopics = null;

        // Per-connection timeout CTS for multi-connection mode, reused with TryReset()
        // to avoid per-send CTS allocation (same pattern as sendTimeoutCts for single-connection).
        // Entries beyond the current connection count are created lazily on scale-up.
        var bucketTimeoutCts = connectionCapacity > 1
            ? new CancellationTokenSource[connectionCapacity]
            : Array.Empty<CancellationTokenSource>();
        for (var c = 0; c < _connectionCount && c < bucketTimeoutCts.Length; c++)
            bucketTimeoutCts[c] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Register shutdown token once — persists for the lifetime of the send loop.
        // Zero per-wait allocation: the signal uses an internal reusable timer for the
        // 100ms poll timeout, and this one-time registration handles shutdown cancellation.
        _anyResponseCompleted.RegisterShutdownToken(cancellationToken);

        async ValueTask AwaitPendingSingleConnectionFireAndForgetSendAsync()
        {
            if (!hasPendingSingleConnectionFireAndForgetSend)
                return;

            await pendingSingleConnectionFireAndForgetSend.ConfigureAwait(false);
            pendingSingleConnectionFireAndForgetSend = default;
            hasPendingSingleConnectionFireAndForgetSend = false;
        }

        // Records an UNEXPECTED loop exit (escaped exception) as opposed to shutdown —
        // set at the one catch site that knows, rather than derived in the finally from
        // shared state that a concurrent RequestCancellation mutates in two steps.
        var crashed = false;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                LogSendLoopIteration(_brokerId, carryOver.Count, _totalPendingResponseCount);
                var transactionEnrollmentReady = false;

                // ── 1. Poll pending responses (like Java's client.poll()) ──
                // Signal events (ResponseReady, Unmute) may have woken us up — processing
                // completed responses here handles them. Batch events stay in the channel
                // and are read lazily during coalescing (step 5) to avoid O(n²) carry-over growth.
                ProcessCompletedResponses(carryOver, cancellationToken, responseLookup);

                // ── 2. Pick up send-failed retries ──
                // Process these through the common retriable-error path. In particular, a
                // stopped leader often fails before returning a ProduceResponse, so this is
                // the only point that can request fresh metadata before the stale route retries.
                while (_sendFailedRetries.TryDequeue(out var retry))
                {
                    if (retry.Batch.IsCurrentIncarnation(retry.Generation))
                    {
                        HandleRetriableBatch(
                            retry.Batch,
                            retry.Generation,
                            ErrorCode.NetworkException,
                            carryOver,
                            cancellationToken,
                            networkRetryPrepared: true);
                    }
                    else
                        LogStaleRetryBatchSkipped(_instanceId, _brokerId);
                }

                // Crash-recovered batches predate all work already owned by this replacement
                // sender. Persistent carry-over insertion preserves FIFO across loop cycles
                // while keeping every recovery ahead of send-failure retries and normal work.
                while (_loopExitRedeliveries.TryDequeue(out var redelivery))
                {
                    if (redelivery.Batch.IsCurrentIncarnation(redelivery.Generation))
                        carryOver.Add(new BatchReference(redelivery.Batch, redelivery.Generation));
                    else
                    {
                        LogStaleRetryBatchSkipped(_instanceId, _brokerId);
                    }
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

                // ── 4b. Adaptive connection scaling ──
                if (_adaptiveScalingEnabled)
                {
                    var hasUnreadEvent = !_isIdempotent && eventReader.TryPeek(out _);
                    var scaledToCount = MaybeScaleConnections(carryOver, hasUnreadEvent);

                    if (scaledToCount > 0)
                    {
                        // Arrays are pre-sized at the scaling ceiling (scale targets are
                        // clamped to it) — just fill in any slots that have never been
                        // used. Entries persist across a scale-down for reuse on the next
                        // scale-up; the CTS entries are disposed in the send loop's
                        // finally block (scratches and buckets are plain managed objects).
                        for (var i = 0; i < scaledToCount; i++)
                        {
                            scratches[i] ??= new ProduceRequestScratch(_options, _compressionCodecs, maxCoalesce);
                            if (connectionBuckets[i].Batches is null)
                            {
                                connectionBuckets[i].Batches = new ReadyBatch[maxCoalesce];
                                connectionBuckets[i].Generations = new int[maxCoalesce];
                            }
                            bucketTimeoutCts[i] ??= CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        }
                    }
                }

                if (_connectionCount > 1)
                    await AwaitPendingSingleConnectionFireAndForgetSendAsync().ConfigureAwait(false);

                // ── 5. Coalesce ──
                coalescedPartitions.Clear();
                coalescedCount = 0;
                coalescedRequestBudgetUsed = 0;

                // Drain carry-over into temp list for iteration. Per-partition FIFO order
                // ensures oldest batch per partition is seen first (Java's Deque.pollFirst).
                drainList.Clear();
                var hadCarryOver = carryOver.Count > 0;
                if (hadCarryOver)
                    carryOver.DrainTo(drainList);

                for (var i = 0; i < drainList.Count; i++)
                {
                    CoalesceBatch(drainList[i], coalescedBatches, coalescedGenerations, ref coalescedCount,
                        ref coalescedRequestBudgetUsed, coalescedPartitions, carryOver);
                }

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
                    while (coalescedCount < maxCoalesce
                        && channelReads < maxCoalesce
                        && eventReader.TryRead(out var evt))
                    {
                        if (evt.Type == SendLoopEventType.NewBatch)
                        {
                            var carryBefore = carryOver.Count;
                            channelReads++;
                            CoalesceBatch(evt.GetBatchReference(), coalescedBatches, coalescedGenerations, ref coalescedCount,
                                ref coalescedRequestBudgetUsed, coalescedPartitions, carryOver);
                            if (carryOver.Count > carryBefore)
                            {
                                channelCarryOvers++;
                                if (channelCarryOvers >= carryOverBudget)
                                    break;
                            }
                        }
                        else if (evt.Type == SendLoopEventType.TransactionEnrollmentReady)
                        {
                            if (evt.EnrollmentError is not null)
                            {
                                FailPendingTransactionEnrollmentBatches(
                                    carryOver,
                                    _transactionEnrollmentPendingPartitions,
                                    evt.EnrollmentError);
                            }
                            _transactionEnrollmentPendingPartitions.Clear();
                            transactionEnrollmentReady = true;
                            break;
                        }
                        // ResponseReady/Unmute: consumed as wake-up signals, no data to process
                    }
                }

                if (carryOver.Count > 0)
                    SweepExpiredCarryOver(carryOver);

                if (carryOver.Count > 0 || coalescedCount == maxCoalesce)
                    RecordSendLoopPressureIfScaleUseful();

                // ── 5b. Micro-linger for small coalesced batches ──
                // When very few batches are coalesced (e.g., broker has only 2 partitions in a
                // multi-broker setup), sending immediately produces many small ProduceRequests.
                // A brief adaptive spin gives the sender coordinator time to post more batches,
                // reducing per-request overhead. SpinWait adapts across core counts (spins on
                // multi-core, yields on single-core) and avoids kernel transitions when possible.
                // The iteration count caps total work (both spins and channel reads).
                //
                // Skip when all known partitions are already coalesced — any new batch from the
                // channel would be for an already-coalesced partition and get carried over, making
                // the spin pure waste. This is especially important for single-partition topics
                // where coalescedCount is always 1 and every MicroLinger iteration is wasted.
                if (coalescedCount > 0 && coalescedCount <= MicroLingerBatchThreshold
                    && coalescedCount < maxCoalesce
                    && coalescedPartitions.Count < _knownPartitions.Count
                    && carryOver.Count == 0
                    && Volatile.Read(ref _totalPendingResponseCount) < _totalMaxInFlight)
                {
                    var spinWait = new SpinWait();
                    for (var spin = 0; spin < MicroLingerMaxSpins && coalescedCount < maxCoalesce; spin++)
                    {
                        if (!eventReader.TryRead(out var evt))
                        {
                            spinWait.SpinOnce();
                            continue;
                        }

                        if (evt.Type == SendLoopEventType.NewBatch)
                        {
                            CoalesceBatch(evt.GetBatchReference(), coalescedBatches, coalescedGenerations, ref coalescedCount,
                                ref coalescedRequestBudgetUsed, coalescedPartitions, carryOver);
                            if (coalescedCount > MicroLingerBatchThreshold)
                                break;
                        }
                        else if (evt.Type == SendLoopEventType.TransactionEnrollmentReady)
                        {
                            if (evt.EnrollmentError is not null)
                            {
                                FailPendingTransactionEnrollmentBatches(
                                    carryOver,
                                    _transactionEnrollmentPendingPartitions,
                                    evt.EnrollmentError);
                            }
                            _transactionEnrollmentPendingPartitions.Clear();
                            transactionEnrollmentReady = true;
                            break;
                        }
                    }
                }

                // ── 6. Send or wait ──
                var sentThisIteration = false;
                var requeuedBlockedBuckets = false;
                if (coalescedCount > 0)
                {
                    var pendingCount = Volatile.Read(ref _totalPendingResponseCount);
                    if (pendingCount >= _totalMaxInFlight)
                    {
                        RecordSendLoopPressureIfScaleUseful();
                        LogWaitingForInFlightCapacity(_brokerId, pendingCount, _totalMaxInFlight);
                    }

                    var pendingBytes = Interlocked.Read(ref _totalPendingResponseBytes);
                    while (pendingCount >= _totalMaxInFlight
                        || (_connectionCount == 1 && pendingBytes >= _totalMaxInFlightBytes))
                    {
                        // Poll for completed responses to free in-flight slots.
                        ProcessCompletedResponses(carryOver, cancellationToken, responseLookup);
                        pendingCount = Volatile.Read(ref _totalPendingResponseCount);
                        pendingBytes = Interlocked.Read(ref _totalPendingResponseBytes);

                        if (pendingCount >= _totalMaxInFlight
                            || (_connectionCount == 1 && pendingBytes >= _totalMaxInFlightBytes))
                        {
                            RecordSendLoopPressureIfScaleUseful();

                            // Handle timed-out requests (Java pattern) to free zombie entries.
                            // Without this, the send loop blocks forever when a response task
                            // never completes.
                            HandleTimedOutRequests(carryOver, cancellationToken);
                            pendingCount = Volatile.Read(ref _totalPendingResponseCount);
                            pendingBytes = Interlocked.Read(ref _totalPendingResponseBytes);
                            if (pendingCount < _totalMaxInFlight
                                && (_connectionCount > 1 || pendingBytes < _totalMaxInFlightBytes))
                                break;

                            // Wait for any pending response to complete.
                            // Cannot use eventReader.WaitToReadAsync here — the channel may
                            // contain unread NewBatch events that cause immediate (synchronous)
                            // return, creating a spin loop that starves the thread pool and
                            // prevents I/O completion callbacks from running.
                            await WaitForAnyResponseAsync(cancellationToken).ConfigureAwait(false);
                            pendingCount = Volatile.Read(ref _totalPendingResponseCount);
                            pendingBytes = Interlocked.Read(ref _totalPendingResponseBytes);
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
                            var batch = coalescedBatches[src];
                            var generation = coalescedGenerations[src];

                            if (!batch.IsCurrentIncarnation(generation))
                            {
                                LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                                continue;
                            }

                            if (!batch.IsRetry
                                && (_mutedPartitions.ContainsKey(batch.TopicPartition)
                                    || _accumulator.IsMuted(batch.TopicPartition)))
                            {
                                // Normal batch whose partition was muted during the in-flight
                                // wait (a different batch for this partition triggered a retry).
                                // Must not send — it would arrive after the retry, violating order.
                                carryOver.AddAfterRetries(new BatchReference(batch, generation));
                            }
                            else
                            {
                                coalescedBatches[dst] = batch;
                                coalescedGenerations[dst] = generation;
                                dst++;
                            }
                        }

                        // Clear trailing slots so we don't hold stale references
                        for (var i = dst; i < coalescedCount; i++)
                        {
                            coalescedBatches[i] = null!;
                            coalescedGenerations[i] = 0;
                        }

                        coalescedCount = dst;
                    }

                    // If a response processed during the in-flight wait triggered an epoch
                    // bump request, we must NOT send the already-coalesced batches — they have
                    // stale epoch/sequences. Move them back to carry-over and loop back so the
                    // epoch bump (step 4) runs before re-coalescing.
                    if (coalescedCount > 0 && Volatile.Read(ref _epochBumpRequestedForEpoch) >= 0)
                    {
                        MoveCoalescedToCarryOver(
                            coalescedBatches, coalescedGenerations, ref coalescedCount, carryOver);
                        continue;
                    }

                    if (coalescedCount > 0)
                    {
                        var throttleDelayMs = GetRemainingBrokerThrottleMs();
                        if (throttleDelayMs > 0)
                        {
                            MoveCoalescedToCarryOver(
                                coalescedBatches, coalescedGenerations, ref coalescedCount, carryOver);
                            SweepExpiredCarryOver(carryOver);

                            if (carryOver.Count == 0)
                                continue;

                            var nextDeadlineMs = ComputeNextWakeupMs(carryOver);

                            // Keep polling already-sent requests while new sends are throttled.
                            // Their acknowledgements must not wait behind the broker delay.
                            if (Volatile.Read(ref _totalPendingResponseCount) > 0)
                            {
                                var responseWaitMs = ComputeThrottledResponseWaitMs(
                                    throttleDelayMs,
                                    nextDeadlineMs);
                                await WaitForAnyResponseAsync(cancellationToken, responseWaitMs)
                                    .ConfigureAwait(false);
                                continue;
                            }

                            var delayMs = Math.Min(throttleDelayMs, nextDeadlineMs);
                            if (delayMs > 0)
                                await _delayForThrottle(delayMs, cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                    }

                    if (coalescedCount > 0)
                    {
                        coalescedCount = CompactCurrentBatches(coalescedBatches, coalescedGenerations, coalescedCount);
                        if (coalescedCount == 0)
                            continue;

                        if (_isTransactional() && _tryEnsurePartitionsInTransaction is not null)
                        {
                            var enrollment = _tryEnsurePartitionsInTransaction(
                                coalescedBatches,
                                coalescedCount,
                                _transactionEnrollmentCompleted,
                                _transactionEnrollmentPendingPartitions);
                            if (enrollment.Error is not null)
                            {
                                for (var i = 0; i < coalescedCount; i++)
                                {
                                    try
                                    {
                                        FailAndCleanupBatch(
                                            coalescedBatches[i],
                                            coalescedGenerations[i],
                                            enrollment.Error);
                                    }
                                    catch (Exception cleanupError)
                                    {
                                        LogBatchCleanupStepFailed(cleanupError, _brokerId);
                                    }
                                }

                                Array.Clear(coalescedBatches, 0, coalescedCount);
                                Array.Clear(coalescedGenerations, 0, coalescedCount);
                                coalescedCount = 0;
                                continue;
                            }

                            if (!enrollment.IsEnrolled)
                            {
                                MoveEnrollmentPendingToCarryOver(
                                    coalescedBatches,
                                    coalescedGenerations,
                                    ref coalescedCount,
                                    carryOver,
                                    _transactionEnrollmentPendingPartitions);
                                if (coalescedCount == 0)
                                    continue;
                            }
                        }

                        // Finalize retry batches now that we know they will actually be sent
                        // (epoch bump check passed). Clear IsRetry and unmute partitions.
                        FinalizeCoalescedRetries(coalescedBatches, coalescedCount);

                        LogSendingCoalesced(_brokerId, coalescedCount);
                        for (var si = 0; si < coalescedCount; si++)
                            coalescedBatches[si].AppendDiag('S');
                        if (_connectionCount == 1)
                        {
                            // === Single-connection fast path (no grouping overhead) ===
                            var batchesToSend = ArrayPool<ReadyBatch>.Shared.Rent(coalescedCount);
                            var countToSend = 0;
                            for (var i = 0; i < coalescedCount; i++)
                            {
                                var batch = coalescedBatches[i];
                                if (batch.IsCurrentIncarnation(coalescedGenerations[i]))
                                    batchesToSend[countToSend++] = batch;
                                else
                                    LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                            }
                            Array.Clear(coalescedBatches, 0, coalescedCount);
                            Array.Clear(coalescedGenerations, 0, coalescedCount);
                            coalescedCount = 0;

                            if (countToSend == 0)
                            {
                                ArrayPool<ReadyBatch>.Shared.Return(batchesToSend, clearArray: true);
                                continue;
                            }

                            if (_options.Acks == Acks.None)
                            {
                                var slot = nextSingleConnectionFireAndForgetSlot;
                                nextSingleConnectionFireAndForgetSlot = 1 - nextSingleConnectionFireAndForgetSlot;

                                // The single-connection fire-and-forget path starts the next write
                                // before awaiting the previous flush. Fence these partitions before
                                // starting the write so a newer same-partition batch stays in
                                // carry-over while other partitions can still use the open slot.
                                MutePartitionsForSend(batchesToSend, countToSend);

                                ResetBucketTimeout(ref singleConnectionFireAndForgetTimeoutCts[slot],
                                    cancellationToken);

                                var currentSend = SendCoalescedAsync(
                                    batchesToSend,
                                    countToSend,
                                    singleConnectionFireAndForgetScratches[slot],
                                    0,
                                    singleConnectionFireAndForgetTimeoutCts[slot].Token);

                                if (hasPendingSingleConnectionFireAndForgetSend)
                                {
                                    try
                                    {
                                        await pendingSingleConnectionFireAndForgetSend.ConfigureAwait(false);
                                    }
                                    catch
                                    {
                                        try { await currentSend.ConfigureAwait(false); }
                                        catch { }
                                        throw;
                                    }
                                }

                                pendingSingleConnectionFireAndForgetSend = currentSend;
                                hasPendingSingleConnectionFireAndForgetSend = true;
                            }
                            else
                            {
                                if (!sendTimeoutCts.TryReset())
                                {
                                    sendTimeoutCts.Dispose();
                                    sendTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                                }
                                sendTimeoutCts.CancelAfter(SendCoalescedTimeoutMs);

                                await SendCoalescedAsync(batchesToSend, countToSend,
                                        scratches[0], 0, sendTimeoutCts.Token)
                                    .ConfigureAwait(false);
                            }
                            sentThisIteration = true;
                        }
                        else
                        {
                            // === Multi-connection path ===

                            var metadataRefreshTopics = retryMetadataRefreshTopics ??=
                                new HashSet<string>(StringComparer.Ordinal);
                            metadataRefreshTopics.Clear();

                            // Distribute coalesced batches into per-connection buckets.
                            // Partition affinity (partition % N) keeps each partition's batches
                            // on the same connection for CPU cache locality. This also preserves
                            // per-partition sequence ordering for idempotent producers.
                            // Note: when ConnectionsPerBroker exceeds the partition count, some
                            // connections will be idle. Adaptive scaling handles this naturally
                            // by scaling down unused connections.
                            for (var i = 0; i < coalescedCount; i++)
                            {
                                var connIdx = GetConnectionForPartition(coalescedBatches[i].TopicPartition);
                                ref var bucket = ref connectionBuckets[connIdx];
                                bucket.Batches[bucket.Count] = coalescedBatches[i];
                                bucket.Generations[bucket.Count] = coalescedGenerations[i];
                                bucket.Count++;
                            }
                            Array.Clear(coalescedBatches, 0, coalescedCount);
                            Array.Clear(coalescedGenerations, 0, coalescedCount);
                            coalescedCount = 0;

                            // Count buckets with batches to avoid O(N) scans in the wait loop.
                            var remainingBuckets = 0;
                            for (var c = 0; c < _connectionCount; c++)
                            {
                                if (connectionBuckets[c].HasBatches) remainingBuckets++;
                            }

                            // Send on connections with in-flight capacity first.
                            // Fire all eligible connection sends concurrently so TCP
                            // FlushAsync waits overlap instead of serializing.
                            var pendingSendCount = 0;
                            for (var c = 0; c < _connectionCount; c++)
                            {
                                if (!connectionBuckets[c].HasBatches) continue;
                                if (_pendingResponsesByConnection[c].Count >= _maxInFlight) continue;
                                if (Interlocked.Read(ref _pendingResponseBytesByConnection[c])
                                    >= _maxInFlightBytesPerConnection) continue;

                                ResetBucketTimeout(ref bucketTimeoutCts[c], cancellationToken);
                                parallelSends[pendingSendCount++] = SendConnectionBucketAsync(c, connectionBuckets,
                                    scratches[c], metadataRefreshTopics, bucketTimeoutCts[c].Token);
                            }

                            var completedSends = await AwaitParallelSendsAsync(parallelSends, pendingSendCount)
                                .ConfigureAwait(false);
                            if (completedSends > 0) sentThisIteration = true;
                            remainingBuckets -= completedSends;

                            // Do not wait here for a saturated connection. Requeue its batches
                            // so the outer loop can read fresh events and keep other connections
                            // busy. Step 7 waits for a response when no bucket made progress.
                            if (remainingBuckets > 0)
                            {
                                requeuedBlockedBuckets = true;
                                for (var c = 0; c < _connectionCount; c++)
                                {
                                    ref var bucket = ref connectionBuckets[c];
                                    if (!bucket.HasBatches) continue;

                                    MoveCoalescedToCarryOver(
                                        bucket.Batches, bucket.Generations, ref bucket.Count, carryOver);
                                }
                                _onBlockedBucketRequeued?.Invoke();
                            }
                        }
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
                if (transactionEnrollmentReady)
                    continue;

                var waitPendingCount = Volatile.Read(ref _totalPendingResponseCount);
                if (carryOver.Count == 0
                    && waitPendingCount == 0
                    && _sendFailedRetries.IsEmpty
                    && _loopExitRedeliveries.IsEmpty)
                {
                    if (hasPendingSingleConnectionFireAndForgetSend && !eventReader.TryPeek(out _))
                        await AwaitPendingSingleConnectionFireAndForgetSendAsync().ConfigureAwait(false);

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
                else if (waitPendingCount > 0)
                {
                    // When we just sent successfully with no carry-over (no muted partitions)
                    // and have in-flight capacity, use the event channel as the unified wait
                    // source. The channel receives both NewBatch events (from the sender loop)
                    // and ResponseReady events (from _responseCompletionCallback).
                    // This wakes the send loop on whichever arrives first, enabling pipelining:
                    // new batches can be sent immediately using remaining in-flight slots
                    // without waiting for the previous response to complete first.
                    //
                    // In multi-broker setups where each broker receives only 2 partitions per
                    // linger cycle, this eliminates dead time where the BrokerSender waits for
                    // a response while new batches sit unprocessed in the channel.
                    //
                    // Can pipeline: progress was made, no muted carry-over that could
                    // generate stale channel events, and in-flight capacity remains.
                    // A request limit of 1 always fills capacity and takes the response-wait path.
                    // Non-idempotent producers can still pipeline batches for other partitions;
                    // their same-partition batches remain in carry-over while muted.
                    var canPipeline = sentThisIteration
                        && carryOver.Count == 0
                        && waitPendingCount < _totalMaxInFlight
                        && (_connectionCount > 1
                            || Interlocked.Read(ref _totalPendingResponseBytes) < _totalMaxInFlightBytes);

                    if (canPipeline)
                    {
                        // Note: if this wakes on a ResponseReady event, the freed in-flight slot
                        // is only visible after ProcessCompletedResponses runs at the top of the
                        // next iteration — not in this one.
                        if (!await eventReader.WaitToReadAsync(cancellationToken)
                            .ConfigureAwait(false))
                            break;
                    }
                    else if (requeuedBlockedBuckets)
                    {
                        // A queued same-connection batch makes WaitToReadAsync complete
                        // synchronously and can starve response callbacks. Poll briefly so
                        // fresh work for another connection waits at most 1 ms while the
                        // guaranteed asynchronous yield lets acknowledgements make progress.
                        await WaitForAnyResponseAsync(cancellationToken, BlockedBucketPollIntervalMs)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        // Either carry-over exists (muted partitions may generate stale events),
                        // or at in-flight capacity limit, or no progress was made this iteration —
                        // wait for a response only.
                        await WaitForAnyResponseAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                else if (carryOver.Count > 0)
                {
                    if (_transactionEnrollmentPendingPartitions.Count > 0)
                    {
                        if (!await eventReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                            break;
                        continue;
                    }

                    if (hasPendingSingleConnectionFireAndForgetSend)
                    {
                        await AwaitPendingSingleConnectionFireAndForgetSendAsync().ConfigureAwait(false);
                        continue;
                    }

                    // Carry-over exists but no pending responses (e.g. retry backoff waiting).
                    // Timed wait — loop back after earliest retry backoff or delivery deadline.
                    var wakeupMs = ComputeNextWakeupMs(carryOver);
                    if (wakeupMs > 0)
                    {
                        await Task.Delay(Math.Min(wakeupMs, 100), cancellationToken).ConfigureAwait(false);
                    }
                }
                else if (_sendFailedRetries.IsEmpty
                    && _loopExitRedeliveries.IsEmpty
                    && !eventReader.TryPeek(out _))
                {
                    // No carry-over, no pending responses, no retries, no events — wait for new batch.
                    if (!await eventReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        catch (ChannelClosedException) { }
        catch (Exception ex)
        {
            LogSendLoopFailed(ex, _brokerId);
            crashed = true;
        }
        finally
        {
            var redeliver = crashed
                && Volatile.Read(ref _disposed) == 0
                && _rerouteBatch is not null;
            List<TopicPartition>? crashBarriers = null;

            // Mark this sender dead BEFORE disposing surviving batches: when the loop exited
            // unexpectedly, batches are handed back through the reroute callback, which goes
            // via GetOrCreateBrokerSender — IsAlive must already be false there so a fresh
            // replacement is built instead of re-enqueueing into this completed channel.
            lock (_loopExitRedeliveryLock)
            {
                if (redeliver)
                {
                    // Fence every partition accepted by this sender before publishing its
                    // death. Replacement senders consult the accumulator mute, so newer work
                    // cannot be sent during survivor discovery and handoff.
                    crashBarriers = new List<TopicPartition>(_enqueuedPartitions.Count);
                    foreach (var topicPartition in _enqueuedPartitions)
                    {
                        _accumulator.MutePartition(topicPartition);
                        crashBarriers.Add(topicPartition);
                    }
                }

                _redeliverAfterLoopExit = redeliver;
                _loopExited = true;
            }

            try
            {
                try { await AwaitPendingSingleConnectionFireAndForgetSendAsync().ConfigureAwait(false); }
                catch (Exception ex) { LogSendLoopFailed(ex, _brokerId); }

                sendTimeoutCts.Dispose();
                for (var c = 0; c < singleConnectionFireAndForgetTimeoutCts.Length; c++)
                    singleConnectionFireAndForgetTimeoutCts[c].Dispose();
                for (var c = 0; c < bucketTimeoutCts.Length; c++)
                    bucketTimeoutCts[c]?.Dispose();
                _eventChannel.Writer.TryComplete();

                // Shutdown (close/disposal) permanently fails surviving batches — the producer
                // is going away. An UNEXPECTED loop exit (escaped exception) must not drop them:
                // the producer is still healthy and replaces this sender on the next drain, so
                // every surviving batch is redelivered through the reroute callback instead.
                // Idempotent sequencing keeps redelivery of already-written requests safe — the
                // batch keeps its sequence and the broker dedupes.
                if (redeliver)
                    LogSendLoopExitRedelivery(_brokerId);

                var disposedException = new ObjectDisposedException(nameof(BrokerSender));
                var recoveredBatches = redeliver ? new List<BatchReference>() : null;
                var recoveredBatchSet = redeliver ? new HashSet<(ReadyBatch Batch, int Generation)>() : null;

                // Disposition order preserves per-partition FIFO for redelivery:
                // in-flight pending responses hold the oldest batches, then already-recovered work,
                // send-failed retries, this pass's coalesced head, carry-over, and channel backlog.
                for (var connIdx = 0; connIdx < _pendingResponsesByConnection.Length; connIdx++)
                {
                    var pendingList = _pendingResponsesByConnection[connIdx];
                    for (var i = 0; i < pendingList.Count; i++)
                    {
                        var pr = pendingList[i];
                        for (var j = 0; j < pr.Count; j++)
                        {
                            if (pr.IsSameIncarnation(j))
                                CollectOrFailOnLoopExit(
                                    new BatchReference(pr.Batches[j], pr.BatchGenerations[j]),
                                    redeliver, disposedException,
                                    recoveredBatches, recoveredBatchSet);
                        }
                        pr.ReturnBatchesArray();
                    }
                    Interlocked.Add(ref _totalPendingResponseCount, -pendingList.Count);
                    var removedBytes = SumEncodedBytes(pendingList);
                    Interlocked.Add(ref _totalPendingResponseBytes, -removedBytes);
                    Interlocked.Add(ref _pendingResponseBytesByConnection[connIdx], -removedBytes);
                    pendingList.Clear();
                    // No TrimExcess — lists are unreachable after disposal
                }

                while (_loopExitRedeliveries.TryDequeue(out var redelivery))
                {
                    if (redelivery.Batch.IsCurrentIncarnation(redelivery.Generation))
                        CollectOrFailOnLoopExit(
                            new BatchReference(redelivery.Batch, redelivery.Generation),
                            redeliver, disposedException,
                            recoveredBatches, recoveredBatchSet);
                }

                while (_sendFailedRetries.TryDequeue(out var retry))
                {
                    if (retry.Batch.IsCurrentIncarnation(retry.Generation))
                        CollectOrFailOnLoopExit(
                            new BatchReference(retry.Batch, retry.Generation),
                            redeliver, disposedException,
                            recoveredBatches, recoveredBatchSet);
                }

                for (var i = 0; i < coalescedCount; i++)
                {
                    if (coalescedBatches[i].IsCurrentIncarnation(coalescedGenerations[i]))
                        CollectOrFailOnLoopExit(
                            new BatchReference(coalescedBatches[i], coalescedGenerations[i]),
                            redeliver, disposedException,
                            recoveredBatches, recoveredBatchSet);
                }
                Array.Clear(coalescedBatches, 0, coalescedCount);
                Array.Clear(coalescedGenerations, 0, coalescedCount);

                DisposeCarryOverBatchesOnLoopExit(carryOver, redeliver, disposedException,
                    recoveredBatches, recoveredBatchSet);

                // Drain remaining events — only NewBatch events carry batches that need cleanup.
                // ResponseReady and Unmute events are lightweight signals with no data.
                while (eventReader.TryRead(out var evt))
                {
                    if (evt.Type == SendLoopEventType.NewBatch)
                    {
                        var batchRef = evt.GetBatchReference();
                        if (batchRef.IsCurrentIncarnation())
                            CollectOrFailOnLoopExit(batchRef, redeliver, disposedException,
                                recoveredBatches, recoveredBatchSet);
                    }
                }

                if (recoveredBatches is not null)
                {
                    // All recovery mute references were registered before the first callback,
                    // so even synchronous completion cannot expose a later sibling or newer work.
                    for (var i = 0; i < recoveredBatches.Count; i++)
                    {
                        var batchRef = recoveredBatches[i];
                        if (batchRef.IsCurrentIncarnation())
                            RedeliverOrFailOnLoopExit(batchRef.Batch, batchRef.Generation,
                                redeliver: true, disposedException);
                    }
                }
            }
            finally
            {
                if (crashBarriers is not null)
                {
                    for (var i = 0; i < crashBarriers.Count; i++)
                        _accumulator.UnmutePartition(crashBarriers[i]);
                }

                // Unmute all partitions this BrokerSender had muted in the accumulator.
                // Without this, partitions stay permanently muted after BrokerSender disposal,
                // preventing Ready()/Drain() from ever picking up queued batches for those
                // partitions. This causes orphaned batch timeouts on slow CI runners where
                // transient connection errors kill the send loop while retries are pending.
                foreach (var (tp, _) in _mutedPartitions)
                    _accumulator.UnmutePartition(tp);
                _mutedPartitions.Clear();
                Interlocked.Exchange(ref _mutedPartitionCount, 0);
                Interlocked.Exchange(ref _mutedPartitionHighWatermark, 0);
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
        BatchReference batchRef,
        ReadyBatch[] coalescedBatches,
        int[] coalescedGenerations,
        ref int coalescedCount,
        ref long coalescedRequestBudgetUsed,
        HashSet<TopicPartition> coalescedPartitions,
        PartitionCarryOver carryOver)
    {
        // Batch may have been returned to pool or re-rented by a racing owner before
        // the send loop sees this reference. Skip silently; the original owner already
        // failed/completed it, and the current owner must not be touched.
        if (!batchRef.IsCurrentIncarnation())
        {
            LogStaleBatchInSendSkipped(_instanceId, _brokerId);
            return;
        }

        var batch = batchRef.Batch;

        // Track distinct partitions this broker serves for MicroLinger skip optimization.
        _knownPartitions.Add(batch.TopicPartition);

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
                carryOver.Add(batchRef);
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
                    batch.RetryNotBefore = 0;
                    if (!batch.IsLoopExitRedelivery)
                        batch.IsRetry = false;
                    UnmutePartition(batch.TopicPartition);
                    _rerouteBatch(batch, batchRef.Generation);
                    return;
                }
            }

            // Retry batch: coalesce it ahead of newer batches.
            // NOTE: Do NOT clear IsRetry or unmute here. These are deferred to
            // FinalizeCoalescedRetries() after the epoch bump check passes.
            // If an epoch bump is pending, coalesced batches are moved back to
            // carry-over — they must retain their retry state and mute status.
            batch.RetryNotBefore = 0;

            if (CarryOverIfCoalescedLimitReached(
                    batchRef,
                    coalescedBatches,
                    coalescedCount,
                    coalescedRequestBudgetUsed,
                    carryOver,
                    out var batchRequestBodySize))
                return;

            if (!coalescedPartitions.Add(batch.TopicPartition))
            {
                // Same partition already in this request (shouldn't happen — only one
                // retry per partition at a time). Carry over as safety net.
                carryOver.Add(batchRef);
                return;
            }

            AddCoalescedBatch(
                batchRef,
                coalescedBatches,
                coalescedGenerations,
                ref coalescedCount,
                ref coalescedRequestBudgetUsed,
                batchRequestBodySize);
            return;
        }

        // Normal batch: skip any local retry/recovery mute and crash barriers owned by
        // another sender. The accumulator check closes the handoff gap before the first
        // recovered batch reaches this replacement sender.
        if (_transactionEnrollmentPendingPartitions.Contains(batch.TopicPartition)
            || _mutedPartitions.ContainsKey(batch.TopicPartition)
            || _accumulator.IsMuted(batch.TopicPartition))
        {
            LogPartitionMuted(_brokerId, batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            batch.AppendDiag('O');
            carryOver.Add(batchRef);
            return;
        }

        if (CarryOverIfCoalescedLimitReached(
                batchRef,
                coalescedBatches,
                coalescedCount,
                coalescedRequestBudgetUsed,
                carryOver,
                out var normalBatchRequestBodySize))
            return;

        // Ensure at most one batch per partition per coalesced request
        if (!coalescedPartitions.Add(batch.TopicPartition))
        {
            batch.AppendDiag('O');
            carryOver.Add(batchRef);
            return;
        }

        AddCoalescedBatch(
            batchRef,
            coalescedBatches,
            coalescedGenerations,
            ref coalescedCount,
            ref coalescedRequestBudgetUsed,
            normalBatchRequestBodySize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CarryOverIfCoalescedLimitReached(
        BatchReference batchRef,
        ReadyBatch[] coalescedBatches,
        int coalescedCount,
        long coalescedRequestBudgetUsed,
        PartitionCarryOver carryOver,
        out int batchRequestBodySize)
    {
        batchRequestBodySize = 0;
        if (coalescedCount < coalescedBatches.Length)
        {
            var candidateBatch = batchRef.Batch;
            batchRequestBodySize = ProduceRequestSizeCalculator.GetSingleBatchRequestBodySize(
                _options.TransactionalId,
                candidateBatch.TopicPartition.Topic,
                candidateBatch.EncodedSize);
            var maxRequestSize = _options.MaxRequestSize > 0
                ? _options.MaxRequestSize
                : ProduceRequestSizeCalculator.DefaultMaxRequestSize;
            if (coalescedCount == 0
                || coalescedRequestBudgetUsed + batchRequestBodySize <= maxRequestSize)
                return false;
        }

        var batch = batchRef.Batch;
        batch.AppendDiag('O');
        carryOver.Add(batchRef);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddCoalescedBatch(
        BatchReference batchRef,
        ReadyBatch[] coalescedBatches,
        int[] coalescedGenerations,
        ref int coalescedCount,
        ref long coalescedRequestBudgetUsed,
        int batchRequestBodySize)
    {
        var batch = batchRef.Batch;
        batch.AppendDiag('C');
        coalescedBatches[coalescedCount] = batch;
        coalescedGenerations[coalescedCount] = batchRef.Generation;
        coalescedCount++;
        coalescedRequestBudgetUsed += batchRequestBodySize;
    }

    private int CompactCurrentBatches(ReadyBatch[] batches, int[] generations, int count)
    {
        var writeIdx = 0;
        for (var readIdx = 0; readIdx < count; readIdx++)
        {
            var batch = batches[readIdx];
            var generation = generations[readIdx];
            if (!batch.IsCurrentIncarnation(generation))
            {
                LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                continue;
            }

            batches[writeIdx] = batch;
            generations[writeIdx] = generation;
            writeIdx++;
        }

        for (var i = writeIdx; i < count; i++)
        {
            batches[i] = null!;
            generations[i] = 0;
        }

        return writeIdx;
    }

    private int AcquireResourcePins(ReadyBatch[] batches, int[] generations, int count)
    {
        var writeIdx = 0;
        for (var readIdx = 0; readIdx < count; readIdx++)
        {
            var batch = batches[readIdx];
            var generation = generations[readIdx];
            if (!batch.TryAcquireResourcePin(generation))
            {
                LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                continue;
            }

            batches[writeIdx] = batch;
            generations[writeIdx] = generation;
            writeIdx++;
        }

        for (var i = writeIdx; i < count; i++)
        {
            batches[i] = null!;
            generations[i] = 0;
        }

        return writeIdx;
    }

    private static void ReleaseResourcePins(ReadyBatch[] batches, int count)
    {
        for (var i = 0; i < count; i++)
            batches[i]?.ReleaseResourcePin();
    }

    private ValueTask<Task<ProduceResponse>> SendPipelinedAfterWriteAsync(
        IKafkaConnection connection,
        ProduceRequest request,
        short apiVersion)
    {
        if (connection is IKafkaPipelinedWriteCompletionConnection writeCompletionConnection)
        {
            return writeCompletionConnection.SendPipelinedAfterWriteAsync<ProduceRequest, ProduceResponse>(
                request,
                apiVersion,
                _cts.Token);
        }

        throw new InvalidOperationException(
            $"{nameof(BrokerSender)} requires {nameof(IKafkaPipelinedWriteCompletionConnection)} for pipelined produce sends; " +
            $"connection type '{connection.GetType().FullName}' cannot safely hold batch resources until the socket write completes.");
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
                UnmutePartition(batch.TopicPartition);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordSendLoopPressureIfScaleUseful()
    {
        if (IsPartitionPressureScaleUseful(
            _adaptiveScalingEnabled,
            _connectionCount,
            _maxConnectionsPerBroker,
            _knownPartitions.Count))
        {
            _sendLoopPressureEvents++;
        }
    }

    /// <summary>
    /// Requeues an unsent request without changing retry state. Retries stay ahead of
    /// normal batches so quota waits and epoch bumps cannot reorder a partition.
    /// </summary>
    private void MoveCoalescedToCarryOver(
        ReadyBatch[] batches,
        int[] generations,
        ref int count,
        PartitionCarryOver carryOver)
    {
        for (var i = 0; i < count; i++)
        {
            var batch = batches[i];
            var generation = generations[i];
            if (!batch.IsCurrentIncarnation(generation))
            {
                LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                continue;
            }

            var batchReference = new BatchReference(batch, generation);
            if (batch.IsRetry)
                carryOver.AddFirst(batchReference);
            else
                carryOver.AddAfterRetries(batchReference);
        }

        Array.Clear(batches, 0, count);
        Array.Clear(generations, 0, count);
        count = 0;
    }

    private void MoveEnrollmentPendingToCarryOver(
        ReadyBatch[] batches,
        int[] generations,
        ref int count,
        PartitionCarryOver carryOver,
        HashSet<TopicPartition> pendingPartitions)
    {
        var writeIdx = 0;
        for (var readIdx = 0; readIdx < count; readIdx++)
        {
            var batch = batches[readIdx];
            var generation = generations[readIdx];
            if (!batch.IsCurrentIncarnation(generation))
            {
                LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                continue;
            }

            if (pendingPartitions.Contains(batch.TopicPartition))
            {
                var batchReference = new BatchReference(batch, generation);
                if (batch.IsRetry)
                    carryOver.AddFirst(batchReference);
                else
                    carryOver.AddAfterRetries(batchReference);
                continue;
            }

            batches[writeIdx] = batch;
            generations[writeIdx] = generation;
            writeIdx++;
        }

        Array.Clear(batches, writeIdx, count - writeIdx);
        Array.Clear(generations, writeIdx, count - writeIdx);
        count = writeIdx;
    }

    private void FailPendingTransactionEnrollmentBatches(
        PartitionCarryOver carryOver,
        HashSet<TopicPartition> pendingPartitions,
        Exception error)
    {
        foreach (var topicPartition in pendingPartitions)
        {
            if (!carryOver.Partitions.TryGetValue(topicPartition, out var queue))
                continue;

            while (queue.Count > 0)
            {
                var batchReference = queue[0];
                carryOver.RemoveAt(queue, 0);
                if (!batchReference.IsCurrentIncarnation())
                {
                    LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                    continue;
                }

                try
                {
                    FailAndCleanupBatch(batchReference.Batch, batchReference.Generation, error);
                }
                catch (Exception cleanupError)
                {
                    LogBatchCleanupStepFailed(cleanupError, _brokerId);
                }
            }
        }
    }

    private void ObserveBrokerThrottle(int throttleTimeMs)
    {
        _onBrokerThrottle?.Invoke(throttleTimeMs);

        if (throttleTimeMs <= 0)
            return;

        var now = _getTimestamp();
        var delayTicks = (long)Math.Ceiling(
            throttleTimeMs * (double)Stopwatch.Frequency / 1000);
        var throttleUntil = delayTicks >= long.MaxValue - now
            ? long.MaxValue
            : now + delayTicks;

        if (throttleUntil > _brokerThrottleUntilTimestamp)
            _brokerThrottleUntilTimestamp = throttleUntil;
    }

    private int GetRemainingBrokerThrottleMs()
    {
        if (_brokerThrottleUntilTimestamp == 0)
            return 0;

        var remainingTicks = _brokerThrottleUntilTimestamp - _getTimestamp();
        if (remainingTicks <= 0)
        {
            _brokerThrottleUntilTimestamp = 0;
            return 0;
        }

        var remainingMs = (long)Math.Ceiling(
            remainingTicks * 1000d / Stopwatch.Frequency);
        return (int)Math.Min(remainingMs, int.MaxValue);
    }

    /// <summary>
    /// Polls _pendingResponsesByConnection for completed response tasks and processes them inline.
    /// Equivalent to Java's Sender processing completed sends inside NetworkClient.poll().
    /// Uses compact-in-place pattern to avoid list allocation during removal.
    /// </summary>
    private void ProcessCompletedResponses(
        PartitionCarryOver carryOver,
        CancellationToken cancellationToken,
        Dictionary<(string Topic, int Partition), ProduceResponsePartitionData>? reusableResponseLookup = null)
    {
        // Aggregate every successful request completed in this pass. Multiple connection
        // lanes drain concurrently, so per-request samples understate broker-wide capacity.
        var ackedPass = new AckedResponsePass();

        // CRITICAL: Process responses in FORWARD order (oldest request first).
        // With multi-inflight, R1 and R2 may both complete with errors for the same partition.
        // Forward iteration ensures R1's retry batches are added to carry-over before R2's,
        // preserving per-partition FIFO ordering during the next coalescing pass.
        // Reverse iteration (swap-with-last O(1) removal) would process R2 before R1,
        // causing the newer batch to be coalesced first and violating ordering.
        for (var connIdx = 0; connIdx < _pendingResponsesByConnection.Length; connIdx++)
        {
            var pendingList = _pendingResponsesByConnection[connIdx];
            if (pendingList.Count == 0) continue;

            var writeIdx = 0;
            long completedResponseBytes = 0;
            for (var i = 0; i < pendingList.Count; i++)
            {
                var pending = pendingList[i];
                if (!pending.ResponseTask.IsCompleted)
                {
                    // Keep this entry — compact in-place
                    pendingList[writeIdx++] = pending;
                    continue;
                }

                var task = pending.ResponseTask;
                completedResponseBytes += pending.EncodedBytes;
                var batches = pending.Batches;
                var count = pending.Count;

                if (task.IsFaulted || task.IsCanceled)
                {
                    var ex = task.Exception?.InnerException ?? new OperationCanceledException();
                    LogResponseFailed(ex, _brokerId);
                    _pinnedConnections[connIdx] = null; // Invalidate only the affected connection

                    for (var j = 0; j < count; j++)
                    {
                        if (pending.IsSameIncarnation(j))
                        {
                            batches[j].AppendDiag('P'); // Faulted/cancelled response processed
                            try
                            {
                                HandleRetriableBatch(batches[j], pending.BatchGenerations[j], ErrorCode.NetworkException,
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

                    pending.ReturnBatchesArray();
                    continue;
                }

                var response = task.Result;
                ObserveBrokerThrottle(response.ThrottleTimeMs);
                var allPartitionsSucceeded = true;
                // Reuse caller-provided dictionary to avoid per-response allocation.
                // The dictionary is cleared after use in ProcessResponseBatches.
                var responseLookup = reusableResponseLookup;
                for (var t = 0; t < response.TopicCount; t++)
                {
                    ref var topicResp = ref response.Responses[t];
                    for (var p = 0; p < topicResp.PartitionCount; p++)
                    {
                        if (topicResp.PartitionResponses[p].ErrorCode != ErrorCode.None)
                            allPartitionsSucceeded = false;
                        responseLookup ??= new Dictionary<(string, int), ProduceResponsePartitionData>();
                        responseLookup[(topicResp.Name, topicResp.PartitionResponses[p].Index)] = topicResp.PartitionResponses[p];
                    }
                }

                if (allPartitionsSucceeded)
                    ackedPass.Add(pending.EncodedBytes, pending.RequestStartTime);

                // Diagnostic: log response content and expected batches for mismatch diagnosis
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var batchKeys = string.Join(", ",
                        Enumerable.Range(0, count)
                            .Where(idx => batches[idx] is not null)
                            .Select(idx => $"{batches[idx].TopicPartition.Topic}-{batches[idx].TopicPartition.Partition}"));
                    var respKeys = responseLookup is not null
                        ? string.Join(", ", responseLookup.Keys.Select(k => $"{k.Topic}-{k.Partition}"))
                        : "(empty)";
                    LogProduceResponseProcessed(_instanceId, _brokerId, task.Id, count, batchKeys, responseLookup?.Count ?? 0, respKeys);
                }

                ProcessResponseBatches(pending, responseLookup, response.NodeEndpoints,
                    carryOver, cancellationToken, task.Id);

                // Clear for reuse on next response (avoids per-response dictionary allocation)
                responseLookup?.Clear();

                // Return pooled ProduceResponse for reuse
                if (response is ProduceResponse poolableResponse)
                    poolableResponse.Return();

                pending.ReturnBatchesArray();
            }

            // Compact: remove processed entries from the end
            if (writeIdx < pendingList.Count)
            {
                Interlocked.Add(ref _totalPendingResponseCount, -(pendingList.Count - writeIdx));
                Interlocked.Add(ref _totalPendingResponseBytes, -completedResponseBytes);
                Interlocked.Add(ref _pendingResponseBytesByConnection[connIdx], -completedResponseBytes);
                pendingList.RemoveRange(writeIdx, pendingList.Count - writeIdx);

                // Prevent unbounded capacity ratcheting: when the list shrinks well below
                // its internal array capacity, trim the excess. This is amortized — it only
                // fires when count drops below 1/4 of capacity, which happens infrequently
                // (e.g., after a burst of completions). TrimExcess() reallocates the internal
                // array to match Count, reclaiming the wasted capacity.
                // The > 16 guard avoids trimming tiny lists: with MaxInFlightRequestsPerConnection
                // defaulting to 5, normal-operation lists stay within 8-16 capacity and the
                // reallocation overhead would exceed the memory savings.
                if (pendingList.Capacity > 16 && pendingList.Count < pendingList.Capacity / 4)
                {
                    pendingList.TrimExcess();
                }

                // Check if any migrating partitions can be unfenced after responses completed
                if (_migratingPartitions.Count > 0)
                {
                    var beforeCount = _migratingPartitions.Count;
                    CompleteMigrations(connIdx);

                    if (_migratingPartitions.Count == 0)
                        LogPartitionMigrationComplete(_brokerId, beforeCount);
                }
            }
        }

        if (ackedPass.Bytes > 0 && _unackedBudget is { } unackedBudget)
        {
            // Oldest start spans the whole concurrent completion window. Dividing total
            // bytes by that busy time captures aggregate broker drain without counting
            // admission-blocked gaps between response passes.
            var ackTimestamp = Stopwatch.GetTimestamp();
            unackedBudget.OnAcked(
                ackedPass.Bytes,
                ackTimestamp - ackedPass.EarliestRequestStart,
                ackTimestamp);
        }
    }

    /// <summary>
    /// Processes per-partition results for a completed response. Extracted from the success path
    /// of the former ProcessCompletedResponses — handles response lookup, error codes, retries,
    /// epoch bump flagging, acknowledgement callbacks, and batch cleanup.
    /// </summary>
    private void ProcessResponseBatches(
        PendingResponse pending,
        Dictionary<(string Topic, int Partition), ProduceResponsePartitionData>? responseLookup,
        IReadOnlyList<NodeEndpoint> nodeEndpoints,
        PartitionCarryOver carryOver,
        CancellationToken cancellationToken,
        int responseTaskId = 0)
    {
        var batches = pending.Batches;
        var count = pending.Count;

        for (var j = 0; j < count; j++)
        {
            var batch = batches[j];
            if (batch is null)
                continue;

            // Detect batch object recycling: if the batch was returned to pool AND
            // re-Initialize'd (new topic, _returnedToPool reset to 0), IsReturnedToPool
            // returns false but the generation won't match. Also catches the simpler case
            // where the batch is still in the pool (IsReturnedToPool = true).
            if (!pending.IsSameIncarnation(j))
            {
                LogBatchAlreadyReturnedToPool(_instanceId, responseTaskId, count, batch.DiagTrace);
                continue;
            }

            try
            {
                batch.AppendDiag('R');
                var expectedTopic = batch.TopicPartition.Topic;
                var expectedPartition = batch.TopicPartition.Partition;

                // default() zero-initializes (LogAppendTimeMs/LogStartOffset get 0, not -1 from property initializers).
                // This is safe because partitionResponse is only consumed when foundResponse is true,
                // in which case TryGetValue has populated it with the real response data.
                var partitionResponse = default(ProduceResponsePartitionData);
                var foundResponse = responseLookup is not null
                    && responseLookup.TryGetValue((expectedTopic, expectedPartition), out partitionResponse);

                if (!foundResponse)
                {
                    // Log what the response actually contains for diagnosis.
                    // The response may be empty (broker didn't respond for this partition),
                    // or contain different topic-partitions than expected.
                    var responseTopicCount = responseLookup?.Count ?? 0;
                    var responseKeys = responseLookup is not null
                        ? string.Join(", ", responseLookup.Keys.Select(k => $"{k.Topic}-{k.Partition}"))
                        : "(null)";
                    LogNoResponseForPartition(_instanceId, expectedTopic, expectedPartition,
                        responseTopicCount, responseKeys, count, responseTaskId, batch.DiagTrace);
                    HandleRetriableBatch(batch, pending.BatchGenerations[j], ErrorCode.NetworkException,
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

                        HandleRetriableBatch(batch, pending.BatchGenerations[j], partitionResponse.ErrorCode,
                            partitionResponse.CurrentLeader, nodeEndpoints,
                            carryOver, cancellationToken);
                        batches[j] = null!;
                        continue;
                    }

                    KafkaException failureException = partitionResponse.ErrorCode == ErrorCode.MessageTooLarge
                        ? new ProduceException(partitionResponse.ErrorCode,
                            $"Produce failed: {partitionResponse.ErrorCode}")
                        {
                            Topic = expectedTopic,
                            Partition = expectedPartition
                        }
                        : KafkaException.FromErrorCode(partitionResponse.ErrorCode,
                            $"Produce failed: {partitionResponse.ErrorCode}");
                    if (_muteOnSend)
                        UnmutePartition(batch.TopicPartition);
                    try { CompleteInflightEntry(batch); }
                    catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
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

                // Only unmute on success when mute-on-send is active (non-idempotent or maxInFlight <= 1).
                // Idempotent producers with maxInFlight > 1 mute partitions only on error.
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
                try
                {
                    FailAndCleanupBatch(batch, new InvalidOperationException(
                    "Unexpected exception during response processing", batchEx));
                }
                catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                batches[j] = null!;
            }
        }
    }

    /// <summary>
    /// Java-style request timeout handling (handleTimedOutRequests pattern).
    /// On every send loop iteration, checks if ANY pending response has exceeded the
    /// request timeout. If so, removes ALL entries from the connection's pending list and processes
    /// each batch — exactly like Java's NetworkClient.handleTimedOutRequests() which closes
    /// the connection and calls cancelInFlightRequests() for the node.
    /// <para/>
    /// Processing happens synchronously in the single-threaded send loop. Entries are
    /// removed from the connection's pending list BEFORE processing, so ProcessCompletedResponses
    /// cannot also process the same batches (eliminating the race condition).
    /// <para/>
    /// The affected connection is also invalidated so the next send
    /// establishes a fresh connection. Response tasks from the old connection are orphaned —
    /// they may eventually complete, but nobody polls them since the entries were removed.
    /// </summary>
    private void HandleTimedOutRequests(
        PartitionCarryOver carryOver,
        CancellationToken cancellationToken)
    {
        var now = Stopwatch.GetTimestamp();
        var requestTimeoutTicks = _options.RequestTimeoutTicks;

        // Check each connection's pending responses for timeout.
        for (var connIdx = 0; connIdx < _pendingResponsesByConnection.Length; connIdx++)
        {
            var pendingList = _pendingResponsesByConnection[connIdx];
            if (pendingList.Count == 0) continue;

            // Check if ANY pending response in this connection's list has exceeded the request timeout.
            var hasTimedOut = false;
            for (var i = 0; i < pendingList.Count; i++)
            {
                if (now >= pendingList[i].RequestStartTime + requestTimeoutTicks)
                {
                    hasTimedOut = true;
                    break;
                }
            }

            if (!hasTimedOut)
                continue;

            // Request timeout detected — invalidate connection and process ALL pending batches
            // for this connection. Must process ALL entries (not just the timed-out one) because
            // they share the same connection, which is now unreliable. This matches Java's behavior:
            // when any request times out, ALL in-flight requests for that node are failed.
            _pinnedConnections[connIdx] = null; // Invalidate only the affected connection
            LogRequestTimeoutDisconnection(_brokerId, pendingList.Count);

            var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks;

            for (var i = 0; i < pendingList.Count; i++)
            {
                var pending = pendingList[i];
                for (var j = 0; j < pending.Count; j++)
                {
                    if (!pending.IsSameIncarnation(j)) continue;
                    var batch = pending.Batches[j];

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
                            HandleRetriableBatch(batch, pending.BatchGenerations[j], ErrorCode.NetworkException,
                                carryOver, cancellationToken);
                        }
                        catch (Exception batchEx)
                        {
                            try { FailAndCleanupBatch(batch, batchEx); }
                            catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                        }
                    }
                }

                pending.ReturnBatchesArray();
            }

            // Remove all entries. Response tasks are orphaned — they'll eventually complete
            // (via CTS timeout or connection disposal) but nobody polls them.
            Interlocked.Add(ref _totalPendingResponseCount, -pendingList.Count);
            var removedBytes = SumEncodedBytes(pendingList);
            Interlocked.Add(ref _totalPendingResponseBytes, -removedBytes);
            Interlocked.Add(ref _pendingResponseBytesByConnection[connIdx], -removedBytes);
            pendingList.Clear();

            // After clearing all entries due to timeout, trim the internal array to
            // prevent capacity from ratcheting up across repeated timeout/recovery cycles.
            // The > 16 guard avoids trimming tiny lists: with MaxInFlightRequestsPerConnection
            // defaulting to 5, normal-operation lists stay within 8-16 capacity.
            if (pendingList.Capacity > 16)
            {
                pendingList.TrimExcess();
            }
        }
    }

    /// <summary>
    /// Computes the next poll timeout in milliseconds. Returns the minimum of:
    /// - Earliest request timeout deadline from _pendingResponsesByConnection
    /// - Earliest retry backoff from carry-over batches
    /// - Earliest delivery deadline from carry-over batches
    /// Returns 0 if a deadline has already passed (immediate re-loop).
    /// Returns int.MaxValue if nothing is pending.
    /// </summary>
    private int ComputeNextWakeupMs(PartitionCarryOver carryOver)
    {
        var now = _getTimestamp();
        var earliestTicks = long.MaxValue;

        // Use request timeout for pending responses (Java handleTimedOutRequests pattern).
        for (var connIdx = 0; connIdx < _pendingResponsesByConnection.Length; connIdx++)
        {
            var pendingList = _pendingResponsesByConnection[connIdx];
            for (var i = 0; i < pendingList.Count; i++)
            {
                var deadline = pendingList[i].RequestStartTime + _options.RequestTimeoutTicks;
                if (deadline < earliestTicks)
                    earliestTicks = deadline;
            }
        }

        var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks;
        foreach (var kvp in carryOver.Partitions)
        {
            var queue = kvp.Value;
            for (var i = 0; i < queue.Count; i++)
            {
                var batchRef = queue[i];
                if (!batchRef.IsCurrentIncarnation())
                    continue;

                var batch = batchRef.Batch;
                if (batch.RetryNotBefore > 0 && batch.RetryNotBefore < earliestTicks)
                    earliestTicks = batch.RetryNotBefore;

                var deadlineTicks = batch.StopwatchCreatedTicks + deliveryTimeoutTicks;
                if (deadlineTicks < earliestTicks)
                    earliestTicks = deadlineTicks;
            }
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
    private void HandleRetriableBatch(ReadyBatch batch, int generation, ErrorCode errorCode,
        PartitionCarryOver carryOver, CancellationToken cancellationToken,
        bool networkRetryPrepared = false)
        => HandleRetriableBatch(batch, generation, errorCode, null, [], carryOver, cancellationToken,
            networkRetryPrepared);

    private void HandleRetriableBatch(
        ReadyBatch batch,
        int generation,
        ErrorCode errorCode,
        LeaderIdAndEpoch? currentLeader,
        IReadOnlyList<NodeEndpoint> nodeEndpoints,
        PartitionCarryOver carryOver,
        CancellationToken cancellationToken,
        bool networkRetryPrepared = false)
    {
        if (!batch.IsCurrentIncarnation(generation))
        {
            LogStaleBatchInSendSkipped(_instanceId, _brokerId);
            return;
        }

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
        if (!networkRetryPrepared && !batch.IsLoopExitRedelivery)
            MutePartition(batch.TopicPartition);

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
        else if (!networkRetryPrepared)
        {
            if (TryApplyInlineLeader(batch.TopicPartition, errorCode, currentLeader, nodeEndpoints))
            {
                LogRetriableErrorWithoutBackoff(errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition);
                batch.RetryNotBefore = 0;
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
        }

        Diagnostics.DekafMetrics.Retries.Add(1,
            new System.Diagnostics.TagList { { Diagnostics.DekafDiagnostics.MessagingDestinationName, batch.TopicPartition.Topic } });

        // Insert after existing retries but before non-retry carry-over batches.
        // When ProcessCompletedResponses processes multiple responses (R1, R2, ...)
        // in forward order, each response's retry must go AFTER earlier responses'
        // retries for the same partition to preserve FIFO ordering.
        batch.IsRetry = true;
        batch.AppendDiag('H'); // HandleRetriableBatch → carry-over
        carryOver.AddAfterRetries(new BatchReference(batch, generation));
    }

    private bool TryApplyInlineLeader(
        TopicPartition topicPartition,
        ErrorCode errorCode,
        LeaderIdAndEpoch? currentLeader,
        IReadOnlyList<NodeEndpoint> nodeEndpoints)
    {
        if (errorCode is not (ErrorCode.NotLeaderOrFollower or ErrorCode.FencedLeaderEpoch)
            || currentLeader is null)
        {
            return false;
        }

        var endpoint = LeaderDiscoveryFields.FindNodeEndpoint(nodeEndpoints, currentLeader.LeaderId);
        return _metadataManager.TryUpdatePartitionLeader(
            topicPartition.Topic,
            topicPartition.Partition,
            currentLeader.LeaderId,
            currentLeader.LeaderEpoch,
            endpoint);
    }

    /// <summary>
    /// Waits for any pending response to complete using an <see cref="AsyncAutoResetSignal"/>,
    /// with a bounded periodic wake-up to re-sweep delivery timeouts for zombie entries.
    /// Signal may be missed if multiple responses complete between iterations;
    /// the 100ms default fallback ensures we don't wait indefinitely.
    ///
    /// Zero-allocation in steady state: the signal uses a reusable internal timer for
    /// the timeout and a one-time shutdown token registration for cancellation.
    /// </summary>
    private async ValueTask WaitForAnyResponseAsync(
        CancellationToken cancellationToken,
        int timeoutMs = ResponsePollIntervalMs)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // WaitAsync returns true if signaled, false on timeout.
        // Throws OperationCanceledException only on shutdown (via RegisterShutdownToken).
        await _anyResponseCompleted.WaitAsync(timeoutMs).ConfigureAwait(false);
    }

    internal static int ComputeThrottledResponseWaitMs(int throttleDelayMs, int batchDeadlineMs)
        => Math.Min(ResponsePollIntervalMs, Math.Min(throttleDelayMs, batchDeadlineMs));

    private long GetInFlightByteBudget(int connectionCount)
        => BrokerUnackedByteBudget.ComputeCap(_options.BatchSize, connectionCount);

    /// <summary>
    /// Recomputes the written-unacked pipeline ceiling for the given routing width and
    /// republishes the admission budget's cap in lockstep. Every path that changes
    /// <c>_connectionCount</c> must go through this so the two ceilings never desync.
    /// </summary>
    private void UpdateInFlightByteBudget(int connectionCount)
    {
        _totalMaxInFlightBytes = GetInFlightByteBudget(connectionCount);
        _unackedBudget?.SetCap(
            _options.UnackedByteBudgetCapOverride ?? _totalMaxInFlightBytes,
            _getTimestamp());
    }

    private static long SumEncodedBytes(List<PendingResponse> pendingResponses)
    {
        long total = 0;
        for (var i = 0; i < pendingResponses.Count; i++)
            total += pendingResponses[i].EncodedBytes;

        return total;
    }

    /// <summary>
    /// Sends coalesced batches (one per partition) as a single ProduceRequest.
    /// The in-flight count was already incremented by the send loop before calling this method.
    /// </summary>
    private async ValueTask SendCoalescedAsync(
        ReadyBatch[] batches,
        int count,
        ProduceRequestScratch scratch,
        int connectionIndex,
        CancellationToken cancellationToken,
        HashSet<string>? metadataRefreshTopics = null)
    {
        // Snapshot batch generations before the first await, while this send still has
        // exclusive ownership handed over by the send loop's coalesce step. If the request
        // builder sorts batches in place, it sorts this generation array in lockstep so
        // every later liveness check still compares the matching batch/generation pair.
        var generations = ArrayPool<int>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
            generations[i] = batches[i].Generation;

        // Tracks whether PendingResponse was added to _pendingResponsesByConnection.
        // Once added, ProcessCompletedResponses owns the batches array AND the generations
        // array — catch blocks must NOT return them to ArrayPool, or they will be recycled
        // while PendingResponse still references them (causing cross-request batch contamination).
        var pendingResponseAdded = false;
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
            //
            // Non-idempotent producers skip sequence assignment and inflight tracking
            // entirely — they have no producer ID, epochs, or sequence numbers.
            // This eliminates per-batch ConcurrentDictionary lookups and pool rent/return
            // that previously ran unnecessarily for non-idempotent producers.
            // Note: transactional producers ARE idempotent and need sequence assignment,
            // but don't use epoch recovery (_getCurrentEpoch is null for them).
            if (_isIdempotent) // EnableIdempotence — covers both idempotent and transactional
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
            }

            using var connectionLease = await GetConnectionLeaseAtIndexAsync(connectionIndex, cancellationToken)
                .ConfigureAwait(false);
            var connection = connectionLease.Connection;

            count = CompactCurrentBatches(batches, generations, count);
            if (count == 0)
            {
                ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
                return;
            }

            // Diagnostic: mark batches as having acquired a connection.
            // If orphan trace shows 'S' but no 'G' (Got connection), the hang is at
            // GetConnectionLeaseAtIndexAsync (connection creation or write lock contention).
            for (var i = 0; i < count; i++)
                batches[i].AppendDiag('G');

            var apiVersion = EnsureApiVersion();

            count = AcquireResourcePins(batches, generations, count);
            if (count == 0)
            {
                ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
                return;
            }

            var resourcePinCount = count;

            var requestStartTime = Stopwatch.GetTimestamp();

            try
            {
                // Build coalesced ProduceRequest (reuses pre-allocated scratch structures)
                var request = scratch.Build(batches, generations, count);

                // Handle Acks.None (fire-and-forget)
                if (_options.Acks == Acks.None)
                {
                    // Use the caller-timeout overload to avoid per-write
                    // CancellationTokenSource + CancellationTokenRegistration allocations.
                    // The cancellationToken already carries BrokerSender's sendTimeoutCts timeout.
                    await connection.SendFireAndForgetWithCallerTimeoutAsync<ProduceRequest, ProduceResponse>(
                        request, (short)apiVersion, cancellationToken).ConfigureAwait(false);

                    // The write is now ordered on the connection. Release the send-time fence
                    // before resource pins so each batch still owns its original partition.
                    if (_muteOnSend)
                    {
                        for (var i = 0; i < count; i++)
                            UnmutePartition(batches[i].TopicPartition);
                    }

                    // Clear batch references from scratch arrays (see ClearReferences() doc for exception-path semantics)
                    scratch.ClearReferences();
                    ReleaseResourcePins(batches, resourcePinCount);
                    resourcePinCount = 0;

                    var fireAndForgetTimestamp = DateTimeOffset.UtcNow;
                    for (var i = 0; i < count; i++)
                    {
                        var batch = batches[i];
                        if (!batch.IsCurrentIncarnation(generations[i]))
                        {
                            LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                            batches[i] = null!;
                            continue;
                        }

                        CompleteInflightEntry(batch);
                        batch.CompleteSend(-1, fireAndForgetTimestamp);
                        try { _onAcknowledgement?.Invoke(batch.TopicPartition, -1, fireAndForgetTimestamp, batch.CompletionSourcesCount, null); }
                        catch (Exception ackEx) { LogBatchCleanupStepFailed(ackEx, _brokerId); }
                    }

                    // Release everything synchronously (no pipelined response — fire-and-forget
                    // doesn't add to _pendingResponsesByConnection, so no in-flight slot to release)
                    for (var i = 0; i < count; i++)
                    {
                        if (batches[i] is not null)
                            CleanupBatch(batches[i]);
                    }

                    ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
                    return;
                }

                // Pipelined send: write request, get response task.
                // Add to _pendingResponsesByConnection for the send loop to process inline (like Java's client.poll()).
                // No fire-and-forget — responses are processed in the single-threaded send loop,
                // making retry ordering deterministic by construction.
                //
                // Use the connection-owned timeout path for pipelined sends. The send loop can
                // have multiple responses in flight on this connection; a reusable caller-owned
                // CTS would let a later send reset or cancel an earlier response timeout.
                var responseTask = await SendPipelinedAfterWriteAsync(
                    connection,
                    request,
                    (short)apiVersion).ConfigureAwait(false);

                // Clear batch references from scratch arrays (see ClearReferences() doc for exception-path semantics)
                scratch.ClearReferences();
                ReleaseResourcePins(batches, resourcePinCount);
                resourcePinCount = 0;

                // Release buffer memory now that data is written to the TCP buffer.
                // The untracked gap (between release and response) is bounded by the
                // per-connection in-flight byte ceiling, independent of request-count depth.
                // This is safe because: (1) the kernel has a copy in the TCP send buffer,
                // (2) the gap is bounded and small, (3) it unblocks producers waiting on
                // BufferMemory without the unbounded growth caused by drain-time release.
                // CleanupBatch still releases for error paths where TCP send wasn't reached.
                long encodedBytes = 0;
                for (var i = 0; i < count; i++)
                {
                    var batch = batches[i];
                    if (!batch.IsCurrentIncarnation(generations[i]))
                    {
                        LogStaleBatchInSendSkipped(_instanceId, _brokerId);
                        batches[i] = null!;
                        continue;
                    }

                    _accumulator.ReleaseBatchMemory(batch);
                    // EncodedSize is unavailable for some synthetic/test batches. DataSize
                    // is a conservative fallback and deliberately overestimates compression.
                    encodedBytes += Math.Max(batch.EncodedSize, batch.DataSize);
                }

                var pendingResponse = PendingResponse.Create(
                    responseTask,
                    batches,
                    generations,
                    count,
                    encodedBytes,
                    requestStartTime);
                _pendingResponsesByConnection[connectionIndex].Add(pendingResponse);
                pendingResponseAdded = true; // Array ownership transferred to PendingResponse
                Interlocked.Increment(ref _totalPendingResponseCount);
                Interlocked.Add(ref _totalPendingResponseBytes, encodedBytes);
                Interlocked.Add(ref _pendingResponseBytesByConnection[connectionIndex], encodedBytes);

                // Diagnostic: log instance+task+partitions at PendingResponse creation time.
                // This traces which batches are paired with which response task.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var pipelinedPartitions = string.Join(", ",
                        Enumerable.Range(0, count)
                            .Where(i => batches[i] is not null)
                            .Select(i => $"{batches[i].TopicPartition.Topic}-{batches[i].TopicPartition.Partition}"));
                    LogPendingResponseCreated(_instanceId, _brokerId, responseTask.Id, count, pipelinedPartitions);
                }

                // Diagnostic: mark batches as successfully pipelined to _pendingResponsesByConnection.
                // If an orphan trace shows 'S' but no 'W' (Wire), the batch never reached here.
                for (var i = 0; i < count; i++)
                    batches[i]?.AppendDiag('W');

                // Signal the send loop to wake up and poll when the response arrives.
                // Only a lightweight signal — actual processing happens in ProcessCompletedResponses.
                // Two signal paths: (1) channel write for the main WaitToReadAsync path,
                // (2) AsyncAutoResetSignal for the direct response-wait paths (replaces Task.WhenAny).
                //
                // Uses UnsafeOnCompleted with a cached Action delegate instead of ContinueWith
                // to avoid allocating a continuation Task on every pipelined send. The callback
                // is pre-allocated once in the constructor and reused for all response tasks.
                if (responseTask.IsCompleted)
                {
                    // Already completed — signal inline without registering a continuation.
                    _responseCompletionCallback();
                }
                else
                {
                    // Unlike ContinueWith(ExecuteSynchronously), UnsafeOnCompleted schedules the
                    // callback on the ThreadPool rather than running inline on the completing thread.
                    // This is intentional — it keeps the I/O completion thread free.
                    responseTask.ConfigureAwait(false).GetAwaiter()
                        .UnsafeOnCompleted(_responseCompletionCallback);
                }

                // Mute partitions when producer sequence numbers cannot preserve order, or when
                // the configured request limit already permits only 1 in-flight request.
                // This ensures at most one batch per partition in-flight across all requests.
                // Idempotent producers with maxInFlight > 1 use sequence numbers instead.
                MutePartitionsForSend(batches, count);

                LogPipelinedSend(_brokerId, count, _totalPendingResponseCount);
            }
            catch
            {
                scratch.ClearReferences();
                if (resourcePinCount > 0)
                {
                    ReleaseResourcePins(batches, resourcePinCount);
                    resourcePinCount = 0;
                }

                throw;
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Shutdown — fail batches permanently (no point retrying).
            // Check _cts (BrokerSender lifetime token) instead of cancellationToken because
            // the parameter may be a linked timeout CTS. On timeout, we want retry (Z path),
            // not permanent failure — only true shutdown should permanently fail batches.
            for (var i = 0; i < count; i++)
            {
                var batch = batches[i];

                // Skip batches recycled by a racing owner (e.g. ForceFailAllInFlightBatches
                // during disposal) — failing a re-rented batch would fail its new owner's data.
                if (batch is null || !batch.IsCurrentIncarnation(generations[i]))
                    continue;

                FailAndCleanupBatch(batch, new ObjectDisposedException(nameof(BrokerSender)));
            }

            scratch.ClearReferences();

            // Only return array if PendingResponse was NOT added. If it was, ProcessCompletedResponses
            // owns the array and will return it when processing the (faulted/cancelled) response.
            if (!pendingResponseAdded)
                ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
        }
        catch (Exception ex)
        {
            // Send failed (connection error, timeout, etc.) — retry batches instead of permanently failing.
            // Aligned with Java Kafka's Sender: transient failures cause reenqueue for retry.
            _pinnedConnections[connectionIndex] = null; // Invalidate only the broken connection
            LogResponseFailed(ex, _brokerId);

            for (var i = 0; i < count; i++)
            {
                var batch = batches[i];

                // Batch may have been recycled by a racing owner (disposal ForceFail, or a
                // completion that outran this handler). IsReturnedToPool alone is insufficient —
                // the flag resets when the pooled object is re-rented — so compare against the
                // generation snapshot taken at send start (#1570).
                if (batch is null || !batch.IsCurrentIncarnation(generations[i]))
                    continue;

                batch.AppendDiag('Z'); // Diagnostic: send failed, entering retry/fail path
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
                        metadataRefreshTopics ??= new HashSet<string>(StringComparer.Ordinal);
                        PrepareDeferredNetworkRetry(batch, metadataRefreshTopics);
                        _sendFailedRetries.Enqueue((batch, generations[i]));
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

            scratch.ClearReferences();

            if (!pendingResponseAdded)
                ArrayPool<ReadyBatch>.Shared.Return(batches, clearArray: true);
        }
        finally
        {
            // The generations snapshot transfers to PendingResponse along with the batches
            // array; ReturnBatchesArray returns both. On every other path it is owned here.
            if (!pendingResponseAdded)
                ArrayPool<int>.Shared.Return(generations);
        }
    }

    private void PrepareDeferredNetworkRetry(ReadyBatch batch, HashSet<string> metadataRefreshTopics)
    {
        // Parallel connection buckets may still be sending. Begin recovery here so a blocked
        // sibling write cannot postpone metadata refresh or the start of retry backoff.
        if (!batch.IsLoopExitRedelivery)
            MutePartition(batch.TopicPartition);
        LogRetriableErrorWithBackoff(ErrorCode.NetworkException, batch.TopicPartition.Topic,
            batch.TopicPartition.Partition, _options.RetryBackoffMs);
        batch.RetryNotBefore = Stopwatch.GetTimestamp() + _options.RetryBackoffTicks;

        bool refreshMetadata;
        lock (metadataRefreshTopics)
        {
            refreshMetadata = metadataRefreshTopics.Add(batch.TopicPartition.Topic);
        }

        if (refreshMetadata)
        {
            // The send token may already be cancelled when a write times out. Use the sender
            // lifetime token so recovery can still refresh metadata.
            _ = ObserveMetadataRefreshAsync(batch.TopicPartition.Topic, _cts.Token);
        }
    }

    /// <summary>
    /// Pre-allocated scratch space for building ProduceRequest without per-send allocations.
    /// The send loop is single-threaded; callers keep one scratch per potentially
    /// outstanding send so arrays and objects are not reused while a write still needs them.
    /// Uses internal array slices so ProduceRequest can serialize scratch data without boxing ArraySegment&lt;T&gt;.
    /// </summary>
    private sealed class ProduceRequestScratch
    {
        private readonly ProduceRequest _request;
        private readonly ProduceRequestTopicData[] _topicData;
        private readonly ProduceRequestPartitionData[] _partitionData;
        private readonly RecordBatch[][] _recordBatches;
        private int _lastTopicCount;
        private int _lastPartitionCount;

        public ProduceRequestScratch(ProducerOptions options, CompressionCodecRegistry compressionCodecs, int capacity)
        {
            _request = new ProduceRequest
            {
                Acks = (short)options.Acks,
                TimeoutMs = options.RequestTimeoutMs,
                TransactionalId = options.TransactionalId
            };
            _topicData = new ProduceRequestTopicData[capacity];
            _partitionData = new ProduceRequestPartitionData[capacity];
            _recordBatches = new RecordBatch[capacity][];
            for (var i = 0; i < capacity; i++)
            {
                _topicData[i] = new ProduceRequestTopicData();
                _partitionData[i] = new ProduceRequestPartitionData
                {
                    Compression = options.CompressionType,
                    CompressionCodecs = compressionCodecs
                };
                _recordBatches[i] = new RecordBatch[1];
            }
        }

        private sealed class ReadyBatchTopicComparer : IComparer<ReadyBatch>, System.Collections.IComparer
        {
            public static readonly ReadyBatchTopicComparer Instance = new();

            public int Compare(ReadyBatch? x, ReadyBatch? y)
                => string.Compare(x?.TopicPartition.Topic, y?.TopicPartition.Topic, StringComparison.Ordinal);

            int System.Collections.IComparer.Compare(object? x, object? y)
                => Compare((ReadyBatch?)x, (ReadyBatch?)y);
        }

        /// <summary>
        /// Populates the reusable request from the given batches. Returns the same
        /// ProduceRequest instance each time — callers must not hold references past
        /// the next call.
        /// </summary>
        public ProduceRequest Build(ReadyBatch[] batches, int[] generations, int count)
        {
            // Sort batches by topic name so equal topics are contiguous.
            // Fast-path: skip the O(n log n) sort when count <= 1 or already sorted.
            // The pre-scan uses string.Compare (ordinal) because it needs the sign of the
            // comparison to detect out-of-order elements, not just inequality.
            var batchesSpan = batches.AsSpan(0, count);
            var alreadySorted = true;
            var topicCount = count > 0 ? 1 : 0;

            for (var i = 1; i < count; i++)
            {
                var cmp = string.Compare(batchesSpan[i - 1].TopicPartition.Topic,
                    batchesSpan[i].TopicPartition.Topic, StringComparison.Ordinal);

                if (cmp > 0)
                {
                    alreadySorted = false;
                    break; // topicCount is partial — will be recomputed after sort below
                }

                if (cmp != 0)
                {
                    topicCount++;
                }
            }

            // Unsorted path: pays a small extra cost (partial pre-scan + sort + recount) compared
            // to sorting directly. This is an acceptable trade-off since multi-topic unsorted
            // batches are rare; the common case (single topic or already sorted) skips the sort.
            if (!alreadySorted)
            {
                System.Array.Sort((System.Array)batches, (System.Array)generations, 0, count, ReadyBatchTopicComparer.Instance);

                // Discard the partial topicCount from the pre-scan and recount from scratch.
                // Post-sort, topics are contiguous so simple != equality suffices (no need for
                // the three-way comparison the pre-scan uses to detect ordering violations).
                topicCount = count > 0 ? 1 : 0;
                for (var i = 1; i < count; i++)
                {
                    if (batchesSpan[i].TopicPartition.Topic != batchesSpan[i - 1].TopicPartition.Topic)
                    {
                        topicCount++;
                    }
                }
            }

            var topicIdx = 0;
            var partIdx = 0;
            var runStart = 0;
            var requestBodySizeHint = checked(
                ProduceRequestSizeCalculator.CompactStringSize(_request.TransactionalId) +
                2 + // Acks
                4 + // TimeoutMs
                ProduceRequestSizeCalculator.CompactArrayLengthSize(topicCount) +
                1); // Request tagged fields

            // Single pass: populate scratch topic and partition data from contiguous runs
            while (runStart < count)
            {
                var topicName = batchesSpan[runStart].TopicPartition.Topic;
                var runEnd = runStart + 1;
                while (runEnd < count && batchesSpan[runEnd].TopicPartition.Topic == topicName)
                {
                    runEnd++;
                }

                var partCount = runEnd - runStart;
                var partitionDataStart = partIdx;
                requestBodySizeHint = checked(
                    requestBodySizeHint +
                    ProduceRequestSizeCalculator.CompactStringSize(topicName) +
                    ProduceRequestSizeCalculator.CompactArrayLengthSize(partCount) +
                    1); // Topic tagged fields

                for (var p = 0; p < partCount; p++)
                {
                    var batch = batchesSpan[runStart + p];
                    _recordBatches[partIdx][0] = batch.RecordBatch;
                    var partData = _partitionData[partIdx];
                    partData.Index = batch.TopicPartition.Partition;
                    partData.Records = _recordBatches[partIdx];
                    requestBodySizeHint = checked(
                        requestBodySizeHint +
                        4 + // Partition index
                        ProduceRequestSizeCalculator.CompactBytesLengthSize(batch.EncodedSize) +
                        batch.EncodedSize +
                        1); // Partition tagged fields
                    partIdx++;
                }

                var topicData = _topicData[topicIdx];
                topicData.Name = topicName;
                topicData.SetPartitionDataScratch(_partitionData, partitionDataStart, partCount);
                topicIdx++;

                runStart = runEnd;
            }

            _request.SetTopicDataScratch(_topicData, topicCount);
            _request.RequestBodySizeHint = requestBodySizeHint;
            _lastTopicCount = topicCount;
            _lastPartitionCount = partIdx;
            return _request;
        }

        /// <summary>
        /// Clears references in scratch structures after a send completes to avoid
        /// holding onto RecordBatch data longer than necessary.
        /// </summary>
        public void ClearReferences()
        {
            _request.ClearTopicDataScratch();
            _request.RequestBodySizeHint = 0;
            for (var i = 0; i < _lastTopicCount; i++)
            {
                _topicData[i].Name = string.Empty;
                _topicData[i].ClearPartitionDataScratch();
            }
            for (var i = 0; i < _lastPartitionCount; i++)
            {
                _partitionData[i].Records = [];
                _recordBatches[i][0] = default!;
            }
            _lastTopicCount = 0;
            _lastPartitionCount = 0;
        }

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
    /// Resets a per-connection timeout CTS for reuse, or recreates it if TryReset fails.
    /// Mirrors the single-connection path's sendTimeoutCts reuse pattern.
    /// </summary>
    private static void ResetBucketTimeout(ref CancellationTokenSource cts, CancellationToken shutdownToken)
    {
        if (!cts.TryReset())
        {
            cts.Dispose();
            cts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        }
        cts.CancelAfter(SendCoalescedTimeoutMs);
    }

    /// <summary>
    /// Sends a bucket of batches on the specified connection. Rents a pooled array,
    /// acquires the connection, and calls SendCoalescedAsync.
    /// Takes <paramref name="connectionBuckets"/> array + index instead of ref struct
    /// because async methods cannot have ref parameters.
    /// </summary>
    private async ValueTask SendConnectionBucketAsync(
        int connIdx, ConnectionBucket[] connectionBuckets,
        ProduceRequestScratch scratch,
        HashSet<string> metadataRefreshTopics,
        CancellationToken cancellationToken)
    {
        ref var bucket = ref connectionBuckets[connIdx];
        var batchesToSend = ArrayPool<ReadyBatch>.Shared.Rent(bucket.Count);
        var countToSend = 0;
        for (var i = 0; i < bucket.Count; i++)
        {
            var batch = bucket.Batches[i];
            if (batch.IsCurrentIncarnation(bucket.Generations[i]))
                batchesToSend[countToSend++] = batch;
            else
                LogStaleBatchInSendSkipped(_instanceId, _brokerId);
        }
        bucket.Clear();

        if (countToSend == 0)
        {
            ArrayPool<ReadyBatch>.Shared.Return(batchesToSend, clearArray: true);
            return;
        }

        await SendCoalescedAsync(
                batchesToSend,
                countToSend,
                scratch,
                connIdx,
                cancellationToken,
                metadataRefreshTopics)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits all pending parallel sends, observing every ValueTask even if one faults.
    /// Returns the number of successfully completed sends. When an exception is thrown,
    /// the return value is not used — the exception unwinds the send loop entirely.
    /// </summary>
    /// <remarks>
    /// All ValueTasks were started before this method is called, so the underlying TCP
    /// FlushAsync operations run concurrently. The sequential await loop simply collects
    /// results in index order — total wall-clock time is max(latencies), identical to
    /// Task.WhenAll. We avoid Task.WhenAll because ValueTask.AsTask() allocates a Task
    /// object per send, violating the zero-allocation hot-path principle.
    /// Stale ValueTask entries from previous iterations are harmlessly overwritten by
    /// the caller before each call (ValueTask is a struct).
    /// </remarks>
    private static async ValueTask<int> AwaitParallelSendsAsync(ValueTask[] sends, int count)
    {
        Exception? firstException = null;

        for (var i = 0; i < count; i++)
        {
            try
            {
                await sends[i].ConfigureAwait(false);
            }
            catch (Exception ex) when (firstException is null)
            {
                firstException = ex;
            }
            catch
            {
                // Observe but discard subsequent exceptions — first one wins.
            }
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }

        // All sends succeeded — partial completion is not possible on the success path
        // because any failure throws above.
        return count;
    }

    /// <summary>
    /// Returns the connection index for a partition, respecting any active migration fence.
    /// During migration, fenced partitions continue routing to their old connection
    /// until in-flight batches complete, preserving per-partition broker append order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetConnectionForPartition(TopicPartition topicPartition)
    {
        // Common case: no migrations active — dictionary Count check is a single branch on 0.
        // During migration: one hash lookup per batch, negligible vs TCP write cost.
        return _migratingPartitions.Count > 0
            && _migratingPartitions.TryGetValue(topicPartition, out var oldConn)
            ? oldConn
            : topicPartition.Partition % _connectionCount;
    }

    /// <summary>
    /// Checks whether a specific partition has any in-flight batches on the given connection.
    /// Scans pending responses (at most _maxInFlight entries, typically 5) for the connection.
    /// Only called during migration transitions — not on the hot path.
    /// </summary>
    private bool HasInflightForPartition(int connectionIndex, TopicPartition topicPartition)
    {
        if ((uint)connectionIndex >= (uint)_pendingResponsesByConnection.Length)
            return false;

        var pendingList = _pendingResponsesByConnection[connectionIndex];
        for (var i = 0; i < pendingList.Count; i++)
        {
            var pr = pendingList[i];
            for (var j = 0; j < pr.Count; j++)
            {
                // Entries within Count can legally be null: batches with stale generations
                // are nulled out before PendingResponse.Create captures the array.
                if (pr.Batches[j] is { } batch && batch.TopicPartition == topicPartition)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks migrating partitions whose old connection is <paramref name="connectionIndex"/>
    /// and unfences them if they no longer have in-flight batches on that connection.
    /// Called from ProcessCompletedResponses only when _migratingPartitions is non-empty.
    /// </summary>
    private void CompleteMigrations(int connectionIndex)
    {
        var pendingList = _pendingResponsesByConnection[connectionIndex];
        _migrationRemovalBuffer.Clear();

        foreach (var (topicPartition, oldConnection) in _migratingPartitions)
        {
            if (oldConnection != connectionIndex)
                continue;

            // Empty connection is the common completion case and avoids rescanning batches.
            if (pendingList.Count == 0 || !HasInflightForPartition(connectionIndex, topicPartition))
                _migrationRemovalBuffer.Add(topicPartition);
        }

        for (var i = 0; i < _migrationRemovalBuffer.Count; i++)
            _migratingPartitions.Remove(_migrationRemovalBuffer[i]);
    }

    /// <summary>
    /// Fences partitions whose connection assignment changes during a scale-up.
    /// Partitions with in-flight batches on their old connection are added to
    /// <see cref="_migratingPartitions"/> so they continue routing to the old connection
    /// until in-flight clears. This preserves broker append order for non-idempotent
    /// producers and sequence order for idempotent producers. Scale-down does not fence:
    /// its drain gate guarantees no in-flight batches before any routing assignment changes.
    /// </summary>
    private void FenceAffectedPartitions(int oldConnCount, int newConnCount)
    {
        foreach (var tp in _knownPartitions)
        {
            var partition = tp.Partition;
            var oldConn = partition % oldConnCount;
            var newConn = partition % newConnCount;

            if (oldConn != newConn && HasInflightForPartition(oldConn, tp))
            {
                _migratingPartitions.TryAdd(tp, oldConn);
            }
        }

        if (_migrationRemovalBuffer.Capacity < _migratingPartitions.Count)
            _migrationRemovalBuffer.Capacity = _migratingPartitions.Count;

        if (_migratingPartitions.Count > 0)
            LogPartitionMigrationStarted(_brokerId, _migratingPartitions.Count, oldConnCount, newConnCount);
    }

    /// <summary>
    /// Leases and returns the pinned connection for the given connection index.
    /// Each connection slot caches a healthy connection for reuse, while the lease bridges
    /// selection to request registration so shared-pool retirement cannot dispose it between them.
    /// </summary>
    private async ValueTask<KafkaConnectionLease> GetConnectionLeaseAtIndexAsync(
        int connIdx,
        CancellationToken cancellationToken)
    {
        var conn = _pinnedConnections[connIdx];
        if (conn is not null
            && conn.IsConnected
            && KafkaConnectionLease.TryAcquire(conn, out var pinnedLease))
        {
            return pinnedLease;
        }

        // Shared pools may retain an expanded group owned by another client. An owning
        // sender also retains a one-slot group after scaling back down. Keep those slots
        // indexed, while preserving the singleton path before this sender first scales up.
        var connectionLease = _connectionCount > 1 || !_canPhysicallyShrinkConnections || _hasScaledConnectionGroup
            ? await _connectionPool.LeaseConnectionByIndexAsync(_brokerId, connIdx, cancellationToken)
                .ConfigureAwait(false)
            : await _connectionPool.LeaseConnectionAsync(_brokerId, cancellationToken)
                .ConfigureAwait(false);

        _pinnedConnections[connIdx] = connectionLease.Connection;
        return connectionLease;
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
    private void MutePartition(TopicPartition tp)
    {
        if (_mutedPartitions.TryAdd(tp, 0))
        {
            var mutedPartitionCount = Interlocked.Increment(ref _mutedPartitionCount);
            AtomicMax(ref _mutedPartitionHighWatermark, mutedPartitionCount);
            _accumulator.MutePartition(tp);
        }
    }

    private void MutePartitionsForSend(ReadyBatch[] batches, int count)
    {
        if (!_muteOnSend)
            return;

        for (var i = 0; i < count; i++)
        {
            if (!batches[i].IsLoopExitRedelivery)
                MutePartition(batches[i].TopicPartition);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UnmutePartition(TopicPartition tp)
    {
        if (_mutedPartitions.TryRemove(tp, out _))
        {
            Interlocked.Decrement(ref _mutedPartitionCount);
            _accumulator.UnmutePartition(tp);
        }
        _eventChannel.Writer.TryWrite(SendLoopEvent.Unmute());
    }

    private static void AtomicMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var original = Interlocked.CompareExchange(ref target, value, current);
            if (original == current)
                return;

            current = original;
        }
    }

    private bool RegisterLoopExitRecovery(ReadyBatch batch, int expectedGeneration)
        => _accumulator.TryRegisterLoopExitRecovery(batch, expectedGeneration);

    private static bool PrepareLoopExitRedelivery(ReadyBatch batch, int expectedGeneration)
    {
        if (!batch.TryAcquireResourcePin(expectedGeneration))
            return false;

        try
        {
            if (!batch.IsCurrentIncarnation(expectedGeneration))
                return false;

            batch.IsRetry = true;
            batch.AppendDiag('Y');
            return batch.IsCurrentIncarnation(expectedGeneration);
        }
        finally
        {
            batch.ReleaseResourcePin();
        }
    }

    /// <summary>
    /// Sweeps carry-over for batches that have exceeded their delivery deadline.
    /// Prevents muted batches from sitting indefinitely while their partition's retry cycles.
    /// Called from the single-threaded send loop after coalescing.
    /// </summary>
    private void SweepExpiredCarryOver(PartitionCarryOver carryOver)
    {
        var now = _getTimestamp();
        var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks;

        foreach (var kvp in carryOver.Partitions)
        {
            var queue = kvp.Value;
            for (var i = queue.Count - 1; i >= 0; i--)
            {
                var batchRef = queue[i];
                if (!batchRef.IsCurrentIncarnation())
                {
                    carryOver.RemoveAt(queue, i);
                    continue;
                }

                var batch = batchRef.Batch;
                if (now < batch.StopwatchCreatedTicks + deliveryTimeoutTicks)
                    continue;

                if (batch.IsRetry)
                {
                    batch.IsRetry = false;
                    batch.RetryNotBefore = 0;
                    UnmutePartition(batch.TopicPartition);
                }

                LogDeliveryTimeoutExceeded(_brokerId, batch.TopicPartition.Topic,
                    batch.TopicPartition.Partition);
                var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks, now);
                var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs);
                FailAndCleanupBatch(batch, new KafkaTimeoutException(
                    TimeoutKind.Delivery,
                    elapsed,
                    configured,
                    $"Delivery timeout exceeded for {batch.TopicPartition}"));
                carryOver.RemoveAt(queue, i);
            }
        }
    }

    private void DisposeCarryOverBatchesOnLoopExit(
        PartitionCarryOver carryOver,
        bool redeliver,
        ObjectDisposedException disposedException,
        List<BatchReference>? recoveredBatches,
        HashSet<(ReadyBatch Batch, int Generation)>? recoveredBatchSet)
    {
        foreach (var kvp in carryOver.Partitions)
        {
            var queue = kvp.Value;
            for (var i = 0; i < queue.Count; i++)
            {
                var batchRef = queue[i];
                if (batchRef.IsCurrentIncarnation())
                    CollectOrFailOnLoopExit(batchRef, redeliver, disposedException,
                        recoveredBatches, recoveredBatchSet);
            }
        }
    }

    private void CollectOrFailOnLoopExit(
        BatchReference batchRef,
        bool redeliver,
        ObjectDisposedException disposedException,
        List<BatchReference>? recoveredBatches,
        HashSet<(ReadyBatch Batch, int Generation)>? recoveredBatchSet)
    {
        if (!batchRef.IsCurrentIncarnation())
            return;

        if (!redeliver)
        {
            RedeliverOrFailOnLoopExit(batchRef.Batch, batchRef.Generation,
                redeliver: false, disposedException);
            return;
        }

        if (!recoveredBatchSet!.Add((batchRef.Batch, batchRef.Generation)))
            return;

        if (RegisterLoopExitRecovery(batchRef.Batch, batchRef.Generation))
            recoveredBatches!.Add(batchRef);
    }

    /// <summary>
    /// Disposes a batch that survived the send loop's exit. During shutdown the batch is
    /// permanently failed. After an unexpected loop crash the batch is handed back to the
    /// producer via the reroute callback, which routes it to a fresh replacement sender —
    /// unless its delivery deadline has already passed, in which case it fails with a
    /// delivery timeout (the bound that prevents endless redelivery cycles).
    /// Never throws — failures are logged so the finally block's sweep over the remaining
    /// batches always completes. Internal for testing.
    /// </summary>
    internal void RedeliverOrFailOnLoopExit(ReadyBatch batch, bool redeliver, ObjectDisposedException disposedException)
        => RedeliverOrFailOnLoopExit(batch, batch.Generation, redeliver, disposedException);

    private void RedeliverOrFailOnLoopExit(
        ReadyBatch batch,
        int expectedGeneration,
        bool redeliver,
        ObjectDisposedException disposedException)
    {
        try
        {
            if (!batch.IsCurrentIncarnation(expectedGeneration))
                return;

            if (!redeliver)
            {
                FailAndCleanupBatch(batch, expectedGeneration, disposedException);
                return;
            }

            if (Stopwatch.GetTimestamp() >= batch.StopwatchCreatedTicks + _options.DeliveryTimeoutTicks)
            {
                var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks);
                var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs);
                FailAndCleanupBatch(batch, expectedGeneration, new KafkaTimeoutException(
                    TimeoutKind.Delivery, elapsed, configured,
                    $"Delivery timeout exceeded for {batch.TopicPartition} after send-loop exit"));
                return;
            }

            if (!RegisterLoopExitRecovery(batch, expectedGeneration))
                return;

            if (!PrepareLoopExitRedelivery(batch, expectedGeneration))
                return;
            _rerouteBatch!(batch, expectedGeneration);
        }
        catch (Exception cleanupEx)
        {
            try { FailAndCleanupBatch(batch, expectedGeneration, cleanupEx); }
            catch (Exception failEx) { LogBatchCleanupStepFailed(failEx, _brokerId); }
            LogBatchCleanupStepFailed(cleanupEx, _brokerId);
        }
    }

    private void FailAndCleanupBatch(ReadyBatch batch, Exception ex)
    {
        FailBatch(batch, ex, sendCompletionClaimed: false);
        try { CleanupBatch(batch); }
        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
    }

    private void FailAndCleanupBatch(ReadyBatch batch, int expectedGeneration, Exception ex)
    {
        if (!batch.TryAcquireResourcePin(expectedGeneration))
            return;

        var sendCompletionClaimed = false;
        try
        {
            sendCompletionClaimed = batch.TryClaimSendCompletion(expectedGeneration);
        }
        finally
        {
            batch.ReleaseResourcePin();
        }

        if (!sendCompletionClaimed)
            return;

        try
        {
            FailBatch(batch, ex, sendCompletionClaimed: true);
            try { CleanupBatchResources(batch); }
            catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
        }
        finally
        {
            try { _accumulator.CompleteTerminalBookkeepingAndReturnReadyBatch(batch); }
            catch (Exception returnEx) { LogBatchCleanupStepFailed(returnEx, _brokerId); }
        }
    }

    private void FailBatch(ReadyBatch batch, Exception ex, bool sendCompletionClaimed)
    {
        batch.AppendDiag('F');
        // Every operation is wrapped in try/catch to guarantee we reach CleanupBatch.
        // If CompleteInflightEntry throws, batch.Fail must still run to resolve completion sources.
        // If batch.Fail throws, CleanupBatch must still run to release memory and return the batch.
        // A single unwrapped throw here causes the caller's loop to skip remaining batches,
        // leaking their completion sources and causing producer hangs (deadlocks).
        try { CompleteInflightEntry(batch); }
        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
        try
        {
            if (sendCompletionClaimed)
                batch.FailAfterSendCompletionClaimed(ex);
            else
                batch.Fail(ex);
        }
        catch (Exception failEx) { LogBatchCleanupStepFailed(failEx, _brokerId); }
        try
        {
            _onAcknowledgement?.Invoke(batch.TopicPartition, -1, DateTimeOffset.UtcNow,
            batch.CompletionSourcesCount, ex);
        }
        catch (Exception ackEx) { LogBatchCleanupStepFailed(ackEx, _brokerId); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CleanupBatch(ReadyBatch batch)
    {
        CleanupBatchResources(batch);

        // ReturnReadyBatch is idempotent (atomic _returnedToPool flag), so this is safe
        // even if ForceFailAllInFlightBatches races during disposal.
        try { _accumulator.ReturnReadyBatch(batch); }
        catch (Exception returnEx) { LogBatchCleanupStepFailed(returnEx, _brokerId); }
    }

    private void CleanupBatchResources(ReadyBatch batch)
    {
        // Release buffer memory at batch completion (matching Java's RecordAccumulator.deallocate()).
        // This is the primary release path: memory is held throughout the entire pipeline
        // (append → drain → send → response) to provide accurate end-to-end backpressure.
        _accumulator.ReleaseBatchMemory(batch);
        // Remove from tracking and decrement in-flight counter. OnBatchExitsPipeline uses
        // TryRemove as an atomic guard — if another path already removed this batch
        // (e.g., SweepExpiredInFlightBatches), it returns false but we still return
        // the batch to the pool since the sweep defers pool return to BrokerSender.
        _accumulator.OnBatchExitsPipeline(batch);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        LogDisposing(_brokerId);

        // Complete the channel so no new events are accepted, then cancel the CTS so
        // WaitToReadAsync is interrupted promptly. We use CancelAsync here (rather than
        // synchronous Cancel) because CTS cancellation callbacks may perform I/O and we
        // are already in an async context that can await them without blocking a thread.
        _eventChannel.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);

        // Wait for send loop to finish (should exit quickly now that CTS is cancelled).
        // The send loop owns _pendingResponsesByConnection — it will process remaining responses
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

        // A final send-loop iteration may have already detached a retirement drain after
        // clearing the raw connection fields. Wait for every such drain before inspecting
        // the remaining scale-down state.
        var retirementDrainTasks = _retirementDrainTasks.Keys.ToArray();
        if (retirementDrainTasks.Length > 0)
        {
            await WaitForDisposalDrainAsync(
                Task.WhenAll(retirementDrainTasks)).ConfigureAwait(false);
        }

        // Observe any in-flight shrink task and dispose the draining connection.
        // After the send loop exits, these won't be polled by MaybeScaleConnections.
        if (_pendingShrinkTask is { } pendingShrinkTask)
        {
            _pendingShrinkTask = null;
            if (pendingShrinkTask.IsCompleted)
            {
                await WaitForDisposalDrainAsync(
                    DisposeShrinkResultAsync(pendingShrinkTask)).ConfigureAwait(false);
            }
            else
            {
                // A test double or custom pool may ignore the cancelled lifetime token.
                // Keep ownership of any connection removed after disposal returns.
                _ = DisposeShrinkResultAsync(pendingShrinkTask);
            }
        }

        if (_drainingConnection is not null)
        {
            await WaitForDisposalDrainAsync(
                RetiredConnectionDisposer.DrainAndDisposeAsync(
                    _drainingConnection,
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            _drainingConnection = null;
        }

        if (_retiringConnection is not null)
        {
            await WaitForDisposalDrainAsync(
                RetiredConnectionDisposer.DrainAndDisposeAsync(
                    _retiringConnection,
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            _retiringConnection = null;
        }

        _migratingPartitions.Clear();

        var totalPending = _totalPendingResponseCount;
        if (totalPending > 0)
        {
            LogFailingPendingResponses(_brokerId, totalPending);

            for (var connIdx = 0; connIdx < _pendingResponsesByConnection.Length; connIdx++)
            {
                var pendingList = _pendingResponsesByConnection[connIdx];
                for (var i = 0; i < pendingList.Count; i++)
                {
                    var pr = pendingList[i];
                    for (var j = 0; j < pr.Count; j++)
                    {
                        if (pr.IsSameIncarnation(j))
                        {
                            try { FailAndCleanupBatch(pr.Batches[j], new ObjectDisposedException(nameof(BrokerSender))); }
                            catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx, _brokerId); }
                        }
                    }

                    pr.ReturnBatchesArray();
                }

                Interlocked.Add(ref _totalPendingResponseCount, -pendingList.Count);
                var removedBytes = SumEncodedBytes(pendingList);
                Interlocked.Add(ref _totalPendingResponseBytes, -removedBytes);
                Interlocked.Add(ref _pendingResponseBytesByConnection[connIdx], -removedBytes);
                pendingList.Clear();
                // No TrimExcess — lists are unreachable after disposal
            }
        }

        _cts.Dispose();
        _anyResponseCompleted.Dispose();
    }

    private async ValueTask WaitForDisposalDrainAsync(Task drainTask)
    {
        try
        {
            if (_canPhysicallyShrinkConnections)
                await drainTask.ConfigureAwait(false);
            else
                await drainTask.WaitAsync(_disposalDrainTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            LogBatchCleanupStepFailed(ex, _brokerId);
            // WaitAsync bounds DisposeAsync only. The drain retains connection ownership
            // and must continue until sibling leases and operations finish.
            _ = drainTask.ContinueWith(
                static (task, _) => { _ = task.Exception; },
                null,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            LogBatchCleanupStepFailed(ex, _brokerId);
        }
    }

    private async Task DisposeShrinkResultAsync(Task<IKafkaConnection?> shrinkTask)
    {
        try
        {
            var removedConnection = await shrinkTask.ConfigureAwait(false);
            if (removedConnection is not null)
            {
                await RetiredConnectionDisposer.DrainAndDisposeAsync(
                    removedConnection,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when sender lifetime cancellation reaches pool shrink.
        }
        catch (Exception ex)
        {
            LogBatchCleanupStepFailed(ex, _brokerId);
        }
    }

    private async Task ObserveMetadataRefreshAsync(string topic, CancellationToken cancellationToken)
    {
        try
        {
            await _metadataManager.RefreshMetadataAsync([topic], forceRefresh: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort refresh — failures are already logged by MetadataManager
        }
    }

    /// <summary>
    /// Non-blocking adaptive scaling check. Connection creation/removal runs in the background
    /// so the send loop is never blocked by TCP handshakes or connection draining.
    /// Field mutations happen on the send loop thread when the background task completes
    /// (polled each iteration).
    /// Returns the new connection count if scaling completed this iteration, or 0 otherwise.
    /// </summary>
    private int MaybeScaleConnections(PartitionCarryOver carryOver, bool hasUnreadEvent)
    {
        // Phase 0: Poll draining connection for disposal
        MaybeDrainAndDisposeConnection();

        // Phase 1a: Check if a pending background scale-up completed
        if (_pendingScaleTask is not null)
        {
            if (!_pendingScaleTask.IsCompleted)
                return 0; // Still connecting — send loop continues unblocked

            var task = _pendingScaleTask;
            _pendingScaleTask = null;

            if (task.Status == TaskStatus.RanToCompletion)
            {
                var actualCount = task.Result;
                if (actualCount > _connectionCount)
                {
                    return ApplyScaleUp(actualCount);
                }
            }
            else if (task.Exception is not null)
            {
                LogAdaptiveScaleFailed(task.Exception.InnerException ?? task.Exception, _brokerId, _connectionCount);
            }

            return 0;
        }

        // Phase 1b: Check if a pending background shrink completed
        if (_pendingShrinkTask is not null)
        {
            if (!_pendingShrinkTask.IsCompleted)
                return 0; // Still shrinking — send loop continues unblocked

            var task = _pendingShrinkTask;
            _pendingShrinkTask = null;

            if (task.Status == TaskStatus.RanToCompletion)
            {
                var removedConnection = task.Result;
                if (removedConnection is not null)
                {
                    return ApplyScaleDown(removedConnection);
                }
                // Nothing was removed (pool group already at or below target) —
                // the reduced routing width stands on its own.
            }
            else if (task.Exception is not null)
            {
                LogAdaptiveScaleDownFailed(task.Exception.InnerException ?? task.Exception, _brokerId, _connectionCount);

                // The pool still owns the old connection count — restore the routing
                // width so the extra connection isn't orphaned. _connectionCount cannot
                // have changed since initiation: this Phase 1b poll runs before any
                // other scale event can start.
                var reducedConnectionCount = _connectionCount;
                _connectionCount++;
                _totalMaxInFlight = _connectionCount * _maxInFlight;
                UpdateInFlightByteBudget(_connectionCount);
                _accumulator.RecordConnectionScaleEvent(
                    _brokerId,
                    reducedConnectionCount,
                    _connectionCount,
                    _accumulator.BufferUtilization,
                    _accumulator.BufferPressureEvents - _lastPressureSnapshot,
                    _sendLoopPressureEvents - _lastSendLoopPressureSnapshot);
            }

            return 0;
        }

        var now = Dekaf.MonotonicClock.GetMilliseconds();
        var activeMutedPartitionCount = _muteOnSend
            ? Volatile.Read(ref _mutedPartitionCount)
            : 0;
        var mutedPartitionHighWatermark = _muteOnSend
            ? Interlocked.Exchange(ref _mutedPartitionHighWatermark, activeMutedPartitionCount)
            : 0;
        if (Math.Max(activeMutedPartitionCount, mutedPartitionHighWatermark) >= _connectionCount)
        {
            _lastMutedPartitionLoadTicks = now;
        }

        // Cooldown applies to both scale-up and scale-down
        if (now - _lastScaleTimeTicks < _scaleCooldownMs)
            return 0;

        // A previous scale-down is still retiring its removed connection — hold off on any
        // new scale event until it drains, so slot indices stay unambiguous (a scale-up
        // would re-activate the retiring slot while its old connection is still pinned).
        if (_retiringConnection is not null)
            return 0;

        var utilization = _accumulator.BufferUtilization;
        var bufferPressureDelta = _accumulator.BufferPressureEvents - _lastPressureSnapshot;
        var sendLoopPressureDelta = _sendLoopPressureEvents - _lastSendLoopPressureSnapshot;
        // The unacked-byte admission gate holds BufferUtilization near zero while it binds,
        // which would silently starve scale-up (and the budget would self-limit at the drain
        // rate of the initial width). Blocked admissions therefore feed scale pressure directly.
        var unackedBlockEvents = _unackedBudget?.AdmissionBlockEvents ?? 0;
        var unackedBlockDelta = unackedBlockEvents - _lastUnackedBlockSnapshot;
        if (unackedBlockEvents != _lastUnackedBlockObserved)
        {
            _lastUnackedBlockObserved = unackedBlockEvents;
            _lastUnackedBlockObservedMs = now;
        }
        var usefulUnackedBlockDelta = IsPartitionPressureScaleUseful(
            _adaptiveScalingEnabled,
            _connectionCount,
            _maxConnectionsPerBroker,
            _knownPartitions.Count)
            ? unackedBlockDelta
            : 0;
        var scalePressureDelta = ComputeScalePressureDelta(
            bufferPressureDelta + usefulUnackedBlockDelta, sendLoopPressureDelta);
        var hasScalePressure = scalePressureDelta >= ScalePressureDeltaThreshold;

        // Phase 2: Check if we should start a new scale-up
        if (sendLoopPressureDelta >= ScalePressureDeltaThreshold
            || usefulUnackedBlockDelta >= ScalePressureDeltaThreshold
            || (utilization >= ScaleUtilizationThreshold && hasScalePressure))
        {
            // High utilization — reset low-utilization tracking
            _lowUtilizationStartTicks = 0;

            if (_connectionCount >= _maxConnectionsPerBroker)
                return 0; // At ceiling — cannot scale up, but scale-down remains active

            var targetCount = ComputeScaleTarget(scalePressureDelta, _connectionCount, _maxConnectionsPerBroker);

            // Partition affinity (partition % N) means connections beyond the partition
            // count can never receive traffic — they'd sit idle and immediately arm
            // scale-down churn. Cap the target at the partitions this broker serves.
            if (_knownPartitions.Count > 0 && targetCount > _knownPartitions.Count)
                targetCount = Math.Max(_connectionCount, _knownPartitions.Count);

            if (targetCount <= _connectionCount)
                return 0; // Cap left nothing to add

            // Launch connection creation in the background — send loop continues immediately
            _lastScaleTimeTicks = now;
            _pendingScaleBufferUtilization = utilization;
            _pendingScaleBufferPressureDelta = bufferPressureDelta;
            _pendingScaleSendLoopPressureDelta = sendLoopPressureDelta;
            _pendingScaleTask = _connectionPool.ScaleConnectionGroupAsync(
                _brokerId, targetCount, _cts.Token).AsTask();

            return 0;
        }

        // Phase 3: Check if we should start a scale-down
        if (_connectionCount <= _minConnectionCount)
        {
            _lowUtilizationStartTicks = 0; // At minimum — nothing to scale down
            return 0;
        }

        // Recent admission-gate blocks mean the workload is throttled by the unacked-byte
        // budget, not idle — buffer utilization is a false-idle signal here (same inversion
        // as the mute-on-send guards below). Never shrink while the gate was recently
        // binding, using the same sustained window as the muted-partition-load guard.
        if (_lastUnackedBlockObservedMs > 0
            && now - _lastUnackedBlockObservedMs < _scaleDownSustainedMs)
        {
            _lowUtilizationStartTicks = 0;
            return 0;
        }

        if (utilization >= ScaleDownUtilizationThreshold)
        {
            // Above scale-down threshold — reset tracking
            _lowUtilizationStartTicks = 0;
            return 0;
        }

        // Utilization is below the scale-down threshold
        if (_lowUtilizationStartTicks == 0)
        {
            // Start tracking sustained low utilization
            _lowUtilizationStartTicks = now;
            return 0;
        }

        if (now - _lowUtilizationStartTicks < _scaleDownSustainedMs)
            return 0; // Not sustained long enough

        // Low buffer occupancy can mean the expanded connection group is keeping up, not
        // that it is idle. Mute-on-send limits each active partition to one request, so
        // connectionCount * maxInFlight overstates usable capacity by up to two orders of
        // magnitude. Compare occupancy with active partition capacity and require a full
        // sustained window after the muted width was large enough to occupy every
        // connection. Tracking the peak catches mute/unmute cycles between scale checks
        // without treating continuous single-partition trickle as full-width load.
        var pendingResponseCount = Volatile.Read(ref _totalPendingResponseCount);
        var effectiveInFlightCapacity = activeMutedPartitionCount > 0
            ? Math.Min(_totalMaxInFlight, activeMutedPartitionCount)
            : _totalMaxInFlight;
        var inFlightUtilization = effectiveInFlightCapacity > 0
            ? (double)pendingResponseCount / effectiveInFlightCapacity
            : 0;
        var hasRecentMutedPartitionLoad = _muteOnSend
            && _lastMutedPartitionLoadTicks > 0
            && now - _lastMutedPartitionLoadTicks < _scaleDownSustainedMs;
        var hasLoadBearingMutedWidth = _muteOnSend
            && activeMutedPartitionCount >= _connectionCount;
        if (inFlightUtilization >= ScaleDownInFlightUtilizationThreshold
            || hasLoadBearingMutedWidth
            || hasRecentMutedPartitionLoad
            || HasMutedPartitionLoad(carryOver, hasUnreadEvent))
        {
            _lowUtilizationStartTicks = 0;
            return 0;
        }

        // Every acknowledged producer requires ALL connections to drain before shrinking.
        // Modulo routing changes for partitions on retained slots too (P % N -> P % (N-1));
        // moving a non-idempotent partition while its old request remains unacknowledged can
        // reorder broker appends just as surely as it can violate idempotent sequence order.
        if (_totalPendingResponseCount > 0)
            return 0;

        // Sustained low utilization with no in-flight on the connection being removed —
        // initiate scale-down.
        var targetShrinkCount = _connectionCount - 1;

        // Reduce the routing width IMMEDIATELY, before the pool shrink runs in the background.
        // This closes the race where sends later in this same iteration (or in iterations
        // while the shrink task runs) pipeline a request onto the connection being removed:
        // previously those pending responses were silently dropped when the arrays were
        // truncated at apply time, stranding their batches with never-completing delivery
        // tasks (#1578). From this instant no new request can target the removed slot, and
        // its pending list stays in place because per-connection arrays are never resized.
        _connectionCount = targetShrinkCount;
        _totalMaxInFlight = targetShrinkCount * _maxInFlight;
        UpdateInFlightByteBudget(targetShrinkCount);

        // The drain gates above guarantee no in-flight batches that require routing to the
        // removed slot, so any leftover migration fences are stale — clear them so no
        // partition keeps routing outside the reduced width.
        _migratingPartitions.Clear();

        _accumulator.RecordConnectionScaleEvent(
            _brokerId,
            targetShrinkCount + 1,
            targetShrinkCount,
            utilization,
            bufferPressureDelta,
            sendLoopPressureDelta);

        _lastScaleTimeTicks = now;
        _lowUtilizationStartTicks = 0; // Reset for the next scale-down cycle

        // KafkaClient shares one ConnectionPool across producers, consumers, and admin
        // clients. This sender can reduce its own routing width, but cannot prove the
        // shared pool slot is idle for every owner and therefore must not remove it.
        if (!_canPhysicallyShrinkConnections)
        {
            _pinnedConnections[targetShrinkCount] = null;
            LogAdaptiveScaleDown(_brokerId, targetShrinkCount + 1, targetShrinkCount);
            return targetShrinkCount;
        }

        _pendingShrinkTask = _connectionPool.ShrinkConnectionGroupAsync(
            _brokerId, targetShrinkCount, _cts.Token).AsTask();

        return 0;
    }

    private bool HasMutedPartitionLoad(PartitionCarryOver carryOver, bool hasUnreadEvent)
    {
        if (!_muteOnSend || _mutedPartitions.IsEmpty)
            return false;

        // The channel is unified, so its unread tail can include wake-up signals as
        // well as batches. For the non-idempotent acks=all workload this guard targets,
        // conservatively defer until the single reader can classify the tail. Idempotent
        // producers retain their existing ability to shrink under continuous traffic.
        if (hasUnreadEvent)
            return true;

        foreach (var (topicPartition, _) in _mutedPartitions)
        {
            if ((carryOver.Partitions.TryGetValue(topicPartition, out var carryOverQueue)
                 && carryOverQueue.Count > 0)
                || _accumulator.HasQueuedBatches(topicPartition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Applies a scale-up by expanding send-loop arrays and updating the connection count.
    /// Returns the new connection count.
    /// </summary>
    private int ApplyScaleUp(int actualCount)
    {
        // A shared pool's group can exceed this sender's ceiling (another producer with a
        // higher MaxConnectionsPerBroker may have grown it) — clamp to our array capacity.
        actualCount = Math.Min(actualCount, _pendingResponsesByConnection.Length);

        var oldCount = _connectionCount;
        _hasScaledConnectionGroup = true;
        _connectionCount = actualCount;
        _totalMaxInFlight = _connectionCount * _maxInFlight;
        UpdateInFlightByteBudget(_connectionCount);
        // Only reset on successful growth — intentional. If the pool returned fewer
        // connections than requested, stale pressure keeps accumulating so the next
        // cooldown window retries with the full delta rather than starting from zero.
        _lastPressureSnapshot = _accumulator.BufferPressureEvents;
        _lastSendLoopPressureSnapshot = _sendLoopPressureEvents;
        _lastUnackedBlockSnapshot = _unackedBudget?.AdmissionBlockEvents ?? 0;

        // Per-connection arrays are pre-sized at the scaling ceiling — nothing to grow.

        // Reset low utilization tracking — we just scaled up
        _lowUtilizationStartTicks = 0;

        // Ratchet serialization buffer pool to cover concurrent request serialization
        // across this broker's new connection count.
        DekafPools.RatchetSerializationBucketCapacity(
            actualCount * PoolSizing.SerializationArraysPerConnection);

        FenceAffectedPartitions(oldCount, actualCount);

        _accumulator.RecordConnectionScaleEvent(
            _brokerId,
            oldCount,
            actualCount,
            _pendingScaleBufferUtilization,
            _pendingScaleBufferPressureDelta,
            _pendingScaleSendLoopPressureDelta);

        LogAdaptiveScaleUp(_brokerId, oldCount, actualCount);
        return actualCount;
    }

    /// <summary>
    /// Finalizes a scale-down after the background pool shrink completes. The routing
    /// width (<see cref="_connectionCount"/>) was already reduced when the shrink was
    /// initiated, so no request has been able to target the removed slot since then and
    /// its pending list is expected to be empty. The removed connection is parked as
    /// retiring; <see cref="MaybeDrainAndDisposeConnection"/> promotes it immediately when
    /// the slot is empty, or during Phase 0 after an unexpected pending response completes.
    /// Should a request ever be found on the removed slot (invariant breach), it is logged
    /// and the connection stays alive until the response completes: the response/timeout
    /// scans iterate the full array, so nothing is dropped. Returns the (already reduced)
    /// connection count.
    /// </summary>
    private int ApplyScaleDown(IKafkaConnection removedConnection)
    {
        var removedIdx = _connectionCount; // Width was reduced at initiation — removed slot is one past it.

        // Reset the sustained-low-utilization timer so the next scale-down cycle
        // must observe the full sustained window from this point forward.
        _lowUtilizationStartTicks = 0;

        var pendingOnRemoved = _pendingResponsesByConnection[removedIdx].Count;
        if (pendingOnRemoved > 0)
            LogScaleDownSlotNotEmpty(_brokerId, removedIdx, pendingOnRemoved);

        // Safe to set without checking the previous value: a parked retiring connection
        // blocks all new scale events, so ApplyScaleDown is never called while a prior
        // retirement is in progress.
        _retiringConnection = removedConnection;
        MaybeDrainAndDisposeConnection();

        LogAdaptiveScaleDown(_brokerId, removedIdx + 1, _connectionCount);
        return _connectionCount;
    }

    /// <summary>
    /// Polls the draining connection and disposes it when ready.
    /// Non-blocking: the connection is disposed on a background task if not yet complete.
    /// </summary>
    private void MaybeDrainAndDisposeConnection()
    {
        // Promote the retiring connection (removed from the pool by scale-down) to
        // draining once its slot's pending list is confirmed empty — its slot index is
        // _connectionCount, frozen while it is parked because a parked connection blocks
        // all new scale events. Deferred while another connection is draining — retried
        // next iteration.
        if (_retiringConnection is not null
            && _drainingConnection is null
            && _pendingResponsesByConnection[_connectionCount].Count == 0)
        {
            _pinnedConnections[_connectionCount] = null;
            _drainingConnection = _retiringConnection;
            _retiringConnection = null;
        }

        if (_drainingConnection is null)
            return;

        var connection = _drainingConnection;
        _drainingConnection = null;

        // This sender's pending list is empty. The shared retirement helper also waits for
        // any leases or operations held by other pool users before disposing in the background.
        StartRetirementDrain(connection);
    }

    private void StartRetirementDrain(IKafkaConnection connection)
    {
        var drainTask = RetiredConnectionDisposer.DrainAndDisposeAsync(
            connection,
            CancellationToken.None).AsTask();
        _retirementDrainTasks.TryAdd(drainTask, 0);
        _ = drainTask.ContinueWith(
            static (task, state) => ((BrokerSender)state!).ObserveCompletedRetirementDrain(task),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ObserveCompletedRetirementDrain(Task task)
    {
        _retirementDrainTasks.TryRemove(task, out _);
        _ = task.Exception;
    }

    /// <summary>
    /// Computes the target connection count for a scale-up based on pressure severity.
    /// Each pressure-threshold multiple suggests one extra connection, capped at
    /// <see cref="MaxScaleStep"/> to avoid over-provisioning from a brief spike.
    /// </summary>
    internal static int ComputeScaleTarget(long pressureDelta, int currentConnections, int maxConnections)
    {
        // Caller guarantees pressureDelta >= ScalePressureDeltaThreshold, so step >= 1.
        Debug.Assert(pressureDelta >= ScalePressureDeltaThreshold,
            $"ComputeScaleTarget called with pressureDelta {pressureDelta} < threshold {ScalePressureDeltaThreshold}");
        var step = (int)Math.Min(pressureDelta / ScalePressureDeltaThreshold, MaxScaleStep);
        return Math.Min(currentConnections + step, maxConnections);
    }

    internal static long ComputeScalePressureDelta(long bufferPressureDelta, long sendLoopPressureDelta) =>
        Math.Max(bufferPressureDelta, sendLoopPressureDelta);

    internal static bool IsPartitionPressureScaleUseful(
        bool adaptiveScalingEnabled,
        int connectionCount,
        int maxConnectionsPerBroker,
        int knownPartitionCount) =>
        adaptiveScalingEnabled
        && connectionCount < maxConnectionsPerBroker
        && knownPartitionCount > connectionCount;

    #region Logging

    [LoggerMessage(Level = LogLevel.Warning, Message = "[BrokerSender] Epoch bump failed for stale epoch {Epoch}, will retry next iteration")]
    private partial void LogEpochBumpFailed(Exception ex, int epoch);

    [LoggerMessage(Level = LogLevel.Error, Message = "BrokerSender[{BrokerId}] send loop failed")]
    private partial void LogSendLoopFailed(Exception ex, int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender[{BrokerId}] send loop exited unexpectedly — redelivering surviving batches to a replacement sender")]
    private partial void LogSendLoopExitRedelivery(int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender[{BrokerId}] scale-down found {PendingCount} pending responses on removed slot {SlotIndex} — width-reduction invariant breached; deferring disposal until drained")]
    private partial void LogScaleDownSlotNotEmpty(int brokerId, int slotIndex, int pendingCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "BrokerSender[{BrokerId}] response failed")]
    private partial void LogResponseFailed(Exception ex, int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BS#{InstanceId}[{Topic}-{Partition}] No response (response has {ResponseCount} entries: [{ResponseKeys}], request had {RequestBatchCount} batches, task={TaskId}, trace={DiagTrace})")]
    private partial void LogNoResponseForPartition(int instanceId, string topic, int partition, int responseCount, string responseKeys, int requestBatchCount, int taskId, string diagTrace);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] DuplicateSequenceNumber for {Topic}-{Partition} at offset {Offset}")]
    private partial void LogDuplicateSequenceNumber(string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BS#{InstanceId}[{BrokerId}] pipelined task={TaskId}: {BatchCount} batches [{PipelinedPartitions}]")]
    private partial void LogPendingResponseCreated(int instanceId, int brokerId, int taskId, int batchCount, string pipelinedPartitions);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BS#{InstanceId}[{BrokerId}] response task={TaskId}: {BatchCount} batches expected [{BatchKeys}], {ResponseCount} received [{ResponseKeys}]")]
    private partial void LogProduceResponseProcessed(int instanceId, int brokerId, int taskId, int batchCount, string batchKeys, int responseCount, string responseKeys);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retriable error {ErrorCode} for {Topic}-{Partition} seq={Seq} count={Count} epoch={Epoch} pid={Pid}")]
    private partial void LogRetriableError(ErrorCode errorCode, string topic, int partition, int seq, int count, short epoch, long pid);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] {ErrorCode} for {Topic}-{Partition} seq={Seq}, signaling epoch bump to send loop")]
    private partial void LogEpochBumpSignaled(ErrorCode errorCode, string topic, int partition, int seq);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] OOSN for {Topic}-{Partition} seq={Seq}, re-enqueueing (transactional)")]
    private partial void LogOosnTransactionalReenqueue(string topic, int partition, int seq);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] Retriable error {ErrorCode} for {Topic}-{Partition}, retrying after {BackoffMs}ms")]
    private partial void LogRetriableErrorWithBackoff(ErrorCode errorCode, string topic, int partition, int backoffMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[BrokerSender] Retriable error {ErrorCode} for {Topic}-{Partition}, inline leader update accepted; retrying without backoff")]
    private partial void LogRetriableErrorWithoutBackoff(ErrorCode errorCode, string topic, int partition);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "BrokerSender[{BrokerId}] adaptive scale-up: {OldCount} -> {NewCount} connections")]
    private partial void LogAdaptiveScaleUp(int brokerId, int oldCount, int newCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender[{BrokerId}] adaptive scale-up failed (current: {CurrentCount} connections)")]
    private partial void LogAdaptiveScaleFailed(Exception exception, int brokerId, int currentCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "BrokerSender[{BrokerId}] adaptive scale-down: {OldCount} -> {NewCount} connections")]
    private partial void LogAdaptiveScaleDown(int brokerId, int oldCount, int newCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender[{BrokerId}] adaptive scale-down failed (current: {CurrentCount} connections)")]
    private partial void LogAdaptiveScaleDownFailed(Exception exception, int brokerId, int currentCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] partition migration: {MigratingCount} partitions fenced during scale {OldCount} -> {NewCount}")]
    private partial void LogPartitionMigrationStarted(int brokerId, int migratingCount, int oldCount, int newCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BrokerSender[{BrokerId}] partition migration complete: {MigratedCount} partitions migrated")]
    private partial void LogPartitionMigrationComplete(int brokerId, int migratedCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BS#{InstanceId} response task={ResponseTaskId}: batch already returned to pool (count={Count}, trace={DiagTrace}), skipping")]
    private partial void LogBatchAlreadyReturnedToPool(int instanceId, int responseTaskId, int count, string diagTrace);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BS#{InstanceId} broker {BrokerId}: send-failed retry batch was recycled while queued (generation mismatch), skipping")]
    private partial void LogStaleRetryBatchSkipped(int instanceId, int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BS#{InstanceId} broker {BrokerId}: batch recycled during send (generation mismatch), skipping completion")]
    private partial void LogStaleBatchInSendSkipped(int instanceId, int brokerId);

    #endregion
}
