using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for object pooling of FetchResponse, FetchResponseTopic, and FetchResponsePartition.
/// Verifies that objects are reused from pools, properly cleared on return, and that
/// use-after-return is detected.
/// </summary>
[NotInParallel("FetchResponsePool")]
public class FetchResponsePoolingTests
{
    // ── FetchResponse pooling ──

    [Test]
    public async Task FetchResponse_Rent_ReturnsInstance()
    {
        var response = FetchResponse.Rent();

        await Assert.That(response).IsNotNull();

        response.ReturnToPool();
    }

    [Test]
    public async Task FetchResponse_ReturnAndRent_ClearsAllFields()
    {
        var response = FetchResponse.Rent();
        response.ThrottleTimeMs = 42;
        response.ErrorCode = ErrorCode.UnknownServerError;
        response.SessionId = 7;
        response.ReturnToPool();

        var reused = FetchResponse.Rent();

        await Assert.That(reused).IsSameReferenceAs(response);
        await Assert.That(reused.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(reused.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(reused.SessionId).IsEqualTo(0);
        await Assert.That(reused.Responses).IsEmpty();
        await Assert.That(reused.PooledMemoryOwner).IsNull();

        reused.ReturnToPool();
    }

    [Test]
    public async Task FetchResponse_ReturnToPool_ClearsNestedTopicsAndPartitions()
    {
        var partition = FetchResponsePartition.Rent();
        partition.PartitionIndex = 3;
        partition.HighWatermark = 999;

        var topic = FetchResponseTopic.Rent();
        topic.Topic = "test-topic";
        topic.Partitions = [partition];

        var response = FetchResponse.Rent();
        response.Responses = [topic];

        // Return should cascade to topics and partitions
        response.ReturnToPool();

        // After return, accessing guarded properties should throw ObjectDisposedException
        await Assert.That(() => response.Responses).Throws<ObjectDisposedException>();
        await Assert.That(() => topic.Partitions).Throws<ObjectDisposedException>();
        await Assert.That(() => partition.Records).Throws<ObjectDisposedException>();

        // Re-rent to verify fields were actually cleared
        var reusedResponse = FetchResponse.Rent();
        var reusedTopic = FetchResponseTopic.Rent();
        var reusedPartition = FetchResponsePartition.Rent();

        await Assert.That(reusedResponse.Responses).IsEmpty();
        await Assert.That(reusedResponse.SessionId).IsEqualTo(0);
        await Assert.That(reusedTopic.Topic).IsNull();
        await Assert.That(reusedTopic.Partitions).IsEmpty();
        await Assert.That(reusedPartition.PartitionIndex).IsEqualTo(0);
        await Assert.That(reusedPartition.HighWatermark).IsEqualTo(0);

        reusedPartition.ReturnToPool();
        reusedTopic.ReturnToPool();
        reusedResponse.ReturnToPool();
    }

    // ── FetchResponseTopic pooling ──

    [Test]
    public async Task FetchResponseTopic_Rent_ReturnsInstance()
    {
        var topic = FetchResponseTopic.Rent();

        await Assert.That(topic).IsNotNull();

        topic.ReturnToPool();
    }

    [Test]
    public async Task FetchResponseTopic_ReturnAndRent_ClearsAllFields()
    {
        var topic = FetchResponseTopic.Rent();
        topic.Topic = "my-topic";
        topic.TopicId = Guid.NewGuid();
        // Use a non-pooled partition to avoid leaking a rented partition into the pool
        // when ReturnToPool cascades.
        topic.Partitions = [new FetchResponsePartition { PartitionIndex = 99 }];
        topic.ReturnToPool();

        var reused = FetchResponseTopic.Rent();

        await Assert.That(reused).IsSameReferenceAs(topic);
        await Assert.That(reused.Topic).IsNull();
        await Assert.That(reused.TopicId).IsEqualTo(Guid.Empty);
        await Assert.That(reused.Partitions).IsEmpty();

        reused.ReturnToPool();
    }

    // ── FetchResponsePartition pooling ──

    [Test]
    public async Task FetchResponsePartition_Rent_ReturnsInstance()
    {
        var partition = FetchResponsePartition.Rent();

        await Assert.That(partition).IsNotNull();

        partition.ReturnToPool();
    }

    [Test]
    public async Task FetchResponsePartition_ReturnAndRent_ClearsAllFields()
    {
        var partition = FetchResponsePartition.Rent();
        partition.PartitionIndex = 5;
        partition.ErrorCode = ErrorCode.OffsetOutOfRange;
        partition.HighWatermark = 12345;
        partition.LastStableOffset = 12300;
        partition.LogStartOffset = 100;
        partition.PreferredReadReplica = 2;
        partition.Records = [];
        partition.AbortedTransactions = [];
        partition.ReturnToPool();

        var reused = FetchResponsePartition.Rent();

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

    // ── Thread-safety ──

    [Test]
    public async Task FetchResponse_ConcurrentRentAndReturn_DoesNotCorrupt()
    {
        const int threadCount = 8;
        const int operationsPerThread = 200;
        var barrier = new Barrier(threadCount);

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < operationsPerThread; i++)
            {
                var response = FetchResponse.Rent();
                response.ThrottleTimeMs = i;
                response.ReturnToPool();
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var final = FetchResponse.Rent();
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
                var partition = FetchResponsePartition.Rent();
                partition.PartitionIndex = i;
                partition.ReturnToPool();
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var final = FetchResponsePartition.Rent();
        await Assert.That(final).IsNotNull();
        await Assert.That(final.PartitionIndex).IsEqualTo(0);
        final.ReturnToPool();
    }

    // ── Double-return safety ──

    [Test]
    public async Task FetchResponse_DoubleReturn_IsIdempotent()
    {
        var response = FetchResponse.Rent();
        response.ReturnToPool();

        // Second return should not throw
        response.ReturnToPool();

        // Object must still be in the "returned" state
        await Assert.That(() => response.Responses).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task FetchResponseTopic_DoubleReturn_IsIdempotent()
    {
        var topic = FetchResponseTopic.Rent();
        topic.ReturnToPool();

        topic.ReturnToPool();

        await Assert.That(() => topic.Partitions).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task FetchResponsePartition_DoubleReturn_IsIdempotent()
    {
        var partition = FetchResponsePartition.Rent();
        partition.ReturnToPool();

        partition.ReturnToPool();

        await Assert.That(() => partition.Records).Throws<ObjectDisposedException>();
    }

    // ── Use-after-return guards ──

    [Test]
    public async Task FetchResponse_AccessResponsesAfterReturn_ThrowsObjectDisposedException()
    {
        var response = FetchResponse.Rent();
        response.ReturnToPool();

        await Assert.That(() => response.Responses).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task FetchResponseTopic_AccessPartitionsAfterReturn_ThrowsObjectDisposedException()
    {
        var topic = FetchResponseTopic.Rent();
        topic.ReturnToPool();

        await Assert.That(() => topic.Partitions).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task FetchResponsePartition_AccessRecordsAfterReturn_ThrowsObjectDisposedException()
    {
        var partition = FetchResponsePartition.Rent();
        partition.ReturnToPool();

        await Assert.That(() => partition.Records).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task FetchResponsePartition_AccessAbortedTransactionsAfterReturn_ThrowsObjectDisposedException()
    {
        var partition = FetchResponsePartition.Rent();
        partition.ReturnToPool();

        await Assert.That(() => partition.AbortedTransactions).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task FetchResponse_AccessResponsesAfterRentAgain_Succeeds()
    {
        var response = FetchResponse.Rent();
        response.ReturnToPool();

        var reused = FetchResponse.Rent();
        await Assert.That(reused.Responses).IsEmpty();
        reused.ReturnToPool();
    }

    [Test]
    public async Task FetchResponseTopic_AccessPartitionsAfterRentAgain_Succeeds()
    {
        var topic = FetchResponseTopic.Rent();
        topic.ReturnToPool();

        var reused = FetchResponseTopic.Rent();
        await Assert.That(reused.Partitions).IsEmpty();
        reused.ReturnToPool();
    }

    [Test]
    public async Task FetchResponsePartition_AccessRecordsAfterRentAgain_Succeeds()
    {
        var partition = FetchResponsePartition.Rent();
        partition.ReturnToPool();

        var reused = FetchResponsePartition.Rent();
        await Assert.That(reused.Records).IsNull();
        reused.ReturnToPool();
    }
}

public class AbortedTransactionTests
{
    [Test]
    public async Task AbortedTransaction_SameValues_AreEqual()
    {
        var a = new AbortedTransaction { ProducerId = 42, FirstOffset = 100 };
        var b = new AbortedTransaction { ProducerId = 42, FirstOffset = 100 };

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task AbortedTransaction_DifferentValues_AreNotEqual()
    {
        var a = new AbortedTransaction { ProducerId = 42, FirstOffset = 100 };
        var b = new AbortedTransaction { ProducerId = 42, FirstOffset = 200 };

        await Assert.That(a).IsNotEqualTo(b);
        await Assert.That(a == b).IsFalse();
    }
}
