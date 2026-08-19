using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerRecordFilterTests
{
    [Test]
    public async Task ConsumeBatch_FilterRejectsBeforeDeserializationAndAdvancesPosition()
    {
        using var pending = CreatePendingFetchData(
            CreateRecord(0, "reject", "one", new Header("route", "drop"u8.ToArray())),
            CreateRecord(1, "accept", "two", new Header("route", "keep"u8.ToArray())));
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        var keyDeserializer = new CountingStringDeserializer();
        var valueDeserializer = new CountingStringDeserializer();
        var storedOffset = -1L;
        var batch = new ConsumeBatch<string, string>(
            pending,
            keyDeserializer,
            valueDeserializer,
            recordFilter: filter,
            storeOffsetOnDelivery: (_, offset, _) => storedOffset = offset);

        var results = new List<ConsumeResult<string, string>>();
        foreach (var result in batch)
            results.Add(result);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0].Offset).IsEqualTo(1L);
        await Assert.That(results[0].Key).IsEqualTo("accept");
        await Assert.That(results[0].Value).IsEqualTo("two");
        await Assert.That(keyDeserializer.Count).IsEqualTo(1);
        await Assert.That(valueDeserializer.Count).IsEqualTo(1);
        await Assert.That(filter.CallCount).IsEqualTo(2);
        await Assert.That(storedOffset).IsEqualTo(2L);
    }

    [Test]
    public async Task ConsumeBatch_FilterContextExposesRawFieldsAndNullHeaderValue()
    {
        using var pending = CreatePendingFetchData(
            CreateRecord(3, key: null, value: "payload", new Header("nullable", (byte[]?)null)));
        var filter = new InspectingFilter();
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            recordFilter: filter);

        foreach (var _ in batch) { }

        await Assert.That(filter.Topic).IsEqualTo("test-topic");
        await Assert.That(filter.Partition).IsEqualTo(2);
        await Assert.That(filter.Offset).IsEqualTo(3L);
        await Assert.That(filter.IsKeyNull).IsTrue();
        await Assert.That(filter.IsValueNull).IsFalse();
        await Assert.That(filter.Value).IsEquivalentTo("payload"u8.ToArray());
        await Assert.That(filter.HeaderWasNull).IsTrue();
    }

    [Test]
    public async Task ConsumeBatch_FilterExceptionPropagatesWithoutAdvancingPosition()
    {
        using var pending = CreatePendingFetchData(CreateRecord(0, "key", "value"));
        var storedOffset = -1L;
        var expected = new InvalidOperationException("filter failed");
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            recordFilter: new ThrowingFilter(expected),
            storeOffsetOnDelivery: (_, offset, _) => storedOffset = offset);
        using var enumerator = batch.GetEnumerator();

        var actual = (await Assert.That(() => enumerator.MoveNext()).Throws<InvalidOperationException>())!;

        await Assert.That(actual).IsSameReferenceAs(expected);
        await Assert.That(storedOffset).IsEqualTo(-1L);
    }

    private static PendingFetchData CreatePendingFetchData(params Record[] records)
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1_700_000_000_000,
            PartitionLeaderEpoch = 7,
            Records = records
        };
        var pending = PendingFetchData.Create("test-topic", 2, [batch]);
        pending.EagerParseAll();
        return pending;
    }

    private static Record CreateRecord(int offset, string? key, string value, params Header[] headers) => new()
    {
        OffsetDelta = offset,
        TimestampDelta = offset * 10,
        Key = key is null ? ReadOnlyMemory<byte>.Empty : Encoding.UTF8.GetBytes(key),
        Value = Encoding.UTF8.GetBytes(value),
        IsKeyNull = key is null,
        IsValueNull = false,
        Headers = headers.Length == 0 ? null : headers,
        HeaderCount = headers.Length
    };

    private sealed class HeaderValueFilter(string headerName, byte[] acceptedValue) : IConsumerRecordFilter
    {
        public int CallCount { get; private set; }

        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context)
        {
            CallCount++;
            var headers = context.Headers;
            for (var i = 0; i < headers.Length; i++)
            {
                ref readonly var header = ref headers[i];
                if (header.Key == headerName)
                    return !header.IsValueNull && header.Value.Span.SequenceEqual(acceptedValue);
            }

            return false;
        }
    }

    private sealed class InspectingFilter : IConsumerRecordFilter
    {
        public string? Topic { get; private set; }
        public int Partition { get; private set; }
        public long Offset { get; private set; }
        public bool IsKeyNull { get; private set; }
        public bool IsValueNull { get; private set; }
        public byte[]? Value { get; private set; }
        public bool HeaderWasNull { get; private set; }

        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context)
        {
            Topic = context.Topic;
            Partition = context.Partition;
            Offset = context.Offset;
            IsKeyNull = context.IsKeyNull;
            IsValueNull = context.IsValueNull;
            Value = context.Value.ToArray();
            HeaderWasNull = context.Headers.Length == 1 && context.Headers[0].IsValueNull;
            return true;
        }
    }

    private sealed class ThrowingFilter(Exception exception) : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context) => throw exception;
    }

    private sealed class CountingStringDeserializer : IDeserializer<string>
    {
        public int Count { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Count++;
            return Encoding.UTF8.GetString(data.Span);
        }
    }
}
