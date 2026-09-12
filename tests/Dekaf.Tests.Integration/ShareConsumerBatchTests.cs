using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[Category("ShareConsumerCore")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerBatchTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task LongHeaderRouting_ResumesColdPreparationAndPreservesBorrowedHeaders()
    {
        const int messageCount = 16;
        var name = new string('r', 512);
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        var unrelatedName = new string('u', 4096);
        var unrelatedNameBytes = System.Text.Encoding.UTF8.GetBytes(unrelatedName);
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-batch-routing-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<int, int>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMinutes(1)).WithBatchSize(1024 * 1024).BuildAsync();
        for (var index = 0; index < messageCount; index++)
            await producer.FireAsync(new ProducerMessage<int, int>
            {
                Topic = topic, Partition = 0, Key = index, Value = index,
                Headers = new Headers
                {
                    { unrelatedName, "other"u8.ToArray() },
                    { name, "selected"u8.ToArray() }
                }
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await producer.FlushAsync(timeout.Token);
        var preparer = new MidBatchPreparer();
        var router = new HeaderRoutingDeserializer<int>(name, new UnexpectedRouteDeserializer(),
            new HeaderDeserializerRoute<int>("selected"u8.ToArray(), preparer));
        await using var consumer = await Kafka.CreateShareConsumer<int, int>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithValueDeserializer(router).WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .BuildAsync(timeout.Token);
        consumer.Subscribe(topic);
        var received = 0;
        await foreach (var batch in consumer.PollBatchesAsync(timeout.Token))
        {
            foreach (var record in batch)
            {
                await Assert.That(record.Value).IsEqualTo(received++);
                var matched = 0;
                var unrelated = 0;
                foreach (var header in record.Headers)
                {
                    if (header.KeyUtf8.Span.SequenceEqual(nameBytes))
                    {
                        await Assert.That(header.Value.Span.SequenceEqual("selected"u8)).IsTrue();
                        matched++;
                    }
                    if (header.KeyUtf8.Span.SequenceEqual(unrelatedNameBytes))
                    {
                        await Assert.That(header.Value.Span.SequenceEqual("other"u8)).IsTrue();
                        unrelated++;
                    }
                }
                await Assert.That(matched).IsEqualTo(1);
                await Assert.That(unrelated).IsEqualTo(1);
                batch.Acknowledge(record);
            }
            await consumer.CommitAsync(timeout.Token);
            if (received == messageCount)
                break;
        }
        await Assert.That(received).IsEqualTo(messageCount);
        await Assert.That(preparer.Preparations).IsEqualTo(1);
        await consumer.CloseAsync(timeout.Token);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ColdPreparation_ResumesWithinProducerBatch(bool prepareKey)
    {
        const int messageCount = 16;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-batch-preparation-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<int, int>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMinutes(1)).WithBatchSize(1024 * 1024).BuildAsync();
        for (var index = 0; index < messageCount; index++)
            await producer.FireAsync(new ProducerMessage<int, int>
            {
                Topic = topic, Partition = 0, Key = index, Value = index,
                Headers = Headers.Create("identity", new byte[] { (byte)index })
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await producer.FlushAsync(timeout.Token);

        var preparer = new MidBatchPreparer();
        await using var consumer = await Kafka.CreateShareConsumer<int, int>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithKeyDeserializer(prepareKey ? preparer : Serializers.Int32)
            .WithValueDeserializer(prepareKey ? Serializers.Int32 : preparer)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithMaxPollRecords(messageCount).BuildAsync(timeout.Token);
        consumer.Subscribe(topic);
        var received = 0;
        var largestBatch = 0;
        await foreach (var batch in consumer.PollBatchesAsync(timeout.Token))
        {
            largestBatch = Math.Max(largestBatch, batch.Count);
            foreach (var record in batch)
            {
                await Assert.That(record.Key).IsEqualTo(received);
                await Assert.That(record.Value).IsEqualTo(received);
                var identityHeaders = 0;
                foreach (var header in record.Headers)
                {
                    // The producer may also propagate the test runner's trace context.
                    if (!header.KeyUtf8.Span.SequenceEqual("identity"u8))
                        continue;
                    await Assert.That(header.Value.Length).IsEqualTo(1);
                    await Assert.That(header.Value.Span[0]).IsEqualTo((byte)received);
                    identityHeaders++;
                }
                await Assert.That(identityHeaders).IsEqualTo(1);
                batch.Acknowledge(record);
                received++;
            }
            await consumer.CommitAsync(timeout.Token);
            if (received == messageCount)
                break;
        }
        await Assert.That(received).IsEqualTo(messageCount);
        await Assert.That(largestBatch).IsEqualTo(messageCount);
        await Assert.That(preparer.Preparations).IsEqualTo(1);
        await consumer.CloseAsync(timeout.Token);
    }

    [Test]
    public async Task Unsubscribe_RedeliversUnparsedProducerBatchesBeforeLockExpiry()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-batch-unsubscribe-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        // Await each delivery to create distinct producer batches before fetching.
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 3);
        await using var first = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).BuildAsync();
        first.Subscribe(topic);
        using var initialTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var poll = first.PollBatchesAsync(initialTimeout.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        await Assert.That(poll.Current.Count).IsEqualTo(1);
        first.Unsubscribe();
        await Assert.That(await poll.MoveNextAsync()).IsFalse();

        // Keep the first consumer alive: Dispose/Close must not mask a missed release.
        // The broker's acquisition timeout is 30 seconds; release must happen sooner.
        using var releaseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var second = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync(releaseTimeout.Token);
        second.Subscribe(topic);
        var values = new HashSet<string?>();
        await foreach (var record in second.PollAsync(releaseTimeout.Token))
        {
            values.Add(record.Value);
            second.Acknowledge(record);
            if (values.Count == 3) break;
        }
        string?[] expected = ["value-0", "value-1", "value-2"];
        await Assert.That(values).IsEquivalentTo(expected);
        await second.CommitAsync(releaseTimeout.Token);
    }

    [Test]
    [Arguments(1)]
    [Arguments(4)]
    public async Task NativeResponsePayload_RemainsReadableThroughBatchDelivery(int messageCount)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-batch-native-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        var values = new string[messageCount];
        for (var index = 0; index < messageCount; index++)
        {
            values[index] = new string((char)('a' + index), 128 * 1024);
            await producer.ProduceAsync(new Dekaf.Producer.ProducerMessage<string, string>
            {
                Topic = topic, Partition = 0, Key = index.ToString(), Value = values[index]
            });
        }
        await producer.FlushAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).WithMaxPollRecords(messageCount)
            .BuildAsync();
        consumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var received = new HashSet<int>();
        await foreach (var batch in consumer.PollBatchesAsync(timeout.Token))
        {
            foreach (var record in batch)
            {
                var index = int.Parse(record.Key!);
                await Assert.That(received.Add(index)).IsTrue();
                await Assert.That(record.Value).IsEqualTo(values[index]);
                batch.Acknowledge(record);
            }
            await consumer.CommitAsync(timeout.Token);
            if (received.Count == messageCount) break;
        }
        await Assert.That(received.Count).IsEqualTo(messageCount);
        await consumer.CloseAsync(timeout.Token);
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit)]
    [Arguments(ShareAcknowledgementMode.Explicit)]
    public async Task UnreadBatch_DisposingPollReleasesLocalTracking(ShareAcknowledgementMode mode)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-batch-unread-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 3);
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(mode).BuildAsync();
        consumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ShareBatchAcknowledgements<string, string>? tracker = null;
        await foreach (var batch in consumer.PollBatchesAsync(timeout.Token))
        {
            await Assert.That(batch.Count).IsGreaterThan(0);
            tracker = batch.Storage.Tracker;
            break;
        }
        await Assert.That(tracker).IsNotNull();
        await Assert.That(tracker!.HasPending).IsFalse();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
        await consumer.CommitAsync(timeout.Token);
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(32)]
    [Arguments(131072)]
    [SupportsKafka(430)]
    public async Task RenewedBatch_ReplaysBorrowedPayloadThroughNewLease(int payloadBytes)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-batch-renew-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        var payload = new string('x', payloadBytes);
        await producer.ProduceAsync(new Dekaf.Producer.ProducerMessage<string, string>
        {
            Topic = topic, Partition = 0, Key = "key-0", Value = payload,
            Headers = new Dekaf.Serialization.Headers { { "batch-identity", "retained"u8.ToArray() } }
        });
        await producer.FlushAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).WithMaxPollRecords(1).BuildAsync();
        consumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var poll = consumer.PollBatchesAsync(timeout.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var first = poll.Current;
        var records = first.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        var original = records.Current;
        var offset = original.Offset;
        var value = original.Value;
        var deliveryCount = original.DeliveryCount;
        first.Acknowledge(original, AcknowledgeType.Renew);
        await consumer.CommitAsync(timeout.Token);
        await Assert.That(consumer.AcquisitionLockTimeoutMs).IsNotNull();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        await Assert.That(() => original.Value).Throws<ObjectDisposedException>();
        var renewed = poll.Current;
        var replays = renewed.GetEnumerator();
        await Assert.That(replays.MoveNext()).IsTrue();
        await Assert.That(replays.Current.Offset).IsEqualTo(offset);
        await Assert.That(replays.Current.Value).IsEqualTo(value);
        await Assert.That(replays.Current.KeyBytes.Span.SequenceEqual("key-0"u8)).IsTrue();
        await Assert.That(replays.Current.ValueBytes.Span.SequenceEqual(System.Text.Encoding.UTF8.GetBytes(payload))).IsTrue();
        var foundHeader = false;
        foreach (var header in replays.Current.Headers)
        {
            if (!header.KeyUtf8.Span.SequenceEqual("batch-identity"u8))
                continue;
            foundHeader = true;
            await Assert.That(header.Value.Span.SequenceEqual("retained"u8)).IsTrue();
        }
        await Assert.That(foundHeader).IsTrue();
        await Assert.That(replays.Current.DeliveryCount).IsEqualTo(deliveryCount);
        renewed.Acknowledge(replays.Current);
        await consumer.CommitAsync(timeout.Token);
    }

    [Test]
    [Arguments(false, AcknowledgeType.Accept)]
    [Arguments(true, AcknowledgeType.Accept)]
    [Arguments(true, AcknowledgeType.Release)]
    [Arguments(true, AcknowledgeType.Reject)]
    public async Task PartialBatch_ClosePreservesDispositionAndRedeliversRemainingRecords(
        bool explicitAcknowledgement, AcknowledgeType type)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"share-batch-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 3);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        string? firstValue = null;
        ShareBatchRecord<string, string> firstRecord = default;
        await using (var first = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(explicitAcknowledgement
                ? ShareAcknowledgementMode.Explicit : ShareAcknowledgementMode.Implicit)
            .BuildAsync())
        {
            first.Subscribe(topic);
            await foreach (var batch in first.PollBatchesAsync(timeout.Token))
            {
                foreach (var record in batch)
                {
                    firstRecord = record;
                    firstValue = record.Value;
                    await Assert.That(record.DeliveryCount).IsGreaterThanOrEqualTo(1);
                    if (explicitAcknowledgement)
                        batch.Acknowledge(record, type);
                    break;
                }
                break;
            }
        }

        await Assert.That(firstValue).IsNotNull();
        await Assert.That(() => firstRecord.Value).Throws<ObjectDisposedException>();
        await using var second = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group).BuildAsync();
        second.Subscribe(topic);
        var terminal = explicitAcknowledgement && type is AcknowledgeType.Accept or AcknowledgeType.Reject;
        var expectedCount = terminal ? 2 : 3;
        var values = new List<string?>();
        await foreach (var record in second.PollAsync(timeout.Token))
        {
            values.Add(record.Value);
            second.Acknowledge(record);
            if (values.Count == expectedCount)
                break;
        }
        string?[] producedValues = ["value-0", "value-1", "value-2"];
        var expected = terminal ? producedValues.Where(value => value != firstValue) : producedValues;
        await Assert.That(values).IsEquivalentTo(expected);
        await second.CommitAsync(timeout.Token);
    }

    private sealed class UnexpectedRouteDeserializer : IDeserializer<int>
    {
        public int Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidOperationException("The configured header route was not selected.");
    }

    private sealed class MidBatchPreparer : IDeserializer<int>, IAsyncDeserializerPreparer<int>
    {
        internal int Preparations;

        public int Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidOperationException("Use the preparation-aware path.");

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out int value)
        {
            value = Serializers.Int32.Deserialize(data, context);
            return value != 3 || Preparations != 0;
        }

        public async ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Preparations++;
        }
    }

    private async Task ConfigureEarliestAsync(string group)
    {
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        });
    }
}
