using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class TransactionalReportingTests
{
    [Test]
    public async Task CreateAllScenarios_IncludesTransactionalProducer()
    {
        var scenario = Dekaf.StressTests.Program.CreateAllScenarios()
            .SingleOrDefault(item => item.Name == "producer-transactional");

        await Assert.That(scenario).IsNotNull();
        await Assert.That(scenario!.Client).IsEqualTo("Dekaf");
    }

    [Test]
    public async Task Generate_TransactionalResult_IncludesVerificationCounts()
    {
        var verification = CreateVerification();
        var result = CreateResult(verification);
        var results = new StressTestResults
        {
            RunStartedAtUtc = result.StartedAtUtc,
            RunCompletedAtUtc = result.CompletedAtUtc,
            MachineName = "test",
            ProcessorCount = 4,
            Results = [result]
        };

        var markdown = MarkdownReporter.Generate(results);

        await Assert.That(markdown).Contains("Transactional EOS");
        await Assert.That(markdown).Contains("Transaction Verification");
        await Assert.That(markdown).Contains("| Dekaf");
        await Assert.That(markdown).Contains("| Dekaf     |      100 |");
        await Assert.That(markdown).Contains("|        75 |      25 |        75 |");
    }

    [Test]
    public async Task CheckForFailures_DuplicateTransactionalRecord_FailsRun()
    {
        var verification = CreateVerification(duplicateMessages: 1);

        var failed = Dekaf.StressTests.Program.CheckForFailures([CreateResult(verification)]);

        await Assert.That(failed).IsTrue();
    }

    [Test]
    public async Task CheckForFailures_SuccessfulTransaction_DoesNotTreatAbortAsLoss()
    {
        var verification = CreateVerification();

        var failed = Dekaf.StressTests.Program.CheckForFailures([CreateResult(verification)]);

        await Assert.That(failed).IsFalse();
    }

    private static StressTestResult CreateResult(TransactionVerificationSnapshot verification)
    {
        var now = DateTime.UtcNow;
        return new StressTestResult
        {
            Scenario = "producer-transactional",
            Client = "Dekaf",
            DurationMinutes = 15,
            MessageSizeBytes = 1000,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(15),
            Throughput = new ThroughputSnapshot
            {
                TotalMessages = verification.AcceptedMessages,
                TotalBytes = verification.AcceptedMessages * 1000,
                TotalErrors = 0,
                ElapsedSeconds = 900,
                AverageMessagesPerSecond = verification.AcceptedMessages / 900.0,
                AverageMegabytesPerSecond = 0.001,
                MessagesPerSecondSamples = [],
                ErrorSamples = []
            },
            DeliveredMessages = verification.DeliveredMessages,
            GcStats = new GcSnapshot
            {
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0,
                AllocatedBytes = 0
            },
            TransactionVerification = verification
        };
    }

    private static TransactionVerificationSnapshot CreateVerification(long duplicateMessages = 0) => new()
    {
        AcceptedMessages = 100,
        CommittedMessages = 75,
        AbortedMessages = 25,
        DeliveredMessages = 75,
        DuplicateMessages = duplicateMessages,
        ShortfallMessages = 0,
        LeakedAbortedMessages = 0,
        UnexpectedMessages = 0,
        MissingSentinelPartitions = 0,
        SentinelCommitFailed = false,
        FailureSamples = duplicateMessages == 0 ? [] : ["Duplicate committed index 1."]
    };
}
