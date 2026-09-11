using Dekaf.StressTests.Scenarios;

namespace Dekaf.StressTests.Tests;

public class FollowerRecoveryOracleTests
{
    [Test]
    public async Task MatchingLeaderResponseRequiresTheRecordedFaultOffset()
    {
        var oracle = new FollowerRecoveryOracle([100]);
        for (var i = 1; i < FollowerRecoveryOracle.FaultInterval; i++)
            await Assert.That(oracle.ObserveFollowerData(0, 10, oracle.Epoch)).IsFalse();
        await Assert.That(oracle.ObserveFollowerData(0, 10, oracle.Epoch)).IsTrue();
        oracle.ObserveLeaderResponse(0, 0, oracle.Epoch);
        await Assert.That(oracle.Snapshot().MatchingLeaderResponses).IsEqualTo(0);
        oracle.ObserveLeaderResponse(0, 10, oracle.Epoch);
        oracle.ObserveLeaderResponse(0, 10, oracle.Epoch);
        await Assert.That(oracle.Snapshot()).IsEqualTo(new FollowerRecoverySnapshot(1, 1, 0, UnmatchedLeaderResponses: 1));
    }

    [Test]
    public async Task ReplayRejectsDuplicatesAndGaps()
    {
        var oracle = new FollowerRecoveryOracle([3]);
        oracle.RecordConsumed(0, 0);
        await Assert.That(() => oracle.RecordConsumed(0, 0)).Throws<InvalidOperationException>();
        await Assert.That(() => oracle.RecordConsumed(0, 2)).Throws<InvalidOperationException>();
        oracle.RecordConsumed(0, 1);
        await Assert.That(oracle.RecordConsumed(0, 2)).IsTrue();
        await Assert.That(oracle.RecordConsumed(0, 0)).IsFalse();
        await Assert.That(oracle.Snapshot().CompletePasses).IsEqualTo(1);
    }

    [Test]
    public async Task ReplayWaitsForEveryPartition()
    {
        var oracle = new FollowerRecoveryOracle([1, 2]);
        await Assert.That(oracle.RecordConsumed(1, 0)).IsFalse();
        await Assert.That(oracle.RecordConsumed(0, 0)).IsFalse();
        await Assert.That(() => oracle.RecordConsumed(0, 0)).Throws<InvalidOperationException>();
        await Assert.That(oracle.RecordConsumed(1, 1)).IsTrue();
        await Assert.That(oracle.RecordConsumed(0, 0)).IsFalse();
    }

    [Test]
    public async Task FaultStateIsIndependentAcrossPartitions()
    {
        var oracle = new FollowerRecoveryOracle([100, 100]);
        for (var i = 0; i < FollowerRecoveryOracle.FaultInterval; i++)
        {
            oracle.ObserveFollowerData(0, 10, oracle.Epoch);
            oracle.ObserveFollowerData(1, 20, oracle.Epoch);
        }
        oracle.ObserveLeaderResponse(1, 20, oracle.Epoch);
        await Assert.That(oracle.Snapshot().MatchingLeaderResponses).IsEqualTo(1);
        oracle.ObserveLeaderResponse(0, 10, oracle.Epoch);
        await Assert.That(oracle.Snapshot().MatchingLeaderResponses).IsEqualTo(2);
    }

    [Test]
    public async Task ConcurrentProgressRequiresEveryInterveningRecord()
    {
        var oracle = new FollowerRecoveryOracle([100]);
        for (var i = 0; i < FollowerRecoveryOracle.FaultInterval; i++)
            oracle.ObserveFollowerData(0, 10, oracle.Epoch);
        oracle.ObserveLeaderResponse(0, 11, oracle.Epoch);
        await Assert.That(oracle.Snapshot().LeaderResponsesAfterVerifiedProgress).IsEqualTo(0);
        for (var i = 0; i <= 10; i++)
            oracle.RecordConsumed(0, i);
        oracle.ObserveLeaderResponse(0, 11, oracle.Epoch);
        await Assert.That(oracle.Snapshot()).IsEqualTo(new FollowerRecoverySnapshot(1, 0, 0, 1, UnmatchedLeaderResponses: 1));
    }

    [Test]
    public async Task SeekInvalidatesOldFaultsAndInFlightResponses()
    {
        var oracle = new FollowerRecoveryOracle([1]);
        var oldEpoch = oracle.Epoch;
        for (var i = 0; i < FollowerRecoveryOracle.FaultInterval; i++)
            oracle.ObserveFollowerData(0, 0, oldEpoch);
        oracle.RecordConsumed(0, 0);
        await Assert.That(oracle.ObserveFollowerData(0, 0, oldEpoch)).IsFalse();
        await Assert.That(oracle.ObserveFollowerData(0, 0, oracle.Epoch)).IsFalse();
        oracle.CompleteRewind();
        oracle.ObserveLeaderResponse(0, 1, oldEpoch);
        for (var i = 0; i < FollowerRecoveryOracle.FaultInterval; i++)
            oracle.ObserveFollowerData(0, 0, oracle.Epoch);
        oracle.ObserveLeaderResponse(0, 0, oracle.Epoch);
        await Assert.That(oracle.Snapshot()).IsEqualTo(new FollowerRecoverySnapshot(2, 1, 1));
    }

    [Test]
    public async Task PhaseBoundaryDoesNotCarryCanceledFaultExpectations()
    {
        var oracle = new FollowerRecoveryOracle([100]);
        for (var i = 0; i < FollowerRecoveryOracle.FaultInterval; i++)
            oracle.ObserveFollowerData(0, 10, oracle.Epoch);
        oracle.BeginPhase();
        oracle.ObserveLeaderResponse(0, 0, oracle.Epoch);
        await Assert.That(oracle.Snapshot().Violations).IsEqualTo(0);
        await Assert.That(oracle.Snapshot().MatchingLeaderResponses).IsEqualTo(0);
    }
}
