using Dekaf.StressTests.FaultInjection;

namespace Dekaf.StressTests.Tests.FaultInjection;

public class TransactionWindowVerifierTests
{
    private const int RecordsPerTransaction = 3;

    [Test]
    public async Task Verify_CommittedWholeAbortedInvisible_Passes()
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(0, TransactionOutcomeKind.Committed), Outcome(1, TransactionOutcomeKind.Aborted)],
            Records(0, 0, 1, 2),
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.CommittedCount).IsEqualTo(1);
        await Assert.That(result.AbortedCount).IsEqualTo(1);
        await Assert.That(result.UnknownCount).IsEqualTo(0);
    }

    [Test]
    public async Task Verify_CommittedTransactionMissingARecord_Fails()
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(0, TransactionOutcomeKind.Committed)],
            Records(0, 0, 2),
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.CommittedButIncomplete).IsEquivalentTo([0L]);
    }

    [Test]
    public async Task Verify_CommittedTransactionNotVisibleAtAll_Fails()
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(0, TransactionOutcomeKind.Committed)],
            [],
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.CommittedButIncomplete).IsEquivalentTo([0L]);
    }

    [Test]
    public async Task Verify_AbortedTransactionVisible_Fails()
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(4, TransactionOutcomeKind.Aborted)],
            Records(4, 1),
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.AbortedButVisible).IsEquivalentTo([4L]);
    }

    [Test]
    [Arguments(new int[0])]
    [Arguments(new[] { 0, 1, 2 })]
    public async Task Verify_UnknownOutcomeAllOrNothing_Passes(int[] visibleIndexes)
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(7, TransactionOutcomeKind.Unknown)],
            Records(7, visibleIndexes),
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.UnknownCount).IsEqualTo(1);
    }

    [Test]
    public async Task Verify_UnknownOutcomePartlyVisible_Fails()
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(7, TransactionOutcomeKind.Unknown)],
            Records(7, 0, 1),
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.UnknownButPartial).IsEquivalentTo([7L]);
    }

    [Test]
    public async Task Verify_RecordVisibleTwice_Fails()
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(2, TransactionOutcomeKind.Committed)],
            Records(2, 0, 1, 2, 1),
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.DuplicatedTransactions).IsEquivalentTo([2L]);
        await Assert.That(result.CommittedButIncomplete).IsEmpty();
    }

    [Test]
    public async Task Verify_RecordOfATransactionThatNeverStarted_Fails()
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(0, TransactionOutcomeKind.Committed)],
            [.. Records(0, 0, 1, 2), new VisibleTransactionRecord(9, 0)],
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.UnexpectedTransactions).IsEquivalentTo([9L]);
    }

    [Test]
    public async Task Verify_RecordIndexOutsideTheTransaction_Fails()
    {
        var result = TransactionWindowVerifier.Verify(
            [Outcome(0, TransactionOutcomeKind.Committed)],
            [.. Records(0, 0, 1, 2), new VisibleTransactionRecord(0, RecordsPerTransaction)],
            RecordsPerTransaction);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.UnexpectedTransactions).IsEquivalentTo([0L]);
    }

    [Test]
    public async Task Verify_TransactionRecordedTwice_Throws()
    {
        await Assert.That(() => TransactionWindowVerifier.Verify(
                [Outcome(0, TransactionOutcomeKind.Committed), Outcome(0, TransactionOutcomeKind.Aborted)],
                [],
                RecordsPerTransaction))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task TransactionRecordKey_RoundTrips()
    {
        var key = FaultInjectionRunner.FormatTransactionRecordKey(transactionId: 1234, index: 3);

        var parsed = FaultInjectionRunner.TryParseTransactionRecordKey(key, out var record);

        await Assert.That(parsed).IsTrue();
        await Assert.That(record).IsEqualTo(new VisibleTransactionRecord(1234, 3));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("12")]
    [Arguments(":3")]
    [Arguments("12:")]
    [Arguments("-1:3")]
    [Arguments("12:x")]
    public async Task TransactionRecordKey_RejectsMalformedKeys(string? key)
    {
        var parsed = FaultInjectionRunner.TryParseTransactionRecordKey(key, out _);

        await Assert.That(parsed).IsFalse();
    }

    [Test]
    public async Task DescribeTransactionViolations_NamesEveryBrokenInvariant()
    {
        var verification = TransactionWindowVerifier.Verify(
            [
                Outcome(0, TransactionOutcomeKind.Committed),
                Outcome(1, TransactionOutcomeKind.Aborted),
                Outcome(2, TransactionOutcomeKind.Unknown)
            ],
            [.. Records(1, 0), .. Records(2, 0)],
            RecordsPerTransaction);

        var violations = FaultInjectionRunner.DescribeTransactionViolations(verification);

        await Assert.That(violations).Count().IsEqualTo(3);
        await Assert.That(violations[0]).Contains("committed but not fully visible: 0");
        await Assert.That(violations[1]).Contains("aborted but visible: 1");
        await Assert.That(violations[2]).Contains("unknown outcome but partly visible: 2");
    }

    [Test]
    public async Task DetermineExitCode_AllowanceDoesNotHideAnotherFailureInTheSameWindow()
    {
        FaultWindowRunResult[] results =
        [
            new()
            {
                Name = "leader-election",
                StartedAtUtc = DateTime.UnixEpoch,
                Succeeded = false,
                LiveConsumerRecoveryFailed = true,
                TransactionViolations = ["aborted but visible: 1"]
            }
        ];
        var allowedFailures = new HashSet<string>(["leader-election"], StringComparer.OrdinalIgnoreCase);

        var exitCode = FaultInjectionRunner.DetermineExitCode(results, allowedFailures);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    private static TransactionOutcome Outcome(long transactionId, TransactionOutcomeKind kind) =>
        new(transactionId, kind);

    private static VisibleTransactionRecord[] Records(long transactionId, params int[] indexes) =>
        [.. indexes.Select(index => new VisibleTransactionRecord(transactionId, index))];
}
