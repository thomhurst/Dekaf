namespace Dekaf.Streams;

/// <summary>
/// Participates in a Kafka Streams group without providing a Streams processing runtime.
/// </summary>
public interface IStreamsGroupMember : IInitializableKafkaClient, IAsyncDisposable
{
    /// <summary>Gets the configured group ID.</summary>
    string GroupId { get; }

    /// <summary>Gets the configured static member ID, or <see langword="null"/> for a dynamic member.</summary>
    string? InstanceId { get; }

    /// <summary>Gets the latest complete membership and assignment snapshot.</summary>
    StreamsGroupMemberSnapshot Snapshot { get; }

    /// <summary>Joins the group and publishes the member's initial state.</summary>
    ValueTask<StreamsGroupHeartbeatResult> JoinAsync(
        StreamsGroupMemberUpdate initialState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes changed member state. Nullable fields mean that the corresponding state did not change.
    /// </summary>
    ValueTask<StreamsGroupHeartbeatResult> UpdateAsync(
        StreamsGroupMemberUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>Reports current and end offsets for stateful tasks.</summary>
    ValueTask<StreamsGroupHeartbeatResult> ReportTaskOffsetsAsync(
        StreamsGroupTaskOffsetReport report,
        CancellationToken cancellationToken = default);

    /// <summary>Closes the member using Kafka's default dynamic/static membership behavior.</summary>
    ValueTask CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the member using the specified group-membership behavior.</summary>
    ValueTask CloseAsync(
        StreamsGroupCloseOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>Configuration shared by all heartbeats from one Streams group member.</summary>
public sealed class StreamsGroupMemberOptions
{
    /// <summary>Gets the Streams group ID.</summary>
    public required string GroupId { get; init; }

    /// <summary>Gets the stable instance ID. A non-null value enables static membership.</summary>
    public string? InstanceId { get; init; }

    /// <summary>Gets the rack ID used by the group assignor.</summary>
    public string? RackId { get; init; }

    /// <summary>Gets the maximum time allowed for a rebalance.</summary>
    public TimeSpan RebalanceTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>A nullable state delta sent by a join or subsequent heartbeat.</summary>
public sealed class StreamsGroupMemberUpdate
{
    /// <summary>Gets the endpoint-information epoch.</summary>
    public int EndpointInformationEpoch { get; init; }

    /// <summary>Gets a changed topology, or <see langword="null"/> when unchanged.</summary>
    public StreamsGroupTopology? Topology { get; init; }

    /// <summary>Gets changed active tasks, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupTaskSet>? ActiveTasks { get; init; }

    /// <summary>Gets changed standby tasks, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupTaskSet>? StandbyTasks { get; init; }

    /// <summary>Gets changed warmup tasks, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupTaskSet>? WarmupTasks { get; init; }

    /// <summary>Gets the process ID, or <see langword="null"/> when unchanged.</summary>
    public string? ProcessId { get; init; }

    /// <summary>Gets the interactive-query endpoint, or <see langword="null"/> when unchanged.</summary>
    public StreamsGroupEndpoint? UserEndpoint { get; init; }

    /// <summary>Gets changed client tags, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupKeyValue>? ClientTags { get; init; }

    /// <summary>Gets changed task offsets, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupTaskOffset>? TaskOffsets { get; init; }

    /// <summary>Gets changed task end offsets, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupTaskOffset>? TaskEndOffsets { get; init; }

    /// <summary>Gets whether this heartbeat requests application-wide shutdown.</summary>
    public bool ShutdownApplication { get; init; }
}

/// <summary>Task offsets reported independently of other member-state changes.</summary>
public sealed class StreamsGroupTaskOffsetReport
{
    /// <summary>Gets the member's current task offsets.</summary>
    public required IReadOnlyList<StreamsGroupTaskOffset> TaskOffsets { get; init; }

    /// <summary>Gets the task end offsets used to calculate recovery lag.</summary>
    public required IReadOnlyList<StreamsGroupTaskOffset> TaskEndOffsets { get; init; }
}

/// <summary>The successful broker response to a Streams group heartbeat.</summary>
/// <remarks>Broker error responses surface as Kafka exceptions instead of successful results.</remarks>
public sealed class StreamsGroupHeartbeatResult
{
    /// <summary>Gets the broker throttle time.</summary>
    public TimeSpan ThrottleTime { get; init; }

    /// <summary>Gets the broker-assigned member ID.</summary>
    public required string MemberId { get; init; }

    /// <summary>Gets the current member epoch.</summary>
    public int MemberEpoch { get; init; }

    /// <summary>Gets the broker-directed heartbeat interval.</summary>
    public TimeSpan HeartbeatInterval { get; init; }

    /// <summary>Gets the maximum acceptable recovery lag.</summary>
    public int AcceptableRecoveryLag { get; init; }

    /// <summary>Gets the broker-directed task-offset reporting interval.</summary>
    public TimeSpan TaskOffsetInterval { get; init; }

    /// <summary>Gets status changes, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupStatus>? Status { get; init; }

    /// <summary>Gets changed active tasks, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupTaskSet>? ActiveTasks { get; init; }

    /// <summary>Gets changed standby tasks, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupTaskSet>? StandbyTasks { get; init; }

    /// <summary>Gets changed warmup tasks, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupTaskSet>? WarmupTasks { get; init; }

    /// <summary>Gets the endpoint-information epoch.</summary>
    public int EndpointInformationEpoch { get; init; }

    /// <summary>Gets endpoint partition changes, or <see langword="null"/> when unchanged.</summary>
    public IReadOnlyList<StreamsGroupEndpointAssignment>? PartitionsByUserEndpoint { get; init; }
}

/// <summary>A complete point-in-time membership snapshot.</summary>
public sealed class StreamsGroupMemberSnapshot
{
    /// <summary>Gets whether the member has joined its group.</summary>
    public bool IsJoined { get; init; }

    /// <summary>Gets whether the member has been closed.</summary>
    public bool IsClosed { get; init; }

    /// <summary>Gets the member ID, or <see langword="null"/> before the first successful join.</summary>
    public string? MemberId { get; init; }

    /// <summary>Gets the current member epoch.</summary>
    public int MemberEpoch { get; init; }

    /// <summary>Gets the current assignment.</summary>
    public required StreamsGroupAssignment Assignment { get; init; }

    /// <summary>Gets the current endpoint-information epoch.</summary>
    public int EndpointInformationEpoch { get; init; }

    /// <summary>Gets the latest endpoint partition snapshot.</summary>
    public required IReadOnlyList<StreamsGroupEndpointAssignment> PartitionsByUserEndpoint { get; init; }

    /// <summary>Gets the latest member statuses.</summary>
    public required IReadOnlyList<StreamsGroupStatus> Status { get; init; }

    /// <summary>Gets the broker-directed heartbeat interval.</summary>
    public TimeSpan HeartbeatInterval { get; init; }

    /// <summary>Gets the maximum acceptable recovery lag.</summary>
    public int AcceptableRecoveryLag { get; init; }

    /// <summary>Gets the broker-directed task-offset reporting interval.</summary>
    public TimeSpan TaskOffsetInterval { get; init; }
}

/// <summary>A complete active, standby, and warmup task assignment.</summary>
public sealed class StreamsGroupAssignment
{
    /// <summary>Gets active task partitions.</summary>
    public required IReadOnlyList<StreamsGroupTaskSet> ActiveTasks { get; init; }

    /// <summary>Gets standby task partitions.</summary>
    public required IReadOnlyList<StreamsGroupTaskSet> StandbyTasks { get; init; }

    /// <summary>Gets warmup task partitions.</summary>
    public required IReadOnlyList<StreamsGroupTaskSet> WarmupTasks { get; init; }
}

/// <summary>A Streams topology supplied to the group coordinator.</summary>
public sealed class StreamsGroupTopology
{
    /// <summary>Gets the topology epoch.</summary>
    public int Epoch { get; init; }

    /// <summary>Gets subtopologies in protocol index order.</summary>
    public required IReadOnlyList<StreamsGroupSubtopology> Subtopologies { get; init; }
}

/// <summary>A subtopology and its external/internal topic metadata.</summary>
public sealed class StreamsGroupSubtopology
{
    /// <summary>Gets the subtopology ID.</summary>
    public required string SubtopologyId { get; init; }

    /// <summary>Gets source topic names.</summary>
    public required IReadOnlyList<string> SourceTopics { get; init; }

    /// <summary>Gets source topic regular expressions.</summary>
    public required IReadOnlyList<string> SourceTopicRegex { get; init; }

    /// <summary>Gets state changelog topic definitions.</summary>
    public required IReadOnlyList<StreamsGroupTopicInfo> StateChangelogTopics { get; init; }

    /// <summary>Gets repartition sink topic names.</summary>
    public required IReadOnlyList<string> RepartitionSinkTopics { get; init; }

    /// <summary>Gets repartition source topic definitions.</summary>
    public required IReadOnlyList<StreamsGroupTopicInfo> RepartitionSourceTopics { get; init; }

    /// <summary>Gets copartition constraints whose indexes refer to the preceding topic lists.</summary>
    public required IReadOnlyList<StreamsGroupCopartitionGroup> CopartitionGroups { get; init; }
}

/// <summary>Topic metadata used by a Streams topology.</summary>
public sealed class StreamsGroupTopicInfo
{
    /// <summary>Gets the topic name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the requested partition count.</summary>
    public int Partitions { get; init; }

    /// <summary>Gets the requested replication factor.</summary>
    public short ReplicationFactor { get; init; }

    /// <summary>Gets topic configuration entries.</summary>
    public required IReadOnlyList<StreamsGroupKeyValue> TopicConfigs { get; init; }
}

/// <summary>Indexes of topics that must have the same partition count.</summary>
public sealed class StreamsGroupCopartitionGroup
{
    /// <summary>Gets indexes into <see cref="StreamsGroupSubtopology.SourceTopics"/>.</summary>
    public required IReadOnlyList<short> SourceTopics { get; init; }

    /// <summary>Gets indexes into <see cref="StreamsGroupSubtopology.SourceTopicRegex"/>.</summary>
    public required IReadOnlyList<short> SourceTopicRegex { get; init; }

    /// <summary>Gets indexes into <see cref="StreamsGroupSubtopology.RepartitionSourceTopics"/>.</summary>
    public required IReadOnlyList<short> RepartitionSourceTopics { get; init; }
}

/// <summary>A client tag or topic configuration entry.</summary>
public sealed class StreamsGroupKeyValue
{
    /// <summary>Gets the entry key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the entry value.</summary>
    public required string Value { get; init; }
}

/// <summary>An interactive-query endpoint.</summary>
public sealed class StreamsGroupEndpoint
{
    /// <summary>Gets the endpoint host.</summary>
    public required string Host { get; init; }

    /// <summary>Gets the endpoint port.</summary>
    public ushort Port { get; init; }
}

/// <summary>A task offset identified by subtopology and partition.</summary>
public sealed class StreamsGroupTaskOffset
{
    /// <summary>Gets the subtopology ID.</summary>
    public required string SubtopologyId { get; init; }

    /// <summary>Gets the task partition.</summary>
    public int Partition { get; init; }

    /// <summary>Gets the cumulative changelog offset.</summary>
    public long Offset { get; init; }
}

/// <summary>Task partitions grouped by subtopology.</summary>
public sealed class StreamsGroupTaskSet
{
    /// <summary>Gets the subtopology ID.</summary>
    public required string SubtopologyId { get; init; }

    /// <summary>Gets task partitions within the subtopology.</summary>
    public required IReadOnlyList<int> Partitions { get; init; }
}

/// <summary>A member status returned by the group coordinator.</summary>
public sealed class StreamsGroupStatus
{
    /// <summary>Gets the protocol-defined status code.</summary>
    public sbyte StatusCode { get; init; }

    /// <summary>Gets the status detail.</summary>
    public required string StatusDetail { get; init; }
}

/// <summary>Topic partitions routed to one interactive-query endpoint.</summary>
public sealed class StreamsGroupEndpointAssignment
{
    /// <summary>Gets the interactive-query endpoint.</summary>
    public required StreamsGroupEndpoint UserEndpoint { get; init; }

    /// <summary>Gets active topic partitions routed to the endpoint.</summary>
    public required IReadOnlyList<StreamsGroupTopicPartitions> ActivePartitions { get; init; }

    /// <summary>Gets standby topic partitions routed to the endpoint.</summary>
    public required IReadOnlyList<StreamsGroupTopicPartitions> StandbyPartitions { get; init; }
}

/// <summary>Partitions grouped by topic name.</summary>
public sealed class StreamsGroupTopicPartitions
{
    /// <summary>Gets the topic name.</summary>
    public required string Topic { get; init; }

    /// <summary>Gets partitions for the topic.</summary>
    public required IReadOnlyList<int> Partitions { get; init; }
}

/// <summary>Specifies how close affects Streams-group membership.</summary>
public enum StreamsGroupMembershipOperation
{
    /// <summary>Dynamic members leave; static members retain membership.</summary>
    Default,

    /// <summary>Permanently leaves the group, including static membership.</summary>
    LeaveGroup,

    /// <summary>Stops without a terminal heartbeat; membership remains until session expiry.</summary>
    RemainInGroup
}

/// <summary>Options controlling Streams group member shutdown.</summary>
public sealed class StreamsGroupCloseOptions
{
    /// <summary>Gets how close affects group membership.</summary>
    public StreamsGroupMembershipOperation GroupMembershipOperation { get; init; } =
        StreamsGroupMembershipOperation.Default;

    /// <summary>Gets whether the terminal heartbeat requests application-wide shutdown.</summary>
    public bool ShutdownApplication { get; init; }
}
