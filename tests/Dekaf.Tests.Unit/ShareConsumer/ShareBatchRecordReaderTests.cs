using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareBatchRecordReaderTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Read_PreservesRawFieldsAndHeaderNullability(bool nullPayloads)
    {
        var data = Encode(new Record
        {
            OffsetDelta = 7,
            TimestampDelta = 13,
            Key = "key"u8.ToArray(),
            Value = "value"u8.ToArray(),
            IsKeyNull = nullPayloads,
            IsValueNull = nullPayloads,
            Headers = [new Header("first", new byte[] { 42 }), new Header("second", (byte[]?)null)],
            HeaderCount = 2
        });
        var parsed = Parse(data);
        await Assert.That(parsed.OffsetDelta).IsEqualTo(7);
        await Assert.That(parsed.TimestampDelta).IsEqualTo(13);
        await Assert.That(parsed.IsKeyNull).IsEqualTo(nullPayloads);
        await Assert.That(parsed.IsValueNull).IsEqualTo(nullPayloads);
        await Assert.That(parsed.Key.Span.SequenceEqual(nullPayloads ? [] : "key"u8)).IsTrue();
        await Assert.That(parsed.Value.Span.SequenceEqual(nullPayloads ? [] : "value"u8)).IsTrue();
        await Assert.That(parsed.HeaderCount).IsEqualTo(2);
        var reader = new KafkaProtocolReader(parsed.HeaderBytes);
        var firstKey = reader.ReadMemorySlice(reader.ReadVarInt());
        var firstValue = reader.ReadMemorySlice(reader.ReadVarInt());
        var secondKey = reader.ReadMemorySlice(reader.ReadVarInt());
        var secondLength = reader.ReadVarInt();
        var consumedAllHeaders = reader.End;
        await Assert.That(firstKey.Span.SequenceEqual("first"u8)).IsTrue();
        await Assert.That(firstValue.Span.SequenceEqual(new byte[] { 42 })).IsTrue();
        await Assert.That(secondKey.Span.SequenceEqual("second"u8)).IsTrue();
        await Assert.That(secondLength).IsEqualTo(-1);
        await Assert.That(consumedAllHeaders).IsTrue();
    }

    [Test]
    public async Task Read_DistinctHeaderKeys_DoNotAllocateOrInternStrings()
    {
        var inputs = new ReadOnlyMemory<byte>[256];
        for (var index = 0; index < inputs.Length; index++)
            inputs[index] = Encode(new Record
            {
                OffsetDelta = index,
                IsKeyNull = true,
                Value = new byte[] { 42 },
                Headers = [new Header($"header-{index}", new byte[] { 1 })],
                HeaderCount = 1
            });
        _ = Parse(inputs[0]);
        var before = GC.GetAllocatedBytesForCurrentThread();
        long checksum = 0;
        for (var index = 0; index < inputs.Length; index++)
            checksum += Parse(inputs[index]).OffsetDelta;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(checksum).IsEqualTo(32640);
        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Read_TruncatedBody_ThrowsInsufficientData()
    {
        var input = Encode(new Record { IsKeyNull = true, Value = "payload"u8.ToArray() });
        await Assert.That(() => Parse(input[..^1])).Throws<InsufficientDataException>();
    }

    private static ShareBatchRecordData Parse(ReadOnlyMemory<byte> data)
    {
        var reader = new KafkaProtocolReader(data);
        var record = ShareBatchRecordReader.Read(ref reader);
        if (!reader.End)
            throw new InvalidOperationException("The record reader left trailing bytes.");
        return record;
    }

    private static ReadOnlyMemory<byte> Encode(Record record)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        record.Write(ref writer);
        return buffer.WrittenMemory;
    }
}
