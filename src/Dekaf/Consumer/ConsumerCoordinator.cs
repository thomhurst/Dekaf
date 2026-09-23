using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Retry;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Microsoft.Extensions.Logging;
#if !NET9_0_OR_GREATER
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Consumer;

/// <summary>
/// Handles consumer group coordination using the KIP-848 protocol (ConsumerGroupHeartbeat API).
/// State machine: Unjoined → Joining → Stable. There is no Syncing phase — the broker
/// performs partition assignment server-side and returns it directly in the heartbeat response.
/// </summary>
public sealed partial class ConsumerCoordinator : IAsyncDisposable
{
    private readonly ConsumerOptions _options;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly IRebalanceListener? _rebalanceListener;
    private readonly IConsumerAwareRebalanceListener? _consumerAwareRebalanceListener;
    private readonly IRebalanceListener[]? _additionalRebalanceListeners;
    // Retained for the public six-parameter constructor and direct coordinator callers.
    // KafkaConsumer uses the pre-publication hook below to invalidate buffered fetches.
    private readonly Action<IReadOnlyList<TopicPartition>>? _onPartitionsRevoked;
    // Heartbeats can revoke and reassign a partition between poll-loop snapshots.
    // Signal before publication to defeat assignment ABA (A -> B -> A with the same final set).
    private readonly Action<IReadOnlyList<TopicPartition>>? _onPartitionsRevoking;
    // Runs after assignment publication but before user revoke callbacks and the next heartbeat.
    // Assignment snapshots wait for this hook so KafkaConsumer cannot discard dirty revoked offsets first.
    private readonly Func<IReadOnlyList<TopicPartition>, CancellationToken, ValueTask>?
        _onPartitionsRevokedAsync;
    private readonly Func<
        IEnumerable<TopicPartition>,
        IEnumerable<TopicPartition>,
        IRebalanceConsumerScope>? _createRebalanceConsumerScope;
    private readonly ConcurrentQueue<TopicPartition> _revokedPartitionsSinceLastSync = new();
    private Task _pendingRevocationCommit = Task.CompletedTask;
    // Completes once the callbacks of the latest published assignment change with newly assigned
    // partitions have been delivered. The consumer does not synchronize that assignment before
    // then, so an OnPartitionsAssigned that seeks is applied before fetching starts.
    private Task _pendingAssignmentCallbacks = Task.CompletedTask;
    // Assignment publication and revocation history form one snapshot. Rebalance callbacks
    // run only after this lock is released so user code cannot extend its critical section.
    private readonly Lock _assignmentStateLock = new();
    private IRebalanceListener? _runtimeRebalanceListener;
    private readonly ILogger _logger;

    private volatile int _coordinatorId = -1;
    private volatile string? _memberId;
    private volatile int _generationId = -1;
    private int _assignmentVersion;
    private int _assignmentProcessingCount;
    // Volatile ensures cross-thread visibility of the reference. Thread-safety relies on
    // all writes replacing the reference entirely (never in-place mutation) — verified at
    // every assignment site: ProcessConsumerGroupAssignment() and DisposeAsync().
    private volatile HashSet<TopicPartition> _assignedPartitions = [];
    private volatile HashSet<TopicPartition> _newlyExpandedPartitions = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    // Serializes user rebalance callbacks without holding the coordinator state lock. This
    // permits callbacks to re-enter consumer APIs while preserving assigned-before-lost order.
    private readonly SemaphoreSlim _rebalanceListenerLock = new(1, 1);
    private readonly object _heartbeatGuard = new();
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    // Pooled dictionaries to avoid allocations in hot paths (protected by _lock or method-local usage)
    private readonly Dictionary<string, List<OffsetCommitRequestPartition>> _commitTopicGroups = new();
    private readonly SemaphoreSlim _commitLock = new(1, 1);
    private readonly Dictionary<string, List<int>> _fetchTopicGroups = new();
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    private volatile CoordinatorState _state = CoordinatorState.Unjoined;
    private KafkaException? _fatalHeartbeatException;
    private long _lastSuccessfulHeartbeatTimestamp;
    private string? _lastHeartbeatFailure;
    private int _disposed;
    // Set by BeginClose; no join starts after it.
    private int _closing;
    // Set when disposal has finished with rebalance callbacks. A drain that outlives it (a
    // listener that ignored the teardown token) invokes no further callbacks.
    private int _callbackDeliveryClosed;
    private readonly Func<int> _getCoordinationConnectionIndex;
    // Where the next coordinator lookup starts. It rests on the last broker that answered, so a
    // broker that refuses or black-holes connections is not tried first by every lookup.
    private int _coordinatorLookupCursor;
    private readonly Func<bool> _isDisposed;

    // KIP-848 consumer protocol state
    private volatile int _heartbeatIntervalMs;
    private readonly long _maxPollIntervalStopwatchTicks;
    private long _lastPollTimestamp;
    private long _pollVersion;
    private long _maxPollExpiredAtPollVersion = -1;
    private long _maxPollExpirationVersion;
    private int _maxPollLossNotificationPending;
    // Set when the coordinator fences this member (FENCED_MEMBER_EPOCH or UNKNOWN_MEMBER_ID).
    // Commits are rejected locally until the member has rejoined and the consumer has
    // synchronized the assignment (AcknowledgeAssignmentSync): its partitions are lost, and until
    // the consumer drops the offsets it stored for them, a commit under the new epoch could move
    // another member's committed offsets backwards.
    private int _membershipFenced;
    // Rebalance callbacks not yet delivered, in publication order: assignments a fence cleared,
    // waiting for OnPartitionsLost, and assignment changes, reserved under the state lock when
    // they are published. Drained under _rebalanceListenerLock so a rejoin can never publish
    // OnPartitionsAssigned first.
    private readonly ConcurrentQueue<PendingRebalanceCallback> _pendingRebalanceCallbacks = new();
    // Entries no publisher is delivering: fence losses, and reserved assignment changes whose
    // publisher's delivery ended without them (cancellation). The stable poll path checks it with
    // one volatile read; it leaves an assignment change its publisher is still delivering alone,
    // so steady-heartbeat callbacks do not suppress buffered polls.
    private int _pendingRebalanceCallbackCount;
    // Makes counting and enqueueing an entry one step for anyone who sees the count but not yet
    // the entry: publishers count and enqueue under it, and such a reader takes it to wait for the
    // publisher to finish. Never held while anything else is acquired.
    private readonly object _pendingPublishLock = new();
    // The drain the current async flow is running in. A listener that re-enters the coordinator
    // (a poll or join from inside its callback) must not wait for the drain it is running in: its
    // own entry stays queued until it returns. The scope is deactivated when the drain ends, so a
    // task the listener started, which inherits the value, drains normally afterwards.
    private static readonly AsyncLocal<DrainScope?> s_drainScope = new();
    // Changes under _lock each time a join publishes a new membership and each time a fence ends
    // one. A fence observed by a request sent under an earlier membership must not clear the
    // assignment of a newer one, and a commit must not send offsets taken under an earlier one.
    // Seqlock-style: odd while a join is publishing (from before the new member id and epoch are
    // written until the new assignment is published), even otherwise; a fence adds 2. A commit
    // captures only an even value (MembershipVersion waits out a publication), so its snapshot
    // is never taken half-way through one.
    private int _membershipVersion;
    // The coordinator whose join publication this thread is running (the odd-version section
    // is synchronous). A hook it calls that starts a commit must not wait for it.
    [ThreadStatic]
    private static ConsumerCoordinator? t_publishingCoordinator;
    private static readonly long MembershipPublicationWaitTicks = Stopwatch.Frequency / 10;
    // Foreground assignment initialization and fetch waits are application poll activity. Track
    // concurrent callers without allocating a scope object on each poll cycle.
    private int _foregroundPollActivityCount;
    private int _subscriptionChanged; // 0 = false, 1 = true; use Interlocked.Exchange for atomic snapshot
    private volatile StringSet? _subscribedTopics;
    private volatile string? _subscribedTopicRegex;
    private IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? _cachedOwnedTopicPartitions;
    private int _cachedOwnedTopicPartitionsVersion = -1;
    private int _sentOwnedTopicPartitionsVersion = -1;

    internal static int GetCoordinationConnectionIndex(int connectionsPerBroker)
        => connectionsPerBroker - 1;

    public ConsumerCoordinator(
        ConsumerOptions options,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILogger<ConsumerCoordinator>? logger = null,
        Func<int>? getConnectionCount = null,
        Action<IReadOnlyList<TopicPartition>>? onPartitionsRevoked = null)
        : this(
            options,
            connectionPool,
            metadataManager,
            logger,
            getConnectionCount,
            onPartitionsRevoked,
            onPartitionsRevoking: null,
            onPartitionsRevokedAsync: null)
    {
    }

    internal ConsumerCoordinator(
        ConsumerOptions options,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILogger<ConsumerCoordinator>? logger,
        Func<int>? getConnectionCount,
        Action<IReadOnlyList<TopicPartition>>? onPartitionsRevoked,
        Action<IReadOnlyList<TopicPartition>>? onPartitionsRevoking,
        Func<IReadOnlyList<TopicPartition>, CancellationToken, ValueTask>? onPartitionsRevokedAsync = null,
        Func<IEnumerable<TopicPartition>, IEnumerable<TopicPartition>, IRebalanceConsumerScope>?
            createRebalanceConsumerScope = null)
    {
        _options = options;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _consumerAwareRebalanceListener = options.ConsumerAwareRebalanceListener;
        _rebalanceListener = _consumerAwareRebalanceListener is null
            ? options.RebalanceListener
            : null;
        _additionalRebalanceListeners = options.AdditionalRebalanceListeners;
        _onPartitionsRevoked = onPartitionsRevoked;
        _onPartitionsRevoking = onPartitionsRevoking;
        _onPartitionsRevokedAsync = onPartitionsRevokedAsync;
        _createRebalanceConsumerScope = createRebalanceConsumerScope;
        if (_consumerAwareRebalanceListener is not null && _createRebalanceConsumerScope is null)
        {
            throw new ArgumentException(
                "Consumer-aware rebalance listeners require a KafkaConsumer-owned coordinator.",
                nameof(options));
        }
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConsumerCoordinator>.Instance;
        _getCoordinationConnectionIndex = getConnectionCount is not null
            ? () => GetCoordinationConnectionIndex(getConnectionCount())
            : () => GetCoordinationConnectionIndex(options.ConnectionsPerBroker);
        _isDisposed = () => Volatile.Read(ref _disposed) != 0;
        _heartbeatIntervalMs = options.HeartbeatIntervalMs;
        _maxPollIntervalStopwatchTicks = options.MaxPollIntervalMs * Stopwatch.Frequency / 1000;
        _lastPollTimestamp = Stopwatch.GetTimestamp();
    }

    // A push samples membership without taking the coordination lock. Suppress identities
    // during joining, fencing, disposal, or an observed epoch/identity transition.
    internal string? CaptureTelemetryMemberId()
    {
        var epoch = _generationId;
        var memberId = _memberId;
        return epoch > 0 && _state == CoordinatorState.Stable &&
            Volatile.Read(ref _disposed) == 0 && _generationId == epoch &&
            ReferenceEquals(memberId, _memberId)
                ? memberId
                : null;
    }

    public string? MemberId => _memberId;
    public int GenerationId => _generationId;
    public CoordinatorState State => _state;
    public TopicPartitionSet Assignment => _assignedPartitions;
    internal int AssignmentVersion => Volatile.Read(ref _assignmentVersion);

    internal ConsumerGroupLiveness CaptureGroupLiveness(bool isStopped, bool hasConsumerGroup)
    {
        var lastHeartbeatTimestamp = Volatile.Read(ref _lastSuccessfulHeartbeatTimestamp);
        return new ConsumerGroupLiveness(
            HasConsumerGroup: hasConsumerGroup,
            IsJoined: hasConsumerGroup && _state == CoordinatorState.Stable,
            IsStopped: isStopped || Volatile.Read(ref _disposed) != 0,
            TimeSinceLastHeartbeat: lastHeartbeatTimestamp == 0
                ? null
                : Stopwatch.GetElapsedTime(lastHeartbeatTimestamp),
            HeartbeatInterval: TimeSpan.FromMilliseconds(Math.Max(_heartbeatIntervalMs, 1)),
            LastHeartbeatFailure: Volatile.Read(ref _lastHeartbeatFailure));
    }

    internal ConsumerGroupStatus CaptureGroupStatus()
    {
        var assignment = _assignedPartitions;
        var lastHeartbeatTimestamp = Volatile.Read(ref _lastSuccessfulHeartbeatTimestamp);
        return new ConsumerGroupStatus
        {
            HasConsumerGroup = true,
            State = _state,
            CoordinatorId = _coordinatorId,
            MemberId = _memberId,
            GenerationOrMemberEpoch = _generationId,
            HeartbeatInterval = TimeSpan.FromMilliseconds(Math.Max(_heartbeatIntervalMs, 1)),
            TimeSinceLastHeartbeat = lastHeartbeatTimestamp == 0
                ? null
                : Stopwatch.GetElapsedTime(lastHeartbeatTimestamp),
            LastHeartbeatFailure = Volatile.Read(ref _lastHeartbeatFailure),
            Assignment = KafkaClientStatusFactory.CopyAssignment(assignment, assignment.Count)
        };
    }

    internal ValueTask RecordPollAsync(CancellationToken cancellationToken)
    {
        return TryRecordPollFast()
            ? ValueTask.CompletedTask
            : ExpireAndRecordPollAsync(cancellationToken);
    }

    private void RecordPollIfLossNotificationComplete(long timestamp)
    {
        if (Volatile.Read(ref _maxPollLossNotificationPending) != 0)
            return;

        RecordPoll(timestamp);
    }

    private async ValueTask ExpireAndRecordPollAsync(CancellationToken cancellationToken)
    {
        var expiration = await TryExpireMaxPollIntervalAsync(cancellationToken).ConfigureAwait(false);
        if (expiration.Expired)
            await CompleteMaxPollExpirationAsync(expiration.Lost).ConfigureAwait(false);

        // Another overdue foreground poll may have won _lock and started loss callbacks
        // after this call passed RecordPollAsync's initial guard. Its expired generation
        // must remain unchanged until those callbacks finish.
        if (Volatile.Read(ref _maxPollLossNotificationPending) != 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        // Advance only after loss callbacks so prefetch cannot rejoin during notification.
        RecordPoll(Stopwatch.GetTimestamp());
    }

    private void RecordPoll(long timestamp)
    {
        Volatile.Write(ref _lastPollTimestamp, timestamp);
        Interlocked.Increment(ref _pollVersion);
    }

    internal void BeginForegroundPollActivity()
    {
        Interlocked.Increment(ref _foregroundPollActivityCount);
        RefreshPollDeadline();
    }

    internal void EndForegroundPollActivity()
    {
        RefreshPollDeadline();
        Interlocked.Decrement(ref _foregroundPollActivityCount);
    }

    private void RefreshPollDeadline() => Volatile.Write(ref _lastPollTimestamp, Stopwatch.GetTimestamp());

    private void ThrowIfMaxPollIntervalExpired()
    {
        if (Volatile.Read(ref _membershipFenced) != 0)
        {
            throw new GroupException(
                ErrorCode.FencedMemberEpoch,
                "Offset commit rejected because the group coordinator fenced this member; its partitions were lost and " +
                "the consumer has not rejoined the group yet; poll to rejoin before committing")
            {
                GroupId = _options.GroupId
            };
        }

        if (Volatile.Read(ref _maxPollExpiredAtPollVersion) < 0
            && !IsCurrentPollGenerationExpired())
            return;

        throw new GroupException(
            ErrorCode.FencedMemberEpoch,
            $"Offset commit rejected because maximum poll interval of {_options.MaxPollIntervalMs}ms was exceeded")
        {
            GroupId = _options.GroupId
        };
    }

    /// <summary>
    /// Rejects a commit whose offsets were taken under a membership that a fence or rejoin has
    /// since replaced. Its request would otherwise carry the new member id and epoch, and a
    /// fence lifted by the rejoin's assignment sync no longer stops it.
    /// </summary>
    /// <summary>
    /// Reads the member id and epoch as one consistent pair with the membership version, the
    /// read side of the seqlock. Every change of membership (join publication, fence, leave)
    /// makes the version odd before it touches the identity and even again afterwards, so an
    /// identity read between two reads of the unchanged, even version the commit captured
    /// belongs to that membership. Otherwise the commit is rejected. The epoch alone may still
    /// move within a membership (a steady heartbeat's refresh), as the StaleMemberEpoch retry
    /// expects.
    /// </summary>
    private void ReadCommitIdentity(int membershipVersion, out string? memberId, out int memberEpoch)
    {
        ThrowIfMembershipChangedSince(membershipVersion);
        memberId = _memberId;
        memberEpoch = _generationId;
        ThrowIfMembershipChangedSince(membershipVersion);
    }

    /// <summary>
    /// Opens a membership change: the version turns odd, and turns even again when the returned
    /// scope is disposed. The change is synchronous and runs under the state lock; the thread is
    /// marked so a hook it runs that starts a commit is rejected instead of waiting for it.
    /// </summary>
    private MembershipChangeScope BeginMembershipChange()
    {
        Interlocked.Increment(ref _membershipVersion);
        var previous = t_publishingCoordinator;
        t_publishingCoordinator = this;
        return new MembershipChangeScope(this, previous);
    }

    private readonly struct MembershipChangeScope(
        ConsumerCoordinator coordinator,
        ConsumerCoordinator? previousPublisher) : IDisposable
    {
        public void Dispose()
        {
            Interlocked.Increment(ref coordinator._membershipVersion);
            t_publishingCoordinator = previousPublisher;
        }
    }

    private void ThrowIfMembershipChangedSince(int membershipVersion)
    {
        if (Volatile.Read(ref _membershipVersion) == membershipVersion)
            return;

        throw new GroupException(
            ErrorCode.FencedMemberEpoch,
            "Offset commit rejected because the group membership changed while the commit was in progress; its " +
            "offsets may belong to partitions this member no longer owns; poll before committing again")
        {
            GroupId = _options.GroupId
        };
    }

    private bool IsCurrentPollGenerationExpired() => IsCurrentPollGenerationExpired(Stopwatch.GetTimestamp());

    private bool IsCurrentPollGenerationExpired(long timestamp)
    {
        if (Volatile.Read(ref _maxPollExpiredAtPollVersion) == Volatile.Read(ref _pollVersion))
            return true;

        if (Volatile.Read(ref _foregroundPollActivityCount) != 0)
            return false;

        return _state == CoordinatorState.Stable
            && timestamp - Volatile.Read(ref _lastPollTimestamp) >= _maxPollIntervalStopwatchTicks;
    }

    internal async ValueTask<(
        TopicPartitionSet Assignment,
        int Version,
        HashSet<TopicPartition>? Revocations,
        HashSet<TopicPartition> NewlyExpandedPartitions)>
        GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task pendingRevocationCommit;
            lock (_assignmentStateLock)
            {
                pendingRevocationCommit = _pendingRevocationCommit;
                if (pendingRevocationCommit.IsCompleted)
                {
                    HashSet<TopicPartition>? revoked = null;
                    while (_revokedPartitionsSinceLastSync.TryDequeue(out var partition))
                    {
                        (revoked ??= []).Add(partition);
                    }

                    return (
                        _assignedPartitions,
                        Volatile.Read(ref _assignmentVersion),
                        revoked,
                        _newlyExpandedPartitions);
                }
            }

            await pendingRevocationCommit.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal void RestoreRevokedPartitionsSinceLastSync(HashSet<TopicPartition> revoked)
    {
        lock (_assignmentStateLock)
        {
            EnqueueRevokedPartitions(revoked);
        }
    }

    internal void AcknowledgeInitializedPartitions(
        IReadOnlyCollection<TopicPartition> initializedPartitions,
        int assignmentVersion)
    {
        if (initializedPartitions.Count == 0)
            return;

        lock (_assignmentStateLock)
        {
            // Position initialization performs asynchronous OffsetFetch/ListOffsets I/O.
            // A newer assignment or classification must not be acknowledged by work that
            // used an older snapshot; the next assignment-sync pass will apply it instead.
            if (Volatile.Read(ref _assignmentVersion) != assignmentVersion)
                return;

            HashSet<TopicPartition>? remaining = null;
            foreach (var partition in initializedPartitions)
            {
                if (!_newlyExpandedPartitions.Contains(partition))
                    continue;

                remaining ??= new HashSet<TopicPartition>(_newlyExpandedPartitions);
                remaining.Remove(partition);
            }

            if (remaining is not null)
                _newlyExpandedPartitions = remaining;
        }
    }

    internal void AcknowledgeAssignmentSync(int assignmentVersion)
    {
        if (Volatile.Read(ref _maxPollExpiredAtPollVersion) < 0
            && Volatile.Read(ref _membershipFenced) == 0
            && _revokedPartitionsSinceLastSync.IsEmpty)
        {
            return;
        }

        lock (_assignmentStateLock)
        {
            if (Volatile.Read(ref _assignmentVersion) != assignmentVersion
                || !_revokedPartitionsSinceLastSync.IsEmpty)
            {
                return;
            }

            // The member has rejoined and the consumer has synchronized its assignment, dropping
            // the positions and stored offsets of the partitions the fence took away. Lifting the
            // fence on the join alone would let a commit made before this sync send those stale
            // offsets under the new epoch.
            if (_state == CoordinatorState.Stable)
                Volatile.Write(ref _membershipFenced, 0);

            if (Volatile.Read(ref _maxPollExpiredAtPollVersion) == Volatile.Read(ref _pollVersion))
                return;

            Volatile.Write(ref _maxPollExpiredAtPollVersion, -1);
        }
    }

    internal bool IsAssignmentSyncCurrent(int assignmentVersion) =>
        _state == CoordinatorState.Stable
        && Volatile.Read(ref _assignmentVersion) == assignmentVersion
        && Volatile.Read(ref _assignmentProcessingCount) == 0
        && _revokedPartitionsSinceLastSync.IsEmpty
        && Volatile.Read(ref _fatalHeartbeatException) is null
        && Volatile.Read(ref _maxPollExpiredAtPollVersion) < 0;

    internal bool TryRecordPollFast() => TryRecordPollFast(out _);

    /// <summary>
    /// Records a poll without taking the coordinator lock. <paramref name="timestamp"/>
    /// is the <see cref="Stopwatch.GetTimestamp"/> value this call read, or 0 when an
    /// early-out branch never needed one — callers can reuse it to avoid a second read.
    /// </summary>
    internal bool TryRecordPollFast(out long timestamp)
    {
        // The fatal can be published after the assignment-currency gate. Surface it here,
        // before either a buffered dequeue or the timeout-bound coordinator lock path.
        ThrowIfFatalHeartbeatException();

        timestamp = 0;

        // Heartbeat expiry invokes user callbacks outside _lock. Keep its poll generation
        // unchanged so EnsureActiveGroup cannot rejoin until notification completes.
        if (Volatile.Read(ref _maxPollLossNotificationPending) != 0)
            return true;

        if (Volatile.Read(ref _foregroundPollActivityCount) != 0)
        {
            RefreshPollDeadline();
            return true;
        }

        // One timestamp read serves the expiry check, the poll record, and the caller.
        timestamp = Stopwatch.GetTimestamp();
        if (IsCurrentPollGenerationExpired(timestamp))
            return false;

        RecordPollIfLossNotificationComplete(timestamp);
        return true;
    }

    private void EnqueueRevokedPartitions(IEnumerable<TopicPartition> revoked)
    {
        foreach (var partition in revoked)
            _revokedPartitionsSinceLastSync.Enqueue(partition);
    }

    internal IDisposable RegisterRuntimeRebalanceListener(IRebalanceListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        if (Interlocked.CompareExchange(ref _runtimeRebalanceListener, listener, null) is not null)
            throw new InvalidOperationException("A partitioned runtime rebalance listener is already registered.");

        return new RuntimeRebalanceListenerRegistration(this, listener);
    }

    private void UnregisterRuntimeRebalanceListener(IRebalanceListener listener)
    {
        Interlocked.CompareExchange(ref _runtimeRebalanceListener, null, listener);
    }

    private sealed class RuntimeRebalanceListenerRegistration(
        ConsumerCoordinator coordinator,
        IRebalanceListener listener) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                coordinator.UnregisterRuntimeRebalanceListener(listener);
        }
    }

    private static void RemoveEmptyTopicGroups<TItem>(Dictionary<string, List<TItem>> topicGroups)
    {
        string[]? emptyTopics = null;
        var emptyTopicCount = 0;

        foreach (var kvp in topicGroups)
        {
            if (kvp.Value.Count != 0)
                continue;

            emptyTopics ??= new string[topicGroups.Count];
            emptyTopics[emptyTopicCount++] = kvp.Key;
        }

        if (emptyTopics is null)
            return;

        for (var i = 0; i < emptyTopicCount; i++)
        {
            topicGroups.Remove(emptyTopics[i]);
        }
    }

    /// <summary>
    /// Forces the coordinator to rejoin the group on the next
    /// <see cref="EnsureActiveGroupAsync(StringSet, CancellationToken)"/> call by transitioning to <see cref="CoordinatorState.Unjoined"/>.
    /// </summary>
    /// <summary>
    /// True while the latest published assignment's OnPartitionsAssigned has not been delivered.
    /// A listener's own flow (a poll from inside its callback) is never held back by it.
    /// </summary>
    internal bool HasPendingAssignmentCallbacks() =>
        !Volatile.Read(ref _pendingAssignmentCallbacks).IsCompleted && !IsInsideOwnRebalanceCallback();

    /// <summary>
    /// Waits until the latest published assignment's callbacks have been delivered, so the
    /// consumer synchronizes the assignment only after OnPartitionsAssigned (and any seek it
    /// staged) has run. Completes at once in the steady state and inside a listener's callback.
    /// </summary>
    internal ValueTask WaitForAssignmentCallbacksAsync(CancellationToken cancellationToken)
    {
        var pending = Volatile.Read(ref _pendingAssignmentCallbacks);
        return pending.IsCompleted || IsInsideOwnRebalanceCallback()
            ? default
            : new ValueTask(pending.WaitAsync(cancellationToken));
    }

    internal void RequestRejoin()
    {
        _state = CoordinatorState.Unjoined;
    }

    /// <summary>
    /// Called when the owning consumer starts closing. From then on the coordinator never joins
    /// the group again, whichever path asks (a straggling prefetch, an offset-fetch recovery),
    /// so close cannot end with a new membership and a live heartbeat.
    /// </summary>
    internal void BeginClose() => Volatile.Write(ref _closing, 1);

    /// <summary>
    /// Ensures the consumer has joined the group.
    /// </summary>
    public ValueTask EnsureActiveGroupAsync(
        StringSet topics,
        CancellationToken cancellationToken)
        => EnsureActiveGroupAsync(topics, null, cancellationToken);

    public async ValueTask EnsureActiveGroupAsync(
        StringSet topics,
        string? subscribedTopicRegex,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ConsumerCoordinator));

        if (string.IsNullOrEmpty(_options.GroupId))
            return;

        ThrowIfFatalHeartbeatException();

        if (_state == CoordinatorState.Stable)
        {
            // Callbacks cancellation deferred after the member became Stable: no rejoin will
            // retry them, so the next poll delivers them before returning.
            if (Volatile.Read(ref _pendingRebalanceCallbackCount) != 0)
            {
                await InvokePendingRebalanceCallbacksAsync(cancellationToken).ConfigureAwait(false);

                // A fence or expiry that queued one of those callbacks ended the membership: fall
                // through to recovery instead of carrying on as a member.
                if (_state != CoordinatorState.Stable)
                {
                    await EnsureActiveGroupConsumerProtocolAsync(topics, subscribedTopicRegex, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }
            }

            if (SubscriptionMatches(topics, subscribedTopicRegex))
                return;

            if (subscribedTopicRegex is not null)
            {
                using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                    _coordinatorId, _getCoordinationConnectionIndex(), cancellationToken).ConfigureAwait(false);
                EnsureServerSideRegexSupported(connectionLease.Connection, subscribedTopicRegex);
            }
        }

        await EnsureActiveGroupConsumerProtocolAsync(topics, subscribedTopicRegex, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks the coordinator as unknown, forcing re-discovery on next operation.
    /// </summary>
    private void MarkCoordinatorUnknown()
    {
        _coordinatorId = -1;
        _state = CoordinatorState.Unjoined;
    }

    /// <summary>
    /// Returns true if the error code indicates a retriable coordinator error.
    /// </summary>
    private static bool IsRetriableCoordinatorError(ErrorCode? errorCode) =>
        errorCode is ErrorCode.NotCoordinator
            or ErrorCode.CoordinatorNotAvailable
            or ErrorCode.CoordinatorLoadInProgress;

    /// <summary>
    /// Classifies a failed join attempt. Transport and connection-setup failures (a coordinator
    /// that refuses or resets connections, a socket that died mid-request, DNS, setup timeouts)
    /// are retried with backoff until the rebalance timeout, like the fetch path: the coordinator
    /// may be restarting or may have moved. Typed group errors have dedicated handlers, and
    /// broker-version and auth failures are fatal.
    /// </summary>
    /// <remarks>
    /// Does not look at the caller's token: a failure that lands after cancellation must still be
    /// caught so the loop reports <see cref="OperationCanceledException"/> rather than letting a
    /// raw socket exception escape a consumer that is shutting down during an outage. A
    /// connection retired by pool churn is retried only while this coordinator is alive, so the
    /// loop cannot spin under the state lock against a disposed pool.
    /// </remarks>
    private bool IsRetriableJoinFailure(Exception exception) =>
        TransportFailureClassifier.IsRetriable(
            exception,
            TransportRetryPolicy.GroupJoin,
            ownerDisposed: Volatile.Read(ref _disposed) != 0);

    private async ValueTask StoreFatalHeartbeatExceptionAsync(KafkaException exception)
    {
        Interlocked.CompareExchange(ref _fatalHeartbeatException, exception, null);

        try
        {
            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            _state = CoordinatorState.Unjoined;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void ThrowIfFatalHeartbeatException()
    {
        var exception = Interlocked.Exchange(ref _fatalHeartbeatException, null);
        if (exception is not null)
            throw exception;
    }

    private static bool SetEquals(StringSet? current, StringSet next)
    {
        if (ReferenceEquals(current, next))
            return true;

        if (current is null || current.Count != next.Count)
            return false;

        foreach (var topic in next)
        {
            if (!current.Contains(topic))
                return false;
        }

        return true;
    }

    private bool SubscriptionMatches(StringSet topics, string? subscribedTopicRegex)
        => string.Equals(_subscribedTopicRegex, subscribedTopicRegex, StringComparison.Ordinal) &&
           SetEquals(_subscribedTopics, topics);

    private void UpdateSubscription(StringSet topics, string? subscribedTopicRegex)
    {
        if (SubscriptionMatches(topics, subscribedTopicRegex))
            return;

        _subscribedTopics = topics;
        _subscribedTopicRegex = subscribedTopicRegex;
        Interlocked.Exchange(ref _subscriptionChanged, 1);
    }

    private async ValueTask FindCoordinatorAsync(CancellationToken cancellationToken)
    {
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var request = new FindCoordinatorRequest
        {
            Key = _options.GroupId!,
            KeyType = CoordinatorType.Group
        };

        // Retry loop for transient errors (CoordinatorNotAvailable, CoordinatorLoadInProgress)
        const int maxRetries = 5;
        var cursor = Volatile.Read(ref _coordinatorLookupCursor);
        var failedLookups = 0;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Cycle through brokers on retries to avoid wasting all attempts on one
                // unavailable broker. The cursor outlives this call, so the next lookup starts
                // at the broker that answered instead of at a dead first broker again.
                var broker = brokers[(int)((uint)(cursor + attempt) % (uint)brokers.Count)];
                using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                    broker.NodeId,
                    _getCoordinationConnectionIndex(),
                    cancellationToken)
                    .ConfigureAwait(false);
                var connection = connectionLease.Connection;

                // Use negotiated API version
                var findCoordinatorVersion = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    ApiKey.FindCoordinator,
                    FindCoordinatorRequest.LowestSupportedVersion,
                    FindCoordinatorRequest.HighestSupportedVersion);

                var response = await connection.SendWithClientTelemetryAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                    request,
                    findCoordinatorVersion, TelemetryMetricCollector,
                    cancellationToken).ConfigureAwait(false);

                if (response.Coordinators.Count == 0)
                {
                    throw new Errors.GroupException(ErrorCode.CoordinatorNotAvailable,
                        "FindCoordinator returned an empty Coordinators array")
                    { GroupId = _options.GroupId };
                }

                var coordinator = response.Coordinators[0];
                var errorCode = coordinator.ErrorCode;
                var nodeId = coordinator.NodeId;
                var host = coordinator.Host;
                var port = coordinator.Port;

                // Retry on transient coordinator errors
                if (errorCode == ErrorCode.CoordinatorNotAvailable ||
                    errorCode == ErrorCode.CoordinatorLoadInProgress)
                {
                    if (attempt == maxRetries - 1)
                        break;

                    var retryDelayMs = CalculateRequestRetryBackoff(attempt + 1);
                    LogCoordinatorNotAvailableRetry(attempt + 1, maxRetries, retryDelayMs);

                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (errorCode != ErrorCode.None)
                {
                    if (errorCode == ErrorCode.GroupAuthorizationFailed)
                        throw KafkaException.FromErrorCode(errorCode, $"FindCoordinator failed: {errorCode}");

                    throw new Errors.GroupException(errorCode, $"FindCoordinator failed: {errorCode}")
                    {
                        GroupId = _options.GroupId
                    };
                }

                // Route first, then publish the ID: a concurrent reader of _coordinatorId must
                // never find the pool still considers the coordinator unknown.
                _connectionPool.RegisterBroker(nodeId, host, port);
                _coordinatorId = nodeId;
                Volatile.Write(ref _coordinatorLookupCursor, cursor + attempt);

                LogFoundCoordinator(nodeId, _options.GroupId!);
                return;
            }
            catch (Exception ex) when (IsRetriableLookupFailure(ex))
            {
                // A failure that lands after cancellation reports the cancellation, not a raw
                // socket exception.
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt == maxRetries - 1)
                {
                    throw new Errors.GroupException(
                        ErrorCode.CoordinatorNotAvailable,
                        $"FindCoordinator failed after {maxRetries} retries: {ex.Message}",
                        ex)
                    {
                        GroupId = _options.GroupId
                    };
                }

                // Every known broker failed once: the broker list itself may be stale (a
                // broker that left still listed, a replacement not yet known).
                if (++failedLookups % brokers.Count == 0)
                {
                    await RetryHelper.RefreshMetadataForRetryAsync(_metadataManager, cancellationToken)
                        .ConfigureAwait(false);
                    var refreshedBrokers = _metadataManager.Metadata.GetBrokers();
                    if (refreshedBrokers.Count > 0)
                        brokers = refreshedBrokers;
                }

                var retryDelayMs = CalculateRequestRetryBackoff(attempt + 1);
                LogCoordinatorNotAvailableRetry(attempt + 1, maxRetries, retryDelayMs);
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new Errors.GroupException(ErrorCode.CoordinatorNotAvailable,
            $"FindCoordinator failed after {maxRetries} retries: CoordinatorNotAvailable")
        {
            GroupId = _options.GroupId
        };
    }

    private bool IsRetriableLookupFailure(Exception exception) =>
        TransportFailureClassifier.IsRetriable(
            exception,
            TransportRetryPolicy.Request,
            ownerDisposed: Volatile.Read(ref _disposed) != 0);

    private int CalculateRequestRetryBackoff(int failureCount) =>
        ExponentialRetryBackoff.CalculateDelayMilliseconds(
            _options.RetryBackoffMs,
            _options.RetryBackoffMaxMs,
            failureCount);

    internal static TimeSpan GetJoinRetryDelay(
        int retryDelayMs,
        TimeSpan elapsed,
        TimeSpan rebalanceTimeout)
    {
        var remaining = rebalanceTimeout - elapsed;
        return remaining <= TimeSpan.Zero
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(Math.Min(retryDelayMs, remaining.TotalMilliseconds));
    }

    private Task DelayForJoinRetryAsync(
        int failureCount,
        long startedAt,
        TimeSpan rebalanceTimeout,
        CancellationToken cancellationToken) =>
        Task.Delay(
            GetJoinRetryDelay(
                CalculateRequestRetryBackoff(failureCount),
                Stopwatch.GetElapsedTime(startedAt),
                rebalanceTimeout),
            cancellationToken);

    /// <summary>
    /// Serializes heartbeat loop starts to prevent concurrent callers from orphaning a loop.
    /// Without this guard, two threads exiting EnsureActiveGroupAsync simultaneously could both
    /// snapshot the same old CTS/task, cancel it, and each assign new CTS/task fields — the first
    /// writer's heartbeat loop would be overwritten and its CTS never cancelled.
    /// </summary>
    private async ValueTask StartHeartbeatCoreAsync(Func<CancellationToken, Task> loopFactory, int intervalMs)
    {
        Task? oldTask;
        CancellationTokenSource? oldCts;

        lock (_heartbeatGuard)
        {
            // Once close or disposal has begun no heartbeat starts, whichever path asks: a join
            // that was already in flight (a listener that ignored cancellation kept it past close's
            // wait) completes as a member but is never kept alive. The stop in close or disposal
            // takes this lock after setting the flag, so a heartbeat installed just before is
            // stopped there.
            if (Volatile.Read(ref _closing) != 0 || Volatile.Read(ref _disposed) != 0)
                return;

            oldCts = _heartbeatCts;
            oldTask = _heartbeatTask;

            // Log inside the lock so only the thread that actually installs a new heartbeat emits the message.
            LogHeartbeatStarted(intervalMs);

            _heartbeatCts = new CancellationTokenSource();
            _heartbeatTask = loopFactory(_heartbeatCts.Token);
        }

        // Clean up old heartbeat outside the lock (awaiting is safe here since the new
        // heartbeat is already running and the fields have been atomically swapped).
        if (oldCts is not null)
        {
            await oldCts.CancelAsync().ConfigureAwait(false);

            if (oldTask is not null)
            {
                try
                {
                    await oldTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore cancellation/timeout exceptions from old heartbeat
                }
            }

            oldCts.Dispose();
        }
    }

    private async ValueTask InvokeRebalanceListenerAsync(
        string callbackName,
        IReadOnlyList<TopicPartition> partitions,
        IRebalanceListener listener,
        Func<IRebalanceListener, IEnumerable<TopicPartition>, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken)
    {
        ThrowIfCallbackDeliveryStopped(cancellationToken);
        try
        {
            LogRebalanceListenerCall(callbackName, partitions.Count);
            await callback(listener, partitions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRebalanceListenerCallbackError(callbackName, ex);
        }
    }

    private async ValueTask InvokeConsumerAwareRebalanceListenerAsync(
        string callbackName,
        IReadOnlyList<TopicPartition> partitions,
        IConsumerAwareRebalanceListener listener,
        Func<
            IConsumerAwareRebalanceListener,
            IRebalanceConsumer,
            IEnumerable<TopicPartition>,
            CancellationToken,
            ValueTask> consumerAwareCallback,
        IEnumerable<TopicPartition> newlyAssigned,
        HashSet<TopicPartition> assignment,
        CancellationToken cancellationToken)
    {
        ThrowIfCallbackDeliveryStopped(cancellationToken);
        IRebalanceConsumerScope? consumerScope = null;
        try
        {
            LogRebalanceListenerCall(callbackName, partitions.Count);
            consumerScope = _createRebalanceConsumerScope!(
                assignment,
                newlyAssigned);
            await consumerAwareCallback(
                listener,
                consumerScope,
                partitions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRebalanceListenerCallbackError(callbackName, ex);
        }
        finally
        {
            consumerScope?.Invalidate();
        }
    }

    private async ValueTask InvokeRebalanceListenersAsync(
        string callbackName,
        IReadOnlyList<TopicPartition> partitions,
        Func<IRebalanceListener, IEnumerable<TopicPartition>, CancellationToken, ValueTask> callback,
        Func<
            IConsumerAwareRebalanceListener,
            IRebalanceConsumer,
            IEnumerable<TopicPartition>,
            CancellationToken,
            ValueTask> consumerAwareCallback,
        IEnumerable<TopicPartition> newlyAssigned,
        PendingRebalanceCallback? progress,
        CancellationToken cancellationToken)
    {
        // With progress, listeners that already completed this notification are skipped and each
        // one that completes is recorded, so a retry after cancellation resumes at the interrupted
        // listener. Configured listeners are fixed and the runtime listener is last, so a
        // listener's position is stable across retries.
        var completed = progress?.ListenersCompleted ?? 0;
        var position = 0;

        var configuredListener = _rebalanceListener;
        if (configuredListener is not null && position++ >= completed)
        {
            await InvokeRebalanceListenerAsync(
                callbackName,
                partitions,
                configuredListener,
                callback,
                cancellationToken).ConfigureAwait(false);
            progress?.ListenersCompleted = position;
        }

        var consumerAwareListener = _consumerAwareRebalanceListener;
        if (consumerAwareListener is not null && position++ >= completed)
        {
            await InvokeConsumerAwareRebalanceListenerAsync(
                callbackName,
                partitions,
                consumerAwareListener,
                consumerAwareCallback,
                newlyAssigned,
                // A queued callback's scope shows the assignment it was queued under, not one
                // published since.
                progress?.Assignment ?? _assignedPartitions,
                cancellationToken).ConfigureAwait(false);
            progress?.ListenersCompleted = position;
        }

        var additionalListeners = _additionalRebalanceListeners;
        if (additionalListeners is not null)
        {
            for (var index = 0; index < additionalListeners.Length; index++)
            {
                if (position++ < completed)
                    continue;

                await InvokeRebalanceListenerAsync(
                    callbackName,
                    partitions,
                    additionalListeners[index],
                    callback,
                    cancellationToken).ConfigureAwait(false);
                progress?.ListenersCompleted = position;
            }
        }

        var runtimeListener = Volatile.Read(ref _runtimeRebalanceListener);
        if (runtimeListener is not null && position++ >= completed)
        {
            await InvokeRebalanceListenerAsync(
                callbackName,
                partitions,
                runtimeListener,
                callback,
                cancellationToken).ConfigureAwait(false);
            progress?.ListenersCompleted = position;
        }
    }

    /// <summary>
    /// A rebalance notification not yet delivered: the partitions a fence cleared when
    /// <see cref="Lost"/> is set, otherwise a published assignment change, reserved when it was
    /// published. Only the drainer holding <c>_rebalanceListenerLock</c> reads or advances the
    /// progress fields, so a retry resumes where cancellation interrupted it.
    /// </summary>
    private sealed class PendingRebalanceCallback
    {
        public IReadOnlyList<TopicPartition>? Lost { get; init; }

        public ConsumerHeartbeatResult Deferred { get; init; }

        // The published assignment when this notification arose, for a consumer-aware
        // listener's scope. Assignment sets are replaced, never mutated, so the reference is a
        // stable snapshot.
        public required HashSet<TopicPartition> Assignment { get; init; }

        public bool RevokedDelivered;

        // Set when the internal revocation commit starts, so it runs once whichever drain
        // delivers this entry; cleared again if cancellation interrupts it, so it is retried.
        public bool RevocationCommitStarted;

        // 0: reserved, its publisher delivers it; 1: unowned, counted in
        // _pendingRebalanceCallbackCount; 2: dequeued. Transitions are interlocked, so an entry is
        // counted at most once and uncounted only if counted. The count always goes up before an
        // entry becomes unowned and down only after it has left the queue, so the stable poll
        // path never reads zero while an unowned entry is queued.
        public int PollVisibility;

        public int ListenersCompleted;
    }

    /// <summary>
    /// Checked before every listener call and every queued entry. Once the delivery's token is
    /// cancelled, or disposal has finished with callbacks, no further callback starts: the
    /// undelivered entries stay queued. A callback already running is not interrupted.
    /// </summary>
    private void ThrowIfCallbackDeliveryStopped(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _callbackDeliveryClosed) != 0)
            throw new OperationCanceledException("The consumer coordinator has been disposed.");

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void EnqueuePendingRebalanceCallback(PendingRebalanceCallback callback, bool reserved = false)
    {
        // Count first: the stable poll path reads only the count, so it must never see zero
        // while an unowned entry is queued. Both happen under the publish lock, so a reader that
        // sees the count but not yet the entry waits for it (HasQueuedRebalanceCallbacks).
        lock (_pendingPublishLock)
        {
            if (!reserved)
            {
                Interlocked.Increment(ref _pendingRebalanceCallbackCount);
                callback.PollVisibility = 1;
            }

            _pendingRebalanceCallbacks.Enqueue(callback);
        }
    }

    /// <summary>
    /// Whether any rebalance callback is queued. A counted entry whose publisher has not finished
    /// enqueueing it is waited for, so a count the stable poll path saw is never mistaken for an
    /// empty queue.
    /// </summary>
    private bool HasQueuedRebalanceCallbacks()
    {
        if (!_pendingRebalanceCallbacks.IsEmpty)
            return true;

        if (Volatile.Read(ref _pendingRebalanceCallbackCount) == 0)
            return false;

        lock (_pendingPublishLock)
            return !_pendingRebalanceCallbacks.IsEmpty;
    }

    /// <summary>
    /// Called when a publisher's delivery ends. Reserved entries it did not deliver (cancellation
    /// interrupted it) become visible to the stable poll path, which delivers them next.
    /// Enumerates the queue only when it is not empty, which is rare.
    /// </summary>
    private void ReleaseReservedRebalanceCallbacks()
    {
        if (_pendingRebalanceCallbacks.IsEmpty)
            return;

        foreach (var pending in _pendingRebalanceCallbacks)
        {
            if (Volatile.Read(ref pending.PollVisibility) != 0)
                continue;

            // Count first, then make the entry unowned. If a drainer dequeued it (or another
            // release counted it) in between, take the count back.
            Interlocked.Increment(ref _pendingRebalanceCallbackCount);
            if (Interlocked.CompareExchange(ref pending.PollVisibility, 1, 0) != 0)
                Interlocked.Decrement(ref _pendingRebalanceCallbackCount);
        }
    }

    /// <summary>
    /// Commits offsets for the group.
    /// </summary>
    /// <remarks>
    /// The membership is captured when this is called, so the caller must take its offsets
    /// under the current membership. Internal callers that snapshot offsets read
    /// <see cref="MembershipVersion"/> before the snapshot and pass it to the versioned overload.
    /// </remarks>
    public ValueTask CommitOffsetsAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken)
        => CommitOffsetsAsync(offsets, retryUntilApiTimeout: false, MembershipVersion, cancellationToken);

    /// <summary>
    /// The current membership version. A caller that snapshots offsets reads it first and passes
    /// it to <see cref="CommitOffsetsAsync(IEnumerable{TopicPartitionOffset}, bool, int, CancellationToken)"/>,
    /// so offsets taken under a membership that has since been replaced are never sent.
    /// </summary>
    internal int MembershipVersion
    {
        get
        {
            // A join publication is a short synchronous section under the state lock, so waiting
            // it out is brief. Nothing on the poll path reads this; a commit reads it once.
            var version = Volatile.Read(ref _membershipVersion);
            if ((version & 1) == 0)
                return version;

            // A commit started by a hook the publication itself runs, on this thread or on one
            // that hook waits for, cannot wait for the publication to end. It gets the version
            // from before the publication instead, so it is rejected as a commit from the
            // previous membership: never a deadlock, never offsets sent under the wrong one.
            if (ReferenceEquals(t_publishingCoordinator, this))
                return version - 1;

            var waitStarted = Stopwatch.GetTimestamp();
            var spinner = new SpinWait();
            while (((version = Volatile.Read(ref _membershipVersion)) & 1) != 0)
            {
                if (Stopwatch.GetTimestamp() - waitStarted > MembershipPublicationWaitTicks)
                    return version - 1;

                spinner.SpinOnce();
            }

            return version;
        }
    }

    /// <param name="retryUntilApiTimeout">
    /// True for an application-facing commit that runs under the consumer's aggregate API
    /// timeout: retriable failures, including a coordinator that refuses connections while
    /// cluster metadata still names it, are retried for that budget and the final error is always
    /// a typed <see cref="KafkaException"/>. Background, rebalance and close-path commits keep the
    /// short count-bounded retry: they swallow the failure, and must not hold the commit lock or
    /// delay a shutdown for the length of an outage.
    /// </param>
    /// <param name="membershipVersion">
    /// The membership version read before the offsets were taken. The commit is rejected if the
    /// membership changes after that, up to the send.
    /// </param>
    internal async ValueTask CommitOffsetsAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        bool retryUntilApiTimeout,
        int membershipVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.GroupId))
            return;

        // The version was read before the fence flag: FenceMembership sets the flag before it
        // advances the version, so a commit that passes the check below under a membership that
        // is being fenced still sees the version change before it sends.
        ThrowIfMaxPollIntervalExpired();
        ThrowIfMembershipChangedSince(membershipVersion);

        LogCommitOffsetsStarted(_options.GroupId!);
        // Lock is intentionally held across retries to protect the shared _commitTopicGroups dictionary,
        // which is reused across calls to avoid allocations. With up to 3 retries x ~600ms each,
        // the lock can be held for up to ~1.8 seconds — longer when a StaleMemberEpoch retry waits
        // up to one heartbeat interval for the refreshed epoch. Concurrent callers block for the
        // full retry duration. A retryUntilApiTimeout commit can hold it for the API timeout, but
        // only while the coordinator is unreachable, when every other commit would fail too; each
        // waiter is bounded by its own token, and the holder finishes within one backoff step of
        // the coordinator coming back.
        await _commitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RetryHelper.WithRetryAsync(async () =>
            {
                // Expiration can happen while this call waits for the commit lock.
                ThrowIfMaxPollIntervalExpired();

                if (_coordinatorId < 0)
                    await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);

                var coordinatorId = _coordinatorId;
                if (coordinatorId < 0)
                {
                    throw new Errors.GroupException(
                        ErrorCode.CoordinatorNotAvailable,
                        "Coordinator was invalidated during offset commit discovery")
                    {
                        GroupId = _options.GroupId
                    };
                }

                using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                    coordinatorId,
                    _getCoordinationConnectionIndex(),
                    cancellationToken)
                    .ConfigureAwait(false);
                var connection = connectionLease.Connection;

                var offsetCommitVersion = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    ApiKey.OffsetCommit,
                    OffsetCommitRequest.LowestSupportedVersion,
                    OffsetCommitRequest.HighestSupportedVersion);

                // Group offsets by topic using pooled dictionary to avoid allocations
                // Clear existing Lists before clearing the dictionary to reuse List instances
                foreach (var list in _commitTopicGroups.Values)
                {
                    list.Clear();
                }

                foreach (var offset in offsets)
                {
                    if (!_commitTopicGroups.TryGetValue(offset.Topic, out var partitions))
                    {
                        partitions = [];
                        _commitTopicGroups[offset.Topic] = partitions;
                    }
                    partitions.Add(new OffsetCommitRequestPartition
                    {
                        PartitionIndex = offset.Partition,
                        CommittedOffset = offset.Offset,
                        CommittedLeaderEpoch = offset.LeaderEpoch,
                        CommittedMetadata = offset.Metadata
                    });
                }

                RemoveEmptyTopicGroups(_commitTopicGroups);

                OffsetTopicIdRequestMap? topicIdMap = offsetCommitVersion >= OffsetCommitRequest.TopicIdVersion
                    ? new OffsetTopicIdRequestMap(_metadataManager.Metadata, _commitTopicGroups.Count)
                    : null;
                var topicOffsets = new List<OffsetCommitRequestTopic>(_commitTopicGroups.Count);
                foreach (var kvp in _commitTopicGroups)
                {
                    topicOffsets.Add(new OffsetCommitRequestTopic
                    {
                        Name = kvp.Key,
                        TopicId = topicIdMap?.AddTopic(kvp.Key, "OffsetCommit") ?? Guid.Empty,
                        Partitions = kvp.Value
                    });
                }

                ReadCommitIdentity(membershipVersion, out var commitMemberId, out var commitMemberEpoch);
                var request = new OffsetCommitRequest
                {
                    GroupId = _options.GroupId!,
                    GenerationIdOrMemberEpoch = commitMemberEpoch,
                    MemberId = commitMemberId,
                    GroupInstanceId = _options.GroupInstanceId,
                    Topics = topicOffsets
                };

                ThrowIfMaxPollIntervalExpired();
                ThrowIfMembershipChangedSince(membershipVersion);
                var response = await connection.SendWithClientTelemetryAsync<OffsetCommitRequest, OffsetCommitResponse>(
                    request, offsetCommitVersion, TelemetryMetricCollector, cancellationToken).ConfigureAwait(false);

                var responseSnapshot = topicIdMap?.CaptureResponseSnapshot();

                // Check for errors
                foreach (var topic in response.Topics)
                {
                    var topicName = topicIdMap is null
                        ? topic.Name
                        : topicIdMap.MatchResponseTopic(
                            topic.TopicId,
                            responseSnapshot!,
                            "OffsetCommit",
                            responseMismatchIsRetriable: false);
                    foreach (var partition in topic.Partitions)
                    {
                        if (partition.ErrorCode != ErrorCode.None)
                        {
                            var staleMemberEpoch = partition.ErrorCode == ErrorCode.StaleMemberEpoch;
                            if (staleMemberEpoch)
                            {
                                // KIP-848: the coordinator bumped the member epoch (e.g. a
                                // reassignment) after this request was built. While the member
                                // is active, wait for the background heartbeat to deliver the
                                // refreshed epoch, then retry the commit with it, matching the
                                // Java client's commit semantics. Once the heartbeat loop has
                                // stopped (session lost, fenced, rejoining) no refresh will
                                // come: fail now instead of retrying until the API timeout.
                                if (_state == CoordinatorState.Stable)
                                {
                                    await WaitForMemberEpochRefreshAsync(
                                        request.GenerationIdOrMemberEpoch,
                                        cancellationToken).ConfigureAwait(false);
                                }

                                // A fence that stopped the wait is the more precise verdict.
                                ThrowIfMaxPollIntervalExpired();
                                staleMemberEpoch = _state == CoordinatorState.Stable;
                            }

                            throw new Errors.GroupException(partition.ErrorCode,
                                $"OffsetCommit failed for {topicName}-{partition.PartitionIndex}: {partition.ErrorCode}" +
                                (partition.ErrorCode == ErrorCode.StaleMemberEpoch && !staleMemberEpoch
                                    ? "; the consumer is not an active group member, poll to rejoin the group before committing"
                                    : string.Empty),
                                isRetriable: staleMemberEpoch || partition.ErrorCode.IsRetriable())
                            {
                                GroupId = _options.GroupId
                            };
                        }
                    }
                }
            }, _metadataManager, cancellationToken, _options.RetryBackoffMs, _options.RetryBackoffMaxMs,
                onRetry: FindCoordinatorAsync,
                shouldRefreshMetadata: ShouldRefreshMetadataForGroupRetry,
                deadline: retryUntilApiTimeout
                    ? new RetryDeadline(
                        $"OffsetCommit for group '{_options.GroupId}'",
                        TimeSpan.FromMilliseconds(_options.DefaultApiTimeoutMs),
                        _isDisposed)
                    : null).ConfigureAwait(false);
        }
        finally
        {
            _commitLock.Release();
        }
    }

    /// <summary>
    /// Waits for the background heartbeat loop to advance the member epoch past the value a
    /// failed OffsetCommit was sent with. Bounded by one heartbeat interval plus slack: if the
    /// epoch has not refreshed by then, the retry proceeds anyway and surfaces the coordinator's
    /// verdict. Stops early when the member leaves the active state (the heartbeat loop stopped).
    /// </summary>
    private async ValueTask WaitForMemberEpochRefreshAsync(int staleEpoch, CancellationToken cancellationToken)
    {
        var maxWait = TimeSpan.FromMilliseconds(_heartbeatIntervalMs + 1_000);
        var startedAt = Stopwatch.GetTimestamp();
        while (_generationId == staleEpoch
               && _state == CoordinatorState.Stable
               && Stopwatch.GetElapsedTime(startedAt) < maxWait)
        {
            ThrowIfMaxPollIntervalExpired();
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fetches committed offsets for the group.
    /// </summary>
    public async ValueTask<IReadOnlyDictionary<TopicPartition, TopicPartitionOffset>> FetchOffsetsAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.GroupId))
            return new Dictionary<TopicPartition, TopicPartitionOffset>();

        var configuredTimeout = TimeSpan.FromMilliseconds(_options.RequestTimeoutMs);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuredTimeout);
        var operationToken = timeout.Token;

        try
        {
            // Lock is intentionally held across retries to protect the shared _fetchTopicGroups dictionary,
            // which is reused across calls to avoid allocations. The operation token bounds lock acquisition,
            // membership recovery, retry delay, and every network request to one aggregate deadline.
            await _fetchLock.WaitAsync(operationToken).ConfigureAwait(false);
            var fetchLockHeld = true;
            try
            {
                return await RetryHelper.WithRetryAsync(async () =>
                {
                    if (_coordinatorId < 0)
                        await FindCoordinatorAsync(operationToken).ConfigureAwait(false);

                    var coordinatorId = _coordinatorId;
                    if (coordinatorId < 0)
                    {
                        throw new Errors.GroupException(
                            ErrorCode.CoordinatorNotAvailable,
                            "Coordinator was invalidated during offset fetch discovery")
                        {
                            GroupId = _options.GroupId
                        };
                    }

                    using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                        coordinatorId,
                        _getCoordinationConnectionIndex(),
                        operationToken)
                        .ConfigureAwait(false);
                    var connection = connectionLease.Connection;

                    var offsetFetchVersion = _metadataManager.GetNegotiatedApiVersion(
                        connection,
                        ApiKey.OffsetFetch,
                        OffsetFetchRequest.LowestSupportedVersion,
                        OffsetFetchRequest.HighestSupportedVersion);

                    // Group partitions by topic using pooled dictionary to avoid allocations
                    // Clear existing Lists before clearing the dictionary to reuse List instances
                    foreach (var list in _fetchTopicGroups.Values)
                    {
                        list.Clear();
                    }

                    foreach (var partition in partitions)
                    {
                        if (!_fetchTopicGroups.TryGetValue(partition.Topic, out var indexes))
                        {
                            indexes = [];
                            _fetchTopicGroups[partition.Topic] = indexes;
                        }
                        indexes.Add(partition.Partition);
                    }

                    RemoveEmptyTopicGroups(_fetchTopicGroups);

                    OffsetTopicIdRequestMap? topicIdMap = offsetFetchVersion >= OffsetFetchRequest.TopicIdVersion
                        ? new OffsetTopicIdRequestMap(_metadataManager.Metadata, _fetchTopicGroups.Count)
                        : null;
                    var topicPartitions = new List<OffsetFetchRequestTopic>(_fetchTopicGroups.Count);
                    foreach (var kvp in _fetchTopicGroups)
                    {
                        topicPartitions.Add(new OffsetFetchRequestTopic
                        {
                            Name = kvp.Key,
                            TopicId = topicIdMap?.AddTopic(kvp.Key, "OffsetFetch") ?? Guid.Empty,
                            PartitionIndexes = kvp.Value
                        });
                    }

                    // A fence or rejoin changes the member id, epoch and membership version
                    // together under the state lock; snapshot them the same way, so a fence this
                    // request reports is applied to the membership it was sent under.
                    string? memberId;
                    int memberEpoch;
                    int membershipVersion;
                    await _lock.WaitAsync(operationToken).ConfigureAwait(false);
                    try
                    {
                        memberId = _memberId;
                        memberEpoch = _generationId;
                        membershipVersion = _membershipVersion;
                    }
                    finally
                    {
                        _lock.Release();
                    }

                    var request = new OffsetFetchRequest
                    {
                        GroupId = _options.GroupId!,
                        Topics = topicPartitions,
                        Groups =
                        [
                            new OffsetFetchRequestGroup
                        {
                            GroupId = _options.GroupId!,
                            MemberId = memberId,
                            MemberEpoch = memberEpoch,
                            Topics = topicPartitions
                        }
                        ]
                    };

                    var response = await connection.SendWithClientTelemetryAsync<OffsetFetchRequest, OffsetFetchResponse>(
                        request,
                        offsetFetchVersion, TelemetryMetricCollector,
                        operationToken).ConfigureAwait(false);

                    var responseSnapshot = topicIdMap?.CaptureResponseSnapshot();

                    // Check top-level error (v6-v7)
                    if (response.ErrorCode != ErrorCode.None)
                    {
                        throw new GroupException(response.ErrorCode,
                            $"OffsetFetch failed: {response.ErrorCode}")
                        {
                            GroupId = _options.GroupId
                        };
                    }

                    var result = new Dictionary<TopicPartition, TopicPartitionOffset>();

                    // v6-v7: Topics field
                    if (response.Topics is not null)
                    {
                        foreach (var topic in response.Topics)
                        {
                            foreach (var partition in topic.Partitions)
                            {
                                if (partition.ErrorCode != ErrorCode.None)
                                {
                                    throw new GroupException(partition.ErrorCode,
                                        $"OffsetFetch failed for {topic.Name}-{partition.PartitionIndex}: {partition.ErrorCode}")
                                    {
                                        GroupId = _options.GroupId
                                    };
                                }

                                if (partition.CommittedOffset >= 0)
                                {
                                    result[new TopicPartition(topic.Name, partition.PartitionIndex)] =
                                        new TopicPartitionOffset(
                                            topic.Name,
                                            partition.PartitionIndex,
                                            partition.CommittedOffset,
                                            partition.CommittedLeaderEpoch)
                                        {
                                            Metadata = partition.Metadata
                                        };
                                }
                            }
                        }
                    }

                    // v8+: Groups field
                    if (response.Groups is not null)
                    {
                        foreach (var group in response.Groups)
                        {
                            if (group.ErrorCode != ErrorCode.None)
                            {
                                var isRetriable = await HandleOffsetFetchMembershipErrorAsync(
                                        group.ErrorCode,
                                        membershipVersion).ConfigureAwait(false)
                                    || group.ErrorCode.IsRetriable();
                                throw new GroupException(
                                    group.ErrorCode,
                                    $"OffsetFetch failed for group: {group.ErrorCode}",
                                    isRetriable)
                                {
                                    GroupId = _options.GroupId
                                };
                            }

                            foreach (var topic in group.Topics)
                            {
                                var topicName = topicIdMap is null
                                    ? topic.Name
                                    : topicIdMap.MatchResponseTopic(
                                        topic.TopicId,
                                        responseSnapshot!,
                                        "OffsetFetch",
                                        responseMismatchIsRetriable: true);
                                foreach (var partition in topic.Partitions)
                                {
                                    if (partition.ErrorCode != ErrorCode.None)
                                    {
                                        throw new GroupException(partition.ErrorCode,
                                            $"OffsetFetch failed for {topicName}-{partition.PartitionIndex}: {partition.ErrorCode}")
                                        {
                                            GroupId = _options.GroupId
                                        };
                                    }

                                    if (partition.CommittedOffset >= 0)
                                    {
                                        result[new TopicPartition(topicName, partition.PartitionIndex)] =
                                            new TopicPartitionOffset(
                                                topicName,
                                                partition.PartitionIndex,
                                                partition.CommittedOffset,
                                                partition.CommittedLeaderEpoch)
                                            {
                                                Metadata = partition.Metadata
                                            };
                                    }
                                }
                            }
                        }
                    }

                    return result;
                },
                _metadataManager,
                operationToken,
                _options.RetryBackoffMs,
                _options.RetryBackoffMaxMs,
                onRetry: async retryToken =>
                {
                    // Recovery can rejoin and deliver rebalance callbacks, and a listener may
                    // fetch committed offsets itself (GetCommittedOffsetAsync). The fetch lock
                    // only guards building a request, so it is not held across recovery. A
                    // recovery that failed is retried without the lock re-acquired, so release
                    // it only while this call holds it; the next request never runs without it.
                    if (fetchLockHeld)
                    {
                        fetchLockHeld = false;
                        _fetchLock.Release();
                    }

                    await RecoverOffsetFetchAsync(retryToken).ConfigureAwait(false);
                    await _fetchLock.WaitAsync(retryToken).ConfigureAwait(false);
                    fetchLockHeld = true;
                },
                shouldRefreshMetadata: ShouldRefreshMetadataForGroupRetry,
                // Position initialization runs on the application's poll: a coordinator that
                // refuses connections while cluster metadata still names it is retried for the
                // aggregate deadline above instead of three quick attempts.
                deadline: new RetryDeadline(
                    "OffsetFetch",
                    Timeout.InfiniteTimeSpan,
                    _isDisposed)).ConfigureAwait(false);
            }
            finally
            {
                if (fetchLockHeld)
                    _fetchLock.Release();
            }
        }
        catch (OperationCanceledException ex) when (
            !cancellationToken.IsCancellationRequested
            && timeout.IsCancellationRequested)
        {
            // A retried fetch reports the failure it was still hitting as the cancellation's cause.
            throw new KafkaTimeoutException(
                $"OffsetFetch for group '{_options.GroupId}' did not complete within request timeout " +
                $"({_options.RequestTimeoutMs}ms)",
                ex.InnerException ?? ex);
        }
    }

    private async ValueTask RecoverOffsetFetchAsync(CancellationToken cancellationToken)
    {
        var subscribedTopics = _subscribedTopics;
        if (_state == CoordinatorState.Unjoined && subscribedTopics is not null)
        {
            await EnsureActiveGroupAsync(
                subscribedTopics,
                _subscribedTopicRegex,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);
    }

    // Membership errors are recovered by the group protocol (epoch refresh or rejoin),
    // so a cluster metadata refresh buys nothing for their retries.
    private static bool ShouldRefreshMetadataForGroupRetry(KafkaException exception) =>
        exception.ErrorCode is not ErrorCode.StaleMemberEpoch and not ErrorCode.UnknownMemberId;

    private async ValueTask<bool> HandleOffsetFetchMembershipErrorAsync(
        ErrorCode errorCode,
        int membershipVersion)
    {
        switch (errorCode)
        {
            case ErrorCode.StaleMemberEpoch:
                RequestRejoin();
                return true;

            case ErrorCode.UnknownMemberId:
                // Under the state lock, like the join and heartbeat fences, so it cannot clear an
                // assignment a concurrent rejoin is publishing. The retry's recovery rejoins and
                // reports the lost partitions before the new assignment. The coordinator has
                // already answered, so cancellation does not discard the fence: the lock is taken
                // without the operation token, and only the callback delivery stays cancellable.
                await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    FenceMembershipIfCurrent(membershipVersion, forgetMember: true);
                }
                finally
                {
                    _lock.Release();
                }

                return true;

            default:
                return false;
        }
    }

    private readonly record struct ConsumerHeartbeatResult(
        bool AssignmentChanged,
        IReadOnlyList<TopicPartition>? Revoked,
        IReadOnlyList<TopicPartition>? Assigned,
        TaskCompletionSource<bool>? RevocationCommitCompletion = null,
        TaskCompletionSource<bool>? AssignmentCallbacksCompletion = null);

    /// <summary>
    /// Resets member identity and assignment to the pre-join state.
    /// Used during leave, fencing, and disposal.
    /// </summary>
    private void ResetMemberState()
    {
        using var change = BeginMembershipChange();
        _memberId = null;
        _generationId = -1;
        _state = CoordinatorState.Unjoined;
        Volatile.Write(ref _membershipFenced, 0);
        ClearAssignment();
    }

    /// <summary>
    /// The coordinator fenced this member: it no longer owns its partitions. Clears the
    /// assignment, queues it for OnPartitionsLost, and fences commits until the member rejoins.
    /// A member fenced by epoch keeps its member id and rejoins with epoch 0 (-2 for static
    /// members); an unknown member rejoins from scratch. Matches the Java client, which treats
    /// both errors as partitions lost.
    /// </summary>
    private void FenceMembership(bool forgetMember)
    {
        // The version is odd from before the identity changes until the fence is complete, so a
        // commit can never pair the reset identity with the previous membership's version.
        using var change = BeginMembershipChange();
        Volatile.Write(ref _membershipFenced, 1);
        if (forgetMember)
        {
            _memberId = null;
            _generationId = -1;
        }
        else
        {
            _generationId = _options.GroupInstanceId is not null ? -2 : 0;
        }

        _state = CoordinatorState.Unjoined;
        var lost = ClearAssignment();
        if (lost is not null)
            EnqueuePendingRebalanceCallback(new PendingRebalanceCallback
            {
                Lost = lost,
                Assignment = _assignedPartitions
            });
    }

    private IReadOnlyList<TopicPartition>? ClearAssignment()
    {
        var revoked = _assignedPartitions.Count != 0 ? _assignedPartitions.ToList() : null;

        NotifyRevoking(revoked);

        lock (_assignmentStateLock)
        {
            _assignedPartitions = [];
            _newlyExpandedPartitions = [];
            if (revoked is not null)
            {
                Interlocked.Increment(ref _assignmentVersion);
                EnqueueRevokedPartitions(revoked);
            }
        }

        if (revoked is not null)
            _onPartitionsRevoked?.Invoke(revoked);

        return revoked;
    }

    /// <summary>
    /// Fires rebalance listener callbacks for a ConsumerHeartbeatResult if assignment changed.
    /// </summary>
    private async ValueTask FireConsumerProtocolRebalanceListenersAsync(
        ConsumerHeartbeatResult result,
        CancellationToken cancellationToken)
    {
        var rebalanceListenerLockHeld = false;
        try
        {
            await _rebalanceListenerLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            rebalanceListenerLockHeld = true;

            // The result's callbacks were reserved in the pending queue when it was published,
            // behind anything queued earlier, so draining the queue delivers everything in
            // publication order. Cancellation leaves undelivered entries queued.
            await InvokePendingRebalanceCallbacksCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // The revocation commit's completion belongs to the entry: whichever drain delivers
            // it runs the commit and completes it, so assignment sync waits for it.
            ReleaseReservedRebalanceCallbacks();
            if (rebalanceListenerLockHeld)
                _rebalanceListenerLock.Release();
        }
    }

    /// <summary>
    /// Reserves the callbacks of an assignment change in the pending queue. Called under the state
    /// lock at the moment the change is published, so a later fence always queues its loss after
    /// it, and the entry carries the assignment it published. Allocated once per assignment
    /// change, never per message.
    /// </summary>
    private void ReserveRebalanceCallbacks(ConsumerHeartbeatResult result, HashSet<TopicPartition> published)
    {
        if (result.Revoked is { Count: > 0 } || result.Assigned is { Count: > 0 })
        {
            EnqueuePendingRebalanceCallback(
                new PendingRebalanceCallback
                {
                    Deferred = result,
                    Assignment = published
                },
                reserved: true);
        }
    }

    private ValueTask InvokePartitionsRevokedListenersAsync(
        IReadOnlyList<TopicPartition> revoked,
        PendingRebalanceCallback? progress,
        CancellationToken cancellationToken) =>
        InvokeRebalanceListenersAsync(
            "OnPartitionsRevoked",
            revoked,
            static (listener, partitions, token) => listener.OnPartitionsRevokedAsync(partitions, token),
            static (listener, consumer, partitions, token) =>
                listener.OnPartitionsRevokedAsync(consumer, partitions, token),
            [],
            progress,
            cancellationToken);

    private ValueTask InvokePartitionsAssignedListenersAsync(
        IReadOnlyList<TopicPartition> assigned,
        PendingRebalanceCallback? progress,
        CancellationToken cancellationToken) =>
        InvokeRebalanceListenersAsync(
            "OnPartitionsAssigned",
            assigned,
            static (listener, partitions, token) => listener.OnPartitionsAssignedAsync(partitions, token),
            static (listener, consumer, partitions, token) =>
                listener.OnPartitionsAssignedAsync(consumer, partitions, token),
            assigned,
            progress,
            cancellationToken);

    internal Telemetry.ClientTelemetryMetricCollector? TelemetryMetricCollector { get; init; }

    /// <summary>
    /// True when the owner (KafkaConsumer) keeps per-partition state for the assignment and
    /// acknowledges each synchronization through <see cref="AcknowledgeAssignmentSync"/>. A fence's
    /// commit block then lasts until that acknowledgement; otherwise a successful rejoin lifts it.
    /// </summary>
    internal bool SynchronizesAssignment { get; init; }

    /// <summary>
    /// The partitions revoked or lost since the owner last synchronized its assignment, without
    /// consuming them. Used at close, when no further synchronization will run.
    /// </summary>
    internal HashSet<TopicPartition>? PeekPartitionsRevokedSinceLastSync()
    {
        if (_revokedPartitionsSinceLastSync.IsEmpty)
            return null;

        lock (_assignmentStateLock)
            return _revokedPartitionsSinceLastSync.IsEmpty ? null : [.. _revokedPartitionsSinceLastSync];
    }
    private Telemetry.StandardClientTelemetryMetrics? StandardTelemetryMetrics => TelemetryMetricCollector?.StandardMetrics;

    private void EnsureServerSideRegexSupported(IKafkaConnection connection, string? subscribedTopicRegex)
    {
        if (subscribedTopicRegex is not null &&
            !_metadataManager.SupportsApiVersion(connection, ApiKey.ConsumerGroupHeartbeat, 1))
        {
            throw new BrokerVersionException(
                "Server-side regex subscriptions require ConsumerGroupHeartbeat v1 " +
                "(Kafka 4.1 or later). Use Subscribe(Func<string, bool>) for client-side filtering on older brokers.");
        }
    }

    /// <summary>
    /// Sends a ConsumerGroupHeartbeat request and processes the response.
    /// </summary>
    /// <param name="coordinatorId">
    /// The coordinator the caller discovered. Captured by the caller rather than read from
    /// <see cref="_coordinatorId"/> here: a heartbeat loop that fails concurrently invalidates
    /// that field, and leasing broker -1 would surface as an unknown-broker failure.
    /// </param>
    /// <param name="sentMembershipVersion">
    /// Steady heartbeat only: receives the membership version the request was built under, so a
    /// fence the response reports is applied to exactly the membership that sent it.
    /// </param>
    private async ValueTask<ConsumerHeartbeatResult> SendConsumerGroupHeartbeatAsync(
        int coordinatorId,
        bool isInitial,
        bool discardIfMembershipChanged,
        StrongBox<int>? sentMembershipVersion,
        CancellationToken cancellationToken)
    {
        var maxPollExpirationVersion = discardIfMembershipChanged
            ? Volatile.Read(ref _maxPollExpirationVersion)
            : 0;
        int membershipVersion;
        string? memberIdSnapshot;
        int memberEpochSnapshot;
        ConsumerGroupHeartbeatResponse response;
        int assignmentVersion;
        IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? ownedTopicPartitions;
        string? subscribedTopicRegex;
        using (var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                   coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
                   .ConfigureAwait(false))
        {
            var connection = connectionLease.Connection;

            if (discardIfMembershipChanged)
            {
                // A rejoin can replace the membership while this heartbeat discovers or leases a
                // connection. Snapshot the member id, epoch and membership version together under
                // the state lock, where every change to them is made, so the request and any fence
                // it reports describe the same membership.
                await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (_state != CoordinatorState.Stable ||
                        Volatile.Read(ref _maxPollExpirationVersion) != maxPollExpirationVersion ||
                        IsCurrentPollGenerationExpired())
                        return default;

                    memberIdSnapshot = _memberId;
                    memberEpochSnapshot = _generationId;
                    membershipVersion = _membershipVersion;
                }
                finally
                {
                    _lock.Release();
                }

                sentMembershipVersion!.Value = membershipVersion;
            }
            else
            {
                // The join path holds the state lock.
                memberIdSnapshot = _memberId;
                memberEpochSnapshot = _generationId;
                membershipVersion = Volatile.Read(ref _membershipVersion);
            }

            if (!_metadataManager.HasApiKey(connection, ApiKey.ConsumerGroupHeartbeat))
            {
                throw new BrokerVersionException(
                    "The target Kafka broker does not support the ConsumerGroupHeartbeat API " +
                    "(KIP-848, introduced in Kafka 4.0). Dekaf's consumer requires Kafka 4.0 or later.");
            }

            EnsureServerSideRegexSupported(connection, _subscribedTopicRegex);

            var version = _metadataManager.GetNegotiatedApiVersion(
                connection,
                ApiKey.ConsumerGroupHeartbeat,
                ConsumerGroupHeartbeatRequest.LowestSupportedVersion,
                ConsumerGroupHeartbeatRequest.HighestSupportedVersion);

            // KIP-1082: v1+ uses client-generated UUID v4 instead of empty string for new members.
            // Generate once when _memberId is null; subsequent heartbeats reuse the stored ID.
            // Thread-safety: _memberId is only null on the initial join path (protected by _lock)
            // or after ResetMemberState() which also transitions to Unjoined before any heartbeat loop restart.
            if (memberIdSnapshot is null && version >= 1 && !discardIfMembershipChanged)
                _memberId = memberIdSnapshot = Guid.NewGuid().ToString();

            var memberId = memberIdSnapshot ?? string.Empty;

            // MemberEpoch: 0 for initial join, -2 for static rejoin (set by fencing handler),
            // or the current epoch for steady-state heartbeats
            var memberEpoch = isInitial
                ? (memberEpochSnapshot == -2 && _options.GroupInstanceId is not null ? -2 : 0)
                : memberEpochSnapshot;

            // On initial join, send empty array (owns nothing). null means "unchanged" in KIP-848
            // which is invalid when there's no previous state.
            assignmentVersion = Volatile.Read(ref _assignmentVersion);
            ownedTopicPartitions =
                isInitial ? [] : GetOwnedTopicPartitionsForHeartbeat(assignmentVersion);

            // Atomically snapshot and clear the subscription-changed flag to prevent a race where
            // a concurrent EnsureActiveGroupConsumerProtocolAsync sets new topics + flag=true,
            // but this heartbeat clears the flag after sending the old topics.
            // Always send topics on initial/re-join — KIP-848 requires SubscribedTopicNames to be
            // non-null when joining. The flag must still be cleared to avoid a stale re-send later.
            var subscriptionChanged = Interlocked.Exchange(ref _subscriptionChanged, 0) == 1;
            var subscriptionShouldBeSent = isInitial || subscriptionChanged;
            var currentSubscribedTopicRegex = _subscribedTopicRegex;
            var subscribedTopics = subscriptionShouldBeSent ? _subscribedTopics?.ToList() : null;
            subscribedTopicRegex = subscriptionShouldBeSent && version >= 1
                ? currentSubscribedTopicRegex ?? (isInitial ? null : string.Empty)
                : null;

            var request = new ConsumerGroupHeartbeatRequest
            {
                GroupId = _options.GroupId!,
                MemberId = memberId,
                MemberEpoch = memberEpoch,
                InstanceId = _options.GroupInstanceId,
                RebalanceTimeoutMs = isInitial ? _options.RebalanceTimeoutMs : -1,
                RackId = isInitial ? _options.ClientRack : null,
                SubscribedTopicNames = subscribedTopics,
                SubscribedTopicRegex = subscribedTopicRegex,
                ServerAssignor = isInitial ? _options.GroupRemoteAssignor : null,
                TopicPartitions = ownedTopicPartitions
            };

            response = await connection.SendWithClientTelemetryAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                request, version, TelemetryMetricCollector, cancellationToken).ConfigureAwait(false);
        }

        // A successful join replaces the membership. The version turns odd before the response's
        // member id and epoch are written and even again once the new assignment is published
        // (below), so a commit that started under the previous membership is rejected, and one
        // that starts during the publication waits for it before taking its snapshot. A join
        // error leaves the membership as it was, and commits under it stay valid.
        var publishingMembership = !discardIfMembershipChanged && response.ErrorCode == ErrorCode.None;
        var membershipChange = publishingMembership ? BeginMembershipChange() : default;

        // A steady heartbeat reports a fence without waiting for the locks, so stopping the loop
        // cannot discard it: the loop applies it under the state lock, checked against the
        // membership version this request was sent under.
        if (discardIfMembershipChanged &&
            response.ErrorCode is ErrorCode.FencedMemberEpoch or ErrorCode.UnknownMemberId)
        {
            HandleConsumerGroupHeartbeatError(response, subscribedTopicRegex);
        }

        var assignmentProcessing = BeginAssignmentProcessing(response.Assignment);

        if (!discardIfMembershipChanged)
        {
            try
            {
                using (assignmentProcessing)
                {
                    return ProcessConsumerGroupHeartbeatResponse(
                        response,
                        isInitial,
                        ownedTopicPartitions,
                        assignmentVersion,
                        subscribedTopicRegex);
                }
            }
            finally
            {
                // Identity and assignment are published (or processing failed): even again.
                if (publishingMembership)
                    membershipChange.Dispose();
            }
        }

        var rebalanceListenerLockHeld = false;
        var rebalanceStarted = response.Assignment is not null
            ? StandardTelemetryMetrics?.RebalanceStarted() ?? -1 : -1;
        ConsumerHeartbeatResult result = default;
        try
        {
            using (assignmentProcessing)
            {
                await _rebalanceListenerLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                rebalanceListenerLockHeld = true;

                // Queued callbacks describe earlier assignments; deliver them while those are
                // still current, before this response publishes a newer one. Cancellation here
                // drops the response as the lock wait does, and the entries stay queued.
                if (HasQueuedRebalanceCallbacks())
                    await InvokePendingRebalanceCallbacksCoreAsync(cancellationToken).ConfigureAwait(false);

                await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    // A foreground poll can expire or fence the member, and a rejoin replace it,
                    // while this request is in flight: its response then describes a membership
                    // that is gone. Validate and publish under the state lock. The listener lock
                    // preserves callback ordering after this lock is released without blocking
                    // re-entrant APIs.
                    if (_state != CoordinatorState.Stable ||
                        _membershipVersion != membershipVersion ||
                        Volatile.Read(ref _maxPollExpirationVersion) != maxPollExpirationVersion ||
                        IsCurrentPollGenerationExpired())
                        return default;

                    result = ProcessConsumerGroupHeartbeatResponse(
                        response,
                        isInitial,
                        ownedTopicPartitions,
                        assignmentVersion,
                        subscribedTopicRegex);
                }
                finally
                {
                    _lock.Release();
                }
            }

            // Assignment state and the immediate stale-fetch marker are published, and the
            // result's callbacks reserved behind anything queued earlier. User callbacks remain
            // serialized, but no longer suppress buffered polls while awaiting.
            await InvokePendingRebalanceCallbacksCoreAsync(cancellationToken).ConfigureAwait(false);
            if (result.AssignmentChanged)
                StandardTelemetryMetrics?.RebalanceCompleted(rebalanceStarted);
        }
        finally
        {
            // The revocation commit's completion belongs to the entry: whichever drain delivers
            // it runs the commit and completes it, so assignment sync waits for it.
            ReleaseReservedRebalanceCallbacks();
            if (rebalanceListenerLockHeld)
                _rebalanceListenerLock.Release();
        }

        // Steady-heartbeat callbacks are fired above while publication is fenced against
        // max-poll expiry. The heartbeat loop must not fire the result a second time.
        return default;
    }

    private AssignmentProcessingScope BeginAssignmentProcessing(
        ConsumerGroupHeartbeatAssignment? assignment)
    {
        if (assignment is null)
            return default;

        Interlocked.Increment(ref _assignmentProcessingCount);
        return new AssignmentProcessingScope(this);
    }

    private readonly struct AssignmentProcessingScope : IDisposable
    {
        private readonly ConsumerCoordinator? _coordinator;

        public AssignmentProcessingScope(ConsumerCoordinator coordinator) => _coordinator = coordinator;

        public void Dispose()
        {
            if (_coordinator is { } coordinator)
                Interlocked.Decrement(ref coordinator._assignmentProcessingCount);
        }
    }

    private ConsumerHeartbeatResult ProcessConsumerGroupHeartbeatResponse(
        ConsumerGroupHeartbeatResponse response,
        bool isInitial,
        IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? ownedTopicPartitions,
        int assignmentVersion,
        string? subscribedTopicRegex)
    {

        if (response.ErrorCode != ErrorCode.None)
        {
            HandleConsumerGroupHeartbeatError(response, subscribedTopicRegex);
        }

        Volatile.Write(ref _lastSuccessfulHeartbeatTimestamp, Stopwatch.GetTimestamp());
        Volatile.Write(ref _lastHeartbeatFailure, null);

        if (!isInitial && ownedTopicPartitions is not null)
            Volatile.Write(ref _sentOwnedTopicPartitionsVersion, assignmentVersion);

        if (response.MemberId is not null)
            _memberId = response.MemberId;

        if (response.MemberEpoch != _generationId)
        {
            LogMemberEpochUpdated(response.MemberEpoch);
            _generationId = response.MemberEpoch;
        }

        if (response.HeartbeatIntervalMs > 0)
            _heartbeatIntervalMs = response.HeartbeatIntervalMs;

        if (response.Assignment is not null)
        {
            return ProcessConsumerGroupAssignment(response.Assignment);
        }

        return default;
    }

    /// <summary>
    /// Throws an appropriate exception for ConsumerGroupHeartbeat error codes.
    /// Does NOT mutate coordinator state — callers own state transitions.
    /// </summary>
    private void HandleConsumerGroupHeartbeatError(
        ConsumerGroupHeartbeatResponse response,
        string? subscribedTopicRegex)
    {
        throw response.ErrorCode switch
        {
            ErrorCode.GroupAuthorizationFailed or ErrorCode.TopicAuthorizationFailed or ErrorCode.ClusterAuthorizationFailed
                => KafkaException.FromErrorCode(response.ErrorCode,
                    $"ConsumerGroupHeartbeat failed: {response.ErrorCode} - {response.ErrorMessage}"),

            ErrorCode.UnknownMemberId => new GroupException(response.ErrorCode,
                $"ConsumerGroupHeartbeat: unknown member ID (fenced): {response.ErrorMessage}")
            { GroupId = _options.GroupId },

            ErrorCode.FencedMemberEpoch => new GroupException(response.ErrorCode,
                $"ConsumerGroupHeartbeat: fenced member epoch: {response.ErrorMessage}")
            { GroupId = _options.GroupId },

            ErrorCode.UnreleasedInstanceId => new GroupException(response.ErrorCode,
                $"ConsumerGroupHeartbeat: unreleased instance ID '{_options.GroupInstanceId}': {response.ErrorMessage}")
            { GroupId = _options.GroupId },

            ErrorCode.UnsupportedAssignor => new GroupException(response.ErrorCode,
                $"ConsumerGroupHeartbeat: unsupported assignor '{_options.GroupRemoteAssignor}': {response.ErrorMessage}")
            { GroupId = _options.GroupId },

            ErrorCode.InvalidRegularExpression => new GroupException(response.ErrorCode,
                $"ConsumerGroupHeartbeat: invalid subscription regex '{subscribedTopicRegex}': {response.ErrorMessage}")
            { GroupId = _options.GroupId },

            _ => new GroupException(response.ErrorCode,
                $"ConsumerGroupHeartbeat failed: {response.ErrorCode} - {response.ErrorMessage}")
            { GroupId = _options.GroupId }
        };
    }

    /// <summary>
    /// Converts the current assignment to the topic-partition format used by ConsumerGroupHeartbeat requests,
    /// resolving topic names to UUIDs via cached metadata.
    /// </summary>
    private IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? BuildOwnedTopicPartitions(
        HashSet<TopicPartition> assignedPartitions)
    {
        if (assignedPartitions.Count == 0)
            return null;

        var byTopic = new Dictionary<string, List<int>>();
        foreach (var tp in assignedPartitions)
        {
            if (!byTopic.TryGetValue(tp.Topic, out var partitions))
            {
                partitions = [];
                byTopic[tp.Topic] = partitions;
            }
            partitions.Add(tp.Partition);
        }

        var result = new List<ConsumerGroupHeartbeatTopicPartitions>(byTopic.Count);
        foreach (var (topicName, partitions) in byTopic)
        {
            var topicInfo = _metadataManager.Metadata.GetTopic(topicName);
            if (topicInfo is null || topicInfo.TopicId == Guid.Empty)
                continue;

            result.Add(new ConsumerGroupHeartbeatTopicPartitions
            {
                TopicId = topicInfo.TopicId,
                Partitions = partitions
            });
        }

        return result.Count > 0 ? result : null;
    }

    private IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? GetOwnedTopicPartitionsForHeartbeat(
        int assignmentVersion)
    {
        if (Volatile.Read(ref _sentOwnedTopicPartitionsVersion) == assignmentVersion)
            return null;

        if (Volatile.Read(ref _cachedOwnedTopicPartitionsVersion) == assignmentVersion)
            return _cachedOwnedTopicPartitions;

        var ownedTopicPartitions = _assignedPartitions.Count == 0
            ? []
            : BuildOwnedTopicPartitions(_assignedPartitions);

        if (ownedTopicPartitions is not null)
        {
            _cachedOwnedTopicPartitions = ownedTopicPartitions;
            Volatile.Write(ref _cachedOwnedTopicPartitionsVersion, assignmentVersion);
        }

        return ownedTopicPartitions;
    }

    /// <summary>
    /// Processes a ConsumerGroupHeartbeat assignment response, resolving topic UUIDs to names
    /// and computing the partition diff (revoked/assigned) against the current assignment.
    /// </summary>
    private ConsumerHeartbeatResult ProcessConsumerGroupAssignment(ConsumerGroupHeartbeatAssignment assignment)
    {
        var newAssignment = new HashSet<TopicPartition>();
        var newlyExpandedPartitions = new HashSet<TopicPartition>();

        foreach (var tp in assignment.AssignedTopicPartitions)
        {
            var topicInfo = _metadataManager.Metadata.GetTopic(tp.TopicId);
            if (topicInfo is null)
            {
                LogUnknownTopicIdInAssignment(tp.TopicId);
                continue;
            }

            foreach (var partition in tp.Partitions)
            {
                newAssignment.Add(new TopicPartition(topicInfo.Name, partition));
            }

            foreach (var partition in tp.NewPartitions)
            {
                newlyExpandedPartitions.Add(new TopicPartition(topicInfo.Name, partition));
            }
        }

        // PendingTopicPartitions are NOT added — per KIP-848, these are still owned by
        // other members and must not be consumed until they appear in AssignedTopicPartitions.

        var oldAssignment = _assignedPartitions;

        List<TopicPartition>? revoked = null;
        foreach (var partition in oldAssignment)
        {
            if (!newAssignment.Contains(partition))
            {
                revoked ??= [];
                revoked.Add(partition);
            }
        }

        List<TopicPartition>? assigned = null;
        foreach (var partition in newAssignment)
        {
            if (!oldAssignment.Contains(partition))
            {
                assigned ??= [];
                assigned.Add(partition);
            }
        }

        var changed = revoked is { Count: > 0 } || assigned is { Count: > 0 };
        TaskCompletionSource<bool>? revocationCommitCompletion = null;
        if (revoked is { Count: > 0 }
            && _options.OffsetCommitMode == OffsetCommitMode.Auto
            && _onPartitionsRevokedAsync is not null)
        {
            revocationCommitCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        var assignmentCallbacksCompletion = assigned is { Count: > 0 }
            ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
            : null;

        NotifyRevoking(revoked);

        var classificationChanged = false;
        lock (_assignmentStateLock)
        {
            // The heartbeat loop can acknowledge ownership before the poll loop initializes
            // positions. Retain prior classifications until that initialization explicitly
            // acknowledges them, while dropping partitions that are no longer assigned.
            foreach (var partition in _newlyExpandedPartitions)
            {
                if (newAssignment.Contains(partition))
                    newlyExpandedPartitions.Add(partition);
            }

            classificationChanged = !_newlyExpandedPartitions.SetEquals(newlyExpandedPartitions);
            if (changed || classificationChanged)
            {
                _assignedPartitions = newAssignment;
                _newlyExpandedPartitions = newlyExpandedPartitions;
                Interlocked.Increment(ref _assignmentVersion);

                if (revoked is not null)
                    EnqueueRevokedPartitions(revoked);
                if (revocationCommitCompletion is not null)
                    _pendingRevocationCommit = revocationCommitCompletion.Task;
                if (assignmentCallbacksCompletion is not null)
                    _pendingAssignmentCallbacks = assignmentCallbacksCompletion.Task;
            }
        }

        if (revoked is not null)
            _onPartitionsRevoked?.Invoke(revoked);

        if (changed)
            LogConsumerProtocolAssignmentUpdate(assigned?.Count ?? 0, revoked?.Count ?? 0);

        var result = new ConsumerHeartbeatResult(
            changed,
            revoked,
            assigned,
            revocationCommitCompletion,
            assignmentCallbacksCompletion);
        if (changed)
            ReserveRebalanceCallbacks(result, newAssignment);

        return result;
    }

    private void NotifyRevoking(IReadOnlyList<TopicPartition>? revoked)
    {
        if (revoked is not null)
            _onPartitionsRevoking?.Invoke(revoked);
    }

    /// <summary>
    /// KIP-848 entry point: ensures the consumer has joined the group using the ConsumerGroupHeartbeat API.
    /// </summary>
    private async ValueTask EnsureActiveGroupConsumerProtocolAsync(
        StringSet topics,
        string? subscribedTopicRegex,
        CancellationToken cancellationToken)
    {
        // A rebalance listener's callback cannot join the group: the join's own callbacks would
        // wait for the delivery the listener is running in. The consumer rejoins once the
        // callback has returned, on its next poll.
        if (IsInsideOwnRebalanceCallback())
        {
            throw new InvalidOperationException(
                "The consumer cannot rejoin its group from inside a rebalance listener callback. " +
                "Return from the callback; the consumer rejoins on its next poll.");
        }

        if (Volatile.Read(ref _closing) != 0)
        {
            throw new ObjectDisposedException(
                nameof(ConsumerCoordinator),
                "The consumer is closing; it does not rejoin its group.");
        }

        UpdateSubscription(topics, subscribedTopicRegex);

        ConsumerHeartbeatResult heartbeatResult = default;
        long rebalanceStarted = -1;
        var rebalanceTimeout = TimeSpan.FromMilliseconds(_options.RebalanceTimeoutMs);
        Exception? lastJoinFailure = null;
        var joinFailed = false;

        // One attempt can outlast the rebalance timeout on its own: a coordinator lookup is five
        // connection attempts, each up to the connection-setup timeout against a broker that
        // black-holes packets. The deadline token bounds every attempt, not just the check
        // between attempts. Armed once the join actually starts.
        using var joinDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var joinToken = joinDeadline.Token;

        // Queued callbacks describe earlier assignments; deliver them while those are still
        // current, before the join publishes a newer one (a consumer-aware listener's scope is
        // built from the published assignment).
        // A max-poll expiry delivering its loss keeps the join from publishing until it
        // completes (the poll generation cannot advance), and its owner drains the queue.
        if (Volatile.Read(ref _maxPollLossNotificationPending) == 0)
            await InvokePendingRebalanceCallbacksAsync(cancellationToken).ConfigureAwait(false);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfFatalHeartbeatException();

            if (_state == CoordinatorState.Stable)
                return;

            var expiredPollVersion = Volatile.Read(ref _maxPollExpiredAtPollVersion);
            if (expiredPollVersion >= 0)
            {
                // Background prefetch also calls EnsureActiveGroup. Do not let it rejoin a
                // member evicted for max.poll.interval until the application polls again.
                if (Volatile.Read(ref _pollVersion) == expiredPollVersion)
                    return;

                // Keep the marker until this join succeeds so commits stay locally fenced.
                // Reuse the member identity for this process. Static members must rejoin with
                // epoch -2; dynamic members rejoin with the initial epoch 0.
                _generationId = _options.GroupInstanceId is null ? 0 : -2;
            }

            LogEnsureActiveGroupStarted(_options.GroupId!, _state);
            var startedAt = Stopwatch.GetTimestamp();
            rebalanceStarted = startedAt;
            var retryFailureCount = 0;
            joinDeadline.CancelAfter(rebalanceTimeout);

            while (_state != CoordinatorState.Stable)
            {
                if (Stopwatch.GetElapsedTime(startedAt) >= rebalanceTimeout)
                    throw CreateJoinTimeoutException(startedAt, rebalanceTimeout, lastJoinFailure);

                try
                {
                    if (_coordinatorId < 0)
                    {
                        await FindCoordinatorAsync(joinToken).ConfigureAwait(false);
                    }

                    // A heartbeat loop from the previous membership can still fail and invalidate
                    // the field after the lookup; join against the coordinator that was found.
                    var coordinatorId = _coordinatorId;
                    if (coordinatorId < 0)
                    {
                        throw new Errors.GroupException(
                            ErrorCode.CoordinatorNotAvailable,
                            "Coordinator was invalidated during group join discovery")
                        {
                            GroupId = _options.GroupId
                        };
                    }

                    _state = CoordinatorState.Joining;
                    LogCoordinatorStateTransition(CoordinatorState.Joining);

                    try
                    {
                        heartbeatResult = await SendConsumerGroupHeartbeatAsync(
                            coordinatorId,
                            isInitial: _memberId is null || _generationId <= 0,
                            discardIfMembershipChanged: false,
                            sentMembershipVersion: null,
                            joinToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (
                        ex is not OperationCanceledException || !joinToken.IsCancellationRequested)
                    {
                        Volatile.Write(ref _lastHeartbeatFailure, ex.Message);
                        throw;
                    }

                    // The membership version was advanced before the response was processed.
                    _state = CoordinatorState.Stable;
                    // With a consumer that synchronizes its assignment, _membershipFenced stays
                    // set until AcknowledgeAssignmentSync: the consumer has not yet dropped the
                    // offsets it stored for the lost partitions. A direct user of the coordinator
                    // keeps no such state, so the rejoin itself lifts the fence.
                    if (!SynchronizesAssignment)
                        Volatile.Write(ref _membershipFenced, 0);
                    if (Volatile.Read(ref _foregroundPollActivityCount) != 0)
                        RefreshPollDeadline();

                    Diagnostics.DekafMetrics.RebalanceDuration.Record(
                        Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                        new System.Diagnostics.TagList
                            { { Diagnostics.DekafDiagnostics.MessagingConsumerGroupName, _options.GroupId } });

                    LogJoinedGroup(_options.GroupId!, _memberId!, _generationId);
                }
                catch (Errors.GroupException ex) when (ex.ErrorCode == ErrorCode.FencedMemberEpoch)
                {
                    // Stale epoch: the partitions are lost; the next attempt sends MemberEpoch=0
                    // (or -2 for static members) and owns nothing.
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    FenceMembership(forgetMember: false);
                }
                catch (Errors.GroupException ex) when (ex.ErrorCode == ErrorCode.UnreleasedInstanceId)
                {
                    // Static member's previous session not yet released — retry with backoff
                    // until the rebalance timeout fires (checked at top of loop)
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    await DelayForJoinRetryAsync(
                        ++retryFailureCount, startedAt, rebalanceTimeout, joinToken).ConfigureAwait(false);
                }
                catch (Errors.GroupException ex) when (ex.ErrorCode == ErrorCode.UnknownMemberId)
                {
                    // Broker forgot this member (e.g. its session expired during a coordinator
                    // outage): the partitions are lost; full reset and retry.
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    FenceMembership(forgetMember: true);
                }
                catch (Errors.GroupException ex) when (IsRetriableCoordinatorError(ex.ErrorCode))
                {
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    lastJoinFailure = ex;
                    MarkCoordinatorUnknown();
                    await DelayForJoinRetryAsync(
                        ++retryFailureCount, startedAt, rebalanceTimeout, joinToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsRetriableJoinFailure(ex))
                {
                    // A failure that lands after the caller cancelled reports the cancellation;
                    // one that lands after the join deadline reports the timeout at the loop top.
                    cancellationToken.ThrowIfCancellationRequested();

                    if (ex is ObjectDisposedException)
                        LogCoordinatorConnectionDisposed();
                    else if (lastJoinFailure is null)
                        LogCoordinatorUnreachableDuringJoin(ex, _coordinatorId, _options.GroupId!);
                    else
                        LogCoordinatorStillUnreachableDuringJoin(ex, _coordinatorId, _options.GroupId!, retryFailureCount + 1);

                    lastJoinFailure = ex;
                    MarkCoordinatorUnknown();

                    // The pool has no route for the coordinator it was told to use; only newer
                    // metadata can fix the next attempt.
                    if (TransportFailureClassifier.RequiresMetadataRefresh(ex))
                    {
                        await RetryHelper.RefreshMetadataForRetryAsync(_metadataManager, joinToken)
                            .ConfigureAwait(false);
                    }

                    await DelayForJoinRetryAsync(
                        ++retryFailureCount, startedAt, rebalanceTimeout, joinToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && joinDeadline.IsCancellationRequested)
        {
            // The join deadline, not the caller, ended an attempt or a backoff still in flight.
            joinFailed = true;
            throw CreateJoinTimeoutException(rebalanceStarted, rebalanceTimeout, lastJoinFailure);
        }
        catch
        {
            joinFailed = true;
            throw;
        }
        finally
        {
            _lock.Release();

            // A fence can precede a failed attempt; the application still learns its partitions
            // are gone rather than waiting for the next successful join.
            if (joinFailed)
            {
                await InvokePendingRebalanceCallbacksUnlessCancelledAsync(cancellationToken).ConfigureAwait(false);
                ReleaseReservedRebalanceCallbacks();
            }
        }

        try
        {
            await FireConsumerProtocolRebalanceListenersAsync(heartbeatResult, cancellationToken).ConfigureAwait(false);
            StandardTelemetryMetrics?.RebalanceCompleted(rebalanceStarted);
        }
        finally
        {
            // The join succeeded even when cancellation interrupted its callbacks (they stay
            // queued), so the membership is kept alive either way.
            if (_state == CoordinatorState.Stable)
                await StartConsumerProtocolHeartbeatAsync().ConfigureAwait(false);
        }
    }

    private KafkaTimeoutException CreateJoinTimeoutException(
        long startedAt,
        TimeSpan rebalanceTimeout,
        Exception? lastJoinFailure)
    {
        var message =
            $"Failed to join group '{_options.GroupId}' within rebalance timeout ({_options.RebalanceTimeoutMs}ms)";
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        return lastJoinFailure is null
            ? new KafkaTimeoutException(TimeoutKind.Rebalance, elapsed, rebalanceTimeout, message)
            : new KafkaTimeoutException(TimeoutKind.Rebalance, elapsed, rebalanceTimeout, message, lastJoinFailure);
    }

    private ValueTask StartConsumerProtocolHeartbeatAsync()
        => StartHeartbeatCoreAsync(ConsumerProtocolHeartbeatLoopAsync, _heartbeatIntervalMs);

    /// <summary>
    /// KIP-848 heartbeat loop: sends ConsumerGroupHeartbeat at the broker-specified interval,
    /// handles assignment changes and errors.
    /// </summary>
    private async Task ConsumerProtocolHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        // Consecutive transient failures (coordinator unreachable or moving). While nonzero the
        // loop re-discovers the coordinator itself and beats on the retry backoff.
        var transientFailureCount = 0;
        var heartbeatCoordinatorId = -1;
        // The membership version each request is built under; a fence it reports applies only
        // to that membership. Allocated once per loop.
        var membershipVersion = new StrongBox<int>(Volatile.Read(ref _membershipVersion));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Task.Delay (not PeriodicTimer): the broker controls the interval via each response,
                // and PeriodicTimer cannot change its period after construction. Wake at the
                // max.poll deadline when it is sooner so eviction is not heartbeat-interval late.
                await Task.Delay(GetConsumerProtocolHeartbeatDelay(transientFailureCount), cancellationToken)
                    .ConfigureAwait(false);

                var expiration = await TryExpireMaxPollIntervalAsync(cancellationToken).ConfigureAwait(false);
                if (expiration.Expired)
                {
                    await CompleteMaxPollExpirationAsync(expiration.Lost).ConfigureAwait(false);

                    break;
                }

                // A foreground poll may have expired the member while this loop waited for _lock.
                if (_state != CoordinatorState.Stable)
                    break;

                heartbeatCoordinatorId = _coordinatorId;
                if (heartbeatCoordinatorId < 0)
                {
                    await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                    heartbeatCoordinatorId = _coordinatorId;
                    if (heartbeatCoordinatorId < 0)
                    {
                        throw new Errors.GroupException(
                            ErrorCode.CoordinatorNotAvailable,
                            "Coordinator was invalidated during heartbeat discovery")
                        {
                            GroupId = _options.GroupId
                        };
                    }
                }

                await SendConsumerGroupHeartbeatAsync(
                    heartbeatCoordinatorId,
                    isInitial: false,
                    discardIfMembershipChanged: true,
                    membershipVersion,
                    cancellationToken).ConfigureAwait(false);
                transientFailureCount = 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested && !IsMembershipFence(ex))
            {
                // A fence the coordinator already returned is still applied below.
                break;
            }
            catch (Exception ex)
            {
                if (transientFailureCount == 0)
                    LogHeartbeatFailed(ex);
                else
                    LogHeartbeatStillFailing(ex, transientFailureCount + 1);
                Volatile.Write(ref _lastHeartbeatFailure, ex.Message);

                if (ex is BrokerVersionException or AuthorizationException or AuthenticationException)
                {
                    await StoreFatalHeartbeatExceptionAsync((KafkaException)ex).ConfigureAwait(false);
                    break;
                }

                if (TryKeepHeartbeating(ex, heartbeatCoordinatorId))
                {
                    transientFailureCount++;
                    continue;
                }

                if (ex is Errors.GroupException ge)
                {
                    switch (ge.ErrorCode)
                    {
                        case ErrorCode.FencedMemberEpoch:
                        case ErrorCode.UnknownMemberId:
                            // The next EnsureActiveGroup rejoins with MemberEpoch=0 (or -2 for static).
                            if (!await TryFenceFromHeartbeatAsync(
                                    membershipVersion.Value,
                                    forgetMember: ge.ErrorCode == ErrorCode.UnknownMemberId).ConfigureAwait(false))
                                return;

                            try
                            {
                                await InvokePendingRebalanceCallbacksAsync(cancellationToken).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                // Stopped mid-callback; anything still queued is reported before
                                // the next assignment.
                                return;
                            }

                            break;

                        case ErrorCode.UnreleasedInstanceId:
                        case ErrorCode.UnsupportedAssignor:
                        case ErrorCode.InvalidRegularExpression:
                            _state = CoordinatorState.Unjoined;
                            break;

                        case var c when IsRetriableCoordinatorError(c):
                            MarkCoordinatorUnknown();
                            break;

                        default:
                            await StoreFatalHeartbeatExceptionAsync(ge).ConfigureAwait(false);
                            break;
                    }

                    break;
                }

                // The session is lost, or the failure is not one this loop understands. Force
                // FindCoordinator and a rejoin on the next foreground group operation.
                MarkCoordinatorUnknown();
                break;
            }
        }
    }

    /// <summary>
    /// Decides whether the heartbeat loop survives <paramref name="exception"/>. A coordinator
    /// that is unreachable or moving is transient: the loop invalidates the coordinator it used,
    /// re-discovers it on its next pass and keeps the membership alive. Handing the rejoin to the
    /// next foreground poll instead would fence a member whose application is busy in a handler
    /// longer than the session timeout while the prefetch loop is parked on a full buffer.
    /// Once a whole session timeout has passed without a successful heartbeat the broker has
    /// expired the member anyway, and the foreground rejoin path takes over.
    /// </summary>
    private bool TryKeepHeartbeating(Exception exception, int heartbeatCoordinatorId)
    {
        var transient = exception is Errors.GroupException groupException
            ? IsRetriableCoordinatorError(groupException.ErrorCode)
            : IsRetriableJoinFailure(exception);
        if (!transient)
            return false;

        // A foreground rejoin owns coordinator discovery once the member left Stable.
        if (_state != CoordinatorState.Stable)
            return false;

        var sinceLastSuccess = Stopwatch.GetElapsedTime(Volatile.Read(ref _lastSuccessfulHeartbeatTimestamp));
        if (sinceLastSuccess >= TimeSpan.FromMilliseconds(_options.SessionTimeoutMs))
            return false;

        // Invalidate only the coordinator this heartbeat used, never one a concurrent commit
        // or offset fetch just discovered. Membership state is untouched.
        if (_coordinatorId == heartbeatCoordinatorId)
            _coordinatorId = -1;

        return true;
    }

    private TimeSpan GetConsumerProtocolHeartbeatDelay(int transientFailureCount = 0)
    {
        var heartbeatDelay = TimeSpan.FromMilliseconds(Math.Max(_heartbeatIntervalMs, 1));
        if (transientFailureCount > 0)
        {
            var retryDelay = TimeSpan.FromMilliseconds(CalculateRequestRetryBackoff(transientFailureCount));
            if (retryDelay < heartbeatDelay)
                heartbeatDelay = retryDelay;
        }

        var remaining = TimeSpan.FromMilliseconds(_options.MaxPollIntervalMs)
            - Stopwatch.GetElapsedTime(Volatile.Read(ref _lastPollTimestamp));

        if (remaining <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return remaining < heartbeatDelay ? remaining : heartbeatDelay;
    }

    private async ValueTask<(bool Expired, IReadOnlyList<TopicPartition>? Lost)> TryExpireMaxPollIntervalAsync(
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state != CoordinatorState.Stable)
                return default;

            if (Volatile.Read(ref _foregroundPollActivityCount) != 0)
            {
                RefreshPollDeadline();
                return default;
            }

            var pollVersion = Volatile.Read(ref _pollVersion);
            var lastPollTimestamp = Volatile.Read(ref _lastPollTimestamp);
            if (Stopwatch.GetTimestamp() - lastPollTimestamp < _maxPollIntervalStopwatchTicks)
                return default;

            // Record the poll generation that expired. A prefetch loop may continue calling
            // EnsureActiveGroup, but only a new foreground poll increments this generation.
            Interlocked.Increment(ref _maxPollExpirationVersion);
            Volatile.Write(ref _maxPollExpiredAtPollVersion, pollVersion);
            var lost = ClearAssignment();
            // Queued under the state lock like a fence's loss, so it follows any callbacks
            // already queued and is delivered by the same ordered drain.
            if (lost is not null)
            {
                EnqueuePendingRebalanceCallback(new PendingRebalanceCallback
                {
                    Lost = lost,
                    Assignment = _assignedPartitions
                });
            }

            Volatile.Write(ref _maxPollLossNotificationPending, 1);
            _state = CoordinatorState.Unjoined;

            LogMaxPollIntervalExceeded(_options.MaxPollIntervalMs);
            await SendConsumerProtocolLeaveRequestAsync(
                ConsumerGroupMembershipOperation.Default,
                cancellationToken).ConfigureAwait(false);
            return (true, lost);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async ValueTask CompleteMaxPollExpirationAsync(IReadOnlyList<TopicPartition>? lost)
    {
        try
        {
            // The loss was queued behind earlier callbacks; the ordered drain delivers them all.
            if (lost is { Count: > 0 })
                await InvokePendingRebalanceCallbacksAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _maxPollLossNotificationPending, 0);
        }
    }

    private ValueTask InvokePartitionsLostCoreAsync(
        IReadOnlyList<TopicPartition> lost,
        PendingRebalanceCallback progress,
        CancellationToken cancellationToken) =>
        InvokeRebalanceListenersAsync(
            "OnPartitionsLost",
            lost,
            static (listener, partitions, token) => listener.OnPartitionsLostAsync(partitions, token),
            static (listener, consumer, partitions, token) =>
                listener.OnPartitionsLostAsync(consumer, partitions, token),
            [],
            progress,
            cancellationToken);

    private async ValueTask InvokePendingRebalanceCallbacksAsync(CancellationToken cancellationToken)
    {
        if (!HasQueuedRebalanceCallbacks() || IsInsideOwnRebalanceCallback())
            return;

        await _rebalanceListenerLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await InvokePendingRebalanceCallbacksCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _rebalanceListenerLock.Release();
        }
    }

    // Caller holds _rebalanceListenerLock, so it is the only drainer. An entry leaves the queue
    // only once every listener's callback has completed: a cancelled callback is delivered again
    // before the next assignment rather than dropped, and listeners that already completed it are
    // not called a second time.
    private async ValueTask InvokePendingRebalanceCallbacksCoreAsync(CancellationToken cancellationToken)
    {
        if (!HasQueuedRebalanceCallbacks())
            return;

        // The value reverts when this async method returns; tasks a listener starts inherit it,
        // so the scope is also deactivated. Allocated once per drain of a non-empty queue.
        var scope = new DrainScope(this);
        s_drainScope.Value = scope;
        try
        {
            while (_pendingRebalanceCallbacks.TryPeek(out var pending))
            {
                ThrowIfCallbackDeliveryStopped(cancellationToken);
                if (pending.Lost is { } lost)
                {
                    await InvokePartitionsLostCoreAsync(lost, pending, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var deferred = pending.Deferred;
                    if (!pending.RevokedDelivered)
                    {
                        if (deferred.Revoked is { Count: > 0 } revoked)
                        {
                            // The internal revocation commit runs once, on whichever delivery reaches
                            // this entry first, before the public callback. Assignment sync waits for
                            // its completion, so the revoked partitions' stored offsets are still
                            // there to commit even when an earlier callback's cancellation delayed it.
                            if (deferred.RevocationCommitCompletion is { } commitCompletion &&
                                !pending.RevocationCommitStarted)
                            {
                                pending.RevocationCommitStarted = true;
                                try
                                {
                                    if (_onPartitionsRevokedAsync is not null)
                                        await _onPartitionsRevokedAsync(revoked, cancellationToken).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    // Cancellation interrupted the commit: the next delivery runs it
                                    // again (the same offsets, so a commit that did reach the broker
                                    // is simply repeated), and sync keeps waiting until then.
                                    pending.RevocationCommitStarted = false;
                                    throw;
                                }
                                catch
                                {
                                    // Any other failure ends the attempt; the consumer's commit hook
                                    // logs its own failures, and retrying could repeat it forever.
                                    commitCompletion.TrySetResult(true);
                                    throw;
                                }

                                commitCompletion.TrySetResult(true);
                            }

                            await InvokePartitionsRevokedListenersAsync(revoked, pending, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        pending.RevokedDelivered = true;
                        pending.ListenersCompleted = 0;
                    }

                    if (deferred.Assigned is { Count: > 0 } assigned)
                    {
                        await InvokePartitionsAssignedListenersAsync(assigned, pending, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                // Delivered: the consumer may now synchronize this assignment.
                pending.Deferred.AssignmentCallbacksCompletion?.TrySetResult(true);

                // Count down only after the entry has left the queue.
                _pendingRebalanceCallbacks.TryDequeue(out _);
                if (Interlocked.Exchange(ref pending.PollVisibility, 2) == 1)
                    Interlocked.Decrement(ref _pendingRebalanceCallbackCount);
            }
        }
        finally
        {
            scope.Deactivate();
        }
    }

    private bool IsInsideOwnRebalanceCallback() =>
        s_drainScope.Value is { IsActive: true } scope && ReferenceEquals(scope.Coordinator, this);

    private sealed class DrainScope(ConsumerCoordinator coordinator)
    {
        private int _active = 1;

        public ConsumerCoordinator Coordinator { get; } = coordinator;

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Deactivate() => Volatile.Write(ref _active, 0);
    }

    /// <summary>
    /// Reports a fence that preceded a failed join or a leave. The join's or leave's own outcome
    /// stays the caller's: once the caller cancels, anything still queued is reported before the
    /// next assignment instead.
    /// </summary>
    internal async ValueTask InvokePendingRebalanceCallbacksUnlessCancelledAsync(CancellationToken cancellationToken)
    {
        if (!HasQueuedRebalanceCallbacks())
            return;

        // The wait is bounded by the token even when a listener ignores it (close, disposal, a
        // failed join or a leave must not hang on one). An abandoned drain keeps its entry queued
        // and the listener lock until the running callback returns, then stops: its token is
        // cancelled. Its outcome is still observed.
        var drain = InvokePendingRebalanceCallbacksAsync(cancellationToken).AsTask();
        try
        {
            await drain.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!drain.IsCompleted)
            {
                _ = drain.ContinueWith(
                    static (task, state) =>
                        ((ConsumerCoordinator)state!).LogAbandonedRebalanceCallbacksFailed(task.Exception!),
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    /// <summary>
    /// Applies a fence observed by a request sent under membership
    /// <paramref name="membershipVersion"/>. Caller holds <c>_lock</c>. Ignored when a foreground
    /// poll already expired or fenced the member, or when a later join replaced it: the fence
    /// then belongs to a membership that is already gone.
    /// </summary>
    private void FenceMembershipIfCurrent(int membershipVersion, bool forgetMember)
    {
        if (_state == CoordinatorState.Stable && _membershipVersion == membershipVersion)
            FenceMembership(forgetMember);
    }

    private static bool IsMembershipFence(Exception exception) =>
        exception is GroupException { ErrorCode: ErrorCode.FencedMemberEpoch or ErrorCode.UnknownMemberId };

    /// <summary>
    /// Applies a fence the heartbeat loop observed, under the state lock so it cannot interleave
    /// with a foreground join or max-poll expiry. The coordinator has already answered, so a
    /// heartbeat stop does not discard it: the member stays fenced, its commits rejected and its
    /// partitions queued as lost, whatever the stop's owner does next. Nothing that holds the
    /// state lock waits for the heartbeat loop. Returns false only when the coordinator is disposed.
    /// </summary>
    private async ValueTask<bool> TryFenceFromHeartbeatAsync(int membershipVersion, bool forgetMember)
    {
        try
        {
            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        try
        {
            FenceMembershipIfCurrent(membershipVersion, forgetMember);
        }
        finally
        {
            _lock.Release();
        }

        return true;
    }

    /// <summary>
    /// KIP-848 leave: sends ConsumerGroupHeartbeat with MemberEpoch=-1 for dynamic or permanently
    /// departing static members, or -2 for default static-member close so the broker keeps the
    /// assignment warm.
    /// </summary>
    private async ValueTask LeaveGroupConsumerProtocolAsync(
        ConsumerGroupMembershipOperation operation,
        CancellationToken cancellationToken,
        CancellationToken responseCancellationToken,
        CancellationToken callbackCancellationToken)
    {
        // Callbacks queued for the membership that is leaving (an assignment whose
        // OnPartitionsAssigned cancellation deferred, a loss) are delivered while it is still
        // current, in order, as they would have been without the cancellation. The heartbeat is
        // stopped first so it cannot publish a newer assignment behind them.
        await StopHeartbeatAsyncCore(cancellationToken).ConfigureAwait(false);
        await InvokePendingRebalanceCallbacksUnlessCancelledAsync(callbackCancellationToken).ConfigureAwait(false);

        await SendConsumerProtocolLeaveRequestAsync(operation, cancellationToken, responseCancellationToken)
            .ConfigureAwait(false);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ResetMemberState();
        }
        finally
        {
            _lock.Release();
        }

        // A fence recorded while leaving is still reported.
        await InvokePendingRebalanceCallbacksUnlessCancelledAsync(callbackCancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SendConsumerProtocolLeaveRequestAsync(
        ConsumerGroupMembershipOperation operation,
        CancellationToken cancellationToken,
        CancellationToken responseCancellationToken = default)
    {
        KafkaRequestWriteContext? writeContext = null;
        try
        {
            using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                _coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
                .ConfigureAwait(false);
            var connection = connectionLease.Connection;

            var request = new ConsumerGroupHeartbeatRequest
            {
                GroupId = _options.GroupId!,
                MemberId = _memberId!,
                MemberEpoch = operation == ConsumerGroupMembershipOperation.LeaveGroup ||
                              _options.GroupInstanceId is null
                    ? -1
                    : -2,
                InstanceId = _options.GroupInstanceId,
            };

            var version = _metadataManager.GetNegotiatedApiVersion(
                connection,
                ApiKey.ConsumerGroupHeartbeat,
                ConsumerGroupHeartbeatRequest.LowestSupportedVersion,
                ConsumerGroupHeartbeatRequest.HighestSupportedVersion);

            ConsumerGroupHeartbeatResponse response;
            if (responseCancellationToken.CanBeCanceled && connection is KafkaConnection kafkaConnection)
            {
                // cancellationToken bounds getting the request onto the wire, to the end of the
                // frame write; after that only responseCancellationToken bounds the wait for the
                // answer, which the leave does not need to take effect.
                writeContext = new KafkaRequestWriteContext(responseCancellationToken);
                response = await kafkaConnection
                    .SendWithResponseCancellationAfterWriteAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                        request, version, TelemetryMetricCollector, writeContext, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                response = await connection.SendWithClientTelemetryAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                    request, version, TelemetryMetricCollector, cancellationToken).ConfigureAwait(false);
            }

            if (response.ErrorCode != ErrorCode.None)
            {
                LogLeaveGroupFailed(response.ErrorCode);
            }
            else
            {
                LogSuccessfullyLeftGroup(_options.GroupId!);
            }
        }
        // The write is bounded by cancellationToken, so while it is still live a cancellation
        // here came from the response wait, after the leave was written.
        catch (OperationCanceledException) when (writeContext is { WriteStarted: true } &&
                                                 !cancellationToken.IsCancellationRequested &&
                                                 responseCancellationToken.IsCancellationRequested)
        {
            LogLeaveGroupSentWithoutResponse(_options.GroupId!);
        }
        catch (Exception ex)
        {
            LogLeaveGroupRequestFailed(ex);
        }
    }

    /// <summary>
    /// True when <see cref="LeaveGroupAsync(ConsumerGroupMembershipOperation, CancellationToken, CancellationToken)"/>
    /// would send a leave request: the member has joined and its coordinator is known.
    /// </summary>
    internal bool CanSendLeaveRequest =>
        Volatile.Read(ref _disposed) == 0
        && !string.IsNullOrEmpty(_options.GroupId)
        && !string.IsNullOrEmpty(_memberId)
        && _coordinatorId >= 0;

    /// <summary>
    /// Leaves the consumer group gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask LeaveGroupAsync(CancellationToken cancellationToken = default)
    {
        await LeaveGroupAsync(ConsumerGroupMembershipOperation.Default, cancellationToken).ConfigureAwait(false);
    }

    /// <param name="operation">How the member leaves.</param>
    /// <param name="cancellationToken">Bounds the whole leave, including getting the request onto the wire.</param>
    /// <param name="responseCancellationToken">
    /// When cancellable, also stops the wait for the response once the request has been written
    /// (the coordinator acts on a written leave whether or not its answer is awaited).
    /// </param>
    internal async ValueTask LeaveGroupAsync(
        ConsumerGroupMembershipOperation operation,
        CancellationToken cancellationToken = default,
        CancellationToken responseCancellationToken = default)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "The group membership operation is invalid.");

        if (Volatile.Read(ref _disposed) != 0)
            return;

        // A close's leave (responseCancellationToken set) delivers queued rebalance callbacks
        // only until close is cancelled: after that, what is left of cancellationToken is the
        // grace for getting the leave onto the wire, and a callback delivered again would use it
        // up (or, when no leave can be sent, only delay close). They stay queued for disposal.
        var callbackCancellationToken = responseCancellationToken.CanBeCanceled
            ? responseCancellationToken
            : cancellationToken;

        // Only leave if we're part of a group and the coordinator is known. A fence can have
        // taken the member id; its partitions are still reported lost.
        if (operation == ConsumerGroupMembershipOperation.RemainInGroup
            || string.IsNullOrEmpty(_options.GroupId)
            || string.IsNullOrEmpty(_memberId)
            || _coordinatorId < 0)
        {
            await InvokePendingRebalanceCallbacksUnlessCancelledAsync(callbackCancellationToken).ConfigureAwait(false);
            return;
        }

        await LeaveGroupConsumerProtocolAsync(
                operation,
                cancellationToken,
                responseCancellationToken,
                callbackCancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the heartbeat background task.
    /// </summary>
    public ValueTask StopHeartbeatAsync() => StopHeartbeatAsyncCore(CancellationToken.None);

    internal async ValueTask StopHeartbeatAsyncCore(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_heartbeatGuard)
        {
            cts = _heartbeatCts;
            task = _heartbeatTask;
            _heartbeatCts = null;
            _heartbeatTask = null;
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (task is not null)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore cancellation exceptions
            }
        }

        cts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        LogCoordinatorDisposing();

        await StopHeartbeatAsync().ConfigureAwait(false);

        // Rebalance callbacks the heartbeat stop interrupted (a fenced member's OnPartitionsLost,
        // say) are delivered before the locks go away. Entries leave the queue only once
        // delivered, so after a consumer close has drained there is nothing to repeat. Bounded
        // by the API timeout so a listener that never returns cannot hang disposal.
        if (HasQueuedRebalanceCallbacks())
        {
            using var drainTimeout = new CancellationTokenSource(_options.DefaultApiTimeoutMs);
            try
            {
                await InvokePendingRebalanceCallbacksUnlessCancelledAsync(drainTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // A listener gave up its callback; disposal goes on.
            }
        }

        // A drain still running (its listener ignored the token) starts no further callback.
        // The listener lock it holds is never disposed.
        Volatile.Write(ref _callbackDeliveryClosed, 1);

        // Backstop: nothing starts a heartbeat once _disposed is set, but stop any that did.
        await StopHeartbeatAsync().ConfigureAwait(false);

        // Revocation commits that will now never run must not hold up an assignment sync.
        foreach (var pending in _pendingRebalanceCallbacks)
        {
            pending.Deferred.RevocationCommitCompletion?.TrySetResult(true);
            pending.Deferred.AssignmentCallbacksCompletion?.TrySetResult(true);
        }

        _lock.Dispose();
        _commitLock.Dispose();
        _fetchLock.Dispose();
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Joined group {GroupId} as member {MemberId} (generation {Generation})")]
    private partial void LogJoinedGroup(string groupId, string memberId, int generation);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retriable coordinator error {ErrorCode}, will re-discover coordinator")]
    private partial void LogRetriableCoordinatorError(ErrorCode? errorCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator connection disposed, will re-discover coordinator")]
    private partial void LogCoordinatorConnectionDisposed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Coordinator {CoordinatorId} unreachable while joining group {GroupId}; re-discovering coordinator and retrying until the rebalance timeout")]
    private partial void LogCoordinatorUnreachableDuringJoin(Exception exception, int coordinatorId, string groupId);

    // The first failure of a join is the Warning above; an outage then repeats at Debug.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator {CoordinatorId} still unreachable while joining group {GroupId} (attempt {Attempt})")]
    private partial void LogCoordinatorStillUnreachableDuringJoin(Exception exception, int coordinatorId, string groupId, int attempt);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator not available (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms")]
    private partial void LogCoordinatorNotAvailableRetry(int attempt, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found coordinator {NodeId} for group {GroupId}")]
    private partial void LogFoundCoordinator(int nodeId, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rebalance callbacks abandoned at a timeout failed after the wait ended")]
    private partial void LogAbandonedRebalanceCallbacksFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat failed")]
    private partial void LogHeartbeatFailed(Exception exception);

    // The first failure of an outage is the Warning above; the retries repeat at Debug.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Heartbeat still failing (attempt {Attempt}); re-discovering coordinator and retrying")]
    private partial void LogHeartbeatStillFailing(Exception exception, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Maximum poll interval of {MaxPollIntervalMs}ms exceeded; leaving consumer group")]
    private partial void LogMaxPollIntervalExceeded(int maxPollIntervalMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LeaveGroup failed with error: {ErrorCode}")]
    private partial void LogLeaveGroupFailed(ErrorCode errorCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully left group {GroupId}")]
    private partial void LogSuccessfullyLeftGroup(string groupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sent the LeaveGroup request for group {GroupId}; stopped waiting for its response because close was cancelled")]
    private partial void LogLeaveGroupSentWithoutResponse(string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send LeaveGroup request")]
    private partial void LogLeaveGroupRequestFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "EnsureActiveGroup: group={GroupId}, current state={State}")]
    private partial void LogEnsureActiveGroupStarted(string groupId, CoordinatorState state);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator state transition to {NewState}")]
    private partial void LogCoordinatorStateTransition(CoordinatorState newState);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rebalance listener {CallbackName}: {PartitionCount} partitions")]
    private partial void LogRebalanceListenerCall(string callbackName, int partitionCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Heartbeat loop started with interval {IntervalMs}ms")]
    private partial void LogHeartbeatStarted(int intervalMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CommitOffsets started for group {GroupId}")]
    private partial void LogCommitOffsetsStarted(string groupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator disposing")]
    private partial void LogCoordinatorDisposing();

    [LoggerMessage(Level = LogLevel.Error, Message = "{CallbackName} rebalance listener callback threw an exception")]
    private partial void LogRebalanceListenerCallbackError(string callbackName, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ConsumerGroupHeartbeat: unknown topic ID {TopicId} in assignment, skipping")]
    private partial void LogUnknownTopicIdInAssignment(Guid topicId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ConsumerGroupHeartbeat: assignment updated, {AssignedCount} assigned, {RevokedCount} revoked")]
    private partial void LogConsumerProtocolAssignmentUpdate(int assignedCount, int revokedCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ConsumerGroupHeartbeat: member epoch updated to {MemberEpoch}")]
    private partial void LogMemberEpochUpdated(int memberEpoch);

    #endregion
}

/// <summary>
/// Consumer group coordinator state.
/// </summary>
public enum CoordinatorState
{
    Unjoined,
    Joining,
    Stable
}
