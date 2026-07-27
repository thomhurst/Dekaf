using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class TopologyResponseArrayBoundsTests
{
    private const int HostileElementCount = 40;
    private const int HostilePayloadLength = 60;

    [Test]
    [Arguments(ArrayTarget.AlterReassignmentTopicsV0)]
    [Arguments(ArrayTarget.AlterReassignmentTopicsV1)]
    [Arguments(ArrayTarget.AlterReassignmentPartitions)]
    [Arguments(ArrayTarget.ListReassignmentTopics)]
    [Arguments(ArrayTarget.ListReassignmentPartitions)]
    [Arguments(ArrayTarget.ListReplicas)]
    [Arguments(ArrayTarget.ListAddingReplicas)]
    [Arguments(ArrayTarget.ListRemovingReplicas)]
    [Arguments(ArrayTarget.AlterLogDirTopicsLegacy)]
    [Arguments(ArrayTarget.AlterLogDirTopicsFlexible)]
    [Arguments(ArrayTarget.AlterLogDirPartitionsLegacy)]
    [Arguments(ArrayTarget.AlterLogDirPartitionsFlexible)]
    [Arguments(ArrayTarget.DescribeLogDirsLegacy)]
    [Arguments(ArrayTarget.DescribeLogDirsFlexibleV2)]
    [Arguments(ArrayTarget.DescribeLogDirsFlexibleV4)]
    [Arguments(ArrayTarget.DescribeLogDirsFlexibleV5)]
    [Arguments(ArrayTarget.DescribeLogDirTopicsLegacy)]
    [Arguments(ArrayTarget.DescribeLogDirTopicsFlexible)]
    [Arguments(ArrayTarget.DescribeLogDirPartitionsLegacy)]
    [Arguments(ArrayTarget.DescribeLogDirPartitionsFlexible)]
    [Arguments(ArrayTarget.QuorumTopicsV0)]
    [Arguments(ArrayTarget.QuorumTopicsV2)]
    [Arguments(ArrayTarget.QuorumNodes)]
    [Arguments(ArrayTarget.QuorumPartitionsV0)]
    [Arguments(ArrayTarget.QuorumPartitionsV2)]
    [Arguments(ArrayTarget.QuorumVotersV0)]
    [Arguments(ArrayTarget.QuorumVotersV1)]
    [Arguments(ArrayTarget.QuorumVotersV2)]
    [Arguments(ArrayTarget.QuorumObserversV2)]
    [Arguments(ArrayTarget.QuorumListeners)]
    [Arguments(ArrayTarget.DescribeTopicTopics)]
    [Arguments(ArrayTarget.DescribeTopicPartitions)]
    [Arguments(ArrayTarget.DescribeTopicReplicas)]
    [Arguments(ArrayTarget.DescribeTopicIsr)]
    [Arguments(ArrayTarget.DescribeTopicEligibleLeaders)]
    [Arguments(ArrayTarget.DescribeTopicLastKnownElr)]
    [Arguments(ArrayTarget.DescribeTopicOfflineReplicas)]
    [Arguments(ArrayTarget.ElectLeaderTopics)]
    [Arguments(ArrayTarget.ElectLeaderPartitions)]
    public async Task Read_HostileArrayCount_RejectsBeforeAllocation(ArrayTarget target)
    {
        var payload = CreatePayload(target);

        await Assert.That(() => ReadContiguous(payload, target))
            .ThrowsExactly<MalformedProtocolDataException>();
        await Assert.That(() => ReadSegmented(payload, target))
            .ThrowsExactly<MalformedProtocolDataException>();
    }

    private static byte[] CreatePayload(ArrayTarget target)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        switch (target)
        {
            case ArrayTarget.AlterReassignmentTopicsV0:
            case ArrayTarget.AlterReassignmentTopicsV1:
                writer.WriteInt32(0);
                if (target is ArrayTarget.AlterReassignmentTopicsV1)
                    writer.WriteBoolean(false);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.AlterReassignmentPartitions:
            case ArrayTarget.ListReassignmentPartitions:
            case ArrayTarget.ElectLeaderPartitions:
                writer.WriteCompactString(string.Empty);
                break;
            case ArrayTarget.ListReassignmentTopics:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.ListReplicas:
            case ArrayTarget.ListAddingReplicas:
            case ArrayTarget.ListRemovingReplicas:
                writer.WriteInt32(0);
                WriteEmptyCompactArrays(
                    ref writer,
                    target - ArrayTarget.ListReplicas);
                break;
            case ArrayTarget.AlterLogDirTopicsLegacy:
            case ArrayTarget.AlterLogDirTopicsFlexible:
            case ArrayTarget.DescribeLogDirsLegacy:
            case ArrayTarget.DescribeLogDirsFlexibleV2:
            case ArrayTarget.DescribeLogDirsFlexibleV4:
            case ArrayTarget.DescribeLogDirsFlexibleV5:
                writer.WriteInt32(0);
                if (target is ArrayTarget.DescribeLogDirsFlexibleV4 or ArrayTarget.DescribeLogDirsFlexibleV5)
                    writer.WriteInt16((short)ErrorCode.None);
                break;
            case ArrayTarget.AlterLogDirPartitionsLegacy:
            case ArrayTarget.DescribeLogDirPartitionsLegacy:
                writer.WriteString(string.Empty);
                break;
            case ArrayTarget.AlterLogDirPartitionsFlexible:
            case ArrayTarget.DescribeLogDirPartitionsFlexible:
                writer.WriteCompactString(string.Empty);
                break;
            case ArrayTarget.DescribeLogDirTopicsLegacy:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteString(string.Empty);
                break;
            case ArrayTarget.DescribeLogDirTopicsFlexible:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactString(string.Empty);
                break;
            case ArrayTarget.QuorumTopicsV0:
                writer.WriteInt16((short)ErrorCode.None);
                break;
            case ArrayTarget.QuorumTopicsV2:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.QuorumNodes:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                writer.WriteUnsignedVarInt(1);
                break;
            case ArrayTarget.QuorumPartitionsV0:
            case ArrayTarget.QuorumPartitionsV2:
                writer.WriteCompactString(string.Empty);
                break;
            case ArrayTarget.QuorumVotersV0:
                WriteQuorumPartitionPreamble(ref writer, version: 0);
                break;
            case ArrayTarget.QuorumVotersV1:
                WriteQuorumPartitionPreamble(ref writer, version: 1);
                break;
            case ArrayTarget.QuorumVotersV2:
            case ArrayTarget.QuorumObserversV2:
                WriteQuorumPartitionPreamble(ref writer, version: 2);
                if (target is ArrayTarget.QuorumObserversV2)
                    writer.WriteUnsignedVarInt(1);
                break;
            case ArrayTarget.QuorumListeners:
                writer.WriteInt32(0);
                break;
            case ArrayTarget.DescribeTopicTopics:
                writer.WriteInt32(0);
                break;
            case ArrayTarget.DescribeTopicPartitions:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                writer.WriteUuid(Guid.Empty);
                writer.WriteBoolean(false);
                break;
            case ArrayTarget.DescribeTopicReplicas:
            case ArrayTarget.DescribeTopicIsr:
            case ArrayTarget.DescribeTopicEligibleLeaders:
            case ArrayTarget.DescribeTopicLastKnownElr:
            case ArrayTarget.DescribeTopicOfflineReplicas:
                WriteDescribeTopicPartitionPreamble(ref writer);
                WriteEmptyCompactArrays(
                    ref writer,
                    target - ArrayTarget.DescribeTopicReplicas);
                break;
            case ArrayTarget.ElectLeaderTopics:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }

        WriteHostileArray(ref writer, UsesLegacyEncoding(target));
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteQuorumPartitionPreamble(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        if (version >= 2)
            writer.WriteCompactNullableString(null);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt64(0);
    }

    private static void WriteDescribeTopicPartitionPreamble(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
    }

    private static void WriteEmptyCompactArrays(ref KafkaProtocolWriter writer, int count)
    {
        for (var index = 0; index < count; index++)
            writer.WriteUnsignedVarInt(1);
    }

    private static void WriteHostileArray(ref KafkaProtocolWriter writer, bool legacy)
    {
        if (legacy)
            writer.WriteInt32(HostileElementCount);
        else
            writer.WriteUnsignedVarInt(HostileElementCount + 1);
        writer.WriteRawBytes(new byte[HostilePayloadLength]);
    }

    private static bool UsesLegacyEncoding(ArrayTarget target) =>
        target is ArrayTarget.AlterLogDirTopicsLegacy
            or ArrayTarget.AlterLogDirPartitionsLegacy
            or ArrayTarget.DescribeLogDirsLegacy
            or ArrayTarget.DescribeLogDirTopicsLegacy
            or ArrayTarget.DescribeLogDirPartitionsLegacy;

    private static void ReadContiguous(byte[] payload, ArrayTarget target)
    {
        var reader = new KafkaProtocolReader(payload);
        ReadTarget(ref reader, target);
    }

    private static void ReadSegmented(byte[] payload, ArrayTarget target)
    {
        var sequence = SequenceTestHelpers.CreateMultiSegmentSequence(payload, payload.Length / 2);
        var reader = new KafkaProtocolReader(sequence);
        ReadTarget(ref reader, target);
    }

    private static void ReadTarget(ref KafkaProtocolReader reader, ArrayTarget target)
    {
        switch (target)
        {
            case ArrayTarget.AlterReassignmentTopicsV0:
                _ = AlterPartitionReassignmentsResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.AlterReassignmentTopicsV1:
                _ = AlterPartitionReassignmentsResponse.Read(ref reader, version: 1);
                break;
            case ArrayTarget.AlterReassignmentPartitions:
                _ = AlterPartitionReassignmentsResponseTopic.Read(ref reader, version: 0);
                break;
            case ArrayTarget.ListReassignmentTopics:
                _ = ListPartitionReassignmentsResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.ListReassignmentPartitions:
                _ = ListPartitionReassignmentsResponseTopic.Read(ref reader, version: 0);
                break;
            case ArrayTarget.ListReplicas:
            case ArrayTarget.ListAddingReplicas:
            case ArrayTarget.ListRemovingReplicas:
                _ = OngoingPartitionReassignmentData.Read(ref reader, version: 0);
                break;
            case ArrayTarget.AlterLogDirTopicsLegacy:
                _ = AlterReplicaLogDirsResponse.Read(ref reader, version: 1);
                break;
            case ArrayTarget.AlterLogDirTopicsFlexible:
                _ = AlterReplicaLogDirsResponse.Read(ref reader, version: 2);
                break;
            case ArrayTarget.AlterLogDirPartitionsLegacy:
                _ = AlterReplicaLogDirsResponseTopic.Read(ref reader, version: 1);
                break;
            case ArrayTarget.AlterLogDirPartitionsFlexible:
                _ = AlterReplicaLogDirsResponseTopic.Read(ref reader, version: 2);
                break;
            case ArrayTarget.DescribeLogDirsLegacy:
                _ = DescribeLogDirsResponse.Read(ref reader, version: 1);
                break;
            case ArrayTarget.DescribeLogDirsFlexibleV2:
                _ = DescribeLogDirsResponse.Read(ref reader, version: 2);
                break;
            case ArrayTarget.DescribeLogDirsFlexibleV4:
                _ = DescribeLogDirsResponse.Read(ref reader, version: 4);
                break;
            case ArrayTarget.DescribeLogDirsFlexibleV5:
                _ = DescribeLogDirsResponse.Read(ref reader, version: 5);
                break;
            case ArrayTarget.DescribeLogDirTopicsLegacy:
                _ = DescribeLogDirsResponseDir.Read(ref reader, version: 1);
                break;
            case ArrayTarget.DescribeLogDirTopicsFlexible:
                _ = DescribeLogDirsResponseDir.Read(ref reader, version: 2);
                break;
            case ArrayTarget.DescribeLogDirPartitionsLegacy:
                _ = DescribeLogDirsResponseTopic.Read(ref reader, version: 1);
                break;
            case ArrayTarget.DescribeLogDirPartitionsFlexible:
                _ = DescribeLogDirsResponseTopic.Read(ref reader, version: 2);
                break;
            case ArrayTarget.QuorumTopicsV0:
                _ = DescribeQuorumResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.QuorumTopicsV2:
            case ArrayTarget.QuorumNodes:
                _ = DescribeQuorumResponse.Read(ref reader, version: 2);
                break;
            case ArrayTarget.QuorumPartitionsV0:
                _ = DescribeQuorumResponseTopic.Read(ref reader, version: 0);
                break;
            case ArrayTarget.QuorumPartitionsV2:
                _ = DescribeQuorumResponseTopic.Read(ref reader, version: 2);
                break;
            case ArrayTarget.QuorumVotersV0:
                _ = DescribeQuorumResponsePartition.Read(ref reader, version: 0);
                break;
            case ArrayTarget.QuorumVotersV1:
                _ = DescribeQuorumResponsePartition.Read(ref reader, version: 1);
                break;
            case ArrayTarget.QuorumVotersV2:
            case ArrayTarget.QuorumObserversV2:
                _ = DescribeQuorumResponsePartition.Read(ref reader, version: 2);
                break;
            case ArrayTarget.QuorumListeners:
                _ = DescribeQuorumResponseNode.Read(ref reader);
                break;
            case ArrayTarget.DescribeTopicTopics:
                _ = DescribeTopicPartitionsResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.DescribeTopicPartitions:
                _ = DescribeTopicPartitionsResponseTopic.Read(ref reader);
                break;
            case ArrayTarget.DescribeTopicReplicas:
            case ArrayTarget.DescribeTopicIsr:
            case ArrayTarget.DescribeTopicEligibleLeaders:
            case ArrayTarget.DescribeTopicLastKnownElr:
            case ArrayTarget.DescribeTopicOfflineReplicas:
                _ = DescribeTopicPartitionsResponsePartition.Read(ref reader);
                break;
            case ArrayTarget.ElectLeaderTopics:
                _ = ElectLeadersResponse.Read(ref reader, version: 2);
                break;
            case ArrayTarget.ElectLeaderPartitions:
                _ = ElectLeadersResponseTopic.Read(ref reader, version: 2);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    public enum ArrayTarget
    {
        AlterReassignmentTopicsV0,
        AlterReassignmentTopicsV1,
        AlterReassignmentPartitions,
        ListReassignmentTopics,
        ListReassignmentPartitions,
        ListReplicas,
        ListAddingReplicas,
        ListRemovingReplicas,
        AlterLogDirTopicsLegacy,
        AlterLogDirTopicsFlexible,
        AlterLogDirPartitionsLegacy,
        AlterLogDirPartitionsFlexible,
        DescribeLogDirsLegacy,
        DescribeLogDirsFlexibleV2,
        DescribeLogDirsFlexibleV4,
        DescribeLogDirsFlexibleV5,
        DescribeLogDirTopicsLegacy,
        DescribeLogDirTopicsFlexible,
        DescribeLogDirPartitionsLegacy,
        DescribeLogDirPartitionsFlexible,
        QuorumTopicsV0,
        QuorumTopicsV2,
        QuorumNodes,
        QuorumPartitionsV0,
        QuorumPartitionsV2,
        QuorumVotersV0,
        QuorumVotersV1,
        QuorumVotersV2,
        QuorumObserversV2,
        QuorumListeners,
        DescribeTopicTopics,
        DescribeTopicPartitions,
        DescribeTopicReplicas,
        DescribeTopicIsr,
        DescribeTopicEligibleLeaders,
        DescribeTopicLastKnownElr,
        DescribeTopicOfflineReplicas,
        ElectLeaderTopics,
        ElectLeaderPartitions
    }
}
