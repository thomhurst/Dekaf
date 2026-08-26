using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class InMemoryConsumeAsyncOwnershipBenchmarks
{
    private const string Topic = "in-memory-consume-async-ownership";
    private const string GroupId = "in-memory-consume-async-ownership-group";
    private const int NormalRecordCount = 8_192;
    private static readonly TopicPartition Partition = new(Topic, 0);

    private InMemoryConsumer<Ignore, Ignore> _normalConsumer = null!;
    private CancellationTokenSource _normalCancellation = null!;
    private InMemoryKafkaCluster _sharedCluster = null!;
    private InMemoryConsumer<Ignore, Ignore> _sharedConsumer = null!;
    private IAsyncEnumerator<ConsumeResult<Ignore, Ignore>> _sharedStream = null!;
    private KafkaFaultBarrier _sharedBarrier = null!;
    private Task<ConsumeResult<Ignore, Ignore>?> _sharedOwner = null!;
    private ConsumeResult<Ignore, Ignore> _result;

    [GlobalSetup]
    public void Setup()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<Ignore, Ignore>(cluster);
        for (var index = 0; index < NormalRecordCount; index++)
            producer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();

        _normalConsumer = CreateConsumer(cluster);
        _normalConsumer.Subscribe(Topic);
    }

    [IterationSetup(Target = nameof(ConsumeNormalProofPath))]
    public void SetupNormalProofPath()
    {
        _normalCancellation = new CancellationTokenSource();
        _normalConsumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
    }

    [IterationCleanup(Target = nameof(ConsumeNormalProofPath))]
    public void CleanupNormalProofPath() => _normalCancellation.Dispose();

    [Benchmark(OperationsPerInvoke = NormalRecordCount)]
    [InvocationCount(1)]
    public async ValueTask<int> ConsumeNormalProofPath()
    {
        var count = 0;
        await foreach (var result in _normalConsumer
                           .ConsumeAsync(_normalCancellation.Token)
                           .ConfigureAwait(false))
        {
            _result = result;
            if (++count == NormalRecordCount)
                _normalCancellation.Cancel();
        }

        return count;
    }

    [IterationSetup(Target = nameof(ConsumeSharedWaiterPath))]
    public void SetupSharedWaiterPath()
    {
        _sharedCluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<Ignore, Ignore>(_sharedCluster);
        for (var index = 0; index < 3; index++)
            producer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();

        _sharedConsumer = CreateConsumer(_sharedCluster);
        _sharedConsumer.Subscribe(Topic);
        _sharedStream = _sharedConsumer.ConsumeAsync().GetAsyncEnumerator();
        var firstMove = _sharedStream.MoveNextAsync();
        if (!firstMove.IsCompletedSuccessfully || !firstMove.Result)
            throw new InvalidOperationException("Shared-waiter setup could not consume the first record synchronously.");

        _sharedBarrier = _sharedCluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        _sharedOwner = _sharedConsumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        _sharedBarrier.WaitUntilEnteredAsync().GetAwaiter().GetResult();
    }

    [IterationCleanup(Target = nameof(ConsumeSharedWaiterPath))]
    public void CleanupSharedWaiterPath()
    {
        _sharedBarrier.Release();
        _sharedStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _sharedConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(OperationsPerInvoke = 2)]
    [InvocationCount(1)]
    public async ValueTask ConsumeSharedWaiterPath()
    {
        var sharedMove = _sharedStream.MoveNextAsync();
        if (sharedMove.IsCompleted)
            throw new InvalidOperationException("Shared waiter did not suspend behind the proof owner.");
        if (!_sharedBarrier.Release())
            throw new InvalidOperationException("Shared-waiter barrier was already released.");

        _result = await _sharedOwner.ConfigureAwait(false)
            ?? throw new InvalidOperationException("Proof owner did not consume the second record.");
        await _sharedConsumer.CommitAsync().ConfigureAwait(false);
        if (!await sharedMove.ConfigureAwait(false))
            throw new InvalidOperationException("Shared waiter did not resume with the third record.");

        _result = _sharedStream.Current;
    }

    [GlobalCleanup]
    public void Cleanup() => _normalConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();

    private static InMemoryConsumer<Ignore, Ignore> CreateConsumer(InMemoryKafkaCluster cluster) =>
        new(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = true,
                OffsetStoreTiming = OffsetStoreTiming.AfterProcessing
            });
}
