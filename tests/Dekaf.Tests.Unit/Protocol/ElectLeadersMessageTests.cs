using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ElectLeaders request/response encoding and decoding.
/// </summary>
public class ElectLeadersMessageTests
{
    #region ElectLeadersRequest Tests

    [Test]
    public async Task ElectLeadersRequest_V0_WithPartitions()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ElectLeadersRequest
        {
            TopicPartitions =
            [
                new ElectLeadersRequestTopic
                {
                    Topic = "test",
                    Partitions = [0, 1]
                }
            ],
            TimeoutMs = 30000
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // TopicPartitions array length (INT32)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 topic
        // Topic (STRING)
        expected.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        expected.AddRange("test"u8.ToArray());
        // Partitions array (INT32 length)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x02 }); // 2 partitions
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // partition 0
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // partition 1
        // TimeoutMs (INT32)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x75, 0x30 }); // 30000

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task ElectLeadersRequest_V0_NullPartitions_AllPartitions()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ElectLeadersRequest
        {
            TopicPartitions = null,
            TimeoutMs = 30000
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // Null array (INT32 = -1)
        expected.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        // TimeoutMs (INT32)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x75, 0x30 }); // 30000

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

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

    #endregion

    #region ElectLeadersResponse Tests

    [Test]
    public async Task ElectLeadersResponse_V0_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ReplicaElectionResults array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 topic
        // Topic (STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());
        // PartitionResult array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 partition
        // PartitionId (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ElectLeadersResponse)ElectLeadersResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ReplicaElectionResults.Count).IsEqualTo(1);
        await Assert.That(response.ReplicaElectionResults[0].Topic).IsEqualTo("test");
        await Assert.That(response.ReplicaElectionResults[0].PartitionResult.Count).IsEqualTo(1);
        await Assert.That(response.ReplicaElectionResults[0].PartitionResult[0].PartitionId).IsEqualTo(0);
        await Assert.That(response.ReplicaElectionResults[0].PartitionResult[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task ElectLeadersResponse_V1_WithErrorCodeAndMessage()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16) - v1+ top-level error
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ReplicaElectionResults array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 topic
        // Topic (STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());
        // PartitionResult array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 partition
        // PartitionId (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x50 }); // PreferredLeaderNotAvailable = 80
        // ErrorMessage (NULLABLE_STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ElectLeadersResponse)ElectLeadersResponse.Read(ref reader, version: 1);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ReplicaElectionResults[0].PartitionResult[0].ErrorCode)
            .IsEqualTo(ErrorCode.PreferredLeaderNotAvailable);
        await Assert.That(response.ReplicaElectionResults[0].PartitionResult[0].ErrorMessage)
            .IsEqualTo("test");
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

    #endregion

    #region Version Flexibility Tests

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, true)]
    public async Task ElectLeadersRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = ElectLeadersRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)1, (short)0)]
    [Arguments((short)2, (short)2, (short)1)]
    public async Task ElectLeadersRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = ElectLeadersRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = ElectLeadersRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    #endregion
}
