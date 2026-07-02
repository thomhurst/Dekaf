using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class DescribeTopicPartitionsMessageTests
{
    [Test]
    public async Task Request_WithNullCursor_EncodesFlexibleV0()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new DescribeTopicPartitionsRequest
        {
            Topics =
            [
                new DescribeTopicPartitionsRequestTopic { Name = "topic-a" }
            ],
            ResponsePartitionLimit = 500,
            Cursor = null
        };

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var topicsLength = reader.ReadUnsignedVarInt() - 1;
        var topicName = reader.ReadCompactString();
        reader.SkipTaggedFields();
        var responsePartitionLimit = reader.ReadInt32();
        var cursorMarker = reader.ReadInt8();
        reader.SkipTaggedFields();

        await Assert.That(topicsLength).IsEqualTo(1);
        await Assert.That(topicName).IsEqualTo("topic-a");
        await Assert.That(responsePartitionLimit).IsEqualTo(500);
        await Assert.That(cursorMarker).IsEqualTo((sbyte)-1);
    }

    [Test]
    public async Task Request_WithCursor_EncodesNullableStructMarkerAndCursor()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new DescribeTopicPartitionsRequest
        {
            Topics =
            [
                new DescribeTopicPartitionsRequestTopic { Name = "topic-a" },
                new DescribeTopicPartitionsRequestTopic { Name = "topic-b" }
            ],
            ResponsePartitionLimit = 1,
            Cursor = new DescribeTopicPartitionsRequestCursor
            {
                TopicName = "topic-a",
                PartitionIndex = 12
            }
        };

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var topicsLength = reader.ReadUnsignedVarInt() - 1;
        _ = reader.ReadCompactString();
        reader.SkipTaggedFields();
        _ = reader.ReadCompactString();
        reader.SkipTaggedFields();
        var responsePartitionLimit = reader.ReadInt32();
        var cursorMarker = reader.ReadInt8();
        var cursorTopicName = reader.ReadCompactString();
        var cursorPartitionIndex = reader.ReadInt32();
        reader.SkipTaggedFields();
        reader.SkipTaggedFields();

        await Assert.That(topicsLength).IsEqualTo(2);
        await Assert.That(responsePartitionLimit).IsEqualTo(1);
        await Assert.That(cursorMarker).IsEqualTo((sbyte)1);
        await Assert.That(cursorTopicName).IsEqualTo("topic-a");
        await Assert.That(cursorPartitionIndex).IsEqualTo(12);
    }

    [Test]
    public async Task Response_CanParseTopicPartitionsAndNextCursor()
    {
        var topicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(25);
        writer.WriteUnsignedVarInt(2); // topics: 1 element
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactString("topic-a");
        writer.WriteUuid(topicId);
        writer.WriteBoolean(false);
        writer.WriteUnsignedVarInt(2); // partitions: 1 element
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(3); // partition_index
        writer.WriteInt32(1); // leader_id
        writer.WriteInt32(9); // leader_epoch
        writer.WriteCompactArray<int>([1, 2], static (ref KafkaProtocolWriter w, int value) => w.WriteInt32(value));
        writer.WriteCompactArray<int>([1], static (ref KafkaProtocolWriter w, int value) => w.WriteInt32(value));
        writer.WriteCompactNullableArray<int>([2], static (ref KafkaProtocolWriter w, int value) => w.WriteInt32(value));
        writer.WriteCompactNullableArray<int>(null, static (ref KafkaProtocolWriter w, int value) => w.WriteInt32(value));
        writer.WriteCompactArray<int>([], static (ref KafkaProtocolWriter w, int value) => w.WriteInt32(value));
        writer.WriteEmptyTaggedFields(); // partition tags
        writer.WriteInt32(123); // topic_authorized_operations
        writer.WriteEmptyTaggedFields(); // topic tags
        writer.WriteInt8(1); // next_cursor present
        writer.WriteCompactString("topic-a");
        writer.WriteInt32(4);
        writer.WriteEmptyTaggedFields(); // cursor tags
        writer.WriteEmptyTaggedFields(); // response tags

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeTopicPartitionsResponse)DescribeTopicPartitionsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(25);
        await Assert.That(response.Topics.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Name).IsEqualTo("topic-a");
        await Assert.That(response.Topics[0].TopicId).IsEqualTo(topicId);
        await Assert.That(response.Topics[0].TopicAuthorizedOperations).IsEqualTo(123);
        await Assert.That(response.Topics[0].Partitions.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Partitions[0].PartitionIndex).IsEqualTo(3);
        await Assert.That(response.Topics[0].Partitions[0].LeaderEpoch).IsEqualTo(9);
        await Assert.That(response.Topics[0].Partitions[0].ReplicaNodes).IsEquivalentTo([1, 2]);
        await Assert.That(response.Topics[0].Partitions[0].IsrNodes).IsEquivalentTo([1]);
        await Assert.That(response.Topics[0].Partitions[0].EligibleLeaderReplicas).IsEquivalentTo([2]);
        await Assert.That(response.Topics[0].Partitions[0].LastKnownElr).IsNull();
        await Assert.That(response.Topics[0].Partitions[0].OfflineReplicas).IsEmpty();
        await Assert.That(response.NextCursor).IsNotNull();
        await Assert.That(response.NextCursor!.TopicName).IsEqualTo("topic-a");
        await Assert.That(response.NextCursor.PartitionIndex).IsEqualTo(4);
    }

    [Test]
    public async Task Response_NullNextCursor_CanBeParsed()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(1); // empty topics
        writer.WriteInt8(-1); // next_cursor null
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeTopicPartitionsResponse)DescribeTopicPartitionsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Topics).IsEmpty();
        await Assert.That(response.NextCursor).IsNull();
    }
}
