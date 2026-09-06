using Dekaf.Admin;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IGroupListingAdminClient
{
    private static readonly string[] ConsumerGroupTypes = ["consumer", "classic"];
    private static readonly string[] ConsumerProtocolTypes = ["consumer", ""];
    private static readonly string[] ShareGroupTypes = ["share"];
    private static readonly string[] StreamsGroupTypes = ["streams"];

    public ValueTask<IReadOnlyList<GroupListing>> ListGroupsAsync(
        ListGroupsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ListGroupsCoreAsync(options?.States, options?.Types, options?.ProtocolTypes, cancellationToken);

    private async ValueTask<IReadOnlyList<GroupListing>> ListGroupsCoreAsync(
        IReadOnlyList<string>? states, IReadOnlyList<string>? types, IReadOnlyList<string>? protocols,
        CancellationToken cancellationToken)
    {
        AdminClient.ValidateGroupFilters(states, types, protocols);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        var inventory = _cluster.ListGroupInventory();
        if (states is not { Count: > 0 } && types is not { Count: > 0 } && protocols is not { Count: > 0 })
            return inventory;
        var result = new List<GroupListing>();
        for (var i = 0; i < inventory.Count; i++)
        {
            var group = inventory[i];
            if (AdminClient.MatchesGroupFilter(states, group.State, StringComparison.OrdinalIgnoreCase) &&
                AdminClient.MatchesGroupFilter(types, group.GroupType, StringComparison.OrdinalIgnoreCase) &&
                AdminClient.MatchesGroupFilter(protocols, group.ProtocolType, StringComparison.Ordinal))
                result.Add(group);
        }
        return result;
    }
}
