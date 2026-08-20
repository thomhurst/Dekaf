using Dekaf.Streams;

namespace Dekaf.Testing;

/// <summary>
/// Deterministic in-memory implementation of <see cref="IStreamsGroupMember"/> for application tests.
/// </summary>
public sealed class InMemoryStreamsGroupMember : IStreamsGroupMember
{
    private static readonly StreamsGroupAssignment EmptyAssignment = new()
    {
        ActiveTasks = [],
        StandbyTasks = [],
        WarmupTasks = []
    };

    private readonly object _gate = new();
    private readonly StreamsGroupMemberOptions _options;
    private StreamsGroupMemberSnapshot _snapshot = new()
    {
        Assignment = EmptyAssignment,
        PartitionsByUserEndpoint = [],
        Status = []
    };
    private StreamsGroupMemberUpdate? _lastUpdate;
    private StreamsGroupTaskOffsetReport? _lastTaskOffsetReport;
    private StreamsGroupCloseOptions? _lastCloseOptions;

    public InMemoryStreamsGroupMember(StreamsGroupMemberOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GroupId);
        if (options.RebalanceTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Rebalance timeout must be positive.");

        _options = options;
    }

    /// <inheritdoc />
    public string GroupId => _options.GroupId;

    /// <inheritdoc />
    public string? InstanceId => _options.InstanceId;

    /// <inheritdoc />
    public StreamsGroupMemberSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Gets the latest state update supplied by the test.</summary>
    public StreamsGroupMemberUpdate? LastUpdate
    {
        get
        {
            lock (_gate)
                return _lastUpdate;
        }
    }

    /// <summary>Gets the latest task-offset report supplied by the test.</summary>
    public StreamsGroupTaskOffsetReport? LastTaskOffsetReport
    {
        get
        {
            lock (_gate)
                return _lastTaskOffsetReport;
        }
    }

    /// <summary>Gets the options used for the first close operation.</summary>
    public StreamsGroupCloseOptions? LastCloseOptions
    {
        get
        {
            lock (_gate)
                return _lastCloseOptions;
        }
    }

    /// <inheritdoc />
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfClosed();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<StreamsGroupHeartbeatResult> JoinAsync(
        StreamsGroupMemberUpdate initialState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        if (initialState.Topology is null)
            throw new ArgumentException("The initial Streams group state must include a topology.", nameof(initialState));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            ThrowIfClosed();
            if (_snapshot.IsJoined)
                throw new InvalidOperationException("The Streams group member has already joined.");

            _lastUpdate = initialState;
            return ValueTask.FromResult(ApplyUpdate(initialState, isJoin: true));
        }
    }

    /// <inheritdoc />
    public ValueTask<StreamsGroupHeartbeatResult> UpdateAsync(
        StreamsGroupMemberUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureJoined();
            _lastUpdate = update;
            return ValueTask.FromResult(ApplyUpdate(update, isJoin: false));
        }
    }

    /// <inheritdoc />
    public ValueTask<StreamsGroupHeartbeatResult> ReportTaskOffsetsAsync(
        StreamsGroupTaskOffsetReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureJoined();
            _lastTaskOffsetReport = report;
            return ValueTask.FromResult(CreateResult(
                activeTasks: null,
                standbyTasks: null,
                warmupTasks: null,
                endpointInformationEpoch: _snapshot.EndpointInformationEpoch));
        }
    }

    /// <inheritdoc />
    public ValueTask CloseAsync(CancellationToken cancellationToken = default) =>
        CloseAsync(new StreamsGroupCloseOptions(), cancellationToken);

    /// <inheritdoc />
    public ValueTask CloseAsync(
        StreamsGroupCloseOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.GroupMembershipOperation))
            throw new ArgumentOutOfRangeException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_snapshot.IsClosed)
                return ValueTask.CompletedTask;

            _lastCloseOptions = options;
            var remainInGroup = options.GroupMembershipOperation == StreamsGroupMembershipOperation.RemainInGroup
                || (options.GroupMembershipOperation == StreamsGroupMembershipOperation.Default
                    && _options.InstanceId is not null);
            Volatile.Write(ref _snapshot, new StreamsGroupMemberSnapshot
            {
                IsJoined = remainInGroup && _snapshot.IsJoined,
                IsClosed = true,
                MemberId = remainInGroup ? _snapshot.MemberId : null,
                MemberEpoch = remainInGroup ? _snapshot.MemberEpoch : 0,
                Assignment = _snapshot.Assignment,
                EndpointInformationEpoch = _snapshot.EndpointInformationEpoch,
                PartitionsByUserEndpoint = _snapshot.PartitionsByUserEndpoint,
                Status = _snapshot.Status,
                HeartbeatInterval = _snapshot.HeartbeatInterval,
                AcceptableRecoveryLag = _snapshot.AcceptableRecoveryLag,
                TaskOffsetInterval = _snapshot.TaskOffsetInterval
            });
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => CloseAsync();

    private StreamsGroupHeartbeatResult ApplyUpdate(StreamsGroupMemberUpdate update, bool isJoin)
    {
        var activeTasks = CopyTaskSets(update.ActiveTasks);
        var standbyTasks = CopyTaskSets(update.StandbyTasks);
        var warmupTasks = CopyTaskSets(update.WarmupTasks);
        var previous = _snapshot;
        var assignment = new StreamsGroupAssignment
        {
            ActiveTasks = activeTasks ?? previous.Assignment.ActiveTasks,
            StandbyTasks = standbyTasks ?? previous.Assignment.StandbyTasks,
            WarmupTasks = warmupTasks ?? previous.Assignment.WarmupTasks
        };
        var memberId = isJoin ? "in-memory-streams-member" : previous.MemberId!;
        var memberEpoch = isJoin ? 1 : previous.MemberEpoch;
        Volatile.Write(ref _snapshot, new StreamsGroupMemberSnapshot
        {
            IsJoined = true,
            MemberId = memberId,
            MemberEpoch = memberEpoch,
            Assignment = assignment,
            EndpointInformationEpoch = update.EndpointInformationEpoch,
            PartitionsByUserEndpoint = previous.PartitionsByUserEndpoint,
            Status = previous.Status,
            HeartbeatInterval = TimeSpan.FromSeconds(1),
            TaskOffsetInterval = TimeSpan.FromSeconds(10)
        });

        return CreateResult(
            activeTasks,
            standbyTasks,
            warmupTasks,
            update.EndpointInformationEpoch);
    }

    private StreamsGroupHeartbeatResult CreateResult(
        IReadOnlyList<StreamsGroupTaskSet>? activeTasks,
        IReadOnlyList<StreamsGroupTaskSet>? standbyTasks,
        IReadOnlyList<StreamsGroupTaskSet>? warmupTasks,
        int endpointInformationEpoch) => new()
        {
            MemberId = _snapshot.MemberId!,
            MemberEpoch = _snapshot.MemberEpoch,
            HeartbeatInterval = _snapshot.HeartbeatInterval,
            AcceptableRecoveryLag = _snapshot.AcceptableRecoveryLag,
            TaskOffsetInterval = _snapshot.TaskOffsetInterval,
            Status = null,
            ActiveTasks = activeTasks,
            StandbyTasks = standbyTasks,
            WarmupTasks = warmupTasks,
            EndpointInformationEpoch = endpointInformationEpoch,
            PartitionsByUserEndpoint = null
        };

    private static IReadOnlyList<StreamsGroupTaskSet>? CopyTaskSets(
        IReadOnlyList<StreamsGroupTaskSet>? taskSets)
    {
        if (taskSets is null)
            return null;

        var copy = new StreamsGroupTaskSet[taskSets.Count];
        for (var index = 0; index < taskSets.Count; index++)
        {
            var taskSet = taskSets[index];
            var partitions = new int[taskSet.Partitions.Count];
            for (var partitionIndex = 0; partitionIndex < partitions.Length; partitionIndex++)
                partitions[partitionIndex] = taskSet.Partitions[partitionIndex];
            copy[index] = new StreamsGroupTaskSet
            {
                SubtopologyId = taskSet.SubtopologyId,
                Partitions = partitions
            };
        }

        return copy;
    }

    private void EnsureJoined()
    {
        ThrowIfClosed();
        if (!_snapshot.IsJoined)
            throw new InvalidOperationException("The Streams group member has not joined.");
    }

    private void ThrowIfClosed() => ObjectDisposedException.ThrowIf(_snapshot.IsClosed, this);
}
