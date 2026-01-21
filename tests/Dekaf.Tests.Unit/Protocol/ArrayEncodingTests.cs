using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for Kafka protocol array encoding.
/// Reference: https://kafka.apache.org/protocol#protocol_types
///
/// ARRAY: INT32 length prefix + elements
/// NULLABLE_ARRAY: -1 length = null/empty
/// COMPACT_ARRAY: UNSIGNED_VARINT (length + 1) prefix + elements
/// COMPACT_NULLABLE_ARRAY: 0 = null, otherwise length + 1
/// </summary>
public class ArrayEncodingTests
{
    #region ARRAY (Legacy) Tests

    [Test]
    public async Task Array_Empty_EncodedWithZeroLength()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteArray<int>([], (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v));

        // INT32 length = 0
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00, 0x00, 0x00, 0x00 });
    }

    [Test]
    public async Task Array_SingleElement_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteArray([42], (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v));

        // INT32 length = 1, then INT32 42
        await Assert.That(buffer.WrittenSpan.ToArray())
            .IsEquivalentTo(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x2A });
    }

    [Test]
    public async Task Array_MultipleElements_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteArray([1, 2, 3], (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v));

        var expected = new byte[]
        {
            0x00, 0x00, 0x00, 0x03, // length = 3
            0x00, 0x00, 0x00, 0x01, // element 1
            0x00, 0x00, 0x00, 0x02, // element 2
            0x00, 0x00, 0x00, 0x03  // element 3
        };
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Array_RoundTrip_Strings()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var input = new[] { "foo", "bar", "baz" };
        writer.WriteArray(
            input.AsSpan(),
            (ref KafkaProtocolWriter w, string v) => w.WriteString(v));

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var result = reader.ReadArray((ref KafkaProtocolReader r) => r.ReadString()!);

        await Assert.That(result).IsEquivalentTo(input);
    }

    [Test]
    public async Task NullableArray_Null_EncodedAsMinusOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteNullableArray<int>([], (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v), isNull: true);

        // INT32 length = -1
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
    }

    [Test]
    public async Task NullableArray_Null_RoundTrip()
    {
        var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        var reader = new KafkaProtocolReader(data);
        var result = reader.ReadArray((ref KafkaProtocolReader r) => r.ReadInt32());

        // ReadArray returns empty array for -1 length
        await Assert.That(result).IsEmpty();
    }

    #endregion

    #region COMPACT_ARRAY Tests

    [Test]
    public async Task CompactArray_Empty_EncodedWithLengthPlusOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactArray<int>([], (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v));

        // length + 1 = 1
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x01 });
    }

    [Test]
    public async Task CompactArray_SingleElement_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactArray([42], (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v));

        // length + 1 = 2, then INT32 42
        await Assert.That(buffer.WrittenSpan.ToArray())
            .IsEquivalentTo(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x2A });
    }

    [Test]
    public async Task CompactArray_MultipleElements_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactArray([1, 2, 3], (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v));

        var expected = new byte[]
        {
            0x04,                   // length + 1 = 4
            0x00, 0x00, 0x00, 0x01, // element 1
            0x00, 0x00, 0x00, 0x02, // element 2
            0x00, 0x00, 0x00, 0x03  // element 3
        };
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task CompactArray_RoundTrip_Strings()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var input = new[] { "foo", "bar", "baz" };
        writer.WriteCompactArray(
            input.AsSpan(),
            (ref KafkaProtocolWriter w, string v) => w.WriteCompactString(v));

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var result = reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadCompactString()!);

        await Assert.That(result).IsEquivalentTo(input);
    }

    [Test]
    public async Task CompactNullableArray_Null_EncodedAsZero()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactNullableArray<int>([], (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v), isNull: true);

        // 0 = null
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task CompactNullableArray_Null_RoundTrip()
    {
        var data = new byte[] { 0x00 };
        var reader = new KafkaProtocolReader(data);
        var result = reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadInt32());

        // ReadCompactArray returns empty array for 0 length
        await Assert.That(result).IsEmpty();
    }

    #endregion

    #region Nested Array Tests

    [Test]
    public async Task Array_NestedArrays_RoundTrip()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // Write array of arrays
        var data = new[] { new[] { 1, 2 }, new[] { 3, 4, 5 } };
        writer.WriteArray(
            data.AsSpan(),
            (ref KafkaProtocolWriter w, int[] arr) =>
                w.WriteArray(arr.AsSpan(), (ref KafkaProtocolWriter w2, int v) => w2.WriteInt32(v)));

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var result = reader.ReadArray((ref KafkaProtocolReader r) =>
            r.ReadArray((ref KafkaProtocolReader r2) => r2.ReadInt32()));

        var expected0 = new[] { 1, 2 };
        var expected1 = new[] { 3, 4, 5 };
        await Assert.That(result.Length).IsEqualTo(2);
        await Assert.That(result[0]).IsEquivalentTo(expected0);
        await Assert.That(result[1]).IsEquivalentTo(expected1);
    }

    #endregion

    #region Large Array Tests

    [Test]
    public async Task CompactArray_LargeArray_MultiByteLength()
    {
        // Array with more than 126 elements requires multi-byte length
        var items = Enumerable.Range(0, 200).ToArray();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactArray(
            items.AsSpan(),
            (ref KafkaProtocolWriter w, int v) => w.WriteInt32(v));

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var result = reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadInt32());

        await Assert.That(result).IsEquivalentTo(items);
    }

    #endregion
}
