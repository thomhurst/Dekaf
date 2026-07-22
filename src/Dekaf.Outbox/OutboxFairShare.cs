namespace Dekaf.Outbox;

/// <summary>
/// Computes a relay's fair share of the ordering buckets. Lives in the core package for the
/// same reason as <see cref="OutboxBucket"/>: the anti-starvation guarantee only holds when
/// every store implementation computes shares identically, so the algorithm must not be
/// re-derived per adapter.
/// </summary>
/// <remarks>
/// Shares are rank-based (floor plus remainder by ordinal relay-id rank) rather than a
/// uniform ceiling: with a ceiling, incumbents at exactly ceil(N/R) never release anything
/// when a new relay joins, starving it forever. Rank shares always sum to exactly the
/// bucket count, so incumbents above their share release and the joiner can claim.
/// </remarks>
public static class OutboxFairShare
{
    /// <summary>
    /// Computes this relay's share of <paramref name="bucketCount"/> buckets.
    /// </summary>
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Ids of relays currently considered active. Sorted
    /// in place (ordinal); <paramref name="relayId"/> is added if missing.</param>
    /// <param name="relayId">The requesting relay's id.</param>
    public static int Compute(int bucketCount, List<string> activeRelayIds, string relayId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketCount, 1);
        ArgumentNullException.ThrowIfNull(activeRelayIds);
        ArgumentException.ThrowIfNullOrEmpty(relayId);

        if (!activeRelayIds.Contains(relayId))
            activeRelayIds.Add(relayId);
        activeRelayIds.Sort(StringComparer.Ordinal);

        var rank = activeRelayIds.IndexOf(relayId);
        var remainder = bucketCount % activeRelayIds.Count;
        return (bucketCount / activeRelayIds.Count) + (rank < remainder ? 1 : 0);
    }
}
