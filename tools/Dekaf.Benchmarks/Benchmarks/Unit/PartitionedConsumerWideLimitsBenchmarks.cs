using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Per-record cost of the consumer-wide partitioned limits on the uncontended path: one lane
/// enqueues and reads records the way the partition-ordered record loop does, taking a handler
/// slot before each read when <see cref="PartitionedProcessingOptions.MaxConcurrentHandlers"/> is set
/// and reserving/releasing the shared buffer budget when
/// <see cref="PartitionedProcessingOptions.MaxBufferedRecords"/> is set. Neither limit is reached.
/// </summary>
[MemoryDiagnoser]
public class PartitionedConsumerWideLimitsBenchmarks
{
    private const int Count = 1024;
    private PartitionLane<int, int> _lane = null!;
    private AsyncHandlerSlotWaiter? _slot;

    [Params(LimitMode.None, LimitMode.Buffer, LimitMode.Handlers, LimitMode.Both)]
    public LimitMode Limits { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var budget = Limits is LimitMode.Buffer or LimitMode.Both
            ? new PartitionedBufferBudget(Count * 2, static () => { }, static () => { })
            : null;
        var handlers = Limits is LimitMode.Handlers or LimitMode.Both ? new HandlerSlotLimiter(4) : null;
        var limits = budget is null && handlers is null ? null : new PartitionedProcessingLimits(budget, handlers);
        _lane = new PartitionLane<int, int>(
            new TopicPartition("consumer-wide-limits", 0),
            Count,
            static (_, _) => default,
            static _ => { },
            static (_, error) => throw error,
            limits);
        _slot = handlers is null ? null : new AsyncHandlerSlotWaiter(handlers);
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void EnqueueAndRead()
    {
        for (var offset = 0; offset < Count; offset++)
        {
            var record = new ConsumeResult<int, int>("consumer-wide-limits", 0, offset, offset, 0,
                null, 0, TimestampType.CreateTime, 7);
            if (!_lane.TryEnqueue(record, completionBatch: null))
                throw new InvalidOperationException("The lane rejected a record below its limits.");
        }

        var slot = _slot;
        for (var index = 0; index < Count; index++)
        {
            if (slot is not null && !slot.TryAcquire())
                throw new InvalidOperationException("The handler limit was reached.");
            if (!_lane.TryReadMessage(out var message))
                throw new InvalidOperationException("Missing record.");
            message.ReleaseStorage();
            slot?.Release();
        }
    }

    public enum LimitMode
    {
        None,
        Buffer,
        Handlers,
        Both
    }
}
