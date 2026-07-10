namespace Dekaf.StressTests.Tests;

public class ProgramTests
{
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
