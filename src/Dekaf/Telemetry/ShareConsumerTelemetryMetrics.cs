using System.Diagnostics;
using System.Collections.Concurrent;
using Dekaf.Networking;

namespace Dekaf.Telemetry;

/// <summary>Request/window aggregates for the KIP-932 standard client metrics.</summary>
internal sealed class ShareConsumerTelemetryMetrics
{
    private const string Prefix = "org.apache.kafka.consumer.share.";
    private const string FetchPrefix = Prefix + "fetch.manager.";
    private const string CoordinatorPrefix = Prefix + "coordinator.";
    private static readonly string[] Names =
    [
        Prefix + "last.poll.seconds.ago", Prefix + "time.between.poll.avg",
        Prefix + "time.between.poll.max", Prefix + "poll.idle.ratio.avg",
        CoordinatorPrefix + "heartbeat.response.time.max", CoordinatorPrefix + "heartbeat.rate",
        CoordinatorPrefix + "heartbeat.total", CoordinatorPrefix + "last.heartbeat.seconds.ago",
        CoordinatorPrefix + "rebalance.total", CoordinatorPrefix + "rebalance.rate.per.hour",
        FetchPrefix + "fetch.size.avg", FetchPrefix + "fetch.size.max",
        FetchPrefix + "bytes.consumed.rate", FetchPrefix + "bytes.consumed.total",
        FetchPrefix + "records.per.request.avg", FetchPrefix + "records.per.request.max",
        FetchPrefix + "records.consumed.rate", FetchPrefix + "records.consumed.total",
        FetchPrefix + "acknowledgements.send.rate", FetchPrefix + "acknowledgements.send.total",
        FetchPrefix + "acknowledgements.error.rate", FetchPrefix + "acknowledgements.error.total",
        FetchPrefix + "fetch.latency.avg", FetchPrefix + "fetch.latency.max",
        FetchPrefix + "fetch.rate", FetchPrefix + "fetch.total",
        FetchPrefix + "fetch.throttle.time.avg", FetchPrefix + "fetch.throttle.time.max"
    ];
    // Declaration order matches Names and the per-metric collection cursors.
    private enum Metric
    {
        LastPollSecondsAgo,
        TimeBetweenPollAverage,
        TimeBetweenPollMax,
        PollIdleRatioAverage,
        HeartbeatResponseTimeMax,
        HeartbeatRate,
        HeartbeatTotal,
        LastHeartbeatSecondsAgo,
        RebalanceTotal,
        RebalanceRatePerHour,
        FetchSizeAverage,
        FetchSizeMax,
        BytesConsumedRate,
        BytesConsumedTotal,
        RecordsPerRequestAverage,
        RecordsPerRequestMax,
        RecordsConsumedRate,
        RecordsConsumedTotal,
        AcknowledgementsSendRate,
        AcknowledgementsSendTotal,
        AcknowledgementsErrorRate,
        AcknowledgementsErrorTotal,
        FetchLatencyAverage,
        FetchLatencyMax,
        FetchRate,
        FetchTotal,
        FetchThrottleTimeAverage,
        FetchThrottleTimeMax,
    }

    private readonly Func<long> _clock;
    private readonly long _frequency;
    private readonly ConcurrentDictionary<int, FetchSample> _fetchSamples = new();
    private readonly Measurement _pollInterval = new();
    private readonly Measurement _pollIdleRatio = new();
    private readonly Measurement _heartbeatLatency = new();
    private readonly Measurement _fetchLatency = new();
    private readonly Measurement _fetchThrottle = new();
    private readonly bool[] _requestedRates = new bool[Names.Length];
    private readonly long[] _previousValues = new long[Names.Length];
    private readonly long[] _previousTimes = new long[Names.Length];
    private long _heartbeats, _rebalances, _fetches, _bytes, _records, _sentAcks, _failedAcks;
    private long _maxFetchBytes, _maxFetchRecords;
    private long _recordFetches;
    private long _lastPoll = -1, _lastHeartbeat = -1;
    private long _pollIdleTicks;
    private int _enabled;
    private int _recordEpoch;
    private int _fetchEpoch;
    private int _acknowledgementEpoch;

    [Flags]
    internal enum Groups { None = 0, Poll = 1, Heartbeat = 2, Rebalance = 4, Fetch = 8, Records = 16, Acknowledgements = 32 }

    internal ShareConsumerTelemetryMetrics(Func<long>? timestampClock = null, long timestampFrequency = 0)
    {
        _clock = timestampClock ?? Stopwatch.GetTimestamp;
        _frequency = timestampFrequency > 0 ? timestampFrequency : Stopwatch.Frequency;
        var created = _clock();
        for (var index = 0; index < _previousTimes.Length; index++)
            _previousTimes[index] = created;
    }

    internal bool Enabled(Groups group) => ((Groups)Volatile.Read(ref _enabled) & group) != 0;
    internal long Timestamp(Groups group) => Enabled(group) ? _clock() : -1;

    internal void Subscribe(IReadOnlyList<string> prefixes)
    {
        var groups = Groups.None;
        var now = _clock();
        for (var index = 0; index < Names.Length; index++)
        {
            var requested = Requested(Names[index], prefixes);
            var metric = (Metric)index;
            if (IsRate(metric))
            {
                if (requested && !_requestedRates[index])
                {
                    _previousTimes[index] = now;
                    _previousValues[index] = GetTotal(metric);
                }
                _requestedRates[index] = requested;
            }
            if (!requested) continue;
            groups |= index switch
            {
                < 4 => Groups.Poll,
                < 8 => Groups.Heartbeat,
                < 10 => Groups.Rebalance,
                < 18 => Groups.Records | Groups.Fetch,
                < 22 => Groups.Acknowledgements,
                _ => Groups.Fetch
            };
        }
        if (Enabled(Groups.Fetch) != ((groups & Groups.Fetch) != 0))
            Interlocked.Increment(ref _fetchEpoch);
        if (Enabled(Groups.Acknowledgements) != ((groups & Groups.Acknowledgements) != 0))
            Interlocked.Increment(ref _acknowledgementEpoch);
        if (Enabled(Groups.Records) != ((groups & Groups.Records) != 0))
            Interlocked.Increment(ref _recordEpoch);
        if (Enabled(Groups.Poll) != ((groups & Groups.Poll) != 0))
        {
            Volatile.Write(ref _lastPoll, -1);
            Interlocked.Exchange(ref _pollIdleTicks, 0);
        }
        Volatile.Write(ref _enabled, (int)groups);
    }

    internal void Disable() => Volatile.Write(ref _enabled, 0);

    internal void PollStarted()
    {
        if (!Enabled(Groups.Poll)) return;
        var now = _clock();
        var previous = Interlocked.Exchange(ref _lastPoll, now);
        var idle = Interlocked.Exchange(ref _pollIdleTicks, 0);
        if (previous < 0) return;
        var elapsed = Math.Max(0, now - previous);
        _pollInterval.Record(elapsed);
        if (elapsed > 0)
            _pollIdleRatio.Record((long)(Math.Min(1d, idle / (double)elapsed) * 1_000_000));
    }

    internal void PollWaitCompleted(long started)
    {
        if (started >= 0 && Enabled(Groups.Poll))
            Interlocked.Add(ref _pollIdleTicks, Math.Max(0, _clock() - started));
    }

    internal long HeartbeatStarted()
    {
        if (!Enabled(Groups.Heartbeat)) return -1;
        var now = _clock();
        Volatile.Write(ref _lastHeartbeat, now);
        Interlocked.Increment(ref _heartbeats);
        return now;
    }

    internal void HeartbeatCompleted(long started)
    {
        if (started >= 0 && Enabled(Groups.Heartbeat))
            _heartbeatLatency.Record(Math.Max(0, _clock() - started));
    }

    internal void Rebalanced()
    {
        if (Enabled(Groups.Rebalance)) Interlocked.Increment(ref _rebalances);
    }

    internal void FetchStarted(int brokerId) => StartFetch(brokerId, 0);

    internal KafkaRequestWriteContext GetRequestWriteContext(int brokerId)
    {
        // Broker requests are serialized, but leases for different brokers may complete together.
        // Reuse the concurrent sample map and allocate the callback once per observed broker.
        var sample = _fetchSamples.GetOrAdd(brokerId, static _ => new FetchSample());
        return sample.WriteContext ??= new KafkaRequestWriteContext(default);
    }

    internal bool RequestWriteStarted(int brokerId) =>
        _fetchSamples.TryGetValue(brokerId, out var sample) && sample.WriteContext is { WriteStarted: true };

    internal void StartFetch(int brokerId, long acknowledgements, bool resetRecords = true)
    {
        if (!Enabled(Groups.Fetch | Groups.Acknowledgements)) return;
        var sample = _fetchSamples.GetOrAdd(brokerId, static _ => new FetchSample());
        if (resetRecords)
        {
            sample.Reset();
            sample.RecordEpoch = Enabled(Groups.Records) ? Volatile.Read(ref _recordEpoch) : -1;
        }
        sample.FetchEpoch = Volatile.Read(ref _fetchEpoch);
        sample.FetchAcknowledgementEpoch = Volatile.Read(ref _acknowledgementEpoch);
        sample.Started = Enabled(Groups.Fetch) ? _clock() : -1;
        sample.FetchAcknowledgements = acknowledgements;
    }

    internal void FetchFailed(int brokerId)
    {
        if (!Enabled(Groups.Fetch | Groups.Acknowledgements)) return;
        if (!_fetchSamples.TryGetValue(brokerId, out var sample)) return;
        if (sample.Started >= 0 && Enabled(Groups.Fetch) && sample.FetchEpoch == Volatile.Read(ref _fetchEpoch))
            RecordFetch(sample);
        if (sample.FetchAcknowledgementEpoch == Volatile.Read(ref _acknowledgementEpoch))
        {
            AcknowledgementsSent(sample.FetchAcknowledgements);
            AcknowledgementsFailed(sample.FetchAcknowledgements);
        }
    }

    internal void FetchCompleted(int brokerId, int throttleMs, bool acknowledgementsFailed = false)
    {
        if (!Enabled(Groups.Fetch | Groups.Acknowledgements)) return;
        if (!_fetchSamples.TryGetValue(brokerId, out var sample)) return;
        if (sample.Started >= 0 && Enabled(Groups.Fetch) && sample.FetchEpoch == Volatile.Read(ref _fetchEpoch))
        {
            RecordFetch(sample);
            _fetchLatency.Record(Math.Max(0, _clock() - sample.Started));
            if (throttleMs >= 0) _fetchThrottle.Record(throttleMs);
        }
        if (sample.FetchAcknowledgementEpoch == Volatile.Read(ref _acknowledgementEpoch))
        {
            AcknowledgementsSent(sample.FetchAcknowledgements);
            if (acknowledgementsFailed) AcknowledgementsFailed(sample.FetchAcknowledgements);
        }
    }

    private void RecordFetch(FetchSample sample)
    {
        Interlocked.Increment(ref _fetches);
        // Record totals only include requests observed under the record subscription.
        // Fetch-only intervals must not dilute their lifetime per-request averages.
        if (Enabled(Groups.Records) && sample.RecordEpoch == Volatile.Read(ref _recordEpoch))
            Interlocked.Increment(ref _recordFetches);
    }

    internal void AcknowledgementRequestStarted(int brokerId, long count)
    {
        if (!Enabled(Groups.Acknowledgements)) return;
        var sample = _fetchSamples.GetOrAdd(brokerId, static _ => new FetchSample());
        sample.StandaloneAcknowledgementEpoch = Volatile.Read(ref _acknowledgementEpoch);
        sample.StandaloneAcknowledgements = count;
    }

    internal void AcknowledgementRequestCompleted(int brokerId, bool failed)
    {
        if (!Enabled(Groups.Acknowledgements) || !_fetchSamples.TryGetValue(brokerId, out var sample)
            || sample.StandaloneAcknowledgementEpoch != Volatile.Read(ref _acknowledgementEpoch)) return;
        AcknowledgementsSent(sample.StandaloneAcknowledgements);
        if (failed) AcknowledgementsFailed(sample.StandaloneAcknowledgements);
    }

    internal FetchSample? GetFetchSample(int brokerId) =>
        Enabled(Groups.Records) && _fetchSamples.TryGetValue(brokerId, out var sample)
        && sample.RecordEpoch == Volatile.Read(ref _recordEpoch) ? sample : null;

    internal void Parsed(FetchSample sample, long bytes, int records)
    {
        if (!Enabled(Groups.Records) || sample.RecordEpoch != Volatile.Read(ref _recordEpoch)) return;
        sample.Bytes += bytes;
        sample.Records += records;
        Interlocked.Add(ref _bytes, bytes);
        Interlocked.Add(ref _records, records);
        AtomicMax(ref _maxFetchBytes, sample.Bytes);
        AtomicMax(ref _maxFetchRecords, sample.Records);
    }

    internal void AcknowledgementsSent(long count)
    {
        if (Enabled(Groups.Acknowledgements)) Interlocked.Add(ref _sentAcks, count);
    }

    internal void AcknowledgementsFailed(long count)
    {
        if (Enabled(Groups.Acknowledgements)) Interlocked.Add(ref _failedAcks, count);
    }

    internal void Collect(ClientTelemetrySubscription subscription, List<ClientTelemetryMetric> metrics)
    {
        var now = _clock();
        var poll = _pollInterval.Read();
        var idle = _pollIdleRatio.Read();
        var heartbeat = _heartbeatLatency.Read();
        var fetch = _fetchLatency.Read();
        var throttle = _fetchThrottle.Read();
        var recordFetches = Volatile.Read(ref _recordFetches);
        for (var index = 0; index < Names.Length; index++)
        {
            if (!Requested(Names[index], subscription.RequestedMetrics)) continue;
            var metric = (Metric)index;
            var kind = IsCounter(metric) ? ClientTelemetryMetricKind.Counter : ClientTelemetryMetricKind.Gauge;
            var total = GetTotal(metric);
            double value = metric switch
            {
                Metric.LastPollSecondsAgo => SecondsSince(Volatile.Read(ref _lastPoll), now),
                Metric.TimeBetweenPollAverage => Milliseconds(poll.Average),
                Metric.TimeBetweenPollMax => Milliseconds(poll.Max),
                Metric.PollIdleRatioAverage => idle.Average / 1_000_000d,
                Metric.HeartbeatResponseTimeMax => Milliseconds(heartbeat.Max),
                Metric.LastHeartbeatSecondsAgo => SecondsSince(Volatile.Read(ref _lastHeartbeat), now),
                Metric.FetchSizeAverage => recordFetches == 0 ? 0 : Volatile.Read(ref _bytes) / (double)recordFetches,
                Metric.FetchSizeMax => Volatile.Read(ref _maxFetchBytes),
                Metric.RecordsPerRequestAverage => recordFetches == 0 ? 0 : Volatile.Read(ref _records) / (double)recordFetches,
                Metric.RecordsPerRequestMax => Volatile.Read(ref _maxFetchRecords),
                Metric.FetchLatencyAverage => Milliseconds(fetch.Average),
                Metric.FetchLatencyMax => Milliseconds(fetch.Max),
                Metric.FetchThrottleTimeAverage => throttle.Average,
                Metric.FetchThrottleTimeMax => throttle.Max,
                _ => total
            };
            if (IsRate(metric))
            {
                var elapsed = (now - _previousTimes[index]) / (double)_frequency;
                value = elapsed > 0 ? Math.Max(0, total - _previousValues[index]) / elapsed : 0;
                if (metric == Metric.RebalanceRatePerHour) value *= 3600;
                _previousTimes[index] = now;
                _previousValues[index] = total;
            }
            else if (kind == ClientTelemetryMetricKind.Counter && subscription.DeltaTemporality)
            {
                value = Math.Max(0, total - _previousValues[index]);
                _previousValues[index] = total;
            }
            metrics.Add(new ClientTelemetryMetric(Names[index], kind, value, []));
        }
    }

    private long GetTotal(Metric metric) => metric switch
    {
        Metric.HeartbeatRate or Metric.HeartbeatTotal => Volatile.Read(ref _heartbeats),
        Metric.RebalanceTotal or Metric.RebalanceRatePerHour => Volatile.Read(ref _rebalances),
        Metric.BytesConsumedRate or Metric.BytesConsumedTotal => Volatile.Read(ref _bytes),
        Metric.RecordsConsumedRate or Metric.RecordsConsumedTotal => Volatile.Read(ref _records),
        Metric.AcknowledgementsSendRate or Metric.AcknowledgementsSendTotal => Volatile.Read(ref _sentAcks),
        Metric.AcknowledgementsErrorRate or Metric.AcknowledgementsErrorTotal => Volatile.Read(ref _failedAcks),
        Metric.FetchRate or Metric.FetchTotal => Volatile.Read(ref _fetches),
        _ => 0
    };

    private static bool IsCounter(Metric metric) => metric is
        Metric.HeartbeatTotal or Metric.RebalanceTotal or Metric.BytesConsumedTotal or Metric.RecordsConsumedTotal
        or Metric.AcknowledgementsSendTotal or Metric.AcknowledgementsErrorTotal or Metric.FetchTotal;

    private static bool IsRate(Metric metric) => metric is
        Metric.HeartbeatRate or Metric.RebalanceRatePerHour or Metric.BytesConsumedRate or Metric.RecordsConsumedRate
        or Metric.AcknowledgementsSendRate or Metric.AcknowledgementsErrorRate or Metric.FetchRate;

    private double Milliseconds(double ticks) => ticks * 1000d / _frequency;
    private double SecondsSince(long previous, long now) => previous < 0 ? -1 : Math.Max(0, now - previous) / (double)_frequency;
    private static bool Requested(string name, IReadOnlyList<string> prefixes)
    {
        for (var index = 0; index < prefixes.Count; index++)
            if (name.StartsWith(prefixes[index], StringComparison.Ordinal)) return true;
        return false;
    }

    private static void AtomicMax(ref long target, long value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref target, value, current);
            if (previous == current) return;
            current = previous;
        }
    }

    // Polling and acknowledgement submission are serialized per broker by the
    // share consumer. Reuse their request state without growing async state machines.
    internal sealed class FetchSample
    {
        internal KafkaRequestWriteContext? WriteContext;
        internal int RecordEpoch = -1;
        internal int FetchEpoch = -1;
        internal int FetchAcknowledgementEpoch = -1;
        internal int StandaloneAcknowledgementEpoch = -1;
        internal long Started = -1;
        internal long FetchAcknowledgements;
        internal long StandaloneAcknowledgements;
        internal long Bytes;
        internal long Records;
        // One prepared partition is active per broker sample. Retain partial accounting
        // here across preparation awaits without enlarging each poll's async state.
        internal long PendingBytes;
        internal int PendingRecords;
        internal void Reset()
        {
            Bytes = 0;
            Records = 0;
            ResetPending();
        }
        internal void ResetPending() { PendingBytes = 0; PendingRecords = 0; }
    }

    private sealed class Measurement
    {
        private long _count, _total, _max;
        internal void Record(long value)
        {
            Interlocked.Add(ref _total, value);
            AtomicMax(ref _max, value);
            Interlocked.Increment(ref _count);
        }
        internal (double Average, long Max) Read()
        {
            var count = Volatile.Read(ref _count);
            return (count == 0 ? 0 : Volatile.Read(ref _total) / (double)count, Volatile.Read(ref _max));
        }
    }
}
