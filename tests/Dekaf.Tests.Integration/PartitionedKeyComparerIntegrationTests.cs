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

    private async Task VerifyOrderingAsync<TKey>(
        IDeserializer<TKey> deserializer, IEqualityComparer<TKey>? comparer, bool batches)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var producer = await Kafka.CreateProducer<byte[], string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        await producer.ProduceAsync(topic, [1], "first");
        await producer.ProduceAsync(topic, [1], "equal");
        await producer.ProduceAsync(topic, [2], "different");
        await using var consumer = await Kafka.CreateConsumer<TKey, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeyDeserializer(deserializer)
            .WithValueDeserializer(Serializers.String)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(1)
            .BuildAsync();
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
