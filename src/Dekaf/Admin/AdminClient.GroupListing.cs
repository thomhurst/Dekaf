using System.Runtime.CompilerServices;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IGroupListingAdminClient
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
        IReadOnlyList<string>? states,
        IReadOnlyList<string>? types,
        IReadOnlyList<string>? protocols,
        CancellationToken cancellationToken)
    {
        ValidateGroupFilters(states, types, protocols);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        return await WithRetryAsync<IReadOnlyList<GroupListing>>(async () =>
        {
            var brokers = _metadataManager.Metadata.GetBrokers();
            if (brokers.Count == 0)
                throw new InvalidOperationException("No brokers available");

            // Listing is an administrative operation. Fan out once per broker, not per group.
            var pending = new Task<ListGroupsResponse>[brokers.Count];
            for (var i = 0; i < brokers.Count; i++)
                pending[i] = ListBrokerGroupsAsync(brokers[i].NodeId, states, types, cancellationToken);
            var responses = await Task.WhenAll(pending).ConfigureAwait(false);

            var capacity = 0;
            foreach (var response in responses)
                capacity = checked(capacity + response.Groups.Count);
            // Broker responses already give an upper bound. Avoid repeatedly growing
            // the inventory and deduplication buffers for large administrative listings.
#if NETSTANDARD2_0
            // The capacity constructor is unavailable in the compatibility target.
            var seen = new HashSet<string>(StringComparer.Ordinal);
#else
            var seen = new HashSet<string>(capacity, StringComparer.Ordinal);
#endif
            var result = new List<GroupListing>(capacity);
            foreach (var response in responses)
            {
                for (var i = 0; i < response.Groups.Count; i++)
                {
                    var group = response.Groups[i];
                    // Filter before deduplication: a stale nonmatching coordinator response
                    // must not hide a matching response from the current coordinator.
                    if (!MatchesGroupFilter(states, group.GroupState, StringComparison.OrdinalIgnoreCase) ||
                        !MatchesGroupFilter(types, group.GroupType, StringComparison.OrdinalIgnoreCase) ||
                        !MatchesGroupFilter(protocols, group.ProtocolType, StringComparison.Ordinal) ||
                        !seen.Add(group.GroupId))
                        continue;

                    result.Add(new GroupListing
                    {
                        GroupId = group.GroupId,
                        GroupType = group.GroupType,
                        ProtocolType = group.ProtocolType,
                        State = group.GroupState
                    });
                }
            }
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ListGroupsResponse> ListBrokerGroupsAsync(int brokerId,
        IReadOnlyList<string>? states, IReadOnlyList<string>? types, CancellationToken cancellationToken)
    {
        using var lease = await _connectionPool.LeaseConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);
        var connection = lease.Connection;
        var version = _metadataManager.GetNegotiatedApiVersion(connection, ApiKey.ListGroups,
            ListGroupsRequest.LowestSupportedVersion, ListGroupsRequest.HighestSupportedVersion);
        if (states is { Count: > 0 } && version < 4)
            throw new KafkaException(ErrorCode.UnsupportedVersion,
                $"Group state filtering requires ListGroups v4; broker {brokerId} negotiated v{version}.");
        if (types is { Count: > 0 } && version < 5)
            throw new KafkaException(ErrorCode.UnsupportedVersion,
                $"Group type filtering requires ListGroups v5; broker {brokerId} negotiated v{version}.");

        var response = await connection.SendAsync<ListGroupsRequest, ListGroupsResponse>(new ListGroupsRequest
        {
            StatesFilter = states,
            TypesFilter = types
        }, version, cancellationToken).ConfigureAwait(false);
        if (response.ErrorCode != ErrorCode.None)
            throw new KafkaException(response.ErrorCode, $"ListGroups failed on broker {brokerId}: {response.ErrorCode}");
        return response;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchesGroupFilter(IReadOnlyList<string>? filter, string? value, StringComparison comparison)
    {
        if (filter is not { Count: > 0 })
            return true;
        if (value is null)
            return false;
        for (var i = 0; i < filter.Count; i++)
        {
            if (string.Equals(filter[i], value, comparison))
                return true;
        }
        return false;
    }

    internal static void ValidateGroupFilters(IReadOnlyList<string>? states,
        IReadOnlyList<string>? types, IReadOnlyList<string>? protocols)
    {
        if (states is not null)
        {
            for (var i = 0; i < states.Count; i++)
                ArgumentException.ThrowIfNullOrWhiteSpace(states[i], nameof(states));
        }
        if (types is not null)
        {
            for (var i = 0; i < types.Count; i++)
                ArgumentException.ThrowIfNullOrWhiteSpace(types[i], nameof(types));
        }
        if (protocols is not null)
        {
            for (var i = 0; i < protocols.Count; i++)
                ArgumentNullException.ThrowIfNull(protocols[i], nameof(protocols));
        }
    }
}
