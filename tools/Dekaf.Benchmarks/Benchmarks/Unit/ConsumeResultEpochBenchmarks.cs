using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures metadata construction and traversal of retained records with mixed nullable epochs.</summary>
[MemoryDiagnoser]
public class ConsumeResultEpochBenchmarks
{
    private const int Records = 1024;
    private readonly int?[] _epochs = new int?[Records];
    private readonly ConsumeResult<string, string>[] _records = new ConsumeResult<string, string>[Records];

    [GlobalSetup]
    public void Setup()
    {
        for (var index = 0; index < Records; index++)
            _epochs[index] = index % 4 == 0 ? null : index - 512;
        ConstructBatch();
        Validate();
    }

    [Benchmark(OperationsPerInvoke = Records)]
    public void ConstructBatch()
    {
        for (var index = 0; index < Records; index++)
            _records[index] = new ConsumeResult<string, string>("epoch", 0, index,
                "key", "value", null, 0, TimestampType.CreateTime, _epochs[index])
            { ProcessingIndex = index };
    }

    [Benchmark(OperationsPerInvoke = Records)]
    public long ReadRetainedBatch()
    {
        long total = 0;
        for (var index = 0; index < Records; index++)
        {
            ref readonly var record = ref _records[index];
            total += record.LeaderEpoch.GetValueOrDefault() + record.ProcessingIndex;
        }
        return total;
    }

    [GlobalCleanup]
    public void Validate()
    {
        for (var index = 0; index < Records; index++)
        {
            if (_records[index].LeaderEpoch != _epochs[index] || _records[index].ProcessingIndex != index)
                throw new InvalidOperationException("Epoch or completion index changed during the batch workload.");
        }
    }
}
