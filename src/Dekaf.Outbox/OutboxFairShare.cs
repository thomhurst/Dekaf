namespace Dekaf.Outbox;

/// <summary>
/// Computes a relay's fair share of the ordering buckets. Lives in the core package for the
/// same reason as <see cref="OutboxBucket"/>: the anti-starvation guarantee only holds when
/// every store implementation computes shares identically, so the algorithm must not be
/// re-derived per adapter.
/// </summary>
/// <remarks>
/// <para>Shares are the floor plus a remainder rather than a uniform ceiling: with a ceiling,
/// incumbents at exactly ceil(N/R) never release anything when a new relay joins, starving it
/// forever. Floor-and-remainder shares always sum to exactly the bucket count, so incumbents
/// above their share release and the joiner can claim.</para>
/// <para>The remainder goes first to the relays that already hold more than the floor, in
/// ordinal relay-id order, and then to the others in the same order. A store that knows what
/// each relay holds passes it in, and a relay that joins then never takes a bucket that an
/// incumbent could have kept: with more relays than buckets it owns nothing and waits, whatever
/// its id. Without that knowledge every relay ranks by id alone, and a joiner whose id sorts
/// early displaces an incumbent although the split was already fair.</para>
/// </remarks>
public static class OutboxFairShare
{
    private static readonly Dictionary<string, int> NoHeldBuckets = new(0);

    /// <summary>
    /// Computes this relay's share of <paramref name="bucketCount"/> buckets from membership
    /// alone. Prefer the overload that takes the held bucket counts when the store can read them.
    /// </summary>
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Ids of relays currently considered active. Sorted
    /// in place (ordinal); <paramref name="relayId"/> is added if missing.</param>
    /// <param name="relayId">The requesting relay's id.</param>
    public static int Compute(int bucketCount, List<string> activeRelayIds, string relayId) =>
        Compute(bucketCount, activeRelayIds, relayId, NoHeldBuckets);

    /// <summary>
    /// Computes this relay's share of <paramref name="bucketCount"/> buckets, leaving the
    /// buckets that incumbents hold where they are whenever the split allows it.
    /// </summary>
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Ids of relays currently considered active. Sorted
    /// in place (ordinal); <paramref name="relayId"/> is added if missing.</param>
    /// <param name="relayId">The requesting relay's id.</param>
    /// <param name="heldBucketCounts">How many unexpired leases name each relay, as the caller
    /// read them. Relays that are missing hold nothing; entries of relays that are not active
    /// are ignored.</param>
    /// <remarks>
    /// Only whether a relay holds more than the floor matters, which a relay moving towards
    /// its share never changes for a peer: a relay that releases stops at its share, and one
    /// that claims stops there too. Relays that agree on membership therefore keep computing
    /// the same shares while their peers are part of the way through a rebalance.
    /// </remarks>
    public static int Compute(
        int bucketCount, List<string> activeRelayIds, string relayId, IReadOnlyDictionary<string, int> heldBucketCounts)
    {
        ArgumentNullException.ThrowIfNull(heldBucketCounts);
        var rank = Rank(bucketCount, activeRelayIds, relayId);
        return Shares(bucketCount, activeRelayIds, heldBucketCounts)[rank];
    }

    /// <summary>
    /// Computes the share of every active relay at once, for a store that plans the claims
    /// of all of them: one call per round instead of one per relay.
    /// </summary>
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Ids of relays currently considered active, including the
    /// requesting relay. Sorted in place (ordinal).</param>
    /// <param name="heldBucketCounts">How many unexpired leases name each relay, as the caller
    /// read them. Relays that are missing hold nothing.</param>
    /// <returns>The shares in the sorted order of <paramref name="activeRelayIds"/>. They sum
    /// to <paramref name="bucketCount"/>, and the running total up to a relay is where its
    /// <c>Assign</c> range starts.</returns>
    public static int[] ComputeAll(
        int bucketCount, List<string> activeRelayIds, IReadOnlyDictionary<string, int> heldBucketCounts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketCount, 1);
        ArgumentNullException.ThrowIfNull(activeRelayIds);
        ArgumentNullException.ThrowIfNull(heldBucketCounts);
        if (activeRelayIds.Count == 0)
            throw new ArgumentException("At least one relay is required.", nameof(activeRelayIds));

        activeRelayIds.Sort(StringComparer.Ordinal);
        return Shares(bucketCount, activeRelayIds, heldBucketCounts);
    }

    /// <summary>
    /// Computes which buckets this relay should try to claim first, from membership alone.
    /// Prefer the overload that takes the held bucket counts when the store can read them.
    /// </summary>
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Ids of relays currently considered active. Sorted
    /// in place (ordinal); <paramref name="relayId"/> is added if missing.</param>
    /// <param name="relayId">The requesting relay's id.</param>
    /// <returns>The assigned buckets in ascending order; as many as <see cref="Compute(int, List{string}, string)"/>
    /// returns for the same arguments.</returns>
    public static IReadOnlyList<int> Assign(int bucketCount, List<string> activeRelayIds, string relayId) =>
        Assign(bucketCount, activeRelayIds, relayId, NoHeldBuckets);

    /// <summary>
    /// Computes which buckets this relay should try to claim first: the contiguous range
    /// obtained by accumulating the <see cref="Compute(int, List{string}, string, IReadOnlyDictionary{string, int})"/>
    /// shares in ordinal relay-id order.
    /// </summary>
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Ids of relays currently considered active. Sorted
    /// in place (ordinal); <paramref name="relayId"/> is added if missing.</param>
    /// <param name="relayId">The requesting relay's id.</param>
    /// <param name="heldBucketCounts">How many unexpired leases name each relay, as the caller
    /// read them. Relays that are missing hold nothing.</param>
    /// <remarks>
    /// Relays that agree on membership and on the leases compute disjoint ranges that cover
    /// exactly [0, <paramref name="bucketCount"/>), so their claims do not collide. This orders
    /// the probes; it does not grant ownership. Relays do not always agree on membership, because
    /// liveness records become visible with a lag (some stores can only read them with
    /// eventual consistency), so ranges overlap transiently and the store's atomic conditional
    /// write must remain the authority. Buckets a relay already holds come before this range:
    /// moving a held bucket to match the range would churn a bucket that is being published.
    /// </remarks>
    /// <returns>The assigned buckets in ascending order; as many as <c>Compute</c> returns for
    /// the same arguments.</returns>
    public static IReadOnlyList<int> Assign(
        int bucketCount, List<string> activeRelayIds, string relayId, IReadOnlyDictionary<string, int> heldBucketCounts)
    {
        ArgumentNullException.ThrowIfNull(heldBucketCounts);
        var rank = Rank(bucketCount, activeRelayIds, relayId);
        var shares = Shares(bucketCount, activeRelayIds, heldBucketCounts);

        var start = 0;
        for (var lower = 0; lower < rank; lower++)
            start += shares[lower];

        var assigned = new int[shares[rank]];
        for (var index = 0; index < assigned.Length; index++)
            assigned[index] = start + index;
        return assigned;
    }

    /// <summary>
    /// Computes how far back this relay waits for a bucket when there are more relays than
    /// buckets.
    /// </summary>
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Ids of relays currently considered active. Sorted
    /// in place (ordinal); <paramref name="relayId"/> is added if missing.</param>
    /// <param name="relayId">The requesting relay's id.</param>
    /// <param name="heldBucketCounts">How many unexpired leases name each relay, as the caller
    /// read them. Relays that are missing hold nothing.</param>
    /// <returns>-1 for a relay with a share. Otherwise the relay's zero-based position, in
    /// ordinal relay-id order, among the relays without one: when buckets free up, the relays
    /// at the lowest positions are the ones given a share. A store can let a relay far back
    /// in that order refresh its liveness record less often than the owners renew.</returns>
    public static int StandbyRank(
        int bucketCount, List<string> activeRelayIds, string relayId, IReadOnlyDictionary<string, int> heldBucketCounts)
    {
        ArgumentNullException.ThrowIfNull(heldBucketCounts);
        var rank = Rank(bucketCount, activeRelayIds, relayId);
        var shares = Shares(bucketCount, activeRelayIds, heldBucketCounts);
        if (shares[rank] > 0)
            return -1;

        var standbysBefore = 0;
        for (var lower = 0; lower < rank; lower++)
        {
            if (shares[lower] == 0)
                standbysBefore++;
        }

        return standbysBefore;
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

    /// <returns>The share of every relay, by rank: the floor, plus one for some.</returns>
    private static int[] Shares(
        int bucketCount, List<string> sortedRelayIds, IReadOnlyDictionary<string, int> heldBucketCounts)
    {
        var floor = bucketCount / sortedRelayIds.Count;
        var shares = new int[sortedRelayIds.Count];
        Array.Fill(shares, floor);

        // The remainder goes to the relays that already hold more than the floor, then to the
        // rest, both in id order. Taking it from a holder to give it to a peer that merely
        // sorts earlier would move a bucket that is being published for nothing.
        var left = bucketCount % sortedRelayIds.Count;
        for (var rank = 0; rank < shares.Length && left > 0; rank++)
        {
            if (heldBucketCounts.TryGetValue(sortedRelayIds[rank], out var held) && held > floor)
            {
                shares[rank]++;
                left--;
            }
        }

        for (var rank = 0; rank < shares.Length && left > 0; rank++)
        {
            if (shares[rank] == floor)
            {
                shares[rank]++;
                left--;
            }
        }

        return shares;
    }
}
