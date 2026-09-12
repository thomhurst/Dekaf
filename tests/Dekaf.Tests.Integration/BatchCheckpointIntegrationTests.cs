using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Transaction")]
public sealed class BatchCheckpointIntegrationTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Timeout(120_000)]
    public async Task ReadCommittedCheckpoint_CommitsTrailingProgressAndRestarts(
        bool raw, bool transactionalCommit, CancellationToken cancellationToken)
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"batch-checkpoint-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"checkpoint-source-{Guid.NewGuid():N}")
            .WithAcks(Acks.All).BuildAsync(cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(new ProducerMessage<string, string>
            { Topic = input, Key = "key", Value = "visible" }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        long abortedOffset;
        await using (var transaction = producer.BeginTransaction())
        {
            var result = await transaction.ProduceAsync(new ProducerMessage<string, string>
            { Topic = input, Key = "key", Value = "aborted" }, cancellationToken);
            abortedOffset = result.Offset;
            await transaction.AbortAsync(cancellationToken);
        }

        TopicPartitionOffset checkpoint;
        var values = new List<string>();
        await using (var consumer = await CreateConsumerAsync(group, cancellationToken))
        {
            // EndTxn can return before the broker writes the abort marker. Wait until the
            // read-committed watermark includes it, otherwise capture can skip a stale end.
            var watermarks = await TestWait.WaitForConditionAsync(
                async () => await consumer.QueryWatermarkOffsetsAsync(new TopicPartition(input, 0), cancellationToken),
                offsets => offsets.High > abortedOffset + 1,
                maxRetries: 30,
                initialDelayMs: 50,
                description: "abort marker visibility before capturing the batch checkpoint",
                formatObserved: offsets => $"high watermark {offsets.High}, aborted record offset {abortedOffset}");
            var end = watermarks.High;
            consumer.Subscribe(input);
            checkpoint = await CaptureAsync(consumer, raw, end, values, cancellationToken);
            await Assert.That(values.Count).IsEqualTo(1);
            await Assert.That(values[0]).IsEqualTo("visible");
            await Assert.That(checkpoint.Offset).IsEqualTo(end);
            await Assert.That(checkpoint.Offset).IsGreaterThan(1);
            await Assert.That(checkpoint.LeaderEpoch).IsGreaterThanOrEqualTo(0);

            if (transactionalCommit)
            {
                await using var sink = await Kafka.CreateProducer<string, string>()
                    .WithBootstrapServers(KafkaContainer.BootstrapServers)
                    .WithTransactionalId($"checkpoint-sink-{Guid.NewGuid():N}")
                    .WithAcks(Acks.All).BuildAsync(cancellationToken);
                await sink.InitTransactionsAsync(cancellationToken);
                await using var transaction = sink.BeginTransaction();
                foreach (var value in values)
                    await transaction.ProduceAsync(new ProducerMessage<string, string>
                    { Topic = output, Key = "key", Value = $"processed-{value}" }, cancellationToken);
                await transaction.SendOffsetsToTransactionAsync([checkpoint], group, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await consumer.CommitAsync([checkpoint], cancellationToken);
            }
        }

        await using (var append = await Kafka.CreateProducer<string, string>()
                         .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken))
        {
            await append.ProduceAsync(input, "key", "after-checkpoint", cancellationToken);
        }
        await using var resumed = await CreateConsumerAsync(group, cancellationToken);
        resumed.Subscribe(input);
        var next = await resumed.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await Assert.That(next.HasValue).IsTrue();
        await Assert.That(next!.Value.Offset).IsEqualTo(checkpoint.Offset);
        await Assert.That(next.Value.Value).IsEqualTo("after-checkpoint");

        if (transactionalCommit)
        {
            await using var verification = await CreateConsumerAsync($"verify-{Guid.NewGuid():N}", cancellationToken);
            verification.Subscribe(output);
            var produced = await verification.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);
            await Assert.That(produced.HasValue).IsTrue();
            await Assert.That(produced!.Value.Value).IsEqualTo("processed-visible");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(90_000)]
    public async Task PauseRetainsProgress_SeekInvalidatesCapture(bool raw, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "key", "value", cancellationToken);
        await using var consumer = await CreateConsumerAsync($"checkpoint-seek-{Guid.NewGuid():N}", cancellationToken);
        consumer.Subscribe(topic);
        var partition = new TopicPartition(topic, 0);
        if (raw)
        {
            await using var batches = consumer.ConsumeRawBatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            await Assert.That(await batches.MoveNextAsync()).IsTrue();
            var batch = batches.Current;
            var records = batch.GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
            consumer.Partitions.Pause(partition);
            await Assert.That(batch.TryGetNextOffset(out var checkpoint)).IsTrue();
            consumer.Partitions.Resume(partition);
            consumer.Seek(new TopicPartitionOffset(topic, 0, 0));
            await Assert.That(batch.TryGetNextOffset(out _)).IsFalse();
            await Assert.That(checkpoint.Offset).IsEqualTo(1);
        }
        else
        {
            await using var batches = consumer.ConsumeBatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            await Assert.That(await batches.MoveNextAsync()).IsTrue();
            var batch = batches.Current;
            var records = batch.GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
            consumer.Partitions.Pause(partition);
            await Assert.That(batch.TryGetNextOffset(out var checkpoint)).IsTrue();
            consumer.Partitions.Resume(partition);
            consumer.Seek(new TopicPartitionOffset(topic, 0, 0));
            await Assert.That(batch.TryGetNextOffset(out _)).IsFalse();
            await Assert.That(checkpoint.Offset).IsEqualTo(1);
        }
    }

    private async ValueTask<IKafkaConsumer<string, string>> CreateConsumerAsync(string group, CancellationToken token) =>
        await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group).WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual).WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithQueuedMinMessages(1).WithMaxPollRecords(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync(token);

    private static async Task<TopicPartitionOffset> CaptureAsync(IKafkaConsumer<string, string> consumer,
        bool raw, long end, List<string> values, CancellationToken token)
    {
        TopicPartitionOffset? captured = null;
        if (raw)
        {
            ConsumeRawBatch? last = null;
            await foreach (var batch in consumer.ConsumeRawBatchAsync(token))
            {
                last = batch;
                foreach (var record in batch)
                    values.Add(Encoding.UTF8.GetString(record.Value.Span));
                if (batch.TryGetNextOffset(out var checkpoint) && checkpoint.Offset == end)
                {
                    captured = checkpoint;
                    break;
                }
            }
            if (last is not null && last.TryGetNextOffset(out _))
                throw new InvalidOperationException("Outer disposal must invalidate raw batch checkpoint access.");
        }
        else
        {
            ConsumeBatch<string, string>? last = null;
            await foreach (var batch in consumer.ConsumeBatchAsync(token))
            {
                last = batch;
                foreach (var record in batch)
                    values.Add(record.Value);
                if (batch.TryGetNextOffset(out var checkpoint) && checkpoint.Offset == end)
                {
                    captured = checkpoint;
                    break;
                }
            }
            if (last is not null && last.TryGetNextOffset(out _))
                throw new InvalidOperationException("Outer disposal must invalidate typed batch checkpoint access.");
        }
        return captured ?? throw new InvalidOperationException(
            "The batch stream ended before its checkpoint reached the stable end offset.");
    }
}
