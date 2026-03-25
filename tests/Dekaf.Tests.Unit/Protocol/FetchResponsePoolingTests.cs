using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for object pooling of FetchResponse, FetchResponseTopic, and FetchResponsePartition.
/// Verifies that objects are reused from pools, properly cleared on return, and that pool
/// overflow is handled gracefully.
/// </summary>
public class FetchResponsePoolingTests
{
    // ── FetchResponse pooling ──

    [Test]
    public async Task FetchResponse_RentFromPool_ReturnsInstance()
    {
        var response = FetchResponse.RentFromPool();

        await Assert.That(response).IsNotNull();
    }

    [Test]
    public async Task FetchResponse_ReturnAndRent_ReusesInstance()
    {
        var response = FetchResponse.RentFromPool();
        response.ThrottleTimeMs = 42;
        response.ErrorCode = ErrorCode.UnknownServerError;
        response.SessionId = 7;
        response.ReturnToPool();

        var reused = FetchResponse.RentFromPool();

        // Should be the same pooled instance, with fields cleared
        await Assert.That(reused).IsSameReferenceAs(response);
        await Assert.That(reused.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(reused.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(reused.SessionId).IsEqualTo(0);
        await Assert.That(reused.Responses).IsEmpty();
        await Assert.That(reused.PooledMemoryOwner).IsNull();

        // Clean up
        reused.ReturnToPool();
    }

    [Test]
    public async Task FetchResponse_ReturnToPool_ClearsNestedTopicsAndPartitions()
    {
        var partition = FetchResponsePartition.RentFromPool();
        partition.PartitionIndex = 3;
        partition.HighWatermark = 999;

        var topic = FetchResponseTopic.RentFromPool();
        topic.Topic = "test-topic";
        topic.Partitions = [partition];

        var response = FetchResponse.RentFromPool();
        response.Responses = [topic];

        // Return should cascade to topics and partitions
        response.ReturnToPool();

        // After return, the original references should have been cleared
        // (fields reset by ReturnToPool before being pushed back to pool)
        // Verify the partition was cleared by checking it was reset
        await Assert.That(partition.PartitionIndex).IsEqualTo(0);
        await Assert.That(partition.HighWatermark).IsEqualTo(0);

        // Verify the topic was cleared
        await Assert.That(topic.Topic).IsNull();
        await Assert.That(topic.Partitions).IsEmpty();

        // Verify the response was cleared
        await Assert.That(response.Responses).IsEmpty();
        await Assert.That(response.SessionId).IsEqualTo(0);
    }

    // ── FetchResponseTopic pooling ──

    [Test]
    public async Task FetchResponseTopic_RentFromPool_ReturnsInstance()
    {
        var topic = FetchResponseTopic.RentFromPool();

        await Assert.That(topic).IsNotNull();

        topic.ReturnToPool();
    }

    [Test]
    public async Task FetchResponseTopic_ReturnAndRent_ClearsAllFields()
    {
        var topic = FetchResponseTopic.RentFromPool();
        topic.Topic = "my-topic";
        topic.TopicId = Guid.NewGuid();
        topic.Partitions = [FetchResponsePartition.RentFromPool()];
        topic.ReturnToPool();

        var reused = FetchResponseTopic.RentFromPool();

        await Assert.That(reused).IsSameReferenceAs(topic);
        await Assert.That(reused.Topic).IsNull();
        await Assert.That(reused.TopicId).IsEqualTo(Guid.Empty);
        await Assert.That(reused.Partitions).IsEmpty();

        reused.ReturnToPool();
    }

    // ── FetchResponsePartition pooling ──

    [Test]
    public async Task FetchResponsePartition_RentFromPool_ReturnsInstance()
    {
        var partition = FetchResponsePartition.RentFromPool();

        await Assert.That(partition).IsNotNull();

        partition.ReturnToPool();
    }

    [Test]
    public async Task FetchResponsePartition_ReturnAndRent_ClearsAllFields()
    {
        var partition = FetchResponsePartition.RentFromPool();
        partition.PartitionIndex = 5;
        partition.ErrorCode = ErrorCode.OffsetOutOfRange;
        partition.HighWatermark = 12345;
        partition.LastStableOffset = 12300;
        partition.LogStartOffset = 100;
        partition.PreferredReadReplica = 2;
        partition.Records = [];
        partition.AbortedTransactions = [];
        partition.ReturnToPool();

        var reused = FetchResponsePartition.RentFromPool();

        await Assert.That(reused).IsSameReferenceAs(partition);
        await Assert.That(reused.PartitionIndex).IsEqualTo(0);
        await Assert.That(reused.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(reused.HighWatermark).IsEqualTo(0);
        await Assert.That(reused.LastStableOffset).IsEqualTo(-1);
        await Assert.That(reused.LogStartOffset).IsEqualTo(-1);
        await Assert.That(reused.PreferredReadReplica).IsEqualTo(-1);
        await Assert.That(reused.Records).IsNull();
        await Assert.That(reused.AbortedTransactions).IsNull();
        await Assert.That(reused.DivergingEpoch).IsNull();
        await Assert.That(reused.CurrentLeader).IsNull();
        await Assert.That(reused.SnapshotId).IsNull();

        reused.ReturnToPool();
    }

    // ── Pool overflow ──

    [Test]
    public async Task FetchResponsePartition_PoolOverflow_DoesNotThrow()
    {
        // Rent and return more than MaxPoolSize (1024) partitions
        // The pool should gracefully discard excess items without throwing
        var partitions = new FetchResponsePartition[1100];
        for (var i = 0; i < partitions.Length; i++)
        {
            partitions[i] = FetchResponsePartition.RentFromPool();
        }

        // Return all - some will exceed pool capacity and be dropped
        foreach (var p in partitions)
        {
            p.ReturnToPool();
        }

        // Verify we can still rent from the pool
        var rented = FetchResponsePartition.RentFromPool();
        await Assert.That(rented).IsNotNull();
        rented.ReturnToPool();
    }

    // ── Thread-safety ──

    [Test]
    public async Task FetchResponse_ConcurrentRentAndReturn_DoesNotCorrupt()
    {
        // Stress test: concurrent rent/return from multiple threads
        const int threadCount = 8;
        const int operationsPerThread = 200;
        var barrier = new Barrier(threadCount);

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < operationsPerThread; i++)
            {
                var response = FetchResponse.RentFromPool();
                response.ThrottleTimeMs = i;
                response.ReturnToPool();
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Verify pool is still functional
        var final = FetchResponse.RentFromPool();
        await Assert.That(final).IsNotNull();
        await Assert.That(final.ThrottleTimeMs).IsEqualTo(0);
        final.ReturnToPool();
    }

    [Test]
    public async Task FetchResponsePartition_ConcurrentRentAndReturn_DoesNotCorrupt()
    {
        const int threadCount = 8;
        const int operationsPerThread = 200;
        var barrier = new Barrier(threadCount);

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < operationsPerThread; i++)
            {
                var partition = FetchResponsePartition.RentFromPool();
                partition.PartitionIndex = i;
                partition.ReturnToPool();
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var final = FetchResponsePartition.RentFromPool();
        await Assert.That(final).IsNotNull();
        await Assert.That(final.PartitionIndex).IsEqualTo(0);
        final.ReturnToPool();
    }
}
