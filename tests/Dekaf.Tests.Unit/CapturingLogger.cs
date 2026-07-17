using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Unit;

/// <summary>
/// Thread-safe ILogger that records every entry for assertion. Shared so tests stop
/// carrying private copies (several older test files still do — consolidate onto this
/// when touching them).
/// </summary>
internal sealed class CapturingLogger : ILogger
{
    public ConcurrentQueue<(LogLevel Level, string Message)> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Messages.Enqueue((logLevel, formatter(state, exception)));
}
