using Dekaf.Outbox;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxFairShareTests
{
    [Test]
    [Arguments(1, 1)]
    [Arguments(8, 1)]
    [Arguments(8, 2)]
    [Arguments(8, 3)]
    [Arguments(7, 4)]
    [Arguments(3, 5)]
    public async Task Assign_AgreeingRelays_PartitionEveryBucketIntoShareSizedRanges(int bucketCount, int relayCount)
    {
        var relayIds = Enumerable.Range(0, relayCount).Select(index => $"relay-{index:D2}").ToArray();
        var assignedByAnyRelay = new List<int>();

        // Reverse order: a relay's range must depend on its rank, never on who asked first.
        for (var index = relayCount - 1; index >= 0; index--)
        {
            var relayId = relayIds[index];
            var assigned = OutboxFairShare.Assign(bucketCount, [.. relayIds], relayId);
            await Assert.That(assigned.Count).IsEqualTo(OutboxFairShare.Compute(bucketCount, [.. relayIds], relayId));
            assignedByAnyRelay.AddRange(assigned);
        }

        assignedByAnyRelay.Sort();
        await Assert.That(string.Join(',', assignedByAnyRelay))
            .IsEqualTo(string.Join(',', Enumerable.Range(0, bucketCount)));
    }

    [Test]
    public async Task Assign_AccumulatesSharesInOrdinalRankOrder()
    {
        // Eight buckets over three relays: the two lowest ranks carry the remainder.
        await Assert.That(string.Join(',', OutboxFairShare.Assign(8, ["c", "a", "b"], "a"))).IsEqualTo("0,1,2");
        await Assert.That(string.Join(',', OutboxFairShare.Assign(8, ["c", "a", "b"], "b"))).IsEqualTo("3,4,5");
        await Assert.That(string.Join(',', OutboxFairShare.Assign(8, ["c", "a", "b"], "c"))).IsEqualTo("6,7");
    }

    [Test]
    public async Task Assign_RelayMissingFromActiveList_IsRankedWithIt()
    {
        List<string> activeRelayIds = ["a", "c"];

        var assigned = OutboxFairShare.Assign(4, activeRelayIds, "b");

        await Assert.That(string.Join(',', assigned)).IsEqualTo("2");
        await Assert.That(string.Join(',', activeRelayIds)).IsEqualTo("a,b,c");
    }

    [Test]
    public async Task Assign_MoreRelaysThanBuckets_LeavesHighRanksEmpty()
    {
        await Assert.That(OutboxFairShare.Assign(2, ["a", "b", "c"], "c")).IsEmpty();
    }

    [Test]
    public async Task HeldCounts_MoreRelaysThanBuckets_ShareGoesToTheHolders_NotToTheLowestIds()
    {
        var held = new Dictionary<string, int> { ["x"] = 1, ["y"] = 1 };

        await Assert.That(OutboxFairShare.Compute(2, ["a", "b", "x", "y"], "a", held)).IsEqualTo(0);
        await Assert.That(OutboxFairShare.Compute(2, ["a", "b", "x", "y"], "x", held)).IsEqualTo(1);
        await Assert.That(OutboxFairShare.Compute(2, ["a", "b", "x", "y"], "y", held)).IsEqualTo(1);
        // Ranges still follow id order, over the relays that have a share.
        await Assert.That(string.Join(',', OutboxFairShare.Assign(2, ["a", "b", "x", "y"], "x", held))).IsEqualTo("0");
        await Assert.That(string.Join(',', OutboxFairShare.Assign(2, ["a", "b", "x", "y"], "y", held))).IsEqualTo("1");
        await Assert.That(OutboxFairShare.Assign(2, ["a", "b", "x", "y"], "a", held)).IsEmpty();
    }

    [Test]
    public async Task HeldCounts_FreeBucket_GoesToTheFirstRelayThatHoldsNothing()
    {
        var held = new Dictionary<string, int> { ["y"] = 1 };

        await Assert.That(OutboxFairShare.Compute(2, ["a", "b", "y"], "a", held)).IsEqualTo(1);
        await Assert.That(OutboxFairShare.Compute(2, ["a", "b", "y"], "b", held)).IsEqualTo(0);
        await Assert.That(OutboxFairShare.Compute(2, ["a", "b", "y"], "y", held)).IsEqualTo(1);
    }

    [Test]
    public async Task HeldCounts_RemainderStaysWithTheRelaysThatHoldIt()
    {
        // Floor two, remainder two: "c" and "d" already hold three, so "a" and "b" get two.
        var held = new Dictionary<string, int> { ["a"] = 2, ["b"] = 0, ["c"] = 3, ["d"] = 5 };
        List<string> relays = ["a", "b", "c", "d"];

        await Assert.That(OutboxFairShare.Compute(10, [.. relays], "a", held)).IsEqualTo(2);
        await Assert.That(OutboxFairShare.Compute(10, [.. relays], "b", held)).IsEqualTo(2);
        await Assert.That(OutboxFairShare.Compute(10, [.. relays], "c", held)).IsEqualTo(3);
        await Assert.That(OutboxFairShare.Compute(10, [.. relays], "d", held)).IsEqualTo(3);
    }

    [Test]
    public async Task HeldCounts_NoneHeld_IsTheMembershipOnlySplit()
    {
        var none = new Dictionary<string, int>();
        foreach (var relay in new[] { "a", "b", "c" })
        {
            await Assert.That(OutboxFairShare.Compute(8, ["c", "a", "b"], relay, none))
                .IsEqualTo(OutboxFairShare.Compute(8, ["c", "a", "b"], relay));
            await Assert.That(string.Join(',', OutboxFairShare.Assign(8, ["c", "a", "b"], relay, none)))
                .IsEqualTo(string.Join(',', OutboxFairShare.Assign(8, ["c", "a", "b"], relay)));
        }
    }

    [Test]
    public async Task StandbyRank_CountsTheRelaysWithoutAShare_InIdOrder()
    {
        var held = new Dictionary<string, int> { ["m"] = 1, ["n"] = 1 };
        List<string> relays = ["a", "m", "b", "n", "c"];

        await Assert.That(OutboxFairShare.StandbyRank(2, [.. relays], "m", held)).IsEqualTo(-1);
        await Assert.That(OutboxFairShare.StandbyRank(2, [.. relays], "a", held)).IsEqualTo(0);
        await Assert.That(OutboxFairShare.StandbyRank(2, [.. relays], "b", held)).IsEqualTo(1);
        await Assert.That(OutboxFairShare.StandbyRank(2, [.. relays], "c", held)).IsEqualTo(2);
        // Enough buckets for everybody: nobody waits.
        await Assert.That(OutboxFairShare.StandbyRank(8, [.. relays], "c", held)).IsEqualTo(-1);
    }

    [Test]
    public async Task HeldCounts_SharesStayPut_WhilePeersMoveTowardsThem()
    {
        // Relays plan from reads taken at different moments of one rebalance. If a peer's
        // release or claim changed anybody's share, their plans would stop being disjoint.
        var random = new Random(20260921);
        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var bucketCount = random.Next(1, 40);
            var relays = Enumerable.Range(0, random.Next(1, 12)).Select(index => $"relay-{index:D2}").ToList();
            var held = relays.ToDictionary(relay => relay, _ => 0);
            var free = bucketCount;
            while (free > 0 && random.Next(8) != 0)
            {
                var taken = random.Next(1, free + 1);
                held[relays[random.Next(relays.Count)]] += taken;
                free -= taken;
            }

            var shares = relays.ToDictionary(
                relay => relay, relay => OutboxFairShare.Compute(bucketCount, [.. relays], relay, held));
            await Assert.That(shares.Values.Sum()).IsEqualTo(bucketCount);
            await Assert.That(shares.Values.Max() - shares.Values.Min()).IsLessThanOrEqualTo(1);

            while (true)
            {
                var movable = relays
                    .Where(relay => held[relay] > shares[relay] || (held[relay] < shares[relay] && free > 0)).ToList();
                if (movable.Count == 0)
                    break;

                var mover = movable[random.Next(movable.Count)];
                var step = held[mover] > shares[mover] ? -1 : 1;
                held[mover] += step;
                free -= step;

                foreach (var relay in relays)
                {
                    await Assert.That(OutboxFairShare.Compute(bucketCount, [.. relays], relay, held))
                        .IsEqualTo(shares[relay]);
                }
            }
        }
    }

    [Test]
    public async Task Assign_InvalidArguments_Throw()
    {
        await Assert.That(() => OutboxFairShare.Assign(0, ["a"], "a")).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => OutboxFairShare.Assign(1, null!, "a")).Throws<ArgumentNullException>();
        await Assert.That(() => OutboxFairShare.Assign(1, ["a"], "")).Throws<ArgumentException>();
        await Assert.That(() => OutboxFairShare.Assign(1, ["a"], "a", null!)).Throws<ArgumentNullException>();
        await Assert.That(() => OutboxFairShare.Compute(1, ["a"], "a", null!)).Throws<ArgumentNullException>();
        await Assert.That(() => OutboxFairShare.StandbyRank(1, ["a"], "a", null!)).Throws<ArgumentNullException>();
    }
}
