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
        var rank = Rank(bucketCount, activeRelayIds, relayId);
        return Share(bucketCount, activeRelayIds.Count, rank);
    }

    /// <summary>
    /// Computes which buckets this relay should try to claim first: the contiguous range
    /// obtained by accumulating the <see cref="Compute"/> shares in relay-id rank order.
    /// </summary>
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Ids of relays currently considered active. Sorted
    /// in place (ordinal); <paramref name="relayId"/> is added if missing.</param>
    /// <param name="relayId">The requesting relay's id.</param>
    /// <remarks>
    /// Relays that agree on membership compute disjoint ranges that cover exactly
    /// [0, <paramref name="bucketCount"/>), so their claims do not collide. This orders the
    /// probes; it does not grant ownership. Relays do not always agree on membership, because
    /// liveness records become visible with a lag (some stores can only read them with
    /// eventual consistency), so ranges overlap transiently and the store's atomic conditional
    /// write must remain the authority. Buckets a relay already holds come before this range:
    /// moving a held bucket to match the range would churn a bucket that is being published.
    /// </remarks>
    /// <returns>The assigned buckets in ascending order; as many as <see cref="Compute"/>
    /// returns for the same arguments.</returns>
    public static IReadOnlyList<int> Assign(int bucketCount, List<string> activeRelayIds, string relayId)
    {
        var rank = Rank(bucketCount, activeRelayIds, relayId);
        var relayCount = activeRelayIds.Count;
        // Every lower rank holds the floor, and the first `remainder` ranks one more.
        var start = (rank * (bucketCount / relayCount)) + Math.Min(rank, bucketCount % relayCount);
        var assigned = new int[Share(bucketCount, relayCount, rank)];
        for (var index = 0; index < assigned.Length; index++)
            assigned[index] = start + index;
        return assigned;
    }

    private static int Rank(int bucketCount, List<string> activeRelayIds, string relayId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketCount, 1);
        ArgumentNullException.ThrowIfNull(activeRelayIds);
        ArgumentException.ThrowIfNullOrEmpty(relayId);

        if (!activeRelayIds.Contains(relayId))
            activeRelayIds.Add(relayId);
        activeRelayIds.Sort(StringComparer.Ordinal);
        return activeRelayIds.IndexOf(relayId);
    }

    private static int Share(int bucketCount, int relayCount, int rank) =>
        (bucketCount / relayCount) + (rank < bucketCount % relayCount ? 1 : 0);
}
