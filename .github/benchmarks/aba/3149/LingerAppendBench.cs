using BenchmarkDotNet.Attributes;
using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Producer;
using Dekaf;

// Isolates append/rotation/linger bookkeeping. The paired loaded fixture supplies
// actual Kafka completion, latency, CPU and stability evidence separately.
[MemoryDiagnoser]
public class LingerAppendBench
{
    private RecordAccumulator _accumulator = null!;
    private byte[] _value = null!;
    private long _completed;
    private TopicPartition[] _partitions = null!;
    private Func<bool, CancellationToken, ValueTask> _seal = null!;
    private long _appended;

    [Params(1000, 65536)] public int MessageSize { get; set; }
    [Params(1, 3)] public int Partitions { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _value = new byte[MessageSize];
        _partitions = new TopicPartition[Partitions];
        for (var i = 0; i < Partitions; i++)
            _partitions[i] = new TopicPartition("linger-bench", i);
        _accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"], BatchSize = 262144,
            BufferMemory = 64L * 1024 * 1024, LingerMs = 5,
            EnableIdempotence = false
        });
        _seal = typeof(RecordAccumulator).GetMethod("SealBatchesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<Func<bool, CancellationToken, ValueTask>>(_accumulator);
        AppendAndDrain();
        var queue = (ConcurrentQueue<TopicPartition>)typeof(RecordAccumulator)
            .GetField("_lingerPartitions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_accumulator)!;
        if (!queue.IsEmpty || _completed != _appended)
            throw new InvalidOperationException("Burst cleanup left work or notifications behind.");
    }

    [Benchmark(OperationsPerInvoke = 101)]
    public void AppendAndDrain()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < 101; i++)
        {
            var append = _accumulator.AppendFromSpansAsync("linger-bench", i % Partitions,
                timestamp, ReadOnlySpan<byte>.Empty, true, _value, false,
                null, 0, null, CancellationToken.None, partitionCount: Partitions);
            if (!append.IsCompletedSuccessfully || !append.GetAwaiter().GetResult())
                throw new InvalidOperationException("Unexpected backpressure in the synchronous fixture.");
            Drain(_partitions[i % Partitions]);
        }
        _appended += 101;
        // Flush each fixed burst, then sweep outstanding notifications with no current
        // batch. ExpireLingerAsync skips that sweep when the accumulator is empty, so
        // bind the common underlying method once during setup. This identical fixture
        // adaptation bounds the defective baseline; loaded runs keep real scheduling.
        AwaitCompleted(_seal(true, CancellationToken.None));
        foreach (var partition in _partitions)
            Drain(partition);
        AwaitCompleted(_seal(false, CancellationToken.None));
    }

    private static void AwaitCompleted(ValueTask sweep)
    {
        if (!sweep.IsCompletedSuccessfully)
            throw new InvalidOperationException("Unexpected asynchronous linger sweep.");
        sweep.GetAwaiter().GetResult();
    }

    private void Drain(TopicPartition partition)
    {
        while (_accumulator.TryDrainBatch(partition, out var batch))
        {
            _completed += batch!.RecordCount;
            _accumulator.ReleaseMemory(batch.DataSize);
            batch.CompleteSend(_completed, DateTimeOffset.UnixEpoch);
            _accumulator.OnBatchExitsPipeline(batch);
            _accumulator.ReturnReadyBatch(batch);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _accumulator.DisposeAsync();
        if (_completed == 0 || _completed != _appended)
            throw new InvalidOperationException("Not every appended message was drained.");
    }
}
