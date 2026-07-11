using Dekaf.StressTests.Scenarios;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class TransactionalSequenceOracleTests
{
    [Test]
    public async Task Observe_Failures_ReportsDuplicatesShortfallAndAbortedLeaks()
    {
        var oracle = new TransactionalSequenceOracle(
            runId: "run-1",
            committedMessages: 3,
            abortedMessages: 2,
            partitionCount: 2);

        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-1", 0));
        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-1", 0));
        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-1", 2));
        oracle.Observe(TransactionalSequenceOracle.AbortedKey("run-1", 0));
        oracle.Observe(TransactionalSequenceOracle.SentinelKey("run-1", 0));
        oracle.Observe(TransactionalSequenceOracle.SentinelKey("run-1", 1));
        oracle.Observe("another-run:c:1");

        var snapshot = oracle.CreateSnapshot(acceptedMessages: 5);

        await Assert.That(snapshot.AcceptedMessages).IsEqualTo(5);
        await Assert.That(snapshot.CommittedMessages).IsEqualTo(3);
        await Assert.That(snapshot.AbortedMessages).IsEqualTo(2);
        await Assert.That(snapshot.DeliveredMessages).IsEqualTo(2);
        await Assert.That(snapshot.DuplicateMessages).IsEqualTo(1);
        await Assert.That(snapshot.ShortfallMessages).IsEqualTo(1);
        await Assert.That(snapshot.LeakedAbortedMessages).IsEqualTo(1);
        await Assert.That(snapshot.MissingSentinelPartitions).IsEqualTo(0);
        await Assert.That(snapshot.IsSuccessful).IsFalse();
        await Assert.That(snapshot.FailureSamples).Contains(sample => sample.Contains("committed index 1"));
    }

    [Test]
    public async Task Observe_CleanRun_ReportsSuccessfulVerification()
    {
        var oracle = new TransactionalSequenceOracle(
            runId: "run-2",
            committedMessages: 2,
            abortedMessages: 1,
            partitionCount: 2);

        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-2", 0));
        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-2", 1));
        oracle.Observe(TransactionalSequenceOracle.SentinelKey("run-2", 0));
        oracle.Observe(TransactionalSequenceOracle.SentinelKey("run-2", 1));

        var snapshot = oracle.CreateSnapshot(acceptedMessages: 3);

        await Assert.That(oracle.AllSentinelsSeen).IsTrue();
        await Assert.That(snapshot.IsSuccessful).IsTrue();
        await Assert.That(snapshot.DeliveredMessages).IsEqualTo(2);
        await Assert.That(snapshot.FailureSamples).IsEmpty();
    }

    [Test]
    public async Task Observe_FailedCommitIntentLeak_IsReportedAsAbortedLeak()
    {
        var oracle = new TransactionalSequenceOracle(
            runId: "run-failed-commit",
            committedMessages: 2,
            abortedMessages: 1,
            partitionCount: 1,
            failedCommitMessages: 2);

        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-failed-commit", 0));
        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-failed-commit", 1));
        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-failed-commit", 2));
        oracle.Observe(TransactionalSequenceOracle.CommittedKey("run-failed-commit", 3));
        oracle.Observe(TransactionalSequenceOracle.SentinelKey("run-failed-commit", 0));

        var snapshot = oracle.CreateSnapshot(acceptedMessages: 5);

        await Assert.That(snapshot.AbortedMessages).IsEqualTo(3);
        await Assert.That(snapshot.DeliveredMessages).IsEqualTo(2);
        await Assert.That(snapshot.LeakedAbortedMessages).IsEqualTo(2);
        await Assert.That(snapshot.UnexpectedMessages).IsEqualTo(0);
        await Assert.That(snapshot.FailureSamples)
            .Contains(sample => sample.Contains("Failed commit index 2"));
    }

    [Test]
    public async Task CreateSnapshot_MissingSentinel_IsActionableFailure()
    {
        var oracle = new TransactionalSequenceOracle(
            runId: "run-3",
            committedMessages: 0,
            abortedMessages: 0,
            partitionCount: 2);

        oracle.Observe(TransactionalSequenceOracle.SentinelKey("run-3", 0));

        var snapshot = oracle.CreateSnapshot(acceptedMessages: 0);

        await Assert.That(snapshot.MissingSentinelPartitions).IsEqualTo(1);
        await Assert.That(snapshot.IsSuccessful).IsFalse();
        await Assert.That(snapshot.FailureSamples).Contains(sample => sample.Contains("partition 1"));
    }

    [Test]
    public async Task CreateSnapshot_SentinelCommitFailed_IsDistinctFailure()
    {
        var oracle = new TransactionalSequenceOracle(
            runId: "run-sentinel-failure",
            committedMessages: 0,
            abortedMessages: 0,
            partitionCount: 2);

        var snapshot = oracle.CreateSnapshot(
            acceptedMessages: 0,
            sentinelCommitFailed: true);

        await Assert.That(snapshot.SentinelCommitFailed).IsTrue();
        await Assert.That(snapshot.IsSuccessful).IsFalse();
        await Assert.That(snapshot.FailureSamples)
            .Contains(sample => sample.Contains("sentinel transaction failed to commit"));
    }
}
