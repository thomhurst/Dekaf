using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures metadata snapshot rebuilds on a rack-labelled cluster: a full metadata refresh and an
/// inline leader hint. Neither path uses a partitioner, so neither should build partitioner indexes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ClusterMetadataSnapshotRebuildBenchmarks
{
    private const int TopicCount = 10;
    private const int BrokerCount = 3;

    private ClusterMetadata _metadata = null!;
    private MetadataResponse _response = null!;
    private int _leaderEpoch;

    [Params(100, 1_000)]
    public int PartitionsPerTopic { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var brokers = new BrokerMetadata[BrokerCount];
        for (var i = 0; i < brokers.Length; i++)
        {
            brokers[i] = new BrokerMetadata
            {
                NodeId = i + 1,
                Host = $"broker{i + 1}",
                Port = 9092,
                Rack = $"rack-{i}"
            };
        }

        var topics = new TopicMetadata[TopicCount];
        for (var t = 0; t < topics.Length; t++)
        {
            var partitions = new PartitionMetadata[PartitionsPerTopic];
            for (var p = 0; p < partitions.Length; p++)
            {
                var leader = p % BrokerCount + 1;
                partitions[p] = new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None,
                    PartitionIndex = p,
                    LeaderId = leader,
                    ReplicaNodes = [leader],
                    IsrNodes = [leader]
                };
            }

            topics[t] = new TopicMetadata
            {
                ErrorCode = ErrorCode.None,
                Name = $"topic-{t}",
                Partitions = partitions
            };
        }

        _response = new MetadataResponse { Brokers = brokers, Topics = topics };
        _metadata = new ClusterMetadata();
        _metadata.Update(_response);
    }

    [Benchmark]
    public void Refresh() => _metadata.Update(_response);

    [Benchmark]
    public bool LeaderHint()
    {
        var epoch = ++_leaderEpoch;
        return _metadata.TryUpdatePartitionLeader(
            "topic-0",
            partition: 0,
            leaderId: epoch % BrokerCount + 1,
            leaderEpoch: epoch,
            leaderEndpoint: null);
    }
}
