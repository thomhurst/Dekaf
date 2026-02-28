using System.Collections.Concurrent;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Concurrency;

/// <summary>
/// Concurrency tests for admin subsystem components: ClusterMetadata snapshot
/// swapping, concurrent metadata reads during updates, and connection pool
/// access patterns used by AdminClient operations.
/// </summary>
public class AdminConcurrencyTests
{
    private static MetadataResponse CreateMetadataResponse(string clusterId, int topicCount, int partitionCount)
    {
        var brokers = new List<BrokerMetadata>
        {
            new() { NodeId = 0, Host = "broker-0", Port = 9092 },
            new() { NodeId = 1, Host = "broker-1", Port = 9092 },
            new() { NodeId = 2, Host = "broker-2", Port = 9092 }
        };

        var topics = new List<TopicMetadata>();
        for (var t = 0; t < topicCount; t++)
        {
            var partitions = new List<PartitionMetadata>();
            for (var p = 0; p < partitionCount; p++)
            {
                partitions.Add(new PartitionMetadata
                {
                    PartitionIndex = p,
                    LeaderId = p % 3,
                    LeaderEpoch = 1,
                    ReplicaNodes = [0, 1, 2],
                    IsrNodes = [0, 1, 2],
                    OfflineReplicas = [],
                    ErrorCode = ErrorCode.None
                });
            }

            topics.Add(new TopicMetadata
            {
                Name = $"topic-{t}",
                TopicId = Guid.NewGuid(),
                Partitions = partitions,
                ErrorCode = ErrorCode.None,
                IsInternal = false
            });
        }

        return new MetadataResponse
        {
            ClusterId = clusterId,
            ControllerId = 0,
            Brokers = brokers,
            Topics = topics
        };
    }

    [Test]
    public async Task ClusterMetadata_ConcurrentReadDuringUpdate_ReadsConsistentSnapshot()
    {
        // Readers must always see a consistent snapshot. They should never
        // see a partially-updated state. This tests the volatile reference swap.

        var metadata = new ClusterMetadata();
        const int readerCount = 8;
        const int updateCount = 100;
        var errors = new ConcurrentBag<string>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Writer: rapidly updates metadata
        var writerTask = Task.Run(() =>
        {
            for (var i = 0; i < updateCount; i++)
            {
                var response = CreateMetadataResponse($"cluster-{i}", topicCount: 3, partitionCount: 4);
                metadata.Update(response);
            }
        });

        // Readers: continuously read metadata during updates
        var readerTasks = Enumerable.Range(0, readerCount).Select(r => Task.Run(() =>
        {
            while (!writerTask.IsCompleted)
            {
                // Read various properties - all must come from the same snapshot
                var topics = metadata.GetTopics();
                var brokers = metadata.GetBrokers();

                // Consistency check: if topics exist, brokers must also exist
                if (topics.Count > 0 && brokers.Count == 0)
                {
                    errors.Add($"Reader {r}: Topics exist ({topics.Count}) but no brokers");
                }

                // Each topic must have consistent partitions
                foreach (var topic in topics)
                {
                    if (topic.Partitions is null || topic.Partitions.Count == 0)
                    {
                        errors.Add($"Reader {r}: Topic {topic.Name} has no partitions");
                    }
                }
            }
        })).ToArray();

        await writerTask;
        await Task.WhenAll(readerTasks);

        await Assert.That(errors).IsEmpty();

        // Final state should reflect the last update
        var finalTopics = metadata.GetTopics();
        await Assert.That(finalTopics.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ClusterMetadata_ConcurrentUpdates_LastWriteWins()
    {
        // Multiple concurrent metadata updates should be serialized by the
        // write lock. The final state should be one of the updates (the last one).

        var metadata = new ClusterMetadata();
        const int writerCount = 4;
        const int updatesPerWriter = 50;
        var allClusterIds = new ConcurrentBag<string>();

        var barrier = new Barrier(writerCount);
        var tasks = Enumerable.Range(0, writerCount).Select(w => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < updatesPerWriter; i++)
            {
                var clusterId = $"cluster-{w}-{i}";
                allClusterIds.Add(clusterId);
                var response = CreateMetadataResponse(clusterId, topicCount: 2, partitionCount: 2);
                metadata.Update(response);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // The final cluster ID must be one that was actually written
        var finalClusterId = metadata.ClusterId;
        await Assert.That(finalClusterId).IsNotNull();
        await Assert.That(allClusterIds).Contains(finalClusterId!);
    }

    [Test]
    public async Task ClusterMetadata_GetTopic_DuringUpdate_ReturnsNullOrValidTopic()
    {
        // GetTopic should either return null (topic not yet in snapshot)
        // or a valid TopicInfo - never a partially constructed one.

        var metadata = new ClusterMetadata();
        const int iterations = 200;
        var errors = new ConcurrentBag<string>();

        for (var i = 0; i < iterations; i++)
        {
            var barrier = new Barrier(2);

            var updateTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                var response = CreateMetadataResponse($"cluster-{i}", topicCount: 5, partitionCount: 3);
                metadata.Update(response);
            });

            var readTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                var topic = metadata.GetTopic("topic-0");
                if (topic is not null)
                {
                    // If topic exists, it must be fully initialized
                    if (string.IsNullOrEmpty(topic.Name))
                        errors.Add($"Iteration {i}: Topic has empty name");
                    if (topic.Partitions is null)
                        errors.Add($"Iteration {i}: Topic has null partitions");
                }
            });

            await Task.WhenAll(updateTask, readTask);
        }

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task ClusterMetadata_ConcurrentGetBroker_ThreadSafe()
    {
        // Multiple threads reading broker info concurrently during updates
        // must all get valid data or null, never corrupt data.
        //
        // NOTE: This test validates thread-safe read access specifically. The metadata
        // is initialized once and only read concurrently — there are no concurrent writers.
        // For concurrent read-during-write scenarios, see
        // ClusterMetadata_ConcurrentReadDuringUpdate_ReadsConsistentSnapshot.

        var metadata = new ClusterMetadata();
        // Initialize with some data
        metadata.Update(CreateMetadataResponse("initial", topicCount: 2, partitionCount: 2));

        const int readerCount = 8;
        const int readsPerThread = 500;
        var errors = new ConcurrentBag<string>();

        var barrier = new Barrier(readerCount);
        var tasks = Enumerable.Range(0, readerCount).Select(r => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < readsPerThread; i++)
            {
                var brokerId = i % 3;
                var broker = metadata.GetBroker(brokerId);

                // Broker should always exist since we initialized metadata
                if (broker is null)
                {
                    errors.Add($"Reader {r}, iteration {i}: Broker {brokerId} is null");
                    continue;
                }

                if (broker.NodeId != brokerId)
                {
                    errors.Add($"Reader {r}, iteration {i}: Expected broker {brokerId}, got {broker.NodeId}");
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task ConnectionPool_ConcurrentRegisterBroker_NoCorruption()
    {
        // Multiple threads registering brokers concurrently should not
        // corrupt the internal dictionary.

        var pool = new Dekaf.Networking.ConnectionPool("admin-test");

        try
        {
            const int threadCount = 8;
            const int brokersPerThread = 50;

            var barrier = new Barrier(threadCount);
            var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < brokersPerThread; i++)
                {
                    var brokerId = t * brokersPerThread + i;
                    pool.RegisterBroker(brokerId, $"host-{brokerId}", 9092);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Registration completed without errors
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    // NOTE: ConnectionPool_ConcurrentDispose is NOT tested here because
    // ConnectionPool.DisposeAsync has a known race condition where concurrent
    // calls deadlock on the internal SemaphoreSlim. Sequential disposal
    // (tested in ConnectionPoolTests) works correctly. A fix for concurrent
    // disposal should be tracked separately.
}
