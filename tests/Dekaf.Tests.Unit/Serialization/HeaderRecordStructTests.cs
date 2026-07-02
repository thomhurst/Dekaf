using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

public class HeaderRecordStructTests
{
    [Test]
    public async Task Constructor_ByteArray_SetsKeyAndValue()
    {
        var value = "hello"u8.ToArray();
        var header = new Header("key", value);
        await Assert.That(header.Key).IsEqualTo("key");
        await Assert.That(header.GetValueAsString()).IsEqualTo("hello");
    }

    [Test]
    public async Task Constructor_NullByteArray_SetsIsValueNull()
    {
        var header = new Header("key", (byte[]?)null);
        await Assert.That(header.Key).IsEqualTo("key");
        await Assert.That(header.IsValueNull).IsTrue();
    }

    [Test]
    public async Task Constructor_ReadOnlyMemory_SetsValue()
    {
        var data = "hello"u8.ToArray();
        var memory = new ReadOnlyMemory<byte>(data);
        var header = new Header("key", memory);
        await Assert.That(header.Key).IsEqualTo("key");
        await Assert.That(header.GetValueAsString()).IsEqualTo("hello");
        await Assert.That(header.IsValueNull).IsFalse();
    }

    [Test]
    public async Task Constructor_ReadOnlyMemory_WithIsNullTrue_SetsIsValueNull()
    {
        var header = new Header("key", ReadOnlyMemory<byte>.Empty, isNull: true);
        await Assert.That(header.IsValueNull).IsTrue();
    }

    [Test]
    public async Task IsValueNull_NonNullValue_ReturnsFalse()
    {
        var header = new Header("key", "value"u8.ToArray());
        await Assert.That(header.IsValueNull).IsFalse();
    }

    [Test]
    public async Task IsValueNull_NullValue_ReturnsTrue()
    {
        var header = new Header("key", (byte[]?)null);
        await Assert.That(header.IsValueNull).IsTrue();
    }

    [Test]
    public async Task GetValueAsArray_NonNull_ReturnsBytes()
    {
        var data = new byte[] { 1, 2, 3 };
        var header = new Header("key", data);
        var result = header.GetValueAsArray();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!).IsEquivalentTo(data);
    }

    [Test]
    public async Task GetValueAsArray_Null_ReturnsNull()
    {
        var header = new Header("key", (byte[]?)null);
        var result = header.GetValueAsArray();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetValueAsString_NonNull_ReturnsUtf8String()
    {
        var header = new Header("key", "hello world"u8.ToArray());
        var result = header.GetValueAsString();
        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task GetValueAsString_Null_ReturnsNull()
    {
        var header = new Header("key", (byte[]?)null);
        var result = header.GetValueAsString();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ToString_WithValue_ReturnsKeyEqualsValue()
    {
        var header = new Header("key", "value"u8.ToArray());
        var result = header.ToString();
        await Assert.That(result).IsEqualTo("key=value");
    }

    [Test]
    public async Task ToString_WithNullValue_ReturnsKeyEqualsNull()
    {
        var header = new Header("key", (byte[]?)null);
        var result = header.ToString();
        await Assert.That(result).IsEqualTo("key=(null)");
    }

    [Test]
    public async Task Equality_SameKeyAndValue_AreEqual()
    {
        var data = "value"u8.ToArray();
        var header1 = new Header("key", data);
        var header2 = new Header("key", data);
        await Assert.That(header1).IsEqualTo(header2);
    }

    [Test]
    public async Task Equality_DifferentKey_AreNotEqual()
    {
        var data = "value"u8.ToArray();
        var header1 = new Header("key1", data);
        var header2 = new Header("key2", data);
        await Assert.That(header1).IsNotEqualTo(header2);
    }

    [Test]
    public async Task Write_LongUtf8Key_RoundTrips()
    {
        var key = new string('x', 140) + "-é";
        var parsed = RoundTrip(new Header(key, "value"u8.ToArray()));

        await Assert.That(parsed.Key).IsEqualTo(key);
        await Assert.That(parsed.GetValueAsString()).IsEqualTo("value");
    }

    [Test]
    public async Task Read_RepeatedKeyBytes_ReusesCachedString()
    {
        var key = "trace-id-" + Guid.NewGuid().ToString("N");

        var first = RoundTrip(new Header(key, "one"u8.ToArray()));
        var second = RoundTrip(new Header(key, "two"u8.ToArray()));

        await Assert.That(second.Key).IsSameReferenceAs(first.Key);
    }

    private static Header RoundTrip(Header header)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        header.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        return Header.Read(ref reader);
    }
}
