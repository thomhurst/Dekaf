using Dekaf.Streams;

namespace Dekaf.Testing;

/// <summary>
/// Deterministic in-memory implementation of <see cref="IStreamsGroupMember"/> for application tests.
/// </summary>
/// <remarks>
/// A member created without a cluster has no assignor: it adopts the tasks the caller reports. A member
/// created with an <see cref="InMemoryKafkaCluster"/> joins that cluster's group, which assigns active tasks
/// round-robin across the group's members. Tasks are ordered by subtopology, in topology order, then by
/// partition, and members by join order. A task exists for each partition of a subtopology's source and
/// repartition source topics (the largest partition count among them; source topic regexes are not matched).
/// Tasks move when a member joins or leaves, and on any heartbeat after the topology or a source topic's
/// partition count changes. A static member that closes while remaining in the group stays a member, as on a
/// broker within its session timeout: it keeps its place in the round-robin, and its tasks do not run until a
/// member with the same instance ID joins and takes over its member ID and tasks without moving any task. A
/// dynamic member always leaves the group on close, so its tasks move. Standby and warmup tasks are never assigned.
/// <para>
/// A <see cref="StreamsGroupMemberOptions.AssignmentListener"/> is told of every assignment change, including one
/// made by another member's join or close, in the same order and on the same kind of separate loop as a broker-backed
/// member. The listener runs after the call that caused the change returns, so wait for it in the listener itself.
/// </para>
/// </remarks>
public sealed class InMemoryStreamsGroupMember : IStreamsGroupMember, IStreamsGroupOffsetCommitter
{
    private static readonly StreamsGroupAssignment EmptyAssignment = new()
    {
        ActiveTasks = [],
        StandbyTasks = [],
        WarmupTasks = []
    };

    private readonly object _gate;
    private readonly InMemoryKafkaCluster? _cluster;
    private readonly InMemoryStreamsGroup? _group;
    private readonly StreamsGroupMemberOptions _options;
    private readonly StreamsGroupAssignmentNotifier? _assignmentNotifier;
    private StreamsGroupMemberSnapshot _snapshot = new()
    {
        Assignment = EmptyAssignment,
        PartitionsByUserEndpoint = [],
        Status = []
    };
    private StreamsGroupMemberUpdate? _lastUpdate;
    private StreamsGroupTaskOffsetReport? _lastTaskOffsetReport;
    private StreamsGroupCloseOptions? _lastCloseOptions;
    private readonly Dictionary<TopicPartition, long> _committedOffsets = [];
    private bool _initialized;
    private bool _assignmentPending;

    /// <summary>Creates a member that adopts the tasks the caller reports, without a group assignor.</summary>
    public InMemoryStreamsGroupMember(StreamsGroupMemberOptions options)
        : this(options, cluster: null)
    {
    }

    /// <summary>
    /// Creates a member of a Streams group on <paramref name="cluster"/>, whose assignor splits active tasks
    /// across every member of the group.
    /// </summary>
    public InMemoryStreamsGroupMember(InMemoryKafkaCluster cluster, StreamsGroupMemberOptions options)
        : this(options, cluster ?? throw new ArgumentNullException(nameof(cluster)))
    {
    }

    private InMemoryStreamsGroupMember(StreamsGroupMemberOptions options, InMemoryKafkaCluster? cluster)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GroupId);
        if (options.RebalanceTimeout.TotalMilliseconds < 1
            || options.RebalanceTimeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "RebalanceTimeout must be greater than zero and no greater than Int32.MaxValue milliseconds.");
        }

        _options = options;
        _cluster = cluster;
        _group = cluster?.GetOrCreateStreamsGroup(options.GroupId);
        _gate = (object?)_group ?? new object();
        if (options.AssignmentListener is { } assignmentListener)
        {
            _assignmentNotifier = new StreamsGroupAssignmentNotifier(assignmentListener);
        }
    }

    /// <inheritdoc />
    public string GroupId => _options.GroupId;

    /// <inheritdoc />
    public string? InstanceId => _options.InstanceId;

    /// <inheritdoc />
    public StreamsGroupMemberSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Gets the offsets most recently committed through <see cref="CommitOffsetsAsync"/>.</summary>
    public IReadOnlyDictionary<TopicPartition, long> CommittedOffsets
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<TopicPartition, long>(_committedOffsets);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask CommitOffsetsAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        int membershipGeneration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_snapshot.IsJoined)
            {
                throw new Dekaf.Errors.GroupException(
                    Dekaf.Protocol.ErrorCode.UnknownMemberId,
                    "Offsets cannot be committed because the Streams group member is not joined.");
            }

            if (_snapshot.MembershipGeneration != membershipGeneration)
            {
                throw new Dekaf.Errors.GroupException(
                    Dekaf.Protocol.ErrorCode.FencedMemberEpoch,
                    "Offsets cannot be committed because the Streams group member lost the membership they were consumed under.");
            }

            foreach (var offset in offsets)
            {
                _committedOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
            }
        }

        return default;
    }

    /// <summary>Gets the latest state update supplied by the test.</summary>
    public StreamsGroupMemberUpdate? LastUpdate
    {
        get
        {
            lock (_gate)
            {
                return _lastUpdate;
            }
        }
    }

    /// <summary>Gets the latest task-offset report supplied by the test.</summary>
    public StreamsGroupTaskOffsetReport? LastTaskOffsetReport
    {
        get
        {
            lock (_gate)
            {
                return _lastTaskOffsetReport;
            }
        }
    }

    /// <summary>Gets the options used for the first close operation.</summary>
    public StreamsGroupCloseOptions? LastCloseOptions
    {
        get
        {
            lock (_gate)
            {
                return _lastCloseOptions;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfClosed();
            _initialized = true;
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
        {
            throw new ArgumentException("The initial Streams group state must include a topology.", nameof(initialState));
        }
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            ThrowIfClosed();
            ThrowIfNotInitialized();
            if (_snapshot.IsJoined)
            {
                throw new InvalidOperationException("The Streams group member has already joined.");
            }

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
            if (_group is not null)
            {
                Rebalance(_group, _cluster!);
                return ValueTask.FromResult(CreateGroupResult(_snapshot.EndpointInformationEpoch));
            }

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
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_snapshot.IsClosed)
            {
                return StopAssignmentNotifierAsync(cancellationToken);
            }

            _lastCloseOptions = options;
            // The in-memory cluster has no session timer, so a cluster-backed dynamic member always leaves on
            // close; with RemainInGroup it would otherwise hold its tasks forever.
            var remainInGroup = (options.GroupMembershipOperation == StreamsGroupMembershipOperation.RemainInGroup
                    || (options.GroupMembershipOperation == StreamsGroupMembershipOperation.Default
                        && _options.InstanceId is not null))
                && (_group is null || _options.InstanceId is not null);
            Publish(new StreamsGroupMemberSnapshot
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
                AcceptableRecoveryLagOffsets = _snapshot.AcceptableRecoveryLagOffsets,
                TaskOffsetInterval = _snapshot.TaskOffsetInterval
            });

            if (_group is not null && !remainInGroup && _group.Members.Remove(this))
            {
                Rebalance(_group, _cluster!);
            }
        }

        return StopAssignmentNotifierAsync(cancellationToken);
    }

    // Stopping always proceeds; the token only bounds how long the caller waits for a running callback.
    private ValueTask StopAssignmentNotifierAsync(CancellationToken cancellationToken) =>
        _assignmentNotifier is null
            ? ValueTask.CompletedTask
            : new ValueTask(_assignmentNotifier.StopAsync().WaitAsync(cancellationToken));

    /// <summary>Replaces the snapshot and queues its assignment changes for the listener. Called under the lock.</summary>
    private void Publish(StreamsGroupMemberSnapshot snapshot)
    {
        Volatile.Write(ref _snapshot, snapshot);
        _assignmentNotifier?.PublishAssignment(snapshot.Assignment);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => CloseAsync();

    private StreamsGroupHeartbeatResult ApplyUpdate(StreamsGroupMemberUpdate update, bool isJoin)
    {
        if (_group is not null)
        {
            return ApplyGroupUpdate(_group, _cluster!, update, isJoin);
        }

        var isTopologyJoin = update.Topology is not null;
        var activeTasks = isTopologyJoin ? null : CopyTaskSets(update.ActiveTasks);
        var standbyTasks = isTopologyJoin ? null : CopyTaskSets(update.StandbyTasks);
        var warmupTasks = isTopologyJoin ? null : CopyTaskSets(update.WarmupTasks);
        var previous = _snapshot;
        var fallbackAssignment = isTopologyJoin ? EmptyAssignment : previous.Assignment;
        var assignment = new StreamsGroupAssignment
        {
            ActiveTasks = activeTasks ?? fallbackAssignment.ActiveTasks,
            StandbyTasks = standbyTasks ?? fallbackAssignment.StandbyTasks,
            WarmupTasks = warmupTasks ?? fallbackAssignment.WarmupTasks
        };
        var memberId = isJoin ? "in-memory-streams-member" : previous.MemberId!;
        var memberEpoch = isJoin ? 1 : previous.MemberEpoch;
        Publish(new StreamsGroupMemberSnapshot
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

    private StreamsGroupHeartbeatResult ApplyGroupUpdate(
        InMemoryStreamsGroup group,
        InMemoryKafkaCluster cluster,
        StreamsGroupMemberUpdate update,
        bool isJoin)
    {
        var previous = _snapshot;
        var memberId = previous.MemberId;
        var memberEpoch = previous.MemberEpoch;
        var assignment = previous.Assignment;
        if (isJoin)
        {
            var predecessor = FindClosedStaticMember(group);
            if (predecessor >= 0)
            {
                var predecessorSnapshot = group.Members[predecessor]._snapshot;
                memberId = predecessorSnapshot.MemberId;
                memberEpoch = predecessorSnapshot.MemberEpoch;
                assignment = predecessorSnapshot.Assignment;
                group.Members[predecessor] = this;
            }
            else
            {
                memberId = $"in-memory-streams-member-{++group.NextMemberNumber}";
                memberEpoch = 1;
                assignment = EmptyAssignment;
                group.Members.Add(this);
            }

            // The join response always carries the full assignment, even an empty one.
            _assignmentPending = true;
        }

        if (update.Topology is not null)
        {
            group.Topology = update.Topology;
        }

        Publish(new StreamsGroupMemberSnapshot
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

        Rebalance(group, cluster);
        return CreateGroupResult(update.EndpointInformationEpoch);
    }

    private int FindClosedStaticMember(InMemoryStreamsGroup group)
    {
        if (_options.InstanceId is null)
        {
            return -1;
        }

        for (var index = 0; index < group.Members.Count; index++)
        {
            var member = group.Members[index];
            if (member._snapshot.IsClosed
                && string.Equals(member._options.InstanceId, _options.InstanceId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Assigns active tasks round-robin across the group's members. A closed static member still in the group
    /// keeps its place, as on a broker within its session timeout, so its replacement takes over the same tasks.
    /// Called under the group lock.
    /// </summary>
    private static void Rebalance(InMemoryStreamsGroup group, InMemoryKafkaCluster cluster)
    {
        var members = group.Members;
        if (members.Count == 0)
        {
            return;
        }

        var assigned = new List<StreamsGroupTaskSet>?[members.Count];
        var subtopologies = group.Topology?.Subtopologies ?? [];
        var next = 0;
        foreach (var subtopology in subtopologies)
        {
            var taskCount = GetTaskCount(subtopology, cluster);
            if (taskCount == 0)
            {
                continue;
            }

            var partitions = new List<int>?[members.Count];
            for (var partition = 0; partition < taskCount; partition++)
            {
                (partitions[next++ % members.Count] ??= []).Add(partition);
            }

            for (var index = 0; index < members.Count; index++)
            {
                if (partitions[index] is { } memberPartitions)
                {
                    (assigned[index] ??= []).Add(new StreamsGroupTaskSet
                    {
                        SubtopologyId = subtopology.SubtopologyId,
                        Partitions = memberPartitions.ToArray()
                    });
                }
            }
        }

        for (var index = 0; index < members.Count; index++)
        {
            members[index].ApplyAssignment(assigned[index]?.ToArray() ?? []);
        }
    }

    private static int GetTaskCount(StreamsGroupSubtopology subtopology, InMemoryKafkaCluster cluster)
    {
        var taskCount = 0;
        foreach (var topic in subtopology.SourceTopics)
        {
            if (cluster.TryGetTopicPartitionCount(topic, out var partitionCount))
            {
                taskCount = Math.Max(taskCount, partitionCount);
            }
        }

        foreach (var topic in subtopology.RepartitionSourceTopics)
        {
            var partitionCount = cluster.TryGetTopicPartitionCount(topic.Name, out var existing)
                ? existing
                : topic.Partitions;
            taskCount = Math.Max(taskCount, partitionCount);
        }

        return taskCount;
    }

    private void ApplyAssignment(IReadOnlyList<StreamsGroupTaskSet> activeTasks)
    {
        var current = _snapshot;
        if (SameTasks(current.Assignment.ActiveTasks, activeTasks))
        {
            return;
        }

        _assignmentPending = true;
        Publish(new StreamsGroupMemberSnapshot
        {
            IsJoined = current.IsJoined,
            IsClosed = current.IsClosed,
            MemberId = current.MemberId,
            MemberEpoch = current.MemberEpoch + 1,
            MembershipGeneration = current.MembershipGeneration,
            Assignment = new StreamsGroupAssignment
            {
                ActiveTasks = activeTasks,
                StandbyTasks = [],
                WarmupTasks = []
            },
            EndpointInformationEpoch = current.EndpointInformationEpoch,
            PartitionsByUserEndpoint = current.PartitionsByUserEndpoint,
            Status = current.Status,
            HeartbeatInterval = current.HeartbeatInterval,
            AcceptableRecoveryLagOffsets = current.AcceptableRecoveryLagOffsets,
            TaskOffsetInterval = current.TaskOffsetInterval
        });
    }

    private static bool SameTasks(IReadOnlyList<StreamsGroupTaskSet> left, IReadOnlyList<StreamsGroupTaskSet> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].SubtopologyId, right[index].SubtopologyId, StringComparison.Ordinal)
                || !left[index].Partitions.SequenceEqual(right[index].Partitions))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns a heartbeat result that carries the assignment once after each change.</summary>
    private StreamsGroupHeartbeatResult CreateGroupResult(int endpointInformationEpoch)
    {
        var deliver = _assignmentPending;
        _assignmentPending = false;
        var assignment = _snapshot.Assignment;
        return CreateResult(
            deliver ? assignment.ActiveTasks : null,
            deliver ? assignment.StandbyTasks : null,
            deliver ? assignment.WarmupTasks : null,
            endpointInformationEpoch);
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
            AcceptableRecoveryLagOffsets = _snapshot.AcceptableRecoveryLagOffsets,
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
        {
            return null;
        }

        var copy = new StreamsGroupTaskSet[taskSets.Count];
        for (var index = 0; index < taskSets.Count; index++)
        {
            var taskSet = taskSets[index];
            var partitions = new int[taskSet.Partitions.Count];
            for (var partitionIndex = 0; partitionIndex < partitions.Length; partitionIndex++)
            {
                partitions[partitionIndex] = taskSet.Partitions[partitionIndex];
            }
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
        ThrowIfNotInitialized();
        if (!_snapshot.IsJoined)
        {
            throw new InvalidOperationException("The Streams group member has not joined.");
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "The Streams group member is not initialized. Call InitializeAsync() before joining.");
        }
    }

    private void ThrowIfClosed() => ObjectDisposedException.ThrowIf(_snapshot.IsClosed, this);
}
