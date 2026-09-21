using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Bucket ownership of horizontally scaled relays on DynamoDB: joining, leaving gracefully,
/// crashing, stalling and rolling over. Each scenario asserts that no bucket ever has two
/// owners, that the relays converge on a fair split, and that DynamoDB refused no
/// conditional write unless the scenario is a real race. A refused write is billed and shows
/// up as an error in the AWS SDK's telemetry, so "quiet" is part of the contract.
/// </summary>
[Category("MessagingPatterns")]
[ClassDataSource<DynamoDbLocalContainer>(Shared = SharedType.PerTestSession)]
public sealed class OutboxDynamoDbLeaseTests(DynamoDbLocalContainer dynamoDb)
{
    [Test]
    public async Task SingleRelay_TakesEveryBucket_AndRenewsQuietly()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);

        for (var round = 0; round < 5; round++)
        {
            await fleet.RoundAsync("relay-a");
            await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
        }

        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task JoiningRelay_IsHandedItsAssignedRange_WithoutARefusedWrite()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.RoundAsync("relay-a");

        // Nothing is free yet, so the joiner writes no lease at all.
        await fleet.RoundAsync("relay-a", "relay-b");
        await Assert.That(fleet.Owned("relay-b")).IsEmpty();

        // The incumbent now sees the joiner, keeps its own range and hands back the rest.
        await fleet.RoundAsync("relay-a", "relay-b");
        await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0,1,2,3");
        await Assert.That(Join(fleet.Owned("relay-b"))).IsEqualTo("4,5,6,7");

        for (var round = 0; round < 3; round++)
            await fleet.RoundAsync("relay-b", "relay-a");
        await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0,1,2,3");
        await Assert.That(Join(fleet.Owned("relay-b"))).IsEqualTo("4,5,6,7");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task JoinerRankedBeforeTheIncumbent_GetsTheLowerRange()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.RoundAsync("relay-m");

        for (var round = 0; round < 3; round++)
            await fleet.RoundAsync("relay-m", "relay-b");

        // Ranges follow relay-id rank. The incumbent keeps the half it is now assigned, so
        // what it hands back is exactly what the joiner is assigned.
        await Assert.That(Join(fleet.Owned("relay-b"))).IsEqualTo("0,1,2,3");
        await Assert.That(Join(fleet.Owned("relay-m"))).IsEqualTo("4,5,6,7");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task ThirdRelay_Joins_WithoutMovingBucketsThePeersKeep()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        var beforeA = fleet.Owned("relay-a");
        var beforeB = fleet.Owned("relay-b");

        await ConvergeAsync(fleet, "relay-a", "relay-b", "relay-c");

        await AssertBalancedAsync(fleet, "relay-a", "relay-b", "relay-c");
        // A bucket a relay still holds is being published by it: it must not change hands.
        await Assert.That(fleet.Owned("relay-a").All(beforeA.Contains)).IsTrue();
        await Assert.That(fleet.Owned("relay-b").All(beforeB.Contains)).IsTrue();
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task CrashedRelay_KeepsItsBucketsUntilItsLeaseExpires_ThenSurvivorsSplitThem()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b", "relay-c");
        var crashed = fleet.Owned("relay-c");
        // The crashed relay's last round started one interval ago, so its lease has two left.
        fleet.Forget("relay-c");

        for (var round = 0; round < 2; round++)
        {
            await fleet.RoundAsync("relay-a", "relay-b");
            // Still leased and still counted: the survivors neither claim nor probe.
            await Assert.That(fleet.Owned("relay-a").Concat(fleet.Owned("relay-b")).Intersect(crashed)).IsEmpty();
        }

        await fleet.RoundAsync("relay-a", "relay-b");

        await AssertBalancedAsync(fleet, "relay-a", "relay-b");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task WholeFleetCrashes_ReplacementsTakeOverOnceTheLeasesExpire()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "old-a", "old-b");
        fleet.Forget("old-a");
        fleet.Forget("old-b");

        // Replacement pods start at once, while every lease still names a dead owner.
        await fleet.RoundAsync("new-a", "new-b");
        await Assert.That(fleet.Owned("new-a").Count + fleet.Owned("new-b").Count).IsEqualTo(0);

        fleet.Clock.Advance(OutboxDynamoDbFleet.LeaseDuration);
        await ConvergeAsync(fleet, "new-a", "new-b");

        await AssertBalancedAsync(fleet, "new-a", "new-b");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task RestartedRelayWithAStableId_ResumesItsBucketsAtOnce()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "pod-0", "pod-1");
        var before = Join(fleet.Owned("pod-1"));

        // A StatefulSet pod crashes and comes back under the same relay id: a new process
        // with a new store. Its leases still name it, so it need not wait for them to expire.
        fleet.Forget("pod-1");
        await fleet.RoundAsync("pod-1", "pod-0");

        await Assert.That(Join(fleet.Owned("pod-1"))).IsEqualTo(before);
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task GracefulRelease_HandsBucketsOverOnThePeersNextRound_NotAfterExpiry()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b");

        await fleet.ReleaseAsync("relay-b");
        await fleet.RoundAsync("relay-a");

        // One round, not LeaseDuration: the leases were freed and the record is stamped as
        // stopped, so the survivor's share is the whole table again.
        await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task Release_IsNotUndoneByTheHeartbeatOfTheRoundTheStopCancelled()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        var cancelledRoundStartedAt = fleet.Clock.GetUtcNow();

        fleet.Clock.Advance(TimeSpan.FromSeconds(1));
        await fleet.ReleaseAsync("relay-b");
        // The stop cancelled a round mid-flight. Its heartbeat still reaches DynamoDB, and
        // without the stopped record to refuse it, peers would count a relay that is gone
        // towards their fair share for a whole lease duration.
        await Assert.That(await fleet.LandStragglingHeartbeatAsync("relay-b", cancelledRoundStartedAt)).IsFalse();

        await fleet.RoundAsync("relay-a");
        await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
    }

    [Test]
    public async Task Release_IsNotUndoneByAClaimOfTheRoundTheStopCancelled()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        var cancelledRoundStartedAt = fleet.Clock.GetUtcNow();
        var bucket = fleet.Owned("relay-b")[0];
        fleet.Clock.Advance(TimeSpan.FromSeconds(1));

        // The stop cancelled a round mid-claim, and the claim reaches DynamoDB after the
        // release freed the lease it names: between the pass that hands the leases back and
        // the read that confirms it. No condition can refuse a claim of a free lease.
        var reads = 0;
        bool? landed = null;
        fleet.BeforeStoreCall = async (method, _) =>
        {
            if (method == nameof(Amazon.DynamoDBv2.IAmazonDynamoDB.QueryAsync) && ++reads == 2)
                landed = await fleet.LandStragglingClaimAsync("relay-b", bucket, cancelledRoundStartedAt);
        };
        await fleet.ReleaseAsync("relay-b");
        fleet.BeforeStoreCall = null;

        // Left alone, the lease would name a relay that is gone for a whole lease duration.
        await Assert.That(landed).IsTrue();
        await Assert.That(await fleet.ReadNamedAsync("relay-b")).IsEmpty();
        await fleet.RoundAsync("relay-a");
        await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
    }

    [Test]
    public async Task Release_IsNotUndoneByARenewalOfTheRoundTheStopCancelled()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        var cancelledRoundStartedAt = fleet.Clock.GetUtcNow();
        var bucket = fleet.Owned("relay-b")[0];
        var seenExpiry = (await fleet.ReadLeaseExpiriesAsync())[bucket];

        fleet.Clock.Advance(TimeSpan.FromSeconds(1));
        await fleet.ReleaseAsync("relay-b");

        // The renewal names the owner and the expiry its round read. The release changed both.
        await Assert.That(await fleet.LandStragglingKeepAsync("relay-b", bucket, seenExpiry, cancelledRoundStartedAt))
            .IsFalse();
        await Assert.That(await fleet.ReadNamedAsync("relay-b")).IsEmpty();
        await fleet.RoundAsync("relay-a");
        await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
    }

    [Test]
    public async Task Fleet_SkewedClocks_WithinSlack_NeverShareABucket()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        // Sixteen seconds apart at the most. The documented tolerance is the renewal slack,
        // LeaseDuration - LeaseRenewInterval, which is twenty.
        fleet.SetClockOffset("relay-a", TimeSpan.FromSeconds(8));
        fleet.SetClockOffset("relay-b", TimeSpan.FromSeconds(-8));

        // AcquireAsync asserts on the true time, after every acquisition, that no bucket has
        // two relays that were told they own it.
        await ConvergeAsync(fleet, "relay-a", "relay-b", "relay-c");
        await AssertBalancedAsync(fleet, "relay-a", "relay-b", "relay-c");
        var settled = fleet.Describe("relay-a", "relay-b", "relay-c");
        for (var round = 0; round < 6; round++)
            await fleet.RoundAsync(round % 2 == 0 ? ["relay-c", "relay-b", "relay-a"] : ["relay-a", "relay-c", "relay-b"]);
        // A fast clock must not take a live peer's lease for an expired one.
        await Assert.That(fleet.Describe("relay-a", "relay-b", "relay-c")).IsEqualTo(settled);

        // The slow-clocked relay crashes. Its peers see its leases lapse at different
        // moments, the fast-clocked one first, and still end up with a fair split.
        fleet.Forget("relay-b");
        await ConvergeAsync(fleet, "relay-a", "relay-c");
        await AssertBalancedAsync(fleet, "relay-a", "relay-c");

        // The fast-clocked relay stops gracefully: the expiry it writes lies in its peers'
        // future, and the lease is free all the same, because it names nobody.
        await fleet.ReleaseAsync("relay-a");
        await fleet.RoundAsync("relay-c");
        await Assert.That(Join(fleet.Owned("relay-c"))).IsEqualTo("0,1,2,3,4,5,6,7");

        // Both come back on their skewed hosts.
        await ConvergeAsync(fleet, "relay-c", "relay-a", "relay-b");
        await AssertBalancedAsync(fleet, "relay-a", "relay-b", "relay-c");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task ClockStepsBackwards_KeepDoesNotShortenLease()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.RoundAsync("relay-a");
        var written = await fleet.ReadLeaseExpiriesAsync();

        // The host's clock is set back by more than a renew interval, so the expiry this round
        // computes lies before the one the last round wrote.
        fleet.SetClockOffset("relay-a", TimeSpan.FromSeconds(-25));
        var owned = await fleet.AcquireAsync("relay-a");

        // Peers may have planned around the later expiry, so it stays. The relay is told it
        // owns nothing: it trusts what it is told for a whole lease duration from now, which
        // the expiry left in place does not promise on a peer's clock.
        await Assert.That(owned).IsEmpty();
        await Assert.That(await fleet.ReadLeaseExpiriesAsync()).IsEquivalentTo(written);
        await Assert.That(Join(await fleet.ReadNamedAsync("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
        // One refusal, the heartbeat's: its timestamp only moves forward too. The renewals
        // were not sent, because their condition was known to refuse them.
        await Assert.That(fleet.RefusedWrites).IsEqualTo(1);

        // Nobody took the leases meanwhile, and the relay has them back once its clock is right.
        fleet.SetClockOffset("relay-a", TimeSpan.Zero);
        await Assert.That(Join(await fleet.AcquireAsync("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
    }

    [Test]
    public async Task StoppedRecord_IsPrunedOnceItCouldNoLongerCountAsActive_NotTenLeasesLater()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        await fleet.ReleaseAsync("relay-b");

        // The record is only there to refuse the requests of the round the stop cancelled.
        // Keeping it for a crashed relay's ten lease durations would leave one item per pod
        // of every rolling update in the partition that every relay reads each round.
        fleet.Clock.Advance(OutboxDynamoDbFleet.LeaseDuration * 2);
        for (var round = 0; round < 10; round++)
            await fleet.RoundAsync("relay-a");

        await Assert.That(await CountHeartbeatsAsync(fleet)).IsEqualTo(1);
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task RelayThatStartsAgainAfterAGracefulStop_IsNotHeldBackByItsOwnStoppedRecord()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        await fleet.ReleaseAsync("relay-b");
        fleet.Clock.Advance(OutboxDynamoDbFleet.RenewInterval);

        // A new pod with the same relay id: its heartbeat is later than the record its
        // predecessor left, so it counts again and takes its range back.
        await ConvergeAsync(fleet, "relay-a", "relay-b");

        await AssertBalancedAsync(fleet, "relay-a", "relay-b");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task Release_FreesEveryLeaseOfTheRelay_WhateverTheHintSays()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.RoundAsync("relay-a");

        // The relay's hint is empty after an acquisition that failed after claiming.
        await fleet.Store("relay-a").ReleaseBucketLeasesAsync(fleet.Request("relay-a"), []);
        fleet.Forget("relay-a");
        await fleet.RoundAsync("relay-b");

        await Assert.That(Join(fleet.Owned("relay-b"))).IsEqualTo("0,1,2,3,4,5,6,7");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task Release_LeavesALeaseAloneThatAPeerTookOver()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.RoundAsync("relay-a");
        var stalled = fleet.Store("relay-a");
        fleet.Forget("relay-a");
        fleet.Clock.Advance(OutboxDynamoDbFleet.LeaseDuration);
        await fleet.RoundAsync("relay-b");

        // The stalled relay finally stops. Nothing names it any more, so it frees nothing.
        await stalled.ReleaseBucketLeasesAsync(fleet.Request("relay-a"), [0, 1, 2, 3, 4, 5, 6, 7]);

        await Assert.That(await fleet.RenewAsync("relay-b")).IsTrue();
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task RollingUpdate_ReplacesEveryRelay_WithoutARefusedWrite()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        string[] relays = ["v1-a", "v1-b", "v1-c"];
        await ConvergeAsync(fleet, relays);

        for (var index = 0; index < relays.Length; index++)
        {
            // Kubernetes starts the replacement, then stops the old pod gracefully.
            var replacement = $"v2-{(char)('a' + index)}";
            var surge = relays.Append(replacement).ToArray();
            await fleet.RoundAsync(surge);
            await fleet.ReleaseAsync(relays[index]);
            relays[index] = replacement;
            await ConvergeAsync(fleet, relays);
            await AssertBalancedAsync(fleet, relays);
        }

        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task ScaleDown_SurvivorEndsUpWithEveryBucket()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b", "relay-c");

        await fleet.ReleaseAsync("relay-a");
        await fleet.ReleaseAsync("relay-c");
        await fleet.RoundAsync("relay-b");

        await Assert.That(Join(fleet.Owned("relay-b"))).IsEqualTo("0,1,2,3,4,5,6,7");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task MoreRelaysThanBuckets_TheSurplusRelayWaitsQuietly_AndStepsInOnARelease()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb, bucketCount: 2);
        await ConvergeAsync(fleet, "relay-a", "relay-b", "relay-c");
        await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0");
        await Assert.That(Join(fleet.Owned("relay-b"))).IsEqualTo("1");
        await Assert.That(fleet.Owned("relay-c")).IsEmpty();

        await fleet.ReleaseAsync("relay-a");
        await ConvergeAsync(fleet, "relay-b", "relay-c");

        await AssertBalancedAsync(fleet, "relay-b", "relay-c");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task StalledRelay_LosesItsBucketsAtExpiry_AndNeverStealsThemBack()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        var stalledBuckets = fleet.Owned("relay-b");

        // relay-b freezes (a long pause, a suspended VM) while relay-a keeps its cadence.
        for (var round = 0; round < 3; round++)
            await fleet.RoundAsync("relay-a");
        await Assert.That(Join(fleet.Owned("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);

        // It wakes up mid-publish and tries to extend leases that a peer now holds. These
        // refusals are the one legitimate kind: the relay really did lose the buckets.
        var renewed = await fleet.Store("relay-b").RenewBucketLeasesAsync(fleet.Request("relay-b"), stalledBuckets);
        await Assert.That(renewed).IsFalse();
        await Assert.That(fleet.RefusedWrites).IsEqualTo(stalledBuckets.Count);

        // Re-acquisition reads before it writes: it takes nothing that relay-a holds, and
        // gets its share back only when relay-a hands it over.
        await fleet.RoundAsync("relay-b", "relay-a");
        await Assert.That(fleet.Owned("relay-b")).IsEmpty();
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        await AssertBalancedAsync(fleet, "relay-a", "relay-b");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(stalledBuckets.Count);
    }

    [Test]
    public async Task Renewal_NeverRevivesAnExpiredLease_ButAcquisitionRetakesAnUnclaimedOne()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.RoundAsync("relay-a");
        fleet.Clock.Advance(OutboxDynamoDbFleet.LeaseDuration);

        // Nobody took the buckets, but a publish cannot prove that it held them throughout.
        await Assert.That(await fleet.RenewAsync("relay-a")).IsFalse();

        // A fresh acquisition can: the owner condition shows that no peer wrote the lease.
        await Assert.That(Join(await fleet.AcquireAsync("relay-a"))).IsEqualTo("0,1,2,3,4,5,6,7");
        await Assert.That(await fleet.RenewAsync("relay-a")).IsTrue();
    }

    [Test]
    public async Task Renewal_ExtendsLeasesAndHeartbeat_SoAJoinerNeitherClaimsNorMiscounts()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.AcquireAsync("relay-a");

        // A long publish: only in-flight renewal runs, no acquisition.
        fleet.Clock.Advance(TimeSpan.FromSeconds(20));
        await Assert.That(await fleet.RenewAsync("relay-a")).IsTrue();
        fleet.Clock.Advance(TimeSpan.FromSeconds(15));

        // 35 s after the acquisition its leases would have lapsed and its heartbeat aged out.
        await Assert.That(await fleet.AcquireAsync("relay-b")).IsEmpty();
        await Assert.That(await fleet.RenewAsync("relay-a")).IsTrue();
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task Renewal_OfABucketOutsideTheRange_IsRefusedWithoutARequest()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.AcquireAsync("relay-a");

        var renewed = await fleet.Store("relay-a").RenewBucketLeasesAsync(fleet.Request("relay-a"), [0, 8]);

        await Assert.That(renewed).IsFalse();
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task SteadyState_ConcurrentRounds_RefuseNothingAndMoveNothing()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb, bucketCount: 16);
        string[] relays = ["relay-a", "relay-b", "relay-c"];
        await ConvergeAsync(fleet, relays);
        var before = fleet.Describe(relays);

        for (var round = 0; round < 5; round++)
            await fleet.ConcurrentRoundAsync(relays);

        await Assert.That(fleet.Describe(relays)).IsEqualTo(before);
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task ConcurrentClaimsAfterARelease_AreDisjoint_SoNothingIsRefused()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb, bucketCount: 16);
        await ConvergeAsync(fleet, "relay-a", "relay-b", "relay-c", "relay-d");
        await fleet.ReleaseAsync("relay-b");

        // Three survivors see the same free buckets at once. Probing them in any per-relay
        // order would collide; the shared plan gives each survivor different ones.
        await fleet.ConcurrentRoundAsync("relay-a", "relay-c", "relay-d");

        await AssertBalancedAsync(fleet, "relay-a", "relay-c", "relay-d");
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    [Test]
    public async Task ColdStart_OfManyRelaysAtOnce_NeverSharesABucket_AndConverges()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb, bucketCount: 16);
        string[] relays = ["relay-a", "relay-b", "relay-c", "relay-d"];

        // The one real race: heartbeats land while peers are already planning, so plans can
        // overlap and the conditional write has to pick the winner. AcquireAsync asserts
        // after every acquisition that no bucket has two owners.
        await fleet.ConcurrentRoundAsync(relays);
        var racing = fleet.RefusedWrites;
        await Assert.That(racing).IsLessThanOrEqualTo(fleet.Options.BucketCount * (relays.Length - 1));

        await ConvergeAsync(fleet, relays);
        await AssertBalancedAsync(fleet, relays);

        // Once membership is agreed the noise stops for good.
        var settled = fleet.RefusedWrites;
        for (var round = 0; round < 3; round++)
            await fleet.ConcurrentRoundAsync(relays);
        await Assert.That(fleet.RefusedWrites).IsEqualTo(settled);
    }

    [Test]
    public async Task ShrunkBucketCount_IgnoresLeaseItemsBeyondTheRange()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.AcquireAsync("relay-a");
        var shrunkOptions = new DynamoDbOutboxOptions { TableName = fleet.Options.TableName, BucketCount = 4 };
        var shrunk = new DynamoDbOutboxStore(fleet.Client, shrunkOptions, fleet.Clock);
        var request = new OutboxLeaseRequest
        {
            RelayId = "relay-a",
            BucketCount = 4,
            LeaseDuration = OutboxDynamoDbFleet.LeaseDuration
        };

        var owned = await shrunk.AcquireBucketLeasesAsync(request);

        // Buckets 4 to 7 no longer exist: they must not fill the relay's share.
        await Assert.That(Join(owned)).IsEqualTo("0,1,2,3");
    }

    [Test]
    public async Task RelayAndStoreBucketCountsThatDiffer_FaultTheRelay()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        var request = new OutboxLeaseRequest
        {
            RelayId = "relay-a",
            BucketCount = 16,
            LeaseDuration = OutboxDynamoDbFleet.LeaseDuration
        };

        await Assert.That(async () => await fleet.Store("relay-a").AcquireBucketLeasesAsync(request))
            .Throws<OutboxMisconfigurationException>();
    }

    [Test]
    public async Task DeadHeartbeats_ArePruned_ByEveryRelayWithoutARefusedWrite()
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb);
        await fleet.RoundAsync("relay-dead");
        fleet.Forget("relay-dead");
        fleet.Clock.Advance(OutboxDynamoDbFleet.LeaseDuration * 11);

        // Both survivors reach their pruning round together, with the same dead record in
        // view. Whoever deletes second must not be refused for finding it gone.
        await ConvergeAsync(fleet, "relay-a", "relay-b");
        var settled = fleet.RefusedWrites;
        for (var round = 0; round < 10; round++)
            await fleet.ConcurrentRoundAsync("relay-a", "relay-b");

        await Assert.That(await CountHeartbeatsAsync(fleet)).IsEqualTo(2);
        await Assert.That(fleet.RefusedWrites).IsEqualTo(settled);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task LargeFleet_ThroughRandomChurn_NeverSharesABucket_AlwaysResettles_AndRefusesNothing(int seed)
    {
        using var fleet = await OutboxDynamoDbFleet.CreateAsync(dynamoDb, bucketCount: 64);
        var random = new Random(seed);
        var live = new List<string>();
        var nextPod = 0;
        string NewPod() => $"pod-{random.Next(100):D2}-{nextPod++}";

        for (var index = 0; index < 5; index++)
            live.Add(NewPod());
        await ConvergeAsync(fleet, [.. live]);

        for (var step = 0; step < 24; step++)
        {
            var victim = live[random.Next(live.Count)];
            switch (random.Next(6))
            {
                case 0 or 1:
                    // Scale out, or the surge pod of a rolling update.
                    live.Add(NewPod());
                    break;
                case 2 when live.Count > 2:
                    await fleet.ReleaseAsync(victim);
                    live.Remove(victim);
                    break;
                case 3 when live.Count > 2:
                    // Killed: leases and heartbeat stay behind until they lapse, three
                    // rounds after the victim's last one.
                    fleet.Forget(victim);
                    live.Remove(victim);
                    for (var round = 0; round < 3; round++)
                        await fleet.RoundAsync([.. live]);
                    break;
                case 4 when live.Count > 2:
                    // Frozen for longer than its lease while its peers keep their cadence.
                    // It wakes up in the rounds below, still believing in its old buckets.
                    var awake = live.Where(pod => pod != victim).ToArray();
                    for (var round = 0; round < 4; round++)
                        await fleet.RoundAsync(awake);
                    break;
                default:
                    // Every pod's round lands at the same moment.
                    await fleet.ConcurrentRoundAsync([.. live]);
                    break;
            }

            // AcquireAsync asserts after every single acquisition that no bucket has two
            // owners and that the table names the relay for everything it was handed.
            await ConvergeAsync(fleet, [.. live.OrderBy(_ => random.Next())]);
            await AssertBalancedAsync(fleet, [.. live]);
        }

        // Membership only ever changed between rounds, never during one, so not a single
        // conditional write was refused in two dozen changes to a 64-bucket fleet.
        await Assert.That(fleet.RefusedWrites).IsEqualTo(0);
    }

    /// <summary>Runs rounds until a full round changes nothing.</summary>
    private static async Task ConvergeAsync(OutboxDynamoDbFleet fleet, params string[] relayIds)
    {
        for (var round = 0; round < 12; round++)
        {
            var before = fleet.Describe(relayIds);
            await fleet.RoundAsync(relayIds);
            if (round > 0 && fleet.Describe(relayIds) == before)
                return;
        }

        Assert.Fail($"Ownership did not settle: {fleet.Describe(relayIds)}");
    }

    /// <summary>Every bucket has exactly one owner and no relay is more than one bucket ahead.</summary>
    private static async Task AssertBalancedAsync(OutboxDynamoDbFleet fleet, params string[] relayIds)
    {
        var all = relayIds.SelectMany(fleet.Owned).Order().ToArray();
        await Assert.That(Join(all)).IsEqualTo(Join(Enumerable.Range(0, fleet.Options.BucketCount)));

        var counts = relayIds.Select(relayId => fleet.Owned(relayId).Count).ToArray();
        await Assert.That(counts.Max() - counts.Min()).IsLessThanOrEqualTo(1);
    }

    private static async Task<int> CountHeartbeatsAsync(OutboxDynamoDbFleet fleet)
    {
        var response = await fleet.Client.QueryAsync(new Amazon.DynamoDBv2.Model.QueryRequest
        {
            TableName = fleet.Options.TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :relay)",
            ExpressionAttributeValues = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
            {
                [":pk"] = new() { S = "OUTBOX#COORDINATION" },
                [":relay"] = new() { S = "RELAY#" }
            },
            ConsistentRead = true
        });
        return response.Items?.Count ?? 0;
    }

    private static string Join(IEnumerable<int> buckets) => string.Join(',', buckets);
}
