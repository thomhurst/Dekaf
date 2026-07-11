using Dekaf.StressTests;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class StressClientLoggingTests
{
    [Test]
    public async Task Create_DefaultsToWarning()
    {
        using var factory = StressClientLogging.Create(null);
        var logger = factory.CreateLogger("test");

        await Assert.That(logger.IsEnabled(LogLevel.Debug)).IsFalse();
        await Assert.That(logger.IsEnabled(LogLevel.Information)).IsFalse();
        await Assert.That(logger.IsEnabled(LogLevel.Warning)).IsTrue();
        await Assert.That(logger.IsEnabled(LogLevel.Error)).IsTrue();
    }

    [Test]
    public async Task Create_DebugOverrideEnablesDebug()
    {
        using var factory = StressClientLogging.Create("Debug");
        var logger = factory.CreateLogger("test");

        await Assert.That(logger.IsEnabled(LogLevel.Debug)).IsTrue();
        await Assert.That(logger.IsEnabled(LogLevel.Trace)).IsFalse();
    }

    [Test]
    public async Task ParseLevel_InvalidValueFallsBackToWarning()
    {
        await Assert.That(StressClientLogging.ParseLevel("verbose"))
            .IsEqualTo(LogLevel.Warning);
    }

    [Test]
    public async Task ParseLevel_UndefinedNumericValueFallsBackToWarning()
    {
        await Assert.That(StressClientLogging.ParseLevel("10"))
            .IsEqualTo(LogLevel.Warning);
    }
}
