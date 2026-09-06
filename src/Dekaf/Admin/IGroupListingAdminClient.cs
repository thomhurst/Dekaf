namespace Dekaf.Admin;

/// <summary>Optional admin capability for listing every group type (KIP-1043).</summary>
public interface IGroupListingAdminClient
{
    /// <summary>Lists groups across brokers, retaining their type and protocol.</summary>
    ValueTask<IReadOnlyList<GroupListing>> ListGroupsAsync(
        ListGroupsOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Filters for a unified group inventory. Filters combine with AND; values within a filter combine with OR.</summary>
public sealed class ListGroupsOptions
{
    /// <summary>Group states, compared without case. Null or empty includes all states. Requires ListGroups v4.</summary>
    public IReadOnlyList<string>? States { get; init; }

    /// <summary>Group types, such as classic, consumer, share, or streams, compared without case. Null or empty includes all types. Requires ListGroups v5.</summary>
    public IReadOnlyList<string>? Types { get; init; }

    /// <summary>Exact protocol strings. An empty string selects offset-only simple groups; null or an empty list includes all protocols.</summary>
    public IReadOnlyList<string>? ProtocolTypes { get; init; }
}

/// <summary>Unified group listing for admin clients.</summary>
public static class AdminClientGroupListingExtensions
{
    /// <summary>Lists groups using the client's optional unified inventory capability.</summary>
    /// <exception cref="NotSupportedException">The client does not implement <see cref="IGroupListingAdminClient"/>.</exception>
    public static ValueTask<IReadOnlyList<GroupListing>> ListGroupsAsync(
        this IAdminClient adminClient,
        ListGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient is IGroupListingAdminClient listing
            ? listing.ListGroupsAsync(options, cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support unified group listing.");
    }
}
