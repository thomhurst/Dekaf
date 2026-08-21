using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
public sealed class ConsumerRecordFilterIntegrationTests(KafkaTestContainer kafka)
    : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task FilteredRecord_AdvancesPositionAndManualCommit()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"record-filter-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await ProduceAsync(producer, topic, "drop", "filtered");
        await ProduceAsync(producer, topic, "keep", "delivered");
        await ProduceAsync(producer, topic, "keep", "after-commit");

        var filter = new RouteHeaderFilter();
        await using (var first = await CreateConsumerAsync(topic, groupId, filter))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await first.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Offset).IsEqualTo(1L);
            await Assert.That(result.Value.Value).IsEqualTo("delivered");
            await Assert.That(filter.CallCount).IsEqualTo(2);

            await first.CommitAsync(timeout.Token);
            await Assert.That(await first.GetCommittedOffsetAsync(
                    new TopicPartition(topic, 0),
                    timeout.Token))
                .IsEqualTo(2L);
        }

        await using var second = await CreateConsumerAsync(topic, groupId, new RouteHeaderFilter());
        using var secondTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var afterCommit = await second.ConsumeOneAsync(TimeSpan.FromSeconds(30), secondTimeout.Token);

        await Assert.That(afterCommit).IsNotNull();
        await Assert.That(afterCommit!.Value.Offset).IsEqualTo(2L);
        await Assert.That(afterCommit.Value.Value).IsEqualTo("after-commit");
    }

    [Test]
    public async Task FilteredRecord_AdvancesAutomaticCommit()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"record-filter-auto-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await ProduceAsync(producer, topic, "drop", "filtered");
        await ProduceAsync(producer, topic, "keep", "delivered");

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .WithQueuedMinMessages(1)
            .WithRecordFilter(new RouteHeaderFilter())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var delivered = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
        await Assert.That(delivered).IsNotNull();
        await Assert.That(delivered!.Value.Offset).IsEqualTo(1L);

        // Requesting the next record proves the delivered record was processed. There is no
        // third record, so the short poll returns null while making offsets 0 and 1 committable.
        _ = await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(100), timeout.Token);

        var partition = new TopicPartition(topic, 0);
        long? committed = null;
        while (!timeout.IsCancellationRequested)
        {
            committed = await consumer.GetCommittedOffsetAsync(partition, timeout.Token);
            if (committed >= 2)
                break;

            await Task.Delay(100, timeout.Token);
        }

        await Assert.That(committed).IsEqualTo(2L);
    }

    [Test]
    public async Task RunPartitionedAsync_FilterSkipsBeforePartitionDelivery()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await ProduceAsync(producer, topic, "drop", "filtered");
        await ProduceAsync(producer, topic, "keep", "delivered");

        var filter = new RouteHeaderFilter();
        await using var consumer = await CreateConsumerAsync(
            topic,
            $"record-filter-partitioned-{Guid.NewGuid():N}",
            filter);
        var deliveredOffsets = new ConcurrentQueue<long>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var run = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    deliveredOffsets.Enqueue(message.Offset);
                    context.MarkProcessed(message);
                    await timeout.CancelAsync().ConfigureAwait(false);
                }
            },
            cancellationToken: timeout.Token).AsTask();

        try
        {
            await run.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await Assert.That(timeout.IsCancellationRequested).IsTrue();
        }

        await Assert.That(deliveredOffsets.ToArray()).IsEquivalentTo([1L]);
        await Assert.That(filter.CallCount).IsEqualTo(2);
    }

    private async Task<IKafkaConsumer<string, string>> CreateConsumerAsync(
        string topic,
        string groupId,
        IConsumerRecordFilter filter)
    {
        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(1)
            .WithRecordFilter(filter)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);
        return consumer;
    }

    private static ValueTask<RecordMetadata> ProduceAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        string route,
        string value) => producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = route,
            Value = value,
            Headers = Headers.Create("route", route)
        });

    private sealed class RouteHeaderFilter : IConsumerRecordFilter
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context)
        {
            Interlocked.Increment(ref _callCount);
            var headers = context.Headers;
            for (var i = headers.Length - 1; i >= 0; i--)
            {
                ref readonly var header = ref headers[i];
                if (header.Key == "route")
                    return !header.IsValueNull && header.Value.Span.SequenceEqual("keep"u8);
            }

            return false;
        }
    }
}
