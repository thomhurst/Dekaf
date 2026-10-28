using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class StreamsGroupTopologyDescriptionUpdateMessageTests
{
    [Test]
    public async Task Metadata_SupportsV0()
    {
        await Assert.That(StreamsGroupTopologyDescriptionUpdateRequest.ApiKey)
            .IsEqualTo(ApiKey.StreamsGroupTopologyDescriptionUpdate);
        await Assert.That(StreamsGroupTopologyDescriptionUpdateRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(StreamsGroupTopologyDescriptionUpdateRequest.HighestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(StreamsGroupTopologyDescriptionUpdateResponse.ApiKey)
            .IsEqualTo(ApiKey.StreamsGroupTopologyDescriptionUpdate);
        await Assert.That(StreamsGroupTopologyDescriptionUpdateResponse.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(StreamsGroupTopologyDescriptionUpdateResponse.HighestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task Request_Write_MatchesKafkaV0Layout()
    {
        var request = new StreamsGroupTopologyDescriptionUpdateRequest
        {
            GroupId = "g",
            MemberId = "m",
            TopologyEpoch = 3,
            TopologyDescription = new StreamsTopologyDescriptionData
            {
                Subtopologies =
                [
                    new StreamsTopologyDescriptionSubtopologyData
                    {
                        SubtopologyId = "0",
                        Nodes =
                        [
                            new StreamsTopologyDescriptionNodeData
                            {
                                Name = "s",
                                NodeType = 1,
                                SourceTopics = ["t"],
                                SinkTopic = null,
                                Stores = [],
                                Successors = []
                            }
                        ]
                    }
                ],
                GlobalStores = []
            }
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        request.Write(ref writer, version: 0);

        // GroupId, MemberId, TopologyEpoch, then the TopologyDescription struct inline (not nullable):
        // one subtopology "0" with source node "s" reading "t", no global stores, and tagged fields.
        await Assert.That(Convert.ToHexString(buffer.WrittenSpan)).IsEqualTo(
            "0267" + "026D" + "00000003" +
            "02" + "0230" + "02" + "0273" + "01" + "020274" + "00" + "01" + "01" + "00" + "00" +
            "01" + "00" +
            "00");
    }

    [Test]
    public async Task Response_Read_DecodesErrorAndThrottle()
    {
        var bytes = Convert.FromHexString("00000005" + "0087" + "0278" + "00");

        var response = Read(bytes, out var consumed);

        await Assert.That(consumed).IsEqualTo((long)bytes.Length);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(5);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.StreamsTopologyDescriptionUpdateFailed);
        await Assert.That(response.ErrorMessage).IsEqualTo("x");
    }

    [Test]
    public async Task Response_Read_NullErrorMessage()
    {
        var bytes = Convert.FromHexString("00000000" + "0000" + "00" + "00");

        var response = Read(bytes, out _);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsNull();
    }

    private static StreamsGroupTopologyDescriptionUpdateResponse Read(byte[] bytes, out long consumed)
    {
        var reader = new KafkaProtocolReader(bytes);
        var response = (StreamsGroupTopologyDescriptionUpdateResponse)
            StreamsGroupTopologyDescriptionUpdateResponse.Read(ref reader, version: 0);
        consumed = reader.Consumed;
        return response;
    }
}
