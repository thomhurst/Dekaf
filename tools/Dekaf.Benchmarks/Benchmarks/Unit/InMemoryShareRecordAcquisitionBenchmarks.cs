using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class InMemoryShareRecordAcquisitionBenchmarks
{
    private const int OperationsPerInvoke = 1024;
    private const string GroupId = "benchmark-group";
    private const string MemberId = "benchmark-member";
    private readonly InMemoryKafkaCluster _cluster = new();
    private readonly TopicPartition _topicPartition = new("benchmark-topic", 0);
    private ShareGroupMemberRegistration _registration = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cluster.CreateTopic(_topicPartition.Topic);
        _registration = _cluster.RegisterShareGroupMember(GroupId, MemberId);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int ActiveMemberMissingRecord()
    {
        var acquired = 0;
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            if (_cluster.TryAcquireShareRecord(
                    GroupId,
                    MemberId,
                    _registration,
                    _topicPartition,
                    offset: 0,
                    out _,
                    out _))
            {
                acquired++;
            }
        }

        return acquired;
    }
}
