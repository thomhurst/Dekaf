using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class BrokerPrefetchSchedulerBenchmarks
{
    private readonly BrokerPrefetchScheduler _scheduler = new();

    [GlobalSetup]
    public void Setup()
    {
        if (!_scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), static () => Task.CompletedTask))
            throw new InvalidOperationException("Could not register benchmark task.");
    }

    [Benchmark]
    public ValueTask WaitForSingleBrokerAsync()
        => _scheduler.WaitForAnyAsync(CancellationToken.None);
}
