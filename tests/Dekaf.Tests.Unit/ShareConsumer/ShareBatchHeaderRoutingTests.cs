using System.Buffers;
using System.Text;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareBatchHeaderRoutingTests
{
    [Test]
    [NotInParallel]
    public async Task ConfiguredNames_DoNotPopulateTheSharedHeaderCache()
    {
        var prefix = Guid.NewGuid().ToString("N");
        var names = new byte[129][];
        IDeserializer<int> router = Serializers.Int32;
        for (var index = 0; index < names.Length; index++)
        {
            var name = $"{prefix}-{index}";
            names[index] = Encoding.UTF8.GetBytes(name);
            router = new HeaderRoutingDeserializer<int>(name, Serializers.Int32,
                new HeaderDeserializerRoute<int>("selected"u8.ToArray(), router));
        }

        var plan = RecordHeaderRoutingPlan.Create(Serializers.Int32, router)!;
        var keys = new ShareBatchHeaderKeys(plan);
        foreach (var name in names)
        {
            await Assert.That(HeaderProtocol.TryGetCachedKey(name, out _, out _)).IsFalse();
            await Assert.That(keys.Get(name)).IsEqualTo(Encoding.UTF8.GetString(name));
            await Assert.That(HeaderProtocol.TryGetCachedKey(name, out _, out _)).IsFalse();
        }
    }

    [Test]
    [Arguments('x', 256)]
    [Arguments('x', 257)]
    [Arguments('x', 512)]
    [Arguments('x', 1024)]
    [Arguments('路', 85)]
    [Arguments('路', 86)]
    public async Task ConfiguredNames_DoNotAllocatePerRecord(char character, int nameLength)
    {
        var name = new string(character, nameLength);
        var router = new HeaderRoutingDeserializer<int>(name, Serializers.Int32,
            new HeaderDeserializerRoute<int>("selected"u8.ToArray(), Serializers.Int32));
        await using var consumer = CreateConsumer(router);
        var small = CreateBytes(name, 1);
        var large = CreateBytes(name, 64);
        ShareFetchAcquiredRecords[] acquired = [new() { FirstOffset = 0, LastOffset = 63, DeliveryCount = 1 }];
        Parse(consumer, small, acquired);
        Parse(consumer, large, acquired);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Parse(consumer, small, acquired);
        var smallAllocation = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        Parse(consumer, large, acquired);
        var largeAllocation = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(largeAllocation).IsEqualTo(smallAllocation);
    }

    [Test]
    [Arguments("route")]
    [Arguments("routé-路")]
    [Arguments("replacement-\uFFFD")]
    public async Task Routing_PreservesDuplicatesNullsAndRawHeaders(string name)
    {
        var selected = new ConstantDeserializer(42);
        var fallback = new ConstantDeserializer(-1);
        var router = new HeaderRoutingDeserializer<int>(name, fallback,
            new HeaderDeserializerRoute<int>("selected"u8.ToArray(), selected));
        await using var consumer = CreateConsumer(router);
        var bytes = CreateBytes(name, 3, duplicateNull: true);
        var reader = new KafkaProtocolReader(bytes);
        using var batch = await consumer.ParseRecordBatchAsync(new TopicPartition("routing", 0),
            RecordBatch.Read(ref reader), [new() { FirstOffset = 0, LastOffset = 2, DeliveryCount = 2 }], 3, default);
        var count = 0;
        foreach (var record in batch)
        {
            await Assert.That(record.Value).IsEqualTo(count == 1 ? -1 : 42);
            await Assert.That(record.DeliveryCount).IsEqualTo(2);
            await Assert.That(record.Headers.Count).IsEqualTo(count == 1 ? 3 : 2);
            var headers = record.Headers.GetEnumerator();
            await Assert.That(headers.MoveNext()).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(headers.Current.KeyUtf8.Span)).IsEqualTo(name);
            count++;
        }
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    [Arguments(4)]
    [Arguments(129)]
    public async Task NestedRoutingNames_UseEveryConfiguredName(int count)
    {
        IDeserializer<int> router = new ConstantDeserializer(42);
        var names = new string[count];
        for (var index = 0; index < count; index++)
        {
            names[index] = new string('r', 512) + index;
            router = new HeaderRoutingDeserializer<int>(names[index], new ConstantDeserializer(-1),
                new HeaderDeserializerRoute<int>("selected"u8.ToArray(), router));
        }
        var headers = new Header[count];
        for (var index = 0; index < count; index++)
            headers[index] = new Header(names[index], "selected"u8.ToArray());
        using var source = new RecordBatch
        {
            Records = [new Record { IsKeyNull = true, Value = new byte[4], Headers = headers, HeaderCount = count }]
        };
        var output = new ArrayBufferWriter<byte>();
        source.Write(output);
        await using var consumer = CreateConsumer(router);
        var reader = new KafkaProtocolReader(output.WrittenMemory);
        using var batch = await consumer.ParseRecordBatchAsync(new TopicPartition("routing", 0),
            RecordBatch.Read(ref reader), [new() { FirstOffset = 0, LastOffset = 0, DeliveryCount = 1 }], 1, default);
        var records = batch.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        await Assert.That(records.Current.Value).IsEqualTo(42);
        await Assert.That(records.Current.Headers.Count).IsEqualTo(count);
    }

    [Test]
    [Arguments(0)]
    [Arguments(256)]
    public async Task InvalidConfiguredSurrogate_DoesNotMatchReplacementEncodedWireName(int padding)
    {
        var invalidName = new string('x', padding) + "invalid-\uD800";
        var router = new HeaderRoutingDeserializer<int>(invalidName, new ConstantDeserializer(-1),
            new HeaderDeserializerRoute<int>("selected"u8.ToArray(), new ConstantDeserializer(42)));
        await using var consumer = CreateConsumer(router);
        var reader = new KafkaProtocolReader(CreateBytes(invalidName, 1));
        using var batch = await consumer.ParseRecordBatchAsync(new TopicPartition("routing", 0),
            RecordBatch.Read(ref reader), [new() { FirstOffset = 0, LastOffset = 0, DeliveryCount = 1 }], 1, default);
        var records = batch.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        await Assert.That(records.Current.Value).IsEqualTo(-1);
    }

    private static KafkaShareConsumer<int, int> CreateConsumer(IDeserializer<int> value) =>
        new(new ShareConsumerOptions { BootstrapServers = ["localhost:9092"], GroupId = "routing" },
            Serializers.Int32, value);

    private static ReadOnlyMemory<byte> CreateBytes(string name, int count, bool duplicateNull = false)
    {
        var records = new Record[count];
        for (var index = 0; index < count; index++)
        {
            Header[] headers = duplicateNull && index == 1
                ? [new(name, "selected"u8.ToArray()), new("unrelated", "other"u8.ToArray()), new(name, (byte[]?)null)]
                : [new(name, "selected"u8.ToArray()), new("unrelated", "other"u8.ToArray())];
            records[index] = new Record
            {
                OffsetDelta = index, IsKeyNull = true, Value = new byte[4], Headers = headers, HeaderCount = headers.Length
            };
        }
        using var source = new RecordBatch { Records = records };
        var output = new ArrayBufferWriter<byte>();
        source.Write(output);
        return output.WrittenMemory;
    }

    private static void Parse(KafkaShareConsumer<int, int> consumer, ReadOnlyMemory<byte> bytes,
        ShareFetchAcquiredRecords[] acquired)
    {
        var reader = new KafkaProtocolReader(bytes);
        var operation = consumer.ParseRecordBatchAsync(new TopicPartition("routing", 0),
            RecordBatch.Read(ref reader), acquired, 64, default);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("The prepared parser must complete synchronously.");
        using var batch = operation.GetAwaiter().GetResult();
        foreach (var record in batch)
            _ = record.Value;
    }

    private sealed class ConstantDeserializer(int value) : IDeserializer<int>
    {
        public int Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => value;
    }
}
