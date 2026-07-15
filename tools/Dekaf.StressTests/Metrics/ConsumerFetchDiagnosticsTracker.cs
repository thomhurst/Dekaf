using System.Diagnostics.Metrics;
using System.Runtime;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Networking;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.StressTests.Metrics;

internal sealed class ConsumerFetchDiagnosticsTracker : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);
    private static int s_activeMetricListener;

    private readonly string _topic;
    private readonly bool _enabled;
    private readonly MeterListener? _listener;
    private readonly Func<GcDiagnosticSnapshot> _captureGcDiagnostics;
    private readonly List<ConsumerFetchDiagnosticSample> _samples = [];
    private DateTimeOffset? _lastSampleAtUtc;
    private long _fetchRequestCount;
    private long _fetchDurationTicks;
    private long _receivedBytes;
    private long _lastFetchRequestCount;
    private long _lastFetchDurationTicks;
    private long _lastReceivedBytes;
    private GcDiagnosticSnapshot _lastGcDiagnostics;
    private ConnectionReapDiagnostic[] _connectionReapEvents = [];

    internal ConsumerFetchDiagnosticsTracker(
        string topic,
        bool listenToMetrics = true,
        Func<GcDiagnosticSnapshot>? captureGcDiagnostics = null,
        bool enabled = true)
    {
        _topic = topic;
        _enabled = enabled;
        _captureGcDiagnostics = captureGcDiagnostics ?? CaptureGcDiagnostics;
        if (!enabled || !listenToMetrics)
            return;
        if (Interlocked.CompareExchange(ref s_activeMetricListener, 1, 0) != 0)
            throw new InvalidOperationException("Only one consumer diagnostics metric listener can be active at a time.");

        var fetchDurationName = DekafMetrics.FetchDuration.Name;
        var bytesReceivedName = DekafMetrics.BytesReceived.Name;
        try
        {
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
                // Fetch duration has no destination tag. Stress scenarios run one consumer and
                // one diagnostics listener at a time, enforced above, so process-wide RTT is exact.
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
        catch
        {
            _listener?.Dispose();
            Interlocked.Exchange(ref s_activeMetricListener, 0);
            throw;
        }
    }

    internal void Start(ConsumerDiagnosticSnapshot snapshot)
    {
        if (!_enabled)
            return;

        _lastSampleAtUtc = snapshot.CapturedAtUtc;
        _lastFetchRequestCount = Interlocked.Read(ref _fetchRequestCount);
        _lastFetchDurationTicks = Interlocked.Read(ref _fetchDurationTicks);
        _lastReceivedBytes = Interlocked.Read(ref _receivedBytes);
        _lastGcDiagnostics = _captureGcDiagnostics();
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
        var gcDiagnostics = _captureGcDiagnostics();

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
            PrefetchedBytes = snapshot.PrefetchedBytes,
            Gen0Collections = Math.Max(0, gcDiagnostics.Gen0Collections - _lastGcDiagnostics.Gen0Collections),
            Gen1Collections = Math.Max(0, gcDiagnostics.Gen1Collections - _lastGcDiagnostics.Gen1Collections),
            Gen2Collections = Math.Max(0, gcDiagnostics.Gen2Collections - _lastGcDiagnostics.Gen2Collections),
            GcPauseDurationMs = Math.Max(0, gcDiagnostics.PauseDurationMs - _lastGcDiagnostics.PauseDurationMs),
            GcHeapSizeBytes = gcDiagnostics.HeapSizeBytes,
            GcHeapCount = gcDiagnostics.HeapCount,
            GcDynamicAdaptationMode = gcDiagnostics.DynamicAdaptationMode,
            ServerGc = gcDiagnostics.ServerGc
        });

        _lastSampleAtUtc = snapshot.CapturedAtUtc;
        _lastFetchRequestCount = fetchRequestCount;
        _lastFetchDurationTicks = fetchDurationTicks;
        _lastReceivedBytes = receivedBytes;
        _lastGcDiagnostics = gcDiagnostics;
    }

    internal Task RunSamplerAsync(
        Func<ConsumerDiagnosticSnapshot?> captureSnapshot,
        CancellationToken cancellationToken) =>
        _enabled
            ? StressTestHelpers.RunPeriodicAsync(
                SampleInterval,
                () => TryTakeSample(captureSnapshot),
                cancellationToken)
            : Task.CompletedTask;

    internal void TryTakeSample(Func<ConsumerDiagnosticSnapshot?> captureSnapshot)
    {
        if (!_enabled)
            return;

        try
        {
            if (captureSnapshot() is { } snapshot)
                TakeSample(snapshot);
        }
        catch
        {
            // Diagnostics are best-effort and must never abort a stress run.
        }
    }

    internal ConsumerFetchDiagnosticsSnapshot? GetSnapshot() => _enabled
        ? new()
        {
            Samples = [.. _samples],
            ConnectionReapEvents = [.. _connectionReapEvents]
        }
        : null;

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

    private static GcDiagnosticSnapshot CaptureGcDiagnostics()
    {
        var memoryInfo = GC.GetGCMemoryInfo();
        var configuration = GC.GetConfigurationVariables();
        return new GcDiagnosticSnapshot(
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            GC.GetTotalPauseDuration().TotalMilliseconds,
            memoryInfo.HeapSizeBytes,
            ReadConfigurationInt32(configuration, "HeapCount"),
            ReadConfigurationInt32(configuration, "GCDynamicAdaptationMode"),
            GCSettings.IsServerGC);
    }

    private static int ReadConfigurationInt32(
        IReadOnlyDictionary<string, object> configuration,
        string key) =>
        configuration.TryGetValue(key, out var value)
            ? value switch
            {
                int result => result,
                long result when result is >= int.MinValue and <= int.MaxValue => (int)result,
                uint result when result <= int.MaxValue => (int)result,
                _ => -1
            }
            : -1;

    public void Dispose()
    {
        _listener?.Dispose();
        if (_listener is not null)
            Interlocked.Exchange(ref s_activeMetricListener, 0);
    }
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
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public double GcPauseDurationMs { get; init; }
    public long GcHeapSizeBytes { get; init; }
    public int GcHeapCount { get; init; }
    public int GcDynamicAdaptationMode { get; init; } = -1;
    public bool ServerGc { get; init; }
}

internal readonly record struct GcDiagnosticSnapshot(
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    double PauseDurationMs,
    long HeapSizeBytes,
    int HeapCount,
    int DynamicAdaptationMode,
    bool ServerGc);
