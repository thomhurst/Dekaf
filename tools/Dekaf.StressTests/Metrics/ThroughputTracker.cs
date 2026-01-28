using System.Diagnostics;

namespace Dekaf.StressTests.Metrics;

/// <summary>
/// Thread-safe throughput tracker for stress tests.
/// Uses atomic operations for lock-free counting.
/// </summary>
internal sealed class ThroughputTracker
{
    private long _messageCount;
    private long _byteCount;
    private long _errorCount;
    private readonly Stopwatch _stopwatch = new();
    private readonly List<double> _messagesPerSecondSamples = [];
    private readonly object _samplesLock = new();
    private long _lastSampleMessageCount;
    private DateTime _lastSampleTime;

    public long MessageCount => Interlocked.Read(ref _messageCount);
    public long ByteCount => Interlocked.Read(ref _byteCount);
    public long ErrorCount => Interlocked.Read(ref _errorCount);
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Start()
    {
        _stopwatch.Start();
        _lastSampleTime = DateTime.UtcNow;
        _lastSampleMessageCount = 0;
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public void RecordMessage(int bytes)
    {
        Interlocked.Increment(ref _messageCount);
        Interlocked.Add(ref _byteCount, bytes);
    }

    public void RecordMessages(long count, long bytes)
    {
        Interlocked.Add(ref _messageCount, count);
        Interlocked.Add(ref _byteCount, bytes);
    }

    public void RecordError()
    {
        Interlocked.Increment(ref _errorCount);
    }

    public void TakeSample()
    {
        var now = DateTime.UtcNow;
        var currentCount = MessageCount;

        var elapsedSeconds = (now - _lastSampleTime).TotalSeconds;
        if (elapsedSeconds > 0)
        {
            var messagesInInterval = currentCount - _lastSampleMessageCount;
            var rate = messagesInInterval / elapsedSeconds;

            lock (_samplesLock)
            {
                _messagesPerSecondSamples.Add(rate);
            }
        }

        _lastSampleTime = now;
        _lastSampleMessageCount = currentCount;
    }

    public double GetAverageMessagesPerSecond()
    {
        if (_stopwatch.Elapsed.TotalSeconds == 0)
        {
            return 0;
        }

        return MessageCount / _stopwatch.Elapsed.TotalSeconds;
    }

    public double GetAverageBytesPerSecond()
    {
        if (_stopwatch.Elapsed.TotalSeconds == 0)
        {
            return 0;
        }

        return ByteCount / _stopwatch.Elapsed.TotalSeconds;
    }

    public double GetAverageMegabytesPerSecond()
    {
        return GetAverageBytesPerSecond() / (1024.0 * 1024.0);
    }

    public ThroughputSnapshot GetSnapshot()
    {
        List<double> samplesCopy;
        lock (_samplesLock)
        {
            samplesCopy = [.. _messagesPerSecondSamples];
        }

        return new ThroughputSnapshot
        {
            TotalMessages = MessageCount,
            TotalBytes = ByteCount,
            TotalErrors = ErrorCount,
            ElapsedSeconds = _stopwatch.Elapsed.TotalSeconds,
            AverageMessagesPerSecond = GetAverageMessagesPerSecond(),
            AverageMegabytesPerSecond = GetAverageMegabytesPerSecond(),
            MessagesPerSecondSamples = samplesCopy
        };
    }
}

internal sealed class ThroughputSnapshot
{
    public required long TotalMessages { get; init; }
    public required long TotalBytes { get; init; }
    public required long TotalErrors { get; init; }
    public required double ElapsedSeconds { get; init; }
    public required double AverageMessagesPerSecond { get; init; }
    public required double AverageMegabytesPerSecond { get; init; }
    public required List<double> MessagesPerSecondSamples { get; init; }
}
