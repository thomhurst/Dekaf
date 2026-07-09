using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

internal sealed class CapturingLoggerProvider(Action<CapturedLogEntry>? entryCaptured = null) : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public IReadOnlyCollection<CapturedLogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) =>
        new CapturingLogger(categoryName, _entries, entryCaptured);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        string categoryName,
        ConcurrentQueue<CapturedLogEntry> entries,
        Action<CapturedLogEntry>? onEntry) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> structuredState
                ? structuredState.ToArray()
                : [];
            var entry = new CapturedLogEntry(
                categoryName,
                logLevel,
                eventId,
                properties,
                formatter(state, exception));

            entries.Enqueue(entry);
            onEntry?.Invoke(entry);
        }
    }
}

internal sealed record CapturedLogEntry(
    string CategoryName,
    LogLevel LogLevel,
    EventId EventId,
    IReadOnlyList<KeyValuePair<string, object?>> Properties,
    string Message)
{
    public bool TryGetProperty<T>(string name, out T? value)
    {
        foreach (var property in Properties)
        {
            if (property.Key == name && property.Value is T typedValue)
            {
                value = typedValue;
                return true;
            }
        }

        value = default;
        return false;
    }
}
