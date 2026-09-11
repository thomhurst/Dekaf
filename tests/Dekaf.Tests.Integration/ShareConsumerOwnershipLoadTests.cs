using System.Buffers.Binary;
using System.Globalization;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(430)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerOwnershipLoadTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(32)]
    [Arguments(128 * 1024)]
    public async Task RenewedBorrowedRecord_SurvivesSustainedMultiPartitionFetches(int payloadSize)
    {
        const int rounds = 64;
        const int recordsPerRound = 16;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        await using var producer = await Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"share-ownership-load-{Guid.NewGuid():N}")
            .WithValueDeserializer(Serializers.RawBytes)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithMaxPollRecords(recordsPerRound).WithFetchMaxBytes(256 * 1024).WithFetchMaxWaitMs(25)
            .BuildAsync();
        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var received = new HashSet<int>();
        ShareConsumeResult<string, ReadOnlyMemory<byte>>? retained = null;
        var retainedId = -1;
        for (var round = 0; round < rounds; round++)
        {
            for (var index = 0; index < recordsPerRound; index++)
            {
                var id = round * recordsPerRound + index;
                var value = new byte[payloadSize];
                value.AsSpan().Fill((byte)id);
                BinaryPrimitives.WriteInt32LittleEndian(value, id);
                // Each awaited delivery creates another broker batch. The large case
                // crosses the native response threshold and repeatedly reuses storage.
                await producer.ProduceAsync(new ProducerMessage<string, byte[]>
                {
                    Topic = topic, Partition = id % 4, Key = id.ToString(CultureInfo.InvariantCulture),
                    Value = value, Headers = Headers.Create("identity", id.ToString(CultureInfo.InvariantCulture))
                }, timeout.Token);
            }

            await foreach (var record in consumer.PollAsync(timeout.Token))
            {
                var id = int.Parse(record.Key!, CultureInfo.InvariantCulture);
                await AssertPayloadAsync(record, id, payloadSize);
                // Renew submissions use acknowledgement-only ShareFetch requests.
                // Requeueing Renew on every local replay would prevent fresh acquisition.
                // The existing renewal stays active until the next round's commit.
                if (ReferenceEquals(record, retained)) continue;

                await Assert.That(received.Add(id)).IsTrue();
                if (retained is null)
                {
                    retained = record;
                    retainedId = id;
                    consumer.Acknowledge(record, AcknowledgeType.Renew);
                }
                else
                {
                    consumer.Acknowledge(record, AcknowledgeType.Accept);
                    await AssertPayloadAsync(retained, retainedId, payloadSize);
                }
                if (received.Count == (round + 1) * recordsPerRound) break;
            }

            // Iterator disposal preserves this poll's views. Repeated Renew extends
            // the original batch lifetime while unrelated batches cycle through pools.
            consumer.Acknowledge(retained!, AcknowledgeType.Renew);
            await consumer.CommitAsync(timeout.Token);
            await AssertPayloadAsync(retained!, retainedId, payloadSize);
        }

        await Assert.That(received.Count).IsEqualTo(rounds * recordsPerRound);
        consumer.Acknowledge(retained!, AcknowledgeType.Accept);
        await consumer.CommitAsync(timeout.Token);
        await consumer.CloseAsync(timeout.Token);
    }

    private static async ValueTask AssertPayloadAsync(
        ShareConsumeResult<string, ReadOnlyMemory<byte>> record, int id, int payloadSize)
    {
        await Assert.That(record.Value.Length).IsEqualTo(payloadSize);
        await Assert.That(BinaryPrimitives.ReadInt32LittleEndian(record.Value.Span)).IsEqualTo(id);
        await Assert.That(record.Value.Span[sizeof(int)..].IndexOfAnyExcept((byte)id)).IsEqualTo(-1);
        // Producer tracing can inject additional headers. Validate the application
        // header by identity, including its borrowed value after other batches parse.
        var header = record.Headers.Single(static item => item.Key == "identity");
        await Assert.That(System.Text.Encoding.UTF8.GetString(header.Value.Span))
            .IsEqualTo(id.ToString(CultureInfo.InvariantCulture));
    }
}
