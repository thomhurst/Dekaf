using System.Buffers;
using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for object pooling of FetchResponse, FetchResponseTopic, and FetchResponsePartition.
/// Verifies that objects are reused from pools, properly cleared on return, and that
/// use-after-return is detected.
/// </summary>
[NotInParallel]
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
    public async Task FetchResponse_Return_ClearsAllFields()
    {
        var response = FetchResponse.Rent();
        response.ThrottleTimeMs = 42;
        response.ErrorCode = ErrorCode.UnknownServerError;
        response.SessionId = 7;
        response.ReturnToPool();

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.SessionId).IsEqualTo(0);
        await Assert.That(GetPrivateField<IReadOnlyList<FetchResponseTopic>>(response, "_responses")).IsEmpty();
        await Assert.That(response.PooledMemoryOwner).IsNull();
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
    public async Task FetchResponseTopic_Return_ClearsAllFields()
    {
        var topic = FetchResponseTopic.Rent();
        topic.Topic = "my-topic";
        topic.TopicId = Guid.NewGuid();
        // Use a non-pooled partition to avoid leaking a rented partition into the pool
        // when ReturnToPool cascades.
        topic.Partitions = [new FetchResponsePartition { PartitionIndex = 99 }];
        topic.ReturnToPool();

        await Assert.That(topic.Topic).IsNull();
        await Assert.That(topic.TopicId).IsEqualTo(Guid.Empty);
        await Assert.That(GetPrivateField<IReadOnlyList<FetchResponsePartition>>(topic, "_partitions")).IsEmpty();
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
    public async Task FetchResponsePartition_Return_ClearsAllFields()
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

        await Assert.That(partition.PartitionIndex).IsEqualTo(0);
        await Assert.That(partition.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(partition.HighWatermark).IsEqualTo(0);
        await Assert.That(partition.LastStableOffset).IsEqualTo(-1);
        await Assert.That(partition.LogStartOffset).IsEqualTo(-1);
        await Assert.That(partition.PreferredReadReplica).IsEqualTo(-1);
        await Assert.That(GetPrivateField<IReadOnlyList<RecordBatch>?>(partition, "_records")).IsNull();
        await Assert.That(GetPrivateField<IReadOnlyList<AbortedTransaction>?>(partition, "_abortedTransactions")).IsNull();
        await Assert.That(partition.DivergingEpoch).IsNull();
        await Assert.That(partition.CurrentLeader).IsNull();
        await Assert.That(partition.SnapshotId).IsNull();
    }

    [Test]
    public async Task FetchResponsePartition_ReturnToPool_ReturnsEmptyRecordsList()
    {
        var heldLists = Enumerable.Range(0, 65)
            .Select(_ => FetchResponsePartition.RentRecordBatchList())
            .ToArray();
        var expected = heldLists[0];
        var partition = FetchResponsePartition.Rent();
        partition.Records = expected;

        partition.ReturnToPool();
        var actual = FetchResponsePartition.RentRecordBatchList();

        await Assert.That(actual).IsSameReferenceAs(expected);

        FetchResponsePartition.ReturnRecordBatchList(actual);
        for (var i = 1; i < heldLists.Length; i++)
        {
            FetchResponsePartition.ReturnRecordBatchList(heldLists[i]);
        }
    }

    [Test]
    public async Task FetchResponse_ReadFailure_ReturnsPartitionsAndLists()
    {
        var responseBytes = CreateResponseWithMalformedSecondTopic();
        var heldPartitions = Enumerable.Range(0, 65)
            .Select(_ => FetchResponsePartition.Rent())
            .ToArray();
        var heldLists = Enumerable.Range(0, 65)
            .Select(_ => FetchResponsePartition.RentRecordBatchList())
            .ToArray();
        var expectedPartition = heldPartitions[0];
        var expectedList = heldLists[0];
        expectedPartition.ReturnToPool();
        FetchResponsePartition.ReturnRecordBatchList(expectedList);

        var threw = ParseMalformedResponse(responseBytes);
        var actualPartition = FetchResponsePartition.Rent();
        var actualList = FetchResponsePartition.RentRecordBatchList();

        try
        {
            await Assert.That(threw).IsTrue();
            await Assert.That(actualPartition).IsSameReferenceAs(expectedPartition);
            await Assert.That(actualList).IsSameReferenceAs(expectedList);
        }
        finally
        {
            actualPartition.ReturnToPool();
            FetchResponsePartition.ReturnRecordBatchList(actualList);

            for (var i = 1; i < heldPartitions.Length; i++)
            {
                heldPartitions[i].ReturnToPool();
                FetchResponsePartition.ReturnRecordBatchList(heldLists[i]);
            }
        }
    }

    private static bool ParseMalformedResponse(byte[] responseBytes)
    {
        var reader = new KafkaProtocolReader(responseBytes);
        try
        {
            FetchResponse.Read(ref reader, version: 12);
            return false;
        }
        catch (MalformedProtocolDataException)
        {
            return true;
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (T)field.GetValue(instance)!;
    }

    private static byte[] CreateResponseWithMalformedSecondTopic()
    {
        var batchBuffer = new ArrayBufferWriter<byte>();
        using (var batch = new RecordBatch
        {
            BaseOffset = 10,
            PartitionLeaderEpoch = -1,
            BaseTimestamp = 1_000,
            MaxTimestamp = 1_000,
            Records = [new Record { OffsetDelta = 0, TimestampDelta = 0, Value = "value"u8.ToArray() }]
        })
        {
            batch.Write(batchBuffer);
        }

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(3); // two topics
        writer.WriteCompactString("valid-topic");
        writer.WriteUnsignedVarInt(2); // one partition
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt64(100);
        writer.WriteInt64(100);
        writer.WriteInt64(0);
        writer.WriteUnsignedVarInt(1); // empty aborted transactions
        writer.WriteInt32(-1);
        writer.WriteUnsignedVarInt(batchBuffer.WrittenCount + 1);
        writer.WriteRawBytes(batchBuffer.WrittenSpan);
        writer.WriteUnsignedVarInt(0); // partition tagged fields
        writer.WriteUnsignedVarInt(0); // topic tagged fields
        writer.WriteUnsignedVarInt(10); // malformed compact-string length for second topic
        writer.WriteUInt8(0x41);
        return buffer.WrittenSpan.ToArray();
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
