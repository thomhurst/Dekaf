using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// One locally committed partial batch, followed by the post-publication cycle. Uses
/// the real notifier and relay with a fixed clock and synchronous store to isolate CPU,
/// allocations and redundant discovery. The fixture also compiles against the baseline.
/// </summary>
[MemoryDiagnoser]
public class OutboxSparseDrainBenchmarks
{
    [Params(1, 8)]
    public int Buckets { get; set; }
    private ServiceProvider _provider = null!;
    private OutboxRelayService _relay = null!;
    private IOutboxBucketNotifier _notifier = null!;
    private Store _store = null!;
    private Func<ValueTask> _cycle = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var time = new FrozenTimeProvider();
        var options = new OutboxRelayOptions { BucketCount = Buckets };
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(time);
        services.AddDekafOutboxRelay(options);
        _provider = services.BuildServiceProvider();
        _notifier = (IOutboxBucketNotifier)_provider.GetRequiredService<IOutboxNotifier>();
        _store = new Store(Buckets);
        _relay = new OutboxRelayService(_store, new Publisher(), options, NullLogger<OutboxRelayService>.Instance, time, _notifier);
        var method = typeof(OutboxRelayService).GetMethod("RunCycleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _cycle = (Func<ValueTask>)typeof(OutboxSparseDrainBenchmarks).GetMethod(nameof(Bind), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(method.ReturnType.GenericTypeArguments[0]).Invoke(null, [_relay, method])!;
        await _cycle();
    }

    private static Func<ValueTask> Bind<T>(OutboxRelayService relay, MethodInfo method)
    {
        var cycle = method.CreateDelegate<Func<CancellationToken, ValueTask<T>>>(relay);
        return async () => { await cycle(default); };
    }

    [Benchmark]
    public async ValueTask<int> CommitAndDrain()
    {
        _store.Pending = true;
        _store.Commands = 0;
        _notifier.NotifyCommitted(0);
        await _notifier.WaitAsync(TimeSpan.FromSeconds(1));
        await _cycle();
        await _cycle();
        return _store.Commands;
    }

    [GlobalCleanup]
    public void Cleanup() { _relay.Dispose(); _provider.Dispose(); }

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override long GetTimestamp() => 1;
    }
    private sealed class Store(int buckets) : IOutboxStore, IOutboxLeaseRenewalStore
    {
        private readonly int[] _owned = Enumerable.Range(0, buckets).ToArray();
        private readonly int[] _pending = [0];
        private readonly OutboxMessage[] _rows = [new() { Id = 1, Bucket = 0, Topic = "benchmark", MessageId = Guid.Empty, CreatedAtUtc = DateTimeOffset.UnixEpoch }];
        public bool Pending;
        public int Commands;
        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request, CancellationToken cancellationToken = default) => new(_owned);
        public ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets, CancellationToken cancellationToken = default) => new(true);
        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets, CancellationToken cancellationToken = default)
        {
            Commands++;
            return new(Pending ? _pending : Array.Empty<int>());
        }
        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount, CancellationToken cancellationToken = default)
        {
            Commands++;
            return new(Pending ? _rows : Array.Empty<OutboxMessage>());
        }
        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> messages, CancellationToken cancellationToken = default)
        {
            Commands++;
            Pending = false;
            return default;
        }
    }
    private sealed class Publisher : IOutboxPublisher
    {
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages, string messageIdHeaderName, CancellationToken cancellationToken = default)
            => new(new OutboxPublishResult(messages.Count, null));
    }
}
