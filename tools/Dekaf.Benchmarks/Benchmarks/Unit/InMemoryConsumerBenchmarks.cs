using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class InMemoryConsumerBenchmarks
{
    private const string Topic = "in-memory-consumer";
    private const string GroupId = "benchmark-group";
    private const int SnapshotRecordCount = 256;
    private static readonly TopicPartitionOffset StoredOffset = new(Topic, 0, 1);
    private static readonly IReadOnlyList<TopicPartitionOffset> ExplicitOffsets = [StoredOffset];
    private InMemoryConsumer<Ignore, Ignore> _consumer = null!;
    private InMemoryConsumer<Ignore, Ignore> _manualCommitFaultConsumer = null!;
    private InMemoryConsumer<Ignore, Ignore> _unrelatedFaultConsumer = null!;
    private InMemoryConsumer<Ignore, Ignore> _asyncAutoCommitConsumer = null!;
    private InMemoryConsumer<Ignore, Ignore> _noStoreCommitFaultConsumer = null!;
    private InMemoryConsumer<Ignore, Ignore> _customPlanStoredOffsetConsumer = null!;
    private InMemoryConsumer<Ignore, Ignore> _snapshotConsumer = null!;
    private ConsumeResult<Ignore, Ignore>? _result;

    [GlobalSetup]
    public void Setup()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<Ignore, Ignore>(cluster);
        producer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();
        _consumer = new InMemoryConsumer<Ignore, Ignore>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        _consumer.Subscribe(Topic);

        var manualCommitFaultCluster = new InMemoryKafkaCluster();
        var manualCommitFaultProducer = new InMemoryProducer<Ignore, Ignore>(manualCommitFaultCluster);
        manualCommitFaultProducer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();
        manualCommitFaultCluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            new InvalidOperationException("commit-only"));
        _manualCommitFaultConsumer = new InMemoryConsumer<Ignore, Ignore>(
            manualCommitFaultCluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        _manualCommitFaultConsumer.Subscribe(Topic);

        var unrelatedFaultCluster = new InMemoryKafkaCluster();
        var unrelatedFaultProducer = new InMemoryProducer<Ignore, Ignore>(unrelatedFaultCluster);
        unrelatedFaultProducer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();
        unrelatedFaultCluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(
                KafkaFaultOperation.Fetch,
                topic: "other-topic",
                partition: 0,
                groupId: GroupId),
            new InvalidOperationException("unrelated"));
        _unrelatedFaultConsumer = new InMemoryConsumer<Ignore, Ignore>(
            unrelatedFaultCluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        _unrelatedFaultConsumer.Subscribe(Topic);

        _consumer.StoreOffset(StoredOffset);
        _unrelatedFaultConsumer.StoreOffset(StoredOffset);

        var asyncAutoCommitCluster = new InMemoryKafkaCluster();
        var asyncAutoCommitProducer = new InMemoryProducer<Ignore, Ignore>(asyncAutoCommitCluster);
        asyncAutoCommitProducer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();
        var completedDeserializer = new CompletedIgnoreDeserializer();
        _asyncAutoCommitConsumer = new InMemoryConsumer<Ignore, Ignore>(
            asyncAutoCommitCluster,
            completedDeserializer,
            completedDeserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = true,
                OffsetCommitMode = OffsetCommitMode.Auto,
                OffsetStoreTiming = OffsetStoreTiming.OnDelivery
            });
        _asyncAutoCommitConsumer.Subscribe(Topic);

        var noStoreCommitFaultCluster = new InMemoryKafkaCluster();
        var noStoreCommitFaultProducer = new InMemoryProducer<Ignore, Ignore>(noStoreCommitFaultCluster);
        noStoreCommitFaultProducer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();
        noStoreCommitFaultCluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            new InvalidOperationException("commit-only"));
        _noStoreCommitFaultConsumer = new InMemoryConsumer<Ignore, Ignore>(
            noStoreCommitFaultCluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                OffsetCommitMode = OffsetCommitMode.Auto
            });
        _noStoreCommitFaultConsumer.Subscribe(Topic);

        var customInnerPlan = new KafkaFaultPlan();
        var customPlanCluster = new InMemoryKafkaCluster(
            new InMemoryKafkaClusterOptions(),
            new DelegatingFaultPlan(customInnerPlan));
        var customPlanProducer = new InMemoryProducer<Ignore, Ignore>(customPlanCluster);
        customPlanProducer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();
        customInnerPlan.FailPersistently(
            new KafkaFaultScope(
                KafkaFaultOperation.Commit,
                topic: "other-topic",
                partition: 0,
                groupId: GroupId),
            new InvalidOperationException("unrelated commit"));
        _customPlanStoredOffsetConsumer = new InMemoryConsumer<Ignore, Ignore>(
            customPlanCluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                OffsetCommitMode = OffsetCommitMode.Auto
            });
        _customPlanStoredOffsetConsumer.Subscribe(Topic);
        for (var partition = 1; partition <= 1024; partition++)
        {
            _customPlanStoredOffsetConsumer.StoreOffset(
                new TopicPartitionOffset("other-topic", partition, 1));
        }
        _customPlanStoredOffsetConsumer.StoreOffset(StoredOffset);

        var snapshotCluster = new InMemoryKafkaCluster();
        var snapshotProducer = new InMemoryProducer<Ignore, Ignore>(snapshotCluster);
        for (var i = 0; i < SnapshotRecordCount; i++)
            snapshotProducer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();

        _snapshotConsumer = new InMemoryConsumer<Ignore, Ignore>(
            snapshotCluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        _snapshotConsumer.Subscribe(Topic);
    }

    [Benchmark]
    [InvocationCount(131072)]
    public void ConsumeOneNoFault()
    {
        _consumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var operation = _consumer.ConsumeOneAsync(TimeSpan.Zero);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("No-fault consume did not complete synchronously.");

        _result = operation.Result;
    }

    [Benchmark]
    [InvocationCount(131072)]
    public void ConsumeOneUnrelatedFault()
    {
        _unrelatedFaultConsumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var operation = _unrelatedFaultConsumer.ConsumeOneAsync(TimeSpan.Zero);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("Unrelated-fault consume did not complete synchronously.");

        _result = operation.Result;
    }

    [Benchmark]
    [InvocationCount(262144)]
    public void ConsumeOneManualCommitFault()
    {
        _manualCommitFaultConsumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var operation = _manualCommitFaultConsumer.ConsumeOneAsync(TimeSpan.Zero);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("Manual commit fault consume did not complete synchronously.");

        _result = operation.Result;
    }

    [Benchmark]
    [InvocationCount(131072)]
    public void ConsumeOneAsyncAutoCommitNoFault()
    {
        _asyncAutoCommitConsumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var operation = _asyncAutoCommitConsumer.ConsumeOneAsync(TimeSpan.Zero);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("Async auto-commit consume did not complete synchronously.");

        _result = operation.Result;
    }

    [Benchmark]
    [InvocationCount(131072)]
    public void ConsumeOneAutoCommitNoStoredOffset()
    {
        _noStoreCommitFaultConsumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var operation = _noStoreCommitFaultConsumer.ConsumeOneAsync(TimeSpan.Zero);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("No-stored-offset consume did not complete synchronously.");

        _result = operation.Result;
    }

    [Benchmark]
    [InvocationCount(131072)]
    public void ConsumeOneCustomPlanStoredOffset()
    {
        _customPlanStoredOffsetConsumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var operation = _customPlanStoredOffsetConsumer.ConsumeOneAsync(TimeSpan.Zero);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("Custom-plan consume did not complete synchronously.");

        _result = operation.Result;
    }

    [Benchmark]
    public async Task<int> ConsumeSnapshotNoFault()
    {
        _snapshotConsumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var count = 0;
        await foreach (var result in _snapshotConsumer.ConsumeSnapshotAsync().ConfigureAwait(false))
        {
            _result = result;
            count++;
        }

        if (count != SnapshotRecordCount)
            throw new InvalidOperationException("Snapshot benchmark consumed an unexpected record count.");

        return count;
    }

    [Benchmark]
    [InvocationCount(4194304)]
    public void StoreOffsetNoFault() => _consumer.StoreOffset(StoredOffset);

    [Benchmark]
    [InvocationCount(4194304)]
    public void StoreOffsetUnrelatedFault() =>
        _unrelatedFaultConsumer.StoreOffset(StoredOffset);

    [Benchmark]
    [InvocationCount(131072)]
    public void CommitExplicitNoFault()
    {
        var operation = _consumer.CommitAsync(ExplicitOffsets);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("No-fault commit did not complete synchronously.");

        operation.GetAwaiter().GetResult();
    }

    private sealed class CompletedIgnoreDeserializer : IAsyncDeserializer<Ignore>
    {
        public ValueTask<Ignore> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(default(Ignore));
    }

    private sealed class DelegatingFaultPlan(KafkaFaultPlan inner) : IKafkaFaultPlan
    {
        public event Action<KafkaFaultObservation>? FaultConsumed
        {
            add => inner.FaultConsumed += value;
            remove => inner.FaultConsumed -= value;
        }

        public int Count => inner.Count;

        public long Version => inner.Version;

        public bool HasMatchingFault(in KafkaFaultScope operationScope) =>
            inner.HasMatchingFault(operationScope);

        public bool HasPotentialFault(
            KafkaFaultOperation operation,
            string? groupId,
            IReadOnlySet<TopicPartition> resources) =>
            inner.HasPotentialFault(operation, groupId, resources);

        public bool TryGetFirstMatchingFaultScope(
            ReadOnlySpan<KafkaFaultScope> operationScopes,
            out KafkaFaultScope operationScope) =>
            inner.TryGetFirstMatchingFaultScope(operationScopes, out operationScope);

        public bool TryApplyFirstMatchingFault(
            ReadOnlySpan<KafkaFaultScope> operationScopes,
            out ValueTask application,
            CancellationToken cancellationToken = default) =>
            inner.TryApplyFirstMatchingFault(operationScopes, out application, cancellationToken);

        public void Fail(KafkaFaultScope scope, Exception exception, int occurrenceCount = 1) =>
            inner.Fail(scope, exception, occurrenceCount);

        public void FailPersistently(KafkaFaultScope scope, Exception exception) =>
            inner.FailPersistently(scope, exception);

        public KafkaFaultBarrier PauseNext(KafkaFaultScope scope) => inner.PauseNext(scope);

        public ValueTask ApplyAsync(
            KafkaFaultScope operationScope,
            CancellationToken cancellationToken = default) =>
            inner.ApplyAsync(operationScope, cancellationToken);

        public int Clear(KafkaFaultScope scope) => inner.Clear(scope);

        public int Clear() => inner.Clear();
    }
}
