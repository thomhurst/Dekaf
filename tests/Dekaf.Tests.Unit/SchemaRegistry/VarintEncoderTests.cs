using Dekaf.SchemaRegistry.Protobuf;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class VarintEncoderTests
{
    [Test]
    public async Task WriteVarint_Zero_WritesSingleByte()
    {
        // 0 encodes as [0x00]
        var buffer = new byte[8];
        var written = VarintEncoder.WriteVarint(buffer, 0);

        await Assert.That(written).IsEqualTo(1);
        await Assert.That(buffer[0]).IsEqualTo((byte)0x00);
    }

    [Test]
    public async Task WriteVarint_One_WritesSingleByte()
    {
        // 1 encodes as [0x01]
        var buffer = new byte[8];
        var written = VarintEncoder.WriteVarint(buffer, 1);

        await Assert.That(written).IsEqualTo(1);
        await Assert.That(buffer[0]).IsEqualTo((byte)0x01);
    }

    [Test]
    public async Task WriteVarint_127_WritesSingleByte()
    {
        // 127 (0x7F) is the maximum single-byte varint value
        // encodes as [0x7F]
        var buffer = new byte[8];
        var written = VarintEncoder.WriteVarint(buffer, 127);

        await Assert.That(written).IsEqualTo(1);
        await Assert.That(buffer[0]).IsEqualTo((byte)0x7F);
    }

    [Test]
    public async Task WriteVarint_128_WritesTwoBytes()
    {
        // 128 (0x80) is the first two-byte varint value
        // 128 = 0b10000000 -> low 7 bits = 0x00 with continuation bit = 0x80, then 0x01
        // encodes as [0x80, 0x01]
        var buffer = new byte[8];
        var written = VarintEncoder.WriteVarint(buffer, 128);

        await Assert.That(written).IsEqualTo(2);
        await Assert.That(buffer[0]).IsEqualTo((byte)0x80);
        await Assert.That(buffer[1]).IsEqualTo((byte)0x01);
    }

    [Test]
    public async Task WriteVarint_300_WritesTwoBytes()
    {
        // 300 = 0b100101100 -> low 7 bits = 0b0101100 = 0x2C with continuation = 0xAC, then 0b10 = 0x02
        // encodes as [0xAC, 0x02]
        var buffer = new byte[8];
        var written = VarintEncoder.WriteVarint(buffer, 300);

        await Assert.That(written).IsEqualTo(2);
        await Assert.That(buffer[0]).IsEqualTo((byte)0xAC);
        await Assert.That(buffer[1]).IsEqualTo((byte)0x02);
    }

    [Test]
    public async Task WriteVarint_16384_WritesThreeBytes()
    {
        // 16384 = 0x4000 = 0b100000000000000
        // low 7 bits = 0x00 with continuation = 0x80
        // next 7 bits = 0x00 with continuation = 0x80
        // remaining bits = 0x01
        // encodes as [0x80, 0x80, 0x01]
        var buffer = new byte[8];
        var written = VarintEncoder.WriteVarint(buffer, 16384);

        await Assert.That(written).IsEqualTo(3);
        await Assert.That(buffer[0]).IsEqualTo((byte)0x80);
        await Assert.That(buffer[1]).IsEqualTo((byte)0x80);
        await Assert.That(buffer[2]).IsEqualTo((byte)0x01);
    }

    [Test]
    public async Task CalculateVarintSize_MatchesWriteVarintOutput()
    {
        // Verify that CalculateVarintSize and WriteVarint agree on sizes
        int[] testValues = [0, 1, 127, 128, 300, 16384, 2097151, 2097152];

        foreach (var value in testValues)
        {
            var calculatedSize = VarintEncoder.CalculateVarintSize(value);
            var buffer = new byte[8];
            var writtenSize = VarintEncoder.WriteVarint(buffer, value);

            await Assert.That(writtenSize).IsEqualTo(calculatedSize);
        }
    }

    [Test]
    public void WriteVarint_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new byte[8];
        Assert.Throws<ArgumentOutOfRangeException>(() => VarintEncoder.WriteVarint(buffer, -1));
    }

    [Test]
    public void CalculateVarintSize_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VarintEncoder.CalculateVarintSize(-1));
    }

    [Test]
    public async Task WriteVarintArray_WritesCountThenElements()
    {
        // Array [0, 1] should write: count=2 (varint), then 0 (varint), then 1 (varint)
        // Expected bytes: [0x02, 0x00, 0x01]
        var buffer = new byte[16];
        var written = VarintEncoder.WriteVarintArray(buffer, [0, 1]);

        await Assert.That(written).IsEqualTo(3);
        await Assert.That(buffer[0]).IsEqualTo((byte)0x02); // count = 2
        await Assert.That(buffer[1]).IsEqualTo((byte)0x00); // value = 0
        await Assert.That(buffer[2]).IsEqualTo((byte)0x01); // value = 1
    }

    [Test]
    public async Task CalculateVarintArraySize_MatchesWriteVarintArrayOutput()
    {
        int[] values = [0, 128, 300];
        var calculatedSize = VarintEncoder.CalculateVarintArraySize(values);
        var buffer = new byte[32];
        var writtenSize = VarintEncoder.WriteVarintArray(buffer, values);

        await Assert.That(writtenSize).IsEqualTo(calculatedSize);
    }
}
