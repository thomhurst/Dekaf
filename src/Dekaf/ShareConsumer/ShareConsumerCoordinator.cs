using System.Diagnostics;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Consumer;
using Microsoft.Extensions.Logging;

namespace Dekaf.ShareConsumer;

/// <summary>
/// Handles share group coordination using the ShareGroupHeartbeat API (KIP-932).
/// State machine: Unjoined → Joining → Stable. The broker performs partition assignment
/// server-side for share groups. Simpler than ConsumerCoordinator — no offset management,
/// no rebalance listener, no static member support.
/// </summary>
internal sealed partial class ShareConsumerCoordinator : IAsyncDisposable
{
    private readonly ShareConsumerOptions _options;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly ILogger _logger;

    private volatile int _coordinatorId = -1;
    private volatile string? _memberId;
    private volatile int _memberEpoch;
    // Volatile ensures cross-thread visibility of the reference. Thread-safety relies on
    // all writes replacing the reference entirely (never in-place mutation).
    private volatile HashSet<TopicPartition> _assignedPartitions = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _heartbeatGuard = new();
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    private volatile CoordinatorState _state = CoordinatorState.Unjoined;
    private int _disposed;
    private readonly Func<int> _getCoordinationConnectionIndex;

    private volatile int _heartbeatIntervalMs;
    private int _subscriptionChanged; // 0 = false, 1 = true; use Interlocked.Exchange for atomic snapshot
    private volatile IReadOnlySet<string>? _subscribedTopics;

    internal static int GetCoordinationConnectionIndex(int connectionsPerBroker)
        => connectionsPerBroker - 1;

    public ShareConsumerCoordinator(
        ShareConsumerOptions options,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILogger? logger = null,
        Func<int>? getConnectionCount = null)
    {
        _options = options;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _getCoordinationConnectionIndex = getConnectionCount is not null
            ? () => GetCoordinationConnectionIndex(getConnectionCount())
            : () => GetCoordinationConnectionIndex(options.ConnectionsPerBroker);
        _heartbeatIntervalMs = options.HeartbeatIntervalMs;
    }

    public string? MemberId => _memberId;
    public int MemberEpoch => _memberEpoch;
    public CoordinatorState State => _state;
    public IReadOnlySet<TopicPartition> Assignment => _assignedPartitions;

    /// <summary>
    /// Forces the coordinator to rejoin the group on the next
    /// <see cref="EnsureActiveGroupAsync"/> call.
    /// </summary>
    internal void RequestRejoin()
    {
        _state = CoordinatorState.Unjoined;
    }

    /// <summary>
    /// Ensures the share consumer has joined the group.
    /// </summary>
    public async ValueTask EnsureActiveGroupAsync(
        IReadOnlySet<string> topics,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ShareConsumerCoordinator));

        if (_state == CoordinatorState.Stable)
            return;

        await EnsureActiveGroupCoreAsync(topics, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks the coordinator as unknown, forcing re-discovery on next operation.
    /// </summary>
    private void MarkCoordinatorUnknown()
    {
        _coordinatorId = -1;
        _state = CoordinatorState.Unjoined;
    }

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
            Key = _options.GroupId,
            KeyType = CoordinatorType.Group
        };

        const int maxRetries = 5;
        var retryDelayMs = 100;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var broker = brokers[attempt % brokers.Count];
            var connection = await _connectionPool.GetConnectionByIndexAsync(
                broker.NodeId, _getCoordinationConnectionIndex(), cancellationToken)
                .ConfigureAwait(false);

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
                throw new GroupException(ErrorCode.CoordinatorNotAvailable,
                    "FindCoordinator returned an empty Coordinators array")
                { GroupId = _options.GroupId };
            }

            var coordinator = response.Coordinators[0];
            var errorCode = coordinator.ErrorCode;
            var nodeId = coordinator.NodeId;
            var host = coordinator.Host;
            var port = coordinator.Port;

            if (errorCode == ErrorCode.CoordinatorNotAvailable ||
                errorCode == ErrorCode.CoordinatorLoadInProgress)
            {
                LogCoordinatorNotAvailableRetry(attempt + 1, maxRetries, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs = Math.Min(retryDelayMs * 2, 1000);
                continue;
            }

            if (errorCode != ErrorCode.None)
            {
                throw new GroupException(errorCode, $"FindCoordinator failed: {errorCode}")
                {
                    GroupId = _options.GroupId
                };
            }

            _coordinatorId = nodeId;
            _connectionPool.RegisterBroker(nodeId, host, port);

            LogFoundCoordinator(_coordinatorId, _options.GroupId);
            return;
        }

        throw new GroupException(ErrorCode.CoordinatorNotAvailable,
            $"FindCoordinator failed after {maxRetries} retries: CoordinatorNotAvailable")
        {
            GroupId = _options.GroupId
        };
    }

    /// <summary>
    /// Serializes heartbeat loop starts to prevent concurrent callers from orphaning a loop.
    /// </summary>
    private async ValueTask StartHeartbeatCoreAsync(int intervalMs)
    {
        Task? oldTask;
        CancellationTokenSource? oldCts;

        lock (_heartbeatGuard)
        {
            oldCts = _heartbeatCts;
            oldTask = _heartbeatTask;

            LogHeartbeatStarted(intervalMs);

            _heartbeatCts = new CancellationTokenSource();
            _heartbeatTask = HeartbeatLoopAsync(_heartbeatCts.Token);
        }

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

    /// <summary>
    /// Resets member identity and assignment to the pre-join state.
    /// </summary>
    private void ResetMemberState()
    {
        _memberId = null;
        _memberEpoch = 0;
        _assignedPartitions = [];
        _state = CoordinatorState.Unjoined;
    }

    /// <summary>
    /// Sends a ShareGroupHeartbeat request and processes the response.
    /// </summary>
    private async ValueTask<bool> SendShareGroupHeartbeatAsync(
        bool isInitial,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionByIndexAsync(
            _coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
            .ConfigureAwait(false);

        var version = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ShareGroupHeartbeat,
            ShareGroupHeartbeatRequest.LowestSupportedVersion,
            ShareGroupHeartbeatRequest.HighestSupportedVersion);

        // Share groups always use client-generated UUID v4 member IDs.
        // Generate once when _memberId is null; subsequent heartbeats reuse the stored ID.
        _memberId ??= Guid.NewGuid().ToString();

        var memberEpoch = isInitial ? 0 : _memberEpoch;

        // Atomically snapshot and clear the subscription-changed flag.
        // Always send topics on initial join — required by the protocol.
        var subscriptionChanged = Interlocked.Exchange(ref _subscriptionChanged, 0) == 1;
        var subscribedTopics = (isInitial || subscriptionChanged) ? _subscribedTopics?.ToList() : null;

        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = _options.GroupId,
            MemberId = _memberId,
            MemberEpoch = memberEpoch,
            RackId = isInitial ? _options.RackId : null,
            SubscribedTopicNames = subscribedTopics
        };

        var response = await connection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
            request, version, cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != ErrorCode.None)
        {
            HandleShareGroupHeartbeatError(response);
        }

        if (response.MemberId is not null)
            _memberId = response.MemberId;

        if (response.MemberEpoch != _memberEpoch)
        {
            LogMemberEpochUpdated(response.MemberEpoch);
            _memberEpoch = response.MemberEpoch;
        }

        if (response.HeartbeatIntervalMs > 0)
            _heartbeatIntervalMs = response.HeartbeatIntervalMs;

        if (response.Assignment is not null)
        {
            ProcessShareGroupAssignment(response.Assignment);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Throws an appropriate exception for ShareGroupHeartbeat error codes.
    /// Does NOT mutate coordinator state — callers own state transitions.
    /// </summary>
    private void HandleShareGroupHeartbeatError(ShareGroupHeartbeatResponse response)
    {
        throw response.ErrorCode switch
        {
            ErrorCode.UnknownMemberId => new GroupException(response.ErrorCode,
                $"ShareGroupHeartbeat: unknown member ID (fenced): {response.ErrorMessage}")
            { GroupId = _options.GroupId },

            ErrorCode.FencedMemberEpoch => new GroupException(response.ErrorCode,
                $"ShareGroupHeartbeat: fenced member epoch: {response.ErrorMessage}")
            { GroupId = _options.GroupId },

            _ => new GroupException(response.ErrorCode,
                $"ShareGroupHeartbeat failed: {response.ErrorCode} - {response.ErrorMessage}")
            { GroupId = _options.GroupId }
        };
    }

    /// <summary>
    /// Processes a ShareGroupHeartbeat assignment response, resolving topic UUIDs to names.
    /// </summary>
    private void ProcessShareGroupAssignment(ShareGroupHeartbeatAssignment assignment)
    {
        var newAssignment = new HashSet<TopicPartition>();

        foreach (var tp in assignment.TopicPartitions)
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

        var oldAssignment = _assignedPartitions;

        if (newAssignment.Count != oldAssignment.Count || !newAssignment.SetEquals(oldAssignment))
        {
            LogAssignmentUpdate(newAssignment.Count);
        }

        _assignedPartitions = newAssignment;
    }

    /// <summary>
    /// Ensures the share consumer has joined the group using the ShareGroupHeartbeat API.
    /// </summary>
    private async ValueTask EnsureActiveGroupCoreAsync(
        IReadOnlySet<string> topics,
        CancellationToken cancellationToken)
    {
        if (!_metadataManager.HasApiKey(ApiKey.ShareGroupHeartbeat))
        {
            throw new BrokerVersionException(
                "The connected Kafka broker does not support the ShareGroupHeartbeat API " +
                "(KIP-932, introduced in Kafka 4.0). Share group consumption requires Kafka 4.0 or later.");
        }

        _subscribedTopics = topics;
        Interlocked.Exchange(ref _subscriptionChanged, 1);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state == CoordinatorState.Stable)
                return;

            LogEnsureActiveGroupStarted(_options.GroupId, _state);
            var startedAt = Stopwatch.GetTimestamp();
            // Reuse SessionTimeoutMs as the client-side join deadline. This is the broker's
            // inactivity timeout (default 45s), not a dedicated join timeout — but it provides
            // a reasonable upper bound for how long we should wait for an assignment.
            var timeout = TimeSpan.FromMilliseconds(_options.SessionTimeoutMs);
            var retryDelayMs = 200;

            while (_state != CoordinatorState.Stable)
            {
                if (Stopwatch.GetElapsedTime(startedAt) > timeout)
                {
                    throw new KafkaTimeoutException(
                        TimeoutKind.Rebalance,
                        Stopwatch.GetElapsedTime(startedAt),
                        timeout,
                        $"Failed to join share group '{_options.GroupId}' within timeout ({_options.SessionTimeoutMs}ms)");
                }

                try
                {
                    if (_coordinatorId < 0)
                    {
                        await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                    }

                    _state = CoordinatorState.Joining;
                    LogCoordinatorStateTransition(CoordinatorState.Joining);

                    var gotAssignment = await SendShareGroupHeartbeatAsync(
                        isInitial: _memberEpoch <= 0,
                        cancellationToken).ConfigureAwait(false);

                    if (gotAssignment && _assignedPartitions.Count > 0)
                    {
                        _state = CoordinatorState.Stable;
                        LogJoinedGroup(_options.GroupId, _memberId!, _memberEpoch);
                    }
                    else
                    {
                        // Broker accepted us but hasn't assigned partitions yet.
                        // Wait before re-sending heartbeat.
                        LogWaitingForAssignment();
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (GroupException ex) when (ex.ErrorCode == ErrorCode.FencedMemberEpoch)
                {
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    _memberEpoch = 0;
                    _state = CoordinatorState.Unjoined;
                }
                catch (GroupException ex) when (ex.ErrorCode == ErrorCode.UnknownMemberId)
                {
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    ResetMemberState();
                }
                catch (GroupException ex) when (IsRetriableCoordinatorError(ex.ErrorCode))
                {
                    LogRetriableCoordinatorError(ex.ErrorCode);
                    MarkCoordinatorUnknown();
                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    retryDelayMs = Math.Min(retryDelayMs * 2, 2000);
                }
                catch (Exception ex) when (
                    ex is ObjectDisposedException ||
                    (ex is KafkaException ke && ke is not GroupException && !cancellationToken.IsCancellationRequested))
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

        if (_state == CoordinatorState.Stable)
        {
            await StartHeartbeatCoreAsync(_heartbeatIntervalMs).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// ShareGroupHeartbeat loop: sends heartbeats at the broker-specified interval.
    /// </summary>
    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_heartbeatIntervalMs, cancellationToken).ConfigureAwait(false);

                await SendShareGroupHeartbeatAsync(
                    isInitial: false, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogHeartbeatFailed(ex);

                if (ex is GroupException ge)
                {
                    switch (ge.ErrorCode)
                    {
                        case ErrorCode.FencedMemberEpoch:
                            // No lock needed: these writes are idempotent resets.
                            // The next EnsureActiveGroupAsync (which holds _lock) will
                            // see the Unjoined state and trigger a fresh join.
                            _memberEpoch = 0;
                            _state = CoordinatorState.Unjoined;
                            break;

                        case ErrorCode.UnknownMemberId:
                            ResetMemberState();
                            break;

                        case var c when IsRetriableCoordinatorError(c):
                            MarkCoordinatorUnknown();
                            break;

                        default:
                            // Unknown or non-retriable group error — the broker may have
                            // invalidated our session. Mark coordinator unknown and break
                            // so EnsureActiveGroupAsync re-discovers on next poll.
                            MarkCoordinatorUnknown();
                            break;
                    }
                }
                else
                {
                    // Network errors, ObjectDisposedException, etc. — mark coordinator
                    // unknown so EnsureActiveGroupAsync re-discovers on next poll.
                    MarkCoordinatorUnknown();
                }

                break;
            }
        }
    }

    /// <summary>
    /// Leaves the share group: sends ShareGroupHeartbeat with MemberEpoch=-1.
    /// </summary>
    public async ValueTask LeaveGroupAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        if (string.IsNullOrEmpty(_memberId))
            return;

        if (_coordinatorId < 0)
            return;

        try
        {
            var connection = await _connectionPool.GetConnectionByIndexAsync(
                _coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
                .ConfigureAwait(false);

            var request = new ShareGroupHeartbeatRequest
            {
                GroupId = _options.GroupId,
                MemberId = _memberId,
                MemberEpoch = -1
            };

            var version = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.ShareGroupHeartbeat,
                ShareGroupHeartbeatRequest.LowestSupportedVersion,
                ShareGroupHeartbeatRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
                request, version, cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode != ErrorCode.None)
            {
                LogLeaveGroupFailed(response.ErrorCode);
            }
            else
            {
                LogSuccessfullyLeftGroup(_options.GroupId);
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
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Joined share group {GroupId} as member {MemberId} (epoch {Epoch})")]
    private partial void LogJoinedGroup(string groupId, string memberId, int epoch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retriable coordinator error {ErrorCode}, will re-discover coordinator")]
    private partial void LogRetriableCoordinatorError(ErrorCode? errorCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator connection disposed, will re-discover coordinator")]
    private partial void LogCoordinatorConnectionDisposed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator not available (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms")]
    private partial void LogCoordinatorNotAvailableRetry(int attempt, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found coordinator {NodeId} for share group {GroupId}")]
    private partial void LogFoundCoordinator(int nodeId, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Share group heartbeat failed")]
    private partial void LogHeartbeatFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LeaveGroup failed with error: {ErrorCode}")]
    private partial void LogLeaveGroupFailed(ErrorCode errorCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully left share group {GroupId}")]
    private partial void LogSuccessfullyLeftGroup(string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send ShareGroupHeartbeat leave request")]
    private partial void LogLeaveGroupRequestFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "EnsureActiveGroup: share group={GroupId}, current state={State}")]
    private partial void LogEnsureActiveGroupStarted(string groupId, CoordinatorState state);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator state transition to {NewState}")]
    private partial void LogCoordinatorStateTransition(CoordinatorState newState);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Heartbeat loop started with interval {IntervalMs}ms")]
    private partial void LogHeartbeatStarted(int intervalMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator disposing")]
    private partial void LogCoordinatorDisposing();

    [LoggerMessage(Level = LogLevel.Warning, Message = "ShareGroupHeartbeat: unknown topic ID {TopicId} in assignment, skipping")]
    private partial void LogUnknownTopicIdInAssignment(Guid topicId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ShareGroupHeartbeat: assignment updated to {PartitionCount} partitions")]
    private partial void LogAssignmentUpdate(int partitionCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ShareGroupHeartbeat: member epoch updated to {MemberEpoch}")]
    private partial void LogMemberEpochUpdated(int memberEpoch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ShareGroupHeartbeat: waiting for partition assignment")]
    private partial void LogWaitingForAssignment();

    #endregion
}
