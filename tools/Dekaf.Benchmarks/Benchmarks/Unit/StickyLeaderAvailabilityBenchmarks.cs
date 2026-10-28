using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures a sticky switch on every record against real metadata in which a tenth of the
/// partitions have no leader. The leader index is built once per snapshot, so steady-state
/// rotation must stay at 0 B per record.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class StickyLeaderAvailabilityBenchmarks
{
    private const string Topic = "sticky-leader-availability";
    private const int OperationsPerInvoke = 1_000;

    private DefaultPartitioner _partitioner = null!;
    private IUniformStickyPartitioner _uniformPartitioner = null!;

    [Params(10, 1_000)]
    public int PartitionCount { get; set; }

    [Params(false, true)]
    public bool RackAware { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var partitions = new PartitionMetadata[PartitionCount];
        for (var p = 0; p < partitions.Length; p++)
        {
            var leader = p % 10 == 9 ? -1 : p % 3 + 1;
            partitions[p] = new PartitionMetadata
            {
                ErrorCode = ErrorCode.None,
                PartitionIndex = p,
                LeaderId = leader,
                ReplicaNodes = [1],
                IsrNodes = [1]
            };
        }

        var metadata = new ClusterMetadata();
        metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker1", Port = 9092, Rack = "rack-a" },
                new BrokerMetadata { NodeId = 2, Host = "broker2", Port = 9092, Rack = "rack-b" },
                new BrokerMetadata { NodeId = 3, Host = "broker3", Port = 9092, Rack = "rack-c" },
            ],
            Topics = [new TopicMetadata { ErrorCode = ErrorCode.None, Name = Topic, Partitions = partitions }]
        });

        _partitioner = new DefaultPartitioner(
            stickyBatchSize: 1,
            adaptivePartitioning: false,
            availabilityTimeoutMs: 0,
            ignoreKeys: false,
            rackAwarePartitioning: RackAware,
            clientRack: "rack-a");
        _uniformPartitioner = _partitioner;
        _uniformPartitioner.SetRackLocalPartitionsProvider(metadata.GetPartitionsForRack);
        _uniformPartitioner.SetAvailablePartitionsProvider(metadata.GetAvailablePartitions);
        RotateBatch();
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int RotateBatch()
    {
        var partition = 0;
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            partition = _partitioner.Partition(
                Topic,
                ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                PartitionCount);
            _uniformPartitioner.OnRecordAppended(Topic, partition, bytes: 1, PartitionCount);
        }

        return partition;
    }
}
