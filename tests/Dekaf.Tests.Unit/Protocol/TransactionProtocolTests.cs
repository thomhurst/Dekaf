using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for transaction protocol message encoding/decoding.
/// </summary>
public sealed class TransactionProtocolTests
{
    #region InitProducerId Tests

    [Test]
    public async Task InitProducerIdRequest_V0_WritesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new InitProducerIdRequest
        {
            TransactionalId = "txn-1",
            TransactionTimeoutMs = 60000
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // STRING: TransactionalId (INT16 length + data)
        var txnId = reader.ReadString();
        // INT32: TransactionTimeoutMs
        var timeoutMs = reader.ReadInt32();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-1");
        await Assert.That(timeoutMs).IsEqualTo(60000);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task InitProducerIdRequest_V2_Flexible_WritesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new InitProducerIdRequest
        {
            TransactionalId = "txn-2",
            TransactionTimeoutMs = 30000
        };
        request.Write(ref writer, version: 2);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // COMPACT_NULLABLE_STRING: TransactionalId
        var txnId = reader.ReadCompactString();
        // INT32: TransactionTimeoutMs
        var timeoutMs = reader.ReadInt32();
        // Empty tagged fields
        reader.SkipTaggedFields();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-2");
        await Assert.That(timeoutMs).IsEqualTo(30000);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task InitProducerIdRequest_V3_IncludesProducerIdAndEpoch()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new InitProducerIdRequest
        {
            TransactionalId = "txn-3",
            TransactionTimeoutMs = 60000,
            ProducerId = 42,
            ProducerEpoch = 5
        };
        request.Write(ref writer, version: 3);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadCompactString();
        var timeoutMs = reader.ReadInt32();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        reader.SkipTaggedFields();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-3");
        await Assert.That(timeoutMs).IsEqualTo(60000);
        await Assert.That(producerId).IsEqualTo(42L);
        await Assert.That(producerEpoch).IsEqualTo((short)5);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task InitProducerIdRequest_V0_NullTransactionalId()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new InitProducerIdRequest
        {
            TransactionalId = null,
            TransactionTimeoutMs = 60000
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadString();
        var timeoutMs = reader.ReadInt32();
        var end = reader.End;

        await Assert.That(txnId).IsNull();
        await Assert.That(timeoutMs).IsEqualTo(60000);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task InitProducerIdResponse_V0_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // ThrottleTimeMs
        writer.WriteInt32(100);
        // ErrorCode
        writer.WriteInt16(0);
        // ProducerId
        writer.WriteInt64(1000);
        // ProducerEpoch
        writer.WriteInt16(3);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (InitProducerIdResponse)InitProducerIdResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ProducerId).IsEqualTo(1000L);
        await Assert.That(response.ProducerEpoch).IsEqualTo((short)3);
    }

    [Test]
    public async Task InitProducerIdResponse_V2_Flexible_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(50);
        writer.WriteInt16(0);
        writer.WriteInt64(2000);
        writer.WriteInt16(7);
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (InitProducerIdResponse)InitProducerIdResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ProducerId).IsEqualTo(2000L);
        await Assert.That(response.ProducerEpoch).IsEqualTo((short)7);
    }

    [Test]
    public async Task InitProducerIdResponse_ErrorCode_Preserved()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.CoordinatorNotAvailable);
        writer.WriteInt64(-1);
        writer.WriteInt16(-1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (InitProducerIdResponse)InitProducerIdResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.CoordinatorNotAvailable);
    }

    #endregion

    #region AddPartitionsToTxn Tests

    [Test]
    public async Task AddPartitionsToTxnRequest_V0_WritesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new AddPartitionsToTxnRequest
        {
            TransactionalId = "txn-1",
            ProducerId = 100,
            ProducerEpoch = 2,
            Topics =
            [
                new AddPartitionsToTxnTopic
                {
                    Name = "topic-a",
                    Partitions = [0, 1, 2]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadString();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        // Read array of topics
        var topicCount = reader.ReadInt32();
        var topicName = reader.ReadString();
        // Read array of partitions
        var partitionCount = reader.ReadInt32();
        var p0 = reader.ReadInt32();
        var p1 = reader.ReadInt32();
        var p2 = reader.ReadInt32();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-1");
        await Assert.That(producerId).IsEqualTo(100L);
        await Assert.That(producerEpoch).IsEqualTo((short)2);
        await Assert.That(topicCount).IsEqualTo(1);
        await Assert.That(topicName).IsEqualTo("topic-a");
        await Assert.That(partitionCount).IsEqualTo(3);
        await Assert.That(p0).IsEqualTo(0);
        await Assert.That(p1).IsEqualTo(1);
        await Assert.That(p2).IsEqualTo(2);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task AddPartitionsToTxnResponse_V0_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // ThrottleTimeMs
        writer.WriteInt32(0);
        // Results array (1 topic)
        writer.WriteInt32(1);
        // Topic name
        writer.WriteString("topic-a");
        // Partitions array (2 partitions)
        writer.WriteInt32(2);
        // Partition 0 - no error
        writer.WriteInt32(0);
        writer.WriteInt16(0);
        // Partition 1 - error
        writer.WriteInt32(1);
        writer.WriteInt16((short)ErrorCode.InvalidTxnState);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (AddPartitionsToTxnResponse)AddPartitionsToTxnResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].Name).IsEqualTo("topic-a");
        await Assert.That(response.Results[0].Partitions.Count).IsEqualTo(2);
        await Assert.That(response.Results[0].Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(response.Results[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results[0].Partitions[1].PartitionIndex).IsEqualTo(1);
        await Assert.That(response.Results[0].Partitions[1].ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);
    }

    #endregion

    #region AddOffsetsToTxn Tests

    [Test]
    public async Task AddOffsetsToTxnRequest_V0_WritesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new AddOffsetsToTxnRequest
        {
            TransactionalId = "txn-1",
            ProducerId = 200,
            ProducerEpoch = 1,
            GroupId = "consumer-group-1"
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadString();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var groupId = reader.ReadString();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-1");
        await Assert.That(producerId).IsEqualTo(200L);
        await Assert.That(producerEpoch).IsEqualTo((short)1);
        await Assert.That(groupId).IsEqualTo("consumer-group-1");
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task AddOffsetsToTxnResponse_V0_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(10);
        writer.WriteInt16(0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (AddOffsetsToTxnResponse)AddOffsetsToTxnResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(10);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
    }

    #endregion

    #region EndTxn Tests

    [Test]
    public async Task EndTxnRequest_V0_Commit_WritesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new EndTxnRequest
        {
            TransactionalId = "txn-1",
            ProducerId = 300,
            ProducerEpoch = 4,
            Committed = true
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadString();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var committed = reader.ReadInt8();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-1");
        await Assert.That(producerId).IsEqualTo(300L);
        await Assert.That(producerEpoch).IsEqualTo((short)4);
        await Assert.That(committed).IsEqualTo((sbyte)1);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task EndTxnRequest_V0_Abort_WritesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new EndTxnRequest
        {
            TransactionalId = "txn-1",
            ProducerId = 300,
            ProducerEpoch = 4,
            Committed = false
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadString();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var committed = reader.ReadInt8();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-1");
        await Assert.That(producerId).IsEqualTo(300L);
        await Assert.That(producerEpoch).IsEqualTo((short)4);
        await Assert.That(committed).IsEqualTo((sbyte)0);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task EndTxnRequest_V3_Flexible_WritesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new EndTxnRequest
        {
            TransactionalId = "txn-flex",
            ProducerId = 500,
            ProducerEpoch = 10,
            Committed = true
        };
        request.Write(ref writer, version: 3);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadCompactString();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var committed = reader.ReadInt8();
        reader.SkipTaggedFields();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-flex");
        await Assert.That(producerId).IsEqualTo(500L);
        await Assert.That(producerEpoch).IsEqualTo((short)10);
        await Assert.That(committed).IsEqualTo((sbyte)1);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task EndTxnResponse_V0_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteInt16(0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (EndTxnResponse)EndTxnResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task EndTxnResponse_V3_Flexible_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(25);
        writer.WriteInt16((short)ErrorCode.InvalidTxnState);
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (EndTxnResponse)EndTxnResponse.Read(ref reader, version: 3);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(25);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);
    }

    #endregion

    #region TxnOffsetCommit Tests

    [Test]
    public async Task TxnOffsetCommitRequest_V0_WritesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new TxnOffsetCommitRequest
        {
            TransactionalId = "txn-1",
            GroupId = "group-1",
            ProducerId = 100,
            ProducerEpoch = 2,
            Topics =
            [
                new TxnOffsetCommitRequestTopic
                {
                    Name = "topic-a",
                    Partitions =
                    [
                        new TxnOffsetCommitRequestPartition
                        {
                            PartitionIndex = 0,
                            CommittedOffset = 42,
                            CommittedMetadata = "meta"
                        }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadString();
        var groupId = reader.ReadString();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        // Topics array
        var topicCount = reader.ReadInt32();
        var topicName = reader.ReadString();
        // Partitions array
        var partitionCount = reader.ReadInt32();
        var partitionIndex = reader.ReadInt32();
        var committedOffset = reader.ReadInt64();
        var committedMetadata = reader.ReadString();
        var end = reader.End;

        await Assert.That(txnId).IsEqualTo("txn-1");
        await Assert.That(groupId).IsEqualTo("group-1");
        await Assert.That(producerId).IsEqualTo(100L);
        await Assert.That(producerEpoch).IsEqualTo((short)2);
        await Assert.That(topicCount).IsEqualTo(1);
        await Assert.That(topicName).IsEqualTo("topic-a");
        await Assert.That(partitionCount).IsEqualTo(1);
        await Assert.That(partitionIndex).IsEqualTo(0);
        await Assert.That(committedOffset).IsEqualTo(42L);
        await Assert.That(committedMetadata).IsEqualTo("meta");
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task TxnOffsetCommitRequest_V3_Flexible_WithGroupInfo()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new TxnOffsetCommitRequest
        {
            TransactionalId = "txn-flex",
            GroupId = "group-flex",
            ProducerId = 500,
            ProducerEpoch = 8,
            GenerationId = 3,
            MemberId = "member-1",
            GroupInstanceId = "instance-1",
            Topics =
            [
                new TxnOffsetCommitRequestTopic
                {
                    Name = "topic-b",
                    Partitions =
                    [
                        new TxnOffsetCommitRequestPartition
                        {
                            PartitionIndex = 1,
                            CommittedOffset = 100,
                            CommittedLeaderEpoch = 5,
                            CommittedMetadata = null
                        }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 3);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var txnId = reader.ReadCompactString();
        var groupId = reader.ReadCompactString();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var generationId = reader.ReadInt32();
        var memberId = reader.ReadCompactString();
        var groupInstanceId = reader.ReadCompactString();
        // Topics compact array
        var topicCount = reader.ReadUnsignedVarInt() - 1;
        var topicName = reader.ReadCompactNonNullableString();
        // Partitions compact array
        var partitionCount = reader.ReadUnsignedVarInt() - 1;
        var partitionIndex = reader.ReadInt32();
        var committedOffset = reader.ReadInt64();
        var committedLeaderEpoch = reader.ReadInt32();

        await Assert.That(txnId).IsEqualTo("txn-flex");
        await Assert.That(groupId).IsEqualTo("group-flex");
        await Assert.That(producerId).IsEqualTo(500L);
        await Assert.That(producerEpoch).IsEqualTo((short)8);
        await Assert.That(generationId).IsEqualTo(3);
        await Assert.That(memberId).IsEqualTo("member-1");
        await Assert.That(groupInstanceId).IsEqualTo("instance-1");
        await Assert.That(topicCount).IsEqualTo(1);
        await Assert.That(topicName).IsEqualTo("topic-b");
        await Assert.That(partitionCount).IsEqualTo(1);
        await Assert.That(partitionIndex).IsEqualTo(1);
        await Assert.That(committedOffset).IsEqualTo(100L);
        await Assert.That(committedLeaderEpoch).IsEqualTo(5);
    }

    [Test]
    public async Task TxnOffsetCommitResponse_V0_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // ThrottleTimeMs
        writer.WriteInt32(5);
        // Topics array (1 topic)
        writer.WriteInt32(1);
        // Topic name
        writer.WriteString("topic-a");
        // Partitions array (1 partition)
        writer.WriteInt32(1);
        // PartitionIndex
        writer.WriteInt32(0);
        // ErrorCode
        writer.WriteInt16(0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (TxnOffsetCommitResponse)TxnOffsetCommitResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(5);
        await Assert.That(response.Topics.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Name).IsEqualTo("topic-a");
        await Assert.That(response.Topics[0].Partitions.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(response.Topics[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    #endregion

    #region Version Constants Tests

    [Test]
    public async Task InitProducerIdRequest_VersionConstants()
    {
        await Assert.That(InitProducerIdRequest.ApiKey).IsEqualTo(ApiKey.InitProducerId);
        await Assert.That(InitProducerIdRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(InitProducerIdRequest.HighestSupportedVersion).IsEqualTo((short)5);
    }

    [Test]
    public async Task AddPartitionsToTxnRequest_VersionConstants()
    {
        await Assert.That(AddPartitionsToTxnRequest.ApiKey).IsEqualTo(ApiKey.AddPartitionsToTxn);
        await Assert.That(AddPartitionsToTxnRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(AddPartitionsToTxnRequest.HighestSupportedVersion).IsEqualTo((short)4);
    }

    [Test]
    public async Task EndTxnRequest_VersionConstants()
    {
        await Assert.That(EndTxnRequest.ApiKey).IsEqualTo(ApiKey.EndTxn);
        await Assert.That(EndTxnRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(EndTxnRequest.HighestSupportedVersion).IsEqualTo((short)4);
    }

    [Test]
    public async Task TxnOffsetCommitRequest_VersionConstants()
    {
        await Assert.That(TxnOffsetCommitRequest.ApiKey).IsEqualTo(ApiKey.TxnOffsetCommit);
        await Assert.That(TxnOffsetCommitRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(TxnOffsetCommitRequest.HighestSupportedVersion).IsEqualTo((short)4);
    }

    [Test]
    public async Task AddOffsetsToTxnRequest_VersionConstants()
    {
        await Assert.That(AddOffsetsToTxnRequest.ApiKey).IsEqualTo(ApiKey.AddOffsetsToTxn);
        await Assert.That(AddOffsetsToTxnRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(AddOffsetsToTxnRequest.HighestSupportedVersion).IsEqualTo((short)4);
    }

    #endregion

    #region Flexible Version Tests

    [Test]
    public async Task InitProducerIdRequest_FlexibleVersions()
    {
        await Assert.That(InitProducerIdRequest.IsFlexibleVersion(0)).IsFalse();
        await Assert.That(InitProducerIdRequest.IsFlexibleVersion(1)).IsFalse();
        await Assert.That(InitProducerIdRequest.IsFlexibleVersion(2)).IsTrue();
        await Assert.That(InitProducerIdRequest.IsFlexibleVersion(3)).IsTrue();
    }

    [Test]
    public async Task AddPartitionsToTxnRequest_FlexibleVersions()
    {
        await Assert.That(AddPartitionsToTxnRequest.IsFlexibleVersion(0)).IsFalse();
        await Assert.That(AddPartitionsToTxnRequest.IsFlexibleVersion(2)).IsFalse();
        await Assert.That(AddPartitionsToTxnRequest.IsFlexibleVersion(3)).IsTrue();
    }

    [Test]
    public async Task EndTxnRequest_FlexibleVersions()
    {
        await Assert.That(EndTxnRequest.IsFlexibleVersion(0)).IsFalse();
        await Assert.That(EndTxnRequest.IsFlexibleVersion(2)).IsFalse();
        await Assert.That(EndTxnRequest.IsFlexibleVersion(3)).IsTrue();
    }

    #endregion
}
