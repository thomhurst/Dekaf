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

    [Test]
    public async Task Read_TopicCountExceedingAbsoluteCap_ThrowsMalformedProtocolData()
    {
        // 20,000 claimed topics satisfy the pre-v13 3-byte wire minimum against the
        // 61,000-byte payload, but the in-memory topic struct is an order of magnitude
        // larger than its minimum encoding, so counts above the absolute cap must be
        // rejected before the array allocation.
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(20_001); // claims 20,000 topics
        writer.WriteRawBytes(new byte[61_000]);

        await Assert.That(() => ReadRaw(buffer, 9)).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task Read_RecordErrorCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        // 40 claimed record errors fit a naive one-byte-per-entry bound against the
        // 60-byte payload, but each record error needs at least 6 bytes on the wire.
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(2); // one topic
        writer.WriteUuid(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        writer.WriteUnsignedVarInt(2); // one partition
        writer.WriteInt32(0);          // partition index
        writer.WriteInt16(0);          // error code
        writer.WriteInt64(0);          // base offset
        writer.WriteInt64(-1);         // log append time
        writer.WriteInt64(-1);         // log start offset
        writer.WriteUnsignedVarInt(41); // claims 40 record errors
        writer.WriteRawBytes(new byte[60]);

        await Assert.That(() => ReadRaw(buffer, 13)).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task Read_NodeEndpointCountExceedingMinimumEncodedSize_ThrowsMalformedProtocolData()
    {
        // 30 claimed node endpoints fit a naive one-byte-per-entry bound against the
        // 41-byte tagged-field payload, but each endpoint needs at least 11 bytes on the wire.
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(1);  // zero topics
        writer.WriteInt32(0);           // throttle time
        writer.WriteUnsignedVarInt(1);  // one response tagged field
        writer.WriteUnsignedVarInt(0);  // tag 0 = node endpoints
        writer.WriteUnsignedVarInt(41); // tagged-field size
        writer.WriteUnsignedVarInt(31); // claims 30 endpoints
        writer.WriteRawBytes(new byte[40]);

        await Assert.That(() => ReadRaw(buffer, 13)).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task Read_AfterHostileParseFailure_PooledInstanceRemainsUsable()
    {
        // A hostile frame throws mid-parse after the response was rented; the instance
        // is returned to the pool and a subsequent valid parse must not observe any
        // stale partially-parsed state.
        var hostile = new ArrayBufferWriter<byte>();
        var hostileWriter = new KafkaProtocolWriter(hostile);
        hostileWriter.WriteUnsignedVarInt(2); // one topic
        hostileWriter.WriteUuid(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        hostileWriter.WriteUnsignedVarInt(int.MaxValue); // claims int.MaxValue - 1 partitions

        await Assert.That(() => ReadRaw(hostile, 13)).Throws<MalformedProtocolDataException>();

        var valid = new ArrayBufferWriter<byte>();
        var validWriter = new KafkaProtocolWriter(valid);
        var topicId = Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
        validWriter.WriteUnsignedVarInt(2); // one topic
        validWriter.WriteUuid(topicId);
        validWriter.WriteUnsignedVarInt(1); // empty partitions
        validWriter.WriteUnsignedVarInt(0); // topic tagged fields
        validWriter.WriteInt32(7);          // throttle time
        validWriter.WriteUnsignedVarInt(0); // response tagged fields

        var reader = new KafkaProtocolReader(valid.WrittenMemory);
        var response = (ProduceResponse)ProduceResponse.Read(ref reader, 13);

        try
        {
            await Assert.That(response.TopicCount).IsEqualTo(1);
            await Assert.That(response.Responses[0].TopicId).IsEqualTo(topicId);
            await Assert.That(response.Responses[0].PartitionCount).IsEqualTo(0);
            await Assert.That(response.ThrottleTimeMs).IsEqualTo(7);
        }
        finally
        {
            response.Return();
        }
    }

    [Test]
    public async Task Return_OversizedTopicArray_IsDroppedInsteadOfPooled()
    {
        var response = new ProduceResponse
        {
            Responses = new ProduceResponseTopicData[2000],
            ThrottleTimeMs = 9
        };

        response.Return();

        // Pooled instances go through Reset (which clears ThrottleTimeMs); a dropped
        // instance is left untouched.
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(9);
    }

    [Test]
    public async Task Return_OversizedPartitionArray_IsTrimmedBeforePooling()
    {
        var response = new ProduceResponse
        {
            Responses = new ProduceResponseTopicData[1],
            TopicCount = 1,
            ThrottleTimeMs = 9
        };
        response.Responses[0].PartitionResponses = new ProduceResponsePartitionData[10_000];

        response.Return();

        await Assert.That(response.Responses[0].PartitionResponses).IsNull();
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
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
