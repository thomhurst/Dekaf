using System.Buffers;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Routes warmed parsed batches, including filters that deliver no records.</summary>
[MemoryDiagnoser]
public class PartitionedBatchRoutingBenchmarks
{
    private Func<ConsumeBatch<Ignore, ReadOnlyMemory<byte>>, CancellationToken, ValueTask> _route = null!;
    private PartitionLane<Ignore, ReadOnlyMemory<byte>> _lane = null!;
    private readonly RecordBatch[] _source = new RecordBatch[1];
    private readonly RejectAllFilter _filter = new();
    private Record[] _records = null!;
    private long _offset;

    [Params(1, 64)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool FilterAll { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var runtime = new PartitionedConsumerRuntime<Ignore, ReadOnlyMemory<byte>>(null!, static (_, _) => default,
            new PartitionedProcessingOptions { CommitPolicy = PartitionCommitPolicy.UserManaged }, null);
        var partition = new TopicPartition("batch-routing", 0);
        _lane = new PartitionLane<Ignore, ReadOnlyMemory<byte>>(partition, RecordCount + 1,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        var runtimeType = runtime.GetType();
        var lanes = (Dictionary<TopicPartition, PartitionLane<Ignore, ReadOnlyMemory<byte>>>)
            runtimeType.GetField("_lanes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        lanes.Add(partition, _lane);
        _route = runtimeType.GetMethod("RouteBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ConsumeBatch<Ignore, ReadOnlyMemory<byte>>, CancellationToken, ValueTask>>(runtime);

        var value = new ArrayBufferWriter<byte>();
        Serializers.Int32.Serialize(42, ref value, default);
        _records = new Record[RecordCount];
        for (var index = 0; index < _records.Length; index++)
            _records[index] = new Record { OffsetDelta = index, IsKeyNull = true, Value = value.WrittenMemory };
        if (RouteBatch() != (FilterAll ? 0 : RecordCount))
            throw new InvalidOperationException("Routing did not preserve filter decisions.");
    }

    // Includes batch construction and cleanup equally on both revisions. Source
    // records and serialized payloads are reused; completion costs are per batch.
    [Benchmark]
    public int RouteBatch()
    {
        var source = RecordBatch.RentFromPool();
        source.BaseOffset = _offset;
        source.LastOffsetDelta = RecordCount - 1;
        source.Records = _records;
        _source[0] = source;
        using var pending = PendingFetchData.Create("batch-routing", 0, _source);
        pending.EagerParseAll();
        var batch = new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(pending, Serializers.Ignore, Serializers.RawBytes,
            recordFilter: FilterAll ? _filter : null);
        _route(batch, default).GetAwaiter().GetResult();
        var delivered = 0;
        while (_lane.TryReadMessage(out var record))
        {
            _lane.MarkProcessed(record);
            record.ReleaseStorage();
            delivered++;
        }
        _offset += RecordCount;
        return delivered;
    }

    [GlobalCleanup]
    public void Cleanup() => _lane.EnableAutomaticCompletion();

    private sealed class RejectAllFilter : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context) => false;
    }
}
