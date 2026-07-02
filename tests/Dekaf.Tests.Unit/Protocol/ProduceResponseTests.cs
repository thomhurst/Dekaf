using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public class ProduceResponseTests
{
    [Test]
    public async Task Read_InternsRepeatedTopicNames()
    {
        var topicName = "produce-topic-" + Guid.NewGuid().ToString("N");

        var first = ReadProduceResponse(topicName);
        var second = ReadProduceResponse(topicName);

        await Assert.That(second.Responses[0].Name).IsSameReferenceAs(first.Responses[0].Name);

        first.Return();
        second.Return();
    }

    [Test]
    public async Task Read_ReusesTopicNameInternedByFetchResponse()
    {
        var topicName = "shared-topic-" + Guid.NewGuid().ToString("N");

        var fetch = ReadFetchResponse(topicName);
        var produce = ReadProduceResponse(topicName);

        await Assert.That(produce.Responses[0].Name).IsSameReferenceAs(fetch.Responses[0].Topic);

        fetch.ReturnToPool();
        produce.Return();
    }

    [Test]
    public async Task FetchResponseRead_InternsRepeatedTopicNames()
    {
        var topicName = "fetch-topic-" + Guid.NewGuid().ToString("N");

        var first = ReadFetchResponse(topicName);
        var second = ReadFetchResponse(topicName);

        await Assert.That(second.Responses[0].Topic).IsSameReferenceAs(first.Responses[0].Topic);

        first.ReturnToPool();
        second.ReturnToPool();
    }

    private static ProduceResponse ReadProduceResponse(string topicName)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteUnsignedVarInt(2); // one topic
        writer.WriteCompactString(topicName);
        writer.WriteUnsignedVarInt(1); // empty partitions
        writer.WriteUnsignedVarInt(0); // topic tagged fields
        writer.WriteInt32(0);          // throttle time
        writer.WriteUnsignedVarInt(0); // response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        return (ProduceResponse)ProduceResponse.Read(ref reader, ProduceResponse.LowestSupportedVersion);
    }

    private static FetchResponse ReadFetchResponse(string topicName)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);                       // throttle time
        writer.WriteInt16((short)ErrorCode.None);   // error code
        writer.WriteInt32(0);                       // session id
        writer.WriteUnsignedVarInt(2);              // one topic
        writer.WriteCompactString(topicName);
        writer.WriteUnsignedVarInt(1);              // empty partitions
        writer.WriteUnsignedVarInt(0);              // topic tagged fields
        writer.WriteUnsignedVarInt(0);              // response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        return (FetchResponse)FetchResponse.Read(ref reader, 12);
    }
}
