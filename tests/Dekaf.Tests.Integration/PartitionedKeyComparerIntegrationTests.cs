using Dekaf.Consumer;
using Dekaf.Serialization;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedKeyComparerIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public Task PublicHandlers_ByteArrayKeysUseContent(bool batches)
        => VerifyOrderingAsync(Serializers.ByteArray, null, batches);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public Task PublicHandlers_RawMemoryKeysUseContent(bool batches)
        => VerifyOrderingAsync(Serializers.RawBytes, null, batches);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public Task PublicHandlers_CustomReferenceKeysUseProvidedComparer(bool batches)
        => VerifyOrderingAsync(new CustomerKeyDeserializer(), new CustomerKeyComparer(), batches);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public Task PublicHandlers_NullAndEmptyRawMemoryKeysStayDistinct(bool batches)
        => VerifyOrderingAsync(Serializers.RawBytes, null, batches, nullAndEmpty: true);

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task AsyncConsumption_PreservesNullAndEmptyKeys(bool singleRecord, bool suspendDeserializer)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var producer = await Kafka.CreateProducer<byte[], string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await producer.ProduceAsync(topic, null, "null");
        await producer.ProduceAsync(topic, [], "empty");
        var started = NewSignal();
        var release = NewSignal();
        await using var consumer = await Kafka.CreateConsumer<ReadOnlyMemory<byte>, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeyDeserializer(new AsyncKeyDeserializer<ReadOnlyMemory<byte>>(Serializers.RawBytes,
                started, suspendDeserializer ? release.Task : Task.CompletedTask))
            .WithValueDeserializer(Serializers.String)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(1).BuildAsync();
        consumer.Assign([new TopicPartition(topic, 0)]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        async ValueTask<ConsumeResult<ReadOnlyMemory<byte>, string>?> NextAsync()
        {
            if (singleRecord)
                return await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), timeout.Token);
            return await records.MoveNextAsync() ? records.Current : null;
        }

        var nullKey = await NextAsync();
        await Assert.That(nullKey).IsNotNull();
        await Assert.That(nullKey!.Value.IsKeyNull).IsTrue();
        var pending = NextAsync().AsTask();
        ConsumeResult<ReadOnlyMemory<byte>, string>? emptyKey;
        try
        {
            await started.Task.WaitAsync(timeout.Token);
            if (suspendDeserializer)
                await Assert.That(pending.IsCompleted).IsFalse();
        }
        finally
        {
            release.TrySetResult();
            emptyKey = await pending;
        }
        await Assert.That(emptyKey).IsNotNull();
        await Assert.That(emptyKey!.Value.IsKeyNull).IsFalse();
        await Assert.That(nullKey.Value.Key.IsEmpty).IsTrue();
        await Assert.That(emptyKey.Value.Key.IsEmpty).IsTrue();
    }

    private async Task VerifyOrderingAsync<TKey>(
        IDeserializer<TKey> deserializer, IEqualityComparer<TKey>? comparer, bool batches,
        bool nullAndEmpty = false)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var producer = await Kafka.CreateProducer<byte[], string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        await producer.ProduceAsync(topic, nullAndEmpty ? null : [1], "first");
        await producer.ProduceAsync(topic, nullAndEmpty ? null : [1], "equal");
        await producer.ProduceAsync(topic, nullAndEmpty ? [] : [2], "different");
        var builder = Kafka.CreateConsumer<TKey, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeyDeserializer(deserializer)
            .WithValueDeserializer(Serializers.String)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(1);
        await using var consumer = await builder.BuildAsync();
        consumer.Assign([new TopicPartition(topic, 0)]);
        var firstStarted = NewSignal();
        var releaseFirst = NewSignal();
        var equalStarted = NewSignal();
        var differentStarted = NewSignal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new PartitionedProcessingOptions
        {
            Ordering = PartitionedProcessingOrder.Key,
            MaxConcurrentHandlersPerPartition = 2,
            MaxHandlerBatchSize = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged
        };

        async ValueTask Handle(ConsumeResult<TKey, string> record, CancellationToken cancellationToken)
        {
            switch (record.Offset)
            {
                case 0:
                    firstStarted.TrySetResult();
                    await releaseFirst.Task.WaitAsync(cancellationToken);
                    break;
                case 1:
                    equalStarted.TrySetResult();
                    break;
                case 2:
                    differentStarted.TrySetResult();
                    break;
            }
        }

        PartitionRecordProcessor<TKey, string> recordHandler = (_, record, token) => Handle(record, token);
        PartitionBatchProcessor<TKey, string> batchHandler = (_, records, token) => Handle(records[0], token);
        var processing = (batches, comparer) switch
        {
            (true, null) => consumer.RunPartitionedBatchesAsync(batchHandler, options, timeout.Token),
            (true, { } keyComparer) => consumer.RunPartitionedBatchesAsync(batchHandler, options, keyComparer, timeout.Token),
            (false, null) => consumer.RunPartitionedAsync(recordHandler, options, timeout.Token),
            (false, { } keyComparer) => consumer.RunPartitionedAsync(recordHandler, options, keyComparer, timeout.Token)
        };
        var running = processing.AsTask();
        try
        {
            await firstStarted.Task.WaitAsync(timeout.Token);
            await differentStarted.Task.WaitAsync(timeout.Token);
            await Assert.That(equalStarted.Task.IsCompleted).IsFalse();
            releaseFirst.TrySetResult();
            await equalStarted.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await timeout.CancelAsync();
            try { await running; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                await Assert.That(running.IsCanceled).IsTrue();
            }
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class AsyncKeyDeserializer<TKey>(IDeserializer<TKey> deserializer,
        TaskCompletionSource started, Task release) : IAsyncDeserializer<TKey>
    {
        public async ValueTask<TKey> DeserializeAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            started.TrySetResult();
            await release.WaitAsync(cancellationToken);
            return deserializer.Deserialize(data, context);
        }
    }

    private sealed class CustomerKey(byte id)
    {
        public byte Id { get; } = id;
    }

    private sealed class CustomerKeyDeserializer : IDeserializer<CustomerKey>
    {
        public CustomerKey Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => new(data.Span[0]);
    }

    private sealed class CustomerKeyComparer : IEqualityComparer<CustomerKey>
    {
        public bool Equals(CustomerKey? x, CustomerKey? y) => x!.Id == y!.Id;
        public int GetHashCode(CustomerKey obj) => obj.Id;
    }
}
