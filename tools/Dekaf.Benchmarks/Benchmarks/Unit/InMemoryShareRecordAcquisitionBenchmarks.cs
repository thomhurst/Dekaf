using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[MinIterationTime(500)]
[SimpleJob(RunStrategy.Throughput, launchCount: 3, warmupCount: 8, iterationCount: 12)]
public class InMemoryShareRecordAcquisitionBenchmarks
{
    private const int OperationsPerInvoke = 1024;
    private const string GroupId = "benchmark-group";
    private const string MemberId = "benchmark-member";
    private readonly InMemoryKafkaCluster _cluster = new();
    private readonly TopicPartition _topicPartition = new("benchmark-topic", 0);
    private TopicPartitionOffset[] _benchmarkLeases = null!;
    private ShareGroupMemberRegistration _registration = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _cluster.CreateTopic(_topicPartition.Topic);
        _registration = _cluster.RegisterShareGroupMember(GroupId, MemberId);
        _benchmarkLeases = new TopicPartitionOffset[OperationsPerInvoke];
        await using var producer = new InMemoryProducer<byte[], byte[]>(_cluster);
        await producer.ProduceAsync(_topicPartition.Topic, [], []).ConfigureAwait(false);
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            await producer.ProduceAsync(_topicPartition.Topic, [], []).ConfigureAwait(false);
            _benchmarkLeases[i] = new TopicPartitionOffset(
                _topicPartition.Topic,
                _topicPartition.Partition,
                i + 1);
        }

        _cluster.TryAcquireShareRecord(
            GroupId,
            MemberId,
            _registration,
            _topicPartition,
            offset: 0,
            out _,
            out _);
    }

    [IterationSetup]
    public void ResetBenchmarkLease() =>
        _cluster.ReleaseShareRecords(GroupId, MemberId, _registration, _benchmarkLeases);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int ActiveMemberSuccessfulAcquisition()
    {
        var acquired = 0;
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            if (_cluster.TryAcquireShareRecord(
                    GroupId,
                    MemberId,
                    _registration,
                    _topicPartition,
                    offset: i + 1,
                    out _,
                    out _))
            {
                acquired++;
            }
        }

        return acquired;
    }
}
