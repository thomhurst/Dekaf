using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class ShareResponseArrayBoundsTests
{
    private const int HostileElementCount = 40;
    private const int HostilePayloadLength = 60;
    private const int MaxStringElementCount = 1_000_000;

    [Test]
    [Arguments(ResponseArrayTarget.AlterTopics)]
    [Arguments(ResponseArrayTarget.AlterPartitions)]
    [Arguments(ResponseArrayTarget.DeleteTopics)]
    [Arguments(ResponseArrayTarget.DescribeOffsetGroups)]
    [Arguments(ResponseArrayTarget.DescribeOffsetTopics)]
    [Arguments(ResponseArrayTarget.DescribeOffsetPartitionsV0)]
    [Arguments(ResponseArrayTarget.DescribeOffsetPartitionsV1)]
    [Arguments(ResponseArrayTarget.AcknowledgeTopics)]
    [Arguments(ResponseArrayTarget.AcknowledgeNodes)]
    [Arguments(ResponseArrayTarget.AcknowledgePartitions)]
    [Arguments(ResponseArrayTarget.ShareDescribeGroups)]
    [Arguments(ResponseArrayTarget.ShareDescribeMembers)]
    [Arguments(ResponseArrayTarget.ShareDescribeSubscribedTopics)]
    [Arguments(ResponseArrayTarget.ShareDescribeTopicPartitions)]
    [Arguments(ResponseArrayTarget.ShareDescribePartitions)]
    [Arguments(ResponseArrayTarget.HeartbeatTopicPartitions)]
    [Arguments(ResponseArrayTarget.HeartbeatPartitions)]
    [Arguments(ResponseArrayTarget.StreamsGroups)]
    [Arguments(ResponseArrayTarget.StreamsMembers)]
    [Arguments(ResponseArrayTarget.StreamsSubtopologies)]
    [Arguments(ResponseArrayTarget.StreamsSourceTopics)]
    [Arguments(ResponseArrayTarget.StreamsRepartitionSinkTopics)]
    [Arguments(ResponseArrayTarget.StreamsStateChangelogTopics)]
    [Arguments(ResponseArrayTarget.StreamsRepartitionSourceTopics)]
    [Arguments(ResponseArrayTarget.StreamsTopicConfigs)]
    [Arguments(ResponseArrayTarget.StreamsClientTags)]
    [Arguments(ResponseArrayTarget.StreamsTaskOffsets)]
    [Arguments(ResponseArrayTarget.StreamsTaskEndOffsets)]
    [Arguments(ResponseArrayTarget.StreamsActiveTasks)]
    [Arguments(ResponseArrayTarget.StreamsStandbyTasks)]
    [Arguments(ResponseArrayTarget.StreamsWarmupTasks)]
    [Arguments(ResponseArrayTarget.StreamsTaskPartitions)]
    public async Task Read_HostileArrayCount_RejectsBeforeAllocation(ResponseArrayTarget target)
    {
        var payload = CreatePayload(target);
        var expectedMessage = UsesAbsoluteCap(target) ? "exceeds maximum" : "Invalid protocol data";

        await Assert.That(() => ReadContiguous(payload, target))
            .ThrowsExactly<MalformedProtocolDataException>()
            .WithMessageContaining(expectedMessage);
        await Assert.That(() => ReadSegmented(payload, target))
            .ThrowsExactly<MalformedProtocolDataException>()
            .WithMessageContaining(expectedMessage);
    }

    private static byte[] CreatePayload(ResponseArrayTarget target)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        switch (target)
        {
            case ResponseArrayTarget.AlterTopics:
            case ResponseArrayTarget.DeleteTopics:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ResponseArrayTarget.AlterPartitions:
                writer.WriteCompactString(string.Empty);
                writer.WriteUuid(Guid.Empty);
                break;
            case ResponseArrayTarget.DescribeOffsetGroups:
            case ResponseArrayTarget.ShareDescribeGroups:
            case ResponseArrayTarget.StreamsGroups:
                writer.WriteInt32(0);
                break;
            case ResponseArrayTarget.DescribeOffsetTopics:
                writer.WriteCompactString(string.Empty);
                break;
            case ResponseArrayTarget.DescribeOffsetPartitionsV0:
            case ResponseArrayTarget.DescribeOffsetPartitionsV1:
                writer.WriteCompactString(string.Empty);
                writer.WriteUuid(Guid.Empty);
                break;
            case ResponseArrayTarget.AcknowledgeTopics:
                WriteAcknowledgePreamble(ref writer);
                break;
            case ResponseArrayTarget.AcknowledgeNodes:
                WriteAcknowledgePreamble(ref writer);
                writer.WriteUnsignedVarInt(1);
                break;
            case ResponseArrayTarget.AcknowledgePartitions:
                writer.WriteUuid(Guid.Empty);
                break;
            case ResponseArrayTarget.ShareDescribeMembers:
                WriteShareDescribeGroupPreamble(ref writer);
                break;
            case ResponseArrayTarget.ShareDescribeSubscribedTopics:
                WriteShareDescribeMemberPreamble(ref writer);
                break;
            case ResponseArrayTarget.ShareDescribeTopicPartitions:
                break;
            case ResponseArrayTarget.ShareDescribePartitions:
                writer.WriteUuid(Guid.Empty);
                writer.WriteCompactNullableString(null);
                break;
            case ResponseArrayTarget.HeartbeatTopicPartitions:
                break;
            case ResponseArrayTarget.HeartbeatPartitions:
                writer.WriteUuid(Guid.Empty);
                break;
            case ResponseArrayTarget.StreamsMembers:
                WriteStreamsGroupPreamble(ref writer);
                break;
            case ResponseArrayTarget.StreamsSubtopologies:
                writer.WriteInt8(1);
                writer.WriteInt32(0);
                break;
            case ResponseArrayTarget.StreamsSourceTopics:
            case ResponseArrayTarget.StreamsRepartitionSinkTopics:
            case ResponseArrayTarget.StreamsStateChangelogTopics:
            case ResponseArrayTarget.StreamsRepartitionSourceTopics:
                writer.WriteCompactString(string.Empty);
                WriteEmptyArraysBefore(ref writer, target - ResponseArrayTarget.StreamsSourceTopics);
                break;
            case ResponseArrayTarget.StreamsTopicConfigs:
                writer.WriteCompactString(string.Empty);
                writer.WriteInt32(0);
                writer.WriteInt16(0);
                break;
            case ResponseArrayTarget.StreamsClientTags:
            case ResponseArrayTarget.StreamsTaskOffsets:
            case ResponseArrayTarget.StreamsTaskEndOffsets:
                WriteStreamsMemberPreamble(ref writer);
                WriteEmptyArraysBefore(ref writer, target - ResponseArrayTarget.StreamsClientTags);
                break;
            case ResponseArrayTarget.StreamsActiveTasks:
            case ResponseArrayTarget.StreamsStandbyTasks:
            case ResponseArrayTarget.StreamsWarmupTasks:
                WriteEmptyArraysBefore(ref writer, target - ResponseArrayTarget.StreamsActiveTasks);
                break;
            case ResponseArrayTarget.StreamsTaskPartitions:
                writer.WriteCompactString(string.Empty);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }

        if (UsesAbsoluteCap(target))
        {
            writer.WriteUnsignedVarInt(MaxStringElementCount + 2);
            writer.WriteRawBytes(new byte[MaxStringElementCount + 1]);
        }
        else
        {
            writer.WriteUnsignedVarInt(HostileElementCount + 1);
            writer.WriteRawBytes(new byte[HostilePayloadLength]);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteAcknowledgePreamble(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
    }

    private static void WriteShareDescribeGroupPreamble(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteCompactNullableString(null);
    }

    private static void WriteShareDescribeMemberPreamble(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactNullableString(null);
        writer.WriteInt32(0);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
    }

    private static void WriteStreamsGroupPreamble(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt8(-1);
    }

    private static void WriteStreamsMemberPreamble(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(string.Empty);
        writer.WriteInt32(0);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt32(0);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt8(-1);
    }

    private static void WriteEmptyArraysBefore(ref KafkaProtocolWriter writer, int count)
    {
        for (var index = 0; index < count; index++)
            writer.WriteUnsignedVarInt(1);
    }

    private static bool UsesAbsoluteCap(ResponseArrayTarget target) =>
        target is ResponseArrayTarget.ShareDescribeSubscribedTopics
            or ResponseArrayTarget.StreamsSourceTopics
            or ResponseArrayTarget.StreamsRepartitionSinkTopics;

    private static void ReadContiguous(byte[] payload, ResponseArrayTarget target)
    {
        var reader = new KafkaProtocolReader(payload);
        ReadTarget(ref reader, target);
    }

    private static void ReadSegmented(byte[] payload, ResponseArrayTarget target)
    {
        var sequence = SequenceTestHelpers.CreateMultiSegmentSequence(payload, payload.Length / 2);
        var reader = new KafkaProtocolReader(sequence);
        ReadTarget(ref reader, target);
    }

    private static void ReadTarget(ref KafkaProtocolReader reader, ResponseArrayTarget target)
    {
        switch (target)
        {
            case ResponseArrayTarget.AlterTopics:
                _ = AlterShareGroupOffsetsResponse.Read(ref reader, version: 0);
                break;
            case ResponseArrayTarget.AlterPartitions:
                _ = AlterShareGroupOffsetsResponseTopic.Read(ref reader);
                break;
            case ResponseArrayTarget.DeleteTopics:
                _ = DeleteShareGroupOffsetsResponse.Read(ref reader, version: 0);
                break;
            case ResponseArrayTarget.DescribeOffsetGroups:
                _ = DescribeShareGroupOffsetsResponse.Read(ref reader, version: 0);
                break;
            case ResponseArrayTarget.DescribeOffsetTopics:
                _ = DescribeShareGroupOffsetsResponseGroup.Read(ref reader, version: 0);
                break;
            case ResponseArrayTarget.DescribeOffsetPartitionsV0:
                _ = DescribeShareGroupOffsetsResponseTopic.Read(ref reader, version: 0);
                break;
            case ResponseArrayTarget.DescribeOffsetPartitionsV1:
                _ = DescribeShareGroupOffsetsResponseTopic.Read(ref reader, version: 1);
                break;
            case ResponseArrayTarget.AcknowledgeTopics:
            case ResponseArrayTarget.AcknowledgeNodes:
                _ = ShareAcknowledgeResponse.Read(ref reader, version: 1);
                break;
            case ResponseArrayTarget.AcknowledgePartitions:
                _ = ShareAcknowledgeResponseTopic.Read(ref reader);
                break;
            case ResponseArrayTarget.ShareDescribeGroups:
                _ = ShareGroupDescribeResponse.Read(ref reader, version: 1);
                break;
            case ResponseArrayTarget.ShareDescribeMembers:
                _ = ShareGroupDescribeGroup.Read(ref reader);
                break;
            case ResponseArrayTarget.ShareDescribeSubscribedTopics:
                _ = ShareGroupDescribeMember.Read(ref reader);
                break;
            case ResponseArrayTarget.ShareDescribeTopicPartitions:
                _ = ShareGroupDescribeAssignment.Read(ref reader);
                break;
            case ResponseArrayTarget.ShareDescribePartitions:
                _ = ShareGroupDescribeTopicPartitions.Read(ref reader);
                break;
            case ResponseArrayTarget.HeartbeatTopicPartitions:
                _ = ShareGroupHeartbeatAssignment.Read(ref reader);
                break;
            case ResponseArrayTarget.HeartbeatPartitions:
                _ = ShareGroupHeartbeatTopicPartitions.Read(ref reader);
                break;
            case ResponseArrayTarget.StreamsGroups:
                _ = StreamsGroupDescribeResponse.Read(ref reader, version: 0);
                break;
            case ResponseArrayTarget.StreamsMembers:
                _ = StreamsGroupDescribeGroup.Read(ref reader);
                break;
            case ResponseArrayTarget.StreamsSubtopologies:
                _ = StreamsGroupDescribeTopology.ReadNullable(ref reader);
                break;
            case ResponseArrayTarget.StreamsSourceTopics:
            case ResponseArrayTarget.StreamsRepartitionSinkTopics:
            case ResponseArrayTarget.StreamsStateChangelogTopics:
            case ResponseArrayTarget.StreamsRepartitionSourceTopics:
                _ = StreamsGroupDescribeSubtopology.Read(ref reader);
                break;
            case ResponseArrayTarget.StreamsTopicConfigs:
                _ = StreamsGroupDescribeTopicInfo.Read(ref reader);
                break;
            case ResponseArrayTarget.StreamsClientTags:
            case ResponseArrayTarget.StreamsTaskOffsets:
            case ResponseArrayTarget.StreamsTaskEndOffsets:
                _ = StreamsGroupDescribeMember.Read(ref reader);
                break;
            case ResponseArrayTarget.StreamsActiveTasks:
            case ResponseArrayTarget.StreamsStandbyTasks:
            case ResponseArrayTarget.StreamsWarmupTasks:
                _ = StreamsGroupDescribeAssignment.Read(ref reader);
                break;
            case ResponseArrayTarget.StreamsTaskPartitions:
                _ = StreamsGroupDescribeTaskIds.Read(ref reader);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    public enum ResponseArrayTarget
    {
        AlterTopics,
        AlterPartitions,
        DeleteTopics,
        DescribeOffsetGroups,
        DescribeOffsetTopics,
        DescribeOffsetPartitionsV0,
        DescribeOffsetPartitionsV1,
        AcknowledgeTopics,
        AcknowledgeNodes,
        AcknowledgePartitions,
        ShareDescribeGroups,
        ShareDescribeMembers,
        ShareDescribeSubscribedTopics,
        ShareDescribeTopicPartitions,
        ShareDescribePartitions,
        HeartbeatTopicPartitions,
        HeartbeatPartitions,
        StreamsGroups,
        StreamsMembers,
        StreamsSubtopologies,
        StreamsSourceTopics,
        StreamsRepartitionSinkTopics,
        StreamsStateChangelogTopics,
        StreamsRepartitionSourceTopics,
        StreamsTopicConfigs,
        StreamsClientTags,
        StreamsTaskOffsets,
        StreamsTaskEndOffsets,
        StreamsActiveTasks,
        StreamsStandbyTasks,
        StreamsWarmupTasks,
        StreamsTaskPartitions
    }
}
