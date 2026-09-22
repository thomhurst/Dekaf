using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Drains an identical fixed backlog per invocation through the real cycle, with a
/// synchronous store/publisher to isolate scheduling CPU and allocations. Full batches
/// exercise readiness reuse; short batches exercise discovery. With <see cref="Poisoned"/>,
/// one extra bucket holds a head row the publisher always rejects, so the healthy buckets
/// drain beside a bucket that is backing off. No Kafka or database I/O.
/// </summary>
[MemoryDiagnoser]
public class OutboxDrainBenchmarks
{
    [Params(1, 8)]
    public int Buckets { get; set; }
    [Params(1, 64)]
    public int RowsPerBucket { get; set; }
    [Params(false, true)]
    public bool Poisoned { get; set; }

    private Store _store = null!;
    private OutboxRelayService _relay = null!;
    private Func<ValueTask> _cycle = null!;

    [GlobalSetup]
    public void Setup()
    {
        var bucketCount = Poisoned ? Buckets + 1 : Buckets;
        _store = new Store(bucketCount, poisonBucket: Poisoned ? Buckets : -1);
        _relay = new OutboxRelayService(_store, new Publisher(poisonBucket: Poisoned ? Buckets : -1),
            new OutboxRelayOptions { BucketCount = bucketCount, BatchSize = 32 },
            NullLogger<OutboxRelayService>.Instance);
        var cycle = typeof(OutboxRelayService).GetMethod("RunCycleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        _cycle = (Func<ValueTask>)typeof(OutboxDrainBenchmarks).GetMethod(nameof(BindCycle), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(cycle.ReturnType.GetGenericArguments()[0]).Invoke(null, new object[] { _relay, cycle })!;
    }

    private static Func<ValueTask> BindCycle<T>(OutboxRelayService relay, MethodInfo method)
    {
        var cycle = method.CreateDelegate<Func<CancellationToken, ValueTask<T>>>(relay);
        return async () => { await cycle(CancellationToken.None); };
    }

    [Benchmark]
    public async ValueTask<int> DrainBacklog()
    {
        _store.Reset(RowsPerBucket);
        // This is one complete drain workload, not a loop repeating benchmark operations.
        while (_store.Remaining > 0)
            await _cycle();
        return Buckets * RowsPerBucket;
    }

    [GlobalCleanup]
    public void Cleanup() => _relay.Dispose();

    private sealed class Store : IOutboxStore, IOutboxLeaseRenewalStore
    {
        private readonly int[] _buckets;
        private readonly int[] _remaining;
        private readonly OutboxMessage[][] _full;
        private readonly OutboxMessage[][] _short;
        private readonly int _poisonBucket;
        public int Remaining { get; private set; }

        public Store(int buckets, int poisonBucket)
        {
            _poisonBucket = poisonBucket;
            _buckets = new int[buckets];
            _remaining = new int[buckets];
            _full = new OutboxMessage[buckets][];
            _short = new OutboxMessage[buckets][];
            for (var bucket = 0; bucket < buckets; bucket++)
            {
                _buckets[bucket] = bucket;
                _full[bucket] = new OutboxMessage[32];
                for (var index = 0; index < 32; index++)
                    _full[bucket][index] = new OutboxMessage
                    {
                        Id = index + 1, MessageId = Guid.Empty, Bucket = bucket,
                        Topic = "benchmark", CreatedAtUtc = DateTimeOffset.UnixEpoch
                    };
                _short[bucket] = [_full[bucket][0]];
            }
        }
        public void Reset(int rows)
        {
            Array.Fill(_remaining, rows);
            Remaining = rows * _buckets.Length;
            if (_poisonBucket >= 0)
            {
                // The poison row never leaves the store and is not part of the drained backlog.
                _remaining[_poisonBucket] = 1;
                Remaining -= rows;
            }
        }
        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default) => new(_buckets);
        public ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => new(true);
        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => new(_buckets);
        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default)
            => new(_remaining[bucket] == 0 ? Array.Empty<OutboxMessage>() : _remaining[bucket] == 1 ? _short[bucket] : _full[bucket]);
        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> messages,
            CancellationToken cancellationToken = default)
        {
            _remaining[bucket] -= messages.Count;
            Remaining -= messages.Count;
            return ValueTask.CompletedTask;
        }
    }
    private sealed class Publisher(int poisonBucket) : IOutboxPublisher
    {
        private static readonly InvalidOperationException Rejected = new("Simulated MESSAGE_TOO_LARGE.");

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
            => new(messages[0].Bucket == poisonBucket
                ? new OutboxPublishResult(0, Rejected)
                : new OutboxPublishResult(messages.Count, null));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
