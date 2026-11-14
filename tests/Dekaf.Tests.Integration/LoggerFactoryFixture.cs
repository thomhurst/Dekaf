using Microsoft.Extensions.Logging;
using TUnit.Logging.Microsoft;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Provides a configured ILoggerFactory for tests.
/// Creates a TUnit-backed logger factory that routes output to the current test's output,
/// making producer/consumer logs visible in CI test reports. The TUnit provider's own minimum
/// level (Information by default) applies after the factory's, so Debug messages are dropped;
/// a test that needs them passes a level to <c>AddTUnit</c>.
/// </summary>
internal static class LoggerFactoryFixture
{
    public static ILoggerFactory Create() => LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddTUnit(TestContext.Current!);
    });
}
