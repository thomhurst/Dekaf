using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

public class KafkaProtocolReaderTests
{
    [Test]
    public async Task ReadInt16_ReadsBigEndian()
    {
        var data = new byte[] { 0x01, 0x02 };
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadInt16();

        await Assert.That(result).IsEqualTo((short)0x0102);
    }

    [Test]
    public async Task ReadInt32_ReadsBigEndian()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadInt32();

        await Assert.That(result).IsEqualTo(0x01020304);
    }

    [Test]
    public async Task ReadInt64_ReadsBigEndian()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadInt64();

        await Assert.That(result).IsEqualTo(0x0102030405060708);
    }

    [Test]
    public async Task ReadVarInt_ReadsZero()
    {
        var data = new byte[] { 0x00 };
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadVarInt();

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task ReadVarInt_ReadsPositiveNumber()
    {
        var data = new byte[] { 0x02 }; // ZigZag encoded 1
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadVarInt();

        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task ReadVarInt_ReadsNegativeNumber()
    {
        var data = new byte[] { 0x01 }; // ZigZag encoded -1
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadVarInt();

        await Assert.That(result).IsEqualTo(-1);
    }

    [Test]
    public async Task ReadVarInt_ReadsLargeNumber()
    {
        var data = new byte[] { 0x80, 0x01 }; // ZigZag encoded 64
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadVarInt();

        await Assert.That(result).IsEqualTo(64);
    }

    [Test]
    public async Task ReadString_ReadsNullString()
    {
        var data = new byte[] { 0xFF, 0xFF }; // -1 length
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadString();

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ReadString_ReadsEmptyString()
    {
        var data = new byte[] { 0x00, 0x00 }; // 0 length
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadString();

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ReadString_ReadsUtf8String()
    {
        var data = new byte[] { 0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t' };
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadString();

        await Assert.That(result).IsEqualTo("test");
    }

    [Test]
    public async Task ReadCompactString_ReadsUtf8String()
    {
        var data = new byte[] { 0x05, (byte)'t', (byte)'e', (byte)'s', (byte)'t' }; // length + 1 = 5
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadCompactString();

        await Assert.That(result).IsEqualTo("test");
    }

    [Test]
    public async Task ReadBytes_ClaimedLengthExceedsRemaining_ThrowsMalformedProtocolDataException()
    {
        var data = new byte[] { 0x7F, 0xFF, 0xFF, 0xFF };

        var exception = ReadThrowsMalformed(data, static (ref KafkaProtocolReader reader) => reader.ReadBytes());

        await Assert.That(exception.Message).Contains("claimed length");
    }

    [Test]
    public async Task ReadBytes_MultiSegmentClaimedLengthExceedsRemaining_ThrowsMalformedProtocolDataException()
    {
        var data = new byte[] { 0x00, 0x00, 0x00, 0x05, 0x01, 0x02 };
        var sequence = SequenceTestHelpers.CreateMultiSegmentSequence(data, splitAt: 2);

        var exception = ReadThrowsMalformed(sequence, static (ref KafkaProtocolReader reader) => reader.ReadBytes());

        await Assert.That(exception.Message).Contains("claimed length");
    }

    [Test]
    public async Task ReadRawBytes_ClaimedLengthExceedsRemaining_ThrowsMalformedProtocolDataException()
    {
        var data = Array.Empty<byte>();

        var exception = ReadThrowsMalformed(data, static (ref KafkaProtocolReader reader) => reader.ReadRawBytes(int.MaxValue));

        await Assert.That(exception.Message).Contains("claimed length");
    }

    [Test]
    public async Task ReadMemorySlice_MultiSegmentClaimedLengthExceedsRemaining_ThrowsMalformedProtocolDataException()
    {
        var sequence = SequenceTestHelpers.CreateMultiSegmentSequence(new byte[] { 0x01, 0x02 }, splitAt: 1);

        var exception = ReadThrowsMalformed(sequence, static (ref KafkaProtocolReader reader) => reader.ReadMemorySlice(3));

        await Assert.That(exception.Message).Contains("claimed length");
    }

    [Test]
    public async Task ReadCompactArray_ClaimedLengthExceedsRemaining_ThrowsMalformedProtocolDataException()
    {
        var data = new byte[] { 0x04 };

        var exception = ReadThrowsMalformed(
            data,
            static (ref KafkaProtocolReader reader) => reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadInt8()));

        await Assert.That(exception.Message).Contains("claimed length");
    }

    [Test]
    public async Task ReadString_ClaimedLengthExceedsRemaining_ThrowsMalformedProtocolDataException()
    {
        var data = new byte[] { 0x00, 0x05, (byte)'a', (byte)'b' };

        var exception = ReadThrowsMalformed(data, static (ref KafkaProtocolReader reader) => reader.ReadString());

        await Assert.That(exception.Message).Contains("claimed length");
    }

    [Test]
    public async Task ReadBoolean_ReadsTrue()
    {
        var data = new byte[] { 0x01 };
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadBoolean();

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ReadBoolean_ReadsFalse()
    {
        var data = new byte[] { 0x00 };
        var reader = new KafkaProtocolReader(data);

        var result = reader.ReadBoolean();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Consumed_TracksConsumedBytes()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
        var reader = new KafkaProtocolReader(data);

        _ = reader.ReadInt32();

        await Assert.That(reader.Consumed).IsEqualTo(4);
    }

    [Test]
    public async Task Remaining_TracksRemainingBytes()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
        var reader = new KafkaProtocolReader(data);

        _ = reader.ReadInt32();

        await Assert.That(reader.Remaining).IsEqualTo(2);
    }

    private static MalformedProtocolDataException ReadThrowsMalformed(
        byte[] data,
        ReadAction read)
    {
        try
        {
            var reader = new KafkaProtocolReader(data);
            read(ref reader);
        }
        catch (MalformedProtocolDataException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected MalformedProtocolDataException");
    }

    private static MalformedProtocolDataException ReadThrowsMalformed(
        ReadOnlySequence<byte> data,
        ReadAction read)
    {
        try
        {
            var reader = new KafkaProtocolReader(data);
            read(ref reader);
        }
        catch (MalformedProtocolDataException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected MalformedProtocolDataException");
    }

    private delegate void ReadAction(ref KafkaProtocolReader reader);
}
