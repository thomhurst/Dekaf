using System.Diagnostics;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for RecordAccumulator.Ready() push-based notification system.
/// Verifies that Ready() drains only notified partitions (O(n_ready)) instead of
/// scanning all partitions (O(n_all)).
/// </summary>
public class RecordAccumulatorReadyTests
{
    private static ProducerOptions CreateTestOptions(int batchSize = 1000, int lingerMs = 10)
    {
        return new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = batchSize,
            LingerMs = lingerMs
        };
    }

    /// <summary>
    /// Creates a MetadataManager with partition leader metadata populated.
    /// </summary>
    private static MetadataManager CreateMetadataManager(string topic, int partitionCount, int nodeId = 1)
    {
        var manager = new MetadataManager(connectionPool: null!, bootstrapServers: ["localhost:9092"]);

        var partitions = new List<PartitionMetadata>();
        for (var i = 0; i < partitionCount; i++)
        {
            partitions.Add(new PartitionMetadata
            {
                ErrorCode = ErrorCode.None,
                PartitionIndex = i,
                LeaderId = nodeId,
                ReplicaNodes = [nodeId],
                IsrNodes = [nodeId]
            });
        }

        var response = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = nodeId, Host = "localhost", Port = 9092 }],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = topic,
                    Partitions = partitions
                }
            ]
        };

        manager.Metadata.Update(response);
        return manager;
    }

    [Test]
    public async Task Ready_AfterSealingBatch_ReturnsReadyNode()
    {
        // Arrange: Create an accumulator with a small batch size so we can seal by filling it
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = CreateMetadataManager("test-topic", 1, nodeId: 1);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Fill the first batch until seal
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.Append("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null, completion, null);
            }

            // Act: Call Ready()
            var readyNodes = new HashSet<int>();
            var (nextCheckDelayMs, unknownLeadersExist) = accumulator.Ready(metadataManager, readyNodes);

            // Assert: Node 1 should be ready (partition 0's leader)
            await Assert.That(readyNodes).Contains(1);
            await Assert.That(unknownLeadersExist).IsFalse();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Ready_NoSealedBatches_ReturnsEmpty()
    {
        // Arrange: Large batch size so appending one record won't seal
        var options = CreateTestOptions(batchSize: 100_000);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = CreateMetadataManager("test-topic", 1, nodeId: 1);

        try
        {
            // Append one record - batch should not seal (large batch size)
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);
            var completion = pool.Rent();

            accumulator.Append("test-topic", 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null, completion, null);

            // Act: Call Ready()
            var readyNodes = new HashSet<int>();
            var (_, unknownLeadersExist) = accumulator.Ready(metadataManager, readyNodes);

            // Assert: No nodes should be ready (batch not sealed yet)
            await Assert.That(readyNodes).IsEmpty();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Ready_MutedPartition_SkipsNotification()
    {
        // Arrange
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = CreateMetadataManager("test-topic", 1, nodeId: 1);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Fill to seal
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.Append("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null, completion, null);
            }

            // Mute the partition before calling Ready
            accumulator.MutePartition(new TopicPartition("test-topic", 0));

            // Act
            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);

            // Assert: No nodes should be ready (partition is muted)
            await Assert.That(readyNodes).IsEmpty();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Ready_UnknownLeader_SetsUnknownLeadersExist()
    {
        // Arrange: Create metadata WITHOUT partition info so leader is unknown
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = new MetadataManager(connectionPool: null!, bootstrapServers: ["localhost:9092"]);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Fill to seal
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.Append("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null, completion, null);
            }

            // Act: Ready() with no metadata
            var readyNodes = new HashSet<int>();
            var (_, unknownLeadersExist) = accumulator.Ready(metadataManager, readyNodes);

            // Assert: No nodes ready, but unknownLeadersExist should be true
            await Assert.That(readyNodes).IsEmpty();
            await Assert.That(unknownLeadersExist).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Ready_CalledTwice_SecondCallReturnsEmpty()
    {
        // Verifies that draining the notification queue means subsequent Ready() calls
        // don't re-process already-drained partitions.
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = CreateMetadataManager("test-topic", 1, nodeId: 1);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Fill to seal
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.Append("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null, completion, null);
            }

            // First Ready() - drains the notification
            var readyNodes1 = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes1);
            await Assert.That(readyNodes1).Contains(1);

            // Second Ready() - no new notifications, should be empty
            var readyNodes2 = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes2);
            await Assert.That(readyNodes2).IsEmpty();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Ready_UnmutePartition_ReNotifiesReadyPartition()
    {
        // Verifies that unmuting a partition with sealed batches triggers a new notification.
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = CreateMetadataManager("test-topic", 1, nodeId: 1);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Fill to seal
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.Append("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null, completion, null);
            }

            // Mute the partition and drain the notification queue
            var tp = new TopicPartition("test-topic", 0);
            accumulator.MutePartition(tp);
            var readyNodes1 = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes1);
            await Assert.That(readyNodes1).IsEmpty();

            // Unmute - this should re-enqueue the notification
            accumulator.UnmutePartition(tp);

            // Now Ready() should find the sealed batch
            var readyNodes2 = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes2);
            await Assert.That(readyNodes2).Contains(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Ready_MultiplePartitions_OnlyReportsReadyOnes()
    {
        // Verifies that with many partitions, only the ones with sealed batches are reported.
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = CreateMetadataManager("test-topic", 10, nodeId: 1);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Only fill partition 3 enough to seal
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.Append("test-topic", 3,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null, completion, null);
            }

            // Append one record to partition 7 (not enough to seal)
            var completion2 = pool.Rent();
            accumulator.Append("test-topic", 7,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null, completion2, null);

            // Act
            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);

            // Assert: Node 1 should be ready (partition 3 has sealed batch)
            await Assert.That(readyNodes).Contains(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Ready_BackoffBatch_DeferredReenqueueBecomesReadyAfterExpiry()
    {
        // Arrange: Create, seal, drain a batch, then reenqueue it with a short backoff.
        // Verify that Ready() defers re-enqueue during backoff (no per-cycle churn),
        // and the partition becomes ready after the backoff timer fires.
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = CreateMetadataManager("test-topic", 1, nodeId: 1);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Fill to seal
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.Append("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null, completion, null);
            }

            // Drain the sealed batch via Ready() + Drain()
            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);
            await Assert.That(readyNodes).Contains(1);

            var drainResult = new Dictionary<int, List<ReadyBatch>>();
            var batchListPool = new Stack<List<ReadyBatch>>();
            accumulator.Drain(metadataManager, readyNodes, int.MaxValue, drainResult, batchListPool);
            await Assert.That(drainResult).ContainsKey(1);

            var batch = drainResult[1][0];

            // Set a short backoff (100ms in the future) and reenqueue
            batch.RetryNotBefore = Stopwatch.GetTimestamp() + (Stopwatch.Frequency / 10); // 100ms
            accumulator.Reenqueue(batch, 0);

            // Act 1: Ready() during backoff - should NOT report the node as ready.
            // The partition is deferred via timer, not immediately re-enqueued.
            var readyNodesDuringBackoff = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodesDuringBackoff);
            await Assert.That(readyNodesDuringBackoff).IsEmpty();

            // A second call should also be empty (no per-cycle churn).
            var readyNodesDuringBackoff2 = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodesDuringBackoff2);
            await Assert.That(readyNodesDuringBackoff2).IsEmpty();

            // Act 2: Wait for backoff + timer to fire, then Ready() should report the node.
            await Task.Delay(200);

            var readyNodesAfterBackoff = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodesAfterBackoff);
            await Assert.That(readyNodesAfterBackoff).Contains(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task ReadyPartitionsQueue_SealViaLinger_EnqueuesNotification()
    {
        // Tests that sealing via linger/flush also pushes to the notification queue.
        // Use large batch size so only linger (not size) triggers seal, with LingerMs=0 for immediate seal.
        var options = CreateTestOptions(batchSize: 100_000, lingerMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var metadataManager = CreateMetadataManager("test-topic", 1, nodeId: 1);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Append one record (batch won't seal from size)
            var completion = pool.Rent();
            accumulator.Append("test-topic", 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null, completion, null);

            // Seal via linger expiry (doesn't wait for delivery like FlushAsync does)
            await accumulator.ExpireLingerAsync(CancellationToken.None);

            // Act: Ready() should find the sealed batch
            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);

            // Assert
            await Assert.That(readyNodes).Contains(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }
}
