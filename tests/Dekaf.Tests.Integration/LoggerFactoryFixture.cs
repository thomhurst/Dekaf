using Microsoft.Extensions.Logging;
using TUnit.Core;
using TUnit.Logging.Microsoft;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Provides a configured ILoggerFactory for tests.
/// Creates a TUnit-backed logger factory that routes output to the current test's output,
/// making producer/consumer debug logs visible in CI test reports.
/// </summary>
internal static class LoggerFactoryFixture
{
    public static ILoggerFactory Create() => LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddTUnit(TestContext.Current!);
    });
}
