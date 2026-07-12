using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Protocol;

// Independent response encoders keep checked-in reader fixtures reproducible without
// generating protocol bytes from the Read methods under test.
internal static class DeterministicResponseFixtureFactory
{
    private static readonly Guid TopicId = new("00112233-4455-6677-8899-aabbccddeeff");

    internal static IReadOnlyDictionary<string, byte[]> CreateAll() =>
        new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["DescribeConfigsResponse.v4"] = Encode(WriteDescribeConfigsResponse),
            ["DescribeGroupsResponse.v5"] = Encode(WriteDescribeGroupsResponse),
            ["FetchResponse.v16"] = Encode(WriteFetchResponse),
            ["ListOffsetsResponse.v8"] = Encode(WriteListOffsetsResponse),
            ["MetadataResponse.v13"] = Encode(WriteMetadataResponse),
            ["OffsetFetchResponse.v9"] = Encode(WriteOffsetFetchResponse),
            ["ProduceResponse.v12"] = Encode(WriteProduceResponse)
        };

    private static byte[] Encode(WriteFixture writeFixture)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writeFixture(ref writer);
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteMetadataResponse(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(17);

        WriteCompactArrayLength(ref writer, 2);
        WriteBroker(ref writer, 1, "broker-a.example", 9092, "rack-a");
        WriteBroker(ref writer, 2, "broker-b.example", 9093, rack: null);

        writer.WriteCompactString("wire-cluster");
        writer.WriteInt32(1);

        WriteCompactArrayLength(ref writer, 2);
        WriteMetadataTopic(ref writer, "wire-topic", TopicId, ErrorCode.None, firstPartition: 0);
        WriteMetadataTopic(
            ref writer,
            "wire-topic-b",
            new Guid("10213243-5465-7687-98a9-bacbdcedfe0f"),
            ErrorCode.UnknownTopicOrPartition,
            firstPartition: 2);

        writer.WriteInt16((short)ErrorCode.RebootstrapRequired);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteBroker(
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
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteMetadataTopic(
        ref KafkaProtocolWriter writer,
        string name,
        Guid topicId,
        ErrorCode errorCode,
        int firstPartition)
    {
        writer.WriteInt16((short)errorCode);
        writer.WriteCompactString(name);
        writer.WriteUuid(topicId);
        writer.WriteBoolean(false);

        WriteCompactArrayLength(ref writer, 2);
        WriteMetadataPartition(ref writer, firstPartition, ErrorCode.None, leaderId: 1);
        WriteMetadataPartition(ref writer, firstPartition + 1, ErrorCode.NotLeaderOrFollower, leaderId: 2);

        writer.WriteInt32(0x1FFF);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteMetadataPartition(
        ref KafkaProtocolWriter writer,
        int partition,
        ErrorCode errorCode,
        int leaderId)
    {
        writer.WriteInt16((short)errorCode);
        writer.WriteInt32(partition);
        writer.WriteInt32(leaderId);
        writer.WriteInt32(7);
        WriteCompactInt32Array(ref writer, [1, 2, 3]);
        WriteCompactInt32Array(ref writer, [1, 2]);
        WriteCompactInt32Array(ref writer, [3]);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteProduceResponse(ref KafkaProtocolWriter writer)
    {
        WriteCompactArrayLength(ref writer, 2);
        WriteProduceTopic(ref writer, "wire-topic", firstPartition: 0);
        WriteProduceTopic(ref writer, "wire-topic-b", firstPartition: 2);

        writer.WriteInt32(29);
        writer.WriteUnsignedVarInt(2);
        WriteTaggedField(ref writer, tag: 0, WriteNodeEndpoints);
        WriteTaggedBytes(ref writer, tag: 9, [0xAA, 0xBB, 0xCC]);
    }

    private static void WriteProduceTopic(
        ref KafkaProtocolWriter writer,
        string topic,
        int firstPartition)
    {
        writer.WriteCompactString(topic);
        WriteCompactArrayLength(ref writer, 2);
        WriteProducePartition(ref writer, firstPartition, ErrorCode.None, includeRecordError: false);
        WriteProducePartition(
            ref writer,
            firstPartition + 1,
            ErrorCode.NotLeaderOrFollower,
            includeRecordError: true);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteProducePartition(
        ref KafkaProtocolWriter writer,
        int partition,
        ErrorCode errorCode,
        bool includeRecordError)
    {
        writer.WriteInt32(partition);
        writer.WriteInt16((short)errorCode);
        writer.WriteInt64(42 + partition);
        writer.WriteInt64(1_700_000_000_000L + partition);
        writer.WriteInt64(5);

        WriteCompactArrayLength(ref writer, includeRecordError ? 1 : 0);
        if (includeRecordError)
        {
            writer.WriteInt32(3);
            writer.WriteCompactString("record rejected");
            WriteEmptyTaggedFields(ref writer);
        }

        writer.WriteCompactString(includeRecordError ? "leader moved" : null);
        writer.WriteUnsignedVarInt(includeRecordError ? 1 : 0);
        if (includeRecordError)
        {
            WriteTaggedField(ref writer, tag: 0, WriteLeaderIdAndEpoch);
        }
    }

    private static void WriteFetchResponse(ref KafkaProtocolWriter writer)
    {
        var recordBatch = CreateRecordBatchBytes();

        writer.WriteInt32(31);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0x10203040);

        WriteCompactArrayLength(ref writer, 1);
        writer.WriteUuid(TopicId);
        WriteCompactArrayLength(ref writer, 2);
        WriteFetchPartition(ref writer, 0, ErrorCode.None, recordBatch);
        WriteFetchPartition(ref writer, 1, ErrorCode.OffsetOutOfRange, recordBatch: null);
        WriteEmptyTaggedFields(ref writer);

        writer.WriteUnsignedVarInt(2);
        WriteTaggedField(ref writer, tag: 0, WriteNodeEndpoints);
        WriteTaggedBytes(ref writer, tag: 9, [0x10, 0x20]);
    }

    private static void WriteFetchPartition(
        ref KafkaProtocolWriter writer,
        int partition,
        ErrorCode errorCode,
        byte[]? recordBatch)
    {
        writer.WriteInt32(partition);
        writer.WriteInt16((short)errorCode);
        writer.WriteInt64(100 + partition);
        writer.WriteInt64(90 + partition);
        writer.WriteInt64(5);

        WriteCompactArrayLength(ref writer, partition == 0 ? 1 : 0);
        if (partition == 0)
        {
            writer.WriteInt64(77);
            writer.WriteInt64(12);
            WriteEmptyTaggedFields(ref writer);
        }

        writer.WriteInt32(2);
        writer.WriteCompactNullableBytes(recordBatch ?? [], recordBatch is null);

        writer.WriteUnsignedVarInt(partition == 0 ? 4 : 1);
        if (partition == 0)
        {
            WriteTaggedField(ref writer, tag: 0, WriteEpochEndOffset);
            WriteTaggedField(ref writer, tag: 1, WriteLeaderIdAndEpoch);
            WriteTaggedField(ref writer, tag: 2, WriteSnapshotId);
            WriteTaggedBytes(ref writer, tag: 8, [0xDE, 0xCA, 0xFB, 0xAD]);
        }
        else
        {
            WriteTaggedField(ref writer, tag: 1, WriteLeaderIdAndEpoch);
        }
    }

    private static byte[] CreateRecordBatchBytes()
    {
        using var batch = new RecordBatch
        {
            BaseOffset = 42,
            PartitionLeaderEpoch = 7,
            LastOffsetDelta = 1,
            BaseTimestamp = 1_700_000_000_000L,
            MaxTimestamp = 1_700_000_000_001L,
            ProducerId = 1234,
            ProducerEpoch = 2,
            BaseSequence = 9,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "wire-key-0"u8.ToArray(),
                    Value = "wire-value-0"u8.ToArray(),
                    Headers = [new Header("wire-header", "header-value"u8.ToArray())],
                    HeaderCount = 1
                },
                new Record
                {
                    OffsetDelta = 1,
                    TimestampDelta = 1,
                    Key = "wire-key-1"u8.ToArray(),
                    Value = "wire-value-1"u8.ToArray()
                }
            ]
        };

        var buffer = new ArrayBufferWriter<byte>();
        batch.Write(buffer);
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteDescribeConfigsResponse(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(37);
        WriteCompactArrayLength(ref writer, 2);

        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactString(null);
        writer.WriteInt8(2);
        writer.WriteCompactString("wire-topic");
        WriteCompactArrayLength(ref writer, 2);
        WriteConfig(ref writer, "cleanup.policy", "compact", readOnly: false, sensitive: false);
        WriteConfig(ref writer, "ssl.keystore.password", value: null, readOnly: true, sensitive: true);
        WriteEmptyTaggedFields(ref writer);

        writer.WriteInt16((short)ErrorCode.InvalidConfig);
        writer.WriteCompactString("unknown resource");
        writer.WriteInt8(4);
        writer.WriteCompactString("missing-resource");
        WriteCompactArrayLength(ref writer, 0);
        WriteEmptyTaggedFields(ref writer);

        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteConfig(
        ref KafkaProtocolWriter writer,
        string name,
        string? value,
        bool readOnly,
        bool sensitive)
    {
        writer.WriteCompactString(name);
        writer.WriteCompactString(value);
        writer.WriteBoolean(readOnly);
        writer.WriteInt8(1);
        writer.WriteBoolean(sensitive);

        WriteCompactArrayLength(ref writer, 1);
        writer.WriteCompactString($"default.{name}");
        writer.WriteCompactString(value);
        writer.WriteInt8(5);
        WriteEmptyTaggedFields(ref writer);

        writer.WriteInt8(4);
        writer.WriteCompactString($"Documentation for {name}");
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteOffsetFetchResponse(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(41);
        WriteCompactArrayLength(ref writer, 2);
        WriteOffsetFetchGroup(ref writer, "wire-group", ErrorCode.None, committedOffset: 42);
        WriteOffsetFetchGroup(ref writer, "wire-group-b", ErrorCode.NotCoordinator, committedOffset: -1);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteOffsetFetchGroup(
        ref KafkaProtocolWriter writer,
        string groupId,
        ErrorCode groupError,
        long committedOffset)
    {
        writer.WriteCompactString(groupId);
        WriteCompactArrayLength(ref writer, 2);
        WriteOffsetFetchTopic(ref writer, "wire-topic", committedOffset);
        WriteOffsetFetchTopic(ref writer, "wire-topic-b", committedOffset + 10);
        writer.WriteInt16((short)groupError);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteOffsetFetchTopic(
        ref KafkaProtocolWriter writer,
        string topic,
        long committedOffset)
    {
        writer.WriteCompactString(topic);
        WriteCompactArrayLength(ref writer, 2);
        WriteOffsetFetchPartition(ref writer, 0, committedOffset, ErrorCode.None);
        WriteOffsetFetchPartition(ref writer, 1, committedOffset + 1, ErrorCode.OffsetOutOfRange);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteOffsetFetchPartition(
        ref KafkaProtocolWriter writer,
        int partition,
        long committedOffset,
        ErrorCode errorCode)
    {
        writer.WriteInt32(partition);
        writer.WriteInt64(committedOffset);
        writer.WriteInt32(7);
        writer.WriteCompactString($"metadata-{partition}");
        writer.WriteInt16((short)errorCode);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteListOffsetsResponse(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(43);
        WriteCompactArrayLength(ref writer, 2);
        WriteListOffsetsTopic(ref writer, "wire-topic", firstPartition: 0);
        WriteListOffsetsTopic(ref writer, "wire-topic-b", firstPartition: 2);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteListOffsetsTopic(
        ref KafkaProtocolWriter writer,
        string topic,
        int firstPartition)
    {
        writer.WriteCompactString(topic);
        WriteCompactArrayLength(ref writer, 2);
        WriteListOffsetsPartition(ref writer, firstPartition, ErrorCode.None, offset: 42);
        WriteListOffsetsPartition(
            ref writer,
            firstPartition + 1,
            ErrorCode.OffsetNotAvailable,
            offset: 84);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteListOffsetsPartition(
        ref KafkaProtocolWriter writer,
        int partition,
        ErrorCode errorCode,
        long offset)
    {
        writer.WriteInt32(partition);
        writer.WriteInt16((short)errorCode);
        writer.WriteInt64(1_700_000_000_000L + partition);
        writer.WriteInt64(offset);
        writer.WriteInt32(7);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteDescribeGroupsResponse(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(47);
        WriteCompactArrayLength(ref writer, 2);
        WriteDescribeGroup(ref writer, "wire-group", ErrorCode.None, includeMembers: true);
        WriteDescribeGroup(ref writer, "wire-group-b", ErrorCode.GroupIdNotFound, includeMembers: false);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteDescribeGroup(
        ref KafkaProtocolWriter writer,
        string groupId,
        ErrorCode errorCode,
        bool includeMembers)
    {
        writer.WriteInt16((short)errorCode);
        writer.WriteCompactString(groupId);
        writer.WriteCompactString(includeMembers ? "Stable" : "Dead");
        writer.WriteCompactString("consumer");
        writer.WriteCompactString("cooperative-sticky");

        WriteCompactArrayLength(ref writer, includeMembers ? 2 : 0);
        if (includeMembers)
        {
            WriteGroupMember(ref writer, memberId: "member-a", groupInstanceId: "instance-a");
            WriteGroupMember(ref writer, memberId: "member-b", groupInstanceId: null);
        }

        writer.WriteInt32(0x1FFF);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteGroupMember(
        ref KafkaProtocolWriter writer,
        string memberId,
        string? groupInstanceId)
    {
        writer.WriteCompactString(memberId);
        writer.WriteCompactString(groupInstanceId);
        writer.WriteCompactString($"client-{memberId}");
        writer.WriteCompactString("/127.0.0.1");
        writer.WriteCompactNullableBytes([0x01, 0x23, 0x45, 0x67], isNull: false);
        writer.WriteCompactNullableBytes([0x89, 0xAB, 0xCD, 0xEF], isNull: false);
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteNodeEndpoints(ref KafkaProtocolWriter writer)
    {
        WriteCompactArrayLength(ref writer, 2);
        WriteNodeEndpoint(ref writer, 1, "broker-a.example", 9092, "rack-a");
        WriteNodeEndpoint(ref writer, 2, "broker-b.example", 9093, rack: null);
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
        WriteEmptyTaggedFields(ref writer);
    }

    private static void WriteEpochEndOffset(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(7);
        writer.WriteInt64(42);
    }

    private static void WriteLeaderIdAndEpoch(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(2);
        writer.WriteInt32(7);
    }

    private static void WriteSnapshotId(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt64(84);
        writer.WriteInt32(8);
    }

    private static void WriteCompactInt32Array(
        ref KafkaProtocolWriter writer,
        IReadOnlyList<int> values)
    {
        WriteCompactArrayLength(ref writer, values.Count);
        for (var index = 0; index < values.Count; index++)
        {
            writer.WriteInt32(values[index]);
        }
    }

    private static void WriteCompactArrayLength(ref KafkaProtocolWriter writer, int count) =>
        writer.WriteUnsignedVarInt(count + 1);

    private static void WriteEmptyTaggedFields(ref KafkaProtocolWriter writer) =>
        writer.WriteUnsignedVarInt(0);

    private static void WriteTaggedField(
        ref KafkaProtocolWriter writer,
        int tag,
        WriteFixture writeValue)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var valueWriter = new KafkaProtocolWriter(buffer);
        writeValue(ref valueWriter);

        writer.WriteUnsignedVarInt(tag);
        writer.WriteUnsignedVarInt(buffer.WrittenCount);
        writer.WriteRawBytes(buffer.WrittenSpan);
    }

    private static void WriteTaggedBytes(
        ref KafkaProtocolWriter writer,
        int tag,
        ReadOnlySpan<byte> value)
    {
        writer.WriteUnsignedVarInt(tag);
        writer.WriteUnsignedVarInt(value.Length);
        writer.WriteRawBytes(value);
    }

    private delegate void WriteFixture(ref KafkaProtocolWriter writer);
}
