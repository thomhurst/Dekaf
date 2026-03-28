using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Emits periodic status lines with instant/average throughput.
/// Uses <see cref="ThroughputTracker.MessageCount"/> as the single source of truth for message counts.
/// Call <see cref="RecordMessage"/> for each consumed or produced message; it checks internally
/// whether enough time has elapsed to warrant a log line (every ~10 seconds).
/// </summary>
internal sealed class PeriodicProgressReporter(ThroughputTracker throughput)
{
    private const double ReportIntervalSeconds = 10;

    private DateTime _lastStatusTime = DateTime.UtcNow;
    private long _lastLoggedMessageCount;

    internal void RecordMessage()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastStatusTime).TotalSeconds >= ReportIntervalSeconds)
            TryLogProgress(now);
    }

    private void TryLogProgress(DateTime now)
    {
        var currentMessageCount = throughput.MessageCount;
        var elapsedSinceLastStatus = (now - _lastStatusTime).TotalSeconds;
        var messagesSinceLastStatus = currentMessageCount - _lastLoggedMessageCount;
        var instantaneousMsgSec = messagesSinceLastStatus / elapsedSinceLastStatus;
        Console.WriteLine($"  [{now:HH:mm:ss}] Progress: {currentMessageCount:N0} messages | instant: {instantaneousMsgSec:N0} msg/sec | avg: {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        _lastStatusTime = now;
        _lastLoggedMessageCount = currentMessageCount;
    }
}
