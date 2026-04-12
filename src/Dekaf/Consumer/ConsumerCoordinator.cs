using System.Diagnostics;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Retry;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger _logger;

    private volatile int _coordinatorId = -1;
    private volatile string? _memberId;
    private volatile int _generationId = -1;
    // Volatile ensures cross-thread visibility of the reference. Thread-safety relies on
    // all writes replacing the reference entirely (never in-place mutation) — verified at
    // every assignment site: ProcessConsumerGroupAssignment() and DisposeAsync().
    private volatile HashSet<TopicPartition> _assignedPartitions = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _heartbeatGuard = new();
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    // Pooled dictionaries to avoid allocations in hot paths (protected by _lock or method-local usage)
    private readonly Dictionary<string, List<OffsetCommitRequestPartition>> _commitTopicGroups = new();
    private readonly SemaphoreSlim _commitLock = new(1, 1);
    private readonly Dictionary<string, List<int>> _fetchTopicGroups = new();
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    private volatile CoordinatorState _state = CoordinatorState.Unjoined;
    private int _disposed;
    private readonly Func<int> _getCoordinationConnectionIndex;

    // KIP-848 consumer protocol state
    private volatile int _heartbeatIntervalMs;
    private int _subscriptionChanged; // 0 = false, 1 = true; use Interlocked.Exchange for atomic snapshot
    private volatile IReadOnlySet<string>? _subscribedTopics;

    internal static int GetCoordinationConnectionIndex(int connectionsPerBroker)
        => connectionsPerBroker - 1;

    public ConsumerCoordinator(
        ConsumerOptions options,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILogger<ConsumerCoordinator>? logger = null,
        Func<int>? getConnectionCount = null)
    {
        _options = options;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _rebalanceListener = options.RebalanceListener;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConsumerCoordinator>.Instance;
        _getCoordinationConnectionIndex = getConnectionCount is not null
            ? () => GetCoordinationConnectionIndex(getConnectionCount())
            : () => GetCoordinationConnectionIndex(options.ConnectionsPerBroker);
        _heartbeatIntervalMs = options.HeartbeatIntervalMs;
    }

    public string? MemberId => _memberId;
    public int GenerationId => _generationId;
    public CoordinatorState State => _state;
    public IReadOnlySet<TopicPartition> Assignment => _assignedPartitions;

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
    public async ValueTask EnsureActiveGroupAsync(
        IReadOnlySet<string> topics,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ConsumerCoordinator));

        if (string.IsNullOrEmpty(_options.GroupId))
            return;

        if (_state == CoordinatorState.Stable)
            return;

        await EnsureActiveGroupConsumerProtocolAsync(topics, cancellationToken).ConfigureAwait(false);
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
            var connection = await _connectionPool.GetConnectionByIndexAsync(broker.NodeId, _getCoordinationConnectionIndex(), cancellationToken)
                .ConfigureAwait(false);

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
        Func<IEnumerable<TopicPartition>, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken)
    {
        LogRebalanceListenerCall(callbackName, partitions.Count);
        try
        {
            await callback(partitions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRebalanceListenerCallbackError(callbackName, ex);
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

        LogCommitOffsetsStarted(_options.GroupId!);
        // Lock is intentionally held across retries to protect the shared _commitTopicGroups dictionary,
        // which is reused across calls to avoid allocations. With up to 3 retries x ~600ms each,
        // the lock can be held for up to ~1.8 seconds. Concurrent callers will block for the full retry duration.
        await _commitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RetryHelper.WithRetryAsync(async () =>
            {
                var connection = await _connectionPool.GetConnectionByIndexAsync(_coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
                    .ConfigureAwait(false);

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
                        CommittedOffset = offset.Offset
                    });
                }

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
                    GroupId = _options.GroupId,
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
    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> FetchOffsetsAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.GroupId))
            return new Dictionary<TopicPartition, long>();

        // If coordinator hasn't been discovered yet, return empty (no committed offsets known)
        if (_coordinatorId < 0)
            return new Dictionary<TopicPartition, long>();

        // Lock is intentionally held across retries to protect the shared _fetchTopicGroups dictionary,
        // which is reused across calls to avoid allocations. With up to 3 retries x ~600ms each,
        // the lock can be held for up to ~1.8 seconds. Concurrent callers will block for the full retry duration.
        await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RetryHelper.WithRetryAsync(async () =>
            {
                var connection = await _connectionPool.GetConnectionByIndexAsync(_coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
                    .ConfigureAwait(false);

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
                    GroupId = _options.GroupId,
                    Topics = topicPartitions
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

                var result = new Dictionary<TopicPartition, long>();

                // v6-v7: Topics field
                if (response.Topics is not null)
                {
                    foreach (var topic in response.Topics)
                    {
                        foreach (var partition in topic.Partitions)
                        {
                            if (partition.ErrorCode != ErrorCode.None)
                            {
                                if (partition.ErrorCode.IsRetriable())
                                {
                                    throw new GroupException(partition.ErrorCode,
                                        $"OffsetFetch failed for {topic.Name}-{partition.PartitionIndex}: {partition.ErrorCode}")
                                    {
                                        GroupId = _options.GroupId
                                    };
                                }
                                continue;
                            }

                            if (partition.CommittedOffset >= 0)
                            {
                                result[new TopicPartition(topic.Name, partition.PartitionIndex)] = partition.CommittedOffset;
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
                            if (group.ErrorCode.IsRetriable())
                            {
                                throw new GroupException(group.ErrorCode,
                                    $"OffsetFetch failed for group: {group.ErrorCode}")
                                {
                                    GroupId = _options.GroupId
                                };
                            }
                            continue;
                        }

                        foreach (var topic in group.Topics)
                        {
                            foreach (var partition in topic.Partitions)
                            {
                                if (partition.ErrorCode != ErrorCode.None)
                                {
                                    if (partition.ErrorCode.IsRetriable())
                                    {
                                        throw new GroupException(partition.ErrorCode,
                                            $"OffsetFetch failed for {topic.Name}-{partition.PartitionIndex}: {partition.ErrorCode}")
                                        {
                                            GroupId = _options.GroupId
                                        };
                                    }
                                    continue;
                                }

                                if (partition.CommittedOffset >= 0)
                                {
                                    result[new TopicPartition(topic.Name, partition.PartitionIndex)] = partition.CommittedOffset;
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
        _assignedPartitions = [];
        _state = CoordinatorState.Unjoined;
    }

    /// <summary>
    /// Fires rebalance listener callbacks for a ConsumerHeartbeatResult if assignment changed.
    /// </summary>
    private async ValueTask FireConsumerProtocolRebalanceListenersAsync(
        ConsumerHeartbeatResult result,
        CancellationToken cancellationToken)
    {
        if (!result.AssignmentChanged || _rebalanceListener is null)
            return;

        if (result.Revoked is { Count: > 0 })
        {
            await InvokeRebalanceListenerAsync(
                "OnPartitionsRevoked", result.Revoked,
                _rebalanceListener.OnPartitionsRevokedAsync, cancellationToken).ConfigureAwait(false);
        }

        if (result.Assigned is { Count: > 0 })
        {
            await InvokeRebalanceListenerAsync(
                "OnPartitionsAssigned", result.Assigned,
                _rebalanceListener.OnPartitionsAssignedAsync, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends a ConsumerGroupHeartbeat request and processes the response.
    /// </summary>
    private async ValueTask<ConsumerHeartbeatResult> SendConsumerGroupHeartbeatAsync(
        bool isInitial,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionByIndexAsync(
            _coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
            .ConfigureAwait(false);

        var memberId = _memberId ?? string.Empty;

        // MemberEpoch: 0 for initial join, -2 for static rejoin (set by fencing handler),
        // or the current epoch for steady-state heartbeats
        var memberEpoch = isInitial
            ? (_generationId == -2 && _options.GroupInstanceId is not null ? -2 : 0)
            : _generationId;

        // On initial join, send empty array (owns nothing). null means "unchanged" in KIP-848
        // which is invalid when there's no previous state.
        IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? ownedTopicPartitions =
            isInitial ? [] : BuildOwnedTopicPartitions(_assignedPartitions);

        // Atomically snapshot and clear the subscription-changed flag to prevent a race where
        // a concurrent EnsureActiveGroupConsumerProtocolAsync sets new topics + flag=true,
        // but this heartbeat clears the flag after sending the old topics.
        // Always send topics on initial/re-join — KIP-848 requires SubscribedTopicNames to be
        // non-null when joining. The flag must still be cleared to avoid a stale re-send later.
        var subscriptionChanged = Interlocked.Exchange(ref _subscriptionChanged, 0) == 1;
        var subscribedTopics = (isInitial || subscriptionChanged) ? _subscribedTopics?.ToList() : null;

        var request = new ConsumerGroupHeartbeatRequest
        {
            GroupId = _options.GroupId!,
            MemberId = memberId,
            MemberEpoch = memberEpoch,
            InstanceId = _options.GroupInstanceId,
            RebalanceTimeoutMs = isInitial ? _options.RebalanceTimeoutMs : -1,
            SubscribedTopicNames = subscribedTopics,
            ServerAssignor = isInitial ? _options.GroupRemoteAssignor : null,
            TopicPartitions = ownedTopicPartitions
        };

        var version = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ConsumerGroupHeartbeat,
            ConsumerGroupHeartbeatRequest.LowestSupportedVersion,
            ConsumerGroupHeartbeatRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            request, version, cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != ErrorCode.None)
        {
            HandleConsumerGroupHeartbeatError(response);
        }

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
    private void HandleConsumerGroupHeartbeatError(ConsumerGroupHeartbeatResponse response)
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

        _assignedPartitions = newAssignment;

        var changed = revoked is { Count: > 0 } || assigned is { Count: > 0 };
        if (changed)
        {
            LogConsumerProtocolAssignmentUpdate(assigned?.Count ?? 0, revoked?.Count ?? 0);
        }

        return new ConsumerHeartbeatResult(changed, revoked, assigned);
    }

    /// <summary>
    /// KIP-848 entry point: ensures the consumer has joined the group using the ConsumerGroupHeartbeat API.
    /// </summary>
    private async ValueTask EnsureActiveGroupConsumerProtocolAsync(
        IReadOnlySet<string> topics,
        CancellationToken cancellationToken)
    {
        if (_metadataManager.HasNegotiatedVersions && !_metadataManager.HasApiKey(ApiKey.ConsumerGroupHeartbeat))
        {
            throw new BrokerVersionException(
                "The connected Kafka broker does not support the ConsumerGroupHeartbeat API " +
                "(KIP-848, introduced in Kafka 4.0). Dekaf's consumer requires Kafka 4.0 or later.");
        }

        _subscribedTopics = topics;
        Interlocked.Exchange(ref _subscriptionChanged, 1);

        ConsumerHeartbeatResult heartbeatResult = default;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
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
                        cancellationToken).ConfigureAwait(false);

                    _state = CoordinatorState.Stable;

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
                // and PeriodicTimer cannot change its period after construction. At ~5s intervals,
                // the per-tick TimerQueueTimer allocation is negligible.
                await Task.Delay(_heartbeatIntervalMs, cancellationToken).ConfigureAwait(false);

                var result = await SendConsumerGroupHeartbeatAsync(
                    isInitial: false, cancellationToken).ConfigureAwait(false);

                await FireConsumerProtocolRebalanceListenersAsync(result, cancellationToken)
                    .ConfigureAwait(false);
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
                            if (_rebalanceListener is not null)
                            {
                                var lost = _assignedPartitions.ToList();
                                if (lost.Count > 0)
                                {
                                    await InvokeRebalanceListenerAsync(
                                        "OnPartitionsLost", lost,
                                        _rebalanceListener.OnPartitionsLostAsync,
                                        CancellationToken.None).ConfigureAwait(false);
                                }
                            }

                            ResetMemberState();
                            break;

                        case ErrorCode.UnreleasedInstanceId:
                        case ErrorCode.UnsupportedAssignor:
                            _state = CoordinatorState.Unjoined;
                            break;

                        case var c when IsRetriableCoordinatorError(c):
                            MarkCoordinatorUnknown();
                            break;

                        default:
                            continue; // Unrecognized group error — continue heartbeating
                    }

                    break;
                }

                // Non-group error — continue heartbeating
            }
        }
    }

    /// <summary>
    /// KIP-848 leave: sends ConsumerGroupHeartbeat with MemberEpoch=-1.
    /// </summary>
    private async ValueTask LeaveGroupConsumerProtocolAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _connectionPool.GetConnectionByIndexAsync(
                _coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
                .ConfigureAwait(false);

            var request = new ConsumerGroupHeartbeatRequest
            {
                GroupId = _options.GroupId!,
                MemberId = _memberId!,
                MemberEpoch = -1,
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
