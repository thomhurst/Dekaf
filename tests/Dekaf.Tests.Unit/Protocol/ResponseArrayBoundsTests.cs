using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class ResponseArrayBoundsTests
{
    private const int HostileElementCount = 40;
    private const int HostilePayloadLength = 60;

    [Test]
    public async Task ApiVersionsResponse_Read_FlexibleApiKeyCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        WriteHostileCompactArray(ref writer);

        await AssertMalformedApiVersions(buffer, version: 3);
    }

    [Test]
    public async Task ApiVersionsResponse_Read_LegacyApiKeyCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        WriteHostileLegacyArray(ref writer);

        await AssertMalformedApiVersions(buffer, version: 0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task ApiVersionsResponse_Read_FeatureCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData(
        int tag)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(1); // empty API keys
        writer.WriteInt32(0);          // throttle time
        writer.WriteUnsignedVarInt(1); // one tagged field
        writer.WriteUnsignedVarInt(tag);
        writer.WriteUnsignedVarInt(HostilePayloadLength + 1);
        WriteHostileCompactArray(ref writer);

        await AssertMalformedApiVersions(buffer, version: 3);
    }

    [Test]
    public async Task MetadataResponse_Read_BrokerCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0); // throttle time
        WriteHostileCompactArray(ref writer);

        await AssertMalformedMetadata(buffer);
    }

    [Test]
    public async Task MetadataResponse_Read_BrokerCountExceedingAbsoluteCap_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var hostileCount = MetadataResponse.MaxBrokerCount + 1;
        writer.WriteInt32(0); // throttle time
        writer.WriteUnsignedVarInt(hostileCount + 1);
        writer.WriteRawBytes(new byte[hostileCount * 11]);

        await AssertMalformedMetadata(buffer);
    }

    [Test]
    public async Task MetadataResponse_Read_TopicCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);          // throttle time
        writer.WriteUnsignedVarInt(1); // empty brokers
        writer.WriteUnsignedVarInt(0); // null cluster ID
        writer.WriteInt32(-1);         // controller ID
        WriteHostileCompactArray(ref writer);

        await AssertMalformedMetadata(buffer);
    }

    [Test]
    public async Task TopicMetadata_Read_PartitionCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactString(string.Empty);
        writer.WriteUuid(Guid.Empty);
        writer.WriteBoolean(false);
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadTopicMetadata(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task PartitionMetadata_Read_NodeCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData(
        int arrayIndex)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);  // partition index
        writer.WriteInt32(-1); // leader ID
        writer.WriteInt32(-1); // leader epoch
        for (var i = 0; i < arrayIndex; i++)
            writer.WriteUnsignedVarInt(1);
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadPartitionMetadata(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    [Arguments(7)]
    [Arguments(10)]
    public async Task OffsetFetchResponse_Read_TopLevelCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData(
        short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0); // throttle time
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadOffsetFetchResponse(buffer, version))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task OffsetFetchResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(Guid.Empty);
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadOffsetFetchTopic(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task OffsetFetchResponseGroup_Read_TopicCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadOffsetFetchGroup(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task AddPartitionsToTxnResponse_Read_TopicCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0); // throttle time
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadAddPartitionsToTxnResponse(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task AddPartitionsToTxnTopicResult_Read_PartitionCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadAddPartitionsToTxnTopic(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task ShareFetchResponse_Read_TopicCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        WriteShareFetchResponsePreamble(ref writer);
        WriteHostileCompactArray(ref writer);

        await AssertMalformedShareFetch(buffer);
    }

    [Test]
    public async Task ShareFetchResponse_Read_NodeEndpointCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        WriteShareFetchResponsePreamble(ref writer);
        writer.WriteUnsignedVarInt(1); // empty topic responses
        WriteHostileCompactArray(ref writer);

        await AssertMalformedShareFetch(buffer);
    }

    [Test]
    public async Task ShareFetchResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(Guid.Empty);
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadShareFetchTopic(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task ShareFetchResponsePartition_Read_AcquiredRecordCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);                  // partition index
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(0);         // null error message
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(0);         // null acknowledgement error message
        writer.WriteInt32(-1);                 // leader ID
        writer.WriteInt32(-1);                 // leader epoch
        writer.WriteUnsignedVarInt(0);         // leader tagged fields
        writer.WriteUnsignedVarInt(0);         // null records
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadShareFetchPartition(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task DescribeShareGroupOffsetsRequestGroup_Read_TopicCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadDescribeShareGroupOffsetsGroup(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task DescribeShareGroupOffsetsRequestTopic_Read_PartitionCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await Assert.That(() => ReadDescribeShareGroupOffsetsTopic(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task ConsumerGroupDescribeResponse_Read_GroupCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadConsumerGroupDescribeResponse(buffer));
    }

    [Test]
    public async Task ConsumerGroupDescribeGroup_Read_MemberCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadConsumerGroupDescribeGroup(buffer));
    }

    [Test]
    public async Task ConsumerGroupDescribeMember_Read_SubscribedTopicCountExceedingAbsoluteCap_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactNullableString(null);
        writer.WriteInt32(0);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        WriteCompactArrayAtAbsoluteCap(ref writer, ResponseArrayLimits.MaxTopicCount);

        await AssertMaximumRejected(
            () => ReadConsumerGroupDescribeMember(buffer),
            ResponseArrayLimits.MaxTopicCount + 1,
            ResponseArrayLimits.MaxTopicCount);
    }

    [Test]
    public async Task ConsumerGroupDescribeAssignment_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadConsumerGroupDescribeAssignment(buffer));
    }

    [Test]
    public async Task ConsumerGroupDescribeTopicPartitions_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(Guid.Empty);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadConsumerGroupDescribeTopicPartitions(buffer));
    }

    [Test]
    public async Task ConsumerGroupHeartbeatAssignment_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadConsumerGroupHeartbeatAssignment(buffer));
    }

    [Test]
    public async Task ConsumerGroupHeartbeatTopicPartitions_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(Guid.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadConsumerGroupHeartbeatTopicPartitions(buffer, version: 0));
    }

    [Test]
    public async Task ConsumerGroupHeartbeatTopicPartitions_Read_NewPartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(Guid.Empty);
        writer.WriteUnsignedVarInt(1);
        writer.WriteUnsignedVarInt(1);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(HostilePayloadLength + 1);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadConsumerGroupHeartbeatTopicPartitions(buffer, version: 2));
    }

    [Test]
    public async Task DeleteGroupsResponse_Read_ResultCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDeleteGroupsResponse(buffer));
    }

    [Test]
    public async Task DescribeGroupsResponse_Read_GroupCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDescribeGroupsResponse(buffer));
    }

    [Test]
    public async Task DescribeGroupsResponse_Read_SegmentedGroupCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);
        var sequence = SequenceTestHelpers.CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), splitAt: 5);

        await AssertMinimumSizeRejected(() => ReadDescribeGroupsResponse(sequence));
    }

    [Test]
    public async Task DescribeGroupsResponseGroup_Read_MemberCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactNullableString(null);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDescribeGroupsResponseGroup(buffer));
    }

    [Test]
    public async Task FindCoordinatorResponse_Read_CoordinatorCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadFindCoordinatorResponse(buffer));
    }

    [Test]
    [Arguments((short)4)]
    [Arguments((short)5)]
    public async Task LeaveGroupResponse_Read_MemberCountExceedingMinimumEncodedSize_RejectsBeforeAllocation(
        short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        if (LeaveGroupRequest.IsFlexibleVersion(version))
        {
            WriteHostileCompactArray(ref writer);
        }
        else
        {
            WriteHostileLegacyArray(ref writer);
        }

        await AssertMinimumSizeRejected(() => ReadLeaveGroupResponse(buffer, version));
    }

    [Test]
    public async Task ListGroupsResponse_Read_GroupCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadListGroupsResponse(buffer));
    }

    [Test]
    [Arguments((short)8)]
    [Arguments((short)10)]
    public async Task OffsetCommitResponse_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation(
        short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadOffsetCommitResponse(buffer, version));
    }

    [Test]
    public async Task OffsetCommitResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(Guid.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadOffsetCommitResponseTopic(buffer));
    }

    [Test]
    public async Task OffsetDeleteResponse_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);
        WriteHostileLegacyArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadOffsetDeleteResponse(buffer));
    }

    [Test]
    public async Task OffsetDeleteResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteString(string.Empty);
        WriteHostileLegacyArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadOffsetDeleteResponseTopic(buffer));
    }

    private static void WriteHostileCompactArray(ref KafkaProtocolWriter writer)
    {
        writer.WriteUnsignedVarInt(HostileElementCount + 1);
        writer.WriteRawBytes(new byte[HostilePayloadLength]);
    }

    private static void WriteHostileLegacyArray(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(HostileElementCount);
        writer.WriteRawBytes(new byte[HostilePayloadLength]);
    }

    private static void WriteCompactArrayAtAbsoluteCap(ref KafkaProtocolWriter writer, int maxCount)
    {
        writer.WriteUnsignedVarInt(maxCount + 2);
        writer.WriteRawBytes(new byte[maxCount + 1]);
    }

    private static async Task AssertMinimumSizeRejected(Action read)
    {
        var exception = Assert.Throws<MalformedProtocolDataException>(read);
        await Assert.That(exception.Message)
            .IsEqualTo(
                $"Invalid protocol data: claimed length {HostileElementCount} exceeds remaining data {HostilePayloadLength}");
    }

    private static async Task AssertMaximumRejected(Action read, int count, int maxCount)
    {
        var exception = Assert.Throws<MalformedProtocolDataException>(read);
        await Assert.That(exception.Message)
            .IsEqualTo($"Invalid protocol data: claimed element count {count} exceeds maximum {maxCount}");
    }

    private static void WriteShareFetchResponsePreamble(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(0); // null error message
    }

    private static async Task AssertMalformedApiVersions(ArrayBufferWriter<byte> buffer, short version)
    {
        await Assert.That(() => ReadApiVersions(buffer, version))
            .Throws<MalformedProtocolDataException>();
    }

    private static async Task AssertMalformedMetadata(ArrayBufferWriter<byte> buffer)
    {
        await Assert.That(() => ReadMetadata(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    private static async Task AssertMalformedShareFetch(ArrayBufferWriter<byte> buffer)
    {
        await Assert.That(() => ReadShareFetchResponse(buffer))
            .Throws<MalformedProtocolDataException>();
    }

    private static void ReadApiVersions(ArrayBufferWriter<byte> buffer, short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ApiVersionsResponse.Read(ref reader, version);
    }

    private static void ReadMetadata(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = MetadataResponse.Read(ref reader, version: 13);
    }

    private static void ReadTopicMetadata(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = TopicMetadata.Read(ref reader, version: 13);
    }

    private static void ReadPartitionMetadata(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = PartitionMetadata.Read(ref reader, version: 13);
    }

    private static void ReadOffsetFetchResponse(ArrayBufferWriter<byte> buffer, short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = OffsetFetchResponse.Read(ref reader, version);
    }

    private static void ReadOffsetFetchTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = OffsetFetchResponseTopic.Read(ref reader, version: 10);
    }

    private static void ReadOffsetFetchGroup(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = OffsetFetchResponseGroup.Read(ref reader, version: 10);
    }

    private static void ReadAddPartitionsToTxnResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = AddPartitionsToTxnResponse.Read(ref reader, version: 3);
    }

    private static void ReadAddPartitionsToTxnTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = AddPartitionsToTxnTopicResult.Read(ref reader, version: 3);
    }

    private static void ReadShareFetchResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ShareFetchResponse.Read(ref reader, version: 0);
    }

    private static void ReadShareFetchTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ShareFetchResponseTopic.Read(ref reader, version: 0);
    }

    private static void ReadShareFetchPartition(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ShareFetchResponsePartition.Read(ref reader, version: 0);
    }

    private static void ReadDescribeShareGroupOffsetsGroup(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeShareGroupOffsetsRequestGroup.Read(ref reader);
    }

    private static void ReadDescribeShareGroupOffsetsTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeShareGroupOffsetsRequestTopic.Read(ref reader);
    }

    private static void ReadConsumerGroupDescribeResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ConsumerGroupDescribeResponse.Read(ref reader, version: 0);
    }

    private static void ReadConsumerGroupDescribeGroup(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ConsumerGroupDescribeGroup.Read(ref reader, version: 0);
    }

    private static void ReadConsumerGroupDescribeMember(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ConsumerGroupDescribeMember.Read(ref reader, version: 0);
    }

    private static void ReadConsumerGroupDescribeAssignment(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ConsumerGroupDescribeAssignment.Read(ref reader);
    }

    private static void ReadConsumerGroupDescribeTopicPartitions(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ConsumerGroupDescribeTopicPartitions.Read(ref reader);
    }

    private static void ReadConsumerGroupHeartbeatAssignment(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ConsumerGroupHeartbeatAssignment.Read(ref reader, version: 0);
    }

    private static void ReadConsumerGroupHeartbeatTopicPartitions(
        ArrayBufferWriter<byte> buffer,
        short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ConsumerGroupHeartbeatTopicPartitions.Read(ref reader, version);
    }

    private static void ReadDeleteGroupsResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DeleteGroupsResponse.Read(ref reader, version: 2);
    }

    private static void ReadDescribeGroupsResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeGroupsResponse.Read(ref reader, version: 5);
    }

    private static void ReadDescribeGroupsResponse(ReadOnlySequence<byte> sequence)
    {
        var reader = new KafkaProtocolReader(sequence);
        _ = DescribeGroupsResponse.Read(ref reader, version: 5);
    }

    private static void ReadDescribeGroupsResponseGroup(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeGroupsResponseGroup.Read(ref reader, version: 5);
    }

    private static void ReadFindCoordinatorResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = FindCoordinatorResponse.Read(ref reader, version: 4);
    }

    private static void ReadLeaveGroupResponse(ArrayBufferWriter<byte> buffer, short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = LeaveGroupResponse.Read(ref reader, version);
    }

    private static void ReadListGroupsResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ListGroupsResponse.Read(ref reader, version: 3);
    }

    private static void ReadOffsetCommitResponse(ArrayBufferWriter<byte> buffer, short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = OffsetCommitResponse.Read(ref reader, version);
    }

    private static void ReadOffsetCommitResponseTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = OffsetCommitResponseTopic.Read(ref reader, version: 10);
    }

    private static void ReadOffsetDeleteResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = OffsetDeleteResponse.Read(ref reader, version: 0);
    }

    private static void ReadOffsetDeleteResponseTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = OffsetDeleteResponseTopic.Read(ref reader, version: 0);
    }
}
