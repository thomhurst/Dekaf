using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// A share-partition acquires whole log slices, including transaction markers. The client
/// must not deliver control records and must acknowledge their offsets as gaps, or the
/// share-partition start offset cannot move past them (#3725).
/// </summary>
[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerTransactionalTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    private static readonly string[] FirstValues = ["a", "b", "c", "d"];
    private static readonly string[] ExpectedValues = ["a", "b", "c", "d", "e"];

    [Test]
    [Arguments(false, ShareAcknowledgementMode.Implicit, false)]
    [Arguments(false, ShareAcknowledgementMode.Explicit, false)]
    [Arguments(true, ShareAcknowledgementMode.Implicit, false)]
    [Arguments(true, ShareAcknowledgementMode.Explicit, false)]
    [Arguments(false, ShareAcknowledgementMode.Implicit, true)]
    [Arguments(false, ShareAcknowledgementMode.Explicit, true)]
    public async Task ShareConsumer_TransactionalTopic_DeliversOnlyDataAndAdvancesPastMarkers(
        bool batches, ShareAcknowledgementMode mode, bool prepared, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-txn-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"share-txn-{Guid.NewGuid():N}").WithAcks(Acks.All)
            .BuildAsync(cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);
        // Data at offsets 0-1 and 3-4, COMMIT markers at offsets 2 and 5.
        await ProduceTransactionAsync(producer, topic, ["a", "b"], cancellationToken);
        await ProduceTransactionAsync(producer, topic, ["c", "d"], cancellationToken);

        // Produce before polling and read from the earliest offset, so no primed poll can
        // strand the records behind an acquisition lock.
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest).WithAcknowledgementMode(mode)
            .WithValueDeserializer(prepared ? new PreparingStringDeserializer() : Serializers.String)
            .WithMaxPollRecords(1)
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);
        var received = new ConcurrentQueue<string>();
        using var polling = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var poll = batches
            ? PollBatchesAsync(consumer, mode, received, polling.Token)
            : PollAsync(consumer, mode, received, polling.Token);

        await using var admin = KafkaContainer.CreateAdminClient();
        var partition = new TopicPartition(topic, 0);
        try
        {
            await TestWait.WaitForConditionAsync(() => received.Count >= FirstValues.Length, TimeSpan.FromSeconds(30),
                description: "data records of both transactions are delivered");
            // The broker persists the start offset it had before applying each acknowledgement,
            // so the advance past the second marker becomes visible with the next acknowledgement.
            await ProduceTransactionAsync(producer, topic, ["e"], cancellationToken);
            await TestWait.WaitForConditionAsync(
                async () => (await admin.DescribeShareGroupOffsetsAsync(group, [partition], cancellationToken))
                    .Single().StartOffset,
                static startOffset => startOffset >= 6,
                maxRetries: 12,
                description: "share-partition start offset advances past both COMMIT markers",
                formatObserved: static startOffset => $"start offset {startOffset}");
        }
        finally
        {
            await polling.CancelAsync();
            await poll;
        }

        await Assert.That(received.Order()).IsEquivalentTo(ExpectedValues);
    }

    private sealed class PreparingStringDeserializer : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        private bool _prepared;

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
            => Serializers.String.Deserialize(data, context);

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            value = _prepared ? Deserialize(data, context) : string.Empty;
            return _prepared;
        }

        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            _prepared = true;
            return ValueTask.CompletedTask;
        }
    }

    private static async Task ProduceTransactionAsync(IKafkaProducer<string, string> producer, string topic,
        string[] values, CancellationToken cancellationToken)
    {
        await using var transaction = producer.BeginTransaction();
        foreach (var value in values)
        {
            await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Partition = 0, Key = value, Value = value
            }, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task PollAsync(IKafkaShareConsumer<string, string> consumer, ShareAcknowledgementMode mode,
        ConcurrentQueue<string> received, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var record in consumer.PollAsync(cancellationToken))
            {
                received.Enqueue(record.Value);
                if (mode == ShareAcknowledgementMode.Explicit)
                {
                    consumer.Acknowledge(record);
                    await consumer.CommitAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task PollBatchesAsync(IKafkaShareConsumer<string, string> consumer,
        ShareAcknowledgementMode mode, ConcurrentQueue<string> received, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var batch in consumer.PollBatchesAsync(cancellationToken))
            {
                foreach (var record in batch)
                {
                    received.Enqueue(record.Value);
                    if (mode == ShareAcknowledgementMode.Explicit)
                        batch.Acknowledge(record);
                }
                if (mode == ShareAcknowledgementMode.Explicit)
                    await consumer.CommitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
