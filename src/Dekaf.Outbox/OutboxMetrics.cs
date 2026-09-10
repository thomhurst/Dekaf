using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace Dekaf.Outbox;

internal static class OutboxMetrics
{
    private static readonly ConcurrentDictionary<OutboxMetricState, byte> States = new();

    internal static readonly Counter<long> Acknowledged = OutboxDiagnostics.Meter.CreateCounter<long>(
        "dekaf.outbox.publish.acknowledged", "{message}", "Acknowledged contiguous-prefix messages, including retries.");
    internal static readonly Counter<long> Failures = OutboxDiagnostics.Meter.CreateCounter<long>(
        "dekaf.outbox.publish.failures", "{attempt}", "Failed batch publish attempts, excluding cooperative shutdown.");
    internal static readonly Counter<long> LeaseExpirations = OutboxDiagnostics.Meter.CreateCounter<long>(
        "dekaf.outbox.lease.expirations", "{event}", "Observed expirations of an owned lease set.");
    internal static readonly Histogram<double> PublishDuration = OutboxDiagnostics.Meter.CreateHistogram<double>(
        "dekaf.outbox.publish.duration", "s", "Batch publisher call duration, including failed and cancelled calls.");
    internal static readonly Histogram<double> CycleDuration = OutboxDiagnostics.Meter.CreateHistogram<double>(
        "dekaf.outbox.cycle.duration", "s", "Relay cycle duration, excluding idle/error backoff.");

    private static readonly ObservableGauge<long> OwnedBuckets = OutboxDiagnostics.Meter.CreateObservableGauge(
        "dekaf.outbox.owned_buckets", static () => Observe(static value => (long?)value.OwnedBuckets), "{bucket}");
    private static readonly ObservableGauge<long> PendingMessages = OutboxDiagnostics.Meter.CreateObservableGauge(
        "dekaf.outbox.pending.messages", static () => Observe(static value => value.PendingCount), "{message}");
    private static readonly ObservableGauge<double> OldestAge = OutboxDiagnostics.Meter.CreateObservableGauge(
        "dekaf.outbox.pending.oldest_age", static () => Observe(static value => value.PendingCount == 0 ? 0 : value.OldestAge), "s");
    private static readonly ObservableGauge<long> PendingAvailable = OutboxDiagnostics.Meter.CreateObservableGauge(
        "dekaf.outbox.pending.available", static () => Observe(static value => (long?)(value.PendingCount.HasValue ? 1 : 0)), "1");

    internal static bool PublishEnabled => Acknowledged.Enabled || Failures.Enabled || PublishDuration.Enabled;
    internal static bool PendingEnabled => PendingMessages.Enabled || OldestAge.Enabled || PendingAvailable.Enabled;

    internal static void Register(OutboxMetricState state) => States.TryAdd(state, 0);
    internal static void Unregister(OutboxMetricState state) => States.TryRemove(state, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Record(Counter<long> counter, OutboxMetricState state, long count)
    {
        if (count != 0 && counter.Enabled)
            Emit(counter, state, count);
    }

    private static void Emit(Counter<long> counter, OutboxMetricState state, long count)
    {
        try
        {
            counter.Add(count, state.Tags.AsSpan());
        }
        catch
        {
            // Listener code runs inline. A telemetry failure must not retry acknowledged rows.
        }
    }

    internal static void RecordDuration(Histogram<double> histogram, OutboxMetricState state, long started)
    {
        try
        {
            RecordDuration(histogram, state, started, state.TimeProvider.GetTimestamp());
        }
        catch
        {
            // A custom clock is telemetry code too; preserve listener isolation.
        }
    }

    internal static void RecordDuration(Histogram<double> histogram, OutboxMetricState state, long started, long finished)
    {
        try
        {
            histogram.Record(state.TimeProvider.GetElapsedTime(started, finished).TotalSeconds, state.Tags.AsSpan());
        }
        catch
        {
            // Match Dekaf's listener isolation: telemetry must not change delivery outcomes.
        }
    }

    private static IEnumerable<Measurement<T>> Observe<T>(Func<Observation, T?> select) where T : struct
    {
        // Collection is a cold exporter path. Group replicas by their configured logical
        // outbox name: ownership sums; whole-store backlog/age take max, avoiding duplicate counts.
        var groups = new Dictionary<string, Observation>(StringComparer.Ordinal);
        foreach (var entry in States)
        {
            var state = entry.Key;
            groups.TryGetValue(state.Name, out var value);
            value.OwnedBuckets += Volatile.Read(ref state.OwnedBuckets);
            var pending = Volatile.Read(ref state.Pending);
            if (pending is not null)
            {
                value.PendingCount = Math.Max(value.PendingCount ?? 0, pending.PendingCount);
                if (pending.PendingCount > 0 && pending.OldestCreatedAtUtc is { } oldest)
                {
                    var age = Math.Max(0, (state.TimeProvider.GetUtcNow() - oldest).TotalSeconds);
                    value.OldestAge = Math.Max(value.OldestAge ?? 0, age);
                }
            }
            groups[state.Name] = value;
        }
        foreach (var group in groups)
        {
            if (select(group.Value) is { } value)
                yield return new Measurement<T>(value, new KeyValuePair<string, object?>("outbox.name", group.Key));
        }
    }

    private struct Observation
    {
        public long OwnedBuckets;
        public long? PendingCount;
        public double? OldestAge;
    }
}

internal sealed class OutboxMetricState(string name, TimeProvider timeProvider) : IDisposable
{
    internal string Name { get; } = name;
    internal TimeProvider TimeProvider { get; } = timeProvider;
    internal KeyValuePair<string, object?>[] Tags { get; } = [new("outbox.name", name)];
    internal int OwnedBuckets;
    internal OutboxPendingMetrics? Pending;

    public void Dispose() => OutboxMetrics.Unregister(this);
}
