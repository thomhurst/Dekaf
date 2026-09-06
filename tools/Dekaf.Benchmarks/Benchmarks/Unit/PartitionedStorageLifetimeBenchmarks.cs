using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures queue ownership transfer without handler or commit-tracker allocations.</summary>
[MemoryDiagnoser]
public class PartitionedStorageLifetimeBenchmarks
{
    private PartitionLane<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _lane = null!;
    private PendingFetchData _pending = null!;
    private ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _record;
    private readonly AutoResetEvent _start = new(false);
    private readonly AutoResetEvent _complete = new(false);
    private Thread _reader = null!;
    private int _stopped;

    [GlobalSetup]
    public void Setup()
    {
        _pending = PendingFetchData.Create("lifetime", 0,
            [new RecordBatch { Records = [new Record { Key = new byte[8], Value = new byte[128] }] }]);
        _pending.EagerParseAll();
        var batch = new ConsumeBatch<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            _pending, Serializers.RawBytes, Serializers.RawBytes);
        var enumerator = batch.GetEnumerator();
        enumerator.MoveNext();
        _record = enumerator.Current;
        _lane = new PartitionLane<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new TopicPartition("lifetime", 0), 1024, static (_, _) => default, static _ => { }, static (_, _) => { });
        _reader = new Thread(ReadConcurrent) { IsBackground = true };
        _reader.Start();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Volatile.Write(ref _stopped, 1);
        _start.Set();
        _reader.Join();
        _pending.Dispose();
        _start.Dispose();
        _complete.Dispose();
    }

    [Benchmark(OperationsPerInvoke = 1024)]
    public void QueueAndComplete()
    {
        for (var index = 0; index < 1024; index++)
            _lane.TryEnqueue(_record);
        for (var index = 0; index < 1024; index++)
        {
            _lane.TryReadMessage(out var message);
            message.ReleaseStorage();
        }
    }

    [Benchmark(OperationsPerInvoke = 1024)]
    public void QueueAndCompleteConcurrent()
    {
        _start.Set();
        for (var index = 0; index < 1024; index++)
        {
            while (!_lane.TryEnqueue(_record))
                Thread.SpinWait(1);
        }
        _complete.WaitOne();
    }

    [Benchmark]
    public object CreateKeyLane() =>
        new KeyOrderedProcessingLane<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(null!, default);

    [Benchmark]
    public void FetchLifecycle()
    {
        using var pending = PendingFetchData.Create("lifetime", 0, Array.Empty<RecordBatch>());
    }

    private void ReadConcurrent()
    {
        while (_start.WaitOne())
        {
            if (Volatile.Read(ref _stopped) != 0)
                return;
            for (var index = 0; index < 1024; index++)
            {
                ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> message;
                while (!_lane.TryReadMessage(out message))
                    Thread.SpinWait(1);
                message.ReleaseStorage();
            }
            _complete.Set();
        }
    }
}
