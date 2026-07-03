using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class PartitionReassignmentMessageTests
{
    [Test]
    public async Task AlterPartitionReassignmentsRequest_HasCorrectApiMetadata()
    {
        await Assert.That(AlterPartitionReassignmentsRequest.ApiKey).IsEqualTo(ApiKey.AlterPartitionReassignments);
        await Assert.That(AlterPartitionReassignmentsRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(AlterPartitionReassignmentsRequest.HighestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task AlterPartitionReassignmentsRequest_V0_EncodesCancel()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = CreateAlterRequest(null, allowReplicationFactorChange: false);

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var timeoutMs = reader.ReadInt32();
        var topicCount = reader.ReadUnsignedVarInt() - 1;
        var topicName = reader.ReadCompactString();
        var partitionCount = reader.ReadUnsignedVarInt() - 1;
        var partitionIndex = reader.ReadInt32();
        var replicasLength = reader.ReadUnsignedVarInt();
        reader.SkipTaggedFields();
        reader.SkipTaggedFields();
        reader.SkipTaggedFields();

        await Assert.That(timeoutMs).IsEqualTo(60000);
        await Assert.That(topicCount).IsEqualTo(1);
        await Assert.That(topicName).IsEqualTo("test-topic");
        await Assert.That(partitionCount).IsEqualTo(1);
        await Assert.That(partitionIndex).IsEqualTo(0);
        await Assert.That(replicasLength).IsEqualTo(0);
    }

    [Test]
    public async Task AlterPartitionReassignmentsRequest_V1_EncodesReplicasAndAllowReplicationFactorChange()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = CreateAlterRequest([1, 2, 3], allowReplicationFactorChange: false);

        request.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var timeoutMs = reader.ReadInt32();
        var allowReplicationFactorChange = reader.ReadBoolean();
        var topicCount = reader.ReadUnsignedVarInt() - 1;
        _ = reader.ReadCompactString();
        var partitionCount = reader.ReadUnsignedVarInt() - 1;
        _ = reader.ReadInt32();
        var replicasCount = reader.ReadUnsignedVarInt() - 1;
        var replica1 = reader.ReadInt32();
        var replica2 = reader.ReadInt32();
        var replica3 = reader.ReadInt32();

        await Assert.That(timeoutMs).IsEqualTo(60000);
        await Assert.That(allowReplicationFactorChange).IsFalse();
        await Assert.That(topicCount).IsEqualTo(1);
        await Assert.That(partitionCount).IsEqualTo(1);
        await Assert.That(replicasCount).IsEqualTo(3);
        await Assert.That(replica1).IsEqualTo(1);
        await Assert.That(replica2).IsEqualTo(2);
        await Assert.That(replica3).IsEqualTo(3);
    }

    [Test]
    public async Task AlterPartitionReassignmentsResponse_V1_CanBeParsed()
    {
        var data = BuildAlterResponse(version: 1, errorCode: ErrorCode.None);
        var reader = new KafkaProtocolReader(data);

        var response = (AlterPartitionReassignmentsResponse)AlterPartitionReassignmentsResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(12);
        await Assert.That(response.AllowReplicationFactorChange).IsTrue();
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].Name).IsEqualTo("test-topic");
        await Assert.That(response.Responses[0].Partitions[0].PartitionIndex).IsEqualTo(0);
    }

    [Test]
    public async Task ListPartitionReassignmentsRequest_NullTopics_EncodesListAll()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new ListPartitionReassignmentsRequest
        {
            TimeoutMs = 60000,
            Topics = null
        };

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var timeoutMs = reader.ReadInt32();
        var topicsLength = reader.ReadUnsignedVarInt();
        reader.SkipTaggedFields();

        await Assert.That(timeoutMs).IsEqualTo(60000);
        await Assert.That(topicsLength).IsEqualTo(0);
    }

    [Test]
    public async Task ListPartitionReassignmentsRequest_WithPartitions_EncodesTopics()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new ListPartitionReassignmentsRequest
        {
            TimeoutMs = 123,
            Topics =
            [
                new ListPartitionReassignmentsRequestTopic
                {
                    Name = "test-topic",
                    PartitionIndexes = [0, 2]
                }
            ]
        };

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var timeoutMs = reader.ReadInt32();
        var topicCount = reader.ReadUnsignedVarInt() - 1;
        var topicName = reader.ReadCompactString();
        var partitionCount = reader.ReadUnsignedVarInt() - 1;
        var partition0 = reader.ReadInt32();
        var partition1 = reader.ReadInt32();

        await Assert.That(timeoutMs).IsEqualTo(123);
        await Assert.That(topicCount).IsEqualTo(1);
        await Assert.That(topicName).IsEqualTo("test-topic");
        await Assert.That(partitionCount).IsEqualTo(2);
        await Assert.That(partition0).IsEqualTo(0);
        await Assert.That(partition1).IsEqualTo(2);
    }

    [Test]
    public async Task ListPartitionReassignmentsResponse_CanBeParsed()
    {
        var data = BuildListResponse();
        var reader = new KafkaProtocolReader(data);

        var response = (ListPartitionReassignmentsResponse)ListPartitionReassignmentsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(34);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Topics.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Name).IsEqualTo("test-topic");
        await Assert.That(response.Topics[0].Partitions[0].Replicas).IsEquivalentTo([1, 2]);
        await Assert.That(response.Topics[0].Partitions[0].AddingReplicas).IsEquivalentTo([3]);
        await Assert.That(response.Topics[0].Partitions[0].RemovingReplicas).IsEquivalentTo([1]);
    }

    private static AlterPartitionReassignmentsRequest CreateAlterRequest(
        IReadOnlyList<int>? replicas,
        bool allowReplicationFactorChange) => new()
    {
        TimeoutMs = 60000,
        AllowReplicationFactorChange = allowReplicationFactorChange,
        Topics =
        [
            new AlterPartitionReassignmentsRequestTopic
            {
                Name = "test-topic",
                Partitions =
                [
                    new AlterPartitionReassignmentsRequestPartition
                    {
                        PartitionIndex = 0,
                        Replicas = replicas
                    }
                ]
            }
        ]
    };

    private static byte[] BuildAlterResponse(short version, ErrorCode errorCode)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(12);
        if (version >= 1)
        {
            writer.WriteBoolean(true);
        }

        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactArray(
            new[]
            {
                new AlterPartitionReassignmentsResponseTopic
                {
                    Name = "test-topic",
                    Partitions =
                    [
                        new AlterPartitionReassignmentsResponsePartition
                        {
                            PartitionIndex = 0,
                            ErrorCode = errorCode
                        }
                    ]
                }
            },
            static (ref KafkaProtocolWriter w, AlterPartitionReassignmentsResponseTopic topic, short v) =>
            {
                w.WriteCompactString(topic.Name);
                w.WriteCompactArray(
                    topic.Partitions,
                    static (ref KafkaProtocolWriter pw, AlterPartitionReassignmentsResponsePartition partition, short pv) =>
                    {
                        pw.WriteInt32(partition.PartitionIndex);
                        pw.WriteInt16((short)partition.ErrorCode);
                        pw.WriteCompactNullableString(partition.ErrorMessage);
                        pw.WriteEmptyTaggedFields();
                    },
                    v);
                w.WriteEmptyTaggedFields();
            },
            version);
        writer.WriteEmptyTaggedFields();

        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] BuildListResponse()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(34);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString("test-topic");
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt32(0);
        WriteIntArray(ref writer, [1, 2]);
        WriteIntArray(ref writer, [3]);
        WriteIntArray(ref writer, [1]);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteIntArray(ref KafkaProtocolWriter writer, IReadOnlyList<int> values)
    {
        writer.WriteCompactArray(
            values,
            static (ref KafkaProtocolWriter w, int value) => w.WriteInt32(value));
    }
}
