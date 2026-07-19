using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class DescribeClusterMessageTests
{
    [Test]
    public async Task Request_V1_EncodesControllerEndpointType()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new DescribeClusterRequest
        {
            IncludeClusterAuthorizedOperations = true,
            EndpointType = DescribeClusterEndpointType.Controller
        };

        request.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var includeAuthorizedOperations = reader.ReadBoolean();
        var endpointType = reader.ReadInt8();
        reader.SkipTaggedFields();
        var remaining = reader.Remaining;

        await Assert.That(includeAuthorizedOperations).IsTrue();
        await Assert.That(endpointType).IsEqualTo((sbyte)DescribeClusterEndpointType.Controller);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task Response_V1_ParsesControllerTopology()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(12);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteInt8((sbyte)DescribeClusterEndpointType.Controller);
        writer.WriteCompactString("cluster-a");
        writer.WriteInt32(2);
        writer.WriteCompactArray<int>([1, 2], static (ref KafkaProtocolWriter w, int nodeId) =>
        {
            w.WriteInt32(nodeId);
            w.WriteCompactString($"controller-{nodeId}");
            w.WriteInt32(9092 + nodeId);
            w.WriteCompactNullableString(nodeId == 1 ? "rack-a" : null);
            w.WriteEmptyTaggedFields();
        });
        writer.WriteInt32(7);
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeClusterResponse)DescribeClusterResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(12);
        await Assert.That(response.EndpointType).IsEqualTo(DescribeClusterEndpointType.Controller);
        await Assert.That(response.ClusterId).IsEqualTo("cluster-a");
        await Assert.That(response.ControllerId).IsEqualTo(2);
        await Assert.That(response.Nodes.Count).IsEqualTo(2);
        await Assert.That(response.Nodes[0].Host).IsEqualTo("controller-1");
        await Assert.That(response.Nodes[0].Rack).IsEqualTo("rack-a");
        await Assert.That(response.Nodes[1].Port).IsEqualTo(9094);
        await Assert.That(response.ClusterAuthorizedOperations).IsEqualTo(7);
    }
}
