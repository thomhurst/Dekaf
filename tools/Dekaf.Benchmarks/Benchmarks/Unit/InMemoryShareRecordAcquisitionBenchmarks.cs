using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 5, warmupCount: 8, iterationCount: 20)]
public class InMemoryShareRecordAcquisitionBenchmarks
{
    private const int OperationsPerInvoke = 32768;
    private const string GroupId = "benchmark-group";
    private const string MemberId = "benchmark-member";
    // Keep released records available throughout pilot, warmup, and measurement.
    private readonly InMemoryKafkaCluster _cluster = new(new InMemoryKafkaClusterOptions
    {
        DeliveryCountLimit = int.MaxValue
    });
    private readonly TopicPartition _topicPartition = new("benchmark-topic", 0);
    private InMemoryRecord[] _benchmarkLeases = null!;
    private ShareGroupMemberRegistration _registration = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _cluster.CreateTopic(_topicPartition.Topic);
        _cluster.CommitShareOffsets(GroupId, [new TopicPartitionOffset(_topicPartition.Topic, _topicPartition.Partition, 0)]);
        _registration = _cluster.RegisterShareGroupMember(GroupId, MemberId);
        _benchmarkLeases = new InMemoryRecord[OperationsPerInvoke];
        await using var producer = new InMemoryProducer<byte[], byte[]>(_cluster);
        await producer.ProduceAsync(_topicPartition.Topic, [], []).ConfigureAwait(false);
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            await producer.ProduceAsync(_topicPartition.Topic, [], []).ConfigureAwait(false);
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

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int ActiveMemberSuccessfulAcquisitionAndRelease()
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
                    out _benchmarkLeases[i],
                    out _))
            {
                acquired++;
            }
        }

        // Include one batch release per invocation so every invocation measures the same workload.
        _cluster.ReleaseShareRecords(GroupId, MemberId, _registration, _benchmarkLeases);
        if (acquired != OperationsPerInvoke)
            throw new InvalidOperationException("Benchmark records must remain available for successful acquisition.");
        return acquired;
    }
}
