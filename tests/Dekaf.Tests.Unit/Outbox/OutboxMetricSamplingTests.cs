using System.Threading.Channels;
using Dekaf.Outbox;

namespace Dekaf.Tests.Unit.Outbox;

public partial class OutboxMetricTests
{
    [Test]
    public async Task BlockedMetricsQuery_DoesNotBlockPublishingAndCancelsOnShutdown()
    {
        using var capture = new MetricCapture("blocked-sample");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var release = new TaskCompletionSource<OutboxPendingMetrics?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new SamplingStore
        {
            Rows = [Row(1), Row(2)],
            Query = (_, token) =>
            {
                queryEntered.TrySetResult();
                return new(release.Task.WaitAsync(token));
            }
        };
        var publisher = new MetricPublisher { Initialization = queryEntered.Task };
        using var relay = Relay(store, publisher, "blocked-sample");
        await relay.StartAsync(timeout.Token);
        try
        {
            await store.Started.Reader.ReadAsync(timeout.Token);
            await store.Marked.Task.WaitAsync(timeout.Token);
            await Assert.That(store.Rows.Count).IsEqualTo(0);
            await Assert.That(publisher.PublishCalls).IsEqualTo(1);
            await Assert.That(release.Task.IsCompleted).IsFalse();
        }
        finally
        {
            await relay.StopAsync(timeout.Token);
        }
        await Assert.That(store.QueryCancelled).IsTrue();
        capture.Observe();
        await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsNull();
    }

    [Test]
    public async Task SamplingCadence_IsBoundedAndFailureClearsCachedValues()
    {
        using var capture = new MetricCapture("cadence");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var time = new ManualTimeProvider();
        var store = new SamplingStore
        {
            Query = (call, _) => call switch
            {
                1 => new(new OutboxPendingMetrics(7, time.GetUtcNow().AddSeconds(-120))),
                2 => throw new InvalidOperationException("metrics database unavailable"),
                _ => new(new OutboxPendingMetrics(0, null))
            }
        };
        using var relay = Relay(store, new MetricPublisher(), "cadence", time);
        await relay.StartAsync(timeout.Token);
        try
        {
            await store.Started.Reader.ReadAsync(timeout.Token);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            capture.Observe();
            await Assert.That(capture.Gauge("dekaf.outbox.pending.messages")).IsEqualTo(7);
            await Assert.That(capture.Gauge("dekaf.outbox.pending.oldest_age")).IsEqualTo(120);
            await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsEqualTo(1);
            time.Advance(TimeSpan.FromSeconds(29));
            await Assert.That(store.Calls).IsEqualTo(1);
            capture.Observe();
            await Assert.That(capture.Gauge("dekaf.outbox.pending.oldest_age")).IsEqualTo(149);
            time.Advance(TimeSpan.FromSeconds(1));
            await store.Started.Reader.ReadAsync(timeout.Token);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            capture.Observe();
            await Assert.That(store.Calls).IsEqualTo(2);
            await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsEqualTo(0);
            await Assert.That(capture.Gauge("dekaf.outbox.pending.messages")).IsNull();
            await Assert.That(capture.Gauge("dekaf.outbox.pending.oldest_age")).IsNull();
            await Assert.That(relay.ExecuteTask!.IsCompleted).IsFalse();
            time.Advance(TimeSpan.FromSeconds(30));
            await store.Started.Reader.ReadAsync(timeout.Token);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            capture.Observe();
            await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsEqualTo(1);
            await Assert.That(capture.Gauge("dekaf.outbox.pending.messages")).IsEqualTo(0);
        }
        finally
        {
            await relay.StopAsync(timeout.Token);
        }
    }

    [Test]
    public async Task QueryTimeout_CancelsOneQueryWithoutOverlappingRetries()
    {
        using var capture = new MetricCapture("sample-timeout");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var time = new ManualTimeProvider();
        var release = new TaskCompletionSource<OutboxPendingMetrics?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new SamplingStore { Query = (_, token) => new(release.Task.WaitAsync(token)) };
        using var relay = Relay(store, new MetricPublisher(), "sample-timeout", time);
        await relay.StartAsync(timeout.Token);
        try
        {
            await store.Started.Reader.ReadAsync(timeout.Token);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(5));
            time.Advance(TimeSpan.FromSeconds(5));
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            await Assert.That(store.QueryCancelled).IsTrue();
            await Assert.That(store.Calls).IsEqualTo(1);
            capture.Observe();
            await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsEqualTo(0);
            time.Advance(TimeSpan.FromSeconds(29));
            await Assert.That(store.Calls).IsEqualTo(1);
        }
        finally
        {
            await relay.StopAsync(timeout.Token);
        }
    }

    [Test]
    public async Task NoPendingListener_PerformsNoMetricsQueries()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var time = new ManualTimeProvider();
        var store = new SamplingStore { Query = static (_, _) => throw new InvalidOperationException("must not query") };
        using var relay = Relay(store, new MetricPublisher(), "disabled-sample", time);
        await relay.StartAsync(timeout.Token);
        try
        {
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            time.Advance(TimeSpan.FromSeconds(30));
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            await Assert.That(store.Calls).IsEqualTo(0);
        }
        finally
        {
            await relay.StopAsync(timeout.Token);
        }
    }

    [Test]
    public async Task EmptyAndUnavailableSnapshots_HaveDifferentObservations()
    {
        using var capture = new MetricCapture("snapshot-states");
        using var state = new OutboxMetricState("snapshot-states", TimeProvider.System);
        OutboxMetrics.Register(state);
        capture.Observe();
        await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsEqualTo(0);
        await Assert.That(capture.Gauge("dekaf.outbox.pending.messages")).IsNull();
        state.Pending = new OutboxPendingMetrics(0, null);
        capture.Observe();
        await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsEqualTo(1);
        await Assert.That(capture.Gauge("dekaf.outbox.pending.messages")).IsEqualTo(0);
        await Assert.That(capture.Gauge("dekaf.outbox.pending.oldest_age")).IsEqualTo(0);
        state.Pending = new OutboxPendingMetrics(7, null);
        capture.Observe();
        await Assert.That(capture.Gauge("dekaf.outbox.pending.messages")).IsEqualTo(7);
        await Assert.That(capture.Gauge("dekaf.outbox.pending.oldest_age")).IsNull();
    }

    private sealed class SamplingStore : MetricStore, IOutboxMetricsStore
    {
        internal required Func<int, CancellationToken, ValueTask<OutboxPendingMetrics?>> Query { get; init; }
        internal int Calls;
        internal bool QueryCancelled;
        internal Channel<int> Started { get; } = Channel.CreateUnbounded<int>();
        public async ValueTask<OutboxPendingMetrics?> GetPendingMetricsAsync(CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref Calls);
            Started.Writer.TryWrite(call);
            try
            {
                return await Query(call, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                QueryCancelled = true;
                throw;
            }
        }
    }
}
