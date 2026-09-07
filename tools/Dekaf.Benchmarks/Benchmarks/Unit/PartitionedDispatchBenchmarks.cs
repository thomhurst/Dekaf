using System.Buffers.Binary;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Compares the production handler paths over one bounded partition lifetime.</summary>
[MemoryDiagnoser]
public class PartitionedDispatchBenchmarks
{
    private const int RecordCount = 128;
    private readonly bool[] _seen = new bool[RecordCount];
    private readonly long[] _lastByKey = new long[RecordCount];
    private ConsumeResult<int, int>[] _records = null!;
    private PartitionProcessor<int, int> _processor = null!;
    private TaskCompletionSource? _release;
    private int _handled;

    [ParamsAllValues]
    public DispatchScenario Scenario { get; set; }

    [Params(false, true)]
    public bool SuspendFirstHandler { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var distinct = Scenario is DispatchScenario.KeyRecordsDistinct or DispatchScenario.KeyBatchesDistinct;
        _records = PartitionedBenchmarkInput.CreateRecords(RecordCount, distinct);
        var options = new PartitionedProcessingOptions
        {
            Ordering = Scenario is DispatchScenario.PartitionRecords or DispatchScenario.PartitionBatches
                ? PartitionedProcessingOrder.Partition : PartitionedProcessingOrder.Key,
            MaxBufferedRecordsPerPartition = RecordCount,
            MaxConcurrentHandlersPerPartition = 1,
            MaxHandlerBatchSize = 16
        };
        var batches = Scenario is DispatchScenario.PartitionBatches
            or DispatchScenario.KeyBatchesRepeated or DispatchScenario.KeyBatchesDistinct;
        var factory = typeof(PartitionedConsumerExtensions).GetMethod(
            batches ? "CreateBatchProcessor" : "CreateRecordProcessor", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(int), typeof(int));
        Delegate handler = batches
            ? (PartitionBatchProcessor<int, int>)HandleBatch
            : (PartitionRecordProcessor<int, int>)HandleRecord;
        _processor = (PartitionProcessor<int, int>)factory.Invoke(null, [handler, options])!;
        await Dispatch();
    }

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public async ValueTask<long> Dispatch()
    {
        Array.Clear(_seen);
        Array.Fill(_lastByKey, -1);
        _handled = 0;
        _release = SuspendFirstHandler ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        var lane = PartitionedBenchmarkInput.CreateLane(RecordCount);
        foreach (var record in _records)
        {
            if (!lane.TryEnqueue(record))
                throw new InvalidOperationException("The bounded input did not fit in the partition queue.");
        }

        // Complete the input without starting PartitionLane's Task.Run wrapper. The actual
        // processor factories still own handler dispatch, storage release and commit tracking.
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var processing = _processor(new PartitionProcessorContext<int, int>(lane), CancellationToken.None);
        var suspended = !processing.IsCompleted && _handled != 0;
        _release?.TrySetResult();
        await processing.AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        if (SuspendFirstHandler && !suspended)
            throw new InvalidOperationException("The first handler did not suspend dispatch.");
        var checkpoint = lane.GetCommitOffset();
        if (_handled != RecordCount || checkpoint is not { Offset: RecordCount, LeaderEpoch: 7 }
            || lane.TryReadMessage(out _))
            throw new InvalidOperationException("Dispatch lost records or produced an incorrect checkpoint.");
        return checkpoint.Value.Offset;
    }

    private ValueTask HandleRecord(PartitionRecordProcessorContext<int, int> context,
        ConsumeResult<int, int> record, CancellationToken cancellationToken)
    {
        var first = _handled == 0;
        Observe(record);
        return first && _release is not null ? new ValueTask(_release.Task) : default;
    }

    private ValueTask HandleBatch(PartitionBatchProcessorContext<int, int> context,
        IReadOnlyList<ConsumeResult<int, int>> records, CancellationToken cancellationToken)
    {
        var first = _handled == 0;
        for (var index = 0; index < records.Count; index++)
            Observe(records[index]);
        return first && _release is not null ? new ValueTask(_release.Task) : default;
    }

    private void Observe(ConsumeResult<int, int> record)
    {
        var offset = checked((int)record.Offset);
        if (_seen[offset] || record.Offset <= _lastByKey[record.Key])
            throw new InvalidOperationException("A record was duplicated or its key was processed out of order.");
        _seen[offset] = true;
        _lastByKey[record.Key] = record.Offset;
        _handled++;
    }

    public enum DispatchScenario
    {
        PartitionRecords,
        PartitionBatches,
        KeyRecordsRepeated,
        KeyRecordsDistinct,
        KeyBatchesRepeated,
        KeyBatchesDistinct
    }
}

/// <summary>Separates partition construction and completion tracking from handler scheduling.</summary>
[MemoryDiagnoser]
public class PartitionedOffsetTrackingBenchmarks
{
    private const int RecordCount = 128;
    private ConsumeResult<int, int>[] _records = null!;

    [GlobalSetup]
    public void Setup() => _records = PartitionedBenchmarkInput.CreateRecords(RecordCount, distinct: false);

    // All three rows use the same denominator: one partition lifetime / 128 records.
    [Benchmark(OperationsPerInvoke = RecordCount)]
    public object CreatePartition() => PartitionedBenchmarkInput.CreateLane(RecordCount);

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public long CompleteInOrder()
    {
        var lane = PartitionedBenchmarkInput.CreateLane(RecordCount);
        foreach (var record in _records)
            lane.MarkProcessed(record);
        return CheckCheckpoint(lane);
    }

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public long CompleteOutOfOrder()
    {
        var lane = PartitionedBenchmarkInput.CreateLane(RecordCount);
        // Establish the low watermark, then leave offset 1 outstanding until all later
        // completions have accumulated. This exercises the real gap-tracking collections.
        lane.MarkProcessed(_records[0]);
        for (var index = RecordCount - 1; index >= 1; index--)
            lane.MarkProcessed(_records[index]);
        return CheckCheckpoint(lane);
    }

    private static long CheckCheckpoint(PartitionLane<int, int> lane)
    {
        var checkpoint = lane.GetCommitOffset();
        if (checkpoint is not { Offset: RecordCount, LeaderEpoch: 7 })
            throw new InvalidOperationException("Completion tracking produced an incorrect checkpoint.");
        return checkpoint.Value.Offset;
    }
}

internal static class PartitionedBenchmarkInput
{
    internal static PartitionLane<int, int> CreateLane(int capacity) => new(
        new TopicPartition("dispatch", 0), capacity,
        static (_, _) => default, static _ => { }, static (_, _) => { });

    internal static ConsumeResult<int, int>[] CreateRecords(int count, bool distinct)
    {
        var records = new ConsumeResult<int, int>[count];
        for (var index = 0; index < count; index++)
        {
            var key = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(key, distinct ? index : 0);
            records[index] = new ConsumeResult<int, int>("dispatch", 0, index,
                key, false, key, false, null, 0, TimestampType.CreateTime, 7,
                Serializers.Int32, Serializers.Int32);
        }
        return records;
    }
}
