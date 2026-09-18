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
    public async Task Assign_InvalidArguments_Throw()
    {
        await Assert.That(() => OutboxFairShare.Assign(0, ["a"], "a")).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => OutboxFairShare.Assign(1, null!, "a")).Throws<ArgumentNullException>();
        await Assert.That(() => OutboxFairShare.Assign(1, ["a"], "")).Throws<ArgumentException>();
    }
}
