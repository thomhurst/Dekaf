using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class DynamoDbLeasePlannerTests
{
    [Test]
    public async Task SteadyState_KeepsEveryOwnedLease_AndWritesNothingElse()
    {
        var leases = Leases(8, ("a", 0, 3), ("b", 4, 7));

        var plan = Plan(8, ["a", "b"], "a", leases);

        await Assert.That(Join(plan.Keep)).IsEqualTo("0,1,2,3");
        await Assert.That(plan.Release).IsEmpty();
        await Assert.That(plan.Claim).IsEmpty();
    }

    [Test]
    public async Task OverShare_HandsBackWhatLiesOutsideTheAssignedRange()
    {
        var leases = Leases(8, ("m", 0, 7));

        // "b" ranks before "m", so "m" is now assigned the upper half.
        var plan = Plan(8, ["m", "b"], "m", leases);

        await Assert.That(Join(plan.Keep)).IsEqualTo("4,5,6,7");
        await Assert.That(Join(plan.Release)).IsEqualTo("0,1,2,3");
        await Assert.That(plan.Claim).IsEmpty();
    }

    [Test]
    public async Task HeldBucketOutsideTheRange_IsKept_RatherThanSwappedForAFreeOneInside()
    {
        // "a" is assigned 0-3 but still publishes bucket 7 from an earlier membership.
        var leases = Leases(8, ("a", 0, 2), ("b", 4, 6));
        leases[7] = new DynamoDbLeaseState("a", Expired: false);

        var plan = Plan(8, ["a", "b"], "a", leases);

        await Assert.That(Join(plan.Keep)).IsEqualTo("0,1,2,7");
        await Assert.That(plan.Release).IsEmpty();
        await Assert.That(plan.Claim).IsEmpty();
    }

    [Test]
    public async Task UnderShare_ClaimsItsOwnRangeFirst()
    {
        var leases = Leases(8, ("a", 0, 3));

        var plan = Plan(8, ["a", "b"], "b", leases);

        await Assert.That(plan.Keep).IsEmpty();
        await Assert.That(Join(plan.Claim)).IsEqualTo("4,5,6,7");
    }

    [Test]
    public async Task FreeBuckets_AreSplitByTheSharedPlan_NotTakenByWhoeverAsksFirst()
    {
        // "b" left buckets 4-7 of 16 behind and all three survivors are short. Probing the
        // lowest free bucket first would send every one of them to bucket 4.
        var leases = Leases(16, ("a", 0, 3), ("c", 8, 11), ("d", 12, 15));
        List<string> relays = ["a", "c", "d"];

        var a = Plan(16, relays, "a", leases);
        var c = Plan(16, relays, "c", leases);
        var d = Plan(16, relays, "d", leases);

        await Assert.That(Join(a.Claim)).IsEqualTo("4,5");
        await Assert.That(Join(c.Claim)).IsEqualTo("6");
        await Assert.That(Join(d.Claim)).IsEqualTo("7");
    }

    [Test]
    public async Task OwnExpiredLease_IsKept_AndAPeersExpiredLeaseIsFree()
    {
        var leases = Leases(4, ("a", 0, 1), ("b", 2, 3));
        leases[1] = new DynamoDbLeaseState("a", Expired: true);
        leases[3] = new DynamoDbLeaseState("dead", Expired: true);

        var plan = Plan(4, ["a", "b"], "a", leases);

        // Bucket 3 belongs to the range of "b": "a" is at its share and takes nothing.
        await Assert.That(Join(plan.Keep)).IsEqualTo("0,1");
        await Assert.That(plan.Claim).IsEmpty();
        await Assert.That(Join(Plan(4, ["a", "b"], "b", leases).Claim)).IsEqualTo("3");
    }

    [Test]
    public async Task LiveLeaseOfARelayWithoutAHeartbeat_IsNotFree()
    {
        var leases = Leases(4, ("gone", 0, 3));

        var plan = Plan(4, ["a"], "a", leases);

        await Assert.That(plan.Claim).IsEmpty();
    }

    [Test]
    public async Task MoreRelaysThanBuckets_TheSurplusRelayOwnsNothing_AndHandsBackWhatItHeld()
    {
        var leases = Leases(2, ("c", 0, 1));

        var plan = Plan(2, ["a", "b", "c"], "c", leases);

        await Assert.That(plan.Keep).IsEmpty();
        await Assert.That(Join(plan.Release)).IsEqualTo("0,1");
        await Assert.That(plan.Claim).IsEmpty();
    }

    [Test]
    public async Task RequestingRelay_IsCountedEvenBeforeItsHeartbeatIsVisible()
    {
        var plan = Plan(8, ["a"], "b", Leases(8, ("a", 0, 3)));

        await Assert.That(Join(plan.Claim)).IsEqualTo("4,5,6,7");
    }

    [Test]
    public async Task LeaseCountThatDiffersFromTheBucketCount_IsRejected()
    {
        await Assert.That(() => DynamoDbLeasePlanner.Plan(8, ["a"], "a", new DynamoDbLeaseState[4]))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task RelaysThatReadTheSameState_NeverPlanTheSameWrite()
    {
        var random = new Random(20260918);
        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var bucketCount = random.Next(1, 40);
            var relays = Enumerable.Range(0, random.Next(1, 9)).Select(index => $"relay-{index}").ToList();
            var leases = new DynamoDbLeaseState[bucketCount];
            for (var bucket = 0; bucket < bucketCount; bucket++)
            {
                // Owners include relays without a heartbeat, and any lease may have lapsed.
                var owner = random.Next(0, relays.Count + 3);
                leases[bucket] = owner >= relays.Count + 1
                    ? default
                    : new DynamoDbLeaseState(owner == relays.Count ? "relay-gone" : relays[owner], random.Next(4) == 0);
            }

            var claimed = new Dictionary<int, string>();
            foreach (var relay in relays)
            {
                var plan = Plan(bucketCount, relays, relay, leases);
                var share = OutboxFairShare.Compute(bucketCount, [.. relays], relay);

                await Assert.That(plan.Keep.Count + plan.Claim.Count).IsLessThanOrEqualTo(share);
                await Assert.That(plan.Keep.Concat(plan.Release).Order()
                    .SequenceEqual(Enumerable.Range(0, bucketCount).Where(bucket => leases[bucket].Owner == relay))).IsTrue();
                foreach (var bucket in plan.Claim)
                {
                    var lease = leases[bucket];
                    await Assert.That(lease.Owner is null || (lease.Expired && lease.Owner != relay)).IsTrue();
                    await Assert.That(claimed.TryAdd(bucket, relay)).IsTrue();
                }
            }
        }
    }

    [Test]
    public async Task ChurningFleet_AlwaysSettlesOnAFairSplit_WithoutMovingAKeptBucket()
    {
        var random = new Random(3356);
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var bucketCount = random.Next(1, 33);
            var leases = new DynamoDbLeaseState[bucketCount];
            var relays = new List<string>();
            var nextRelay = 0;

            for (var change = 0; change < 6; change++)
            {
                // Relays join, or leave gracefully, a few at a time.
                if (relays.Count > 1 && random.Next(3) == 0)
                {
                    var leaving = relays[random.Next(relays.Count)];
                    relays.Remove(leaving);
                    for (var bucket = 0; bucket < bucketCount; bucket++)
                    {
                        if (leases[bucket].Owner == leaving)
                            leases[bucket] = default;
                    }
                }
                else
                {
                    relays.Add($"relay-{random.Next(100):D2}-{nextRelay++}");
                }

                for (var round = 0; round < 3; round++)
                {
                    // Each relay acquires in turn, in a different order every round.
                    foreach (var relay in relays.OrderBy(_ => random.Next()).ToArray())
                    {
                        var before = Owned(leases, relay);
                        var plan = Plan(bucketCount, relays, relay, leases);
                        foreach (var bucket in plan.Release)
                            leases[bucket] = default;
                        foreach (var bucket in plan.Claim)
                            leases[bucket] = new DynamoDbLeaseState(relay, Expired: false);

                        // Whatever a relay keeps is a bucket it held: nothing is swapped.
                        await Assert.That(plan.Keep.All(before.Contains)).IsTrue();
                    }
                }

                var counts = relays.Select(relay => Owned(leases, relay).Count).ToArray();
                await Assert.That(counts.Sum()).IsEqualTo(bucketCount);
                await Assert.That(counts.Max() - counts.Min()).IsLessThanOrEqualTo(1);

                // Settled means settled: another round writes no release and no claim.
                foreach (var relay in relays)
                {
                    var plan = Plan(bucketCount, relays, relay, leases);
                    await Assert.That(plan.Release.Count + plan.Claim.Count).IsEqualTo(0);
                }
            }
        }
    }

    private static DynamoDbLeasePlan Plan(
        int bucketCount, List<string> relays, string relayId, DynamoDbLeaseState[] leases) =>
        // A copy: the planner sorts the membership in place.
        DynamoDbLeasePlanner.Plan(bucketCount, [.. relays], relayId, leases);

    private static DynamoDbLeaseState[] Leases(int bucketCount, params (string Owner, int First, int Last)[] ranges)
    {
        var leases = new DynamoDbLeaseState[bucketCount];
        foreach (var (owner, first, last) in ranges)
        {
            for (var bucket = first; bucket <= last; bucket++)
                leases[bucket] = new DynamoDbLeaseState(owner, Expired: false);
        }

        return leases;
    }

    private static List<int> Owned(DynamoDbLeaseState[] leases, string relayId) =>
        [.. Enumerable.Range(0, leases.Length).Where(bucket => leases[bucket].Owner == relayId)];

    private static string Join(IEnumerable<int> buckets) => string.Join(',', buckets);
}
