using BenchmarkDotNet.Attributes;
using Dekaf.Producer;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class KafkaFaultPlanBenchmarks
{
    private static readonly byte[] Key = [1];
    private static readonly byte[] Value = [2];
    private readonly KafkaFaultPlan _emptyPlan = new();
    private readonly KafkaFaultScope _scope = new(KafkaFaultOperation.Produce, "orders", 0);
    private InMemoryProducer<byte[], byte[]> _producer = null!;

    [IterationSetup(Target = nameof(ProduceEmptyPlan))]
    public void SetupProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        _producer = new InMemoryProducer<byte[], byte[]>(cluster);
    }

    [IterationCleanup(Target = nameof(ProduceEmptyPlan))]
    public void CleanupProducer() => _producer.DisposeAsync().GetAwaiter().GetResult();

    [Benchmark]
    public ValueTask ApplyEmptyPlan() => _emptyPlan.ApplyAsync(_scope);

    [Benchmark]
    [InvocationCount(131072)]
    public ValueTask<RecordMetadata> ProduceEmptyPlan() =>
        _producer.ProduceAsync("orders", Key, Value);
}
