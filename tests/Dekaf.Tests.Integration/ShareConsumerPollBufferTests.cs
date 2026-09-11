using System.Buffers.Binary;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerPollBufferTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    [Arguments(false, false, true)]
    [Arguments(true, false, true)]
    public async Task OversizedBatches_DeliverEveryRecordWithoutReacquisition(
        bool prepared, bool restartEnumeration, bool useDefaultPollLimit)
    {
        var messageCount = useDefaultPollLimit ? 2048 : 256;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"share-buffer-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<int, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMinutes(1)).WithBatchSize(1024 * 1024).BuildAsync();
        var consumerBuilder = Kafka.CreateShareConsumer<int, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(groupId)
            .WithValueDeserializer(prepared ? new ReadyBorrowedDeserializer() : Serializers.RawBytes)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit);
        if (!useDefaultPollLimit)
            consumerBuilder.WithMaxPollRecords(2);
        await using var consumer = await consumerBuilder.BuildAsync();
        consumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var received = new HashSet<int>();
        await using (var poll = consumer.PollAsync(timeout.Token).GetAsyncEnumerator())
        {
            var firstMove = poll.MoveNextAsync().AsTask();
            try
            {
                // Keep the first poll active while the group initializes its start offsets.
                // Cancelling an empty priming fetch can leave an acquisition in flight (#3245).
                await WaitForConditionAsync(
                    () => consumer.Assignment.Count == 2 && consumer.AcquisitionLockTimeoutMs is > 0,
                    TimeSpan.FromSeconds(15), description: "both partitions assigned and the first ShareFetch response received");

                for (var index = 0; index < messageCount; index++)
                {
                    var payload = new byte[32];
                    payload.AsSpan().Fill((byte)index);
                    BinaryPrimitives.WriteInt32LittleEndian(payload, index);
                    await producer.FireAsync(new ProducerMessage<int, byte[]>
                    {
                        Topic = topic, Partition = index % 2, Key = index, Value = payload,
                        Headers = Headers.Create("identity", payload)
                    });
                }
                // Flush the accumulated batches explicitly; no timer determines their size.
                await producer.FlushAsync(timeout.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                await Assert.That(await firstMove).IsTrue();
                await ValidateRecordAsync(poll.Current, received);
                consumer.Acknowledge(poll.Current);
                if (restartEnumeration)
                {
                    await poll.DisposeAsync();
                    for (var index = 1; index < messageCount; index++)
                    {
                        await using var next = consumer.PollAsync(timeout.Token).GetAsyncEnumerator();
                        await Assert.That(await next.MoveNextAsync()).IsTrue();
                        await ValidateRecordAsync(next.Current, received);
                        consumer.Acknowledge(next.Current);
                    }
                }
                else
                {
                    for (var index = 1; index < messageCount; index++)
                    {
                        await Assert.That(await poll.MoveNextAsync()).IsTrue();
                        await ValidateRecordAsync(poll.Current, received);
                        consumer.Acknowledge(poll.Current);
                    }
                }
            }
            finally
            {
                if (!firstMove.IsCompleted)
                {
                    await timeout.CancelAsync();
                    try { await firstMove; }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
                }
            }
        }
        await Assert.That(received.Count).IsEqualTo(messageCount);
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.CommitAsync(shutdown.Token);
        await consumer.CloseAsync(shutdown.Token);
    }

    private static async Task ValidateRecordAsync(ShareConsumeResult<int, ReadOnlyMemory<byte>> record, HashSet<int> received)
    {
        await Assert.That(received.Add(record.Key)).IsTrue();
        await Assert.That(record.DeliveryCount).IsEqualTo(1);
        await Assert.That(record.Value.Length).IsEqualTo(32);
        await Assert.That(BinaryPrimitives.ReadInt32LittleEndian(record.Value.Span)).IsEqualTo(record.Key);
        await Assert.That(record.Value.Span[sizeof(int)..].IndexOfAnyExcept((byte)record.Key)).IsEqualTo(-1);
        var header = record.Headers.Single(static header => header.Key == "identity");
        await Assert.That(header.Value.Span.SequenceEqual(record.Value.Span)).IsTrue();
    }

    private sealed class ReadyBorrowedDeserializer : IDeserializer<ReadOnlyMemory<byte>>, IAsyncDeserializerPreparer<ReadOnlyMemory<byte>>
    {
        public ReadOnlyMemory<byte> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => data;
        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out ReadOnlyMemory<byte> value)
        {
            value = data;
            return true;
        }
        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The borrowed deserializer is always prepared.");
    }
}
