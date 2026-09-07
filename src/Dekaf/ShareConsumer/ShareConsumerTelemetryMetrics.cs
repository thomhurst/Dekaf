using System.Diagnostics;
using Dekaf.Telemetry;

namespace Dekaf.ShareConsumer;

/// <summary>Request/batch aggregates for the client metrics in KIP-932.</summary>
internal sealed class ShareConsumerTelemetryMetrics
{
    internal const string Prefix = "org.apache.kafka.consumer.share.";
    private const string FetchPrefix = Prefix + "fetch.manager.";
    private const string CoordinatorPrefix = Prefix + "coordinator.";
    private static readonly IReadOnlyList<ClientTelemetryMetricAttribute> EmptyAttributes = [];
    internal static readonly string[] Names =
    [
        FetchPrefix + "bytes.consumed.total",
        FetchPrefix + "bytes.consumed.rate",
        FetchPrefix + "records.consumed.total",
        FetchPrefix + "records.consumed.rate",
        FetchPrefix + "acknowledgements.send.total",
        FetchPrefix + "acknowledgements.send.rate",
        FetchPrefix + "acknowledgements.error.total",
        FetchPrefix + "acknowledgements.error.rate",
        FetchPrefix + "fetch.total",
        FetchPrefix + "fetch.rate",
        CoordinatorPrefix + "heartbeat.total",
        CoordinatorPrefix + "heartbeat.rate",
        CoordinatorPrefix + "rebalance.total",
        CoordinatorPrefix + "rebalance.rate.per.hour",
        FetchPrefix + "fetch.size.avg",
        FetchPrefix + "fetch.size.max",
        FetchPrefix + "records.per.request.avg",
        FetchPrefix + "records.per.request.max",
        FetchPrefix + "fetch.latency.avg",
        FetchPrefix + "fetch.latency.max",
        FetchPrefix + "fetch.throttle.time.avg",
        FetchPrefix + "fetch.throttle.time.max",
        CoordinatorPrefix + "heartbeat.response.time.max",
        Prefix + "time.between.poll.avg",
        Prefix + "time.between.poll.max",
        Prefix + "poll.idle.ratio.avg",
        Prefix + "last.poll.seconds.ago",
        CoordinatorPrefix + "last.heartbeat.seconds.ago",
    ];

    private readonly Func<long> _timestamp;
    private readonly long _timestampFrequency;
    private long _started = -1;
    private readonly Counter _bytes = new();
    private readonly Counter _records = new();
    private readonly Counter _acknowledgements = new();
    private readonly Counter _acknowledgementErrors = new();
    private readonly Counter _fetches = new();
    private readonly Counter _heartbeats = new();
    private readonly Counter _rebalances = new();
    private readonly Summary _fetchSize = new();
    private readonly Summary _recordsPerRequest = new();
    private readonly Summary _fetchLatency = new();
    private readonly Summary _throttle = new();
    private readonly Summary _heartbeatLatency = new();
    private readonly Summary _pollInterval = new();
    private readonly Summary _pollIdleRatio = new();
    private long _lastPoll = -1;
    private long _lastHeartbeat = -1;
    private int _enabled;
    private string? _groupId;
    private string? _rack;
    private string? _memberId;

    internal void ConfigureIdentity(string groupId, string? rack)
    {
        _groupId = groupId;
        _rack = rack;
    }

    internal void SetMemberId(string? memberId) => Volatile.Write(ref _memberId, memberId);

    internal IReadOnlyList<ClientTelemetryMetricAttribute> ResourceAttributes()
    {
        var attributes = new List<ClientTelemetryMetricAttribute>(3);
        if (_groupId is not null) attributes.Add(new("group_id", _groupId));
        if (_rack is not null) attributes.Add(new("client_rack", _rack));
        if (Volatile.Read(ref _memberId) is { } memberId) attributes.Add(new("group_member_id", memberId));
        return attributes;
    }

    internal ShareConsumerTelemetryMetrics(Func<long>? timestamp = null, long timestampFrequency = 0)
    {
        _timestamp = timestamp ?? Stopwatch.GetTimestamp;
        _timestampFrequency = timestampFrequency > 0 ? timestampFrequency : Stopwatch.Frequency;
    }

    internal bool Enabled => Volatile.Read(ref _enabled) != 0;
    internal long Timestamp => _timestamp();
    internal long ParsedBytes => Volatile.Read(ref _bytes.Total);
    internal long ParsedRecords => Volatile.Read(ref _records.Total);

    internal void Configure(IReadOnlyList<string> requestedMetrics)
    {
        var enabled = false;
        for (var i = 0; i < requestedMetrics.Count; i++)
        {
            var requested = requestedMetrics[i];
            for (var j = 0; j < Names.Length; j++)
            {
                if (!Names[j].StartsWith(requested, StringComparison.Ordinal)) continue;
                enabled = true;
                break;
            }
            if (enabled) break;
        }
        if (enabled) Interlocked.CompareExchange(ref _started, Timestamp, -1);
        Volatile.Write(ref _enabled, enabled ? 1 : 0);
    }

    internal long BeginPoll()
    {
        if (!Enabled) return -1;
        var now = Timestamp;
        var previous = Interlocked.Exchange(ref _lastPoll, now);
        if (previous >= 0) _pollInterval.Record(ElapsedMilliseconds(previous, now));
        return now;
    }

    internal void EndPoll(long started, long deliveryTicks)
    {
        if (started < 0) return;
        var duration = Math.Max(0, Timestamp - started);
        if (duration > 0) _pollIdleRatio.Record(Math.Max(0, Math.Min(1, 1 - deliveryTicks / (double)duration)));
    }

    internal long BeginHeartbeat()
    {
        if (!Enabled) return -1;
        var now = Timestamp;
        Volatile.Write(ref _lastHeartbeat, now);
        _heartbeats.Add(1);
        return now;
    }

    internal void EndHeartbeat(long started)
    {
        if (started >= 0) _heartbeatLatency.Record(ElapsedMilliseconds(started, Timestamp));
    }

    internal void RecordRebalance()
    {
        if (Enabled) _rebalances.Add(1);
    }

    internal long BeginFetch()
    {
        if (!Enabled) return -1;
        _fetches.Add(1);
        return Timestamp;
    }

    internal void EndFetch(long started, int throttleMs)
    {
        if (started < 0) return;
        _fetchLatency.Record(ElapsedMilliseconds(started, Timestamp));
        if (throttleMs >= 0) _throttle.Record(throttleMs);
    }

    internal void RecordParsed(long bytes, int records)
    {
        _bytes.Add(bytes);
        _records.Add(records);
    }

    internal void RecordFetchSize(long bytesBefore, long recordsBefore)
    {
        _fetchSize.Record(ParsedBytes - bytesBefore);
        _recordsPerRequest.Record(ParsedRecords - recordsBefore);
    }

    internal FetchResponseMeasurement MeasureFetchResponse() => new(this);

    internal readonly struct FetchResponseMeasurement : IDisposable
    {
        private readonly ShareConsumerTelemetryMetrics? _metrics;
        private readonly long _bytes;
        private readonly long _records;

        internal FetchResponseMeasurement(ShareConsumerTelemetryMetrics metrics)
        {
            _metrics = metrics.Enabled ? metrics : null;
            _bytes = _metrics?.ParsedBytes ?? 0;
            _records = _metrics?.ParsedRecords ?? 0;
        }

        public void Dispose() => _metrics?.RecordFetchSize(_bytes, _records);
    }

    internal void RecordAcknowledgements(long sent, long errors)
    {
        if (!Enabled) return;
        _acknowledgements.Add(sent);
        _acknowledgementErrors.Add(errors);
    }

    internal void Collect(ClientTelemetrySubscription subscription, List<ClientTelemetryMetric> metrics)
    {
        if (!Enabled) return;
        var seconds = (Timestamp - Volatile.Read(ref _started)) / (double)_timestampFrequency;
        AddCounter(_bytes, FetchPrefix + "bytes.consumed.total", FetchPrefix + "bytes.consumed.rate", 1);
        AddCounter(_records, FetchPrefix + "records.consumed.total", FetchPrefix + "records.consumed.rate", 1);
        AddCounter(_acknowledgements, FetchPrefix + "acknowledgements.send.total", FetchPrefix + "acknowledgements.send.rate", 1);
        AddCounter(_acknowledgementErrors, FetchPrefix + "acknowledgements.error.total", FetchPrefix + "acknowledgements.error.rate", 1);
        AddCounter(_fetches, FetchPrefix + "fetch.total", FetchPrefix + "fetch.rate", 1);
        AddCounter(_heartbeats, CoordinatorPrefix + "heartbeat.total", CoordinatorPrefix + "heartbeat.rate", 1);
        AddCounter(_rebalances, CoordinatorPrefix + "rebalance.total", CoordinatorPrefix + "rebalance.rate.per.hour", 3600);
        AddSummary(_fetchSize, FetchPrefix + "fetch.size.avg", FetchPrefix + "fetch.size.max");
        AddSummary(_recordsPerRequest, FetchPrefix + "records.per.request.avg", FetchPrefix + "records.per.request.max");
        AddSummary(_fetchLatency, FetchPrefix + "fetch.latency.avg", FetchPrefix + "fetch.latency.max");
        AddSummary(_throttle, FetchPrefix + "fetch.throttle.time.avg", FetchPrefix + "fetch.throttle.time.max");
        AddSummary(_heartbeatLatency, null, CoordinatorPrefix + "heartbeat.response.time.max");
        AddSummary(_pollInterval, Prefix + "time.between.poll.avg", Prefix + "time.between.poll.max");
        AddSummary(_pollIdleRatio, Prefix + "poll.idle.ratio.avg", null);
        AddAge(Volatile.Read(ref _lastPoll), Prefix + "last.poll.seconds.ago");
        AddAge(Volatile.Read(ref _lastHeartbeat), CoordinatorPrefix + "last.heartbeat.seconds.ago");

        void AddCounter(Counter counter, string totalName, string rateName, double scale)
        {
            var total = Volatile.Read(ref counter.Total);
            if (Requested(totalName))
            {
                var previous = Interlocked.Exchange(ref counter.Previous, total);
                var value = subscription.DeltaTemporality ? total - previous : total;
                metrics.Add(new(totalName, ClientTelemetryMetricKind.Counter, value, EmptyAttributes));
            }
            AddGauge(rateName, seconds > 0 ? total * scale / seconds : 0);
        }
        void AddSummary(Summary summary, string? averageName, string? maxName)
        {
            var count = Volatile.Read(ref summary.Count);
            if (count == 0) return;
            if (averageName is not null) AddGauge(averageName, Volatile.Read(ref summary.Total) / count);
            if (maxName is not null) AddGauge(maxName, Volatile.Read(ref summary.Max));
        }
        void AddAge(long timestamp, string name)
        {
            if (timestamp >= 0) AddGauge(name, (Timestamp - timestamp) / (double)_timestampFrequency);
        }
        void AddGauge(string name, double value)
        {
            if (Requested(name)) metrics.Add(new(name, ClientTelemetryMetricKind.Gauge, value, EmptyAttributes));
        }
        bool Requested(string name)
        {
            for (var i = 0; i < subscription.RequestedMetrics.Count; i++)
                if (name.StartsWith(subscription.RequestedMetrics[i], StringComparison.Ordinal)) return true;
            return false;
        }
    }

    private double ElapsedMilliseconds(long start, long end) => (end - start) * 1000.0 / _timestampFrequency;

    private sealed class Counter
    {
        internal long Total;
        internal long Previous;
        internal void Add(long value) => Interlocked.Add(ref Total, value);
    }

    private sealed class Summary
    {
        internal long Count;
        internal double Total;
        internal double Max;
        internal void Record(double value)
        {
            double observed;
            double updated;
            do
            {
                observed = Volatile.Read(ref Total);
                updated = observed + value;
            } while (Interlocked.CompareExchange(ref Total, updated, observed) != observed);
            observed = Volatile.Read(ref Max);
            while (value > observed)
            {
                var original = Interlocked.CompareExchange(ref Max, value, observed);
                if (original == observed) break;
                observed = original;
            }
            Interlocked.Increment(ref Count);
        }
    }
}
