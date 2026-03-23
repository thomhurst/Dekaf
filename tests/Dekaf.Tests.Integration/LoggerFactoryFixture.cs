using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Provides a configured ILoggerFactory for tests.
/// Replaces the removed Kafka.DefaultLoggerFactory static property.
/// </summary>
internal sealed class LoggerFactoryFixture
{
    private readonly ILoggerFactory _loggerFactory;

    public LoggerFactoryFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss.fff ";
            });
        });
        
        var provider = services.BuildServiceProvider();
        _loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    }

    public ILoggerFactory LoggerFactory => _loggerFactory;

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}
