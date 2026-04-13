namespace Dekaf.Admin;

/// <summary>
/// Share group description.
/// </summary>
public sealed class ShareGroupDescription
{
    public required string GroupId { get; init; }
    public required string GroupState { get; init; }
    public int GroupEpoch { get; init; }
    public int AssignmentEpoch { get; init; }
    public string? AssignorName { get; init; }
    public required IReadOnlyList<ShareGroupMemberDescription> Members { get; init; }
    public int AuthorizedOperations { get; init; }
}

/// <summary>
/// Share group member description.
/// </summary>
public sealed class ShareGroupMemberDescription
{
    public required string MemberId { get; init; }
    public string? RackId { get; init; }
    public int MemberEpoch { get; init; }
    public required string ClientId { get; init; }
    public required string ClientHost { get; init; }
    public required IReadOnlyList<string> SubscribedTopicNames { get; init; }
    public IReadOnlyList<TopicPartition>? Assignment { get; init; }
}

/// <summary>
/// Options for ListShareGroups.
/// </summary>
public sealed class ListShareGroupsOptions
{
    public IReadOnlyList<string>? States { get; init; }
}

/// <summary>
/// Description of a share group's offset for a partition.
/// </summary>
public sealed class ShareGroupOffsetDescription
{
    public required TopicPartition TopicPartition { get; init; }
    public long StartOffset { get; init; }
    public int LeaderEpoch { get; init; }
    public long Lag { get; init; } = -1;
    public Protocol.ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Specification for altering a share group offset.
/// </summary>
public sealed class ShareGroupOffsetAlteration
{
    public required TopicPartition TopicPartition { get; init; }
    public required long StartOffset { get; init; }
}
