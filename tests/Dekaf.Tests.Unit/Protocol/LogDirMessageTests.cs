using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class LogDirMessageTests
{
    [Test]
    public async Task AlterReplicaLogDirsRequest_V2_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new AlterReplicaLogDirsRequest
        {
            Dirs =
            [
                new AlterReplicaLogDirsRequestDir
                {
                    Path = "/data-1",
                    Topics =
                    [
                        new AlterReplicaLogDirsRequestTopic
                        {
                            Name = "topic-a",
                            Partitions = [0, 1]
                        }
                    ]
                }
            ]
        };

        request.Write(ref writer, version: 2);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var dirCount = reader.ReadUnsignedVarInt() - 1;
        var path = reader.ReadCompactString();
        var topicCount = reader.ReadUnsignedVarInt() - 1;
        var topic = reader.ReadCompactString();
        var partitionCount = reader.ReadUnsignedVarInt() - 1;
        var firstPartition = reader.ReadInt32();
        var secondPartition = reader.ReadInt32();
        reader.SkipTaggedFields();
        reader.SkipTaggedFields();
        reader.SkipTaggedFields();

        await Assert.That(dirCount).IsEqualTo(1);
        await Assert.That(path).IsEqualTo("/data-1");
        await Assert.That(topicCount).IsEqualTo(1);
        await Assert.That(topic).IsEqualTo("topic-a");
        await Assert.That(partitionCount).IsEqualTo(2);
        await Assert.That(firstPartition).IsEqualTo(0);
        await Assert.That(secondPartition).IsEqualTo(1);
    }

    [Test]
    public async Task DescribeLogDirsRequest_V1_UsesLegacyHeadersAndNullArray()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeLogDirsRequest { Topics = null };
        request.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var topicArrayLength = reader.ReadInt32();

        await Assert.That(DescribeLogDirsRequest.GetRequestHeaderVersion(1)).IsEqualTo((short)1);
        await Assert.That(DescribeLogDirsRequest.GetResponseHeaderVersion(1)).IsEqualTo((short)0);
        await Assert.That(DescribeLogDirsRequest.GetRequestHeaderVersion(2)).IsEqualTo((short)2);
        await Assert.That(DescribeLogDirsRequest.GetResponseHeaderVersion(2)).IsEqualTo((short)1);
        await Assert.That(topicArrayLength).IsEqualTo(-1);
    }

    [Test]
    public async Task AlterReplicaLogDirsResponse_V2_Flexible_CanBeParsed()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(25);
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString("topic-a");
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (AlterReplicaLogDirsResponse)AlterReplicaLogDirsResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(25);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].TopicName).IsEqualTo("topic-a");
        await Assert.That(response.Results[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task DescribeLogDirsResponse_V5_Flexible_CanBeParsed()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(100);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactString("/data-1");
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString("topic-a");
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt32(0);
        writer.WriteInt64(1234);
        writer.WriteInt64(7);
        writer.WriteBoolean(true);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
        writer.WriteInt64(10000);
        writer.WriteInt64(9000);
        writer.WriteBoolean(false);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeLogDirsResponse)DescribeLogDirsResponse.Read(ref reader, version: 5);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results[0].LogDir).IsEqualTo("/data-1");
        await Assert.That(response.Results[0].TotalBytes).IsEqualTo(10000);
        await Assert.That(response.Results[0].UsableBytes).IsEqualTo(9000);
        await Assert.That(response.Results[0].IsCordoned).IsFalse();
        await Assert.That(response.Results[0].Topics[0].Partitions[0].PartitionSize).IsEqualTo(1234);
        await Assert.That(response.Results[0].Topics[0].Partitions[0].OffsetLag).IsEqualTo(7);
        await Assert.That(response.Results[0].Topics[0].Partitions[0].IsFutureKey).IsTrue();
    }
}
