using BenchmarkDotNet.Attributes;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class KafkaFaultOperationProbeBenchmarks
{
    private readonly KafkaFaultPlan _emptyPlan = new();
    private readonly KafkaFaultPlan _unrelatedPlan = CreateUnrelatedPlan();

    [Benchmark(Baseline = true)]
    public bool NoProbe() => false;

    [Benchmark]
    public bool EmptyPlanProbe() =>
        _emptyPlan.HasPotentialMatch(KafkaFaultOperation.ShareConsume);

    [Benchmark]
    public bool UnrelatedPlanProbe() =>
        _unrelatedPlan.HasPotentialMatch(KafkaFaultOperation.ShareConsume);

    private static KafkaFaultPlan CreateUnrelatedPlan()
    {
        var plan = new KafkaFaultPlan();
        plan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Admin),
            new InvalidOperationException("admin only"));
        return plan;
    }
}
