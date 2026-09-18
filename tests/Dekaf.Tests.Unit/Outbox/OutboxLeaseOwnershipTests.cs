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
        var store = new OwnershipStore { FailFirstPendingProbe = true };
        using var relay = CreateRelay(store, new GatedPublisher(), time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await Assert.That(await NextAcquisitionAsync(store)).IsEmpty();
            // The failed probe drops local ownership; the store still holds the leases, so
            // the hint must still steer the retry onto them.
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

        await Assert.That(string.Join(',', events)).IsEqualTo("publish-returned,released");
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
            await Gate.Task;
            Events?.Enqueue("publish-returned");
            return new OutboxPublishResult(messages.Count, null);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class OwnershipStore : IOutboxStore, IOutboxLeaseRenewalStore, IOutboxLeaseOwnershipStore
    {
        private static readonly IReadOnlyList<int> Buckets = [1, 3];
        private readonly OutboxMessage[] _batch = [new()
        { Id = 1, MessageId = Guid.NewGuid(), Bucket = 1, Topic = "topic", CreatedAtUtc = DateTimeOffset.UnixEpoch }];
        private int _legacyAcquisitionCount;
        private int _releaseCount;
        private int _pendingProbeCount;

        internal bool HasPending { get; init; }
        internal bool FailFirstPendingProbe { get; init; }
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
            return ValueTask.FromResult(Buckets);
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
            if (FailFirstPendingProbe && Interlocked.Increment(ref _pendingProbeCount) == 1)
                return ValueTask.FromException<IReadOnlyList<int>>(new InvalidOperationException("probe failed"));
            return ValueTask.FromResult<IReadOnlyList<int>>(HasPending ? [1] : []);
        }

        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<OutboxMessage>>(_batch);

        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> publishedMessages,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
