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
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
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

            accumulator.TryAppendWithCompletion("test-topic", 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, completion);

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
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
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
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
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
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
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
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
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
                accumulator.TryAppendWithCompletion("test-topic", 3,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
            }

            // Append one record to partition 7 (not enough to seal)
            var completion2 = pool.Rent();
            accumulator.TryAppendWithCompletion("test-topic", 7,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, completion2);

            // Act
            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);

            // Assert: Node 1 should be ready (partition 3 has sealed batch)
            await Assert.That(readyNodes).Count().IsEqualTo(1);
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
    public async Task Ready_BackoffBatch_ActiveBackoff_DoesNotReport()
    {
        // Arrange: Create, seal, drain a batch, then reenqueue it with a far-future backoff.
        // Verify that Ready() does not report the node while backoff is active.
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
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
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

            // Set backoff far in the future (10 seconds) and reenqueue
            batch.RetryNotBefore = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * 10);
            accumulator.Reenqueue(batch, 0);

            // Act: Ready() during backoff - should NOT report the node as ready.
            var readyNodesDuringBackoff = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodesDuringBackoff);
            await Assert.That(readyNodesDuringBackoff).IsEmpty();

            // A second call should also be empty (no per-cycle churn).
            var readyNodesDuringBackoff2 = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodesDuringBackoff2);
            await Assert.That(readyNodesDuringBackoff2).IsEmpty();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Ready_BackoffBatch_ExpiredBackoff_ReportsReady()
    {
        // Arrange: Create, seal, drain a batch, then reenqueue it with an already-expired backoff.
        // Verify that Ready() reports the node immediately when backoff is in the past.
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
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
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

            // Set backoff in the past (already expired) and reenqueue
            batch.RetryNotBefore = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            accumulator.Reenqueue(batch, 0);

            // Act: Ready() should report the node immediately (backoff already expired).
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
            accumulator.TryAppendWithCompletion("test-topic", 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, completion);

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

    [Test]
    public async Task Drain_MaxRequestSizeExceeded_ReenqueuesSkippedPartitions()
    {
        // Regression test: when DrainBatchesForOneNode hits maxRequestSize and breaks,
        // the skipped partitions' notifications were lost — their batches stayed in the deque
        // forever because Ready() had already consumed the notifications.
        var options = CreateTestOptions(batchSize: 50);
        await using var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        // All 3 partitions on the same node
        await using var metadataManager = CreateMetadataManager("test-topic", 3, nodeId: 1);

        var pooledKey = new PooledMemory(null, 0, isNull: true);
        var pooledValue = new PooledMemory(null, 0, isNull: true);

        // Seal batches on all 3 partitions
        for (var p = 0; p < 3; p++)
        {
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.TryAppendWithCompletion("test-topic", p,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
            }
        }

        // Ready() consumes notifications for all 3 partitions
        var readyNodes = new HashSet<int>();
        accumulator.Ready(metadataManager, readyNodes);
        await Assert.That(readyNodes).Contains(1);

        // Drain with a tiny maxRequestSize so only 1 partition's batch fits.
        // This should re-enqueue notifications for the other 2 partitions.
        var drainResult = new Dictionary<int, List<ReadyBatch>>();
        var batchListPool = new Stack<List<ReadyBatch>>();
        accumulator.Drain(metadataManager, readyNodes, maxRequestSize: 1, drainResult, batchListPool);

        // Verify only 1 batch was drained (maxRequestSize too small for more)
        await Assert.That(drainResult).ContainsKey(1);
        await Assert.That(drainResult[1]).Count().IsEqualTo(1);

        // Key assertion: a subsequent Ready() call must find the remaining partitions
        // because Drain re-enqueued their notifications.
        var readyNodes2 = new HashSet<int>();
        accumulator.Ready(metadataManager, readyNodes2);
        await Assert.That(readyNodes2).Contains(1);
    }

    [Test]
    public async Task Drain_MultipleBatchesSamePartition_ReenqueuesRemaining()
    {
        // Regression test: when PollFirst drains one batch from a partition that has
        // multiple sealed batches, the remaining batches must get a re-notification.
        // Without this, the sender sleeps with batches stuck in the deque.
        var options = CreateTestOptions(batchSize: 50);
        await using var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        await using var metadataManager = CreateMetadataManager("test-topic", 1, nodeId: 1);

        var pooledKey = new PooledMemory(null, 0, isNull: true);
        var pooledValue = new PooledMemory(null, 0, isNull: true);

        // Seal multiple batches on partition 0 (each batch seals when full)
        for (var i = 0; i < 30; i++)
        {
            var completion = pool.Rent();
            accumulator.TryAppendWithCompletion("test-topic", 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, completion);
        }

        // Ready() consumes all notifications for partition 0
        var readyNodes = new HashSet<int>();
        accumulator.Ready(metadataManager, readyNodes);
        await Assert.That(readyNodes).Contains(1);

        // Drain takes ONE batch per partition
        var drainResult = new Dictionary<int, List<ReadyBatch>>();
        var batchListPool = new Stack<List<ReadyBatch>>();
        accumulator.Drain(metadataManager, readyNodes, int.MaxValue, drainResult, batchListPool);
        await Assert.That(drainResult[1]).Count().IsEqualTo(1);

        // Key assertion: remaining batches must have a notification so the next
        // Ready() call finds them. Without the PollFirst re-enqueue fix, this fails.
        var readyNodes2 = new HashSet<int>();
        accumulator.Ready(metadataManager, readyNodes2);
        await Assert.That(readyNodes2).Contains(1);
    }

    [Test]
    public async Task Drain_LeaderMigration_ReenqueuesOnlyAffectedPartitions()
    {
        // Verifies that when GetPartitionsForNode returns empty (leader migrated between
        // Ready() and Drain()), only the specific partitions Ready() consumed for that node
        // are re-enqueued — not all partition deques (the O(n) scan from #577).
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        // Setup: 2 brokers, 4 partitions — partitions 0,1 on node 1; partitions 2,3 on node 2
        var metadataManager = CreateMultiBrokerMetadataManager("test-topic",
            [(0, 1), (1, 1), (2, 2), (3, 2)]);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Seal batches on partition 0 (node 1) and partition 2 (node 2)
            for (var p = 0; p <= 2; p += 2) // partitions 0 and 2
            {
                for (var i = 0; i < 10; i++)
                {
                    var completion = pool.Rent();
                    accumulator.TryAppendWithCompletion("test-topic", p,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey, pooledValue, null, 0, completion);
                }
            }

            // Ready() resolves partition 0 → node 1, partition 2 → node 2
            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);
            await Assert.That(readyNodes).Contains(1);
            await Assert.That(readyNodes).Contains(2);

            // Simulate leader migration: update metadata so node 1 owns no partitions.
            // Partition 0 moves to node 2. Node 1 now returns empty from GetPartitionsForNode.
            var migratedManager = CreateMultiBrokerMetadataManager("test-topic",
                [(0, 2), (1, 2), (2, 2), (3, 2)]);

            // Drain with migrated metadata — node 1's partitions should be re-enqueued,
            // node 2's partitions should drain normally.
            var drainResult = new Dictionary<int, List<ReadyBatch>>();
            var batchListPool = new Stack<List<ReadyBatch>>();
            accumulator.Drain(migratedManager, readyNodes, int.MaxValue, drainResult, batchListPool);

            // Node 1 had leader migration — its batches should NOT be in drain result
            await Assert.That(drainResult.ContainsKey(1)).IsFalse();

            // Node 2 should have drained its partition 2 batch
            await Assert.That(drainResult).ContainsKey(2);

            // After migration, the next Ready() with updated metadata should find partition 0
            // re-enqueued and now resolving to node 2
            var readyNodes2 = new HashSet<int>();
            accumulator.Ready(migratedManager, readyNodes2);
            await Assert.That(readyNodes2).Contains(2);

            await migratedManager.DisposeAsync();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Drain_LeaderMigration_DoesNotReenqueueUnrelatedPartitions()
    {
        // Verifies that leader migration for node 1 does NOT cause partition 2 (on node 2)
        // to be re-enqueued — only partition 0 (which Ready() consumed for node 1) is affected.
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        // Partition 0 on node 1, partition 1 on node 2
        var metadataManager = CreateMultiBrokerMetadataManager("test-topic",
            [(0, 1), (1, 2)]);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Seal batch only on partition 0 (node 1)
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
            }

            // Also put an unsealed record on partition 1 (node 2)
            var comp2 = pool.Rent();
            accumulator.TryAppendWithCompletion("test-topic", 1,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, comp2);

            // Ready() only sees partition 0 as ready (sealed)
            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);
            await Assert.That(readyNodes.Count).IsEqualTo(1);
            await Assert.That(readyNodes).Contains(1);

            // Migrate node 1 away — all partitions now on node 2
            var migratedManager = CreateMultiBrokerMetadataManager("test-topic",
                [(0, 2), (1, 2)]);

            // Drain — node 1 migration causes re-enqueue of partition 0 only
            var drainResult = new Dictionary<int, List<ReadyBatch>>();
            var batchListPool = new Stack<List<ReadyBatch>>();
            accumulator.Drain(migratedManager, readyNodes, int.MaxValue, drainResult, batchListPool);

            // Nothing should be drained for node 1 (migration)
            await Assert.That(drainResult.ContainsKey(1)).IsFalse();

            // Next Ready() should find partition 0 re-enqueued and resolving to node 2
            var readyNodes2 = new HashSet<int>();
            accumulator.Ready(migratedManager, readyNodes2);
            await Assert.That(readyNodes2).Contains(2);

            // Drain node 2 — should get partition 0's batch
            var drainResult2 = new Dictionary<int, List<ReadyBatch>>();
            accumulator.Drain(migratedManager, readyNodes2, int.MaxValue, drainResult2, batchListPool);
            await Assert.That(drainResult2).ContainsKey(2);
            await Assert.That(drainResult2[2].Count).IsGreaterThanOrEqualTo(1);

            await migratedManager.DisposeAsync();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task Drain_LeaderMigration_MultipleCycles_NoPartitionLoss()
    {
        // Verifies that repeated leader migration cycles don't lose partition notifications.
        var options = CreateTestOptions(batchSize: 50);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        var metadataManager = CreateMultiBrokerMetadataManager("test-topic",
            [(0, 1)]);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Seal a batch on partition 0
            for (var i = 0; i < 10; i++)
            {
                var completion = pool.Rent();
                accumulator.TryAppendWithCompletion("test-topic", 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, completion);
            }

            // Ready() sees partition 0 on node 1
            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);
            await Assert.That(readyNodes).Contains(1);

            // First migration: node 1 → empty
            var emptyManager = new MetadataManager(connectionPool: null!, bootstrapServers: ["localhost:9092"]);
            var drainResult = new Dictionary<int, List<ReadyBatch>>();
            var batchListPool = new Stack<List<ReadyBatch>>();
            accumulator.Drain(emptyManager, readyNodes, int.MaxValue, drainResult, batchListPool);
            await Assert.That(drainResult.ContainsKey(1)).IsFalse();

            // Partition 0 should be re-enqueued. Ready() with correct metadata should find it.
            var readyNodes2 = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes2);
            await Assert.That(readyNodes2).Contains(1);

            // Drain successfully this time
            drainResult.Clear();
            accumulator.Drain(metadataManager, readyNodes2, int.MaxValue, drainResult, batchListPool);
            await Assert.That(drainResult).ContainsKey(1);
            await Assert.That(drainResult[1].Count).IsGreaterThanOrEqualTo(1);

            await emptyManager.DisposeAsync();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a MetadataManager with multiple brokers and per-partition leader assignments.
    /// </summary>
    private static MetadataManager CreateMultiBrokerMetadataManager(
        string topic, (int PartitionIndex, int LeaderId)[] assignments)
    {
        var manager = new MetadataManager(connectionPool: null!, bootstrapServers: ["localhost:9092"]);

        var brokerIds = new HashSet<int>();
        var partitions = new List<PartitionMetadata>();

        foreach (var (partitionIndex, leaderId) in assignments)
        {
            brokerIds.Add(leaderId);
            partitions.Add(new PartitionMetadata
            {
                ErrorCode = ErrorCode.None,
                PartitionIndex = partitionIndex,
                LeaderId = leaderId,
                ReplicaNodes = [leaderId],
                IsrNodes = [leaderId]
            });
        }

        var brokers = new List<BrokerMetadata>();
        foreach (var nodeId in brokerIds)
        {
            brokers.Add(new BrokerMetadata { NodeId = nodeId, Host = "localhost", Port = 9090 + nodeId });
        }

        var response = new MetadataResponse
        {
            Brokers = brokers,
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
}
