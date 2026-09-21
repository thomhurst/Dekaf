namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// One bucket's lease as a relay read it. <see cref="Owner"/> is null for a lease that was
/// never written or was released.
/// </summary>
internal readonly record struct DynamoDbLeaseState(string? Owner, bool Expired);

/// <summary>
/// The conditional writes one relay makes in one acquisition round.
/// </summary>
internal sealed class DynamoDbLeasePlan
{
    public static readonly DynamoDbLeasePlan Empty = new([], [], [], standbyRank: -1);

    public DynamoDbLeasePlan(
        IReadOnlyList<int> keep, IReadOnlyList<int> release, IReadOnlyList<int> claim, int standbyRank)
    {
        Keep = keep;
        Release = release;
        Claim = claim;
        StandbyRank = standbyRank;
    }

    /// <summary>Leases this relay already owns and renews.</summary>
    public IReadOnlyList<int> Keep { get; }

    /// <summary>Leases this relay owns beyond its fair share and hands back.</summary>
    public IReadOnlyList<int> Release { get; }

    /// <summary>Free leases this relay tries to take.</summary>
    public IReadOnlyList<int> Claim { get; }

    /// <summary>
    /// <see cref="OutboxFairShare.StandbyRank"/> of a relay that holds nothing and has no
    /// share; -1 for every other relay.
    /// </summary>
    public int StandbyRank { get; }
}

/// <summary>
/// Decides which leases a relay writes, so that relays which read the same lease table and
/// the same membership never write the same lease.
/// </summary>
/// <remarks>
/// <para>DynamoDB has no set-based conditional write, so every lease is one conditional
/// request, and every refused request is billed and reported as an error by the AWS SDK. A
/// relay that probed free buckets in its own order would collide with every peer doing the
/// same. Instead each relay computes the claims of <em>every</em> active relay with one
/// deterministic rule and carries out only its own part. Relays that agree on the inputs get
/// disjoint claims and no refusal.</para>
/// <para>Relays disagree only while a membership change is becoming visible: their reads
/// happen at different moments. The plans can then overlap for a round, the conditional write
/// refuses the loser, and the next round agrees again. The plan orders writes; it never grants
/// ownership.</para>
/// <para>Shares follow what the relays hold (see <see cref="OutboxFairShare"/>), so a relay
/// that joins a fleet with more relays than buckets takes nothing from an incumbent, and the
/// ranges follow relay-id order alone, so a peer that is part of the way through its claims
/// moves nobody's range.</para>
/// </remarks>
internal static class DynamoDbLeasePlanner
{
    /// <param name="bucketCount">Total bucket count.</param>
    /// <param name="activeRelayIds">Relays with a live heartbeat. Sorted in place; the
    /// requesting relay is added if missing.</param>
    /// <param name="relayId">The requesting relay.</param>
    /// <param name="leases">The lease of every bucket, indexed by bucket.</param>
    public static DynamoDbLeasePlan Plan(
        int bucketCount, List<string> activeRelayIds, string relayId, ReadOnlySpan<DynamoDbLeaseState> leases)
    {
        if (leases.Length != bucketCount)
            throw new ArgumentException("One lease state per bucket is required.", nameof(leases));

        // What every relay holds, as this relay sees it: a lapsed lease is free, except this
        // relay's own, which the owner condition still lets it renew.
        var free = new bool[bucketCount];
        var held = new Dictionary<string, int>(activeRelayIds.Count, StringComparer.Ordinal);
        var owned = new List<int>();
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var lease = leases[bucket];
            if (lease.Owner is null || (lease.Expired && lease.Owner != relayId))
            {
                free[bucket] = true;
                continue;
            }

            held[lease.Owner] = held.GetValueOrDefault(lease.Owner) + 1;
            if (lease.Owner == relayId)
                owned.Add(bucket);
        }

        // Every relay's share from one pass, and with it every range: a range starts where
        // the shares of the relays that sort before it end.
        if (!activeRelayIds.Contains(relayId))
            activeRelayIds.Add(relayId);
        var shares = OutboxFairShare.ComputeAll(bucketCount, activeRelayIds, held);
        var starts = new int[shares.Length];
        for (var lower = 1; lower < shares.Length; lower++)
            starts[lower] = starts[lower - 1] + shares[lower - 1];

        var rank = activeRelayIds.IndexOf(relayId);
        var share = shares[rank];
        var first = starts[rank];
        bool InRange(int bucket) => bucket >= first && bucket < first + share;

        // Keep the assigned range first. What an over-share relay hands back is then what its
        // peers are assigned, so a joining relay finds its own range free.
        owned.Sort((left, right) =>
        {
            var byRange = InRange(right).CompareTo(InRange(left));
            return byRange != 0 ? byRange : left.CompareTo(right);
        });
        var keepCount = Math.Min(owned.Count, share);
        var keep = owned.GetRange(0, keepCount);
        var release = owned.GetRange(keepCount, owned.Count - keepCount);
        keep.Sort();
        release.Sort();

        var claim = keepCount < share ? PlanClaims(activeRelayIds, rank, free, held, shares, starts) : [];
        return new DynamoDbLeasePlan(keep, release, claim, StandbyRank(shares, rank, owned.Count));
    }

    /// <returns><see cref="OutboxFairShare.StandbyRank"/>, read from the shares this round
    /// already computed.</returns>
    private static int StandbyRank(int[] shares, int rank, int ownedCount)
    {
        if (shares[rank] > 0 || ownedCount > 0)
            return -1;

        var standbysBefore = 0;
        for (var lower = 0; lower < rank; lower++)
        {
            if (shares[lower] == 0)
                standbysBefore++;
        }

        return standbysBefore;
    }

    /// <param name="sortedRelayIds">The membership, in the order of <paramref name="shares"/>.</param>
    /// <param name="ownRank">The requesting relay's position in it.</param>
    /// <param name="free">Whether each bucket is free. Consumed by the simulation.</param>
    /// <param name="held">What each relay holds.</param>
    /// <param name="shares">Every relay's share, by rank.</param>
    /// <param name="starts">Where every relay's range starts, by rank.</param>
    private static List<int> PlanClaims(
        List<string> sortedRelayIds, int ownRank, bool[] free, Dictionary<string, int> held, int[] shares, int[] starts)
    {
        var claims = new List<int>();
        if (Array.IndexOf(free, true) < 0)
            return claims;

        var deficits = new int[shares.Length];
        for (var rank = 0; rank < shares.Length; rank++)
            deficits[rank] = Math.Max(0, shares[rank] - held.GetValueOrDefault(sortedRelayIds[rank]));

        // First every relay takes the free buckets of its own range. Ranges are disjoint, so
        // these claims cannot overlap whatever order the relays run in.
        for (var rank = 0; rank < shares.Length; rank++)
        {
            for (var bucket = starts[rank]; bucket < starts[rank] + shares[rank] && deficits[rank] > 0; bucket++)
            {
                if (free[bucket])
                    Take(rank, bucket);
            }
        }

        // Then the buckets nobody is assigned and short of, lowest relay id first. A relay is
        // left short when a peer still holds part of its range from an earlier membership.
        var next = 0;
        for (var rank = 0; rank < shares.Length; rank++)
        {
            for (; deficits[rank] > 0 && next < free.Length; next++)
            {
                if (free[next])
                    Take(rank, next);
            }
        }

        claims.Sort();
        return claims;

        void Take(int rank, int bucket)
        {
            free[bucket] = false;
            deficits[rank]--;
            if (rank == ownRank)
                claims.Add(bucket);
        }
    }
}
