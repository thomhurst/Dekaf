using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ElectLeaders request/response encoding and decoding.
/// </summary>
public class ElectLeadersMessageTests
{
    [Test]
    public async Task ElectLeadersRequest_V1_WithElectionType()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ElectLeadersRequest
        {
            ElectionType = 1, // Unclean
            TopicPartitions =
            [
                new ElectLeadersRequestTopic
                {
                    Topic = "test",
                    Partitions = [0]
                }
            ],
            TimeoutMs = 30000
        };
        request.Write(ref writer, version: 1);

        // Read synchronously before await
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var electionType = reader.ReadInt8();

        await Assert.That(electionType).IsEqualTo((sbyte)1);
    }

    [Test]
    public async Task ElectLeadersRequest_V2_Flexible_WithPartitions()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ElectLeadersRequest
        {
            ElectionType = 0, // Preferred
            TopicPartitions =
            [
                new ElectLeadersRequestTopic
                {
                    Topic = "test",
                    Partitions = [0]
                }
            ],
            TimeoutMs = 30000
        };
        request.Write(ref writer, version: 2);

        // Read synchronously before await
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var electionType = reader.ReadInt8();
        var topicsLength = reader.ReadUnsignedVarInt();

        await Assert.That(electionType).IsEqualTo((sbyte)0);
        await Assert.That(topicsLength).IsEqualTo(2); // 1 topic + 1
    }

    [Test]
    public async Task ElectLeadersRequest_V2_NullPartitions()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ElectLeadersRequest
        {
            ElectionType = 0,
            TopicPartitions = null,
            TimeoutMs = 30000
        };
        request.Write(ref writer, version: 2);

        // Read synchronously before await
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        reader.ReadInt8(); // ElectionType
        var topicsLength = reader.ReadUnsignedVarInt();

        await Assert.That(topicsLength).IsEqualTo(0); // null
    }

    [Test]
    public async Task ElectLeadersResponse_V2_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x64 }); // 100ms
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ReplicaElectionResults COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Topic (COMPACT_STRING length+1 = 5)
        data.Add(0x05);
        data.AddRange("test"u8.ToArray());
        // PartitionResult COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // PartitionId (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // Partition tagged fields
        data.Add(0x00);
        // Topic tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ElectLeadersResponse)ElectLeadersResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ReplicaElectionResults.Count).IsEqualTo(1);
        await Assert.That(response.ReplicaElectionResults[0].Topic).IsEqualTo("test");
    }

}
