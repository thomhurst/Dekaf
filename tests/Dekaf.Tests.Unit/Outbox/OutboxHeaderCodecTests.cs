using Dekaf.Outbox;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Outbox;

public class OutboxHeaderCodecTests
{
    [Test]
    public async Task Encode_NullHeaders_ReturnsNull()
    {
        await Assert.That(OutboxHeaderCodec.Encode(null)).IsNull();
    }

    [Test]
    public async Task Encode_EmptyHeaders_ReturnsNull()
    {
        await Assert.That(OutboxHeaderCodec.Encode(new Headers())).IsNull();
    }

    [Test]
    public async Task Decode_NullBlob_ReturnsNull()
    {
        await Assert.That(OutboxHeaderCodec.Decode(null)).IsNull();
    }

    [Test]
    public async Task Encode_StampsFormatVersion()
    {
        var blob = OutboxHeaderCodec.Encode(new Headers().Add("a", "b"))!;

        await Assert.That(blob[0]).IsEqualTo((byte)1);
    }

    [Test]
    public async Task Decode_UnknownVersion_Throws()
    {
        var blob = OutboxHeaderCodec.Encode(new Headers().Add("a", "b"))!;
        blob[0] = 99;

        await Assert.That(() => OutboxHeaderCodec.Decode(blob)).Throws<FormatException>();
    }

    [Test]
    public async Task Decode_CountExceedingRemainingBytes_ThrowsInsteadOfAllocating()
    {
        // A corrupt 5-byte blob claiming int.MaxValue headers must be rejected as
        // malformed, not turned into an enormous allocation.
        var blob = new byte[5];
        blob[0] = 1;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(1), int.MaxValue);

        await Assert.That(() => OutboxHeaderCodec.Decode(blob)).Throws<FormatException>();
    }

    [Test]
    public async Task RoundTrip_PreservesKeysValuesAndOrder()
    {
        var headers = new Headers()
            .Add("first", "value-1")
            .Add("second", new byte[] { 1, 2, 3, 255 })
            .Add("null-valued", (byte[]?)null)
            .Add("empty", Array.Empty<byte>())
            .Add("unicode-ключ", "значение");

        var decoded = OutboxHeaderCodec.Decode(OutboxHeaderCodec.Encode(headers))!;

        await Assert.That(decoded.Count).IsEqualTo(headers.Count);
        for (var i = 0; i < headers.Count; i++)
        {
            await Assert.That(decoded[i].Key).IsEqualTo(headers[i].Key);
            await Assert.That(decoded[i].IsValueNull).IsEqualTo(headers[i].IsValueNull);
            await Assert.That(decoded[i].Value.ToArray()).IsEquivalentTo(headers[i].Value.ToArray());
        }
    }

    [Test]
    public async Task RoundTrip_DuplicateKeys_PreservesBoth()
    {
        var headers = new Headers()
            .Add("dup", "one")
            .Add("dup", "two");

        var decoded = OutboxHeaderCodec.Decode(OutboxHeaderCodec.Encode(headers))!;

        await Assert.That(decoded.Count).IsEqualTo(2);
        await Assert.That(decoded[0].Value.ToArray()).IsEquivalentTo("one"u8.ToArray());
        await Assert.That(decoded[1].Value.ToArray()).IsEquivalentTo("two"u8.ToArray());
    }

}
