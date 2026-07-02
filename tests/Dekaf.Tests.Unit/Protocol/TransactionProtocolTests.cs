using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for transaction protocol message encoding/decoding.
/// </summary>
public sealed class TransactionProtocolTests
{
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
    public async Task AddPartitionsToTxnRequest_V4_WritesTransactionsArray()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new AddPartitionsToTxnRequest
        {
            TransactionalId = "txn-v4",
            ProducerId = 200,
            ProducerEpoch = 5,
            VerifyOnly = false,
            Topics =
            [
                new AddPartitionsToTxnTopic
                {
                    Name = "topic-x",
                    Partitions = [3]
                }
            ]
        };
        request.Write(ref writer, version: 4);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // v4: Transactions compact array (length + 1 = 2 for 1 element)
        var txnArrayLen = reader.ReadUnsignedVarInt();
        // TransactionalId (compact string)
        var txnId = reader.ReadCompactNonNullableString();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var verifyOnly = reader.ReadUInt8();
        // Topics compact array (length + 1 = 2 for 1 element)
        var topicsArrayLen = reader.ReadUnsignedVarInt();
        var topicName = reader.ReadCompactNonNullableString();
        // Partitions compact array (length + 1 = 2 for 1 element)
        var partitionsArrayLen = reader.ReadUnsignedVarInt();
        var partition = reader.ReadInt32();

        await Assert.That(txnArrayLen).IsEqualTo(2); // 1 element + 1
        await Assert.That(txnId).IsEqualTo("txn-v4");
        await Assert.That(producerId).IsEqualTo(200L);
        await Assert.That(producerEpoch).IsEqualTo((short)5);
        await Assert.That(verifyOnly).IsEqualTo((byte)0);
        await Assert.That(topicsArrayLen).IsEqualTo(2); // 1 element + 1
        await Assert.That(topicName).IsEqualTo("topic-x");
        await Assert.That(partitionsArrayLen).IsEqualTo(2); // 1 element + 1
        await Assert.That(partition).IsEqualTo(3);
    }

    [Test]
    public async Task AddPartitionsToTxnResponse_V4_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // ThrottleTimeMs
        writer.WriteInt32(50);
        // ErrorCode (top-level, new in v4)
        writer.WriteInt16(0);
        // ResultsByTransaction compact array (1 element → length + 1 = 2)
        writer.WriteUnsignedVarInt(2);
        // TransactionalId
        writer.WriteCompactString("txn-v4");
        // TopicResults compact array (1 element → length + 1 = 2)
        writer.WriteUnsignedVarInt(2);
        // Topic name
        writer.WriteCompactString("topic-x");
        // ResultsByPartition compact array (1 element → length + 1 = 2)
        writer.WriteUnsignedVarInt(2);
        // Partition 3, no error
        writer.WriteInt32(3);
        writer.WriteInt16(0);
        // Partition-level tagged fields
        writer.WriteEmptyTaggedFields();
        // Topic-level tagged fields
        writer.WriteEmptyTaggedFields();
        // Transaction-level tagged fields
        writer.WriteEmptyTaggedFields();
        // Response-level tagged fields
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (AddPartitionsToTxnResponse)AddPartitionsToTxnResponse.Read(ref reader, version: 4);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].Name).IsEqualTo("topic-x");
        await Assert.That(response.Results[0].Partitions.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].Partitions[0].PartitionIndex).IsEqualTo(3);
        await Assert.That(response.Results[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
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

    [Test]
    public async Task EndTxnResponse_V4_WithEpochBump_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // ThrottleTimeMs
        writer.WriteInt32(10);
        // ErrorCode = None
        writer.WriteInt16(0);
        // Tagged fields: 2 fields
        writer.WriteUnsignedVarInt(2);
        // Tag 0: ProducerId (INT64 = 8 bytes)
        writer.WriteUnsignedVarInt(0); // tag
        writer.WriteUnsignedVarInt(8); // size
        writer.WriteInt64(42L);
        // Tag 1: ProducerEpoch (INT16 = 2 bytes)
        writer.WriteUnsignedVarInt(1); // tag
        writer.WriteUnsignedVarInt(2); // size
        writer.WriteInt16(7);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (EndTxnResponse)EndTxnResponse.Read(ref reader, version: 4);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(10);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ProducerId).IsEqualTo(42L);
        await Assert.That(response.ProducerEpoch).IsEqualTo((short)7);
    }

    [Test]
    public async Task EndTxnResponse_V4_NoTaggedFields_DefaultsToMinusOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteInt16(0);
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (EndTxnResponse)EndTxnResponse.Read(ref reader, version: 4);

        await Assert.That(response.ProducerId).IsEqualTo(-1L);
        await Assert.That(response.ProducerEpoch).IsEqualTo((short)-1);
    }

    [Test]
    public async Task EndTxnResponse_V3_BackwardCompatible_NoEpochFields()
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
        await Assert.That(response.ProducerId).IsEqualTo(-1L);
        await Assert.That(response.ProducerEpoch).IsEqualTo((short)-1);
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

    #region Version Constants Tests

    [Test]
    public async Task InitProducerIdRequest_VersionConstants()
    {
        await Assert.That(InitProducerIdRequest.ApiKey).IsEqualTo(ApiKey.InitProducerId);
        await Assert.That(InitProducerIdRequest.LowestSupportedVersion).IsEqualTo((short)2);
        await Assert.That(InitProducerIdRequest.HighestSupportedVersion).IsEqualTo((short)5);
    }

    [Test]
    public async Task AddPartitionsToTxnRequest_VersionConstants()
    {
        await Assert.That(AddPartitionsToTxnRequest.ApiKey).IsEqualTo(ApiKey.AddPartitionsToTxn);
        await Assert.That(AddPartitionsToTxnRequest.LowestSupportedVersion).IsEqualTo((short)3);
        await Assert.That(AddPartitionsToTxnRequest.HighestSupportedVersion).IsEqualTo((short)4);
    }

    [Test]
    public async Task EndTxnRequest_VersionConstants()
    {
        await Assert.That(EndTxnRequest.ApiKey).IsEqualTo(ApiKey.EndTxn);
        await Assert.That(EndTxnRequest.LowestSupportedVersion).IsEqualTo((short)3);
        await Assert.That(EndTxnRequest.HighestSupportedVersion).IsEqualTo((short)4);
    }

    [Test]
    public async Task TxnOffsetCommitRequest_VersionConstants()
    {
        await Assert.That(TxnOffsetCommitRequest.ApiKey).IsEqualTo(ApiKey.TxnOffsetCommit);
        await Assert.That(TxnOffsetCommitRequest.LowestSupportedVersion).IsEqualTo((short)3);
        await Assert.That(TxnOffsetCommitRequest.HighestSupportedVersion).IsEqualTo((short)4);
    }

    [Test]
    public async Task AddOffsetsToTxnRequest_VersionConstants()
    {
        await Assert.That(AddOffsetsToTxnRequest.ApiKey).IsEqualTo(ApiKey.AddOffsetsToTxn);
        await Assert.That(AddOffsetsToTxnRequest.LowestSupportedVersion).IsEqualTo((short)3);
        await Assert.That(AddOffsetsToTxnRequest.HighestSupportedVersion).IsEqualTo((short)4);
    }

    #endregion

}
