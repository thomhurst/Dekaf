using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Tests;

public class ProgramTests
{
    [Test]
    public async Task StressTestResults_SerializesPairedSampleMetadata()
    {
        var results = new StressTestResults
        {
            RunStartedAtUtc = DateTime.UtcNow,
            RunCompletedAtUtc = DateTime.UtcNow,
            MachineName = "test",
            ProcessorCount = 2,
            PairedClientOrder = "dekaf-first",
            PairedSampleIndex = 1,
            PairedSampleCount = 2,
            Results = []
        };

        var json = results.ToJson();

        await Assert.That(json).Contains("\"pairedClientOrder\": \"dekaf-first\"");
        await Assert.That(json).Contains("\"pairedSampleIndex\": 1");
        await Assert.That(json).Contains("\"pairedSampleCount\": 2");
    }

    [Test]
    public async Task EscalateUnobservedTaskExceptions_SuccessfulRunFails()
    {
        Exception[] exceptions = [new InvalidOperationException("escaped background failure")];

        var exitCode = Program.EscalateUnobservedTaskExceptions(0, exceptions);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task EscalateUnobservedTaskExceptions_NoExceptionsPreservesExitCode()
    {
        var exitCode = Program.EscalateUnobservedTaskExceptions(7, []);

        await Assert.That(exitCode).IsEqualTo(7);
    }
}
