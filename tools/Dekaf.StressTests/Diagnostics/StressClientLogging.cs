using Microsoft.Extensions.Logging;

namespace Dekaf.StressTests;

internal static class StressClientLogging
{
    internal const string LogLevelEnvironmentVariable = "DEKAF_STRESS_LOG_LEVEL";

    internal static LogLevel MinimumLevel { get; } =
        ParseLevel(Environment.GetEnvironmentVariable(LogLevelEnvironmentVariable));

    internal static ILoggerFactory LoggerFactory { get; } = new ConsoleLoggerFactory(MinimumLevel);

    internal static ILoggerFactory Create(string? configuredLevel) =>
        new ConsoleLoggerFactory(ParseLevel(configuredLevel));

    internal static LogLevel ParseLevel(string? configuredLevel) =>
        Enum.TryParse<LogLevel>(configuredLevel, ignoreCase: true, out var level)
            ? level
            : LogLevel.Warning;

    private sealed class ConsoleLoggerFactory(LogLevel minimumLevel) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new ConsoleLogger(categoryName, minimumLevel);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }
    }

    private sealed class ConsoleLogger(string categoryName, LogLevel minimumLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && logLevel >= minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            Console.Error.WriteLine(
                $"[{DateTimeOffset.UtcNow:O}] {logLevel,-11} {categoryName}[{eventId.Id}] {formatter(state, exception)}");
            if (exception is not null)
                Console.Error.WriteLine(exception);
        }
    }
}
