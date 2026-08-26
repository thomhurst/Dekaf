namespace Dekaf.Admin;

/// <summary>
/// Streams group description.
/// </summary>
public sealed class StreamsGroupDescription
{
    public required string GroupId { get; init; }
    public required string GroupState { get; init; }
    public int GroupEpoch { get; init; }
    public int AssignmentEpoch { get; init; }
    public StreamsGroupTopology? Topology { get; init; }
    public required IReadOnlyList<StreamsGroupMemberDescription> Members { get; init; }
    public int AuthorizedOperations { get; init; } = int.MinValue;
}

/// <summary>
/// Streams topology metadata initialized for a group.
/// </summary>
public sealed class StreamsGroupTopology
{
    public int Epoch { get; init; }
    public IReadOnlyList<StreamsGroupSubtopology>? Subtopologies { get; init; }
}

/// <summary>
/// Streams subtopology metadata.
/// </summary>
public sealed class StreamsGroupSubtopology
{
    public required string SubtopologyId { get; init; }
    public required IReadOnlyList<string> SourceTopics { get; init; }
    public required IReadOnlyList<string> RepartitionSinkTopics { get; init; }
    public required IReadOnlyList<StreamsGroupTopicInfo> StateChangelogTopics { get; init; }
    public required IReadOnlyList<StreamsGroupTopicInfo> RepartitionSourceTopics { get; init; }
}

/// <summary>
/// Streams-internal topic metadata.
/// </summary>
public sealed class StreamsGroupTopicInfo
{
    public required string Name { get; init; }
    public int Partitions { get; init; }
    public short ReplicationFactor { get; init; }
    public required IReadOnlyList<StreamsGroupKeyValue> TopicConfigs { get; init; }
}

/// <summary>
/// Streams group key/value metadata.
/// </summary>
public sealed class StreamsGroupKeyValue
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}

/// <summary>
/// Streams group member description.
/// </summary>
public sealed class StreamsGroupMemberDescription
{
    public required string MemberId { get; init; }
    public int MemberEpoch { get; init; }
    public string? InstanceId { get; init; }
    public string? RackId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientHost { get; init; }
    public int TopologyEpoch { get; init; }
    public required string ProcessId { get; init; }
    public StreamsGroupEndpoint? UserEndpoint { get; init; }
    public required IReadOnlyList<StreamsGroupKeyValue> ClientTags { get; init; }
    public required IReadOnlyList<StreamsGroupTaskOffset> TaskOffsets { get; init; }
    public required IReadOnlyList<StreamsGroupTaskOffset> TaskEndOffsets { get; init; }
    public required StreamsGroupMemberAssignment Assignment { get; init; }
    public required StreamsGroupMemberAssignment TargetAssignment { get; init; }
    public bool IsClassic { get; init; }
}

/// <summary>
/// Interactive Query endpoint for a streams group member.
/// </summary>
public sealed class StreamsGroupEndpoint
{
    public required string Host { get; init; }
    public int Port { get; init; }
}

/// <summary>
/// Cumulative task changelog offset.
/// </summary>
public sealed class StreamsGroupTaskOffset
{
    public required string SubtopologyId { get; init; }
    public int Partition { get; init; }
    public long Offset { get; init; }
}

/// <summary>
/// Streams member assignment.
/// </summary>
public sealed class StreamsGroupMemberAssignment
{
    public required IReadOnlyList<StreamsGroupTaskIds> ActiveTasks { get; init; }
    public required IReadOnlyList<StreamsGroupTaskIds> StandbyTasks { get; init; }
    public required IReadOnlyList<StreamsGroupTaskIds> WarmupTasks { get; init; }
}

/// <summary>
/// Streams task IDs grouped by subtopology.
/// </summary>
public sealed class StreamsGroupTaskIds
{
    public required string SubtopologyId { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }
}

/// <summary>
/// Options for ListStreamsGroups.
/// </summary>
public sealed class ListStreamsGroupsOptions
{
    public IReadOnlyList<string>? States { get; init; }
}

/// <summary>
/// Selects the partitions whose committed offsets should be listed for a streams group.
/// </summary>
public sealed class ListStreamsGroupOffsetsSpec
{
    /// <summary>
    /// Partitions to list, or null to list every committed partition in the group.
    /// </summary>
    public IReadOnlyList<TopicPartition>? TopicPartitions { get; init; }
}

/// <summary>
/// Options for listing streams-group offsets.
/// </summary>
public sealed class ListStreamsGroupOffsetsOptions
{
    public bool RequireStable { get; init; }
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Result of listing one streams group's committed offsets.
/// </summary>
public sealed class StreamsGroupOffsetsResult
{
    public required string GroupId { get; init; }
    public Protocol.ErrorCode ErrorCode { get; init; }
    public required IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetDescription> Offsets { get; init; }
}

/// <summary>
/// A committed streams-group offset and its partition-level result.
/// </summary>
public sealed class StreamsGroupOffsetDescription
{
    public required TopicPartition TopicPartition { get; init; }
    public long Offset { get; init; } = -1;
    public int LeaderEpoch { get; init; } = -1;
    public string? Metadata { get; init; }
    public Protocol.ErrorCode ErrorCode { get; init; }
}

/// <summary>
/// Options for altering streams-group offsets.
/// </summary>
public sealed class AlterStreamsGroupOffsetsOptions
{
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Options for deleting streams-group offsets.
/// </summary>
public sealed class DeleteStreamsGroupOffsetsOptions
{
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Result of altering or deleting one streams-group partition offset.
/// </summary>
public sealed class StreamsGroupOffsetOperationResult
{
    public required TopicPartition TopicPartition { get; init; }
    public Protocol.ErrorCode ErrorCode { get; init; }
}

/// <summary>
/// Options for deleting streams groups.
/// </summary>
public sealed class DeleteStreamsGroupsOptions
{
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Result of deleting one streams group.
/// </summary>
public sealed class DeleteStreamsGroupResult
{
    public required string GroupId { get; init; }
    public Protocol.ErrorCode ErrorCode { get; init; }
}
