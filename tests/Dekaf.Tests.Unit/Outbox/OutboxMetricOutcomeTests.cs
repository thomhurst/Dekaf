using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Reflection;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

public partial class OutboxMetricTests
{
    [Test]
    public async Task PartialAcknowledgementAndRetry_CountAttemptedPrefixesAndFailures()
    {
        using var capture = new MetricCapture("partial");
        var store = new MetricStore { Rows = [Row(1), Row(2), Row(3)] };
        var first = true;
        var publisher = new MetricPublisher
        {
            Handler = (messages, _) =>
            {
                var result = first ? new OutboxPublishResult(1, new InvalidOperationException("publish failed"))
                    : new OutboxPublishResult(messages.Count, null);
                first = false;
                return new(result);
            }
        };
        using var relay = Relay(store, publisher, "partial");
        var cycle = BindCycle(relay);
        await cycle(default);
        await cycle(default);
        await Assert.That(store.Rows.Count).IsEqualTo(0);
        await Assert.That(capture.Counter("dekaf.outbox.publish.acknowledged")).IsEqualTo(3);
        await Assert.That(capture.Counter("dekaf.outbox.publish.failures")).IsEqualTo(1);
        await Assert.That(capture.Samples("dekaf.outbox.publish.duration")).IsEqualTo(2);
        await Assert.That(capture.Samples("dekaf.outbox.cycle.duration")).IsEqualTo(2);
        capture.Observe();
        await Assert.That(capture.Gauge("dekaf.outbox.owned_buckets")).IsEqualTo(1);
        await Assert.That(capture.UnexpectedTags).IsFalse();
    }

    [Test]
    public async Task DeleteFailure_ReportsAcknowledgementsAgainOnDuplicateAttempt()
    {
        using var capture = new MetricCapture("duplicate");
        var store = new MetricStore { Rows = [Row(1), Row(2)], DeleteFailures = 1 };
        using var relay = Relay(store, new MetricPublisher(), "duplicate");
        var cycle = BindCycle(relay);
        await Assert.That(async () => await cycle(default)).Throws<InvalidOperationException>();
        await cycle(default);
        await Assert.That(capture.Counter("dekaf.outbox.publish.acknowledged")).IsEqualTo(4);
        await Assert.That(capture.Counter("dekaf.outbox.publish.failures")).IsEqualTo(0);
        await Assert.That(capture.Samples("dekaf.outbox.cycle.duration")).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PublisherException_ExcludesCooperativeCancellationFromFailures(bool shutdown)
    {
        using var capture = new MetricCapture("exception");
        using var cancellation = new CancellationTokenSource();
        var publisher = new MetricPublisher
        {
            Handler = (_, _) =>
            {
                if (shutdown)
                {
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                }
                throw new InvalidOperationException("publish failed");
            }
        };
        using var relay = Relay(new MetricStore { Rows = [Row(1)] }, publisher, "exception");
        var cycle = BindCycle(relay);
        if (shutdown)
            await Assert.That(async () => await cycle(cancellation.Token)).Throws<OperationCanceledException>();
        else
            await Assert.That(async () => await cycle(cancellation.Token)).Throws<InvalidOperationException>();
        await Assert.That(capture.Counter("dekaf.outbox.publish.failures")).IsEqualTo(shutdown ? 0 : 1);
        await Assert.That(capture.Samples("dekaf.outbox.publish.duration")).IsEqualTo(1);
        await Assert.That(capture.Samples("dekaf.outbox.cycle.duration")).IsEqualTo(1);
    }

    [Test]
    public async Task ExpiredAcquisition_ReportsOneEventAndClearsOwnership()
    {
        using var capture = new MetricCapture("expired");
        var time = new FakeOutboxTimeProvider();
        var store = new MetricStore
        {
            Rows = [Row(1)],
            OnAcquire = () => time.Advance(TimeSpan.FromSeconds(61))
        };
        var publisher = new MetricPublisher();
        using var relay = Relay(store, publisher, "expired", time);
        await BindCycle(relay)(default);
        capture.Observe();
        await Assert.That(capture.Counter("dekaf.outbox.lease.expirations")).IsEqualTo(1);
        await Assert.That(capture.Gauge("dekaf.outbox.owned_buckets")).IsEqualTo(0);
        await Assert.That(publisher.PublishCalls).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PendingPublisher_RecordsOutcomeAndDurationsAfterCompletion(bool fails)
    {
        using var capture = new MetricCapture("pending-publish");
        var completion = new TaskCompletionSource<OutboxPublishResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new MetricStore { Rows = [Row(1), Row(2)] };
        var publisher = new MetricPublisher { Handler = (_, _) => new(completion.Task) };
        using var relay = Relay(store, publisher, "pending-publish");
        var cycle = BindCycle(relay)(default);
        await Assert.That(cycle.IsCompleted).IsFalse();
        await Assert.That(capture.Samples("dekaf.outbox.cycle.duration")).IsEqualTo(0);
        if (fails)
        {
            completion.SetException(new InvalidOperationException("pending publish failed"));
            await Assert.That(async () => await cycle).Throws<InvalidOperationException>();
        }
        else
        {
            completion.SetResult(new OutboxPublishResult(2, null));
            await cycle;
        }
        await Assert.That(store.Rows.Count).IsEqualTo(fails ? 2 : 0);
        await Assert.That(capture.Counter("dekaf.outbox.publish.acknowledged")).IsEqualTo(fails ? 0 : 2);
        await Assert.That(capture.Counter("dekaf.outbox.publish.failures")).IsEqualTo(fails ? 1 : 0);
        await Assert.That(capture.Samples("dekaf.outbox.publish.duration")).IsEqualTo(1);
        await Assert.That(capture.Samples("dekaf.outbox.cycle.duration")).IsEqualTo(1);
    }

    [Test]
    public async Task ThrowingMetricListener_DoesNotChangePublishingOrDeletion()
    {
        using var capture = new MetricCapture("throwing") { ThrowOnMeasurement = true };
        var store = new MetricStore { Rows = [Row(1), Row(2)] };
        using var relay = Relay(store, new MetricPublisher(), "throwing");
        await BindCycle(relay)(default);
        await Assert.That(store.Rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExpiryBetweenCycles_IsCountedBeforeReacquisition()
    {
        using var capture = new MetricCapture("paused");
        var time = new FakeOutboxTimeProvider();
        using var relay = Relay(new MetricStore(), new MetricPublisher(), "paused", time);
        var cycle = BindCycle(relay);
        await cycle(default);
        time.Advance(TimeSpan.FromSeconds(61));
        await cycle(default);
        await cycle(default);
        capture.Observe();
        await Assert.That(capture.Counter("dekaf.outbox.lease.expirations")).IsEqualTo(1);
        await Assert.That(capture.Gauge("dekaf.outbox.owned_buckets")).IsEqualTo(1);
    }

    [Test]
    public async Task WholeStoreSnapshots_AreNotSummedAcrossReplicas()
    {
        using var capture = new MetricCapture("replicas");
        var time = new FakeOutboxTimeProvider();
        using var first = new OutboxMetricState("replicas", time)
        {
            OwnedBuckets = 2,
            Pending = new OutboxPendingMetrics(7, time.GetUtcNow().AddSeconds(-20))
        };
        using var second = new OutboxMetricState("replicas", time)
        {
            OwnedBuckets = 3,
            Pending = new OutboxPendingMetrics(7, time.GetUtcNow().AddSeconds(-10))
        };
        OutboxMetrics.Register(first);
        OutboxMetrics.Register(second);
        capture.Observe();
        await Assert.That(capture.Gauge("dekaf.outbox.owned_buckets")).IsEqualTo(5);
        await Assert.That(capture.Gauge("dekaf.outbox.pending.messages")).IsEqualTo(7);
        await Assert.That(capture.Gauge("dekaf.outbox.pending.oldest_age")).IsEqualTo(20);
        first.Pending = new OutboxPendingMetrics(0, time.GetUtcNow().AddSeconds(-20));
        second.Pending = new OutboxPendingMetrics(7, null);
        capture.Observe();
        await Assert.That(capture.Gauge("dekaf.outbox.pending.oldest_age")).IsNull();
    }

    private static OutboxMessage Row(long id) => new()
    {
        Id = id, MessageId = Guid.NewGuid(), Bucket = 0, Topic = "metric-topic",
        CreatedAtUtc = DateTimeOffset.UnixEpoch, Value = [1]
    };

    private static OutboxRelayOptions Options(string name) => new()
    {
        MetricsName = name, RelayId = "metric-relay", BucketCount = 1, BatchSize = 100,
        LeaseDuration = TimeSpan.FromSeconds(60), LeaseRenewInterval = TimeSpan.FromSeconds(20),
        MaxPublishDuration = TimeSpan.FromSeconds(10), PollInterval = TimeSpan.FromMinutes(1),
        MetricsCollectionInterval = TimeSpan.FromSeconds(30), MetricsCollectionTimeout = TimeSpan.FromSeconds(5)
    };

    private static OutboxRelayService Relay(IOutboxStore store, IOutboxPublisher publisher,
        string name, TimeProvider? time = null) =>
        new(store, publisher, Options(name), NullLogger<OutboxRelayService>.Instance, time);

    private static Func<CancellationToken, Task> BindCycle(OutboxRelayService relay)
    {
        var method = typeof(OutboxRelayService).GetMethod("RunCycleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Func<CancellationToken, Task>)typeof(OutboxMetricTests)
            .GetMethod(nameof(BindValueTaskCycle), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(method.ReturnType.GenericTypeArguments[0])
            .Invoke(null, [relay, method])!;
    }

    private static Func<CancellationToken, Task> BindValueTaskCycle<TResult>(OutboxRelayService relay, MethodInfo method)
    {
        var cycle = method.CreateDelegate<Func<CancellationToken, ValueTask<TResult>>>(relay);
        return token => cycle(token).AsTask();
    }

    private class MetricStore : IOutboxStore
    {
        private static readonly int[] Owned = [0];
        internal IReadOnlyList<OutboxMessage> Rows { get; set; } = [];
        internal Action? OnAcquire { get; init; }
        internal int DeleteFailures { get; set; }
        internal TaskCompletionSource Marked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default)
        {
            OnAcquire?.Invoke();
            return new(Owned);
        }
        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => new(Rows.Count == 0 ? [] : Owned);
        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default) => new(Rows);
        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> messages,
            CancellationToken cancellationToken = default)
        {
            if (DeleteFailures > 0)
            {
                DeleteFailures--;
                throw new InvalidOperationException("delete failed");
            }
            Rows = Rows.Skip(messages.Count).ToArray();
            Marked.TrySetResult();
            return default;
        }
    }

    private sealed class MetricPublisher : IOutboxPublisher
    {
        internal Task Initialization { get; init; } = Task.CompletedTask;
        internal Func<IReadOnlyList<OutboxMessage>, CancellationToken, ValueTask<OutboxPublishResult>>? Handler { get; init; }
        internal int PublishCalls;
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => new(Initialization.WaitAsync(cancellationToken));
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref PublishCalls);
            return Handler?.Invoke(messages, cancellationToken) ?? new(new OutboxPublishResult(messages.Count, null));
        }
        public ValueTask DisposeAsync() => default;
    }

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentDictionary<string, long> _counters = new();
        private readonly ConcurrentDictionary<string, double> _gauges = new();
        private readonly ConcurrentDictionary<string, int> _samples = new();
        internal bool ThrowOnMeasurement { get; init; }
        internal bool UnexpectedTags { get; private set; }

        internal MetricCapture(string name)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == OutboxDiagnostics.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                if (!Matches(tags, name)) return;
                if (ThrowOnMeasurement) throw new InvalidOperationException("listener failed");
                if (instrument.IsObservable) _gauges[instrument.Name] = value;
                else _counters.AddOrUpdate(instrument.Name, value, (_, total) => total + value);
            });
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            {
                if (!Matches(tags, name)) return;
                if (ThrowOnMeasurement) throw new InvalidOperationException("listener failed");
                if (instrument.IsObservable) _gauges[instrument.Name] = value;
                else _samples.AddOrUpdate(instrument.Name, 1, static (_, count) => count + 1);
            });
            _listener.Start();
        }

        private bool Matches(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
        {
            if (tags.Length == 0 || !Equals(tags[0].Value, name)) return false;
            UnexpectedTags |= tags.Length != 1 || tags[0].Key != "outbox.name";
            return true;
        }
        internal long Counter(string name) => _counters.GetValueOrDefault(name);
        internal int Samples(string name) => _samples.GetValueOrDefault(name);
        internal double? Gauge(string name) => _gauges.TryGetValue(name, out var value) ? value : null;
        internal void Observe() { _gauges.Clear(); _listener.RecordObservableInstruments(); }
        public void Dispose() => _listener.Dispose();
    }
}
