using System.Diagnostics;
using System.Net.Sockets;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;
using Dekaf.Telemetry;
using Microsoft.Extensions.Logging;
#if !NET9_0_OR_GREATER
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

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
    private long _lastSuccessfulHeartbeatTimestamp;
    private string? _lastHeartbeatFailure;
    private int _disposed;
    private TaskCompletionSource<bool>? _assignmentChanged;
    private readonly Func<int> _getCoordinationConnectionIndex;
    // Where the next coordinator lookup starts. It rests on the last broker that answered, so a
    // broker that refuses or black-holes connections is not tried first by every lookup.
    private int _coordinatorLookupCursor;

    private volatile int _heartbeatIntervalMs;
    private volatile HashSet<string>? _subscribedTopics;
    private int _subscriptionVersion;
    private int _acknowledgedSubscriptionVersion;

    internal static int GetCoordinationConnectionIndex(int connectionsPerBroker)
        => connectionsPerBroker - 1;

    internal static int GetWaitForAssignmentDelayMs(int heartbeatIntervalMs)
        => Math.Max(heartbeatIntervalMs, 1);

    private readonly ShareConsumerTelemetryMetrics? _telemetryMetrics;

    public ShareConsumerCoordinator(
        ShareConsumerOptions options,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILogger? logger = null,
        Func<int>? getConnectionCount = null,
        ShareConsumerTelemetryMetrics? telemetryMetrics = null)
    {
        _telemetryMetrics = telemetryMetrics;
        _options = options;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _getCoordinationConnectionIndex = getConnectionCount is not null
            ? () => GetCoordinationConnectionIndex(getConnectionCount())
            : () => GetCoordinationConnectionIndex(options.ConnectionsPerBroker);
        _heartbeatIntervalMs = options.HeartbeatIntervalMs;
    }

    // A push samples membership without taking the coordination lock. Suppress identities
    // during joining, fencing, disposal, or an observed epoch/identity transition.
    internal string? CaptureTelemetryMemberId()
    {
        var epoch = _memberEpoch;
        var memberId = _memberId;
        return epoch > 0 && _state == CoordinatorState.Stable &&
            Volatile.Read(ref _disposed) == 0 && _memberEpoch == epoch &&
            ReferenceEquals(memberId, _memberId)
                ? memberId
                : null;
    }

    public string? MemberId => _memberId;
    public int MemberEpoch => _memberEpoch;
    public CoordinatorState State => _state;
    public TopicPartitionSet Assignment => _assignedPartitions;

    // Allocate only when a poll waits for assignment or leader recovery. Subscribe to the
    // signal before rechecking state so updates racing waiter registration cannot be lost.
    internal Task GetAssignmentChangeTask()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.CompletedTask;
        var pending = Volatile.Read(ref _assignmentChanged);
        if (pending is not null)
            return pending.Task;
        var created = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = Interlocked.CompareExchange(ref _assignmentChanged, created, null) ?? created;
        if (Volatile.Read(ref _disposed) != 0)
            NotifyAssignmentChange();
        return pending.Task;
    }

    internal void NotifyAssignmentChange()
        => Interlocked.Exchange(ref _assignmentChanged, null)?.TrySetResult(true);

    // Subscription snapshots are immutable after publication. The consumer compares
    // contents on Subscribe, keeping this work out of the stable per-poll path.
    internal void UpdateSubscription(HashSet<string> topics)
    {
        _subscribedTopics = topics;
        Interlocked.Increment(ref _subscriptionVersion);
        NotifyAssignmentChange();
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
            GenerationOrMemberEpoch = _memberEpoch,
            HeartbeatInterval = TimeSpan.FromMilliseconds(Math.Max(_heartbeatIntervalMs, 1)),
            TimeSinceLastHeartbeat = lastHeartbeatTimestamp == 0
                ? null
                : Stopwatch.GetElapsedTime(lastHeartbeatTimestamp),
            LastHeartbeatFailure = Volatile.Read(ref _lastHeartbeatFailure),
            Assignment = KafkaClientStatusFactory.CopyAssignment(assignment, assignment.Count)
        };
    }

    /// <summary>
    /// Forces the coordinator to rejoin the group on the next
    /// <see cref="EnsureActiveGroupAsync"/> call.
    /// </summary>
    internal void RequestRejoin()
    {
        _state = CoordinatorState.Unjoined;
        NotifyAssignmentChange();
    }

    /// <summary>
    /// Ensures the share consumer has joined the group.
    /// </summary>
    public async ValueTask EnsureActiveGroupAsync(
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ShareConsumerCoordinator));

        if (_state == CoordinatorState.Stable)
            return;

        await EnsureActiveGroupCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks the coordinator as unknown, forcing re-discovery on next operation.
    /// </summary>
    private void MarkCoordinatorUnknown()
    {
        _coordinatorId = -1;
        _state = CoordinatorState.Unjoined;
        NotifyAssignmentChange();
    }

    private static bool IsRetriableCoordinatorError(ErrorCode? errorCode) =>
        errorCode is ErrorCode.NotCoordinator
            or ErrorCode.CoordinatorNotAvailable
            or ErrorCode.CoordinatorLoadInProgress;

    /// <summary>
    /// Classifies a failed join attempt. Transport and connection-setup failures (a coordinator
    /// that refuses or resets connections, a socket that died mid-request, DNS, setup timeouts)
    /// are retried with backoff until the join timeout: the coordinator may be restarting or
    /// may have moved. Typed group errors have dedicated handlers; broker-version,
    /// authentication and authorization failures are fatal.
    /// </summary>
    /// <remarks>
    /// Does not look at the caller's token: a failure that lands after cancellation must still be
    /// caught so the loop reports <see cref="OperationCanceledException"/> rather than a raw
    /// socket exception. A connection retired by pool churn is retried only while this
    /// coordinator is alive, so the loop cannot spin under the state lock against a disposed pool.
    /// </remarks>
    private bool IsRetriableJoinFailure(Exception exception) =>
        TransportFailureClassifier.IsRetriable(
            exception,
            TransportRetryPolicy.GroupJoin,
            ownerDisposed: Volatile.Read(ref _disposed) != 0);

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
        var cursor = Volatile.Read(ref _coordinatorLookupCursor);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            // The cursor outlives this call: a lookup that fails against a dead broker advances
            // it, so the join loop's next lookup asks a different broker instead of the same one.
            var broker = brokers[(int)((uint)(cursor + attempt) % (uint)brokers.Count)];
            Volatile.Write(ref _coordinatorLookupCursor, cursor + attempt + 1);
            using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                broker.NodeId, _getCoordinationConnectionIndex(), cancellationToken)
                .ConfigureAwait(false);
            var connection = connectionLease.Connection;

            var findCoordinatorVersion = _metadataManager.GetNegotiatedApiVersion(
                connection,
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
                if (attempt == maxRetries - 1)
                    break;

                var retryDelayMs = CalculateRequestRetryBackoff(attempt + 1);
                LogCoordinatorNotAvailableRetry(attempt + 1, maxRetries, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (errorCode != ErrorCode.None)
            {
                throw new GroupException(errorCode, $"FindCoordinator failed: {errorCode}")
                {
                    GroupId = _options.GroupId
                };
            }

            // Route first, then publish the ID: a concurrent reader of _coordinatorId must never
            // find the pool still considers the coordinator unknown.
            _connectionPool.RegisterBroker(nodeId, host, port);
            _coordinatorId = nodeId;
            // Rest on the broker that answered.
            Volatile.Write(ref _coordinatorLookupCursor, cursor + attempt);

            LogFoundCoordinator(nodeId, _options.GroupId);
            return;
        }

        throw new GroupException(ErrorCode.CoordinatorNotAvailable,
            $"FindCoordinator failed after {maxRetries} retries: CoordinatorNotAvailable")
        {
            GroupId = _options.GroupId
        };
    }

    private int CalculateRequestRetryBackoff(int failureCount) =>
        ExponentialRetryBackoff.CalculateDelayMilliseconds(
            _options.RetryBackoffMs,
            _options.RetryBackoffMaxMs,
            failureCount);

    internal static TimeSpan GetJoinRetryDelay(
        int retryDelayMs,
        TimeSpan elapsed,
        TimeSpan joinTimeout)
    {
        var remaining = joinTimeout - elapsed;
        return remaining <= TimeSpan.Zero
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(Math.Min(retryDelayMs, remaining.TotalMilliseconds));
    }

    private Task DelayForJoinRetryAsync(
        int failureCount,
        long startedAt,
        TimeSpan joinTimeout,
        CancellationToken cancellationToken) =>
        Task.Delay(
            GetJoinRetryDelay(
                CalculateRequestRetryBackoff(failureCount),
                Stopwatch.GetElapsedTime(startedAt),
                joinTimeout),
            cancellationToken);

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
        NotifyAssignmentChange();
    }

    /// <summary>
    /// Sends a ShareGroupHeartbeat request and processes the response.
    /// </summary>
    /// <param name="coordinatorId">
    /// The coordinator the caller discovered, captured by the caller rather than read from
    /// <see cref="_coordinatorId"/> here: a heartbeat loop that fails concurrently invalidates
    /// that field, and leasing broker -1 would surface as an unknown-broker failure.
    /// </param>
    private async ValueTask<bool> SendShareGroupHeartbeatAsync(
        int coordinatorId,
        CancellationToken cancellationToken)
    {
        using var connectionLease = await LeaseHeartbeatConnectionAsync(coordinatorId, cancellationToken)
            .ConfigureAwait(false);
        var connection = connectionLease.Connection;

        var memberEpoch = _memberEpoch;
        // Capture the version before the immutable snapshot. Replacements during this
        // request remain pending, and failures never acknowledge their version.
        var subscriptionVersion = Volatile.Read(ref _subscriptionVersion);
        var subscription = _subscribedTopics;
        // Unsubscribe may race coordinator discovery or an in-flight join. Kafka rejects
        // an empty epoch-zero subscription, so let the polling loop observe unsubscribe.
        if (memberEpoch == 0 && subscription is not { Count: > 0 })
            return false;

        // Share groups always use client-generated UUID v4 member IDs.
        // Generate once when _memberId is null; subsequent heartbeats reuse the stored ID.
        _memberId ??= Guid.NewGuid().ToString();

        var subscribedTopics = (memberEpoch == 0 || subscriptionVersion != Volatile.Read(ref _acknowledgedSubscriptionVersion))
            ? subscription?.ToList() : null;

        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = _options.GroupId,
            MemberId = _memberId,
            MemberEpoch = memberEpoch,
            RackId = memberEpoch == 0 ? _options.RackId : null,
            SubscribedTopicNames = subscribedTopics
        };

        ShareGroupHeartbeatResponse response;
        try
        {
            var version = _metadataManager.GetNegotiatedApiVersion(
                connection,
                ApiKey.ShareGroupHeartbeat,
                ShareGroupHeartbeatRequest.LowestSupportedVersion,
                ShareGroupHeartbeatRequest.HighestSupportedVersion);
            var heartbeatStarted = _telemetryMetrics?.HeartbeatStarted() ?? -1;
            response = await connection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
                request, version, cancellationToken).ConfigureAwait(false);
            _telemetryMetrics?.HeartbeatCompleted(heartbeatStarted);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _lastHeartbeatFailure, ex.Message);
            throw;
        }

        if (response.ErrorCode != ErrorCode.None)
        {
            try
            {
                HandleShareGroupHeartbeatError(response);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _lastHeartbeatFailure, ex.Message);
                throw;
            }
        }

        Volatile.Write(ref _acknowledgedSubscriptionVersion, subscriptionVersion);

        Volatile.Write(ref _lastSuccessfulHeartbeatTimestamp, Stopwatch.GetTimestamp());
        Volatile.Write(ref _lastHeartbeatFailure, null);

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

    private async ValueTask<KafkaConnectionLease> LeaseHeartbeatConnectionAsync(
        int coordinatorId,
        CancellationToken cancellationToken)
    {
        var connectionLease = default(KafkaConnectionLease);
        try
        {
            connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
                .ConfigureAwait(false);
            if (!_metadataManager.HasApiKey(connectionLease.Connection, ApiKey.ShareGroupHeartbeat))
            {
                throw new BrokerVersionException(
                    "The target Kafka broker does not support the ShareGroupHeartbeat API " +
                    "(KIP-932, introduced in Kafka 4.0). Share group consumption requires Kafka 4.0 or later.");
            }

            return connectionLease;
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            connectionLease.Dispose();
            Volatile.Write(ref _lastHeartbeatFailure, ex.Message);
            throw;
        }
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
        if (assignment.TopicPartitions.Count == 0 && _assignedPartitions.Count == 0)
            return;

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

        if (newAssignment.Count == oldAssignment.Count && newAssignment.SetEquals(oldAssignment))
        {
            // A rejoin can complete with the existing assignment. Repeated assignment
            // payloads while stable are not new rebalances and do not notify waiters.
            if (_state == CoordinatorState.Joining && newAssignment.Count > 0)
                _telemetryMetrics?.Rebalanced();
            return;
        }

        _telemetryMetrics?.Rebalanced();

        LogAssignmentUpdate(newAssignment.Count);
        _assignedPartitions = newAssignment;
        NotifyAssignmentChange();
    }

    /// <summary>
    /// Ensures the share consumer has joined the group using the ShareGroupHeartbeat API.
    /// </summary>
    private async ValueTask EnsureActiveGroupCoreAsync(
        CancellationToken cancellationToken)
    {
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
            var retryFailureCount = 0;
            Exception? lastJoinFailure = null;

            while (_state != CoordinatorState.Stable)
            {
                if (_subscribedTopics is not { Count: > 0 } && _memberEpoch <= 0)
                    return;

                if (Stopwatch.GetElapsedTime(startedAt) > timeout)
                {
                    var message =
                        $"Failed to join share group '{_options.GroupId}' within timeout ({_options.SessionTimeoutMs}ms)";
                    throw lastJoinFailure is null
                        ? new KafkaTimeoutException(
                            TimeoutKind.Rebalance, Stopwatch.GetElapsedTime(startedAt), timeout, message)
                        : new KafkaTimeoutException(
                            TimeoutKind.Rebalance, Stopwatch.GetElapsedTime(startedAt), timeout, message, lastJoinFailure);
                }

                try
                {
                    if (_coordinatorId < 0)
                    {
                        await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                    }

                    // A heartbeat loop from the previous membership can still fail and invalidate
                    // the field after the lookup; join against the coordinator that was found.
                    var coordinatorId = _coordinatorId;
                    if (coordinatorId < 0)
                    {
                        throw new GroupException(
                            ErrorCode.CoordinatorNotAvailable,
                            "Coordinator was invalidated during share group join discovery")
                        {
                            GroupId = _options.GroupId
                        };
                    }

                    _state = CoordinatorState.Joining;
                    LogCoordinatorStateTransition(CoordinatorState.Joining);

                    var gotAssignment = await SendShareGroupHeartbeatAsync(coordinatorId, cancellationToken)
                        .ConfigureAwait(false);

                    if (_subscribedTopics is not { Count: > 0 })
                    {
                        // A join accepted before unsubscribe must publish the empty snapshot
                        // before activation finishes. Failed publication uses the join retries.
                        if (_memberEpoch > 0 && Volatile.Read(ref _subscriptionVersion) ==
                            Volatile.Read(ref _acknowledgedSubscriptionVersion))
                        {
                            _state = CoordinatorState.Stable;
                            break;
                        }
                        continue;
                    }

                    if (gotAssignment && _assignedPartitions.Count > 0)
                    {
                        _state = CoordinatorState.Stable;
                        LogJoinedGroup(_options.GroupId, _memberId!, _memberEpoch);
                    }
                    else
                    {
                        // Broker accepted us but hasn't assigned partitions yet.
                        // This is a successful heartbeat, so preserve the broker cadence.
                        LogWaitingForAssignment();
                        retryFailureCount = 0;
                        await Task.Delay(
                            GetWaitForAssignmentDelayMs(_heartbeatIntervalMs),
                            cancellationToken).ConfigureAwait(false);
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
                    lastJoinFailure = ex;
                    MarkCoordinatorUnknown();
                    await DelayForJoinRetryAsync(
                        ++retryFailureCount, startedAt, timeout, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsRetriableJoinFailure(ex))
                {
                    // A failure that lands after the caller cancelled reports the cancellation.
                    cancellationToken.ThrowIfCancellationRequested();

                    if (ex is ObjectDisposedException)
                        LogCoordinatorConnectionDisposed();
                    else if (lastJoinFailure is null)
                        LogCoordinatorUnreachableDuringJoin(ex, _coordinatorId, _options.GroupId);
                    else
                        LogCoordinatorStillUnreachableDuringJoin(ex, _coordinatorId, _options.GroupId, retryFailureCount + 1);

                    lastJoinFailure = ex;
                    MarkCoordinatorUnknown();

                    // The pool has no route for the coordinator it was told to use; only newer
                    // metadata can fix the next attempt.
                    if (TransportFailureClassifier.RequiresMetadataRefresh(ex))
                    {
                        await RetryHelper.RefreshMetadataForRetryAsync(_metadataManager, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    await DelayForJoinRetryAsync(
                        ++retryFailureCount, startedAt, timeout, cancellationToken).ConfigureAwait(false);
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
        // Consecutive transient failures (coordinator unreachable or moving). While nonzero the
        // loop re-discovers the coordinator itself and beats on the retry backoff.
        var transientFailureCount = 0;
        var heartbeatCoordinatorId = -1;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(GetHeartbeatDelayMs(transientFailureCount), cancellationToken).ConfigureAwait(false);

                // A foreground rejoin owns the membership once it left Stable.
                if (transientFailureCount > 0 && _state != CoordinatorState.Stable)
                    break;

                heartbeatCoordinatorId = _coordinatorId;
                if (heartbeatCoordinatorId < 0)
                {
                    await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                    heartbeatCoordinatorId = _coordinatorId;
                    if (heartbeatCoordinatorId < 0)
                    {
                        throw new GroupException(
                            ErrorCode.CoordinatorNotAvailable,
                            "Coordinator was invalidated during share heartbeat discovery")
                        {
                            GroupId = _options.GroupId
                        };
                    }
                }

                await SendShareGroupHeartbeatAsync(heartbeatCoordinatorId, cancellationToken).ConfigureAwait(false);
                transientFailureCount = 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (transientFailureCount == 0)
                    LogHeartbeatFailed(ex);
                else
                    LogHeartbeatStillFailing(ex, transientFailureCount + 1);

                if (TryKeepHeartbeating(ex, heartbeatCoordinatorId))
                {
                    transientFailureCount++;
                    continue;
                }

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
                            NotifyAssignmentChange();
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
                    // The session is lost, or the failure is not one this loop understands —
                    // mark coordinator unknown so EnsureActiveGroupAsync re-discovers on next poll.
                    MarkCoordinatorUnknown();
                }

                break;
            }
        }
    }

    /// <summary>
    /// Decides whether the heartbeat loop survives <paramref name="exception"/>. A coordinator
    /// that is unreachable or moving is transient: the loop invalidates the coordinator it used,
    /// re-discovers it on its next pass and keeps the membership alive, instead of waiting for a
    /// poll that an application busy in a handler may not make before the session expires. Once
    /// a whole session timeout has passed without a successful heartbeat the broker has expired
    /// the member anyway, and the rejoin path takes over.
    /// </summary>
    private bool TryKeepHeartbeating(Exception exception, int heartbeatCoordinatorId)
    {
        var transient = exception is GroupException groupException
            ? IsRetriableCoordinatorError(groupException.ErrorCode)
            : IsRetriableJoinFailure(exception);
        if (!transient || _state != CoordinatorState.Stable)
            return false;

        var sinceLastSuccess = Stopwatch.GetElapsedTime(Volatile.Read(ref _lastSuccessfulHeartbeatTimestamp));
        if (sinceLastSuccess >= TimeSpan.FromMilliseconds(_options.SessionTimeoutMs))
            return false;

        // Invalidate only the coordinator this heartbeat used. Membership state is untouched.
        if (_coordinatorId == heartbeatCoordinatorId)
            _coordinatorId = -1;

        return true;
    }

    private int GetHeartbeatDelayMs(int transientFailureCount)
    {
        var heartbeatDelayMs = _heartbeatIntervalMs;
        return transientFailureCount > 0
            ? Math.Min(heartbeatDelayMs, CalculateRequestRetryBackoff(transientFailureCount))
            : heartbeatDelayMs;
    }

    /// <summary>
    /// Leaves the share group: sends ShareGroupHeartbeat with MemberEpoch=-1.
    /// </summary>
    public async ValueTask LeaveGroupAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        var memberId = _memberId;
        if (string.IsNullOrEmpty(memberId))
            return;

        if (_coordinatorId < 0)
            return;

        try
        {
            using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
                _coordinatorId, _getCoordinationConnectionIndex(), cancellationToken)
                .ConfigureAwait(false);
            var connection = connectionLease.Connection;

            var request = new ShareGroupHeartbeatRequest
            {
                GroupId = _options.GroupId,
                MemberId = memberId!,
                MemberEpoch = -1
            };

            var version = _metadataManager.GetNegotiatedApiVersion(
                connection,
                ApiKey.ShareGroupHeartbeat,
                ShareGroupHeartbeatRequest.LowestSupportedVersion,
                ShareGroupHeartbeatRequest.HighestSupportedVersion);

            var heartbeatStarted = _telemetryMetrics?.HeartbeatStarted() ?? -1;
            var response = await connection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
                request, version, cancellationToken).ConfigureAwait(false);
            _telemetryMetrics?.HeartbeatCompleted(heartbeatStarted);

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
        NotifyAssignmentChange();
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Coordinator {CoordinatorId} unreachable while joining share group {GroupId}; re-discovering coordinator and retrying until the join timeout")]
    private partial void LogCoordinatorUnreachableDuringJoin(Exception exception, int coordinatorId, string groupId);

    // The first failure of a join is the Warning above; an outage then repeats at Debug.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator {CoordinatorId} still unreachable while joining share group {GroupId} (attempt {Attempt})")]
    private partial void LogCoordinatorStillUnreachableDuringJoin(Exception exception, int coordinatorId, string groupId, int attempt);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Coordinator not available (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms")]
    private partial void LogCoordinatorNotAvailableRetry(int attempt, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found coordinator {NodeId} for share group {GroupId}")]
    private partial void LogFoundCoordinator(int nodeId, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Share group heartbeat failed")]
    private partial void LogHeartbeatFailed(Exception exception);

    // The first failure of an outage is the Warning above; the retries repeat at Debug.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Share group heartbeat still failing (attempt {Attempt}); re-discovering coordinator and retrying")]
    private partial void LogHeartbeatStillFailing(Exception exception, int attempt);

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
