using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class PartitionedBackpressureBenchmarks
{
    private const int Count = 1024;
    private PartitionedConsumerRuntime<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _runtime = null!;
    private PartitionLane<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _lane = null!;
    private Func<ConsumeBatch<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>, CancellationToken, ValueTask> _route = null!;
    private RecordBatch[] _batches = null!;
    private Record[] _records = null!;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AutoResetEvent _start = new(false);
    private readonly AutoResetEvent _complete = new(false);
    private Thread? _reader;
    private int _stopped;

    [Params(1024, 1)]
    public int Capacity { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // AwaitCapacity routing does not call the consumer. A null fixture dependency
        // guarantees that no recording substitute or network work contaminates measurement.
        _runtime = new(null!, static (_, _) => default, new PartitionedProcessingOptions
        {
            BackpressureMode = PartitionBackpressureMode.AwaitCapacity,
            MaxBufferedRecordsPerPartition = Capacity,
            CommitPolicy = PartitionCommitPolicy.UserManaged
        }, null);
        var runtimeType = _runtime.GetType();
        var capacityAvailable = runtimeType.GetMethod("OnLaneCapacityAvailable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<PartitionLane<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>>(_runtime);
        var partition = new TopicPartition("backpressure", 0);
        _lane = new(partition, Capacity, static (_, _) => default, capacityAvailable, static (_, error) => throw error);
        var lanes = (Dictionary<TopicPartition, PartitionLane<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>)
            runtimeType.GetField("_lanes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_runtime)!;
        lanes.Add(partition, _lane);
        _route = runtimeType.GetMethod("RouteBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ConsumeBatch<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>, CancellationToken, ValueTask>>(_runtime);
        _records = new Record[Count];
        for (var index = 0; index < Count; index++)
            _records[index] = new Record { OffsetDelta = index, Key = new byte[8], Value = new byte[128] };
        _batches = new RecordBatch[1];
        if (Capacity < Count)
        {
            _reader = new Thread(Read) { IsBackground = true };
            _reader.Start();
        }
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void RouteBatch()
    {
        // ConsumeBatch is one existing 128-byte wrapper per 1024-record batch.
        // MemoryDiagnoser rounds that amortized cost down; '-' is not zero total bytes.
        var recordBatch = RecordBatch.RentFromPool();
        recordBatch.Records = _records;
        recordBatch.LastOffsetDelta = Count - 1;
        _batches[0] = recordBatch;
        using var pending = PendingFetchData.Create("backpressure", 0, _batches);
        var batch = new ConsumeBatch<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            pending, Serializers.RawBytes, Serializers.RawBytes);
        if (_reader is not null)
        {
            _start.Set();
            _route(batch, _lifetime.Token).AsTask().GetAwaiter().GetResult();
            _complete.WaitOne();
        }
        else
        {
            _route(batch, _lifetime.Token).GetAwaiter().GetResult();
            for (var index = 0; index < Count; index++)
            {
                if (!_lane.TryReadMessage(out var record)) throw new InvalidOperationException("Missing record");
                record.ReleaseStorage();
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Volatile.Write(ref _stopped, 1);
        _start.Set();
        _reader?.Join();
        _lifetime.Cancel();
        (_runtime.GetType().GetField("_capacitySignal", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_runtime) as IDisposable)?.Dispose();
        _lifetime.Dispose();
        _start.Dispose();
        _complete.Dispose();
    }

    private void Read()
    {
        while (_start.WaitOne())
        {
            if (Volatile.Read(ref _stopped) != 0) return;
            for (var index = 0; index < Count; index++)
            {
                ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> record;
                while (!_lane.TryReadMessage(out record))
                {
                    if (Volatile.Read(ref _stopped) != 0) return;
                    Thread.SpinWait(1);
                }
                record.ReleaseStorage();
            }
            _complete.Set();
        }
    }
}
