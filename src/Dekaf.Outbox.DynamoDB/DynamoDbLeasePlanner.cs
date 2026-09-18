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
    public static readonly DynamoDbLeasePlan Empty = new([], [], []);

    public DynamoDbLeasePlan(IReadOnlyList<int> keep, IReadOnlyList<int> release, IReadOnlyList<int> claim)
    {
        Keep = keep;
        Release = release;
        Claim = claim;
    }

    /// <summary>Leases this relay already owns and renews.</summary>
    public IReadOnlyList<int> Keep { get; }

    /// <summary>Leases this relay owns beyond its fair share and hands back.</summary>
    public IReadOnlyList<int> Release { get; }

    /// <summary>Free leases this relay tries to take.</summary>
    public IReadOnlyList<int> Claim { get; }
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

        // Assign also sorts the membership and adds this relay, for every later call.
        var range = OutboxFairShare.Assign(bucketCount, activeRelayIds, relayId);
        var share = range.Count;

        // An expired lease that still names this relay is still this relay's to renew: the
        // owner condition proves that no peer took it in the meantime.
        var owned = new List<int>();
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            if (leases[bucket].Owner == relayId)
                owned.Add(bucket);
        }

        // Keep the assigned range first. What an over-share relay hands back is then what its
        // peers are assigned, so a joining relay finds its own range free.
        owned.Sort((left, right) =>
        {
            var byRange = InRange(range, right).CompareTo(InRange(range, left));
            return byRange != 0 ? byRange : left.CompareTo(right);
        });
        var keepCount = Math.Min(owned.Count, share);
        var keep = owned.GetRange(0, keepCount);
        var release = owned.GetRange(keepCount, owned.Count - keepCount);
        keep.Sort();
        release.Sort();

        var claim = keepCount < share ? PlanClaims(bucketCount, activeRelayIds, relayId, leases) : [];
        return new DynamoDbLeasePlan(keep, release, claim);
    }

    private static List<int> PlanClaims(
        int bucketCount, List<string> sortedRelayIds, string relayId, ReadOnlySpan<DynamoDbLeaseState> leases)
    {
        var free = new bool[bucketCount];
        var anyFree = false;
        var held = new Dictionary<string, int>(sortedRelayIds.Count, StringComparer.Ordinal);
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var lease = leases[bucket];
            if (lease.Owner is null || (lease.Expired && lease.Owner != relayId))
            {
                free[bucket] = true;
                anyFree = true;
            }
            else
            {
                held[lease.Owner] = held.GetValueOrDefault(lease.Owner) + 1;
            }
        }

        var claims = new List<int>();
        if (!anyFree)
            return claims;

        var ranges = new IReadOnlyList<int>[sortedRelayIds.Count];
        var deficits = new int[sortedRelayIds.Count];
        for (var rank = 0; rank < sortedRelayIds.Count; rank++)
        {
            ranges[rank] = OutboxFairShare.Assign(bucketCount, sortedRelayIds, sortedRelayIds[rank]);
            deficits[rank] = Math.Max(0, ranges[rank].Count - held.GetValueOrDefault(sortedRelayIds[rank]));
        }

        // First every relay takes the free buckets of its own range. Ranges are disjoint, so
        // these claims cannot overlap whatever order the relays run in.
        for (var rank = 0; rank < sortedRelayIds.Count; rank++)
        {
            foreach (var bucket in ranges[rank])
            {
                if (deficits[rank] == 0)
                    break;
                if (free[bucket])
                    Take(rank, bucket);
            }
        }

        // Then the buckets nobody is assigned and short of, lowest rank first. A relay is left
        // short when a peer still holds part of its range from an earlier membership.
        var next = 0;
        for (var rank = 0; rank < sortedRelayIds.Count; rank++)
        {
            for (; deficits[rank] > 0 && next < bucketCount; next++)
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
            if (sortedRelayIds[rank] == relayId)
                claims.Add(bucket);
        }
    }

    // Ranges are contiguous and ascending.
    private static bool InRange(IReadOnlyList<int> range, int bucket) =>
        range.Count > 0 && bucket >= range[0] && bucket <= range[^1];
}
