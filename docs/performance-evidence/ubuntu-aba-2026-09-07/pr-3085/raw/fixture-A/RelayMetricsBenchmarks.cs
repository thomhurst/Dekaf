using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

[MemoryDiagnoser]
[MedianColumn, MaxColumn]
public class RelayMetricsBenchmarks
{
    [ParamsSource(nameof(Modes))]
    public bool Enabled { get; set; }
    public IEnumerable<bool> Modes()
    {
        yield return false;
        yield return true;
    }

    private OutboxRelayService _syncRelay = null!;
    private OutboxRelayService _pendingRelay = null!;
    private Func<CancellationToken, ValueTask> _syncCycle = null!;
    private Func<CancellationToken, ValueTask> _pendingCycle = null!;
    private Store _syncStore = null!;
    private Store _pendingStore = null!;
    private Publisher _syncPublisher = null!;
    private PendingPublisher _pendingPublisher = null!;
    private MeterListener? _listener;
    public long Acknowledged;
    public long Deleted => _syncStore.Deleted + _pendingStore.Deleted;

    [GlobalSetup]
    public void Setup()
    {
        if (Enabled)
        {
            _listener = new MeterListener();
            _listener.InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Dekaf.Outbox")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            {
                if (instrument.Name == "dekaf.outbox.publish.acknowledged")
                    Acknowledged += value;
            });
            _listener.SetMeasurementEventCallback<double>(static (_, _, _, _) => { });
            _listener.Start();
        }
        _syncStore = new Store();
        _pendingStore = new Store();
        _syncPublisher = new Publisher();
        _pendingPublisher = new PendingPublisher();
        _syncRelay = CreateRelay(_syncStore, _syncPublisher);
        _pendingRelay = CreateRelay(_pendingStore, _pendingPublisher);
        _syncCycle = Bind(_syncRelay);
        _pendingCycle = Bind(_pendingRelay);
    }

    internal static OutboxRelayService CreateRelay(Store store, IOutboxPublisher publisher) =>
        new(store, publisher, new OutboxRelayOptions
        {
            RelayId = "benchmark", BucketCount = 1, BatchSize = 501,
            MaxPublishDuration = TimeSpan.FromSeconds(1),
            LeaseDuration = TimeSpan.FromDays(1), LeaseRenewInterval = TimeSpan.FromHours(12)
        }, NullLogger<OutboxRelayService>.Instance);

    internal static Func<CancellationToken, ValueTask> Bind(OutboxRelayService relay)
    {
        var method = typeof(OutboxRelayService).GetMethod("RunCycleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var factory = method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>)
            ? nameof(BindValueTask) : nameof(BindTask);
        return (Func<CancellationToken, ValueTask>)typeof(RelayMetricsBenchmarks)
            .GetMethod(factory, BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(method.ReturnType.GenericTypeArguments[0]).Invoke(null, [relay, method])!;
    }

    private static Func<CancellationToken, ValueTask> BindValueTask<TResult>(OutboxRelayService relay, MethodInfo method)
    {
        var cycle = method.CreateDelegate<Func<CancellationToken, ValueTask<TResult>>>(relay);
        return Invoke;
        [System.Runtime.CompilerServices.AsyncMethodBuilder(typeof(System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder))]
        async ValueTask Invoke(CancellationToken token) => _ = await cycle(token).ConfigureAwait(false);
    }

    private static Func<CancellationToken, ValueTask> BindTask<TResult>(OutboxRelayService relay, MethodInfo method)
    {
        var cycle = method.CreateDelegate<Func<CancellationToken, Task<TResult>>>(relay);
        return Invoke;
        [System.Runtime.CompilerServices.AsyncMethodBuilder(typeof(System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder))]
        async ValueTask Invoke(CancellationToken token) => _ = await cycle(token).ConfigureAwait(false);
    }

    [Benchmark]
    public ValueTask SynchronousBatch() => _syncCycle(default);

    [Benchmark]
    public ValueTask PendingBatch()
    {
        var cycle = _pendingCycle(default);
        _pendingPublisher.Complete();
        return cycle;
    }

    [Benchmark]
    public ValueTask<OutboxPublishResult> PublisherControl() =>
        _syncPublisher.PublishAsync(_syncStore.Rows, "x-outbox-message-id");

    [GlobalCleanup]
    public void Cleanup()
    {
        _syncRelay.Dispose();
        _pendingRelay.Dispose();
        _listener?.Dispose();
    }

    internal sealed class Store : IOutboxStore
    {
        private static readonly int[] Buckets = [0];
        internal readonly OutboxMessage[] Rows = Enumerable.Range(1, 500).Select(id => new OutboxMessage
        {
            Id = id, Bucket = 0, MessageId = Guid.NewGuid(), Topic = "metrics-bench",
            Value = [1], CreatedAtUtc = DateTimeOffset.UnixEpoch
        }).ToArray();
        internal long Deleted;
        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default) => new(Buckets);
        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => new(Buckets);
        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default) => new(Rows);
        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> messages,
            CancellationToken cancellationToken = default)
        {
            Deleted += messages.Count;
            return default;
        }
    }

    private sealed class Publisher : IOutboxPublisher
    {
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default) =>
            new(new OutboxPublishResult(messages.Count, null));
    }

    private sealed class PendingPublisher : IOutboxPublisher, IValueTaskSource<OutboxPublishResult>
    {
        private ManualResetValueTaskSourceCore<OutboxPublishResult> _source;
        private int _count;
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            _source.Reset();
            _count = messages.Count;
            return new(this, _source.Version);
        }
        internal void Complete() => _source.SetResult(new OutboxPublishResult(_count, null));
        public OutboxPublishResult GetResult(short token) => _source.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) => _source.OnCompleted(continuation, state, token, flags);
    }
}
