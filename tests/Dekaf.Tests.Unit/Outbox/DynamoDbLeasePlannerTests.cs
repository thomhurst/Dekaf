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
    public async Task MoreRelaysThanBuckets_TheIncumbentKeepsOneBucket_AndHandsBackTheRest()
    {
        var leases = Leases(2, ("c", 0, 1));
        List<string> relays = ["a", "b", "c"];

        var plan = Plan(2, relays, "c", leases);

        // "c" ranks last, but it is the one publishing: it keeps a bucket, and the other goes
        // to the first relay that holds nothing.
        await Assert.That(Join(plan.Keep)).IsEqualTo("1");
        await Assert.That(Join(plan.Release)).IsEqualTo("0");
        await Assert.That(plan.Claim).IsEmpty();

        leases[0] = default;
        await Assert.That(Join(Plan(2, relays, "a", leases).Claim)).IsEqualTo("0");
        await Assert.That(Plan(2, relays, "b", leases).Claim).IsEmpty();
    }

    [Test]
    public async Task JoinerIntoAFleetWithMoreRelaysThanBuckets_TakesNothing_WhateverItsId()
    {
        // Pod names decide the rank, and a new ReplicaSet can sort before the old one. The
        // joiner must not push an incumbent out of a split that was already fair.
        var leases = Leases(4, ("pod-m", 0, 0), ("pod-n", 1, 1), ("pod-o", 2, 2), ("pod-p", 3, 3));
        List<string> relays = ["pod-m", "pod-n", "pod-o", "pod-p", "pod-q", "pod-a"];

        foreach (var incumbent in new[] { "pod-m", "pod-n", "pod-o", "pod-p" })
        {
            var plan = Plan(4, relays, incumbent, leases);
            await Assert.That(plan.Keep.Count).IsEqualTo(1);
            await Assert.That(plan.Release.Count + plan.Claim.Count).IsEqualTo(0);
            await Assert.That(plan.StandbyRank).IsEqualTo(-1);
        }

        var joiner = Plan(4, relays, "pod-a", leases);
        await Assert.That(joiner.Keep.Count + joiner.Release.Count + joiner.Claim.Count).IsEqualTo(0);
        await Assert.That(joiner.StandbyRank).IsEqualTo(0);
        await Assert.That(Plan(4, relays, "pod-q", leases).StandbyRank).IsEqualTo(1);
    }

    [Test]
    public async Task FreedBucket_GoesToTheFirstStandby_AndToNobodyElse()
    {
        var leases = Leases(4, ("pod-m", 0, 0), ("pod-n", 1, 1), ("pod-p", 3, 3));
        List<string> relays = ["pod-m", "pod-n", "pod-p", "pod-q", "pod-a"];

        await Assert.That(Join(Plan(4, relays, "pod-a", leases).Claim)).IsEqualTo("2");
        foreach (var relay in new[] { "pod-m", "pod-n", "pod-p", "pod-q" })
            await Assert.That(Plan(4, relays, relay, leases).Claim).IsEmpty();
    }

    [Test]
    public async Task Joiner_TakesTheRemainderFromNobody_WhenAnIncumbentAlreadyHoldsIt()
    {
        // Eight buckets over five relays are 2,2,2,1,1. With a sixth they are 2,2,1,1,1,1:
        // one bucket has to move. Ranking by id alone would move two, because the joiner
        // sorts first and would be given one of the remaining pairs.
        var leases = Leases(8, ("b", 0, 1), ("c", 2, 3), ("d", 4, 5), ("e", 6, 6), ("f", 7, 7));
        List<string> relays = ["a", "b", "c", "d", "e", "f"];

        var released = 0;
        foreach (var relay in relays)
            released += Plan(8, relays, relay, leases).Release.Count;

        await Assert.That(released).IsEqualTo(1);
        await Assert.That(Plan(8, relays, "d", leases).Release.Count).IsEqualTo(1);
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
                // What the relay sees held: a lapsed lease is free unless it is its own.
                var held = leases
                    .Where(lease => lease.Owner is not null && (!lease.Expired || lease.Owner == relay))
                    .GroupBy(lease => lease.Owner!)
                    .ToDictionary(group => group.Key, group => group.Count());
                var share = OutboxFairShare.Compute(bucketCount, [.. relays], relay, held);

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

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task DisagreeingMembership_NeverSharesABucket_AndSettlesQuietlyOnceTheRelaysAgree(int seed)
    {
        const int bucketCount = 16;
        var random = new Random(seed);
        var relays = Enumerable.Range(0, 5).Select(index => $"relay-{index}").ToList();
        var table = new ConditionalLeaseTable(bucketCount);
        var believed = relays.ToDictionary(relay => relay, _ => new HashSet<int>());
        var earlierReads = new Dictionary<string, ConditionalLeaseTable.Snapshot>();

        // While a membership change is becoming visible, every relay plans from the
        // heartbeats it happens to see, and from a read that peers' writes may have overtaken.
        for (var step = 0; step < 80; step++)
        {
            var relay = relays[random.Next(relays.Count)];
            var seen = relays.Where(peer => peer == relay || random.Next(2) == 0).ToList();
            var read = earlierReads.Remove(relay, out var earlier) && random.Next(2) == 0 ? earlier : table.Read();
            believed[relay] = table.Apply(relay, Plan(bucketCount, seen, relay, read.Leases), read);

            // A peer reads now and writes some steps later.
            earlierReads[relays[random.Next(relays.Count)]] = table.Read();

            // The plan orders writes and never grants ownership: whatever the relays planned
            // from, the conditions leave each bucket with one relay that was told it owns it.
            var told = believed.SelectMany(pair => pair.Value.Select(bucket => (bucket, pair.Key))).ToList();
            await Assert.That(told.Select(entry => entry.bucket).Distinct().Count()).IsEqualTo(told.Count);
            await Assert.That(told.All(entry => table.Owner(entry.bucket) == entry.Key)).IsTrue();
        }

        // The membership has settled: everybody sees everybody and reads before writing.
        table.Refused = 0;
        var settledAfter = -1;
        for (var round = 1; round <= 4 && settledAfter < 0; round++)
        {
            var writes = 0;
            foreach (var relay in relays.OrderBy(_ => random.Next()).ToArray())
            {
                var read = table.Read();
                var plan = Plan(bucketCount, relays, relay, read.Leases);
                writes += plan.Release.Count + plan.Claim.Count;
                believed[relay] = table.Apply(relay, plan, read);
            }

            if (writes == 0)
                settledAfter = round;
        }

        await Assert.That(settledAfter).IsGreaterThan(0);
        await Assert.That(table.Refused).IsEqualTo(0);
        var counts = relays.Select(relay => believed[relay].Count).ToArray();
        await Assert.That(counts.Sum()).IsEqualTo(bucketCount);
        await Assert.That(counts.Max() - counts.Min()).IsLessThanOrEqualTo(1);
    }

    /// <summary>
    /// The lease items as DynamoDB keeps them, with the store's conditions: keep and release
    /// require the owner and the version that was read, claim requires a free lease.
    /// </summary>
    private sealed class ConditionalLeaseTable(int bucketCount)
    {
        private readonly string?[] _owners = new string?[bucketCount];
        private readonly int[] _versions = new int[bucketCount];

        public int Refused { get; set; }

        public string? Owner(int bucket) => _owners[bucket];

        public Snapshot Read() => new(
            [.. _owners.Select(owner => new DynamoDbLeaseState(owner, Expired: false))], [.. _versions]);

        /// <returns>The buckets the relay is told it owns: the writes that were accepted.</returns>
        public HashSet<int> Apply(string relay, DynamoDbLeasePlan plan, Snapshot read)
        {
            var owned = new HashSet<int>();
            foreach (var bucket in plan.Keep)
            {
                if (Write(bucket, _owners[bucket] == relay && _versions[bucket] == read.Versions[bucket], relay))
                    owned.Add(bucket);
            }

            foreach (var bucket in plan.Release)
                Write(bucket, _owners[bucket] == relay && _versions[bucket] == read.Versions[bucket], owner: null);

            foreach (var bucket in plan.Claim)
            {
                if (Write(bucket, _owners[bucket] is null, relay))
                    owned.Add(bucket);
            }

            return owned;
        }

        private bool Write(int bucket, bool condition, string? owner)
        {
            if (!condition)
            {
                Refused++;
                return false;
            }

            _owners[bucket] = owner;
            _versions[bucket]++;
            return true;
        }

        public sealed record Snapshot(DynamoDbLeaseState[] Leases, int[] Versions);
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
