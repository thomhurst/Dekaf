namespace Dekaf.Admin;

/// <summary>Optional capability for explicit member identities and snapshot-based group eviction.</summary>
public interface IConsumerGroupMemberRemovalAdminClient
{
    ValueTask<RemoveMembersFromConsumerGroupResult> RemoveMembersFromConsumerGroupAsync(
        string groupId, ConsumerGroupMemberRemovalOptions options, CancellationToken cancellationToken = default);
}

/// <summary>Consumer-group eviction for admin clients supporting the optional capability.</summary>
public static class AdminClientConsumerGroupMemberRemovalExtensions
{
    /// <summary>Removes explicit members or the members discovered in one group snapshot.</summary>
    /// <remarks>Eviction does not stop applications or prevent concurrent joins and rejoins.</remarks>
    public static ValueTask<RemoveMembersFromConsumerGroupResult> RemoveMembersFromConsumerGroupAsync(
        this IAdminClient adminClient, string groupId, ConsumerGroupMemberRemovalOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient is IConsumerGroupMemberRemovalAdminClient capability
            ? capability.RemoveMembersFromConsumerGroupAsync(groupId, options, cancellationToken)
            : throw new NotSupportedException("This admin client does not support consumer-group member identities or remove-all.");
    }
}

/// <summary>Identifies a member by exactly one static instance ID or dynamic member ID.</summary>
public sealed class ConsumerGroupMemberIdentity
{
    public string? GroupInstanceId { get; init; }
    public string? MemberId { get; init; }
}

/// <summary>Options for explicit or snapshot-based consumer-group member removal.</summary>
public sealed class ConsumerGroupMemberRemovalOptions
{
    /// <summary>Discovers current membership once. Cannot be combined with explicit members.</summary>
    public bool RemoveAll { get; init; }
    /// <summary>Explicit identities. A nonempty collection is required unless RemoveAll is true.</summary>
    public IReadOnlyList<ConsumerGroupMemberIdentity> Members { get; init; } = [];
    /// <summary>Optional audit reason sent when LeaveGroup v5 is supported.</summary>
    public string? Reason { get; init; }
    /// <summary>End-to-end timeout, including discovery and retries. Zero requests an immediate deadline.</summary>
    public int TimeoutMs { get; init; } = 30000;
}
