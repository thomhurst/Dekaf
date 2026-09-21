using System.Collections.Concurrent;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

/// <summary>
/// How the relay behaves while something keeps failing: a row Kafka never accepts, a store
/// that throttles, a renewal that throws, a notifier that throws.
/// </summary>
public sealed class OutboxRelayResilienceTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task PoisonHeadRow_RowsBehindItAreDeliveredOnce_NotOnEveryRetry()
    {
        var store = new RowStore();
        store.Enqueue(Row(1), Row(2), Row(3), Row(4), Row(5));
        var publisher = new ConcurrentSendPublisher { AttemptsToAwait = 6 };
        publisher.Reject(1);

        using var relay = CreateRelay(store, publisher, FastOptions());
        await relay.StartAsync(CancellationToken.None);
        try
        {
            // Row 1 keeps failing. The publisher sends the whole batch at once, as the default
            // one does, so rows 2..5 reach Kafka on the first attempt although none is marked.
            await publisher.AttemptsReached.Task.WaitAsync(SignalTimeout);

            for (var id = 2L; id <= 5; id++)
                await Assert.That(publisher.Deliveries(id)).IsEqualTo(1);
            await Assert.That(store.MarkedIds).IsEmpty();
            // After the first attempt the relay asks for the head row alone.
            await Assert.That(store.FetchSizes.Skip(1).All(size => size == 1)).IsTrue();

            publisher.Accept(1);
            await store.WaitForEmptyAsync(SignalTimeout);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None).WaitAsync(SignalTimeout);
        }

        // One batch of duplicates for the poison row, however long it was stuck.
        await Assert.That(publisher.Deliveries(1)).IsEqualTo(1);
        for (var id = 2L; id <= 5; id++)
            await Assert.That(publisher.Deliveries(id)).IsEqualTo(2);
        await Assert.That(string.Join(',', store.MarkedIds)).IsEqualTo("1,2,3,4,5");
    }

    [Test]
    public async Task PoisonRowInOneBucket_DoesNotStopTheOtherBuckets()
    {
        var store = new RowStore(ownedBuckets: [0, 1]);
        store.Enqueue(Row(1, bucket: 0), Row(2, bucket: 0));
        var publisher = new ConcurrentSendPublisher { AttemptsToAwait = 4 };
        publisher.Reject(1);

        using var relay = CreateRelay(store, publisher, FastOptions(bucketCount: 2));
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await publisher.AttemptsReached.Task.WaitAsync(SignalTimeout);
            store.Enqueue(Row(10, bucket: 1), Row(11, bucket: 1));
            await store.WaitForBucketEmptyAsync(1, SignalTimeout);

            await Assert.That(store.MarkedIds.Order().ToArray()).IsEquivalentTo(new long[] { 10, 11 });
            await Assert.That(publisher.Deliveries(2)).IsEqualTo(1);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None).WaitAsync(SignalTimeout);
        }
    }

    [Test]
    public async Task FruitlessFailures_BackOffExponentiallyWithJitter_AndKeepTheLeases()
    {
        const int failures = 14;
        var time = new AutoAdvanceTimeProvider(fireLimit: failures);
        var store = new RowStore { ProbeAlwaysFails = true };
        var options = new OutboxRelayOptions
        {
            RelayId = "relay-backoff", BucketCount = 1, MaxPublishDuration = TimeSpan.FromSeconds(5),
            ErrorBackoff = TimeSpan.FromSeconds(1), LeaseRenewInterval = TimeSpan.FromSeconds(10)
        };

        using var relay = CreateRelay(store, new ConcurrentSendPublisher(), options, time);
        await relay.StartAsync(CancellationToken.None);
        await time.LimitReached.Task.WaitAsync(SignalTimeout);
        await relay.StopAsync(CancellationToken.None).WaitAsync(SignalTimeout);

        var delays = time.Delays.Take(failures).ToArray();
        await Assert.That(delays[0]).IsEqualTo(options.ErrorBackoff);
        for (var index = 0; index < delays.Length; index++)
        {
            var ceiling = TimeSpan.FromTicks(Math.Min(
                options.LeaseRenewInterval.Ticks, options.ErrorBackoff.Ticks << Math.Min(index, 20)));
            await Assert.That(delays[index]).IsGreaterThanOrEqualTo(options.ErrorBackoff);
            await Assert.That(delays[index]).IsLessThanOrEqualTo(ceiling);
        }

        // The ceiling is reached, and the waits under it are spread rather than equal.
        await Assert.That(delays.Max()).IsGreaterThan(options.ErrorBackoff * 4);
        await Assert.That(delays.Distinct().Count()).IsGreaterThan(failures / 2);

        // A failed probe wrote no lease, so it is no reason to reacquire: the only
        // acquisitions are the ones the renew interval asks for.
        var elapsed = TimeSpan.FromTicks(delays.Sum(delay => delay.Ticks));
        await Assert.That(store.LeaseCalls).IsLessThanOrEqualTo(1 + (int)(elapsed / options.LeaseRenewInterval));
    }

    [Test]
    public async Task BackoffStartsAgainAtTheConfiguredDelay_AfterACycleThatWorked()
    {
        var time = new AutoAdvanceTimeProvider(fireLimit: 5);
        // Probes 1 to 3 fail, the fourth finds nothing, the fifth fails again.
        var store = new RowStore { FailingProbes = [1, 2, 3, 5] };
        var options = new OutboxRelayOptions
        {
            RelayId = "relay-backoff", BucketCount = 1, MaxPublishDuration = TimeSpan.FromSeconds(5),
            ErrorBackoff = TimeSpan.FromSeconds(1), PollInterval = TimeSpan.FromMilliseconds(1500)
        };

        using var relay = CreateRelay(store, new ConcurrentSendPublisher(), options, time);
        await relay.StartAsync(CancellationToken.None);
        await time.LimitReached.Task.WaitAsync(SignalTimeout);
        await relay.StopAsync(CancellationToken.None).WaitAsync(SignalTimeout);

        var delays = time.Delays;
        await Assert.That(delays[3]).IsEqualTo(options.PollInterval);
        await Assert.That(delays[4]).IsEqualTo(options.ErrorBackoff);
    }

    [Test]
    public async Task RenewalThatThrows_WhileThePublishFinishesInsideTheLease_StillMarksTheRows()
    {
        var time = new ManualTimeProvider();
        var store = new RenewableStore(time) { ThrowingRenewals = 1 };
        var publisher = new PausedPublisher();
        using var relay = CreateRenewingRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await publisher.Entered.Task.WaitAsync(SignalTimeout);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(10));
            time.Advance(TimeSpan.FromSeconds(10));
            await store.RenewalAttempted.Task.WaitAsync(SignalTimeout);

            // 10 s into a 30 s lease: a throttled renewal proves nothing about ownership.
            publisher.Completion.SetResult(new OutboxPublishResult(1, null));
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));

            await Assert.That(store.MarkCount).IsEqualTo(1);
            // Ownership was still dropped, so the next cycle reacquires before publishing.
            time.Advance(TimeSpan.FromSeconds(1));
            await store.SecondAcquisition.Task.WaitAsync(SignalTimeout);
        }
        finally
        {
            publisher.Completion.TrySetResult(new OutboxPublishResult(0, null));
            await relay.StopAsync(CancellationToken.None).WaitAsync(SignalTimeout);
        }
    }

    [Test]
    public async Task RenewalThatThrows_WhileThePublishOutlivesTheLease_RetainsTheRows()
    {
        var time = new ManualTimeProvider();
        var store = new RenewableStore(time) { ThrowingRenewals = 1 };
        var publisher = new PausedPublisher();
        using var relay = CreateRenewingRelay(store, publisher, time);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await publisher.Entered.Task.WaitAsync(SignalTimeout);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(10));
            time.Advance(TimeSpan.FromSeconds(10));
            await store.RenewalAttempted.Task.WaitAsync(SignalTimeout);

            // The lease nobody could extend runs out before Kafka answers.
            time.Advance(TimeSpan.FromSeconds(25));
            publisher.Completion.SetResult(new OutboxPublishResult(1, null));
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));

            await Assert.That(store.MarkCount).IsEqualTo(0);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None).WaitAsync(SignalTimeout);
        }
    }

    [Test]
    public async Task ThrowingNotifier_DoesNotStopTheRelay()
    {
        var store = new RowStore();
        var notifier = new ThrowingNotifier();
        using var relay = new OutboxRelayService(store, new ConcurrentSendPublisher(), FastOptions(),
            NullLogger<OutboxRelayService>.Instance, timeProvider: null, notifier);
        await relay.StartAsync(CancellationToken.None);
        try
        {
            // A relay that a notifier could stop has already faulted by the time it would wait.
            var waited = notifier.Waited.Task;
            await Assert.That(await Task.WhenAny(waited, relay.ExecuteTask!).WaitAsync(SignalTimeout))
                .IsSameReferenceAs(waited);
            store.Enqueue(Row(1));

            var drained = store.WaitForEmptyAsync(SignalTimeout);
            await Assert.That(await Task.WhenAny(drained, relay.ExecuteTask!)).IsSameReferenceAs(drained);
            await drained;
            await Assert.That(notifier.OwnershipCalls).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None).WaitAsync(SignalTimeout);
        }
    }

    private static OutboxRelayOptions FastOptions(int bucketCount = 1) => new()
    {
        BucketCount = bucketCount,
        PollInterval = TimeSpan.FromMilliseconds(1),
        ErrorBackoff = TimeSpan.FromMilliseconds(1),
        // The ceiling of the error backoff: low, so a test that waits out several failures stays quick.
        LeaseDuration = TimeSpan.FromSeconds(60),
        LeaseRenewInterval = TimeSpan.FromMilliseconds(20),
        MaxPublishDuration = TimeSpan.FromSeconds(5),
        RelayId = "test-relay"
    };

    private static OutboxRelayService CreateRelay(
        IOutboxStore store, IOutboxPublisher publisher, OutboxRelayOptions options, TimeProvider? time = null) =>
        new(store, publisher, options, NullLogger<OutboxRelayService>.Instance, time);

    private static OutboxRelayService CreateRenewingRelay(IOutboxStore store, IOutboxPublisher publisher, TimeProvider time) =>
        new(store, publisher, new OutboxRelayOptions { RelayId = "relay-a", BucketCount = 1 },
            NullLogger<OutboxRelayService>.Instance, time);

    private static OutboxMessage Row(long id, int bucket = 0) => new()
    {
        Id = id,
        MessageId = Guid.NewGuid(),
        Bucket = bucket,
        Topic = "topic",
        Value = [1],
        CreatedAtUtc = DateTimeOffset.UnixEpoch
    };

    /// <summary>
    /// Fires every timer at once and moves the clock by its due time, so a relay that only
    /// waits between failures runs through them without real delays. Stops firing after
    /// <paramref name="fireLimit"/> timers, which parks the relay in its next wait.
    /// </summary>
    private sealed class AutoAdvanceTimeProvider(int fireLimit) : TimeProvider
    {
        private readonly List<TimeSpan> _delays = [];
        private long _ticks = TimeSpan.TicksPerSecond;

        public TaskCompletionSource LimitReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<TimeSpan> Delays { get { lock (_delays) return [.. _delays]; } }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            int count;
            lock (_delays)
            {
                _delays.Add(dueTime);
                count = _delays.Count;
            }

            if (count > fireLimit)
            {
                LimitReached.TrySetResult();
                return new InertTimer();
            }

            Interlocked.Add(ref _ticks, dueTime.Ticks);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new InertTimer();
        }

        private sealed class InertTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RowStore(IReadOnlyList<int>? ownedBuckets = null) : IOutboxStore
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, List<OutboxMessage>> _rows = [];
        private readonly List<(int Bucket, TaskCompletionSource Empty)> _bucketWaiters = [];
        private readonly TaskCompletionSource _empty = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly IReadOnlyList<int> _ownedBuckets = ownedBuckets ?? [0];
        private int _leaseCalls;
        private int _probeCalls;

        public bool ProbeAlwaysFails { get; init; }
        public HashSet<int> FailingProbes { get; init; } = [];
        public int LeaseCalls => Volatile.Read(ref _leaseCalls);
        public ConcurrentQueue<long> MarkedIds { get; } = [];
        public ConcurrentQueue<int> FetchSizes { get; } = [];

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

        public Task WaitForEmptyAsync(TimeSpan timeout) => _empty.Task.WaitAsync(timeout);

        public Task WaitForBucketEmptyAsync(int bucket, TimeSpan timeout)
        {
            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                if (!_rows.TryGetValue(bucket, out var list) || list.Count == 0)
                    return Task.CompletedTask;
                _bucketWaiters.Add((bucket, waiter));
            }

            return waiter.Task.WaitAsync(timeout);
        }

        public async ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
            OutboxLeaseRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            Interlocked.Increment(ref _leaseCalls);
            return _ownedBuckets;
        }

        public async ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(
            IReadOnlyList<int> buckets, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var call = Interlocked.Increment(ref _probeCalls);
            if (ProbeAlwaysFails || FailingProbes.Contains(call))
                throw new InvalidOperationException("Simulated throttled probe.");

            lock (_lock)
                return [.. buckets.Where(bucket => _rows.TryGetValue(bucket, out var list) && list.Count > 0)];
        }

        public async ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(
            int bucket, int maxCount, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            lock (_lock)
            {
                if (!_rows.TryGetValue(bucket, out var list) || list.Count == 0)
                    return [];
                FetchSizes.Enqueue(maxCount);
                return list.GetRange(0, Math.Min(maxCount, list.Count));
            }
        }

        public async ValueTask MarkPublishedAsync(
            int bucket, IReadOnlyList<OutboxMessage> publishedMessages, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            lock (_lock)
            {
                var list = _rows[bucket];
                foreach (var message in publishedMessages)
                {
                    MarkedIds.Enqueue(message.Id);
                    list.RemoveAll(row => row.Id == message.Id);
                }

                for (var index = _bucketWaiters.Count - 1; index >= 0; index--)
                {
                    if (_bucketWaiters[index].Bucket == bucket && list.Count == 0)
                    {
                        _bucketWaiters[index].Empty.TrySetResult();
                        _bucketWaiters.RemoveAt(index);
                    }
                }

                if (_rows.Values.All(rows => rows.Count == 0))
                    _empty.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Behaves as <see cref="DekafOutboxPublisher"/> does: every row of the batch is sent at
    /// once, so the rows behind a rejected one are delivered although the acknowledged prefix
    /// stops in front of it.
    /// </summary>
    private sealed class ConcurrentSendPublisher : IOutboxPublisher
    {
        private readonly ConcurrentDictionary<long, int> _deliveries = new();
        private readonly ConcurrentDictionary<long, byte> _rejected = new();
        private int _rejectedAttempts;

        public int AttemptsToAwait { get; init; } = int.MaxValue;
        public TaskCompletionSource AttemptsReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Reject(long id) => _rejected[id] = 0;
        public void Accept(long id) => _rejected.TryRemove(id, out _);
        public int Deliveries(long id) => _deliveries.GetValueOrDefault(id);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public async ValueTask<OutboxPublishResult> PublishAsync(
            IReadOnlyList<OutboxMessage> messages, string messageIdHeaderName,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var acked = 0;
            Exception? firstError = null;
            for (var index = 0; index < messages.Count; index++)
            {
                if (_rejected.ContainsKey(messages[index].Id))
                {
                    firstError ??= new InvalidOperationException("Simulated MESSAGE_TOO_LARGE.");
                    if (Interlocked.Increment(ref _rejectedAttempts) >= AttemptsToAwait)
                        AttemptsReached.TrySetResult();
                    continue;
                }

                _deliveries.AddOrUpdate(messages[index].Id, 1, static (_, count) => count + 1);
                if (firstError is null)
                    acked++;
            }

            return new OutboxPublishResult(acked, firstError);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class PausedPublisher : IOutboxPublisher
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<OutboxPublishResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            return new ValueTask<OutboxPublishResult>(Completion.Task);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RenewableStore(ManualTimeProvider time) : IOutboxStore, IOutboxLeaseRenewalStore
    {
        private static readonly IReadOnlyList<int> Buckets = [0];
        private readonly OutboxMessage[] _batch = [new()
        { Id = 1, MessageId = Guid.NewGuid(), Bucket = 0, Topic = "topic", CreatedAtUtc = DateTimeOffset.UnixEpoch }];
        private int _markCount;
        private int _renewals;
        private int _acquisitions;

        internal int ThrowingRenewals { get; init; }
        internal int MarkCount => Volatile.Read(ref _markCount);
        internal TaskCompletionSource RenewalAttempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondAcquisition { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = time;
            if (Interlocked.Increment(ref _acquisitions) == 2)
                SecondAcquisition.TrySetResult();
            return ValueTask.FromResult(Buckets);
        }

        public ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default)
        {
            var throws = Interlocked.Increment(ref _renewals) <= ThrowingRenewals;
            RenewalAttempted.TrySetResult();
            return throws
                ? ValueTask.FromException<bool>(new InvalidOperationException("Simulated throttled renewal."))
                : ValueTask.FromResult(true);
        }

        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<int>>(MarkCount == 0 ? Buckets : []);

        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<OutboxMessage>>(_batch);

        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> publishedMessages,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _markCount);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingNotifier : IOutboxBucketNotifier
    {
        private int _ownershipCalls;

        internal TaskCompletionSource Waited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int OwnershipCalls => Volatile.Read(ref _ownershipCalls);

        public void NotifyCommitted() { }
        public void NotifyCommitted(IReadOnlySet<int> buckets) { }
        public void NotifyCommitted(int bucket) { }

        public void SetOwnedBuckets(IReadOnlyList<int> buckets)
        {
            Interlocked.Increment(ref _ownershipCalls);
            throw new InvalidOperationException("Simulated notifier failure.");
        }

        public ValueTask WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Waited.TrySetResult();
            throw new InvalidOperationException("Simulated notifier failure.");
        }
    }
}
