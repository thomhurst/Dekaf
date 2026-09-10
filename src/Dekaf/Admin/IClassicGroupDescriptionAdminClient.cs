using Dekaf.Protocol;

namespace Dekaf.Admin;

/// <summary>Optional capability for inspecting classic groups, including non-consumer protocols.</summary>
public interface IClassicGroupDescriptionAdminClient
{
    /// <summary>Describes classic groups through DescribeGroups, preserving each group's outcome.</summary>
    ValueTask<IReadOnlyDictionary<string, ClassicGroupDescriptionResult>> DescribeClassicGroupsAsync(
        IEnumerable<string> groupIds, DescribeClassicGroupsOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Options for classic-group inspection.</summary>
public sealed class DescribeClassicGroupsOptions
{
    /// <summary>Requests the broker's authorized-operations bit field.</summary>
    public bool IncludeAuthorizedOperations { get; init; }
    /// <summary>Total operation budget in milliseconds, including discovery and retries.</summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>The outcome for one requested group. Description is null on failure.</summary>
public sealed class ClassicGroupDescriptionResult
{
    public required string GroupId { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public ClassicGroupDescription? Description { get; init; }
}

/// <summary>A classic group's protocol, state and members, without assuming a consumer protocol.</summary>
public sealed class ClassicGroupDescription
{
    public required string GroupId { get; init; }
    public string? ProtocolType { get; init; }
    /// <summary>Selected protocol name: assignor for consumer groups, protocol name for Connect/custom groups.</summary>
    public string? ProtocolData { get; init; }
    public required string State { get; init; }
    public int CoordinatorId { get; init; }
    /// <summary>Broker bit field, or null when not requested or unavailable.</summary>
    public int? AuthorizedOperations { get; init; }
    public required IReadOnlyList<ClassicGroupMemberDescription> Members { get; init; }
}

/// <summary>A classic member with opaque protocol bytes retained for custom decoders.</summary>
public sealed class ClassicGroupMemberDescription
{
    public required string MemberId { get; init; }
    public string? GroupInstanceId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientHost { get; init; }
    public ReadOnlyMemory<byte> Metadata { get; init; }
    public ReadOnlyMemory<byte> AssignmentData { get; init; }
    /// <summary>Decoded only for the exact consumer protocol; null for custom protocols or an absent/unparseable assignment.</summary>
    public IReadOnlyList<TopicPartition>? Assignment { get; init; }
}

/// <summary>Classic-group inspection for clients supporting the additive capability.</summary>
public static class AdminClientClassicGroupDescriptionExtensions
{
    /// <summary>Describes classic groups. Successful groups remain available alongside group errors.</summary>
    /// <exception cref="NotSupportedException">The client does not implement the optional capability.</exception>
    public static ValueTask<IReadOnlyDictionary<string, ClassicGroupDescriptionResult>> DescribeClassicGroupsAsync(
        this IAdminClient adminClient, IEnumerable<string> groupIds,
        DescribeClassicGroupsOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient is IClassicGroupDescriptionAdminClient classic
            ? classic.DescribeClassicGroupsAsync(groupIds, options, cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support classic-group descriptions.");
    }
}
