using System.Diagnostics;

namespace Dekaf.StressTests.Metrics;

/// <summary>
/// Thread-safe throughput tracker for stress tests.
/// Uses atomic operations for lock-free counting.
/// </summary>
internal sealed class ThroughputTracker
{
    private const int MaxErrorSamples = 5;

    private long _messageCount;
    private long _byteCount;
    private long _errorCount;
    private long _deliveryErrorCount;
    private readonly Stopwatch _stopwatch = new();
    private readonly List<double> _messagesPerSecondSamples = [];
    private readonly object _samplesLock = new();
    private readonly List<ThroughputErrorSample> _errorSamples = [];
    private readonly object _errorsLock = new();
    // Lock-free fast path once the sample list is full, so error storms don't contend
    // the produce loop just to discard samples.
    private volatile bool _errorSamplesFull;
    private long _lastSampleMessageCount;
    private DateTime _lastSampleTime;
    private TimeSpan _cpuTimeStart;
    private double _cpuTimeSeconds;

    public long MessageCount => Interlocked.Read(ref _messageCount);
    public long ByteCount => Interlocked.Read(ref _byteCount);
    public long ErrorCount => Interlocked.Read(ref _errorCount);
    public long DeliveryErrorCount => Interlocked.Read(ref _deliveryErrorCount);
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Process CPU time consumed over the Start/Stop window (valid after Stop).
    /// CPU cost per message differentiates client efficiency even when the broker
    /// caps throughput.
    /// </summary>
    public double CpuTimeSeconds => _cpuTimeSeconds;

    public void Start()
    {
        _stopwatch.Start();
        _cpuTimeStart = Environment.CpuUsage.TotalTime;
        _lastSampleTime = DateTime.UtcNow;
        _lastSampleMessageCount = 0;
    }

    public void Stop()
    {
        _stopwatch.Stop();
        _cpuTimeSeconds = (Environment.CpuUsage.TotalTime - _cpuTimeStart).TotalSeconds;
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
        RecordErrorCore(errorType: null, message: null, details: null, operation: null, messageIndex: null);
    }

    public void RecordError(Exception exception, string? operation = null, long? messageIndex = null)
    {
        AddErrorSample(Interlocked.Increment(ref _errorCount), exception, operation, messageIndex);
    }

    public void RecordError(string errorType, string? message, string? operation = null, long? messageIndex = null)
    {
        RecordErrorCore(errorType, message, details: null, operation, messageIndex);
    }

    /// <summary>
    /// Records broker-side delivery failures of messages this client had already accepted
    /// (i.e. counted in <see cref="MessageCount"/>). Kept separate from <see cref="RecordError"/>:
    /// loop errors happen before a message is accepted, delivery errors after — the distinction
    /// is what lets undelivered loss be computed as accepted - delivered - deliveryErrors.
    /// Count-only overload for metric listeners that observe failures in bulk (per batch).
    /// </summary>
    public void RecordDeliveryErrors(long count)
    {
        Interlocked.Add(ref _deliveryErrorCount, count);
    }

    /// <summary>
    /// Records a single delivery failure with exception detail for the failure report.
    /// </summary>
    public void RecordDeliveryError(string errorType, string? message, string? operation = null, long? messageIndex = null)
    {
        AddErrorSample(Interlocked.Increment(ref _deliveryErrorCount), errorType, message, details: null, operation, messageIndex);
    }

    /// <summary>
    /// Records exception detail for a delivery failure whose count is already tracked
    /// elsewhere (e.g. via the producer's error metric), without double-counting.
    /// </summary>
    public void RecordDeliveryErrorDetail(Exception exception, string? operation = null, long? messageIndex = null)
    {
        AddErrorSample(DeliveryErrorCount, exception, operation, messageIndex);
    }

    private void RecordErrorCore(
        string? errorType,
        string? message,
        string? details,
        string? operation,
        long? messageIndex)
    {
        var errorNumber = Interlocked.Increment(ref _errorCount);
        AddErrorSample(errorNumber, errorType, message, details, operation, messageIndex);
    }

    private void AddErrorSample(long errorNumber, Exception exception, string? operation, long? messageIndex)
    {
        // Checked before formatting: rendering a stack trace per message during an
        // error storm would contend the produce loop for nothing once samples are full.
        if (_errorSamplesFull)
        {
            return;
        }

        AddErrorSample(
            errorNumber,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.ToString(),
            operation,
            messageIndex);
    }

    private void AddErrorSample(
        long errorNumber,
        string? errorType,
        string? message,
        string? details,
        string? operation,
        long? messageIndex)
    {
        if (_errorSamplesFull)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(errorType) && string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(details))
        {
            return;
        }

        lock (_errorsLock)
        {
            if (_errorSamples.Count >= MaxErrorSamples)
            {
                _errorSamplesFull = true;
                return;
            }

            _errorSamples.Add(new ThroughputErrorSample
            {
                ErrorNumber = errorNumber,
                OccurredAtUtc = DateTime.UtcNow,
                ElapsedSeconds = _stopwatch.Elapsed.TotalSeconds,
                AcceptedMessagesAtError = MessageCount,
                MessageIndex = messageIndex,
                Operation = string.IsNullOrWhiteSpace(operation) ? null : operation,
                ExceptionType = string.IsNullOrWhiteSpace(errorType) ? "Unknown" : errorType,
                Message = string.IsNullOrWhiteSpace(message) ? null : message,
                Details = string.IsNullOrWhiteSpace(details) ? null : details
            });
        }
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

        List<ThroughputErrorSample> errorSamplesCopy;
        lock (_errorsLock)
        {
            errorSamplesCopy = [.. _errorSamples];
        }

        return new ThroughputSnapshot
        {
            TotalMessages = MessageCount,
            TotalBytes = ByteCount,
            TotalErrors = ErrorCount,
            TotalDeliveryErrors = DeliveryErrorCount,
            ElapsedSeconds = _stopwatch.Elapsed.TotalSeconds,
            AverageMessagesPerSecond = GetAverageMessagesPerSecond(),
            AverageMegabytesPerSecond = GetAverageMegabytesPerSecond(),
            MessagesPerSecondSamples = samplesCopy,
            ErrorSamples = errorSamplesCopy
        };
    }
}

internal sealed class ThroughputSnapshot
{
    public required long TotalMessages { get; init; }
    public required long TotalBytes { get; init; }
    public required long TotalErrors { get; init; }

    /// <summary>
    /// Broker-side delivery failures of accepted messages. Absent from older result
    /// files, so it defaults to zero on deserialization.
    /// </summary>
    public long TotalDeliveryErrors { get; init; }

    public required double ElapsedSeconds { get; init; }
    public required double AverageMessagesPerSecond { get; init; }
    public required double AverageMegabytesPerSecond { get; init; }
    public required List<double> MessagesPerSecondSamples { get; init; }
    public List<ThroughputErrorSample> ErrorSamples { get; init; } = [];
}

internal sealed class ThroughputErrorSample
{
    public required long ErrorNumber { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public required double ElapsedSeconds { get; init; }
    public required long AcceptedMessagesAtError { get; init; }
    public long? MessageIndex { get; init; }
    public string? Operation { get; init; }
    public required string ExceptionType { get; init; }
    public string? Message { get; init; }
    public string? Details { get; init; }
}
