using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class LeaderDiscoveryResponseTests
{
    [Test]
    public async Task ProduceResponse_Read_WithCurrentLeaderTaggedField_ParsesLeaderAndEndpoint()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteUnsignedVarInt(1 + 1); // Responses
        writer.WriteCompactString("topic-a");
        writer.WriteUnsignedVarInt(1 + 1); // PartitionResponses
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.NotLeaderOrFollower);
        writer.WriteInt64(-1);
        writer.WriteInt64(-1);
        writer.WriteInt64(-1);
        writer.WriteUnsignedVarInt(0 + 1); // RecordErrors
        writer.WriteUnsignedVarInt(0);     // ErrorMessage

        var leaderField = new ArrayBufferWriter<byte>();
        var leaderWriter = new KafkaProtocolWriter(leaderField);
        leaderWriter.WriteInt32(2);
        leaderWriter.WriteInt32(9);
        leaderWriter.WriteUnsignedVarInt(0);

        writer.WriteUnsignedVarInt(1);
        WriteTaggedField(ref writer, tag: 0, leaderField);
        writer.WriteUnsignedVarInt(0); // topic tagged fields
        writer.WriteInt32(0);          // throttle time

        var endpointsField = new ArrayBufferWriter<byte>();
        var endpointsWriter = new KafkaProtocolWriter(endpointsField);
        endpointsWriter.WriteUnsignedVarInt(1 + 1);
        WriteNodeEndpoint(ref endpointsWriter, nodeId: 2, host: "broker-2", port: 9094, rack: "rack-b");

        writer.WriteUnsignedVarInt(1);
        WriteTaggedField(ref writer, tag: 0, endpointsField);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ProduceResponse)ProduceResponse.Read(ref reader, version: 11);

        var partition = response.Responses[0].PartitionResponses[0];
        await Assert.That(partition.CurrentLeader).IsNotNull();
        await Assert.That(partition.CurrentLeader!.LeaderId).IsEqualTo(2);
        await Assert.That(partition.CurrentLeader.LeaderEpoch).IsEqualTo(9);
        await Assert.That(response.NodeEndpoints).Count().IsEqualTo(1);
        await Assert.That(response.NodeEndpoints[0].NodeId).IsEqualTo(2);
        await Assert.That(response.NodeEndpoints[0].Host).IsEqualTo("broker-2");
        await Assert.That(response.NodeEndpoints[0].Port).IsEqualTo(9094);
        await Assert.That(response.NodeEndpoints[0].Rack).IsEqualTo("rack-b");

        response.Return();
    }

    [Test]
    public async Task FetchResponse_Read_WithCurrentLeaderTaggedField_ParsesLeaderAndEndpoint()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(1 + 1); // Responses
        writer.WriteUuid(Guid.NewGuid());
        writer.WriteUnsignedVarInt(1 + 1); // Partitions
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.NotLeaderOrFollower);
        writer.WriteInt64(10);
        writer.WriteInt64(9);
        writer.WriteInt64(0);
        writer.WriteUnsignedVarInt(0 + 1); // AbortedTransactions
        writer.WriteInt32(-1);
        writer.WriteUnsignedVarInt(0); // Records

        var leaderField = new ArrayBufferWriter<byte>();
        var leaderWriter = new KafkaProtocolWriter(leaderField);
        leaderWriter.WriteInt32(3);
        leaderWriter.WriteInt32(11);
        leaderWriter.WriteUnsignedVarInt(0);

        writer.WriteUnsignedVarInt(1);
        WriteTaggedField(ref writer, tag: 1, leaderField);
        writer.WriteUnsignedVarInt(0); // topic tagged fields

        var endpointsField = new ArrayBufferWriter<byte>();
        var endpointsWriter = new KafkaProtocolWriter(endpointsField);
        endpointsWriter.WriteUnsignedVarInt(1 + 1);
        WriteNodeEndpoint(ref endpointsWriter, nodeId: 3, host: "broker-3", port: 9095, rack: null);

        writer.WriteUnsignedVarInt(1);
        WriteTaggedField(ref writer, tag: 0, endpointsField);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (FetchResponse)FetchResponse.Read(ref reader, version: 16);

        var partition = response.Responses[0].Partitions[0];
        await Assert.That(partition.CurrentLeader).IsNotNull();
        await Assert.That(partition.CurrentLeader!.LeaderId).IsEqualTo(3);
        await Assert.That(partition.CurrentLeader.LeaderEpoch).IsEqualTo(11);
        await Assert.That(response.NodeEndpoints).Count().IsEqualTo(1);
        await Assert.That(response.NodeEndpoints[0].NodeId).IsEqualTo(3);
        await Assert.That(response.NodeEndpoints[0].Host).IsEqualTo("broker-3");
        await Assert.That(response.NodeEndpoints[0].Port).IsEqualTo(9095);
        await Assert.That(response.NodeEndpoints[0].Rack).IsNull();

        response.ReturnToPool();
    }

    private static void WriteTaggedField(ref KafkaProtocolWriter writer, int tag, ArrayBufferWriter<byte> field)
    {
        writer.WriteUnsignedVarInt(tag);
        writer.WriteUnsignedVarInt(field.WrittenCount);
        writer.WriteRawBytes(field.WrittenSpan);
    }

    private static void WriteNodeEndpoint(
        ref KafkaProtocolWriter writer,
        int nodeId,
        string host,
        int port,
        string? rack)
    {
        writer.WriteInt32(nodeId);
        writer.WriteCompactString(host);
        writer.WriteInt32(port);
        writer.WriteCompactString(rack);
        writer.WriteUnsignedVarInt(0);
    }
}
