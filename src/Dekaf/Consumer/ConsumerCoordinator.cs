using System.Collections.Concurrent;
using System.Diagnostics;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Retry;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Microsoft.Extensions.Logging;
#if NETSTANDARD2_0
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
    // Heartbeats can revoke and reassign a partition between poll-loop snapshots.
    // Preserve both the immediate stale-fetch signal and the revocation history needed
    // to defeat assignment ABA (A -> B -> A with the same final set).
    private readonly Action<IReadOnlyList<TopicPartition>>? _onPartitionsRevoked;
    private readonly ConcurrentQueue<TopicPartition> _revokedPartitionsSinceLastSync = new();
    // Assignment publication and revocation history form one snapshot. Rebalance callbacks
    // run only after this lock is released so user code cannot extend its critical section.
    private readonly Lock _assignmentStateLock = new();
    private IRebalanceListener? _runtimeRebalanceListener;
    private readonly ILogger _logger;

    private volatile int _coordinatorId = -1;
    private volatile string? _memberId;
    private volatile int _generationId = -1;
    private int _assignmentVersion;
    // Volatile ensures cross-thread visibility of the reference. Thread-safety relies on
    // all writes replacing the reference entirely (never in-place mutation) — verified at
    // every assignment site: ProcessConsumerGroupAssignment() and DisposeAsync().
    private volatile HashSet<TopicPartition> _assignedPartitions = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    // Serializes max-poll expiry with the synchronous start of steady-heartbeat
    // rebalance callbacks. User callbacks are awaited only after this gate is released.
    private readonly object _heartbeatResponsePublicationGate = new();
    private readonly object _heartbeatGuard = new();
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    // Pooled dictionaries to avoid allocations in hot paths (protected by _lock or method-local usage)
    private readonly Dictionary<string, List<OffsetCommitRequestPartition>> _commitTopicGroups = new();
    private readonly SemaphoreSlim _commitLock = new(1, 1);
    private readonly Dictionary<string, List<int>> _fetchTopicGroups = new();
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    private volatile CoordinatorState _state = CoordinatorState.Unjoined;
    private GroupException? _fatalHeartbeatException;
    private int _disposed;
    private readonly Func<int> _getCoordinationConnectionIndex;

    // KIP-848 consumer protocol state
    private volatile int _heartbeatIntervalMs;
    private readonly long _maxPollIntervalStopwatchTicks;
    private long _lastPollTimestamp;
    private long _pollVersion;
    private long _maxPollExpiredAtPollVersion = -1;
    private long _maxPollExpirationVersion;
    private int _maxPollLossNotificationPending;
    // A pending MoveNext/ConsumeOne fetch is application poll activity. Track concurrent callers
    // without allocating a scope object on each fetch cycle.
    private int _foregroundFetchWaitCount;
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
    {
        _options = options;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _rebalanceListener = options.RebalanceListener;
        _onPartitionsRevoked = onPartitionsRevoked;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConsumerCoordinator>.Instance;
        _getCoordinationConnectionIndex = getConnectionCount is not null
            ? () => GetCoordinationConnectionIndex(getConnectionCount())
            : () => GetCoordinationConnectionIndex(options.ConnectionsPerBroker);
        _heartbeatIntervalMs = options.HeartbeatIntervalMs;
        _maxPollIntervalStopwatchTicks = options.MaxPollIntervalMs * Stopwatch.Frequency / 1000;
        _lastPollTimestamp = Stopwatch.GetTimestamp();
    }

    public string? MemberId => _memberId;
    public int GenerationId => _generationId;
    public CoordinatorState State => _state;
    public TopicPartitionSet Assignment => _assignedPartitions;
    internal int AssignmentVersion => Volatile.Read(ref _assignmentVersion);

    internal ValueTask RecordPollAsync(CancellationToken cancellationToken)
    {
        // Heartbeat expiry invokes user callbacks outside _lock. Keep its poll generation
        // unchanged so EnsureActiveGroup cannot rejoin until notification completes.
        if (Volatile.Read(ref _maxPollLossNotificationPending) != 0)
            return ValueTask.CompletedTask;

        if (Volatile.Read(ref _foregroundFetchWaitCount) != 0)
        {
            RefreshPollDeadline();
            return ValueTask.CompletedTask;
        }

        var now = Stopwatch.GetTimestamp();
        if (_state == CoordinatorState.Stable
            && now - Volatile.Read(ref _lastPollTimestamp) >= _maxPollIntervalStopwatchTicks)
        {
            return ExpireAndRecordPollAsync(cancellationToken);
        }

        RecordPollIfLossNotificationComplete(now);
        return ValueTask.CompletedTask;
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

    internal void BeginForegroundFetchWait()
    {
        Interlocked.Increment(ref _foregroundFetchWaitCount);
        RefreshPollDeadline();
    }

    internal void EndForegroundFetchWait()
    {
        RefreshPollDeadline();
        Interlocked.Decrement(ref _foregroundFetchWaitCount);
    }

    private void RefreshPollDeadline() => Volatile.Write(ref _lastPollTimestamp, Stopwatch.GetTimestamp());

    private void ThrowIfMaxPollIntervalExpired()
    {
        if (!IsMaxPollIntervalExpired())
            return;

        throw new GroupException(
            ErrorCode.FencedMemberEpoch,
            $"Offset commit rejected because maximum poll interval of {_options.MaxPollIntervalMs}ms was exceeded")
        {
            GroupId = _options.GroupId
        };
    }

    private bool IsMaxPollIntervalExpired()
    {
        if (Volatile.Read(ref _maxPollExpiredAtPollVersion) >= 0)
            return true;

        if (Volatile.Read(ref _foregroundFetchWaitCount) != 0)
            return false;

        return _state == CoordinatorState.Stable
            && Stopwatch.GetTimestamp() - Volatile.Read(ref _lastPollTimestamp) >= _maxPollIntervalStopwatchTicks;
    }

    internal (TopicPartitionSet Assignment, int Version, HashSet<TopicPartition>? Revocations)
        GetAssignmentSnapshotAndDrainRevocations()
    {
        lock (_assignmentStateLock)
        {
            HashSet<TopicPartition>? revoked = null;
            while (_revokedPartitionsSinceLastSync.TryDequeue(out var partition))
            {
                (revoked ??= []).Add(partition);
            }

            return (_assignedPartitions, Volatile.Read(ref _assignmentVersion), revoked);
        }
    }

    internal void RestoreRevokedPartitionsSinceLastSync(HashSet<TopicPartition> revoked)
    {
        lock (_assignmentStateLock)
        {
            EnqueueRevokedPartitions(revoked);
        }
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
    /// <see cref="EnsureActiveGroupAsync"/> call by transitioning to <see cref="CoordinatorState.Unjoined"/>.
    /// </summary>
    internal void RequestRejoin()
    {
        _state = CoordinatorState.Unjoined;
    }

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

        if (_state == CoordinatorState.Stable && SubscriptionMatches(topics, subscribedTopicRegex))
            return;

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

    private async ValueTask StoreFatalHeartbeatExceptionAsync(GroupException exception)
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
        var retryDelayMs = 100;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            // Cycle through brokers on retries to avoid wasting all attempts on one slow broker.
            var broker = brokers[attempt % brokers.Count];
            using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                broker.NodeId,
                _getCoordinationConnectionIndex(),
                cancellationToken)
                .ConfigureAwait(false);
            var connection = connectionLease.Connection;

            // Use negotiated API version
            var findCoordinatorVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.FindCoordinator,
                FindCoordinatorRequest.LowestSupportedVersion,
                FindCoordinatorRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                request,
                findCoordinatorVersion,
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
                LogCoordinatorNotAvailableRetry(attempt + 1, maxRetries, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs = Math.Min(retryDelayMs * 2, 1000); // Exponential backoff, max 1s
                continue;
            }

            if (errorCode != ErrorCode.None)
            {
                throw new Errors.GroupException(errorCode, $"FindCoordinator failed: {errorCode}")
                {
                    GroupId = _options.GroupId
                };
            }

            _coordinatorId = nodeId;
            _connectionPool.RegisterBroker(nodeId, host, port);

            LogFoundCoordinator(_coordinatorId, _options.GroupId!);
            return;
        }

        throw new Errors.GroupException(ErrorCode.CoordinatorNotAvailable,
            $"FindCoordinator failed after {maxRetries} retries: CoordinatorNotAvailable")
        {
            GroupId = _options.GroupId
        };
    }

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
        CancellationToken cancellationToken,
        long? maxPollExpirationVersion = null)
    {
        try
        {
            ValueTask callbackTask;
            if (maxPollExpirationVersion is { } expectedVersion)
            {
                lock (_heartbeatResponsePublicationGate)
                {
                    if (_state != CoordinatorState.Stable ||
                        Volatile.Read(ref _maxPollExpirationVersion) != expectedVersion)
                        return;

                    LogRebalanceListenerCall(callbackName, partitions.Count);
                    callbackTask = callback(listener, partitions, cancellationToken);
                }
            }
            else
            {
                LogRebalanceListenerCall(callbackName, partitions.Count);
                callbackTask = callback(listener, partitions, cancellationToken);
            }

            await callbackTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRebalanceListenerCallbackError(callbackName, ex);
        }
    }

    private async ValueTask InvokeRebalanceListenersAsync(
        string callbackName,
        IReadOnlyList<TopicPartition> partitions,
        Func<IRebalanceListener, IEnumerable<TopicPartition>, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        long? maxPollExpirationVersion = null)
    {
        var configuredListener = _rebalanceListener;
        if (configuredListener is not null)
        {
            await InvokeRebalanceListenerAsync(
                callbackName,
                partitions,
                configuredListener,
                callback,
                cancellationToken,
                maxPollExpirationVersion).ConfigureAwait(false);
        }

        var runtimeListener = Volatile.Read(ref _runtimeRebalanceListener);
        if (runtimeListener is not null)
        {
            await InvokeRebalanceListenerAsync(
                callbackName,
                partitions,
                runtimeListener,
                callback,
                cancellationToken,
                maxPollExpirationVersion).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Commits offsets for the group.
    /// </summary>
    public async ValueTask CommitOffsetsAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.GroupId))
            return;

        ThrowIfMaxPollIntervalExpired();

        LogCommitOffsetsStarted(_options.GroupId!);
        // Lock is intentionally held across retries to protect the shared _commitTopicGroups dictionary,
        // which is reused across calls to avoid allocations. With up to 3 retries x ~600ms each,
        // the lock can be held for up to ~1.8 seconds. Concurrent callers will block for the full retry duration.
        await _commitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RetryHelper.WithRetryAsync(async () =>
            {
                // Expiration can happen while this call waits for the commit lock.
                ThrowIfMaxPollIntervalExpired();

                using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                    _coordinatorId,
                    _getCoordinationConnectionIndex(),
                    cancellationToken)
                    .ConfigureAwait(false);
                var connection = connectionLease.Connection;

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
                        CommittedLeaderEpoch = offset.LeaderEpoch
                    });
                }

                RemoveEmptyTopicGroups(_commitTopicGroups);

                var topicOffsets = new List<OffsetCommitRequestTopic>(_commitTopicGroups.Count);
                foreach (var kvp in _commitTopicGroups)
                {
                    topicOffsets.Add(new OffsetCommitRequestTopic
                    {
                        Name = kvp.Key,
                        Partitions = kvp.Value
                    });
                }

                var request = new OffsetCommitRequest
                {
                    GroupId = _options.GroupId!,
                    GenerationIdOrMemberEpoch = _generationId,
                    MemberId = _memberId,
                    GroupInstanceId = _options.GroupInstanceId,
                    Topics = topicOffsets
                };

                // Use negotiated API version
                var offsetCommitVersion = _metadataManager.GetNegotiatedApiVersion(
                    ApiKey.OffsetCommit,
                    OffsetCommitRequest.LowestSupportedVersion,
                    OffsetCommitRequest.HighestSupportedVersion);

                ThrowIfMaxPollIntervalExpired();
                var response = await connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                    request,
                    offsetCommitVersion,
                    cancellationToken).ConfigureAwait(false);

                // Check for errors
                foreach (var topic in response.Topics)
                {
                    foreach (var partition in topic.Partitions)
                    {
                        if (partition.ErrorCode != ErrorCode.None)
                        {
                            throw new Errors.GroupException(partition.ErrorCode,
                                $"OffsetCommit failed for {topic.Name}-{partition.PartitionIndex}: {partition.ErrorCode}")
                            {
                                GroupId = _options.GroupId
                            };
                        }
                    }
                }
            }, _metadataManager, cancellationToken, onRetry: FindCoordinatorAsync).ConfigureAwait(false);
        }
        finally
        {
            _commitLock.Release();
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

        // If coordinator hasn't been discovered yet, return empty (no committed offsets known)
        if (_coordinatorId < 0)
            return new Dictionary<TopicPartition, TopicPartitionOffset>();

        // Lock is intentionally held across retries to protect the shared _fetchTopicGroups dictionary,
        // which is reused across calls to avoid allocations. With up to 3 retries x ~600ms each,
        // the lock can be held for up to ~1.8 seconds. Concurrent callers will block for the full retry duration.
        await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RetryHelper.WithRetryAsync(async () =>
            {
                using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                    _coordinatorId,
                    _getCoordinationConnectionIndex(),
                    cancellationToken)
                    .ConfigureAwait(false);
                var connection = connectionLease.Connection;

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

                var topicPartitions = new List<OffsetFetchRequestTopic>(_fetchTopicGroups.Count);
                foreach (var kvp in _fetchTopicGroups)
                {
                    topicPartitions.Add(new OffsetFetchRequestTopic
                    {
                        Name = kvp.Key,
                        PartitionIndexes = kvp.Value
                    });
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
                            MemberId = _memberId,
                            MemberEpoch = _generationId,
                            Topics = topicPartitions
                        }
                    ]
                };

                // Use negotiated API version
                var offsetFetchVersion = _metadataManager.GetNegotiatedApiVersion(
                    ApiKey.OffsetFetch,
                    OffsetFetchRequest.LowestSupportedVersion,
                    OffsetFetchRequest.HighestSupportedVersion);

                var response = await connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                    request,
                    offsetFetchVersion,
                    cancellationToken).ConfigureAwait(false);

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
                                        partition.CommittedLeaderEpoch);
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
                            HandleOffsetFetchMembershipError(group.ErrorCode);
                            throw new GroupException(group.ErrorCode,
                                $"OffsetFetch failed for group: {group.ErrorCode}")
                            {
                                GroupId = _options.GroupId
                            };
                        }

                        foreach (var topic in group.Topics)
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
                                            partition.CommittedLeaderEpoch);
                                }
                            }
                        }
                    }
                }

                return result;
            }, _metadataManager, cancellationToken, onRetry: FindCoordinatorAsync).ConfigureAwait(false);
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private void HandleOffsetFetchMembershipError(ErrorCode errorCode)
    {
        switch (errorCode)
        {
            case ErrorCode.StaleMemberEpoch:
                RequestRejoin();
                break;

            case ErrorCode.UnknownMemberId:
                ResetMemberState();
                break;
        }
    }

    private readonly record struct ConsumerHeartbeatResult(
        bool AssignmentChanged,
        IReadOnlyList<TopicPartition>? Revoked,
        IReadOnlyList<TopicPartition>? Assigned);

    /// <summary>
    /// Resets member identity and assignment to the pre-join state.
    /// Used during leave, fencing, and disposal.
    /// </summary>
    private void ResetMemberState()
    {
        _memberId = null;
        _generationId = -1;
        _state = CoordinatorState.Unjoined;
        ClearAssignment();
    }

    private IReadOnlyList<TopicPartition>? ClearAssignment()
    {
        var revoked = _assignedPartitions.Count != 0 ? _assignedPartitions.ToList() : null;

        lock (_assignmentStateLock)
        {
            _assignedPartitions = [];
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
        CancellationToken cancellationToken,
        long? maxPollExpirationVersion = null)
    {
        if (!result.AssignmentChanged)
            return;

        if (result.Revoked is { Count: > 0 })
            await InvokeRebalanceListenersAsync(
                "OnPartitionsRevoked",
                result.Revoked,
                static (listener, partitions, token) => listener.OnPartitionsRevokedAsync(partitions, token),
                cancellationToken,
                maxPollExpirationVersion).ConfigureAwait(false);

        if (result.Assigned is { Count: > 0 })
            await InvokeRebalanceListenersAsync(
                "OnPartitionsAssigned",
                result.Assigned,
                static (listener, partitions, token) => listener.OnPartitionsAssignedAsync(partitions, token),
                cancellationToken,
                maxPollExpirationVersion).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a ConsumerGroupHeartbeat request and processes the response.
    /// </summary>
    private async ValueTask<ConsumerHeartbeatResult> SendConsumerGroupHeartbeatAsync(
        bool isInitial,
        bool discardIfMembershipChanged,
        CancellationToken cancellationToken)
    {
        var maxPollExpirationVersion = discardIfMembershipChanged
            ? Volatile.Read(ref _maxPollExpirationVersion)
            : 0;
        using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
            _coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
            .ConfigureAwait(false);
        var connection = connectionLease.Connection;

        if (discardIfMembershipChanged &&
            (_state != CoordinatorState.Stable ||
             Volatile.Read(ref _maxPollExpirationVersion) != maxPollExpirationVersion ||
             IsMaxPollIntervalExpired()))
            return default;

        var version = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ConsumerGroupHeartbeat,
            ConsumerGroupHeartbeatRequest.LowestSupportedVersion,
            ConsumerGroupHeartbeatRequest.HighestSupportedVersion);

        // KIP-1082: v1+ uses client-generated UUID v4 instead of empty string for new members.
        // Generate once when _memberId is null; subsequent heartbeats reuse the stored ID.
        // Thread-safety: _memberId is only null on the initial join path (protected by _lock)
        // or after ResetMemberState() which also transitions to Unjoined before any heartbeat loop restart.
        if (_memberId is null && version >= 1)
            _memberId = Guid.NewGuid().ToString();

        var memberId = _memberId ?? string.Empty;

        // MemberEpoch: 0 for initial join, -2 for static rejoin (set by fencing handler),
        // or the current epoch for steady-state heartbeats
        var memberEpoch = isInitial
            ? (_generationId == -2 && _options.GroupInstanceId is not null ? -2 : 0)
            : _generationId;

        // On initial join, send empty array (owns nothing). null means "unchanged" in KIP-848
        // which is invalid when there's no previous state.
        var assignmentVersion = Volatile.Read(ref _assignmentVersion);
        IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? ownedTopicPartitions =
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
        var subscribedTopicRegex = subscriptionShouldBeSent && version >= 1
            ? currentSubscribedTopicRegex ?? (isInitial ? null : string.Empty)
            : null;

        var request = new ConsumerGroupHeartbeatRequest
        {
            GroupId = _options.GroupId!,
            MemberId = memberId,
            MemberEpoch = memberEpoch,
            InstanceId = _options.GroupInstanceId,
            RebalanceTimeoutMs = isInitial ? _options.MaxPollIntervalMs : -1,
            RackId = isInitial ? _options.ClientRack : null,
            SubscribedTopicNames = subscribedTopics,
            SubscribedTopicRegex = subscribedTopicRegex,
            ServerAssignor = isInitial ? _options.GroupRemoteAssignor : null,
            TopicPartitions = ownedTopicPartitions
        };

        var response = await connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            request, version, cancellationToken).ConfigureAwait(false);

        if (!discardIfMembershipChanged)
            return ProcessConsumerGroupHeartbeatResponse(
                response,
                isInitial,
                ownedTopicPartitions,
                assignmentVersion,
                subscribedTopicRegex);

        ConsumerHeartbeatResult result;
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A foreground poll can expire the member while this request is in flight.
            // Validate and publish under the same lock used by expiry so it cannot clear
            // membership between this guard and the epoch/assignment writes below.
            if (_state != CoordinatorState.Stable ||
                Volatile.Read(ref _maxPollExpirationVersion) != maxPollExpirationVersion ||
                IsMaxPollIntervalExpired())
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

        await FireConsumerProtocolRebalanceListenersAsync(
            result,
            cancellationToken,
            maxPollExpirationVersion).ConfigureAwait(false);

        // Steady-heartbeat callbacks are fired above while publication is fenced against
        // max-poll expiry. The heartbeat loop must not fire the result a second time.
        return default;
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
        if (changed)
        {
            lock (_assignmentStateLock)
            {
                _assignedPartitions = newAssignment;
                Interlocked.Increment(ref _assignmentVersion);

                if (revoked is not null)
                    EnqueueRevokedPartitions(revoked);
            }

            if (revoked is not null)
                _onPartitionsRevoked?.Invoke(revoked);

            LogConsumerProtocolAssignmentUpdate(assigned?.Count ?? 0, revoked?.Count ?? 0);
        }

        return new ConsumerHeartbeatResult(changed, revoked, assigned);
    }

    /// <summary>
    /// KIP-848 entry point: ensures the consumer has joined the group using the ConsumerGroupHeartbeat API.
    /// </summary>
    private async ValueTask EnsureActiveGroupConsumerProtocolAsync(
        StringSet topics,
        string? subscribedTopicRegex,
        CancellationToken cancellationToken)
    {
        if (!_metadataManager.HasApiKey(ApiKey.ConsumerGroupHeartbeat))
        {
            throw new BrokerVersionException(
                "The connected Kafka broker does not support the ConsumerGroupHeartbeat API " +
                "(KIP-848, introduced in Kafka 4.0). Dekaf's consumer requires Kafka 4.0 or later.");
        }

        if (subscribedTopicRegex is not null &&
            !_metadataManager.SupportsApiVersion(ApiKey.ConsumerGroupHeartbeat, 1))
        {
            throw new BrokerVersionException(
                "Server-side regex subscriptions require ConsumerGroupHeartbeat v1 " +
                "(Kafka 4.1 or later). Use Subscribe(Func<string, bool>) for client-side filtering on older brokers.");
        }

        UpdateSubscription(topics, subscribedTopicRegex);

        ConsumerHeartbeatResult heartbeatResult = default;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfFatalHeartbeatException();

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

            if (_state == CoordinatorState.Stable)
                return;

            LogEnsureActiveGroupStarted(_options.GroupId!, _state);
            var startedAt = Stopwatch.GetTimestamp();
            var rebalanceTimeout = TimeSpan.FromMilliseconds(_options.RebalanceTimeoutMs);
            var retryDelayMs = 200;

            while (_state != CoordinatorState.Stable)
            {
                if (Stopwatch.GetElapsedTime(startedAt) > rebalanceTimeout)
                {
                    throw new KafkaTimeoutException(
                        TimeoutKind.Rebalance,
                        Stopwatch.GetElapsedTime(startedAt),
                        rebalanceTimeout,
                        $"Failed to join group '{_options.GroupId}' within rebalance timeout ({_options.RebalanceTimeoutMs}ms)");
                }

                try
                {
                    if (_coordinatorId < 0)
                    {
                        await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                    }

                    _state = CoordinatorState.Joining;
                    LogCoordinatorStateTransition(CoordinatorState.Joining);

                    heartbeatResult = await SendConsumerGroupHeartbeatAsync(
                        isInitial: _memberId is null || _generationId <= 0,
                        discardIfMembershipChanged: false,
                        cancellationToken).ConfigureAwait(false);

                    _state = CoordinatorState.Stable;
                    RefreshPollDeadline();
                    Volatile.Write(ref _maxPollExpiredAtPollVersion, -1);

                    Diagnostics.DekafMetrics.RebalanceDuration.Record(
                        Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                        new System.Diagnostics.TagList
                            { { Diagnostics.DekafDiagnostics.MessagingConsumerGroupName, _options.GroupId } });

                    LogJoinedGroup(_options.GroupId!, _memberId!, _generationId);
                }
                catch (Errors.GroupException ex) when (ex.ErrorCode == ErrorCode.FencedMemberEpoch)
                {
                    // Stale epoch — reset generation so next attempt sends MemberEpoch=0 (or -2 for static members)
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    _generationId = _options.GroupInstanceId is not null ? -2 : 0;
                    _state = CoordinatorState.Unjoined;
                }
                catch (Errors.GroupException ex) when (ex.ErrorCode == ErrorCode.UnreleasedInstanceId)
                {
                    // Static member's previous session not yet released — retry with backoff
                    // until the rebalance timeout fires (checked at top of loop)
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    retryDelayMs = Math.Min(retryDelayMs * 2, 2000);
                }
                catch (Errors.GroupException ex) when (ex.ErrorCode == ErrorCode.UnknownMemberId)
                {
                    // Broker forgot this member (e.g. broker restart after fencing) — full reset and retry
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    ResetMemberState();
                }
                catch (Errors.GroupException ex) when (IsRetriableCoordinatorError(ex.ErrorCode))
                {
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    MarkCoordinatorUnknown();
                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    retryDelayMs = Math.Min(retryDelayMs * 2, 2000);
                }
                catch (Exception ex) when (
                    ex is ObjectDisposedException ||
                    (ex is Errors.KafkaException ke && ke is not Errors.GroupException && !cancellationToken.IsCancellationRequested))
                {
                    LogCoordinatorConnectionDisposed();
                    MarkCoordinatorUnknown();
                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    retryDelayMs = Math.Min(retryDelayMs * 2, 2000);
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        await FireConsumerProtocolRebalanceListenersAsync(heartbeatResult, cancellationToken).ConfigureAwait(false);

        if (_state == CoordinatorState.Stable)
        {
            await StartConsumerProtocolHeartbeatAsync().ConfigureAwait(false);
        }
    }

    private ValueTask StartConsumerProtocolHeartbeatAsync()
        => StartHeartbeatCoreAsync(ConsumerProtocolHeartbeatLoopAsync, _heartbeatIntervalMs);

    /// <summary>
    /// KIP-848 heartbeat loop: sends ConsumerGroupHeartbeat at the broker-specified interval,
    /// handles assignment changes and errors.
    /// </summary>
    private async Task ConsumerProtocolHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Task.Delay (not PeriodicTimer): the broker controls the interval via each response,
                // and PeriodicTimer cannot change its period after construction. Wake at the
                // max.poll deadline when it is sooner so eviction is not heartbeat-interval late.
                await Task.Delay(GetConsumerProtocolHeartbeatDelay(), cancellationToken).ConfigureAwait(false);

                var expiration = await TryExpireMaxPollIntervalAsync(cancellationToken).ConfigureAwait(false);
                if (expiration.Expired)
                {
                    await CompleteMaxPollExpirationAsync(expiration.Lost).ConfigureAwait(false);

                    break;
                }

                // A foreground poll may have expired the member while this loop waited for _lock.
                if (_state != CoordinatorState.Stable)
                    break;

                await SendConsumerGroupHeartbeatAsync(
                    isInitial: false,
                    discardIfMembershipChanged: true,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogHeartbeatFailed(ex);

                if (ex is Errors.GroupException ge)
                {
                    switch (ge.ErrorCode)
                    {
                        case ErrorCode.FencedMemberEpoch:
                            // Reset generation so next EnsureActiveGroup sends MemberEpoch=0 (or -2 for static)
                            _generationId = _options.GroupInstanceId is not null ? -2 : 0;
                            _state = CoordinatorState.Unjoined;
                            break;

                        case ErrorCode.UnknownMemberId:
                            var lost = _assignedPartitions.ToList();
                            if (lost.Count > 0)
                            {
                                await InvokeRebalanceListenersAsync(
                                    "OnPartitionsLost",
                                    lost,
                                    static (listener, partitions, token) => listener.OnPartitionsLostAsync(partitions, token),
                                    CancellationToken.None).ConfigureAwait(false);
                            }

                            ResetMemberState();
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

                // Non-group error — continue heartbeating
            }
        }
    }

    private TimeSpan GetConsumerProtocolHeartbeatDelay()
    {
        var heartbeatDelay = TimeSpan.FromMilliseconds(Math.Max(_heartbeatIntervalMs, 1));
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

            if (Volatile.Read(ref _foregroundFetchWaitCount) != 0)
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
            IReadOnlyList<TopicPartition>? lost;
            lock (_heartbeatResponsePublicationGate)
            {
                Interlocked.Increment(ref _maxPollExpirationVersion);
                Volatile.Write(ref _maxPollExpiredAtPollVersion, pollVersion);
                lost = ClearAssignment();
                Volatile.Write(ref _maxPollLossNotificationPending, 1);
                _state = CoordinatorState.Unjoined;
            }

            LogMaxPollIntervalExceeded(_options.MaxPollIntervalMs);
            await SendConsumerProtocolLeaveRequestAsync(cancellationToken).ConfigureAwait(false);
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
            await InvokePartitionsLostAsync(lost).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _maxPollLossNotificationPending, 0);
        }
    }

    private ValueTask InvokePartitionsLostAsync(IReadOnlyList<TopicPartition>? lost)
        => lost is { Count: > 0 }
            ? InvokeRebalanceListenersAsync(
                "OnPartitionsLost",
                lost,
                static (listener, partitions, token) => listener.OnPartitionsLostAsync(partitions, token),
                CancellationToken.None)
            : ValueTask.CompletedTask;

    /// <summary>
    /// KIP-848 leave: sends ConsumerGroupHeartbeat with MemberEpoch=-1 for dynamic members,
    /// or -2 for static members so the broker keeps the assignment warm.
    /// </summary>
    private async ValueTask LeaveGroupConsumerProtocolAsync(CancellationToken cancellationToken)
    {
        await SendConsumerProtocolLeaveRequestAsync(cancellationToken).ConfigureAwait(false);

        await StopHeartbeatAsync().ConfigureAwait(false);

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            ResetMemberState();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async ValueTask SendConsumerProtocolLeaveRequestAsync(CancellationToken cancellationToken)
    {
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
                MemberEpoch = _options.GroupInstanceId is null ? -1 : -2,
                InstanceId = _options.GroupInstanceId,
            };

            var version = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.ConsumerGroupHeartbeat,
                ConsumerGroupHeartbeatRequest.LowestSupportedVersion,
                ConsumerGroupHeartbeatRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                request, version, cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode != ErrorCode.None)
            {
                LogLeaveGroupFailed(response.ErrorCode);
            }
            else
            {
                LogSuccessfullyLeftGroup(_options.GroupId!);
            }
        }
        catch (Exception ex)
        {
            LogLeaveGroupRequestFailed(ex);
        }
    }

    /// <summary>
    /// Leaves the consumer group gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask LeaveGroupAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        // Only leave if we're part of a group
        if (string.IsNullOrEmpty(_options.GroupId) || string.IsNullOrEmpty(_memberId))
            return;

        // If coordinator is unknown, we can't send LeaveGroup
        if (_coordinatorId < 0)
            return;

        await LeaveGroupConsumerProtocolAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the heartbeat background task.
    /// </summary>
    public async ValueTask StopHeartbeatAsync()
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
                await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator not available (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms")]
    private partial void LogCoordinatorNotAvailableRetry(int attempt, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found coordinator {NodeId} for group {GroupId}")]
    private partial void LogFoundCoordinator(int nodeId, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat failed")]
    private partial void LogHeartbeatFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Maximum poll interval of {MaxPollIntervalMs}ms exceeded; leaving consumer group")]
    private partial void LogMaxPollIntervalExceeded(int maxPollIntervalMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LeaveGroup failed with error: {ErrorCode}")]
    private partial void LogLeaveGroupFailed(ErrorCode errorCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully left group {GroupId}")]
    private partial void LogSuccessfullyLeftGroup(string groupId);

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
