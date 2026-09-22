using System.Collections.Concurrent;
using System.Threading.Channels;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxLeaseOwnershipTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(10);

    [Test]
    public async Task Reacquisition_PassesPreviousAcquisitionInsteadOfCallingLegacyAcquire()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore();
        using var relay = CreateRelay(store, new GatedPublisher(), time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await Assert.That(await NextAcquisitionAsync(store)).IsEmpty();
            await time.WaitForTimerAsync(RenewInterval);
            time.Advance(RenewInterval);

            await Assert.That(string.Join(',', await NextAcquisitionAsync(store))).IsEqualTo("1,3");
            await Assert.That(store.LegacyAcquisitionCount).IsEqualTo(0);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task PreviousBuckets_SurviveLeaseStateReset()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore { FailingAcquisition = 2 };
        using var relay = CreateRelay(store, new GatedPublisher(), time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await Assert.That(await NextAcquisitionAsync(store)).IsEmpty();
            await time.WaitForTimerAsync(RenewInterval);
            time.Advance(RenewInterval);
            await Assert.That(string.Join(',', await NextAcquisitionAsync(store))).IsEqualTo("1,3");

            // The failed acquisition drops local ownership; the store may still hold the
            // leases, so the hint must still steer the retry onto them.
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            time.Advance(TimeSpan.FromSeconds(1));

            await Assert.That(string.Join(',', await NextAcquisitionAsync(store))).IsEqualTo("1,3");
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task FailedPendingProbe_KeepsTheLeases_InsteadOfReacquiring()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore { FailFirstPendingProbe = true };
        using var relay = CreateRelay(store, new GatedPublisher(), time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await NextAcquisitionAsync(store);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            time.Advance(TimeSpan.FromSeconds(1));
            await store.SecondProbe.Task.WaitAsync(SignalTimeout);

            // A read that failed wrote no lease. Reacquiring after it would add a heartbeat,
            // a coordination read and a write per bucket to every retry against a failing store.
            await Assert.That(store.AcquisitionCount).IsEqualTo(1);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task GracefulStop_MarksTheRowsKafkaAcknowledgedBeforeTheStop_ThenReleases()
    {
        var time = new ManualTimeProvider();
        var events = new ConcurrentQueue<string>();
        var store = new OwnershipStore { HasPending = true, Events = events, BatchSize = 5 };
        var publisher = new GatedPublisher { Events = events, AckedCount = 3 };
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        await publisher.Entered.Task.WaitAsync(SignalTimeout);

        var stop = relay.StopAsync(CancellationToken.None);
        publisher.Gate.SetResult();
        await stop.WaitAsync(SignalTimeout);

        // The release hands the bucket to a peer on its next round. Rows left unmarked here
        // are rows that peer publishes a second time, on every rolling deployment.
        await Assert.That(string.Join(',', events)).IsEqualTo("publish-returned,marked:1;2;3,released");
    }

    [Test]
    public async Task StopPastTheShutdownDeadline_LeavesAcknowledgedRowsForTheNextOwner()
    {
        var time = new ManualTimeProvider();
        var events = new ConcurrentQueue<string>();
        var store = new OwnershipStore { HasPending = true, Events = events, BatchSize = 5 };
        var publisher = new GatedPublisher { Events = events, AckedCount = 3 };
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        await publisher.Entered.Task.WaitAsync(SignalTimeout);

        // No deadline is left to bound a store write, so none is started.
        await relay.StopAsync(new CancellationToken(canceled: true));
        publisher.Gate.SetResult();
        await relay.ExecuteTask!.WaitAsync(SignalTimeout);

        await Assert.That(string.Join(',', events)).IsEqualTo("publish-returned");
    }

    [Test]
    public async Task StopAsync_AlreadyCancelledToken_SkipsReleaseQuietly()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore();
        using var relay = CreateRelay(store, new GatedPublisher(), time);
        await relay.StartAsync(CancellationToken.None);
        await NextAcquisitionAsync(store);
        await time.WaitForTimerAsync(RenewInterval);

        // The idle loop ends at once, so only the spent deadline stands between the stop and
        // a release whose every request would fail with it.
        await relay.StopAsync(new CancellationToken(canceled: true));
        await relay.ExecuteTask!.WaitAsync(SignalTimeout);
        await Assert.That(store.ReleaseCount).IsEqualTo(0);

        // A later stop with time left still hands the leases back.
        await relay.StopAsync(CancellationToken.None);
        await Assert.That(store.ReleaseCount).IsEqualTo(1);
    }

    [Test]
    public async Task GracefulStop_ReleasesPreviousBucketsOnlyAfterPublisherReturns()
    {
        var time = new ManualTimeProvider();
        var events = new ConcurrentQueue<string>();
        var store = new OwnershipStore { HasPending = true, Events = events };
        var publisher = new GatedPublisher { Events = events };
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        await publisher.Entered.Task.WaitAsync(SignalTimeout);

        var stop = relay.StopAsync(CancellationToken.None);
        await Assert.That(store.ReleaseCount).IsEqualTo(0);
        publisher.Gate.SetResult();
        await stop.WaitAsync(SignalTimeout);

        // The acknowledged row is marked in between: see GracefulStop_MarksTheRows...
        await Assert.That(string.Join(',', events)).IsEqualTo("publish-returned,marked:1,released");
        await Assert.That(string.Join(',', store.Released!)).IsEqualTo("1,3");
        await Assert.That(store.ReleaseRequest!.RelayId).IsEqualTo("relay-a");
    }

    [Test]
    public async Task StopDeadlineBeforeRelayStops_DoesNotReleaseUntilTheLoopHasEnded()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore { HasPending = true };
        var publisher = new GatedPublisher();
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        await publisher.Entered.Task.WaitAsync(SignalTimeout);

        // The publisher is still running when the shutdown deadline fires.
        await relay.StopAsync(new CancellationToken(canceled: true));
        await Assert.That(store.ReleaseCount).IsEqualTo(0);

        publisher.Gate.SetResult();
        await relay.ExecuteTask!.WaitAsync(SignalTimeout);
        await relay.StopAsync(CancellationToken.None);
        await relay.StopAsync(CancellationToken.None);
        await Assert.That(store.ReleaseCount).IsEqualTo(1);
    }

    [Test]
    public async Task ReleaseFailure_DoesNotFailStop()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore { ReleaseThrows = true };
        using var relay = CreateRelay(store, new GatedPublisher(), time);
        await relay.StartAsync(CancellationToken.None);
        await NextAcquisitionAsync(store);

        await relay.StopAsync(CancellationToken.None);

        await Assert.That(store.ReleaseCount).IsEqualTo(1);
    }

    [Test]
    public async Task MisconfigurationFault_StillReleasesOnStop()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore { HasPending = true };
        var publisher = new GatedPublisher { OnPublish = () => time.Advance(TimeSpan.FromSeconds(6)) };
        publisher.Gate.SetResult();
        using var relay = new OutboxRelayService(store, publisher,
            new OutboxRelayOptions { RelayId = "relay-a", BucketCount = 4, MaxPublishDuration = TimeSpan.FromSeconds(5) },
            NullLogger<OutboxRelayService>.Instance, time);
        await relay.StartAsync(CancellationToken.None);
        await Assert.That(async () => await relay.ExecuteTask!.WaitAsync(SignalTimeout))
            .Throws<OutboxMisconfigurationException>();

        // The faulted loop has ended like a stopped one: nothing is in flight, so the host's
        // stop after the fault must still hand the leases back.
        await relay.StopAsync(CancellationToken.None);

        await Assert.That(store.ReleaseCount).IsEqualTo(1);
    }

    [Test]
    public async Task ReleaseIgnoringCancellation_DoesNotHoldStopPastTheShutdownDeadline()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore { ReleaseNeverCompletes = true };
        using var relay = CreateRelay(store, new GatedPublisher(), time);
        await relay.StartAsync(CancellationToken.None);
        await NextAcquisitionAsync(store);
        using var shutdownDeadline = new CancellationTokenSource();

        var stop = relay.StopAsync(shutdownDeadline.Token);
        await store.ReleaseEntered.Task.WaitAsync(SignalTimeout);
        shutdownDeadline.Cancel();

        await stop.WaitAsync(SignalTimeout);
        await Assert.That(store.ReleaseCount).IsEqualTo(1);
    }

    [Test]
    public async Task StopBeforeFirstAcquisition_DoesNotRelease()
    {
        var time = new ManualTimeProvider();
        var store = new OwnershipStore();
        var publisher = new GatedPublisher { BlockInitialization = true };
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        await publisher.Entered.Task.WaitAsync(SignalTimeout);

        await relay.StopAsync(CancellationToken.None);

        await Assert.That(store.ReleaseCount).IsEqualTo(0);
    }

    private static async Task<IReadOnlyList<int>> NextAcquisitionAsync(OwnershipStore store) =>
        await store.Acquisitions.Reader.ReadAsync().AsTask().WaitAsync(SignalTimeout);

    // PollInterval equals the renewal interval so an idle relay schedules exactly one timer
    // per cycle, which the tests advance to reach the next acquisition.
    private static OutboxRelayService CreateRelay(IOutboxStore store, IOutboxPublisher publisher, TimeProvider time) =>
        new(store, publisher,
            new OutboxRelayOptions
            {
                RelayId = "relay-a", BucketCount = 4, PollInterval = RenewInterval, LeaseRenewInterval = RenewInterval
            },
            NullLogger<OutboxRelayService>.Instance, time);

    /// <summary>
    /// Ignores cancellation until the gate opens, like a producer that cannot retract
    /// records it has already appended.
    /// </summary>
    private sealed class GatedPublisher : IOutboxPublisher
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ConcurrentQueue<string>? Events { get; init; }
        internal bool BlockInitialization { get; init; }
        internal Action? OnPublish { get; init; }
        /// <summary>Rows acknowledged per publish; the whole batch when null.</summary>
        internal int? AckedCount { get; init; }

        public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!BlockInitialization)
                return;
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public async ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            OnPublish?.Invoke();
            await Gate.Task;
            Events?.Enqueue("publish-returned");
            return new OutboxPublishResult(AckedCount ?? messages.Count, null);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class OwnershipStore : IOutboxStore, IOutboxLeaseRenewalStore, IOutboxLeaseOwnershipStore
    {
        private static readonly IReadOnlyList<int> Buckets = [1, 3];
        private int _legacyAcquisitionCount;
        private int _acquisitionCount;
        private int _releaseCount;
        private int _pendingProbeCount;

        internal bool HasPending { get; init; }
        internal int BatchSize { get; init; } = 1;
        internal bool FailFirstPendingProbe { get; init; }
        /// <summary>The one-based acquisition that throws after recording its hint.</summary>
        internal int FailingAcquisition { get; init; }
        internal int AcquisitionCount => Volatile.Read(ref _acquisitionCount);
        internal TaskCompletionSource SecondProbe { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool ReleaseThrows { get; init; }
        internal bool ReleaseNeverCompletes { get; init; }
        internal TaskCompletionSource ReleaseEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ConcurrentQueue<string>? Events { get; init; }
        internal Channel<IReadOnlyList<int>> Acquisitions { get; } = Channel.CreateUnbounded<IReadOnlyList<int>>();
        internal int LegacyAcquisitionCount => Volatile.Read(ref _legacyAcquisitionCount);
        internal int ReleaseCount => Volatile.Read(ref _releaseCount);
        internal IReadOnlyList<int>? Released { get; private set; }
        internal OutboxLeaseRequest? ReleaseRequest { get; private set; }

        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _legacyAcquisitionCount);
            return ValueTask.FromResult(Buckets);
        }

        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            IReadOnlyList<int> previousBuckets, CancellationToken cancellationToken = default)
        {
            Acquisitions.Writer.TryWrite(previousBuckets);
            return Interlocked.Increment(ref _acquisitionCount) == FailingAcquisition
                ? ValueTask.FromException<IReadOnlyList<int>>(new InvalidOperationException("acquisition failed"))
                : ValueTask.FromResult(Buckets);
        }

        public ValueTask ReleaseBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> previousBuckets,
            CancellationToken cancellationToken = default)
        {
            Released = previousBuckets;
            ReleaseRequest = request;
            Interlocked.Increment(ref _releaseCount);
            Events?.Enqueue("released");
            ReleaseEntered.TrySetResult();
            if (ReleaseNeverCompletes)
                return new ValueTask(new TaskCompletionSource().Task);
            return ReleaseThrows
                ? ValueTask.FromException(new InvalidOperationException("release failed"))
                : ValueTask.CompletedTask;
        }

        public ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default)
        {
            var probe = Interlocked.Increment(ref _pendingProbeCount);
            if (probe == 2)
                SecondProbe.TrySetResult();
            if (FailFirstPendingProbe && probe == 1)
                return ValueTask.FromException<IReadOnlyList<int>>(new InvalidOperationException("probe failed"));
            return ValueTask.FromResult<IReadOnlyList<int>>(HasPending ? [1] : []);
        }

        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default)
        {
            var batch = new OutboxMessage[Math.Min(BatchSize, maxCount)];
            for (var index = 0; index < batch.Length; index++)
            {
                batch[index] = new OutboxMessage
                {
                    Id = index + 1, MessageId = Guid.NewGuid(), Bucket = 1, Topic = "topic",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch
                };
            }

            return ValueTask.FromResult<IReadOnlyList<OutboxMessage>>(batch);
        }

        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> publishedMessages,
            CancellationToken cancellationToken = default)
        {
            Events?.Enqueue($"marked:{string.Join(';', publishedMessages.Select(message => message.Id))}");
            return ValueTask.CompletedTask;
        }
    }
}
