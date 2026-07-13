using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class TransactionalReportingTests
{
    [Test]
    public async Task CreateAllScenarios_IncludesPairedTransactionalProducers()
    {
        var scenarios = Dekaf.StressTests.Program.CreateAllScenarios()
            .Where(item => item.Name == "producer-transactional")
            .ToList();

        await Assert.That(scenarios).Count().IsEqualTo(2);
        await Assert.That(scenarios.Select(item => item.Client))
            .IsEquivalentTo(["Dekaf", "Confluent"]);
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
    public async Task Generate_PairedTransactionalResults_UsesConfluentBaseline()
    {
        var verification = CreateVerification();
        var dekaf = CreateResult(verification, client: "Dekaf", elapsedSeconds: 100);
        var confluent = CreateResult(verification, client: "Confluent", elapsedSeconds: 200);
        var results = new StressTestResults
        {
            RunStartedAtUtc = dekaf.StartedAtUtc,
            RunCompletedAtUtc = dekaf.CompletedAtUtc,
            MachineName = "test",
            ProcessorCount = 4,
            Results = [dekaf, confluent]
        };

        var markdown = MarkdownReporter.Generate(results);

        await Assert.That(markdown).Contains("| Confluent");
        await Assert.That(markdown).Contains("1.00x");
        await Assert.That(markdown).Contains("2.00x");
    }

    [Test]
    public async Task Generate_TransactionalResult_ReportsCpuPerRequestAndStandingCores()
    {
        var result = CreateResult(
            CreateVerification(),
            elapsedSeconds: 100,
            cpuTimeSeconds: 20,
            produceRequestCount: 50);
        var results = new StressTestResults
        {
            RunStartedAtUtc = result.StartedAtUtc,
            RunCompletedAtUtc = result.CompletedAtUtc,
            MachineName = "test",
            ProcessorCount = 4,
            Results = [result]
        };

        var markdown = MarkdownReporter.Generate(results);

        await Assert.That(result.CpuMicrosPerRequest).IsEqualTo(400_000);
        await Assert.That(result.StandingCores).IsEqualTo(0.2);
        await Assert.That(markdown).Contains("CPU μs/request");
        await Assert.That(markdown).Contains("Standing cores");
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

    [Test]
    public async Task CheckForFailures_MissingAllocationMeasurement_FailsRun()
    {
        var failed = Dekaf.StressTests.Program.CheckForFailures([
            CreateResult(CreateVerification(), allocatedBytes: null)
        ]);

        await Assert.That(failed).IsTrue();
    }

    private static StressTestResult CreateResult(
        TransactionVerificationSnapshot verification,
        string client = "Dekaf",
        double elapsedSeconds = 900,
        long? allocatedBytes = 0,
        double? cpuTimeSeconds = null,
        long? produceRequestCount = null)
    {
        var now = DateTime.UtcNow;
        return new StressTestResult
        {
            Scenario = "producer-transactional",
            Client = client,
            DurationMinutes = 15,
            MessageSizeBytes = 1000,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(15),
            Throughput = new ThroughputSnapshot
            {
                TotalMessages = verification.AcceptedMessages,
                TotalBytes = verification.AcceptedMessages * 1000,
                TotalErrors = 0,
                ElapsedSeconds = elapsedSeconds,
                AverageMessagesPerSecond = verification.AcceptedMessages / elapsedSeconds,
                AverageMegabytesPerSecond = 0.001,
                MessagesPerSecondSamples = [],
                ErrorSamples = []
            },
            DeliveredMessages = verification.DeliveredMessages,
            CpuTimeSeconds = cpuTimeSeconds,
            ProducerDeliveryDiagnostics = produceRequestCount is { } requestCount
                ? new ProducerDeliveryDiagnosticsSnapshot { ProduceRequestCount = requestCount }
                : null,
            GcStats = new GcSnapshot
            {
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0,
                AllocatedBytes = allocatedBytes
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
