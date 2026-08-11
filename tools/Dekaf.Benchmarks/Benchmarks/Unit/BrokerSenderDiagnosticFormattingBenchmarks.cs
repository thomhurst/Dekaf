using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BrokerSenderDiagnosticFormattingBenchmarks
{
    private ReadyBatch[] _batches = null!;

    [Params(16)]
    public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _batches = new ReadyBatch[BatchCount];
        for (var i = 0; i < BatchCount; i++)
            _batches[i] = CreateBatch("benchmark-topic", i);
    }

    [Benchmark(Baseline = true)]
    public string CapturingLinq() => string.Join(", ",
        Enumerable.Range(0, BatchCount)
            .Where(i => _batches[i] is not null)
            .Select(i => $"{_batches[i].TopicPartition.Topic}-{_batches[i].TopicPartition.Partition}"));

    [Benchmark]
    public string IndexedStringCreate() => BrokerSender.FormatBatchKeys(_batches, BatchCount);

    private static ReadyBatch CreateBatch(string topic, int partition)
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = [] },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            recordCount: 0,
            dataSize: 0);
        return batch;
    }
}

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BrokerSenderDisabledDebugLoggingBenchmarks
{
    private readonly ILogger _logger = NullLogger.Instance;
    private ReadyBatch[] _batches = null!;
    private string _sink = string.Empty;

    [Params(16)]
    public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _batches = new ReadyBatch[BatchCount];
        for (var i = 0; i < BatchCount; i++)
            _batches[i] = CreateBatch("benchmark-topic", i);
    }

    [Benchmark(Baseline = true)]
    public int DisabledOriginalCallSite()
    {
        var batches = _batches;
        var count = BatchCount;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _sink = string.Join(", ",
                Enumerable.Range(0, count)
                    .Where(i => batches[i] is not null)
                    .Select(i => $"{batches[i].TopicPartition.Topic}-{batches[i].TopicPartition.Partition}"));
        }

        return batches.Length + count + _sink.Length;
    }

    [Benchmark]
    public int DisabledCurrentCallSite()
    {
        var batches = _batches;
        var count = BatchCount;

        if (_logger.IsEnabled(LogLevel.Debug))
            _sink = BrokerSender.FormatBatchKeys(batches, count);

        return batches.Length + count + _sink.Length;
    }

    private static ReadyBatch CreateBatch(string topic, int partition)
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = [] },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            recordCount: 0,
            dataSize: 0);
        return batch;
    }
}
