using BenchmarkDotNet.Attributes;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class KafkaFaultPlanBenchmarks
{
    private readonly KafkaFaultPlan _emptyPlan = new();
    private readonly KafkaFaultScope _scope = new(KafkaFaultOperation.Produce, "orders", 0);

    [Benchmark]
    public ValueTask ApplyEmptyPlan() => _emptyPlan.ApplyAsync(_scope);
}
