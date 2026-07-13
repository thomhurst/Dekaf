using System.Diagnostics.Metrics;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Networking;

namespace Dekaf.StressTests.Metrics;

internal sealed class ConsumerFetchDiagnosticsTracker : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);

    private readonly string _topic;
    private readonly MeterListener? _listener;
    private readonly List<ConsumerFetchDiagnosticSample> _samples = [];
    private DateTimeOffset? _lastSampleAtUtc;
    private long _fetchRequestCount;
    private long _fetchDurationTicks;
    private long _receivedBytes;
    private long _lastFetchRequestCount;
    private long _lastFetchDurationTicks;
    private long _lastReceivedBytes;
    private ConnectionReapDiagnostic[] _connectionReapEvents = [];

    internal ConsumerFetchDiagnosticsTracker(string topic, bool listenToMetrics = true)
    {
        _topic = topic;
        if (!listenToMetrics)
            return;

        var fetchDurationName = DekafMetrics.FetchDuration.Name;
        var bytesReceivedName = DekafMetrics.BytesReceived.Name;
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                    (instrument.Name == fetchDurationName || instrument.Name == bytesReceivedName))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == fetchDurationName)
                RecordFetchDuration(measurement);
        });
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == bytesReceivedName && HasTopic(tags))
                RecordBytesReceived(measurement);
        });
        _listener.Start();
    }

    internal void Start(ConsumerDiagnosticSnapshot snapshot)
    {
        _lastSampleAtUtc = snapshot.CapturedAtUtc;
        _lastFetchRequestCount = Interlocked.Read(ref _fetchRequestCount);
        _lastFetchDurationTicks = Interlocked.Read(ref _fetchDurationTicks);
        _lastReceivedBytes = Interlocked.Read(ref _receivedBytes);
        _connectionReapEvents = snapshot.ConnectionReapEvents;
    }

    internal void RecordFetchDuration(double durationSeconds)
    {
        Interlocked.Increment(ref _fetchRequestCount);
        Interlocked.Add(
            ref _fetchDurationTicks,
            (long)Math.Round(durationSeconds * TimeSpan.TicksPerSecond));
    }

    internal void RecordBytesReceived(long bytes) => Interlocked.Add(ref _receivedBytes, bytes);

    internal void TakeSample(ConsumerDiagnosticSnapshot snapshot)
    {
        if (_lastSampleAtUtc is not { } lastSampleAtUtc)
        {
            Start(snapshot);
            return;
        }

        _connectionReapEvents = snapshot.ConnectionReapEvents;
        var intervalSeconds = (snapshot.CapturedAtUtc - lastSampleAtUtc).TotalSeconds;
        if (intervalSeconds <= 0)
            return;

        var fetchRequestCount = Interlocked.Read(ref _fetchRequestCount);
        var fetchDurationTicks = Interlocked.Read(ref _fetchDurationTicks);
        var receivedBytes = Interlocked.Read(ref _receivedBytes);
        var intervalFetches = fetchRequestCount - _lastFetchRequestCount;
        var intervalFetchDurationTicks = fetchDurationTicks - _lastFetchDurationTicks;
        var intervalReceivedBytes = receivedBytes - _lastReceivedBytes;

        _samples.Add(new ConsumerFetchDiagnosticSample
        {
            CapturedAtUtc = snapshot.CapturedAtUtc,
            IntervalSeconds = intervalSeconds,
            FetchRequestCount = intervalFetches,
            FetchRequestsPerSecond = intervalFetches / intervalSeconds,
            BytesPerFetch = intervalFetches > 0 ? (double)intervalReceivedBytes / intervalFetches : 0,
            AverageFetchRttMs = intervalFetches > 0
                ? intervalFetchDurationTicks * 1_000.0 / TimeSpan.TicksPerSecond / intervalFetches
                : 0,
            PendingFetchDepth = snapshot.PendingFetchDepth,
            PrefetchBufferDepth = snapshot.PrefetchBufferDepth,
            PrefetchDepth = snapshot.PrefetchDepth,
            PrefetchedBytes = snapshot.PrefetchedBytes
        });

        _lastSampleAtUtc = snapshot.CapturedAtUtc;
        _lastFetchRequestCount = fetchRequestCount;
        _lastFetchDurationTicks = fetchDurationTicks;
        _lastReceivedBytes = receivedBytes;
    }

    internal async Task RunSamplerAsync(
        Func<ConsumerDiagnosticSnapshot?> captureSnapshot,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SampleInterval, cancellationToken).ConfigureAwait(false);
                if (captureSnapshot() is { } snapshot)
                    TakeSample(snapshot);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal ConsumerFetchDiagnosticsSnapshot GetSnapshot() => new()
    {
        Samples = [.. _samples],
        ConnectionReapEvents = [.. _connectionReapEvents]
    };

    private bool HasTopic(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == DekafDiagnostics.MessagingDestinationName &&
                tag.Value is string topic &&
                topic == _topic)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose() => _listener?.Dispose();
}

internal sealed class ConsumerFetchDiagnosticsSnapshot
{
    public List<ConsumerFetchDiagnosticSample> Samples { get; init; } = [];
    public List<ConnectionReapDiagnostic> ConnectionReapEvents { get; init; } = [];
}

internal sealed class ConsumerFetchDiagnosticSample
{
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required double IntervalSeconds { get; init; }
    public required long FetchRequestCount { get; init; }
    public required double FetchRequestsPerSecond { get; init; }
    public required double BytesPerFetch { get; init; }
    public required double AverageFetchRttMs { get; init; }
    public required int PendingFetchDepth { get; init; }
    public required int PrefetchBufferDepth { get; init; }
    public required int PrefetchDepth { get; init; }
    public required long PrefetchedBytes { get; init; }
}
