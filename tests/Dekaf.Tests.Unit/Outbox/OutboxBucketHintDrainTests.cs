using System.Reflection;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxBucketHintDrainTests
{
    [Test]
    public async Task SparseCommit_UsesOnlyFetchAndDelete_AfterInitialDiscovery()
    {
        using var fixture = new Fixture();
        await fixture.Cycle();
        fixture.Add(0);
        fixture.Notifier.NotifyCommitted(0);
        await fixture.Cycle();
        await fixture.Cycle(); // The immediate post-publication cycle must not query again.
        await Assert.That(fixture.Store.Probes).IsEqualTo(1);
        await Assert.That(fixture.Store.Fetches).IsEqualTo(1);
        await Assert.That(fixture.Store.Deletes).IsEqualTo(1);
        await Assert.That(fixture.Store.Rows[0]).IsEmpty();
    }

    [Test]
    public async Task ContinuousLocalHints_DoNotPostponeRemoteDiscovery()
    {
        using var fixture = new Fixture();
        await fixture.Cycle();
        fixture.Add(1); // No notification: committed on a different process.
        for (var index = 0; index < 5; index++)
        {
            fixture.Add(0);
            fixture.Notifier.NotifyCommitted(0);
            fixture.Time.Advance(TimeSpan.FromMilliseconds(200));
            await fixture.Cycle();
        }
        await Assert.That(fixture.Store.Probes).IsEqualTo(2);
        await Assert.That(fixture.Store.Rows[1]).IsEmpty();
    }

    [Test]
    public async Task CommitDuringDiscovery_IsRetainedForNextCycle()
    {
        using var fixture = new Fixture();
        fixture.Store.AfterProbe = () =>
        {
            fixture.Store.AfterProbe = null;
            fixture.Add(1);
            fixture.Notifier.NotifyCommitted(1);
        };
        await fixture.Cycle();
        await fixture.Cycle();
        await Assert.That(fixture.Store.Rows[1]).IsEmpty();
        await Assert.That(fixture.Store.Probes).IsEqualTo(1);
    }

    [Test]
    public async Task CommitDuringPublication_IsRetainedAfterPartialBatch()
    {
        using var fixture = new Fixture();
        await fixture.Cycle();
        fixture.Add(0);
        fixture.Notifier.NotifyCommitted(0);
        fixture.Publisher.OnPublish = () =>
        {
            fixture.Publisher.OnPublish = null;
            fixture.Add(0);
            fixture.Notifier.NotifyCommitted(0);
        };
        await fixture.Cycle();
        await fixture.Cycle();
        await Assert.That(fixture.Store.Rows[0]).IsEmpty();
        await Assert.That(fixture.Store.Fetches).IsEqualTo(2);
        await Assert.That(fixture.Store.Probes).IsEqualTo(1);
    }

    [Test]
    public async Task UnknownHint_ForcesDiscovery_AndLostOwnershipDiscardsHints()
    {
        using var fixture = new Fixture();
        await fixture.Cycle();
        fixture.Add(1);
        fixture.Notifier.NotifyCommitted();
        await fixture.Cycle();
        await Assert.That(fixture.Store.Probes).IsEqualTo(2);
        fixture.Add(1);
        fixture.Notifier.NotifyCommitted(1);
        fixture.Store.Owned = [0];
        fixture.Time.Advance(TimeSpan.FromSeconds(10));
        await fixture.Cycle();
        await fixture.Cycle();
        await Assert.That(fixture.Store.Rows[1].Count).IsEqualTo(1);
        await Assert.That(fixture.Store.Fetches).IsEqualTo(1);
    }

    [Test]
    public async Task FailedHintedBatch_RetriesWithoutWaitingForPollingDeadline()
    {
        using var fixture = new Fixture();
        await fixture.Cycle();
        fixture.Add(0);
        fixture.Notifier.NotifyCommitted(0);
        fixture.Publisher.Fail = true;
        await fixture.Cycle();
        fixture.Publisher.Fail = false;
        await fixture.Cycle();
        await Assert.That(fixture.Store.Rows[0]).IsEmpty();
        await Assert.That(fixture.Store.Probes).IsEqualTo(2);
    }

    [Test]
    public async Task HintsAcrossWords_CoalesceWithoutDroppingBuckets()
    {
        var hints = new OutboxBucketHints(130);
        var output = new int[130];
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var index = 0; index < 1000; index++)
            {
                hints.Add(0);
                hints.Add(65);
                hints.Add(129);
            }
        })));
        var count = hints.Drain(output, out var unknown);
        await Assert.That(output.Take(count)).IsEquivalentTo([0, 65, 129]);
        await Assert.That(unknown).IsFalse();
        await Assert.That(hints.Drain(output, out _)).IsEqualTo(0);
        hints.Add(-1);
        hints.Add(130);
        hints.Drain(output, out unknown);
        await Assert.That(unknown).IsTrue();
    }

    private sealed class Fixture : IDisposable
    {
        public FakeOutboxTimeProvider Time { get; } = new();
        public Store Store { get; } = new();
        public Publisher Publisher { get; } = new();
        public OutboxNotifier Notifier { get; }
        private readonly OutboxRelayService _relay;
        public Func<Task> Cycle { get; }
        private long _id;

        public Fixture()
        {
            Notifier = new OutboxNotifier(Time, 2);
            _relay = new OutboxRelayService(Store, Publisher, new OutboxRelayOptions { BucketCount = 2 },
                NullLogger<OutboxRelayService>.Instance, Time, Notifier);
            Cycle = OutboxTestCycle.Bind(_relay);
        }

        public void Add(int bucket) => Store.Rows[bucket].Add(new OutboxMessage
        {
            Id = ++_id, MessageId = Guid.NewGuid(), Bucket = bucket, Topic = "test", CreatedAtUtc = Time.GetUtcNow()
        });
        public void Dispose() { _relay.Dispose(); Notifier.Dispose(); }
    }

    private sealed class Store : IOutboxStore, IOutboxLeaseRenewalStore
    {
        public List<OutboxMessage>[] Rows { get; } = [[], []];
        public IReadOnlyList<int> Owned { get; set; } = new[] { 0, 1 };
        public int Probes;
        public int Fetches;
        public int Deletes;
        public Action? AfterProbe;
        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request, CancellationToken cancellationToken = default) => new(Owned);
        public ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets, CancellationToken cancellationToken = default) => new(true);
        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets, CancellationToken cancellationToken = default)
        {
            Probes++;
            var result = buckets.Where(bucket => Rows[bucket].Count > 0).ToArray();
            AfterProbe?.Invoke();
            return new(result);
        }
        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount, CancellationToken cancellationToken = default)
        {
            Fetches++;
            return new(Rows[bucket].Take(maxCount).ToArray());
        }
        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> messages, CancellationToken cancellationToken = default)
        {
            Deletes++;
            Rows[bucket].RemoveRange(0, messages.Count);
            return default;
        }
    }

    private sealed class Publisher : IOutboxPublisher
    {
        public Action? OnPublish;
        public bool Fail;
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages, string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            OnPublish?.Invoke();
            return new(Fail ? new OutboxPublishResult(0, new InvalidOperationException("retry")) : new OutboxPublishResult(messages.Count, null));
        }
    }
}

internal static class OutboxTestCycle
{
    public static Func<Task> Bind(OutboxRelayService relay)
    {
        var method = typeof(OutboxRelayService).GetMethod("RunCycleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Func<Task>)typeof(OutboxTestCycle).GetMethod(nameof(BindCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(method.ReturnType.GenericTypeArguments[0]).Invoke(null, [relay, method])!;
    }
    private static Func<Task> BindCore<T>(OutboxRelayService relay, MethodInfo method)
    {
        var cycle = method.CreateDelegate<Func<CancellationToken, ValueTask<T>>>(relay);
        return () => cycle(default).AsTask();
    }
}
