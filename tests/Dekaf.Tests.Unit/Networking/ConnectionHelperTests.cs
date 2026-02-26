using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for <see cref="ConnectionHelper.ReadUnsignedVarInt"/> which decodes
/// Kafka's unsigned variable-length integer encoding used for tagged field parsing.
/// </summary>
public class ConnectionHelperTests
{
    #region Empty and Truncated Input Tests

    [Test]
    public async Task ReadUnsignedVarInt_EmptySpan_ReturnsFailure()
    {
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(ReadOnlySpan<byte>.Empty);

        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task ReadUnsignedVarInt_TruncatedMultiByte_ReturnsFailure()
    {
        // 0x80 has continuation bit set, indicating more bytes follow, but span ends
        ReadOnlySpan<byte> truncated = [0x80];
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(truncated);

        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task ReadUnsignedVarInt_TruncatedThreeByte_ReturnsFailure()
    {
        // First two bytes have continuation bits set, but third byte is missing
        // This would encode a value >= 16384 which needs 3 bytes
        ReadOnlySpan<byte> truncated = [0x80, 0x80];
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(truncated);

        await Assert.That(success).IsFalse();
    }

    #endregion

    #region Malformed Over-Length VarInt Tests

    [Test]
    public async Task ReadUnsignedVarInt_OverLengthVarInt_ReturnsFailure()
    {
        // 6 bytes all with continuation bit set - exceeds 5-byte limit for 32-bit int
        // (5 bytes * 7 bits = 35 bits which is the max for a 32-bit varint)
        ReadOnlySpan<byte> overLength = [0x80, 0x80, 0x80, 0x80, 0x80, 0x00];
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(overLength);

        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task ReadUnsignedVarInt_FiveContinuationBytes_ReturnsFailure()
    {
        // 5 bytes with continuation bits, then a terminating byte = 6 bytes total
        // shift would reach 35, exceeding the limit
        ReadOnlySpan<byte> malformed = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01];
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(malformed);

        await Assert.That(success).IsFalse();
    }

    #endregion

    #region Single-Byte Value Tests

    [Test]
    [Arguments(0, new byte[] { 0x00 })]
    [Arguments(1, new byte[] { 0x01 })]
    [Arguments(127, new byte[] { 0x7F })]
    public async Task ReadUnsignedVarInt_SingleByteValues_DecodesCorrectly(int expectedValue, byte[] input)
    {
        var (value, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(expectedValue);
        await Assert.That(bytesRead).IsEqualTo(1);
    }

    #endregion

    #region Multi-Byte Value Tests

    [Test]
    [Arguments(128, new byte[] { 0x80, 0x01 })]
    [Arguments(300, new byte[] { 0xAC, 0x02 })]
    [Arguments(16384, new byte[] { 0x80, 0x80, 0x01 })]
    public async Task ReadUnsignedVarInt_MultiByteValues_DecodesCorrectly(int expectedValue, byte[] input)
    {
        var (value, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(expectedValue);
        await Assert.That(bytesRead).IsEqualTo(input.Length);
    }

    #endregion

    #region Bytes Read Count Tests

    [Test]
    public async Task ReadUnsignedVarInt_SingleByte_ReportsOneBytesRead()
    {
        ReadOnlySpan<byte> input = [0x00];
        var (_, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(bytesRead).IsEqualTo(1);
    }

    [Test]
    public async Task ReadUnsignedVarInt_TwoBytes_ReportsTwoBytesRead()
    {
        // 128 = 0x80 0x01
        ReadOnlySpan<byte> input = [0x80, 0x01];
        var (_, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(bytesRead).IsEqualTo(2);
    }

    [Test]
    public async Task ReadUnsignedVarInt_WithTrailingBytes_OnlyConsumesVarInt()
    {
        // Value 1 (0x01) followed by trailing data that should not be consumed
        ReadOnlySpan<byte> input = [0x01, 0xFF, 0xFF];
        var (value, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(1);
        await Assert.That(bytesRead).IsEqualTo(1);
    }

    #endregion

    #region Five-Byte Maximum Value Tests

    [Test]
    public async Task ReadUnsignedVarInt_MaxFiveByteValue_DecodesCorrectly()
    {
        // int.MaxValue (2147483647) encoded as unsigned varint = 5 bytes
        // 2147483647 = 0x7FFFFFFF
        // Varint encoding: FF FF FF FF 07
        ReadOnlySpan<byte> input = [0xFF, 0xFF, 0xFF, 0xFF, 0x07];
        var (value, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(int.MaxValue);
        await Assert.That(bytesRead).IsEqualTo(5);
    }

    #endregion
}
