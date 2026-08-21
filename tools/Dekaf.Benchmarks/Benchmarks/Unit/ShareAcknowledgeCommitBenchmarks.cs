using BenchmarkDotNet.Attributes;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class ShareAcknowledgeCommitBenchmarks
{
    private const string Topic = "shared";
    private const string GroupId = "workers";

    private InMemoryKafkaCluster _matchingCluster = null!;
    private InMemoryKafkaCluster _nonMatchingCluster = null!;
    private InMemoryKafkaCluster _emptyCluster = null!;
    private InMemoryProducer<string, string> _matchingProducer = null!;
    private InMemoryProducer<string, string> _nonMatchingProducer = null!;
    private InMemoryProducer<string, string> _emptyProducer = null!;
    private InMemoryShareConsumer<string, string> _matchingConsumer = null!;
    private InMemoryShareConsumer<string, string> _nonMatchingConsumer = null!;
    private InMemoryShareConsumer<string, string> _emptyConsumer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _matchingCluster = new InMemoryKafkaCluster();
        _matchingCluster.CreateTopic(Topic);
        _matchingProducer = new InMemoryProducer<string, string>(_matchingCluster);
        _matchingConsumer = CreateConsumer(_matchingCluster);

        _nonMatchingCluster = new InMemoryKafkaCluster();
        _nonMatchingCluster.CreateTopic(Topic);
        _nonMatchingCluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.ShareAcknowledge, "other-topic"),
            new InvalidOperationException("unrelated"));
        _nonMatchingProducer = new InMemoryProducer<string, string>(_nonMatchingCluster);
        _nonMatchingConsumer = CreateConsumer(_nonMatchingCluster);

        _emptyCluster = new InMemoryKafkaCluster();
        _emptyCluster.CreateTopic(Topic);
        _emptyProducer = new InMemoryProducer<string, string>(_emptyCluster);
        _emptyConsumer = CreateConsumer(_emptyCluster);

        Prepare(_matchingProducer, _matchingConsumer);
        var barrier = _matchingCluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.ShareAcknowledge, Topic, 0, GroupId));
        barrier.Release();
        _matchingConsumer.CommitAsync().GetAwaiter().GetResult();

        Prepare(_nonMatchingProducer, _nonMatchingConsumer);
        _nonMatchingConsumer.CommitAsync().GetAwaiter().GetResult();

        Prepare(_emptyProducer, _emptyConsumer);
        _emptyConsumer.CommitAsync().GetAwaiter().GetResult();
    }

    [IterationSetup(Target = nameof(CommitMatchingPlan))]
    public void SetupMatchingPlan()
    {
        Prepare(_matchingProducer, _matchingConsumer);
        var barrier = _matchingCluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.ShareAcknowledge, Topic, 0, GroupId));
        barrier.Release();
    }

    [IterationSetup(Target = nameof(CommitNonMatchingPlan))]
    public void SetupNonMatchingPlan() => Prepare(_nonMatchingProducer, _nonMatchingConsumer);

    [IterationSetup(Target = nameof(CommitEmptyPlan))]
    public void SetupEmptyPlan() => Prepare(_emptyProducer, _emptyConsumer);

    [GlobalCleanup]
    public void Cleanup()
    {
        _matchingConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _nonMatchingConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _emptyConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _matchingProducer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _nonMatchingProducer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _emptyProducer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    [InvocationCount(1)]
    public void CommitMatchingPlan() => _matchingConsumer.CommitAsync().GetAwaiter().GetResult();

    [Benchmark]
    [InvocationCount(1)]
    public void CommitNonMatchingPlan() => _nonMatchingConsumer.CommitAsync().GetAwaiter().GetResult();

    [Benchmark]
    [InvocationCount(1)]
    public void CommitEmptyPlan() => _emptyConsumer.CommitAsync().GetAwaiter().GetResult();

    private static InMemoryShareConsumer<string, string> CreateConsumer(InMemoryKafkaCluster cluster)
    {
        var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = GroupId });
        consumer.Subscribe(Topic);
        return consumer;
    }

    private static void Prepare(
        InMemoryProducer<string, string> producer,
        InMemoryShareConsumer<string, string> consumer)
    {
        producer.ProduceAsync(Topic, "key", "value").GetAwaiter().GetResult();
        var record = consumer.PollAsync().FirstAsync().AsTask().GetAwaiter().GetResult();
        consumer.Acknowledge(record);
    }
}
