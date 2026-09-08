using BenchmarkDotNet.Attributes;
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
        AppendAndDrain();
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
        // Bound queued notifications in the baseline between repeated invocations.
        // The loaded fixture deliberately retains the real producer sweep cadence.
        var linger = _accumulator.ExpireLingerAsync(CancellationToken.None);
        if (!linger.IsCompletedSuccessfully)
            throw new InvalidOperationException("Unexpected asynchronous linger sweep.");
        linger.GetAwaiter().GetResult();
        foreach (var partition in _partitions)
            Drain(partition);
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
        if (_completed == 0)
            throw new InvalidOperationException("The fixture never rotated and drained a batch.");
    }
}
