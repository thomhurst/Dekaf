using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class TransactionResponseArrayBoundsTests
{
    private const int HostileElementCount = 40;
    private const int HostilePayloadLength = 60;

    [Test]
    public async Task DeleteRecordsResponse_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDeleteRecordsResponse(buffer));
    }

    [Test]
    public async Task DeleteRecordsResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDeleteRecordsResponseTopic(buffer));
    }

    [Test]
    public async Task FetchResponse_Read_SegmentedNodeEndpointCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(1);
        writer.WriteUnsignedVarInt(1);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(HostilePayloadLength + 1);
        WriteHostileCompactArray(ref writer);
        var sequence = SequenceTestHelpers.CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), splitAt: 11);

        await AssertMinimumSizeRejected(() => ReadFetchResponse(sequence));
    }

    [Test]
    public async Task ListOffsetsResponse_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadListOffsetsResponse(buffer));
    }

    [Test]
    [Arguments((short)0)]
    [Arguments((short)6)]
    public async Task ListOffsetsResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation(
        short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadListOffsetsResponseTopic(buffer, version));
    }

    [Test]
    public async Task ListOffsetsResponsePartition_Read_OldStyleOffsetCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        WriteHostileLegacyArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadListOffsetsResponsePartition(buffer));
    }

    [Test]
    public async Task DescribeProducersResponse_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDescribeProducersResponse(buffer));
    }

    [Test]
    public async Task DescribeProducersResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDescribeProducersResponseTopic(buffer));
    }

    [Test]
    public async Task DescribeProducersResponsePartition_Read_ProducerCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDescribeProducersResponsePartition(buffer));
    }

    [Test]
    [Arguments((short)0)]
    [Arguments((short)1)]
    public async Task DescribeTransactionsResponse_Read_StateCountExceedingMinimumEncodedSize_RejectsBeforeAllocation(
        short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDescribeTransactionsResponse(buffer, version));
    }

    [Test]
    public async Task DescribeTransactionsResponseState_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt32(0);
        writer.WriteInt64(0);
        writer.WriteInt64(0);
        writer.WriteInt64(0);
        writer.WriteInt16(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDescribeTransactionsResponseState(buffer));
    }

    [Test]
    public async Task DescribeTransactionsResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadDescribeTransactionsResponseTopic(buffer));
    }

    [Test]
    public async Task ListTransactionsResponse_Read_UnknownFilterCountExceedingAbsoluteCap_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        WriteCompactArrayAtAbsoluteCap(
            ref writer,
            maxCount: ListTransactionsResponse.MaxUnknownStateFilterCount);

        await AssertMaximumRejected(
            () => ReadListTransactionsResponse(buffer),
            count: ListTransactionsResponse.MaxUnknownStateFilterCount + 1,
            maxCount: ListTransactionsResponse.MaxUnknownStateFilterCount);
    }

    [Test]
    public async Task ListTransactionsResponse_Read_StateCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(1);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadListTransactionsResponse(buffer));
    }

    [Test]
    [Arguments((short)3)]
    [Arguments((short)6)]
    public async Task TxnOffsetCommitResponse_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation(
        short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadTxnOffsetCommitResponse(buffer, version));
    }

    [Test]
    public async Task TxnOffsetCommitResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(Guid.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadTxnOffsetCommitResponseTopic(buffer));
    }

    [Test]
    public async Task WriteTxnMarkersResponse_Read_MarkerCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadWriteTxnMarkersResponse(buffer));
    }

    [Test]
    public async Task WriteTxnMarkersResponseMarker_Read_TopicCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt64(0);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadWriteTxnMarkersResponseMarker(buffer));
    }

    [Test]
    public async Task WriteTxnMarkersResponseTopic_Read_PartitionCountExceedingMinimumEncodedSize_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);
        WriteHostileCompactArray(ref writer);

        await AssertMinimumSizeRejected(() => ReadWriteTxnMarkersResponseTopic(buffer));
    }

    private static void WriteHostileCompactArray(ref KafkaProtocolWriter writer)
    {
        writer.WriteUnsignedVarInt(HostileElementCount + 1);
        writer.WriteRawBytes(new byte[HostilePayloadLength]);
    }

    private static void WriteHostileLegacyArray(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(HostileElementCount);
        writer.WriteRawBytes(new byte[HostilePayloadLength]);
    }

    private static void WriteCompactArrayAtAbsoluteCap(ref KafkaProtocolWriter writer, int maxCount)
    {
        writer.WriteUnsignedVarInt(maxCount + 2);
        writer.WriteRawBytes(new byte[maxCount + 1]);
    }

    private static async Task AssertMinimumSizeRejected(Action read)
    {
        var exception = Assert.Throws<MalformedProtocolDataException>(read);
        await Assert.That(exception.Message)
            .IsEqualTo(
                $"Invalid protocol data: claimed length {HostileElementCount} exceeds remaining data {HostilePayloadLength}");
    }

    private static async Task AssertMaximumRejected(Action read, int count, int maxCount)
    {
        var exception = Assert.Throws<MalformedProtocolDataException>(read);
        await Assert.That(exception.Message)
            .IsEqualTo($"Invalid protocol data: claimed element count {count} exceeds maximum {maxCount}");
    }

    private static void ReadDeleteRecordsResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DeleteRecordsResponse.Read(ref reader, version: 2);
    }

    private static void ReadDeleteRecordsResponseTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DeleteRecordsResponseTopic.Read(ref reader, version: 2);
    }

    private static void ReadFetchResponse(ReadOnlySequence<byte> sequence)
    {
        var reader = new KafkaProtocolReader(sequence);
        _ = FetchResponse.Read(ref reader, version: 16);
    }

    private static void ReadListOffsetsResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ListOffsetsResponse.Read(ref reader, version: 6);
    }

    private static void ReadListOffsetsResponseTopic(ArrayBufferWriter<byte> buffer, short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ListOffsetsResponseTopic.Read(ref reader, version);
    }

    private static void ReadListOffsetsResponsePartition(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ListOffsetsResponsePartition.Read(ref reader, version: 0);
    }

    private static void ReadDescribeProducersResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeProducersResponse.Read(ref reader, version: 0);
    }

    private static void ReadDescribeProducersResponseTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeProducersResponseTopic.Read(ref reader);
    }

    private static void ReadDescribeProducersResponsePartition(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeProducersResponsePartition.Read(ref reader);
    }

    private static void ReadDescribeTransactionsResponse(
        ArrayBufferWriter<byte> buffer,
        short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeTransactionsResponse.Read(ref reader, version);
    }

    private static void ReadDescribeTransactionsResponseState(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeTransactionsResponseState.Read(ref reader, version: 1);
    }

    private static void ReadDescribeTransactionsResponseTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = DescribeTransactionsResponseTopic.Read(ref reader);
    }

    private static void ReadListTransactionsResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = ListTransactionsResponse.Read(ref reader, version: 2);
    }

    private static void ReadTxnOffsetCommitResponse(ArrayBufferWriter<byte> buffer, short version)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = TxnOffsetCommitResponse.Read(ref reader, version);
    }

    private static void ReadTxnOffsetCommitResponseTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = TxnOffsetCommitResponseTopic.Read(ref reader, version: 6);
    }

    private static void ReadWriteTxnMarkersResponse(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = WriteTxnMarkersResponse.Read(ref reader, version: 2);
    }

    private static void ReadWriteTxnMarkersResponseMarker(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = WriteTxnMarkersResponseMarker.Read(ref reader, version: 2);
    }

    private static void ReadWriteTxnMarkersResponseTopic(ArrayBufferWriter<byte> buffer)
    {
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = WriteTxnMarkersResponseTopic.Read(ref reader, version: 2);
    }
}
