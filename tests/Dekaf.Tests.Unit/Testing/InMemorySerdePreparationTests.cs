using System.Buffers;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;
using Dekaf.Testing;
using Dekaf.Tests.Unit.SchemaRegistry;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemorySerdePreparationTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Producer_PreparesColdComponent_IncludingMixedAndTransactionalPaths(bool prepareKey, bool transactional)
    {
        var cluster = new InMemoryKafkaCluster();
        var serializer = new PreparingSerializer();
        var asyncSerde = new AsyncStringSerde();
        await using var producer = prepareKey
            ? new InMemoryProducer<string, string>(cluster, serializer, asyncSerde)
            : new InMemoryProducer<string, string>(cluster, asyncSerde, serializer);
        var message = new ProducerMessage<string, string>
        {
            Topic = "prepared", Key = "key", Value = "value", Headers = Headers.Create("trace", "123")
        };

        if (transactional)
        {
            await producer.InitTransactionsAsync();
            await using var transaction = producer.BeginTransaction();
            await transaction.ProduceAsync(message);
            await transaction.CommitAsync();
        }
        else
        {
            await producer.ProduceAsync(message);
        }

        var record = cluster.ReadRecords("prepared")[0];
        await Assert.That(Encoding.UTF8.GetString(record.Key)).IsEqualTo("key");
        await Assert.That(Encoding.UTF8.GetString(record.Value)).IsEqualTo("value");
        await Assert.That(serializer.PrepareCalls).IsEqualTo(1);
        await Assert.That(serializer.LastContext.Topic).IsEqualTo("prepared");
        await Assert.That(serializer.LastContext.Component)
            .IsEqualTo(prepareKey ? SerializationComponent.Key : SerializationComponent.Value);
        await Assert.That(serializer.LastContext.Headers![0].GetValueAsString()).IsEqualTo("123");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Producer_UsesExactPreparationAdmission(bool emptyAdmission)
    {
        var cluster = new InMemoryKafkaCluster();
        var serializer = new AdmissionSerializer(emptyAdmission);
        await using var producer = new InMemoryProducer<string, string>(cluster, serializer, serializer);
        await producer.ProduceAsync("prepared", "key", "value");

        await Assert.That(serializer.AdmissionCalls).IsEqualTo(2);
        await Assert.That(serializer.PreparedSerializeCalls).IsEqualTo(emptyAdmission ? 0 : 2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Producer_CancelDuringPreparation_NeverAppends(bool transactional)
    {
        var cluster = new InMemoryKafkaCluster();
        var serializer = new PreparingSerializer { Block = true };
        await using var producer = new InMemoryProducer<string, string>(cluster, Serializers.String, serializer);
        using var cancellation = new CancellationTokenSource();
        if (transactional)
            await producer.InitTransactionsAsync();
        await using var transaction = transactional ? producer.BeginTransaction() : null;
        var pending = transactional
            ? transaction!.ProduceAsync("prepared", "key", "value", cancellation.Token).AsTask()
            : producer.ProduceAsync("prepared", "key", "value", cancellation.Token).AsTask();

        await serializer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancellation.CancelAsync();
        // Deliberately ignore cancellation in the preparer. The producer must check before serialization/append.
        serializer.Release.TrySetResult();
        await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        await Assert.That(serializer.SerializeCalls).IsEqualTo(0);
        await Assert.That(serializer.LastCancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(cluster.ReadRecords("prepared")).IsEmpty();
    }

    [Test]
    public async Task Producer_NullComponents_SkipPreparation()
    {
        var cluster = new InMemoryKafkaCluster();
        var serializer = new PreparingSerializer();
        await using var producer = new InMemoryProducer<string, string>(cluster, serializer, serializer);
        await producer.ProduceAsync("prepared", null, null!);

        await Assert.That(serializer.PrepareCalls).IsEqualTo(0);
        await Assert.That(serializer.SerializeCalls).IsEqualTo(0);
        await Assert.That(cluster.ReadRecords("prepared")[0].IsValueNull).IsTrue();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Consumers_PrepareColdThenUseWarmResult_WithMixedComponents(bool share, bool prepareKey)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var message = new ProducerMessage<string, string>
        {
            Topic = "prepared", Key = "key", Value = "value", Headers = Headers.Create("trace", "123")
        };
        await producer.ProduceAsync(message);
        await producer.ProduceAsync(message);
        var deserializer = new PreparingDeserializer();
        var asyncSerde = new AsyncStringSerde();
        if (share)
        {
            await using var consumer = prepareKey
                ? new InMemoryShareConsumer<string, string>(cluster, deserializer, asyncSerde, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest })
                : new InMemoryShareConsumer<string, string>(cluster, asyncSerde, deserializer, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
            consumer.Subscribe("prepared");
            var count = 0;
            await foreach (var result in consumer.PollAsync())
            {
                await Assert.That(result.Key).IsEqualTo("key");
                await Assert.That(result.Value).IsEqualTo("value");
                count++;
            }
            await Assert.That(count).IsEqualTo(2);
        }
        else
        {
            await using var consumer = prepareKey
                ? new InMemoryConsumer<string, string>(cluster, deserializer, asyncSerde, ConsumerOptions())
                : new InMemoryConsumer<string, string>(cluster, asyncSerde, deserializer, ConsumerOptions());
            consumer.Subscribe("prepared");
            await using var records = consumer.ConsumeAsync().GetAsyncEnumerator();
            for (var index = 0; index < 2; index++)
            {
                await Assert.That(await records.MoveNextAsync()).IsTrue();
                await Assert.That(records.Current.Key).IsEqualTo("key");
                await Assert.That(records.Current.Value).IsEqualTo("value");
            }
        }

        await Assert.That(deserializer.PrepareCalls).IsEqualTo(1);
        await Assert.That(deserializer.LastContext.Topic).IsEqualTo("prepared");
        await Assert.That(deserializer.LastContext.Component)
            .IsEqualTo(prepareKey ? SerializationComponent.Key : SerializationComponent.Value);
        await Assert.That(deserializer.LastContext.Headers![0].GetValueAsString()).IsEqualTo("123");
        await Assert.That(Encoding.UTF8.GetString(deserializer.LastContext.KeyData.Span))
            .IsEqualTo(prepareKey ? "" : "key");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Consumers_CancelPreparation_RedeliverRecord(bool share)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("prepared", "key", "value");
        var deserializer = new PreparingDeserializer { Block = true };
        using var cancellation = new CancellationTokenSource();

        if (share)
        {
            await using var consumer = new InMemoryShareConsumer<string, string>(cluster, Serializers.String, deserializer, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
            consumer.Subscribe("prepared");
            await using (var records = consumer.PollAsync(cancellation.Token).GetAsyncEnumerator())
            {
                var pending = records.MoveNextAsync().AsTask();
                await deserializer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await cancellation.CancelAsync();
                deserializer.Release.TrySetResult();
                await Assert.That(async () => await pending).Throws<OperationCanceledException>();
                await Assert.That(deserializer.LastCancellationToken).IsEqualTo(cancellation.Token);
            }
            await using var retry = consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(await retry.MoveNextAsync()).IsTrue();
            await Assert.That(retry.Current.Offset).IsEqualTo(0);
        }
        else
        {
            await using var consumer = new InMemoryConsumer<string, string>(cluster, Serializers.String, deserializer, ConsumerOptions());
            consumer.Subscribe("prepared");
            var pending = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellation.Token).AsTask();
            await deserializer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            deserializer.Release.TrySetResult();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
            await Assert.That(deserializer.LastCancellationToken).IsEqualTo(cancellation.Token);
            var retry = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
            await Assert.That(retry!.Value.Offset).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Consumers_NullKeySkipsPreparation_NullValuePreservesContext(bool share)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("prepared", null, null!);
        var key = new PreparingDeserializer();
        var value = new PreparingDeserializer();
        if (share)
        {
            await using var consumer = new InMemoryShareConsumer<string, string>(cluster, key, value, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
            consumer.Subscribe("prepared");
            await using var records = consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(await records.MoveNextAsync()).IsTrue();
            await Assert.That(records.Current.Key).IsNull();
        }
        else
        {
            await using var consumer = new InMemoryConsumer<string, string>(cluster, key, value, ConsumerOptions());
            consumer.Subscribe("prepared");
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
            await Assert.That(result!.Value.Key).IsNull();
        }
        await Assert.That(key.PrepareCalls).IsEqualTo(0);
        await Assert.That(value.PrepareCalls).IsEqualTo(1);
        await Assert.That(value.LastContext.IsNull).IsTrue();
        await Assert.That(value.LastContext.KeyData.IsEmpty).IsTrue();
        await Assert.That(value.LastData.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Producer_ColdJsonAsyncSubject_RoundTripsWithoutManualWarmUp()
    {
        var cluster = new InMemoryKafkaCluster();
        using var registry = new MockSchemaRegistryClient();
        var strategy = new AsyncSubjectStrategy();
        await using var serializer = new JsonSchemaRegistrySerializer<int>(registry, strategy, "{\"type\":\"integer\"}");
        await using var deserializer = new JsonSchemaRegistryDeserializer<int>(registry);
        await using var producer = new InMemoryProducer<string, int>(cluster, Serializers.String, serializer);
        await using var consumer = new InMemoryConsumer<string, int>(cluster, Serializers.String, deserializer, ConsumerOptions());
        await producer.ProduceAsync("prepared", "key", 42);
        consumer.Subscribe("prepared");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
        await Assert.That(result!.Value.Value).IsEqualTo(42);
        await Assert.That(strategy.Calls).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Consumers_ColdJsonGuidHeader_PrepareSchema(bool share)
    {
        var cluster = new InMemoryKafkaCluster();
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("prepared-value", new Schema
        {
            SchemaType = SchemaType.Json, SchemaString = "{\"type\":\"integer\"}"
        });
        await using var deserializer = new JsonSchemaRegistryDeserializer<int>(
            registry, jsonOptions: null,
            new SchemaRegistryDeserializerConfig { SchemaIdStrategy = SchemaIdDeserializerStrategy.Header });
        await using var producer = new InMemoryProducer<string, byte[]>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, byte[]>
        {
            Topic = "prepared", Key = "key", Value = "42"u8.ToArray(),
            Headers = new Headers(1).Add(new Header(SchemaIdentityHeaderNames.Value,
                SchemaIdentityFraming.CreateSchemaGuidFrame(new Guid(schemaId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))))
        });

        if (share)
        {
            await using var consumer = new InMemoryShareConsumer<string, int>(cluster, Serializers.String, deserializer, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
            consumer.Subscribe("prepared");
            await using var records = consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(await records.MoveNextAsync()).IsTrue();
            await Assert.That(records.Current.Value).IsEqualTo(42);
        }
        else
        {
            await using var consumer = new InMemoryConsumer<string, int>(cluster, Serializers.String, deserializer, ConsumerOptions());
            consumer.Subscribe("prepared");
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
            await Assert.That(result!.Value.Value).IsEqualTo(42);
        }
    }

    [Test]
    [Arguments(false, 2)]
    [Arguments(true, 2)]
    [Arguments(false, 3)]
    [Arguments(true, 3)]
    public async Task Consumers_BoundPreparationRetries_KeepFailedRecord(bool share, int requiredPreparations)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("prepared", "key", "value");
        var deserializer = new PreparingDeserializer { RequiredPreparations = requiredPreparations };
        if (share)
        {
            await using var consumer = new InMemoryShareConsumer<string, string>(cluster, Serializers.String, deserializer, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
            consumer.Subscribe("prepared");
            await using (var records = consumer.PollAsync().GetAsyncEnumerator())
            {
                if (requiredPreparations > 2)
                    await Assert.That(async () => await records.MoveNextAsync()).Throws<InvalidOperationException>();
                else
                    await Assert.That(await records.MoveNextAsync()).IsTrue();
            }
            await Assert.That(deserializer.PrepareCalls).IsEqualTo(2);
            if (requiredPreparations > 2)
            {
                await using var retry = consumer.PollAsync().GetAsyncEnumerator();
                await Assert.That(await retry.MoveNextAsync()).IsTrue();
                await Assert.That(retry.Current.Offset).IsEqualTo(0);
            }
        }
        else
        {
            await using var consumer = new InMemoryConsumer<string, string>(cluster, Serializers.String, deserializer, ConsumerOptions());
            consumer.Subscribe("prepared");
            if (requiredPreparations > 2)
            {
                var exception = await Assert.That(async () => await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1)))
                    .Throws<RecordDeserializationException>();
                await Assert.That(exception!.Origin).IsEqualTo(DeserializationExceptionOrigin.Value);
            }
            else
            {
                await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1))).IsNotNull();
            }
            await Assert.That(deserializer.PrepareCalls).IsEqualTo(2);
            if (requiredPreparations > 2)
            {
                var retry = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
                await Assert.That(retry!.Value.Offset).IsEqualTo(0);
            }
        }
    }

    private sealed class AsyncSubjectStrategy : IAsyncSubjectNameStrategy
    {
        public int Calls { get; private set; }

        public async ValueTask<string> GetSubjectNameAsync(
            string topic, string? recordType, bool isKey, CancellationToken cancellationToken = default)
        {
            Calls++;
            await Task.Yield();
            return topic + (isKey ? "-key" : "-value");
        }
    }

    private static InMemoryConsumerOptions ConsumerOptions() => new() { AutoOffsetReset = AutoOffsetReset.Earliest };

    private sealed class PreparingSerializer : ISerializer<string>, IAsyncSerializerPreparer<string>
    {
        public bool Block { get; init; }
        public int PrepareCalls { get; private set; }
        public int SerializeCalls { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public SerializationContext LastContext { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _prepared;

        public async ValueTask PrepareAsync(string value, SerializationContext context, CancellationToken cancellationToken = default)
        {
            PrepareCalls++;
            LastContext = context;
            LastCancellationToken = cancellationToken;
            Entered.TrySetResult();
            if (Block)
                await Release.Task;
            _prepared = true;
        }

        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        {
            if (!_prepared)
                throw new InvalidOperationException("Serialize ran before preparation.");
            SerializeCalls++;
            Serializers.String.Serialize(value, ref destination, context);
        }
    }

    private sealed class AdmissionSerializer(bool emptyAdmission) : ISerializer<string>, IAsyncSerializerPreparationAdmission<string>
    {
        public int AdmissionCalls { get; private set; }
        public int PreparedSerializeCalls { get; private set; }

        public ValueTask PrepareAsync(string value, SerializationContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Exact admission required.");

        public ValueTask<SerializerPreparationAdmission> PrepareForSerializationAsync(
            string value, SerializationContext context, CancellationToken cancellationToken = default)
        {
            AdmissionCalls++;
            return new(emptyAdmission ? default : new SerializerPreparationAdmission(value, AdmissionCalls, value));
        }

        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        {
            if (!emptyAdmission)
                throw new InvalidOperationException("Exact admission required.");
            Serializers.String.Serialize(value, ref destination, context);
        }

        public void SerializePrepared<TWriter>(string value, ref TWriter destination,
            SerializationContext context, in SerializerPreparationAdmission admission)
            where TWriter : IBufferWriter<byte>
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        {
            if (admission.Subject != value || !Equals(admission.Schema, value))
                throw new InvalidOperationException("Wrong admission.");
            PreparedSerializeCalls++;
            Serializers.String.Serialize(value, ref destination, context);
        }
    }

    private sealed class PreparingDeserializer : IDeserializer<string>, IAsyncDeserializerPreparer<string>, IRecordHeaderDeserializer
    {
        public bool ConsumesRecordHeaders => true;
        public int RequiredPreparations { get; init; } = 1;
        public bool Block { get; init; }
        public int PrepareCalls { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public SerializationContext LastContext { get; private set; }
        public ReadOnlyMemory<byte> LastData { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _prepared;

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            LastContext = context;
            LastData = data;
            value = _prepared ? Serializers.String.Deserialize(data, context) : "";
            return _prepared;
        }

        public async ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context, CancellationToken cancellationToken = default)
        {
            PrepareCalls++;
            LastCancellationToken = cancellationToken;
            Entered.TrySetResult();
            if (Block)
                await Release.Task;
            _prepared = PrepareCalls >= RequiredPreparations;
        }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidOperationException("Use TryDeserialize to preserve prepared result.");
    }

    private sealed class AsyncStringSerde : IAsyncSerializer<string>, IAsyncDeserializer<string>
    {
        public ValueTask SerializeAsync(string value, IBufferWriter<byte> destination,
            SerializationContext context, CancellationToken cancellationToken = default)
        {
            Serializers.String.Serialize(value, ref destination, context);
            return default;
        }

        public ValueTask<string> DeserializeAsync(ReadOnlyMemory<byte> data,
            SerializationContext context, CancellationToken cancellationToken = default) =>
            new(Serializers.String.Deserialize(data, context));
    }
}
