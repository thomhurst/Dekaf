using System.Reflection;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

public partial class OutboxMetricTests
{
    [Test]
    [Arguments(0, true, 1)]
    [Arguments(1, true, 0)]
    [Arguments(-1, true, 0)]
    [Arguments(-1, false, 1)]
    public async Task CoordinatedSampling_QueriesOnlyBucketZeroOwner(int bucket, bool coordinated, int expected)
    {
        using var capture = new MetricCapture("coordinated");
        var time = new ManualTimeProvider();
        var store = new SamplingStore
        {
            Acquired = bucket < 0 ? [] : new[] { bucket },
            Query = static (_, _) => new(new OutboxPendingMetrics(7, null))
        };
        using var relay = CoordinatedRelay(store, time, coordinated);
        await BindCycle(relay)(default);
        using var cancellation = new CancellationTokenSource();
        var sampling = StartSampling(relay, store, cancellation.Token);
        try
        {
            await Assert.That(store.Calls).IsEqualTo(expected);
            capture.Observe();
            await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsEqualTo(expected);
        }
        finally
        {
            cancellation.Cancel();
            await sampling.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Test]
    public async Task CoordinatedSampling_StartsWhenTheRelayIsHandedBucketZero_NotAtTheNextInterval()
    {
        using var capture = new MetricCapture("coordinated");
        var time = new ManualTimeProvider();
        var store = new SamplingStore { Query = static (_, _) => new(new OutboxPendingMetrics(7, null)) };
        using var relay = CoordinatedRelay(store, time, true);
        using var cancellation = new CancellationTokenSource();
        // As in a starting host: the sampler runs before the first acquisition has returned.
        var sampling = StartSampling(relay, store, cancellation.Token);
        try
        {
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            await Assert.That(store.Calls).IsEqualTo(0);

            await BindCycle(relay)(default);

            await store.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(store.Calls).IsEqualTo(1);
        }
        finally
        {
            cancellation.Cancel();
            await sampling.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Test]
    public async Task Sampling_IsCoordinatedByDefault()
    {
        await Assert.That(new OutboxRelayOptions().CollectMetricsOnBucketZeroOwnerOnly).IsTrue();
    }

    [Test]
    public async Task CoordinatedSampling_RejectsExpiredOwnership_AndResumesAfterReacquisition()
    {
        using var capture = new MetricCapture("coordinated");
        var time = new ManualTimeProvider();
        var store = new SamplingStore { Query = static (_, _) => new(new OutboxPendingMetrics(7, null)) };
        using var relay = CoordinatedRelay(store, time, true);
        var cycle = BindCycle(relay);
        await cycle(default);
        time.Advance(TimeSpan.FromSeconds(61));
        using var cancellation = new CancellationTokenSource();
        var sampling = StartSampling(relay, store, cancellation.Token);
        try
        {
            await Assert.That(store.Calls).IsEqualTo(0);
            await cycle(default); // Reacquire a valid lease.
            time.Advance(TimeSpan.FromSeconds(30));
            await store.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            await Assert.That(store.Calls).IsEqualTo(1);
        }
        finally
        {
            cancellation.Cancel();
            await sampling.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Test]
    public async Task CoordinatedSampling_OwnershipLossDiscardsInFlightSnapshot()
    {
        using var capture = new MetricCapture("coordinated");
        var time = new ManualTimeProvider();
        var completion = new TaskCompletionSource<OutboxPendingMetrics?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new SamplingStore { Query = (_, token) => new(completion.Task.WaitAsync(token)) };
        using var relay = CoordinatedRelay(store, time, true);
        var cycle = BindCycle(relay);
        await cycle(default);
        using var cancellation = new CancellationTokenSource();
        var sampling = StartSampling(relay, store, cancellation.Token);
        try
        {
            store.Acquired = [1];
            time.Advance(TimeSpan.FromSeconds(20));
            await cycle(default);
            completion.SetResult(new OutboxPendingMetrics(99, null));
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            capture.Observe();
            await Assert.That(capture.Gauge("dekaf.outbox.pending.messages")).IsNull();
            await Assert.That(capture.Gauge("dekaf.outbox.pending.available")).IsEqualTo(0);
        }
        finally
        {
            cancellation.Cancel();
            await sampling.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private static OutboxRelayService CoordinatedRelay(SamplingStore store, TimeProvider time, bool coordinated) =>
        new(store, new MetricPublisher(), new OutboxRelayOptions
        {
            BucketCount = 2, MetricsName = "coordinated", MaxPublishDuration = TimeSpan.FromSeconds(1),
            LeaseDuration = TimeSpan.FromSeconds(60), LeaseRenewInterval = TimeSpan.FromSeconds(20),
            MetricsCollectionTimeout = TimeSpan.FromSeconds(50), CollectMetricsOnBucketZeroOwnerOnly = coordinated
        }, NullLogger<OutboxRelayService>.Instance, time);

    private static Task StartSampling(OutboxRelayService relay, IOutboxMetricsStore store, CancellationToken cancellationToken) =>
        (Task)typeof(OutboxRelayService).GetMethod("CollectPendingMetricsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(relay, [store, cancellationToken])!;
}
