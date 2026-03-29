using System.Buffers;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

/// <summary>
/// Verifies that ISpanDeserializer&lt;T&gt;.DeserializeSpan produces the same result as
/// IDeserializer&lt;T&gt;.Deserialize for the same input across all built-in serdes.
/// This ensures the two deserialization paths never diverge.
/// </summary>
public class SpanDeserializerTests
{
    private static SerializationContext CreateContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Value };

    [Test]
    public async Task StringSerde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.String;
        var spanSerde = (ISpanDeserializer<string>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize("hello world", ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task Int32Serde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.Int32;
        var spanSerde = (ISpanDeserializer<int>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize(42, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task Int64Serde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.Int64;
        var spanSerde = (ISpanDeserializer<long>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize(9876543210L, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task GuidSerde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.Guid;
        var spanSerde = (ISpanDeserializer<Guid>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var guid = Guid.NewGuid();

        serde.Serialize(guid, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task DoubleSerde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.Double;
        var spanSerde = (ISpanDeserializer<double>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize(3.14159, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task FloatSerde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.Float;
        var spanSerde = (ISpanDeserializer<float>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize(1.5f, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task ByteArraySerde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.ByteArray;
        var spanSerde = (ISpanDeserializer<byte[]>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var input = new byte[] { 1, 2, 3, 4, 5 };

        serde.Serialize(input, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEquivalentTo(sequenceResult);
    }

    [Test]
    public async Task DateTimeSerde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.DateTime;
        var spanSerde = (ISpanDeserializer<DateTime>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);

        serde.Serialize(value, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task DateTimeOffsetSerde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.DateTimeOffset;
        var spanSerde = (ISpanDeserializer<DateTimeOffset>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = new DateTimeOffset(2024, 6, 15, 14, 30, 45, new TimeSpan(5, 30, 0));

        serde.Serialize(value, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task TimeSpanSerde_DeserializeSpan_MatchesDeserialize()
    {
        var serde = Serializers.TimeSpan;
        var spanSerde = (ISpanDeserializer<TimeSpan>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = TimeSpan.FromHours(2.5);

        serde.Serialize(value, ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task NullableStringSerde_DeserializeSpan_MatchesDeserialize_NonNull()
    {
        var serde = Serializers.NullableString;
        var spanSerde = (ISpanDeserializer<string?>)serde;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize("test value", ref buffer, context);
        var data = buffer.WrittenMemory;

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(data), context);
        var spanResult = spanSerde.DeserializeSpan(data.Span, context);

        await Assert.That(spanResult).IsEqualTo(sequenceResult);
    }

    [Test]
    public async Task NullableStringSerde_DeserializeSpan_MatchesDeserialize_Null()
    {
        var serde = Serializers.NullableString;
        var spanSerde = (ISpanDeserializer<string?>)serde;
        var context = new SerializationContext
        {
            Topic = "test",
            Component = SerializationComponent.Value,
            IsNull = true
        };

        var sequenceResult = serde.Deserialize(new ReadOnlySequence<byte>(ReadOnlyMemory<byte>.Empty), context);
        var spanResult = spanSerde.DeserializeSpan(ReadOnlySpan<byte>.Empty, context);

        await Assert.That(spanResult).IsNull();
        await Assert.That(sequenceResult).IsNull();
    }

    [Test]
    public async Task RawBytesSerde_DoesNotImplementISpanDeserializer()
    {
        // RawBytesSerde intentionally does not implement ISpanDeserializer<ReadOnlyMemory<byte>>
        // because it already provides zero-copy access via ReadOnlyMemory<byte>.
        var serde = Serializers.RawBytes;

        await Assert.That(serde is ISpanDeserializer<ReadOnlyMemory<byte>>).IsFalse();
    }
}
