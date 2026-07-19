using System.Text;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class RecordDeserializationExceptionTests
{
    private const string Topic = "orders";
    private const int Partition = 3;
    private const long Offset = 42;
    private const long TimestampMs = 1_700_000_000_123;

    [Test]
    public async Task Constructor_CopiesRecordDataAndPreservesMetadata()
    {
        var key = Encoding.UTF8.GetBytes("key");
        var value = Encoding.UTF8.GetBytes("value");
        var firstHeaderValue = Encoding.UTF8.GetBytes("first");
        var headers = new Header[]
        {
            new("trace-id", firstHeaderValue),
            new("trace-id", (byte[]?)null),
            new("empty", Array.Empty<byte>())
        };
        var inner = new InvalidDataException("bad payload");

        var exception = new RecordDeserializationException(
            DeserializationExceptionOrigin.Value,
            new TopicPartition(Topic, Partition),
            Offset,
            TimestampMs,
            TimestampType.LogAppendTime,
            key,
            value,
            headers,
            inner);

        key[0] = (byte)'X';
        value[0] = (byte)'X';
        firstHeaderValue[0] = (byte)'X';

        await Assert.That(exception.Origin).IsEqualTo(DeserializationExceptionOrigin.Value);
        await Assert.That(exception.TopicPartition).IsEqualTo(new TopicPartition(Topic, Partition));
        await Assert.That(exception.Offset).IsEqualTo(Offset);
        await Assert.That(exception.TimestampMs).IsEqualTo(TimestampMs);
        await Assert.That(exception.Timestamp).IsEqualTo(DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs));
        await Assert.That(exception.TimestampType).IsEqualTo(TimestampType.LogAppendTime);
        await Assert.That(exception.Topic).IsEqualTo(Topic);
        await Assert.That(exception.Component).IsEqualTo(SerializationComponent.Value);
        await Assert.That(exception.InnerException).IsSameReferenceAs(inner);
        await Assert.That(Encoding.UTF8.GetString(exception.KeyData!)).IsEqualTo("key");
        await Assert.That(Encoding.UTF8.GetString(exception.ValueData!)).IsEqualTo("value");
        await Assert.That(exception.Headers.Select(static header => header.Key).ToArray())
            .IsEquivalentTo(["trace-id", "trace-id", "empty"]);
        await Assert.That(Encoding.UTF8.GetString(exception.Headers[0].Value.Span)).IsEqualTo("first");
        await Assert.That(exception.Headers[1].IsValueNull).IsTrue();
        await Assert.That(exception.Headers[2].IsValueNull).IsFalse();
        await Assert.That(exception.Headers[2].Value.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Constructor_DistinguishesEmptyDataFromNullData()
    {
        var exception = new RecordDeserializationException(
            DeserializationExceptionOrigin.Key,
            new TopicPartition(Topic, Partition),
            Offset,
            TimestampMs,
            TimestampType.CreateTime,
            ReadOnlyMemory<byte>.Empty,
            valueData: null,
            headers: null,
            new InvalidDataException());

        await Assert.That(exception.KeyData).IsNotNull();
        await Assert.That(exception.KeyData).IsEmpty();
        await Assert.That(exception.ValueData).IsNull();
    }

    [Test]
    public async Task ConsumeResult_CancellationFromDeserializer_IsNotWrapped()
    {
        await Assert.That(() => new ConsumeResult<string, string>(
                Topic,
                Partition,
                Offset,
                Encoding.UTF8.GetBytes("key"),
                isKeyNull: false,
                Encoding.UTF8.GetBytes("value"),
                isValueNull: false,
                headers: null,
                TimestampMs,
                TimestampType.CreateTime,
                leaderEpoch: null,
                keyDeserializer: Serializers.String,
                valueDeserializer: new ThrowingDeserializer(new OperationCanceledException())))
            .Throws<OperationCanceledException>();
    }

    private sealed class ThrowingDeserializer(Exception exception) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => throw exception;
    }
}
