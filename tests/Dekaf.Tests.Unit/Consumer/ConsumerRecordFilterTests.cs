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
    public async Task ConsumeOneAsync_FilterYieldsToPollAfterRejectedRecordInterval()
    {
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        await using var consumer = CreateConsumer(
            CreatePollRefreshRecords(),
            filter,
            Serializers.String,
            Serializers.String);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(65L);
        await Assert.That(filter.CallCount).IsEqualTo(66);
        await Assert.That(consumer.GetPosition(new TopicPartition("test-topic", 2))).IsEqualTo(66L);
    }

    [Test]
    public async Task ConsumeOneAsync_AsyncDeserializerFilterYieldsToPollAfterRejectedRecordInterval()
    {
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        var valueDeserializer = new CountingAsyncStringDeserializer();
        await using var consumer = CreateConsumer(
            CreatePollRefreshRecords(),
            filter,
            Serializers.String,
            Serializers.String,
            valueDeserializer);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(65L);
        await Assert.That(filter.CallCount).IsEqualTo(66);
        await Assert.That(valueDeserializer.Count).IsEqualTo(1);
        await Assert.That(consumer.GetPosition(new TopicPartition("test-topic", 2))).IsEqualTo(66L);
    }

    [Test]
    public async Task ConsumeOneAsync_FilterObservesCancellationBetweenRejectedRecords()
    {
        using var cancellation = new CancellationTokenSource();
        var filter = new CancellingFilter(cancellation);
        await using var consumer = CreateConsumer(
            CreatePollRefreshRecords(),
            filter,
            Serializers.String,
            Serializers.String);

        await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), cancellation.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(filter.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_ZeroTimeoutStopsAfterFirstRejectedRecord()
    {
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        await using var consumer = CreateConsumer(
            CreatePollRefreshRecords(),
            filter,
            Serializers.String,
            Serializers.String);

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNull();
        await Assert.That(filter.CallCount).IsLessThanOrEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_AsyncDeserializerFilterObservesCancellationBetweenRejectedRecords()
    {
        using var cancellation = new CancellationTokenSource();
        var filter = new CancellingFilter(cancellation);
        await using var consumer = CreateConsumer(
            CreatePollRefreshRecords(),
            filter,
            Serializers.String,
            Serializers.String,
            new CountingAsyncStringDeserializer());

        await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), cancellation.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(filter.CallCount).IsEqualTo(1);
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
    public async Task ConsumeAsync_FilterObservesCancellationBetweenRejectedRecords()
    {
        using var cancellation = new CancellationTokenSource();
        var filter = new CancellingFilter(cancellation);
        await using var consumer = CreateConsumer(
            CreatePollRefreshRecords(),
            filter,
            Serializers.String,
            Serializers.String);
        await using var records = consumer.ConsumeAsync(cancellation.Token).GetAsyncEnumerator();

        await Assert.That(async () => await records.MoveNextAsync())
            .Throws<OperationCanceledException>();

        await Assert.That(filter.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeAsync_FilterExceptionPropagatesWithoutAdvancingPosition()
    {
        var fetch = CreatePendingFetchData(CreateRecord(0, "key", "value"));
        var topicPartition = fetch.TopicPartition;
        var expected = new InvalidOperationException("filter failed");
        await using var consumer = CreateConsumer(
            fetch,
            new ThrowingFilter(expected),
            Serializers.String,
            Serializers.String);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        var actual = (await Assert.That(async () => await records.MoveNextAsync())
            .Throws<InvalidOperationException>())!;

        await Assert.That(actual).IsSameReferenceAs(expected);
        await Assert.That(GetFetchPositions(consumer)[topicPartition]).IsEqualTo(0L);
        await Assert.That(GetPendingFetches(consumer)).IsEmpty();
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
    public async Task ConsumeOneAsync_NestedHeaderRoutersReuseParsedLookup()
    {
        var fetch = CreatePendingFetchData(
            CreateRecord(
                0,
                "key",
                "payload",
                new Header("event-family", "domain"u8.ToArray()),
                new Header("event-type", "created"u8.ToArray())));
        var inner = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("inner-fallback"),
            new HeaderDeserializerRoute<string>(
                "created"u8.ToArray(),
                new PrefixDeserializer("created")));
        var outer = new HeaderRoutingDeserializer<string>(
            "event-family",
            new PrefixDeserializer("outer-fallback"),
            new HeaderDeserializerRoute<string>("domain"u8.ToArray(), inner));
        await using var consumer = CreateConsumer(fetch, null, Serializers.String, outer);

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
        await Assert.That(pending.ProvenOffset).IsEqualTo(0L);
    }

    [Test]
    public async Task ConsumeBatch_RejectedAfterAcceptedDoesNotProveAcceptedRecord()
    {
        using var pending = CreatePendingFetchData(
            CreateRecord(0, "accept", "one", new Header("route", "keep"u8.ToArray())),
            CreateRecord(1, "reject", "two", new Header("route", "drop"u8.ToArray())));
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            recordFilter: new HeaderValueFilter("route", "keep"u8.ToArray()));

        var results = batch.ToArray();

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(pending.LastYieldedOffset).IsEqualTo(1L);
        await Assert.That(pending.ProvenOffset).IsEqualTo(-1L);
    }

    [Test]
    public async Task ConsumeBatch_MaxPollRecordsCountsRejectedRecords()
    {
        using var pending = CreatePendingFetchData(
            CreateRecord(0, "reject", "one", new Header("route", "drop"u8.ToArray())),
            CreateRecord(1, "reject", "two", new Header("route", "drop"u8.ToArray())),
            CreateRecord(2, "accept", "three", new Header("route", "keep"u8.ToArray())));
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        var storedOffset = -1L;
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            storeOffsetOnDelivery: (_, offset, _) => storedOffset = offset,
            maxRecords: 2,
            recordFilter: filter);

        var results = batch.ToArray();

        await Assert.That(results).IsEmpty();
        await Assert.That(filter.CallCount).IsEqualTo(2);
        await Assert.That(storedOffset).IsEqualTo(2L);
    }

    [Test]
    public async Task ConsumeBatch_RejectedDrainReturnsControlAfterBoundedWork()
    {
        using var pending = CreatePollRefreshRecords();
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        var firstBatch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            recordFilter: filter);

        var firstResults = firstBatch.ToArray();

        await Assert.That(firstResults).IsEmpty();
        await Assert.That(filter.CallCount).IsEqualTo(64);

        var secondBatch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            recordFilter: filter);

        var result = secondBatch.Single();

        await Assert.That(result.Offset).IsEqualTo(65L);
        await Assert.That(filter.CallCount).IsEqualTo(66);
    }

    [Test]
    public async Task ConsumeBatch_RejectedDrainContinuesAfterFastPollRefresh()
    {
        using var pending = CreatePollRefreshRecords();
        var filter = new HeaderValueFilter("route", "keep"u8.ToArray());
        var refreshCount = 0;
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            recordFilter: filter,
            tryRecordPollFast: () =>
            {
                refreshCount++;
                return true;
            });

        var result = batch.Single();

        await Assert.That(result.Offset).IsEqualTo(65L);
        await Assert.That(filter.CallCount).IsEqualTo(66);
        await Assert.That(refreshCount).IsEqualTo(1);
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

    private static PendingFetchData CreatePollRefreshRecords()
    {
        var records = new Record[66];
        for (var offset = 0; offset < records.Length - 1; offset++)
        {
            records[offset] = CreateRecord(
                offset,
                "reject",
                "value",
                new Header("route", "drop"u8.ToArray()));
        }

        records[^1] = CreateRecord(
            records.Length - 1,
            "accept",
            "value",
            new Header("route", "keep"u8.ToArray()));
        return CreatePendingFetchData(records);
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

    private sealed class CancellingFilter(CancellationTokenSource cancellation) : IConsumerRecordFilter
    {
        public int CallCount { get; private set; }

        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context)
        {
            CallCount++;
            cancellation.Cancel();
            return false;
        }
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
