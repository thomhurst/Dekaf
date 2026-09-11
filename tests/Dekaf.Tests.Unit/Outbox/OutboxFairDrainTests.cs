using System.Threading.Channels;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxFairDrainTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FullBucket_YieldsToPeer_AfterInFlightRenewal_AndRebalances(bool singleOwner)
    {
        var time = new ManualTimeProvider();
        var store = new Store { SingleOwner = singleOwner };
        var publisher = new Publisher();
        using var relay = new OutboxRelayService(store, publisher,
            new OutboxRelayOptions { BucketCount = 2, BatchSize = 1 },
            NullLogger<OutboxRelayService>.Instance, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            var first = await publisher.Calls.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            await Assert.That(first.Bucket).IsEqualTo(0);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(10));
            time.Advance(TimeSpan.FromSeconds(10));
            await store.Renewed.Task.WaitAsync(Timeout);
            first.Completion.SetResult(new OutboxPublishResult(1, null));
            var second = await publisher.Calls.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            await Assert.That(second.Bucket).IsEqualTo(1);
            second.Completion.SetResult(new OutboxPublishResult(1, null));
            await store.Rebalanced.Task.WaitAsync(Timeout);
            var afterRebalance = await publisher.Calls.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            await Assert.That(afterRebalance.Bucket).IsEqualTo(1);
            await Assert.That(store.Acquisitions).IsEqualTo(2);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        }
    }

    [Test]
    public async Task BusySweeps_ReusePendingBuckets_AndDiscoverNewWorkOnPollingCadence()
    {
        var time = new ManualTimeProvider();
        var store = new Store { OnlyFirstInitially = true };
        var publisher = new Publisher();
        using var relay = new OutboxRelayService(store, publisher,
            new OutboxRelayOptions { BucketCount = 2, BatchSize = 1 },
            NullLogger<OutboxRelayService>.Instance, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            for (var index = 0; index < 4; index++)
            {
                var call = await publisher.Calls.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
                await Assert.That(call.Bucket).IsEqualTo(0);
                await Assert.That(store.Probes).IsEqualTo(1);
                call.Completion.SetResult(new OutboxPublishResult(1, null));
            }
            var pending = await publisher.Calls.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            store.OnlyFirstInitially = false;
            time.Advance(TimeSpan.FromSeconds(1));
            pending.Completion.SetResult(new OutboxPublishResult(1, null));
            var first = await publisher.Calls.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            await Assert.That(store.Probes).IsEqualTo(2);
            first.Completion.SetResult(new OutboxPublishResult(1, null));
            var second = await publisher.Calls.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            await Assert.That(second.Bucket).IsEqualTo(1);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        }
    }

    private sealed class Store : IOutboxStore, IOutboxLeaseRenewalStore
    {
        private readonly int[] _both = [0, 1];
        private readonly int[] _first = [0];
        private readonly int[] _second = [1];
        private readonly OutboxMessage[][] _rows =
        [
            [new OutboxMessage { Id = 1, Bucket = 0, Topic = "test", MessageId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UnixEpoch }],
            [new OutboxMessage { Id = 2, Bucket = 1, Topic = "test", MessageId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UnixEpoch }]
        ];
        public bool OnlyFirstInitially { get; set; }
        public bool SingleOwner { get; init; }
        public int Acquisitions { get; private set; }
        public int Probes { get; private set; }
        public TaskCompletionSource Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Rebalanced { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default)
        {
            if (++Acquisitions > 1)
                Rebalanced.TrySetResult();
            return new(Acquisitions == 1 ? (SingleOwner ? _first : _both) : _second);
        }

        public ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default)
        {
            Renewed.TrySetResult();
            return new(true);
        }

        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default)
        {
            Probes++;
            return new(OnlyFirstInitially ? _first : buckets);
        }

        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default) => new(_rows[bucket]);

        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> messages,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed record Call(int Bucket, TaskCompletionSource<OutboxPublishResult> Completion);

    private sealed class Publisher : IOutboxPublisher
    {
        public Channel<Call> Calls { get; } = Channel.CreateUnbounded<Call>();
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public async ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<OutboxPublishResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Calls.Writer.TryWrite(new Call(messages[0].Bucket, completion));
            return await completion.Task.WaitAsync(cancellationToken);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
