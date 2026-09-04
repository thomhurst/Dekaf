using System.Diagnostics;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class WaveCoalesceProbeBenchmarks
{
    private readonly long _expiredDeadline = 0;
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    [GlobalSetup]
    public void Setup() => _channel.Writer.TryWrite(1);

    [Benchmark(Baseline = true)]
    public bool DeadlineFirst()
    {
        if (Stopwatch.GetTimestamp() >= _expiredDeadline)
            return false;

        return _channel.Reader.TryRead(out _);
    }

    [Benchmark]
    public bool ProbeFirst()
    {
        if (!_channel.Reader.TryRead(out var item))
            return false;

        return _channel.Writer.TryWrite(item);
    }
}

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class WaveCoalesceTailBenchmarks
{
    private long _configuredQuietTicks = 1_000;
    private long _maximumArrivalGapTicks = 100;
    private int _additionalBatchCount = 2;

    [Benchmark(Baseline = true)]
    public long FixedTail() => _configuredQuietTicks;

    [Benchmark]
    public long AdaptiveTail() => BrokerSender.SelectWaveCoalesceTailTicks(
        _configuredQuietTicks,
        _maximumArrivalGapTicks,
        _additionalBatchCount);
}

/// <summary>
/// Per-wave cost of the single-batch spin decision (<see cref="BrokerSender.ShouldMicroLinger"/>).
/// The loaded shapes (multi-batch wave, unflagged single-batch wave) must not move; the two
/// skip shapes show what proving the spin unnecessary costs. The spin these decisions avoid
/// is a full quiet window (75 µs at zero linger, 1 ms at the default) and is measured
/// end-to-end by the Docker-based ProducerSingleBenchmarks.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class WaveCoalesceSoleDemandBenchmarks
{
    private readonly ReadyBatch[] _multiBatchWave = new ReadyBatch[2];
    private readonly ReadyBatch[] _unflaggedWave = new ReadyBatch[1];
    private readonly ReadyBatch[] _soleDemandWave = new ReadyBatch[1];

    [GlobalSetup]
    public void Setup()
    {
        _multiBatchWave[0] = CreateSingleAwaitedRecordBatch(partition: 0);
        _multiBatchWave[1] = CreateSingleAwaitedRecordBatch(partition: 1);
        _unflaggedWave[0] = CreateSingleAwaitedRecordBatch(partition: 0);
        _soleDemandWave[0] = CreateSingleAwaitedRecordBatch(partition: 0);
        _soleDemandWave[0].SealedAsSoleDemand = true;
    }

    [Benchmark(Baseline = true)]
    public bool MultiBatchWave()
        => BrokerSender.ShouldMicroLinger(_multiBatchWave, 2, isTransactional: false);

    [Benchmark]
    public bool UnflaggedSingleBatchWave()
        => BrokerSender.ShouldMicroLinger(_unflaggedWave, 1, isTransactional: false);

    [Benchmark]
    public bool TransactionalSerialWave()
        => BrokerSender.ShouldMicroLinger(_unflaggedWave, 1, isTransactional: true);

    [Benchmark]
    public bool SoleDemandWave()
        => BrokerSender.ShouldMicroLinger(_soleDemandWave, 1, isTransactional: false);

    private static ReadyBatch CreateSingleAwaitedRecordBatch(int partition)
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition("wave-coalesce", partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            [new PooledValueTaskSource<RecordMetadata>()],
            completionSourcesCount: 1,
            recordCount: 1,
            dataSize: 100);
        return batch;
    }
}
