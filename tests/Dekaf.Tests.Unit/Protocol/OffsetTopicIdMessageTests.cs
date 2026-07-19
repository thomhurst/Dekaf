using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class OffsetTopicIdMessageTests
{
    private static readonly Guid TopicId = new("00112233-4455-6677-8899-aabbccddeeff");

    [Test]
    public async Task OffsetCommitRequest_V10_WritesTopicIdInsteadOfName()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new OffsetCommitRequest
        {
            GroupId = "group-a",
            Topics =
            [
                new OffsetCommitRequestTopic
                {
                    Name = "topic-a",
                    TopicId = TopicId,
                    Partitions =
                    [
                        new OffsetCommitRequestPartition
                        {
                            PartitionIndex = 2,
                            CommittedOffset = 42,
                            CommittedLeaderEpoch = 3
                        }
                    ]
                }
            ]
        };

        request.Write(ref writer, version: 10);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = reader.ReadCompactNonNullableString();
        _ = reader.ReadInt32();
        _ = reader.ReadCompactNonNullableString();
        _ = reader.ReadCompactString();
        _ = reader.ReadUnsignedVarInt();

        await Assert.That(reader.ReadUuid()).IsEqualTo(TopicId);
    }

    [Test]
    public async Task OffsetCommitResponse_V10_ReadsTopicId()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(2);
        writer.WriteUuid(TopicId);
        writer.WriteUnsignedVarInt(1);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (OffsetCommitResponse)OffsetCommitResponse.Read(ref reader, version: 10);

        await Assert.That(response.Topics.Single().TopicId).IsEqualTo(TopicId);
        await Assert.That(response.Topics.Single().Name).IsEmpty();
    }

    [Test]
    public async Task TxnOffsetCommitRequest_V6_WritesTopicIdInsteadOfName()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new TxnOffsetCommitRequest
        {
            TransactionalId = "transaction-a",
            GroupId = "group-a",
            ProducerId = 42,
            ProducerEpoch = 3,
            GenerationIdOrMemberEpoch = 7,
            MemberId = "member-a",
            Topics =
            [
                new TxnOffsetCommitRequestTopic
                {
                    Name = "topic-a",
                    TopicId = TopicId,
                    Partitions =
                    [
                        new TxnOffsetCommitRequestPartition
                        {
                            PartitionIndex = 2,
                            CommittedOffset = 42,
                            CommittedLeaderEpoch = 3,
                            CommittedMetadata = "metadata-a"
                        }
                    ]
                }
            ]
        };

        request.Write(ref writer, version: 6);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = reader.ReadCompactNonNullableString();
        _ = reader.ReadCompactNonNullableString();
        _ = reader.ReadInt64();
        _ = reader.ReadInt16();
        _ = reader.ReadInt32();
        _ = reader.ReadCompactNonNullableString();
        _ = reader.ReadCompactString();
        _ = reader.ReadUnsignedVarInt();

        await Assert.That(reader.ReadUuid()).IsEqualTo(TopicId);
    }

    [Test]
    public async Task TxnOffsetCommitResponse_V6_ReadsTopicId()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(2);
        writer.WriteUuid(TopicId);
        writer.WriteUnsignedVarInt(1);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (TxnOffsetCommitResponse)TxnOffsetCommitResponse.Read(ref reader, version: 6);

        await Assert.That(response.Topics.Single().TopicId).IsEqualTo(TopicId);
        await Assert.That(response.Topics.Single().Name).IsEmpty();
    }

    [Test]
    public async Task OffsetFetchRequest_V10_WritesTopicIdInsteadOfName()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new OffsetFetchRequest
        {
            GroupId = "group-a",
            Groups =
            [
                new OffsetFetchRequestGroup
                {
                    GroupId = "group-a",
                    Topics =
                    [
                        new OffsetFetchRequestTopic
                        {
                            Name = "topic-a",
                            TopicId = TopicId,
                            PartitionIndexes = [2]
                        }
                    ]
                }
            ]
        };

        request.Write(ref writer, version: 10);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = reader.ReadUnsignedVarInt();
        _ = reader.ReadCompactNonNullableString();
        _ = reader.ReadCompactString();
        _ = reader.ReadInt32();
        _ = reader.ReadUnsignedVarInt();

        await Assert.That(reader.ReadUuid()).IsEqualTo(TopicId);
    }

    [Test]
    public async Task OffsetFetchResponse_V10_ReadsTopicId()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString("group-a");
        writer.WriteUnsignedVarInt(2);
        writer.WriteUuid(TopicId);
        writer.WriteUnsignedVarInt(1);
        writer.WriteEmptyTaggedFields();
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (OffsetFetchResponse)OffsetFetchResponse.Read(ref reader, version: 10);
        var topic = response.Groups!.Single().Topics.Single();

        await Assert.That(topic.TopicId).IsEqualTo(TopicId);
        await Assert.That(topic.Name).IsEmpty();
    }

    [Test]
    public async Task OffsetFetchRequest_V10_MultipleGroups_PreservesNullAllTopics()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new OffsetFetchRequest
        {
            GroupId = "unused",
            Groups =
            [
                new OffsetFetchRequestGroup
                {
                    GroupId = "all-topics",
                    Topics = null
                },
                new OffsetFetchRequestGroup
                {
                    GroupId = "selected-topics",
                    Topics =
                    [
                        new OffsetFetchRequestTopic
                        {
                            Name = "topic-a",
                            TopicId = TopicId,
                            PartitionIndexes = [2]
                        }
                    ]
                }
            ]
        };

        request.Write(ref writer, version: 10);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var groupArrayLength = reader.ReadUnsignedVarInt();
        var allTopicsGroupId = reader.ReadCompactNonNullableString();
        _ = reader.ReadCompactString();
        _ = reader.ReadInt32();
        var allTopicsArrayLength = reader.ReadUnsignedVarInt();
        reader.SkipTaggedFields();

        var selectedTopicsGroupId = reader.ReadCompactNonNullableString();
        _ = reader.ReadCompactString();
        _ = reader.ReadInt32();
        var selectedTopicsArrayLength = reader.ReadUnsignedVarInt();
        var selectedTopicId = reader.ReadUuid();

        await Assert.That(groupArrayLength).IsEqualTo(3);
        await Assert.That(allTopicsGroupId).IsEqualTo("all-topics");
        await Assert.That(allTopicsArrayLength).IsEqualTo(0);
        await Assert.That(selectedTopicsGroupId).IsEqualTo("selected-topics");
        await Assert.That(selectedTopicsArrayLength).IsEqualTo(2);
        await Assert.That(selectedTopicId).IsEqualTo(TopicId);
    }
}
