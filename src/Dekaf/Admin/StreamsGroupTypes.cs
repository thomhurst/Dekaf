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

    /// <summary>
    /// The full processing-graph description (KIP-1331). Non-null only when
    /// <see cref="TopologyDescriptionStatus"/> is <see cref="StreamsGroupTopologyDescriptionStatus.Available"/>.
    /// </summary>
    public StreamsGroupTopologyDescription? TopologyDescription { get; init; }

    /// <summary>
    /// Whether the topology description was requested and could be retrieved. This status is independent of
    /// the group's describe error: a group with <see cref="StreamsGroupTopologyDescriptionStatus.Error"/> is
    /// still described.
    /// </summary>
    public StreamsGroupTopologyDescriptionStatus TopologyDescriptionStatus { get; init; }
}

/// <summary>
/// Status of a Streams group's topology description (KIP-1331).
/// </summary>
public enum StreamsGroupTopologyDescriptionStatus : sbyte
{
    /// <summary>The description was not requested, or the broker predates StreamsGroupDescribe v1.</summary>
    NotRequested = 0,

    /// <summary>The broker has no description recorded for the group.</summary>
    NotStored = 1,

    /// <summary>The broker failed to fetch the description; see the broker logs.</summary>
    Error = 2,

    /// <summary>The description is available in <see cref="StreamsGroupDescription.TopologyDescription"/>.</summary>
    Available = 3
}

/// <summary>
/// Full Streams processing graph published by the application (KIP-1331).
/// </summary>
public sealed class StreamsGroupTopologyDescription
{
    public required IReadOnlyList<StreamsGroupTopologyDescriptionSubtopology> Subtopologies { get; init; }
    public required IReadOnlyList<StreamsGroupGlobalStore> GlobalStores { get; init; }
}

/// <summary>
/// Subtopology in a Streams topology description.
/// </summary>
public sealed class StreamsGroupTopologyDescriptionSubtopology
{
    public required string SubtopologyId { get; init; }
    public required IReadOnlyList<StreamsGroupTopologyNode> Nodes { get; init; }
}

/// <summary>
/// Type of a node in a Streams topology description.
/// </summary>
public enum StreamsGroupTopologyNodeType : sbyte
{
    Source = 1,
    Processor = 2,
    Sink = 3
}

/// <summary>
/// Processing node in a Streams topology description. Edges are directed from a node to its
/// <see cref="Successors"/>.
/// </summary>
public sealed class StreamsGroupTopologyNode
{
    public required string Name { get; init; }
    public StreamsGroupTopologyNodeType NodeType { get; init; }

    /// <summary>Topics read by a source node; empty when dynamically determined or for other node types.</summary>
    public required IReadOnlyList<string> SourceTopics { get; init; }

    /// <summary>Topic written by a sink node; null when dynamically determined or for other node types.</summary>
    public string? SinkTopic { get; init; }

    /// <summary>State stores accessed by a processor node.</summary>
    public required IReadOnlyList<string> Stores { get; init; }

    /// <summary>Names of the nodes this node forwards records to.</summary>
    public required IReadOnlyList<string> Successors { get; init; }
}

/// <summary>
/// Global state store in a Streams topology description.
/// </summary>
public sealed class StreamsGroupGlobalStore
{
    public required StreamsGroupTopologyNode Source { get; init; }
    public required StreamsGroupTopologyNode Processor { get; init; }
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
