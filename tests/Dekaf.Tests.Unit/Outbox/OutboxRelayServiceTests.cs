using System.Collections.Concurrent;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

public class OutboxRelayServiceTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);

    private static OutboxRelayOptions FastOptions(int bucketCount = 1, int batchSize = 500) => new()
    {
        BucketCount = bucketCount,
        BatchSize = batchSize,
        PollInterval = TimeSpan.FromMilliseconds(1),
        ErrorBackoff = TimeSpan.FromMilliseconds(1),
        LeaseDuration = TimeSpan.FromSeconds(60),
        LeaseRenewInterval = TimeSpan.FromSeconds(20),
        RelayId = "test-relay"
    };

    private static OutboxMessage Row(long id, int bucket = 0) => new()
    {
        Id = id,
        MessageId = Guid.NewGuid(),
        Bucket = bucket,
        Topic = "topic",
        Value = [1],
        CreatedAtUtc = DateTimeOffset.UnixEpoch
    };

    [Test]
    public async Task Constructor_RenewIntervalNotBelowLeaseDuration_Throws()
    {
        var options = new OutboxRelayOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(5),
            LeaseRenewInterval = TimeSpan.FromSeconds(5)
        };

        await Assert.That(() => new OutboxRelayService(
                new FakeStore(), new FakePublisher(), options,
                NullLogger<OutboxRelayService>.Instance))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_RelayIdOverContractBound_Throws()
    {
        // An oversized relay id must fail at startup: stores size their lease/heartbeat
        // columns to MaxRelayIdLength, so it could never be written and the relay would
        // otherwise back off forever.
        var options = new OutboxRelayOptions
        {
            RelayId = new string('r', OutboxRelayOptions.MaxRelayIdLength + 1)
        };

        await Assert.That(() => new OutboxRelayService(
                new FakeStore(), new FakePublisher(), options,
                NullLogger<OutboxRelayService>.Instance))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Relay_PublishesBatchAndMarksAllIds()
    {
        var store = new FakeStore();
        store.Enqueue(Row(1), Row(2), Row(3));
        var publisher = new FakePublisher();

        await using (await StartRelayAsync(store, publisher, FastOptions()))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        await Assert.That(store.MarkedIds.ToArray()).IsEquivalentTo(new long[] { 1, 2, 3 });
        await Assert.That(publisher.PublishedIds.ToArray()).IsEquivalentTo(new long[] { 1, 2, 3 });
        await Assert.That(store.Rows(0)).IsEmpty();
    }

    [Test]
    public async Task Relay_PartialAck_MarksOnlyContiguousPrefix()
    {
        var store = new FakeStore();
        store.Enqueue(Row(1), Row(2), Row(3));
        // Row 3 permanently fails, so only the contiguous prefix 1,2 may ever be marked.
        var publisher = new FakePublisher { FailIds = [3] };

        await using (await StartRelayAsync(store, publisher, FastOptions()))
        {
            await store.Marked.Task.WaitAsync(SignalTimeout);
        }

        await Assert.That(store.MarkedIds.ToArray()).IsEquivalentTo(new long[] { 1, 2 });
        await Assert.That(store.Rows(0).Count).IsEqualTo(1);
        await Assert.That(store.Rows(0)[0].Id).IsEqualTo(3L);
    }

    [Test]
    public async Task Relay_FailedRowsRetryNextCycleInOrder()
    {
        var store = new FakeStore();
        store.Enqueue(Row(1), Row(2));
        // First publish call acks nothing; every later call acks everything.
        var publisher = new FakePublisher { FailFirstCalls = 1 };

        await using (await StartRelayAsync(store, publisher, FastOptions()))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        await Assert.That(store.MarkedIds.ToArray()).IsEquivalentTo(new long[] { 1, 2 });
        await Assert.That(publisher.PublishCalls).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Relay_PublisherInitFailure_RetriesInsteadOfFaulting()
    {
        var store = new FakeStore();
        store.Enqueue(Row(1));
        // Broker unreachable at process start: the relay must back off and retry, not fault.
        var publisher = new FakePublisher { FailFirstInitCalls = 2 };

        await using (await StartRelayAsync(store, publisher, FastOptions()))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        await Assert.That(store.MarkedIds.ToArray()).IsEquivalentTo(new long[] { 1 });
        await Assert.That(publisher.InitCalls).IsEqualTo(3);
    }

    [Test]
    public async Task Relay_StoreFailure_BacksOffAndRecovers()
    {
        var store = new FakeStore { FailFirstLeaseCalls = 2 };
        store.Enqueue(Row(1));
        var publisher = new FakePublisher();

        await using (await StartRelayAsync(store, publisher, FastOptions()))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        await Assert.That(store.MarkedIds.ToArray()).IsEquivalentTo(new long[] { 1 });
        await Assert.That(store.LeaseCalls).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task Relay_MultipleBuckets_EachDrainedIndependently()
    {
        var store = new FakeStore(ownedBuckets: [0, 1]);
        store.Enqueue(Row(1, bucket: 0), Row(2, bucket: 1), Row(3, bucket: 1));
        var publisher = new FakePublisher();

        await using (await StartRelayAsync(store, publisher, FastOptions(bucketCount: 2)))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        var marked = store.MarkedIds.ToArray();
        Array.Sort(marked);
        await Assert.That(marked).IsEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Test]
    public async Task Relay_LongBacklog_RenewsLeaseBetweenBatches()
    {
        var time = new FakeOutboxTimeProvider();
        var store = new FakeStore();
        store.Enqueue(Row(1), Row(2));
        // Each served batch jumps past the renew interval (20s < 60s lease duration): the
        // drain loop must stop so the next cycle renews before the lease can expire, instead
        // of publishing a long backlog on an aging lease.
        var leaseCallsAtLastBatch = 0;
        store.OnBatchReturned = () =>
        {
            leaseCallsAtLastBatch = store.LeaseCalls;
            time.Advance(TimeSpan.FromSeconds(25));
        };
        var publisher = new FakePublisher();

        await using (await StartRelayAsync(store, publisher, FastOptions(batchSize: 1), time))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        await Assert.That(store.MarkedIds.ToArray()).IsEquivalentTo(new long[] { 1, 2 });
        // The second single-row batch must only have been fetched after a lease renewal.
        await Assert.That(leaseCallsAtLastBatch).IsEqualTo(2);
    }

    [Test]
    public async Task Relay_SlowLeaseRenewal_StillPublishes_NoLivelock()
    {
        var time = new FakeOutboxTimeProvider();
        var store = new FakeStore();
        store.Enqueue(Row(1), Row(2));
        // Every acquisition takes longer than the renew interval (25s > 20s, < 60s lease
        // duration), so each lease is already renewal-due the moment it is acquired. The
        // relay must still publish - one batch per cycle - instead of renewing forever.
        store.OnLeaseAcquired = () => time.Advance(TimeSpan.FromSeconds(25));
        var publisher = new FakePublisher();

        await using (await StartRelayAsync(store, publisher, FastOptions(batchSize: 1), time))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        await Assert.That(store.MarkedIds.ToArray()).IsEquivalentTo(new long[] { 1, 2 });
    }

    [Test]
    public async Task Relay_AcquisitionOutlastingLeaseDuration_PausesInsteadOfPublishingDead()
    {
        var time = new FakeOutboxTimeProvider();
        var store = new FakeStore();
        store.Enqueue(Row(1));
        // The first two acquisitions take longer than the whole 60s lease duration: their
        // leases are dead on arrival and publishing under them would break single-writer.
        // The relay must pause (no publish) and recover once the store speeds up.
        var acquisitions = 0;
        store.OnLeaseAcquired = () =>
        {
            if (++acquisitions <= 2)
                time.Advance(TimeSpan.FromSeconds(65));
        };
        var publisher = new FakePublisher();

        await using (await StartRelayAsync(store, publisher, FastOptions(), time))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        await Assert.That(store.MarkedIds.ToArray()).IsEquivalentTo(new long[] { 1 });
        // Exactly one publish: nothing was published under the two dead leases.
        await Assert.That(publisher.PublishCalls).IsEqualTo(1);
        await Assert.That(store.LeaseCalls).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task Relay_MarksSameInstancesReturnedByStore_SoStoresCanSubclass()
    {
        // NoSQL stores identify rows by downcasting to their own OutboxMessage subclass;
        // that only works if the relay passes back the exact instances the store returned.
        var store = new FakeStore();
        var first = new SubclassedRow { MessageId = Guid.NewGuid(), Bucket = 0, Topic = "topic", Value = [1], CreatedAtUtc = DateTimeOffset.UnixEpoch, Id = 1, NativeId = "doc-1" };
        var second = new SubclassedRow { MessageId = Guid.NewGuid(), Bucket = 0, Topic = "topic", Value = [1], CreatedAtUtc = DateTimeOffset.UnixEpoch, Id = 2, NativeId = "doc-2" };
        store.Enqueue(first, second);
        var publisher = new FakePublisher();

        await using (await StartRelayAsync(store, publisher, FastOptions()))
        {
            await store.WaitForEmptyAsync(SignalTimeout);
        }

        var marked = store.MarkedMessages.ToArray();
        await Assert.That(marked.Length).IsEqualTo(2);
        await Assert.That(ReferenceEquals(marked[0], first)).IsTrue();
        await Assert.That(ReferenceEquals(marked[1], second)).IsTrue();
        await Assert.That(((SubclassedRow)marked[0]).NativeId).IsEqualTo("doc-1");
    }

    private sealed class SubclassedRow : OutboxMessage
    {
        public required string NativeId { get; init; }
    }

    private static async Task<RelayHandle> StartRelayAsync(
        FakeStore store, FakePublisher publisher, OutboxRelayOptions options, TimeProvider? timeProvider = null)
    {
        var relay = new OutboxRelayService(
            store, publisher, options, NullLogger<OutboxRelayService>.Instance, timeProvider);
        await relay.StartAsync(CancellationToken.None);
        return new RelayHandle(relay);
    }

    private sealed class RelayHandle(OutboxRelayService relay) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            using var cts = new CancellationTokenSource(SignalTimeout);
            await relay.StopAsync(cts.Token);
            relay.Dispose();
        }
    }

    private sealed class FakeStore : IOutboxStore
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, List<OutboxMessage>> _rows = [];
        private readonly IReadOnlyList<int> _ownedBuckets;
        private readonly TaskCompletionSource _empty = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _leaseCalls;

        public FakeStore(IReadOnlyList<int>? ownedBuckets = null)
        {
            _ownedBuckets = ownedBuckets ?? [0];
        }

        public TaskCompletionSource Marked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<long> MarkedIds { get; } = [];
        public ConcurrentQueue<OutboxMessage> MarkedMessages { get; } = [];
        public int FailFirstLeaseCalls { get; init; }
        public int LeaseCalls => Volatile.Read(ref _leaseCalls);

        /// <summary>Invoked after each non-empty batch is served (e.g. to advance a fake clock).</summary>
        public Action? OnBatchReturned { get; set; }

        /// <summary>Invoked inside each successful lease acquisition (e.g. to simulate a slow store).</summary>
        public Action? OnLeaseAcquired { get; set; }

        public void Enqueue(params OutboxMessage[] rows)
        {
            lock (_lock)
            {
                foreach (var row in rows)
                {
                    if (!_rows.TryGetValue(row.Bucket, out var list))
                        _rows[row.Bucket] = list = [];
                    list.Add(row);
                }
            }
        }

        public IReadOnlyList<OutboxMessage> Rows(int bucket)
        {
            lock (_lock)
            {
                return _rows.TryGetValue(bucket, out var list) ? [.. list] : [];
            }
        }

        public async Task WaitForEmptyAsync(TimeSpan timeout) => await _empty.Task.WaitAsync(timeout);

        public async ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
            OutboxLeaseRequest request, CancellationToken cancellationToken = default)
        {
            // Yield so a permanently-busy relay cannot spin synchronously inside StartAsync.
            await Task.Yield();
            var call = Interlocked.Increment(ref _leaseCalls);
            if (call <= FailFirstLeaseCalls)
                throw new InvalidOperationException("Simulated lease store outage.");
            OnLeaseAcquired?.Invoke();
            return _ownedBuckets;
        }

        public async ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(
            IReadOnlyList<int> buckets, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            lock (_lock)
            {
                var pending = new List<int>();
                for (var i = 0; i < buckets.Count; i++)
                {
                    if (_rows.TryGetValue(buckets[i], out var list) && list.Count > 0)
                        pending.Add(buckets[i]);
                }

                return pending;
            }
        }

        public async ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(
            int bucket, int maxCount, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            List<OutboxMessage> batch;
            lock (_lock)
            {
                if (!_rows.TryGetValue(bucket, out var list))
                    return [];
                batch = list.GetRange(0, Math.Min(maxCount, list.Count));
            }

            if (batch.Count > 0)
                OnBatchReturned?.Invoke();

            return batch;
        }

        public async ValueTask MarkPublishedAsync(
            int bucket, IReadOnlyList<OutboxMessage> publishedMessages, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            lock (_lock)
            {
                var list = _rows[bucket];
                for (var i = 0; i < publishedMessages.Count; i++)
                {
                    var id = publishedMessages[i].Id;
                    MarkedIds.Enqueue(id);
                    MarkedMessages.Enqueue(publishedMessages[i]);
                    list.RemoveAll(r => r.Id == id);
                }

                var anyLeft = false;
                foreach (var rows in _rows.Values)
                    anyLeft |= rows.Count > 0;
                if (!anyLeft)
                    _empty.TrySetResult();
            }

            Marked.TrySetResult();
        }
    }

    private sealed class FakePublisher : IOutboxPublisher
    {
        private int _publishCalls;
        private int _initCalls;

        /// <summary>Ids that permanently fail; acking stops at the first one encountered.</summary>
        public HashSet<long> FailIds { get; init; } = [];

        /// <summary>The first N publish calls ack nothing and report a failure.</summary>
        public int FailFirstCalls { get; init; }

        /// <summary>The first N initialization attempts throw (broker unreachable at start).</summary>
        public int FailFirstInitCalls { get; init; }

        public int PublishCalls => Volatile.Read(ref _publishCalls);
        public int InitCalls => Volatile.Read(ref _initCalls);
        public ConcurrentQueue<long> PublishedIds { get; } = [];

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _initCalls) <= FailFirstInitCalls)
                return ValueTask.FromException(new InvalidOperationException("Simulated broker unreachable."));
            return ValueTask.CompletedTask;
        }

        public async ValueTask<OutboxPublishResult> PublishAsync(
            IReadOnlyList<OutboxMessage> messages, string messageIdHeaderName,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var call = Interlocked.Increment(ref _publishCalls);
            if (call <= FailFirstCalls)
                return new OutboxPublishResult(0, new InvalidOperationException("Simulated broker outage."));

            var acked = 0;
            for (var i = 0; i < messages.Count; i++)
            {
                if (FailIds.Contains(messages[i].Id))
                    return new OutboxPublishResult(acked, new InvalidOperationException("Simulated per-record failure."));
                PublishedIds.Enqueue(messages[i].Id);
                acked++;
            }

            return new OutboxPublishResult(acked, null);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
