using System.Globalization;
using Dekaf.Networking;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// The last allowed varint byte carries fewer than seven payload bits (four for 32-bit, one for
/// 64-bit). Setting any higher bit there must be rejected rather than truncated to a smaller value.
/// Each case runs on contiguous input and on every two-segment split, so the fast, contiguous-slow
/// and segmented-slow decoders are all covered.
/// </summary>
public class VarIntOverflowTests
{
    public enum VarIntKind
    {
        VarInt,
        UnsignedVarInt,
        VarLong
    }

    [Test]
    [Arguments(VarIntKind.VarInt, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 })]
    [Arguments(VarIntKind.VarInt, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x7F })]
    [Arguments(VarIntKind.UnsignedVarInt, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 })]
    [Arguments(VarIntKind.UnsignedVarInt, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x40 })]
    [Arguments(VarIntKind.VarLong, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02 })]
    [Arguments(VarIntKind.VarLong, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F })]
    public async Task OverflowingTerminalByte_ThrowsMalformed(VarIntKind kind, byte[] data)
    {
        foreach (var splitAt in SplitPositions(data))
        {
            await Assert.That(Read(kind, data, splitAt))
                .IsEqualTo("malformed")
                .Because($"split at {splitAt}");
        }
    }

    [Test]
    [Arguments(VarIntKind.VarInt, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, "-2147483648")]
    [Arguments(VarIntKind.VarInt, new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0x0F }, "2147483647")]
    [Arguments(VarIntKind.UnsignedVarInt, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 }, "2147483647")]
    [Arguments(VarIntKind.VarLong, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, "-9223372036854775808")]
    [Arguments(VarIntKind.VarLong, new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, "9223372036854775807")]
    public async Task MaximumValidEncoding_Decodes(VarIntKind kind, byte[] data, string expected)
    {
        foreach (var splitAt in SplitPositions(data))
        {
            await Assert.That(Read(kind, data, splitAt))
                .IsEqualTo(expected)
                .Because($"split at {splitAt}");
        }
    }

    [Test]
    [Arguments(VarIntKind.VarInt, new byte[] { 0x80, 0x80, 0x80, 0x80 })]
    [Arguments(VarIntKind.UnsignedVarInt, new byte[] { 0x80, 0x80, 0x80, 0x80 })]
    [Arguments(VarIntKind.VarLong, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 })]
    public async Task TruncatedBeforeTerminalByte_ThrowsInsufficientData(VarIntKind kind, byte[] data)
    {
        foreach (var splitAt in SplitPositions(data))
        {
            await Assert.That(Read(kind, data, splitAt))
                .IsEqualTo("insufficient")
                .Because($"split at {splitAt}");
        }
    }

    [Test]
    [Arguments(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x08 })]
    [Arguments(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 })]
    [Arguments(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public async Task ConnectionHelper_TerminalByteBeyondInt32_ReturnsFailure(byte[] data)
    {
        var (_, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(data);

        await Assert.That(success).IsFalse();
        await Assert.That(bytesRead).IsEqualTo(5);
    }

    // 0 means contiguous input; any other value splits the input into two segments there.
    private static IEnumerable<int> SplitPositions(byte[] data) => Enumerable.Range(0, data.Length);

    private static string Read(VarIntKind kind, byte[] data, int splitAt)
    {
        var reader = splitAt == 0
            ? new KafkaProtocolReader(data)
            : new KafkaProtocolReader(SequenceTestHelpers.CreateMultiSegmentSequence(data, splitAt));
        try
        {
            var value = kind switch
            {
                VarIntKind.VarInt => reader.ReadVarInt().ToString(CultureInfo.InvariantCulture),
                VarIntKind.UnsignedVarInt => reader.ReadUnsignedVarInt().ToString(CultureInfo.InvariantCulture),
                _ => reader.ReadVarLong().ToString(CultureInfo.InvariantCulture)
            };
            return reader.Remaining == 0 ? value : $"{value} (remaining {reader.Remaining})";
        }
        catch (MalformedProtocolDataException)
        {
            return "malformed";
        }
        catch (InsufficientDataException)
        {
            return "insufficient";
        }
    }
}
