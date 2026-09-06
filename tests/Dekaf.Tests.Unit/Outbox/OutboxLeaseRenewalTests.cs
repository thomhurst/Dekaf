using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Channels;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxLeaseRenewalTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task SlowPublish_RenewsBeyondOriginalExpiry_PeerCannotTakeOver()
    {
        var time = new ManualLeaseTimeProvider();
        var store = new RenewableStore(time);
        var publisher = new PausedPublisher();
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await publisher.Entered.Task.WaitAsync(SignalTimeout);
            for (var i = 0; i < 4; i++)
            {
                await time.WaitForTimerAsync(TimeSpan.FromSeconds(10));
                time.Advance(TimeSpan.FromSeconds(10));
                await store.Renewals.Reader.ReadAsync().AsTask().WaitAsync(SignalTimeout);
                await Assert.That(await store.AcquireBucketLeasesAsync(PeerRequest())).IsEmpty();
            }

            publisher.Completion.SetResult(new OutboxPublishResult(1, null));
            await store.Marked.Task.WaitAsync(SignalTimeout);
            await Assert.That(store.MarkCount).IsEqualTo(1);
        }
        finally
        {
            publisher.Completion.TrySetResult(new OutboxPublishResult(0, null));
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task ExpiredLease_TakenByPeer_OriginalPublishIsObservedButRowsRetained()
    {
        var time = new ManualLeaseTimeProvider();
        var store = new RenewableStore(time);
        var publisher = new PausedPublisher();
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await publisher.Entered.Task.WaitAsync(SignalTimeout);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(10));
            // Simulate a process pause: renewal cannot run before the original expiry.
            time.Advance(TimeSpan.FromSeconds(31));
            await Assert.That(await store.AcquireBucketLeasesAsync(PeerRequest())).IsEquivalentTo([0]);
            publisher.Completion.SetResult(new OutboxPublishResult(1, null));
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1)); // error backoff, after observing publish
            await Assert.That(store.MarkCount).IsEqualTo(0);
            await Assert.That(store.Owner).IsEqualTo("relay-b");
            await Assert.That(publisher.Calls).IsEqualTo(1);
        }
        finally
        {
            publisher.Completion.TrySetResult(new OutboxPublishResult(0, null));
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task FetchNearLeaseBoundary_RenewsBeforeStartingPublisher()
    {
        var time = new ManualLeaseTimeProvider();
        var store = new RenewableStore(time) { FetchDuration = TimeSpan.FromSeconds(29) };
        var publisher = new PausedPublisher();
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await publisher.Entered.Task.WaitAsync(SignalTimeout);
            await Assert.That(store.RenewalCount).IsEqualTo(1);
            await Assert.That(store.ExpiresAt - time.GetUtcNow()).IsEqualTo(TimeSpan.FromSeconds(30));
            publisher.Completion.SetResult(new OutboxPublishResult(1, null));
            await store.Marked.Task.WaitAsync(SignalTimeout);
        }
        finally
        {
            publisher.Completion.TrySetResult(new OutboxPublishResult(0, null));
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task RenewalResponseAfterOldExpiry_RetainsRowsDespiteSuccessfulStoreResponse()
    {
        var time = new ManualLeaseTimeProvider();
        var store = new RenewableStore(time) { RenewalDuration = TimeSpan.FromSeconds(21) };
        var publisher = new PausedPublisher();
        using var relay = CreateRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await publisher.Entered.Task.WaitAsync(SignalTimeout);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(10));
            time.Advance(TimeSpan.FromSeconds(10));
            await store.Renewals.Reader.ReadAsync().AsTask().WaitAsync(SignalTimeout);
            publisher.Completion.SetResult(new OutboxPublishResult(1, null));
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            await Assert.That(store.MarkCount).IsEqualTo(0);
            await Assert.That(store.RenewalCount).IsEqualTo(1);
        }
        finally
        {
            publisher.Completion.TrySetResult(new OutboxPublishResult(0, null));
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task SynchronousPublisherCrossesExpiry_RetainsRows()
    {
        var time = new ManualLeaseTimeProvider();
        var store = new RenewableStore(time);
        using var relay = CreateRelay(store,
            new Publisher(() => time.Advance(TimeSpan.FromSeconds(31))), time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            await Assert.That(store.MarkCount).IsEqualTo(0);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task WholePublishBudgetExceeded_FaultsRelayWithoutMarkingRows()
    {
        var time = new ManualLeaseTimeProvider();
        var store = new RenewableStore(time);
        using var relay = new OutboxRelayService(store,
            new Publisher(() => time.Advance(TimeSpan.FromSeconds(6))),
            new OutboxRelayOptions { MaxPublishDuration = TimeSpan.FromSeconds(5) },
            NullLogger<OutboxRelayService>.Instance, time);
        await relay.StartAsync(CancellationToken.None);
        await Assert.That(async () => await relay.ExecuteTask!.WaitAsync(SignalTimeout))
            .Throws<OutboxMisconfigurationException>();
        await Assert.That(store.MarkCount).IsEqualTo(0);
    }

    [Test]
    public async Task FailedRenewal_DoesNotPublishAnotherBucketFromStaleProbe()
    {
        var time = new ManualLeaseTimeProvider();
        var store = new RenewableStore(time)
        {
            Buckets = [0, 1], FetchDuration = TimeSpan.FromSeconds(10), RenewalSucceeds = false
        };
        var publisher = new PausedPublisher();
        using var relay = new OutboxRelayService(store, publisher,
            new OutboxRelayOptions { RelayId = "relay-a", BucketCount = 2 },
            NullLogger<OutboxRelayService>.Instance, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            await Assert.That(store.FetchCount).IsEqualTo(1);
            await Assert.That(publisher.Calls).IsEqualTo(0);
        }
        finally
        {
            publisher.Completion.TrySetResult(new OutboxPublishResult(0, null));
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    [Arguments(0, true)]
    [Arguments(20, false)]
    public async Task LegacyStore_ReservesWholePublishBudgetAfterFetch(int acquisitionSeconds, bool canPublish)
    {
        var time = new ManualLeaseTimeProvider();
        var inner = new RenewableStore(time)
        {
            FetchDuration = TimeSpan.FromSeconds(26),
            SubsequentAcquisitionDuration = TimeSpan.FromSeconds(acquisitionSeconds)
        };
        var publisher = new PausedPublisher();
        using var relay = new OutboxRelayService(new LegacyStore(inner), publisher,
            new OutboxRelayOptions { RelayId = "relay-a", BucketCount = 1, MaxPublishDuration = TimeSpan.FromSeconds(5) },
            NullLogger<OutboxRelayService>.Instance, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            if (canPublish)
            {
                await publisher.Entered.Task.WaitAsync(SignalTimeout);
                await Assert.That(inner.ExpiresAt - time.GetUtcNow()).IsEqualTo(TimeSpan.FromSeconds(30));
                publisher.Completion.SetResult(new OutboxPublishResult(1, null));
                await inner.Marked.Task.WaitAsync(SignalTimeout);
            }
            else
            {
                await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
                await Assert.That(publisher.Calls).IsEqualTo(0);
                await Assert.That(inner.MarkCount).IsEqualTo(0);
            }

            await Assert.That(inner.AcquisitionCount).IsEqualTo(2);
        }
        finally
        {
            publisher.Completion.TrySetResult(new OutboxPublishResult(0, null));
            await relay.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task StoreWithoutRenewal_RequiresWholePublishBudget()
    {
        await Assert.That(() => new OutboxRelayService(
            new LegacyStore(), new Publisher(), new OutboxRelayOptions(),
            NullLogger<OutboxRelayService>.Instance)).Throws<OutboxMisconfigurationException>();
    }

    [Test]
    public async Task WholePublishBudget_MustFitWithRenewalSlack()
    {
        await Assert.That(() => new OutboxRelayOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(30),
            LeaseRenewInterval = TimeSpan.FromSeconds(10),
            MaxPublishDuration = TimeSpan.FromSeconds(20)
        }.Validate()).Throws<ArgumentException>();
    }

    [Test]
    public async Task LegacyStore_WithExplicitWholePublishBudget_RemainsSupported()
    {
        using var relay = new OutboxRelayService(new LegacyStore(), new Publisher(),
            new OutboxRelayOptions { MaxPublishDuration = TimeSpan.FromSeconds(5) },
            NullLogger<OutboxRelayService>.Instance);
        await Assert.That(relay).IsNotNull();
    }

    private sealed class LegacyStore(IOutboxStore? inner = null) : IOutboxStore
    {
        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default) => inner?.AcquireBucketLeasesAsync(request, cancellationToken)
                ?? ValueTask.FromResult<IReadOnlyList<int>>([0]);
        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => inner?.GetBucketsWithPendingAsync(buckets, cancellationToken)
                ?? ValueTask.FromResult<IReadOnlyList<int>>([]);
        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default) => inner?.GetNextBatchAsync(bucket, maxCount, cancellationToken)
                ?? ValueTask.FromResult<IReadOnlyList<OutboxMessage>>([]);
        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> publishedMessages,
            CancellationToken cancellationToken = default) => inner?.MarkPublishedAsync(bucket, publishedMessages, cancellationToken)
                ?? ValueTask.CompletedTask;
    }

    private sealed class Publisher(Action? beforeReturn = null) : IOutboxPublisher
    {
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            beforeReturn?.Invoke();
            return ValueTask.FromResult(new OutboxPublishResult(messages.Count, null));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static OutboxLeaseRequest PeerRequest() => new()
    {
        RelayId = "relay-b", BucketCount = 1, LeaseDuration = TimeSpan.FromSeconds(30)
    };

    private static OutboxRelayService CreateRelay(IOutboxStore store, IOutboxPublisher publisher, TimeProvider time) =>
        new(store, publisher, new OutboxRelayOptions { RelayId = "relay-a", BucketCount = 1 },
            NullLogger<OutboxRelayService>.Instance, time);

    private sealed class PausedPublisher : IOutboxPublisher
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<OutboxPublishResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls { get; private set; }
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            Calls++;
            Entered.TrySetResult();
            return new ValueTask<OutboxPublishResult>(Completion.Task.WaitAsync(cancellationToken));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RenewableStore(ManualLeaseTimeProvider time) : IOutboxStore, IOutboxLeaseRenewalStore
    {
        private readonly object _gate = new();
        private readonly OutboxMessage[] _batch = [new()
        { Id = 1, MessageId = Guid.NewGuid(), Bucket = 0, Topic = "topic", CreatedAtUtc = DateTimeOffset.UnixEpoch }];
        private string? _owner;
        private DateTimeOffset _expiresAt;
        private int _markCount;
        private int _renewalCount;
        private int _acquisitionCount;
        private int _fetchCount;
        internal string? Owner { get { lock (_gate) return _owner; } }
        internal DateTimeOffset ExpiresAt { get { lock (_gate) return _expiresAt; } }
        internal int MarkCount => Volatile.Read(ref _markCount);
        internal int RenewalCount => Volatile.Read(ref _renewalCount);
        internal int AcquisitionCount => Volatile.Read(ref _acquisitionCount);
        internal int FetchCount => Volatile.Read(ref _fetchCount);
        internal IReadOnlyList<int> Buckets { get; init; } = [0];
        internal bool RenewalSucceeds { get; init; } = true;
        internal TimeSpan FetchDuration { get; init; }
        internal TimeSpan RenewalDuration { get; init; }
        internal TimeSpan SubsequentAcquisitionDuration { get; init; }
        internal Channel<int> Renewals { get; } = Channel.CreateUnbounded<int>();
        internal TaskCompletionSource Marked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_owner is null || _owner == request.RelayId || _expiresAt <= time.GetUtcNow())
                {
                    _owner = request.RelayId;
                    _expiresAt = time.GetUtcNow() + request.LeaseDuration;
                    if (Interlocked.Increment(ref _acquisitionCount) > 1)
                        time.Advance(SubsequentAcquisitionDuration);
                    return ValueTask.FromResult(Buckets);
                }

                return ValueTask.FromResult<IReadOnlyList<int>>([]);
            }
        }

        public ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (!RenewalSucceeds || _owner != request.RelayId || _expiresAt <= time.GetUtcNow())
                    return ValueTask.FromResult(false);
                _expiresAt = time.GetUtcNow() + request.LeaseDuration;
                time.Advance(RenewalDuration);
                Renewals.Writer.TryWrite(Interlocked.Increment(ref _renewalCount));
                return ValueTask.FromResult(true);
            }
        }

        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<int>>(MarkCount == 0 ? Buckets : []);

        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _fetchCount);
            time.Advance(FetchDuration);
            return ValueTask.FromResult<IReadOnlyList<OutboxMessage>>(_batch);
        }

        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> publishedMessages,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _markCount);
            Marked.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
