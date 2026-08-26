using System.Buffers;
using System.Text;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

/// <summary>
/// Covers <see cref="IAsyncSerializer{T}"/>/<see cref="IAsyncDeserializer{T}"/> support in the
/// in-memory test doubles, including mixed sync/async configurations (issue #2518).
/// </summary>
public sealed class InMemoryAsyncSerdeTests
{
    [Test]
    public async Task Producer_AsyncSerializers_RoundTripThroughAsyncDeserializers()
    {
        var cluster = new InMemoryKafkaCluster();
        var keySerde = new AsyncPrefixSerde("key:");
        var valueSerde = new AsyncPrefixSerde("value:");
        var producer = new InMemoryProducer<string, string>(cluster, keySerde, valueSerde);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            keySerde,
            valueSerde,
            new InMemoryConsumerOptions
            {
                GroupId = "async-workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Key = "order-1",
            Value = "created",
            Headers = Headers.Create("trace-id", "abc")
        });
        consumer.Subscribe("orders");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        // The stored bytes carry the prefix, proving the asynchronous serializer produced them.
        var stored = cluster.ReadRecords("orders")[0];
        await Assert.That(Encoding.UTF8.GetString(stored.Key)).IsEqualTo("key:order-1");
        await Assert.That(Encoding.UTF8.GetString(stored.Value)).IsEqualTo("value:created");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("order-1");
        await Assert.That(result.Value.Value).IsEqualTo("created");
        await Assert.That(result.Value.Headers.Single().GetValueAsString()).IsEqualTo("abc");
        await Assert.That(keySerde.SerializeCalls).IsEqualTo(1);
        await Assert.That(valueSerde.SerializeCalls).IsEqualTo(1);
        await Assert.That(keySerde.DeserializeCalls).IsEqualTo(1);
        await Assert.That(valueSerde.DeserializeCalls).IsEqualTo(1);
        await Assert.That(keySerde.LastSerializeContext.Component).IsEqualTo(SerializationComponent.Key);
        await Assert.That(valueSerde.LastSerializeContext.Component).IsEqualTo(SerializationComponent.Value);
        await Assert.That(valueSerde.LastSerializeContext.Headers!.Single().GetValueAsString()).IsEqualTo("abc");
    }

    [Test]
    public async Task Producer_SyncKeyAsyncValue_EncodesEachComponentWithItsOwnSerializer()
    {
        var cluster = new InMemoryKafkaCluster();
        var valueSerde = new AsyncPrefixSerde("value:");
        var producer = new InMemoryProducer<string, string>(cluster, Serializers.String, valueSerde);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            Serializers.String,
            valueSerde,
            new InMemoryConsumerOptions
            {
                GroupId = "mixed-workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync("orders", "order-1", "created");
        consumer.Subscribe("orders");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        var stored = cluster.ReadRecords("orders")[0];
        await Assert.That(Encoding.UTF8.GetString(stored.Key)).IsEqualTo("order-1");
        await Assert.That(Encoding.UTF8.GetString(stored.Value)).IsEqualTo("value:created");
        await Assert.That(result!.Value.Key).IsEqualTo("order-1");
        await Assert.That(result.Value.Value).IsEqualTo("created");
    }

    [Test]
    public async Task Producer_AsyncKeySyncValue_EncodesEachComponentWithItsOwnSerializer()
    {
        var cluster = new InMemoryKafkaCluster();
        var keySerde = new AsyncPrefixSerde("key:");
        var producer = new InMemoryProducer<string, string>(cluster, keySerde, Serializers.String);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            keySerde,
            Serializers.String,
            new InMemoryConsumerOptions
            {
                GroupId = "mixed-workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync("orders", "order-1", "created");
        consumer.Subscribe("orders");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        var stored = cluster.ReadRecords("orders")[0];
        await Assert.That(Encoding.UTF8.GetString(stored.Key)).IsEqualTo("key:order-1");
        await Assert.That(Encoding.UTF8.GetString(stored.Value)).IsEqualTo("created");
        await Assert.That(result!.Value.Key).IsEqualTo("order-1");
        await Assert.That(result.Value.Value).IsEqualTo("created");
    }

    [Test]
    public async Task AsyncSerdes_NullKeyAndValue_MatchSynchronousNullSemantics()
    {
        var cluster = new InMemoryKafkaCluster();
        var keySerde = new AsyncPrefixSerde("key:");
        var valueSerde = new AsyncPrefixSerde("value:");
        var producer = new InMemoryProducer<string, string>(cluster, keySerde, valueSerde);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            keySerde,
            valueSerde,
            new InMemoryConsumerOptions
            {
                GroupId = "null-workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync("orders", key: null, value: null!);
        consumer.Subscribe("orders");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        var stored = cluster.ReadRecords("orders")[0];
        await Assert.That(stored.IsKeyNull).IsTrue();
        await Assert.That(stored.IsValueNull).IsTrue();
        // Null components skip the serializer entirely, and a null key skips the deserializer —
        // the same contract the synchronous eager ConsumeResult constructor implements.
        await Assert.That(keySerde.SerializeCalls).IsEqualTo(0);
        await Assert.That(valueSerde.SerializeCalls).IsEqualTo(0);
        await Assert.That(keySerde.DeserializeCalls).IsEqualTo(0);
        await Assert.That(valueSerde.DeserializeCalls).IsEqualTo(1);
        await Assert.That(valueSerde.LastDeserializeContext.IsNull).IsTrue();
        await Assert.That(valueSerde.LastDeserializedLength).IsEqualTo(0);
        await Assert.That(result!.Value.Key).IsNull();
    }

    [Test]
    public async Task FireAsync_AsyncSerializers_DeliversRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var serde = new AsyncPrefixSerde("v:");
        var producer = new InMemoryProducer<string, string>(cluster, serde, serde);

        await producer.FireAsync("orders", "order-1", "created");
        await producer.FlushAsync();

        var stored = cluster.ReadRecords("orders")[0];
        await Assert.That(Encoding.UTF8.GetString(stored.Value)).IsEqualTo("v:created");
    }

    [Test]
    public async Task ProduceAsync_AsyncSerializer_ObservesCancellationToken()
    {
        var cluster = new InMemoryKafkaCluster();
        var serde = new AsyncPrefixSerde("v:");
        var producer = new InMemoryProducer<string, string>(cluster, serde, serde);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () => await producer.ProduceAsync("orders", "k", "v", cts.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(cluster.ReadRecords("orders")).IsEmpty();
    }

    [Test]
    public async Task AsyncDeserializer_Failure_SurfacesOriginAndLeavesOffsetForRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        var valueSerde = new AsyncPrefixSerde("value:") { FailDeserializeOnCall = 1 };
        var producer = new InMemoryProducer<string, string>(cluster, Serializers.String, valueSerde);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            Serializers.String,
            valueSerde,
            new InMemoryConsumerOptions
            {
                GroupId = "failing-workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync("orders", "order-1", "created");
        consumer.Subscribe("orders");

        var exception = await Assert.That(async () => await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1)))
            .Throws<RecordDeserializationException>();

        await Assert.That(exception!.Origin).IsEqualTo(DeserializationExceptionOrigin.Value);
        await Assert.That(exception.Offset).IsEqualTo(0);

        // The failed record kept its position, so the next consume redelivers it.
        var retry = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
        await Assert.That(retry!.Value.Offset).IsEqualTo(0);
        await Assert.That(retry.Value.Value).IsEqualTo("created");
    }

    [Test]
    public async Task AsyncDeserializers_AdvanceOffsetsAndCommit()
    {
        var cluster = new InMemoryKafkaCluster();
        var serde = new AsyncPrefixSerde("v:");
        var producer = new InMemoryProducer<string, string>(cluster, Serializers.String, serde);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            Serializers.String,
            serde,
            new InMemoryConsumerOptions
            {
                GroupId = "committing-workers",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        var admin = new InMemoryAdminClient(cluster);

        await producer.ProduceAsync("orders", "a", "one");
        await producer.ProduceAsync("orders", "b", "two");
        consumer.Subscribe("orders");

        var first = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
        var second = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
        await consumer.CommitAsync();

        var offsets = await admin.ListConsumerGroupOffsetsAsync("committing-workers");

        await Assert.That(first!.Value.Value).IsEqualTo("one");
        await Assert.That(second!.Value.Value).IsEqualTo("two");
        await Assert.That(offsets[new TopicPartition("orders", 0)]).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumeAsync_AsyncDeserializers_StreamsRecords()
    {
        var cluster = new InMemoryKafkaCluster();
        var serde = new AsyncPrefixSerde("v:");
        var producer = new InMemoryProducer<string, string>(cluster, Serializers.String, serde);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            Serializers.String,
            serde,
            new InMemoryConsumerOptions
            {
                GroupId = "streaming-workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync("orders", "a", "one");
        await producer.ProduceAsync("orders", "b", "two");
        consumer.Subscribe("orders");

        var values = new List<string>();
        using var cts = new CancellationTokenSource();
        await foreach (var record in consumer.ConsumeAsync(cts.Token))
        {
            values.Add(record.Value);
            if (values.Count == 2)
                await cts.CancelAsync();
        }

        await Assert.That(values).IsEquivalentTo(["one", "two"]);
    }

    [Test]
    public async Task Consumer_AsyncRecordHeaderDeserializerReceivesHeaders()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var deserializer = new AsyncHeaderCapturingDeserializer();
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            Serializers.String,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = "async-header-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Key = "key",
            Value = "value",
            Headers = Headers.Create("trace-id", "abc")
        });
        consumer.Subscribe("orders");

        _ = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(deserializer.Headers).IsNotNull();
        await Assert.That(deserializer.Headers!).Count().IsEqualTo(1);
        await Assert.That(deserializer.Headers![0].Key).IsEqualTo("trace-id");
    }

    [Test]
    public async Task Consumer_AsyncKeySuspensionPreservesSyncValueHeadersAcrossConsumers()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var firstKeyDeserializer = new BlockingAsyncDeserializer();
        var firstValueDeserializer = new HeaderValueCapturingDeserializer();
        var secondValueDeserializer = new HeaderValueCapturingDeserializer();
        await using var firstConsumer = new InMemoryConsumer<string, string>(
            cluster,
            firstKeyDeserializer,
            firstValueDeserializer,
            new InMemoryConsumerOptions
            {
                GroupId = "first-header-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        await using var secondConsumer = new InMemoryConsumer<string, string>(
            cluster,
            new CompletedAsyncDeserializer(),
            secondValueDeserializer,
            new InMemoryConsumerOptions
            {
                GroupId = "second-header-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "first-records",
            Key = "first-key",
            Value = "first-value",
            Headers = Headers.Create("record-id", "first")
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "second-records",
            Key = "second-key",
            Value = "second-value",
            Headers = Headers.Create("record-id", "second")
        });
        firstConsumer.Subscribe("first-records");
        secondConsumer.Subscribe("second-records");

        var firstConsume = firstConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(10));
        if (firstConsume.IsCompleted)
            throw new InvalidOperationException("The first key deserializer did not suspend.");

        try
        {
            var secondConsume = secondConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
            if (!secondConsume.IsCompletedSuccessfully)
                throw new InvalidOperationException("The second consume unexpectedly suspended.");
            _ = secondConsume.Result;
        }
        finally
        {
            firstKeyDeserializer.Release();
        }
        _ = await firstConsume;

        await Assert.That(firstValueDeserializer.HeaderValue).IsEqualTo("first");
        await Assert.That(secondValueDeserializer.HeaderValue).IsEqualTo("second");
    }

    [Test]
    public async Task ShareConsumer_AsyncDeserializers_RoundTripAndAcknowledge()
    {
        var cluster = new InMemoryKafkaCluster();
        var serde = new AsyncPrefixSerde("v:");
        var producer = new InMemoryProducer<string, string>(cluster, serde, serde);
        var shareConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            serde,
            serde,
            new InMemoryShareConsumerOptions { GroupId = "async-share" });
        var admin = new InMemoryAdminClient(cluster);

        await producer.ProduceAsync("shared", "k", "v");
        shareConsumer.Subscribe("shared");

        var record = await shareConsumer.PollAsync().FirstAsync();
        shareConsumer.Acknowledge(record);
        await shareConsumer.CommitAsync();

        var offsets = await admin.DescribeShareGroupOffsetsAsync("async-share");

        await Assert.That(record.Key).IsEqualTo("k");
        await Assert.That(record.Value).IsEqualTo("v");
        await Assert.That(Encoding.UTF8.GetString(serde.LastDeserializeContext.KeyData.Span)).IsEqualTo("v:k");
        await Assert.That(serde.LastDeserializeContext.IsKeyNull).IsFalse();
        await Assert.That(offsets.Single().StartOffset).IsEqualTo(1);
    }

    [Test]
    public async Task ShareConsumer_AsyncRecordHeaderDeserializerReceivesHeaders()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var deserializer = new AsyncHeaderCapturingDeserializer();
        var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            Serializers.String,
            deserializer,
            new InMemoryShareConsumerOptions { GroupId = "async-header-share" });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "shared",
            Key = "key",
            Value = "value",
            Headers = Headers.Create("trace-id", "abc")
        });
        consumer.Subscribe("shared");

        _ = await consumer.PollAsync().FirstAsync();

        await Assert.That(deserializer.Headers).IsNotNull();
        await Assert.That(deserializer.Headers!).Count().IsEqualTo(1);
        await Assert.That(deserializer.Headers![0].Key).IsEqualTo("trace-id");
    }

    [Test]
    public async Task ShareConsumer_SyncValueDeserializer_ReceivesRawKeyContext()
    {
        var cluster = new InMemoryKafkaCluster();
        var valueDeserializer = new CapturingStringDeserializer();
        var producer = new InMemoryProducer<string, string>(cluster);
        var shareConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            Serializers.String,
            valueDeserializer,
            new InMemoryShareConsumerOptions { GroupId = "sync-key-context" });

        await producer.ProduceAsync("shared", "key", "value");
        shareConsumer.Subscribe("shared");

        var record = await shareConsumer.PollAsync().FirstAsync();

        await Assert.That(record.Value).IsEqualTo("value");
        await Assert.That(Encoding.UTF8.GetString(valueDeserializer.Context.KeyData.Span)).IsEqualTo("key");
        await Assert.That(valueDeserializer.Context.IsKeyNull).IsFalse();
    }

    [Test]
    public async Task ShareConsumer_SyncKeyAsyncValue_RoundTrips()
    {
        var cluster = new InMemoryKafkaCluster();
        var valueSerde = new AsyncPrefixSerde("v:");
        var producer = new InMemoryProducer<string, string>(cluster, Serializers.String, valueSerde);
        var shareConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            Serializers.String,
            valueSerde,
            new InMemoryShareConsumerOptions { GroupId = "mixed-share" });

        await producer.ProduceAsync("shared", "k", "v");
        shareConsumer.Subscribe("shared");

        var record = await shareConsumer.PollAsync().FirstAsync();

        await Assert.That(record.Key).IsEqualTo("k");
        await Assert.That(record.Value).IsEqualTo("v");
    }

    [Test]
    public async Task ShareConsumer_AsyncDeserializerFailure_ReleasesRecordForOtherMembers()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var failing = new AsyncPrefixSerde(string.Empty) { FailDeserializeOnCall = 1 };
        var failingMember = new InMemoryShareConsumer<string, string>(
            cluster,
            Serializers.String,
            failing,
            new InMemoryShareConsumerOptions { GroupId = "share-failure", MemberId = "member-1" });
        var otherMember = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-failure", MemberId = "member-2" });

        await producer.ProduceAsync("shared", "k", "v");
        failingMember.Subscribe("shared");
        otherMember.Subscribe("shared");

        await Assert.That(async () => await failingMember.PollAsync().FirstAsync())
            .Throws<InvalidOperationException>();

        // The failed record never became a pending result, so nothing else can release its
        // lease — the poll path must release it or it is stranded for the rest of the run.
        var record = await otherMember.PollAsync().FirstAsync();

        await Assert.That(record.Value).IsEqualTo("v");
        await Assert.That(record.DeliveryCount).IsEqualTo(2);
    }

    [Test]
    public async Task ShareConsumer_CloseDuringAsyncDeserialization_ReleasesAcquiredRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var deserializer = new BlockingAsyncDeserializer();
        var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            Serializers.String,
            deserializer,
            new InMemoryShareConsumerOptions { GroupId = "share-close-race" });
        var admin = new InMemoryAdminClient(cluster);

        await producer.ProduceAsync("shared", "k", "v");
        consumer.Subscribe("shared");

        var poll = consumer.PollAsync().FirstAsync().AsTask();
        try
        {
            await deserializer.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            await consumer.CloseAsync();

            var activeDeletion = await admin.DeleteShareGroupsAsync(["share-close-race"]);
            await Assert.That(activeDeletion["share-close-race"].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        }
        finally
        {
            deserializer.Release();
        }

        await Assert.That(async () => await poll).Throws<ObjectDisposedException>();

        var deletion = await admin.DeleteShareGroupsAsync(["share-close-race"]);
        await Assert.That(deletion["share-close-race"].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task ShareConsumer_UnsubscribeDuringAsyncDeserializationRejectsStaleRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var deserializer = new BlockingAsyncDeserializer();
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            Serializers.String,
            deserializer,
            new InMemoryShareConsumerOptions { GroupId = "share-unsubscribe-race", MemberId = "first" });
        var admin = new InMemoryAdminClient(cluster);

        await producer.ProduceAsync("shared", "k", "v");
        consumer.Subscribe("shared");
        var poll = consumer.PollAsync().FirstAsync().AsTask();
        try
        {
            await deserializer.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            consumer.Unsubscribe();
        }
        finally
        {
            deserializer.Release();
        }

        await Assert.That(async () => await poll).Throws<InvalidOperationException>();
        await consumer.CommitAsync();
        await Assert.That(await admin.DescribeShareGroupOffsetsAsync("share-unsubscribe-race")).IsEmpty();

        await using var otherConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-unsubscribe-race", MemberId = "second" });
        otherConsumer.Subscribe("shared");
        var redelivery = await otherConsumer.PollAsync()
            .FirstAsync()
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(redelivery.Offset).IsEqualTo(0);
        await Assert.That(redelivery.DeliveryCount).IsEqualTo(2);
    }

    [Test]
    public async Task ShareConsumer_SyncDeserializerFailure_ReleasesRecordForOtherMembers()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var failingMember = new InMemoryShareConsumer<string, string>(
            cluster,
            Serializers.String,
            new ThrowingDeserializer(),
            new InMemoryShareConsumerOptions { GroupId = "share-sync-failure", MemberId = "member-1" });
        var otherMember = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-sync-failure", MemberId = "member-2" });

        await producer.ProduceAsync("shared", "k", "v");
        failingMember.Subscribe("shared");
        otherMember.Subscribe("shared");

        await Assert.That(async () => await failingMember.PollAsync().FirstAsync())
            .Throws<InvalidOperationException>();

        var record = await otherMember.PollAsync().FirstAsync();

        await Assert.That(record.Value).IsEqualTo("v");
        await Assert.That(record.DeliveryCount).IsEqualTo(2);
    }

    [Test]
    public async Task AsyncSerdeConstructors_RejectNullSerdes()
    {
        var cluster = new InMemoryKafkaCluster();

        await Assert.That(() => new InMemoryProducer<string, string>(
                cluster,
                (IAsyncSerializer<string>)null!,
                new AsyncPrefixSerde("v:")))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new InMemoryConsumer<string, string>(
                cluster,
                new AsyncPrefixSerde("v:"),
                (IAsyncDeserializer<string>)null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new InMemoryShareConsumer<string, string>(
                cluster,
                (IAsyncDeserializer<string>)null!,
                new AsyncPrefixSerde("v:"),
                new InMemoryShareConsumerOptions { GroupId = "g" }))
            .Throws<ArgumentNullException>();
    }

    private sealed class ThrowingDeserializer : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidOperationException("Deserialization failed.");
    }

    private sealed class CapturingStringDeserializer : IDeserializer<string>
    {
        public SerializationContext Context { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Context = context;
            return Encoding.UTF8.GetString(data.Span);
        }
    }

    private sealed class BlockingAsyncDeserializer : IAsyncDeserializer<string>
    {
        private readonly TaskCompletionSource _entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public async ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return Encoding.UTF8.GetString(data.Span);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class AsyncHeaderCapturingDeserializer :
        IAsyncDeserializer<string>,
        IRecordHeaderDeserializer
    {
        public bool ConsumesRecordHeaders => true;

        public Headers? Headers { get; private set; }

        public ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            Headers = context.Headers;
            return ValueTask.FromResult(Encoding.UTF8.GetString(data.Span));
        }
    }

    private sealed class CompletedAsyncDeserializer : IAsyncDeserializer<string>
    {
        public ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Encoding.UTF8.GetString(data.Span));
    }

    private sealed class HeaderValueCapturingDeserializer :
        IDeserializer<string>,
        IRecordHeaderDeserializer
    {
        public bool ConsumesRecordHeaders => true;

        public string? HeaderValue { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            HeaderValue = context.Headers?[0].GetValueAsString();
            return Encoding.UTF8.GetString(data.Span);
        }
    }

    /// <summary>
    /// Serde whose encode/decode genuinely suspends, so a caller that forgets to await it observes
    /// the wrong bytes. The prefix makes the asynchronous path visible on the wire.
    /// </summary>
    private sealed class AsyncPrefixSerde : IAsyncSerde<string>
    {
        private readonly string _prefix;

        public AsyncPrefixSerde(string prefix) => _prefix = prefix;

        public int SerializeCalls { get; private set; }
        public int DeserializeCalls { get; private set; }
        public int LastDeserializedLength { get; private set; }
        public SerializationContext LastSerializeContext { get; private set; }
        public SerializationContext LastDeserializeContext { get; private set; }

        /// <summary>1-based deserialize call that should throw, or 0 to never throw.</summary>
        public int FailDeserializeOnCall { get; init; }

        public async ValueTask SerializeAsync(
            string value,
            IBufferWriter<byte> destination,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            SerializeCalls++;
            LastSerializeContext = context;
            destination.Write(Encoding.UTF8.GetBytes(_prefix + value));
        }

        public async ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            DeserializeCalls++;
            LastDeserializeContext = context;
            LastDeserializedLength = data.Length;

            if (FailDeserializeOnCall == DeserializeCalls)
                throw new InvalidOperationException("Deserialization failed.");

            var text = Encoding.UTF8.GetString(data.Span);
            return text.StartsWith(_prefix, StringComparison.Ordinal) ? text[_prefix.Length..] : text;
        }
    }
}
