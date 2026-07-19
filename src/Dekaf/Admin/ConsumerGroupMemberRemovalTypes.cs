namespace Dekaf.Admin;

/// <summary>
/// Identifies a static consumer group member to remove.
/// </summary>
public sealed class ConsumerGroupMemberToRemove
{
    /// <summary>
    /// Static membership identifier configured as group.instance.id.
    /// </summary>
    public required string GroupInstanceId { get; init; }
}

/// <summary>
/// Options for removing static members from a consumer group.
/// </summary>
public sealed class RemoveMembersFromConsumerGroupOptions
{
    /// <summary>
    /// Optional audit reason sent to brokers supporting LeaveGroup v5 or later.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Results of a static consumer group member removal request.
/// </summary>
public sealed class RemoveMembersFromConsumerGroupResult
{
    public required string GroupId { get; init; }
    public required IReadOnlyList<ConsumerGroupMemberRemovalResult> Members { get; init; }
    public bool Succeeded => Members.All(static member => member.ErrorCode == Protocol.ErrorCode.None);
}

/// <summary>
/// Removal result for one static consumer group member.
/// </summary>
public sealed class ConsumerGroupMemberRemovalResult
{
    public required string GroupInstanceId { get; init; }
    public required string MemberId { get; init; }
    public Protocol.ErrorCode ErrorCode { get; init; }
    public bool Succeeded => ErrorCode == Protocol.ErrorCode.None;
}
