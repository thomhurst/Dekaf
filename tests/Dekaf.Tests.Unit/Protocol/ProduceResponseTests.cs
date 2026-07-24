using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public class ProduceResponseTests
{
    [Test]
    public async Task Read_V13_UsesTopicIdInsteadOfName()
    {
        var topicId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(2); // one topic
        writer.WriteUuid(topicId);
        writer.WriteUnsignedVarInt(1); // empty partitions
        writer.WriteUnsignedVarInt(0); // topic tagged fields
        writer.WriteInt32(0);          // throttle time
        writer.WriteUnsignedVarInt(0); // response tagged fields

        ProduceResponse response;
        bool readerEnd;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            response = (ProduceResponse)ProduceResponse.Read(ref reader, 13);
            readerEnd = reader.End;
        }

        try
        {
            await Assert.That(response.Responses[0].TopicId).IsEqualTo(topicId);
            await Assert.That(response.Responses[0].Name).IsEmpty();
            await Assert.That(readerEnd).IsTrue();
        }
        finally
        {
            response.Return();
        }
    }

    [Test]
    public async Task Read_TopicCountExceedingRemainingData_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(int.MaxValue); // claims int.MaxValue - 1 topics
        writer.WriteInt32(0);

        await Assert.That(() => ReadRaw(buffer, 13)).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task Read_PartitionCountExceedingRemainingData_ThrowsMalformedProtocolData()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(2); // one topic
        writer.WriteUuid(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        writer.WriteUnsignedVarInt(int.MaxValue); // claims int.MaxValue - 1 partitions

        await Assert.That(() => ReadRaw(buffer, 13)).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task Read_TopicCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        // 100 claimed topics fit a naive one-byte-per-entry bound against the 200-byte
        // payload, but each v13 topic entry needs at least 18 bytes on the wire.
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(101); // claims 100 topics
        writer.WriteRawBytes(new byte[200]);

        await Assert.That(() => ReadRaw(buffer, 13)).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task Read_PartitionCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        // 50 claimed partitions fit a naive one-byte-per-entry bound against the 100-byte
        // payload, but each partition entry needs at least 33 bytes on the wire.
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(2); // one topic
        writer.WriteUuid(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        writer.WriteUnsignedVarInt(51); // claims 50 partitions
        writer.WriteRawBytes(new byte[100]);

        await Assert.That(() => ReadRaw(buffer, 13)).Throws<MalformedProtocolDataException>();
    }

    private static void ReadRaw(ArrayBufferWriter<byte> buffer, short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ProduceResponse)ProduceResponse.Read(ref reader, version);
        response.Return();
    }

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
