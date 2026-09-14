using System.Diagnostics;
using Dekaf.Protocol;

namespace Dekaf.Telemetry;

/// <summary>Batch, request and membership measurements for the remaining KIP-714 metrics.</summary>
internal sealed class StandardClientTelemetryMetrics
{
    internal const string ProducerPrefix = "org.apache.kafka.producer.";
    internal const string ConsumerPrefix = "org.apache.kafka.consumer.";
    internal const string QueuePrefix = ProducerPrefix + "record.queue.time.";
    internal const string CommitPrefix = ConsumerPrefix + "coordinator.commit.latency.";
    internal const string FetchPrefix = ConsumerPrefix + "fetch.manager.fetch.latency.";
    internal const string RebalancePrefix = ConsumerPrefix + "coordinator.rebalance.latency.";
    internal const string AssignedPartitions = ConsumerPrefix + "coordinator.assigned.partitions";
    internal const string PollIdleRatio = ConsumerPrefix + "poll.idle.ratio.avg";

    private readonly bool _producer;
    private readonly Func<long> _clock;
    private readonly long _frequency;
    private readonly string _connectionRateName;
    private Measurement? _queue, _commit, _fetch, _rebalance;
    private bool _connectionRateRequested, _rebalanceTotalRequested;
    private long _connectionPrevious, _connectionPreviousTime;
    private long _rebalanceTicks, _rebalancePreviousTicks;
    private int _pollEnabled;
    private long _pollPreviousTicks, _pollPreviousTime;
    private int _pollStartingWaitSequence;
    private bool _pollHasSample;

    // The consumer serializes foreground operations. Only the collection thread reads
    // this sequence-protected state; recording never spins or acquires a lock.
    private int _waitDepth, _waitSequence;
    private long _waitStarted = -1, _waitTotal;

    internal Func<int>? AssignedPartitionCountProvider { get; set; }

    internal StandardClientTelemetryMetrics(bool producer, Func<long>? clock = null, long frequency = 0)
    {
        _producer = producer;
        _clock = clock ?? Stopwatch.GetTimestamp;
        _frequency = frequency > 0 ? frequency : Stopwatch.Frequency;
        _connectionRateName = producer
            ? ProducerPrefix + "connection.creation.rate"
            : ConsumerPrefix + "connection.creation.rate";
    }

    internal void Subscribe(IReadOnlyList<string> prefixes, long connections)
    {
        var now = _clock();
        var connectionRate = Requested(_connectionRateName, prefixes);
        if (connectionRate && !_connectionRateRequested)
        {
            _connectionPrevious = connections;
            _connectionPreviousTime = now;
        }
        _connectionRateRequested = connectionRate;
        if (_producer)
        {
            SubscribeMeasurement(ref _queue, QueuePrefix, prefixes, now);
            return;
        }

        SubscribeMeasurement(ref _commit, CommitPrefix, prefixes, now);
        SubscribeMeasurement(ref _fetch, FetchPrefix, prefixes, now);
        SubscribeMeasurement(ref _rebalance, RebalancePrefix, prefixes, now, includeTotal: true);
        var rebalanceTotal = Requested(RebalancePrefix + "total", prefixes);
        if (rebalanceTotal && !_rebalanceTotalRequested)
            _rebalancePreviousTicks = Volatile.Read(ref _rebalanceTicks);
        _rebalanceTotalRequested = rebalanceTotal;

        var poll = Requested(PollIdleRatio, prefixes);
        if (poll && Volatile.Read(ref _pollEnabled) == 0)
        {
            _pollPreviousTicks = ReadWaitTicks(out _pollPreviousTime, out _pollStartingWaitSequence, out _pollHasSample);
        }
        Volatile.Write(ref _pollEnabled, poll ? 1 : 0);
    }

    internal void Disable() => Subscribe([], 0);

    private static void SubscribeMeasurement(ref Measurement? measurement, string prefix,
        IReadOnlyList<string> prefixes, long now, bool includeTotal = false)
    {
        var requested = Requested(prefix + "avg", prefixes) || Requested(prefix + "max", prefixes)
            || (includeTotal && Requested(prefix + "total", prefixes));
        if (!requested)
            Volatile.Write(ref measurement, null);
        else if (Volatile.Read(ref measurement) is null)
            Volatile.Write(ref measurement, new Measurement(now));
    }

    internal void RecordRequest(ApiKey apiKey, long started, long elapsed)
    {
        var measurement = GetRequestMeasurement(apiKey);
        if (measurement is not null && started >= measurement.Started)
            measurement.Record(elapsed);
    }

    internal void RecordRequest(ApiKey apiKey, long started)
    {
        var measurement = GetRequestMeasurement(apiKey);
        if (measurement is not null && started >= measurement.Started)
            measurement.Record(Math.Max(0, _clock() - started));
    }

    private Measurement? GetRequestMeasurement(ApiKey apiKey) => apiKey switch
    {
        ApiKey.Fetch => Volatile.Read(ref _fetch),
        ApiKey.OffsetCommit => Volatile.Read(ref _commit),
        _ => null
    };

    internal QueueTimeBatch BeginQueueTimeBatch() => new(this);

    /// <summary>Accumulates request-local samples; abandoned builds publish nothing.</summary>
    internal struct QueueTimeBatch
    {
        private readonly Measurement? _measurement;
        private long _count, _total, _max;

        internal QueueTimeBatch(StandardClientTelemetryMetrics metrics)
            => _measurement = Volatile.Read(ref metrics._queue);

        internal void Record(long created, long drained)
        {
            if (_measurement is null || created < _measurement.Started) return;
            var ticks = Math.Max(0, drained - created);
            _total += ticks;
            _max = Math.Max(_max, ticks);
            _count++;
        }

        internal readonly void Complete()
        {
            if (_count != 0) _measurement!.Record(_count, _total, _max);
        }
    }

    internal long RebalanceStarted() => Volatile.Read(ref _rebalance) is null ? -1 : _clock();

    internal void RebalanceCompleted(long started)
    {
        var measurement = Volatile.Read(ref _rebalance);
        if (measurement is null || started < measurement.Started)
            return;
        var elapsed = Math.Max(0, _clock() - started);
        measurement.Record(elapsed);
        Interlocked.Add(ref _rebalanceTicks, elapsed);
    }

    internal void BeginPollWait()
    {
        if (_waitDepth++ != 0 || Volatile.Read(ref _pollEnabled) == 0)
            return;
        Interlocked.Increment(ref _waitSequence);
        _waitStarted = _clock();
        Interlocked.Increment(ref _waitSequence);
    }

    internal void EndPollWait()
    {
        if (--_waitDepth != 0 || _waitStarted < 0)
            return;
        Interlocked.Increment(ref _waitSequence);
        _waitTotal += Math.Max(0, _clock() - _waitStarted);
        _waitStarted = -1;
        Interlocked.Increment(ref _waitSequence);
    }

    private long ReadWaitTicks(out long now, out int version, out bool active)
    {
        while (true)
        {
            var sequence = Volatile.Read(ref _waitSequence);
            if ((sequence & 1) != 0) continue;
            var total = Volatile.Read(ref _waitTotal);
            var started = Volatile.Read(ref _waitStarted);
            now = _clock();
            Interlocked.MemoryBarrier();
            if (sequence == Volatile.Read(ref _waitSequence))
            {
                version = sequence;
                active = started >= 0;
                return total + (started >= 0 ? Math.Max(0, now - started) : 0);
            }
        }
    }

    internal void Collect(ClientTelemetrySubscription subscription, List<ClientTelemetryMetric> metrics, long connections)
    {
        var prefixes = subscription.RequestedMetrics;
        var now = _clock();
        if (_connectionRateRequested && Requested(_connectionRateName, prefixes) && now > _connectionPreviousTime)
        {
            Add(metrics, _connectionRateName,
                (connections - _connectionPrevious) * (double)_frequency / (now - _connectionPreviousTime), ClientTelemetryMetricUnit.PerSecond);
            _connectionPrevious = connections;
            _connectionPreviousTime = now;
        }
        if (_producer)
        {
            CollectMeasurement(_queue, QueuePrefix, prefixes, metrics);
            return;
        }

        CollectMeasurement(_commit, CommitPrefix, prefixes, metrics);
        CollectMeasurement(_fetch, FetchPrefix, prefixes, metrics);
        CollectMeasurement(_rebalance, RebalancePrefix, prefixes, metrics);
        if (_rebalanceTotalRequested && Requested(RebalancePrefix + "total", prefixes))
        {
            var total = Volatile.Read(ref _rebalanceTicks);
            var ticks = subscription.DeltaTemporality ? total - _rebalancePreviousTicks : total;
            _rebalancePreviousTicks = total;
            Add(metrics, RebalancePrefix + "total", Milliseconds(ticks), ClientTelemetryMetricUnit.Milliseconds, ClientTelemetryMetricKind.Counter);
        }
        if (Requested(AssignedPartitions, prefixes) && AssignedPartitionCountProvider is { } assignment)
            Add(metrics, AssignedPartitions, assignment(), ClientTelemetryMetricUnit.Dimensionless);

        if (Volatile.Read(ref _pollEnabled) != 0 && Requested(PollIdleRatio, prefixes))
        {
            var idle = ReadWaitTicks(out var pollTime, out var version, out _);
            if (pollTime <= _pollPreviousTime) return;
            _pollHasSample |= version != _pollStartingWaitSequence;
            if (_pollHasSample)
                Add(metrics, PollIdleRatio, Math.Max(0, Math.Min(1, (idle - _pollPreviousTicks) / (double)(pollTime - _pollPreviousTime))), ClientTelemetryMetricUnit.Dimensionless);
            _pollPreviousTicks = idle;
            _pollPreviousTime = pollTime;
        }
    }

    private void CollectMeasurement(Measurement? measurement, string prefix, IReadOnlyList<string> prefixes,
        List<ClientTelemetryMetric> metrics)
    {
        if (measurement is null) return;
        measurement.ReadSnapshot(out var count, out var total, out var max);
        if (count == 0) return;
        if (Requested(prefix + "avg", prefixes))
            Add(metrics, prefix + "avg", Milliseconds(total) / count, ClientTelemetryMetricUnit.Milliseconds);
        if (Requested(prefix + "max", prefixes))
            Add(metrics, prefix + "max", Milliseconds(max), ClientTelemetryMetricUnit.Milliseconds);
    }

    private double Milliseconds(long ticks) => ticks * (1000d / _frequency);

    private static void Add(List<ClientTelemetryMetric> metrics, string name, double value, ClientTelemetryMetricUnit unit,
        ClientTelemetryMetricKind kind = ClientTelemetryMetricKind.Gauge)
        => metrics.Add(new ClientTelemetryMetric(name, kind, value, []) { UnitCode = unit });

    private static bool Requested(string name, IReadOnlyList<string> prefixes)
    {
        for (var index = 0; index < prefixes.Count; index++)
            if (name.StartsWith(prefixes[index], StringComparison.Ordinal)) return true;
        return false;
    }

    private sealed class Measurement(long started)
    {
        internal readonly long Started = started;
        private long _startedCount, _count, _total, _max;
        // Collection is serialized by ClientTelemetryManager. Keep its last
        // coherent snapshot if overlapping writers prevent a fresh bounded read.
        private long _snapshotCount, _snapshotTotal, _snapshotMax;

        internal void Record(long ticks)
            => Record(1, ticks, ticks);

        internal void Record(long count, long total, long max)
        {
            Interlocked.Add(ref _startedCount, count);
            Interlocked.Add(ref _total, total);
            var maximum = Volatile.Read(ref _max);
            while (max > maximum)
            {
                var previous = Interlocked.CompareExchange(ref _max, max, maximum);
                if (previous == maximum) break;
                maximum = previous;
            }
            Interlocked.Add(ref _count, count);
        }

        internal void ReadSnapshot(out long count, out long total, out long max)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var completed = Volatile.Read(ref _count);
                var observedTotal = Volatile.Read(ref _total);
                var observedMax = Volatile.Read(ref _max);
                Interlocked.MemoryBarrier();
                // Every recording adds the same positive weight before and after
                // its field updates. Equality rules out both unfinished writers
                // and any recording that started while these fields were read.
                if (completed != Volatile.Read(ref _startedCount)) continue;
                _snapshotCount = completed;
                _snapshotTotal = observedTotal;
                _snapshotMax = observedMax;
                break;
            }
            count = _snapshotCount;
            total = _snapshotTotal;
            max = _snapshotMax;
        }
    }
}
