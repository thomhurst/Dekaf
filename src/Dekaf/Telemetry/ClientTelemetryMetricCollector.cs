using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Dekaf.Telemetry;

internal enum ClientTelemetryClientRole
{
    Producer,
    Consumer
}

internal enum ClientTelemetryMetricKind
{
    Counter,
    Gauge
}

internal readonly record struct ClientTelemetryMetricAttribute(string Name, string Value);

internal sealed record ClientTelemetryMetric(
    string Name,
    ClientTelemetryMetricKind Kind,
    double Value,
    IReadOnlyList<ClientTelemetryMetricAttribute> Attributes);

internal sealed record ClientTelemetryMetricSnapshot(
    bool DeltaTemporality,
    IReadOnlyList<ClientTelemetryMetric> Metrics)
{
    private static readonly IReadOnlyList<ClientTelemetryMetric> EmptyMetrics = [];

    public static ClientTelemetryMetricSnapshot Empty(bool deltaTemporality) =>
        new(deltaTemporality, EmptyMetrics);
}

internal static class ClientTelemetryMetricNames
{
    public const string ProducerConnectionCreationTotal = "org.apache.kafka.producer.connection.creation.total";
    public const string ProducerNodeRequestLatencyAvg = "org.apache.kafka.producer.node.request.latency.avg";
    public const string ProducerNodeRequestLatencyMax = "org.apache.kafka.producer.node.request.latency.max";

    public const string ConsumerConnectionCreationTotal = "org.apache.kafka.consumer.connection.creation.total";
    public const string ConsumerNodeRequestLatencyAvg = "org.apache.kafka.consumer.node.request.latency.avg";
    public const string ConsumerNodeRequestLatencyMax = "org.apache.kafka.consumer.node.request.latency.max";
}

internal sealed class ClientTelemetryMetricCollector
{
    private static readonly IReadOnlyList<ClientTelemetryMetricAttribute> EmptyAttributes = [];

    private readonly ConcurrentDictionary<int, NodeRequestLatency> _nodeRequestLatencies = new();
    private readonly string _connectionCreationTotalName;
    private readonly string _nodeRequestLatencyAvgName;
    private readonly string _nodeRequestLatencyMaxName;

    private long _connectionCreationTotal;
    private long _connectionCreationDelta;

    public ClientTelemetryMetricCollector(ClientTelemetryClientRole role)
    {
        (_connectionCreationTotalName, _nodeRequestLatencyAvgName, _nodeRequestLatencyMaxName) = role switch
        {
            ClientTelemetryClientRole.Producer => (
                ClientTelemetryMetricNames.ProducerConnectionCreationTotal,
                ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg,
                ClientTelemetryMetricNames.ProducerNodeRequestLatencyMax),
            ClientTelemetryClientRole.Consumer => (
                ClientTelemetryMetricNames.ConsumerConnectionCreationTotal,
                ClientTelemetryMetricNames.ConsumerNodeRequestLatencyAvg,
                ClientTelemetryMetricNames.ConsumerNodeRequestLatencyMax),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported telemetry client role")
        };
    }

    public void RecordConnectionCreated()
    {
        Interlocked.Increment(ref _connectionCreationTotal);
        Interlocked.Increment(ref _connectionCreationDelta);
    }

    public void RecordRequestLatency(int brokerId, long startTimestamp)
    {
        if (brokerId < 0)
        {
            return;
        }

        var elapsedTimestampTicks = Math.Max(0, Stopwatch.GetTimestamp() - startTimestamp);
        RecordRequestLatencyTicks(brokerId, elapsedTimestampTicks);
    }

    internal void RecordRequestLatency(int brokerId, TimeSpan elapsed)
    {
        if (brokerId < 0)
        {
            return;
        }

        var elapsedTimestampTicks = Math.Max(0, (long)(elapsed.TotalSeconds * Stopwatch.Frequency));
        RecordRequestLatencyTicks(brokerId, elapsedTimestampTicks);
    }

    public ClientTelemetryMetricSnapshot Collect(ClientTelemetrySubscription subscription)
    {
        var requestedMetrics = subscription.RequestedMetrics;
        if (requestedMetrics.Count == 0)
        {
            return ClientTelemetryMetricSnapshot.Empty(subscription.DeltaTemporality);
        }

        var includeConnectionCreationTotal = IsRequested(_connectionCreationTotalName, requestedMetrics);
        var includeRequestLatencyAvg = IsRequested(_nodeRequestLatencyAvgName, requestedMetrics);
        var includeRequestLatencyMax = IsRequested(_nodeRequestLatencyMaxName, requestedMetrics);

        if (!includeConnectionCreationTotal &&
            !includeRequestLatencyAvg &&
            !includeRequestLatencyMax)
        {
            return ClientTelemetryMetricSnapshot.Empty(subscription.DeltaTemporality);
        }

        var capacity = (includeConnectionCreationTotal ? 1 : 0) +
            ((includeRequestLatencyAvg ? 1 : 0) + (includeRequestLatencyMax ? 1 : 0)) *
            Math.Max(1, _nodeRequestLatencies.Count);
        var metrics = new List<ClientTelemetryMetric>(capacity);

        if (includeConnectionCreationTotal)
        {
            var value = subscription.DeltaTemporality
                ? Interlocked.Exchange(ref _connectionCreationDelta, 0)
                : Volatile.Read(ref _connectionCreationTotal);

            if (value != 0)
            {
                metrics.Add(new ClientTelemetryMetric(
                    _connectionCreationTotalName,
                    ClientTelemetryMetricKind.Counter,
                    value,
                    EmptyAttributes));
            }
        }

        if (includeRequestLatencyAvg || includeRequestLatencyMax)
        {
            foreach (var (nodeId, latency) in _nodeRequestLatencies)
            {
                var snapshot = subscription.DeltaTemporality
                    ? latency.CollectDelta()
                    : latency.CollectCumulative();

                if (snapshot.Count == 0)
                {
                    continue;
                }

                var attributes = new[]
                {
                    new ClientTelemetryMetricAttribute("node_id", nodeId.ToString(CultureInfo.InvariantCulture))
                };

                if (includeRequestLatencyAvg)
                {
                    metrics.Add(new ClientTelemetryMetric(
                        _nodeRequestLatencyAvgName,
                        ClientTelemetryMetricKind.Gauge,
                        StopwatchTicksToMilliseconds(snapshot.TotalTimestampTicks) / snapshot.Count,
                        attributes));
                }

                if (includeRequestLatencyMax)
                {
                    metrics.Add(new ClientTelemetryMetric(
                        _nodeRequestLatencyMaxName,
                        ClientTelemetryMetricKind.Gauge,
                        StopwatchTicksToMilliseconds(snapshot.MaxTimestampTicks),
                        attributes));
                }
            }
        }

        return metrics.Count == 0
            ? ClientTelemetryMetricSnapshot.Empty(subscription.DeltaTemporality)
            : new ClientTelemetryMetricSnapshot(subscription.DeltaTemporality, metrics);
    }

    private void RecordRequestLatencyTicks(int brokerId, long elapsedTimestampTicks)
    {
        var latency = _nodeRequestLatencies.GetOrAdd(
            brokerId,
            static _ => new NodeRequestLatency());

        latency.Record(elapsedTimestampTicks);
    }

    private static bool IsRequested(string metricName, IReadOnlyList<string> requestedMetrics)
    {
        for (var i = 0; i < requestedMetrics.Count; i++)
        {
            var prefix = requestedMetrics[i];
            if (prefix.Length == 0 ||
                metricName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static double StopwatchTicksToMilliseconds(long stopwatchTicks) =>
        stopwatchTicks * 1000.0 / Stopwatch.Frequency;

    private readonly record struct LatencySnapshot(
        long Count,
        long TotalTimestampTicks,
        long MaxTimestampTicks);

    private sealed class NodeRequestLatency
    {
        private long _count;
        private long _totalTimestampTicks;
        private long _maxTimestampTicks;
        private long _deltaCount;
        private long _deltaTotalTimestampTicks;
        private long _deltaMaxTimestampTicks;

        public void Record(long elapsedTimestampTicks)
        {
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _totalTimestampTicks, elapsedTimestampTicks);
            AtomicMax(ref _maxTimestampTicks, elapsedTimestampTicks);

            Interlocked.Increment(ref _deltaCount);
            Interlocked.Add(ref _deltaTotalTimestampTicks, elapsedTimestampTicks);
            AtomicMax(ref _deltaMaxTimestampTicks, elapsedTimestampTicks);
        }

        public LatencySnapshot CollectCumulative() =>
            new(
                Volatile.Read(ref _count),
                Volatile.Read(ref _totalTimestampTicks),
                Volatile.Read(ref _maxTimestampTicks));

        public LatencySnapshot CollectDelta() =>
            new(
                Interlocked.Exchange(ref _deltaCount, 0),
                Interlocked.Exchange(ref _deltaTotalTimestampTicks, 0),
                Interlocked.Exchange(ref _deltaMaxTimestampTicks, 0));

        private static void AtomicMax(ref long target, long value)
        {
            var current = Volatile.Read(ref target);
            while (value > current)
            {
                var original = Interlocked.CompareExchange(ref target, value, current);
                if (original == current)
                {
                    return;
                }

                current = original;
            }
        }
    }
}
