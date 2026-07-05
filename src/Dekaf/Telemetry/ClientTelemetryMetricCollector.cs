using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Dekaf.Telemetry;

internal enum ClientTelemetryClientRole
{
    Producer,
    Consumer,
    Admin
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
    private readonly ConcurrentDictionary<string, ApplicationTelemetryMetric> _applicationMetrics =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, double> _applicationCounterPreviousValues =
        new(StringComparer.Ordinal);
    private readonly string? _connectionCreationTotalName;
    private readonly string? _nodeRequestLatencyAvgName;
    private readonly string? _nodeRequestLatencyMaxName;

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
            ClientTelemetryClientRole.Admin => (null, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported telemetry client role")
        };
    }

    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        CompatibilityThrowHelpers.ThrowIfNull(metric);
        _applicationMetrics[metric.Name] = metric;
        _applicationCounterPreviousValues.TryRemove(metric.Name, out _);
    }

    public void RegisterMetricsForSubscription(IEnumerable<ApplicationTelemetryMetric> metrics)
    {
        CompatibilityThrowHelpers.ThrowIfNull(metrics);

        foreach (var metric in metrics)
        {
            RegisterMetricForSubscription(metric);
        }
    }

    public void UnregisterMetricFromSubscription(string name)
    {
        CompatibilityThrowHelpers.ThrowIfNullOrWhiteSpace(name);
        _applicationMetrics.TryRemove(name, out _);
        _applicationCounterPreviousValues.TryRemove(name, out _);
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

        var includeConnectionCreationTotal = _connectionCreationTotalName is not null &&
            IsRequested(_connectionCreationTotalName, requestedMetrics);
        var includeRequestLatencyAvg = _nodeRequestLatencyAvgName is not null &&
            IsRequested(_nodeRequestLatencyAvgName, requestedMetrics);
        var includeRequestLatencyMax = _nodeRequestLatencyMaxName is not null &&
            IsRequested(_nodeRequestLatencyMaxName, requestedMetrics);

        var capacity = (includeConnectionCreationTotal ? 1 : 0) +
            ((includeRequestLatencyAvg ? 1 : 0) + (includeRequestLatencyMax ? 1 : 0)) *
            Math.Max(1, _nodeRequestLatencies.Count) +
            _applicationMetrics.Count;
        var metrics = new List<ClientTelemetryMetric>(capacity);

        if (includeConnectionCreationTotal)
        {
            var value = subscription.DeltaTemporality
                ? Interlocked.Exchange(ref _connectionCreationDelta, 0)
                : Volatile.Read(ref _connectionCreationTotal);

            if (value != 0)
            {
                metrics.Add(new ClientTelemetryMetric(
                    _connectionCreationTotalName!,
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
                        _nodeRequestLatencyAvgName!,
                        ClientTelemetryMetricKind.Gauge,
                        StopwatchTicksToMilliseconds(snapshot.TotalTimestampTicks) / snapshot.Count,
                        attributes));
                }

                if (includeRequestLatencyMax)
                {
                    metrics.Add(new ClientTelemetryMetric(
                        _nodeRequestLatencyMaxName!,
                        ClientTelemetryMetricKind.Gauge,
                        StopwatchTicksToMilliseconds(snapshot.MaxTimestampTicks),
                        attributes));
                }
            }
        }

        AddApplicationMetrics(subscription, requestedMetrics, metrics);

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

    private void AddApplicationMetrics(
        ClientTelemetrySubscription subscription,
        IReadOnlyList<string> requestedMetrics,
        List<ClientTelemetryMetric> metrics)
    {
        foreach (var (_, metric) in _applicationMetrics)
        {
            if (!IsRequested(metric.Name, requestedMetrics))
            {
                continue;
            }

            double value;
            try
            {
                value = metric.Observe();
            }
            catch
            {
                continue;
            }

            if (!CompatibilityBcl.IsFinite(value))
            {
                continue;
            }

            metrics.Add(new ClientTelemetryMetric(
                metric.Name,
                ToClientMetricKind(metric.Kind),
                ToClientMetricValue(metric, value, subscription.DeltaTemporality),
                ToClientMetricAttributes(metric.Attributes)));
        }
    }

    private double ToClientMetricValue(
        ApplicationTelemetryMetric metric,
        double value,
        bool deltaTemporality) =>
        metric.Kind == ApplicationTelemetryMetricKind.Counter && deltaTemporality
            ? GetApplicationCounterDelta(metric.Name, value)
            : value;

    private double GetApplicationCounterDelta(string name, double value)
    {
        while (true)
        {
            if (!_applicationCounterPreviousValues.TryGetValue(name, out var previous))
            {
                if (_applicationCounterPreviousValues.TryAdd(name, value))
                {
                    return value;
                }

                continue;
            }

            if (_applicationCounterPreviousValues.TryUpdate(name, value, previous))
            {
                return value >= previous ? value - previous : value;
            }
        }
    }

    private static ClientTelemetryMetricKind ToClientMetricKind(ApplicationTelemetryMetricKind kind) =>
        kind switch
        {
            ApplicationTelemetryMetricKind.Counter => ClientTelemetryMetricKind.Counter,
            ApplicationTelemetryMetricKind.Gauge => ClientTelemetryMetricKind.Gauge,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported application metric kind")
        };

    private static IReadOnlyList<ClientTelemetryMetricAttribute> ToClientMetricAttributes(
        IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.Count == 0)
        {
            return EmptyAttributes;
        }

        var result = new ClientTelemetryMetricAttribute[attributes.Count];
        var i = 0;
        foreach (var (name, value) in attributes)
        {
            result[i++] = new ClientTelemetryMetricAttribute(name, value);
        }

        return result;
    }

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
