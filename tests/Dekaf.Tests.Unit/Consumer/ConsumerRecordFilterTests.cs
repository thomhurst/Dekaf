using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerRecordFilterTests
{
    [Test]
    public async Task ConsumeOneAsync_FilterSkipsRejectedRecordBeforeDeserialization()
    {
        var fetch = CreatePendingFetchData(
            CreateRecord(0, "reject", "one", new Header("route", "drop"u8.ToArray())),
            CreateRecord(1, "accept", "two", new Header("route", "keep"u8.ToArray())));
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        var keyDeserializer = new CountingStringDeserializer();
        var valueDeserializer = new CountingStringDeserializer();
        await using var consumer = CreateConsumer(fetch, filter, keyDeserializer, valueDeserializer);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(1L);
        await Assert.That(keyDeserializer.Count).IsEqualTo(1);
        await Assert.That(valueDeserializer.Count).IsEqualTo(1);
        await Assert.That(filter.CallCount).IsEqualTo(2);
        await Assert.That(consumer.GetPosition(new TopicPartition("test-topic", 2))).IsEqualTo(2L);
    }

    [Test]
    public async Task ConsumeAsync_FilterSkipsRejectedRecordBeforeDeserialization()
    {
        var fetch = CreatePendingFetchData(
            CreateRecord(0, "reject", "one", new Header("route", "drop"u8.ToArray())),
            CreateRecord(1, "accept", "two", new Header("route", "keep"u8.ToArray())));
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        var keyDeserializer = new CountingStringDeserializer();
        var valueDeserializer = new CountingStringDeserializer();
        await using var consumer = CreateConsumer(fetch, filter, keyDeserializer, valueDeserializer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        await Assert.That(await records.MoveNextAsync()).IsTrue();

        await Assert.That(records.Current.Offset).IsEqualTo(1L);
        await Assert.That(keyDeserializer.Count).IsEqualTo(1);
        await Assert.That(valueDeserializer.Count).IsEqualTo(1);
        await Assert.That(filter.CallCount).IsEqualTo(2);
        await Assert.That(consumer.GetPosition(new TopicPartition("test-topic", 2))).IsEqualTo(2L);
    }

    [Test]
    public async Task ConsumeOneAsync_FilterRunsBeforeAsyncDeserializer()
    {
        var fetch = CreatePendingFetchData(
            CreateRecord(0, "reject", "one", new Header("route", "drop"u8.ToArray())),
            CreateRecord(1, "accept", "two", new Header("route", "keep"u8.ToArray())));
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        var valueDeserializer = new CountingAsyncStringDeserializer();
        await using var consumer = CreateConsumer(
            fetch,
            filter,
            Serializers.String,
            Serializers.String,
            valueDeserializer);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("two");
        await Assert.That(valueDeserializer.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_HeaderRouterReceivesPooledRecordHeaders()
    {
        var fetch = CreatePendingFetchData(
            CreateRecord(0, "key", "payload", new Header("event-type", "created"u8.ToArray())));
        var router = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("fallback"),
            new HeaderDeserializerRoute<string>("created"u8.ToArray(), new PrefixDeserializer("created")));
        await using var consumer = CreateConsumer(fetch, null, Serializers.String, router);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("created:payload");
    }

    [Test]
    public async Task ConsumerBuilder_WithRecordFilterPropagatesOption()
    {
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        await using var consumer = (KafkaConsumer<string, string>)Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithRecordFilter(filter)
            .Build();

        await Assert.That(consumer.Options.RecordFilter).IsSameReferenceAs(filter);
    }

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
    public async Task ConsumeBatch_HeaderRouterReceivesPooledRecordHeaders()
    {
        using var pending = CreatePendingFetchData(
            CreateRecord(0, "key", "payload", new Header("event-type", "created"u8.ToArray())));
        var router = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("fallback"),
            new HeaderDeserializerRoute<string>("created"u8.ToArray(), new PrefixDeserializer("created")));
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            router);

        var result = batch.Single();

        await Assert.That(result.Value).IsEqualTo("created:payload");
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

    [Test]
    public async Task ConsumeOneAsync_FilterExceptionPropagatesWithoutAdvancingPosition()
    {
        var fetch = CreatePendingFetchData(CreateRecord(0, "key", "value"));
        var topicPartition = fetch.TopicPartition;
        var expected = new InvalidOperationException("filter failed");
        await using var consumer = CreateConsumer(
            fetch,
            new ThrowingFilter(expected),
            Serializers.String,
            Serializers.String);

        var actual = (await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1)))
            .Throws<InvalidOperationException>())!;

        await Assert.That(actual).IsSameReferenceAs(expected);
        await Assert.That(GetFetchPositions(consumer)[topicPartition]).IsEqualTo(0L);
        await Assert.That(GetPendingFetches(consumer)).IsEmpty();
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

    private static KafkaConsumer<string, string> CreateConsumer(
        PendingFetchData fetch,
        IConsumerRecordFilter? filter,
        IDeserializer<string> keyDeserializer,
        IDeserializer<string> valueDeserializer,
        IAsyncDeserializer<string>? asyncValueDeserializer = null)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
                RecordFilter = filter
            },
            keyDeserializer,
            valueDeserializer,
            asyncValueDeserializer: asyncValueDeserializer);
        var initialized = typeof(KafkaConsumer<string, string>).GetField(
            "_initialized",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found.");
        initialized.SetValue(consumer, true);
        consumer.Assign(fetch.TopicPartition);
        GetFetchPositions(consumer)[fetch.TopicPartition] = 0;
        GetPendingFetches(consumer).Enqueue(fetch);
        return consumer;
    }

    private static ConcurrentDictionary<TopicPartition, long> GetFetchPositions(
        KafkaConsumer<string, string> consumer) =>
        (ConcurrentDictionary<TopicPartition, long>)(typeof(KafkaConsumer<string, string>)
            .GetField("_fetchPositions", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchPositions field not found."))
        .GetValue(consumer)!;

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer) =>
        (Queue<PendingFetchData>)(typeof(KafkaConsumer<string, string>)
            .GetField("_pendingFetches", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingFetches field not found."))
        .GetValue(consumer)!;

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

    private sealed class CountingAsyncStringDeserializer : IAsyncDeserializer<string>
    {
        public int Count { get; private set; }

        public ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(Encoding.UTF8.GetString(data.Span));
        }
    }

    private sealed class PrefixDeserializer(string prefix) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            $"{prefix}:{Encoding.UTF8.GetString(data.Span)}";
    }
}
