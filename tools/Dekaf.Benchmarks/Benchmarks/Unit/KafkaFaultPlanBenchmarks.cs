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
    private InMemoryProducer<byte[], byte[]> _unrelatedFaultProducer = null!;
    private InMemoryProducer<byte[], byte[]> _transactionProducer = null!;
    private ITransaction<byte[], byte[]> _transaction = null!;
    private InMemoryProducer<byte[], byte[]> _unrelatedTransactionProducer = null!;
    private ITransaction<byte[], byte[]> _unrelatedTransaction = null!;

    [IterationSetup(Target = nameof(ProduceEmptyPlan))]
    public void SetupProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        _producer = new InMemoryProducer<byte[], byte[]>(cluster);
    }

    [IterationCleanup(Target = nameof(ProduceEmptyPlan))]
    public void CleanupProducer() => _producer.DisposeAsync().GetAwaiter().GetResult();

    [IterationSetup(Target = nameof(ProduceUnrelatedFaultPlan))]
    public void SetupUnrelatedFaultProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, "orders", partition: 0),
            new InvalidOperationException("consumer only"));
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Produce, "other-topic"),
            new InvalidOperationException("other topic"));
        _unrelatedFaultProducer = new InMemoryProducer<byte[], byte[]>(cluster);
    }

    [IterationCleanup(Target = nameof(ProduceUnrelatedFaultPlan))]
    public void CleanupUnrelatedFaultProducer() =>
        _unrelatedFaultProducer.DisposeAsync().GetAwaiter().GetResult();

    [IterationSetup(Target = nameof(ProduceTransactionEmptyPlan))]
    public void SetupTransactionProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        _transactionProducer = new InMemoryProducer<byte[], byte[]>(cluster);
        _transaction = _transactionProducer.BeginTransaction();
    }

    [IterationCleanup(Target = nameof(ProduceTransactionEmptyPlan))]
    public void CleanupTransactionProducer()
    {
        _transaction.DisposeAsync().GetAwaiter().GetResult();
        _transactionProducer.DisposeAsync().GetAwaiter().GetResult();
    }

    [IterationSetup(Target = nameof(ProduceTransactionUnrelatedFaultPlan))]
    public void SetupUnrelatedTransactionProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, "orders", partition: 0),
            new InvalidOperationException("consumer only"));
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, "other-topic"),
            new InvalidOperationException("other transaction topic"));
        _unrelatedTransactionProducer = new InMemoryProducer<byte[], byte[]>(cluster);
        _unrelatedTransaction = _unrelatedTransactionProducer.BeginTransaction();
    }

    [IterationCleanup(Target = nameof(ProduceTransactionUnrelatedFaultPlan))]
    public void CleanupUnrelatedTransactionProducer()
    {
        _unrelatedTransaction.DisposeAsync().GetAwaiter().GetResult();
        _unrelatedTransactionProducer.DisposeAsync().GetAwaiter().GetResult();
    }

    [Benchmark]
    public ValueTask ApplyEmptyPlan() => _emptyPlan.ApplyAsync(_scope);

    [Benchmark]
    [InvocationCount(131072)]
    public ValueTask<RecordMetadata> ProduceEmptyPlan() =>
        _producer.ProduceAsync("orders", Key, Value);

    [Benchmark]
    [InvocationCount(131072)]
    public ValueTask<RecordMetadata> ProduceUnrelatedFaultPlan() =>
        _unrelatedFaultProducer.ProduceAsync("orders", Key, Value);

    [Benchmark]
    [InvocationCount(1048576)]
    public ValueTask<RecordMetadata> ProduceTransactionEmptyPlan() =>
        _transaction.ProduceAsync("orders", Key, Value);

    [Benchmark]
    [InvocationCount(1048576)]
    public ValueTask<RecordMetadata> ProduceTransactionUnrelatedFaultPlan() =>
        _unrelatedTransaction.ProduceAsync("orders", Key, Value);
}
