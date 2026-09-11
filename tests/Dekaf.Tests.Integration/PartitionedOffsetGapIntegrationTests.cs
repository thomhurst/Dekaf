using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedOffsetGapIntegrationTests(KafkaTestContainer kafka)
    : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task HiddenTransactionalOffsets_CommitCompletedPrefixAndRestartWithoutReplay()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"partitioned-gaps-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"partitioned-gaps-txn-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .BuildAsync(timeout.Token);
        await producer.InitTransactionsAsync(timeout.Token);

        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(new ProducerMessage<string, string>
                { Topic = topic, Partition = 0, Key = "visible", Value = "first" }, timeout.Token);
            await transaction.CommitAsync(timeout.Token);
        }
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(new ProducerMessage<string, string>
                { Topic = topic, Partition = 0, Key = "hidden", Value = "aborted" }, timeout.Token);
            await transaction.AbortAsync(timeout.Token);
        }
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(new ProducerMessage<string, string>
                { Topic = topic, Partition = 0, Key = "visible", Value = "second" }, timeout.Token);
            await transaction.ProduceAsync(new ProducerMessage<string, string>
                { Topic = topic, Partition = 0, Key = "visible", Value = "third" }, timeout.Token);
            await transaction.CommitAsync(timeout.Token);
        }

        var offsets = new List<long>();
        await using (var consumer = await CreateConsumerAsync())
        {
            consumer.Subscribe(topic);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var run = consumer.RunPartitionedAsync(async (context, token) =>
            {
                await foreach (var message in context.Messages.WithCancellation(token))
                {
                    await Assert.That(message.Value).IsEqualTo(offsets.Count switch
                    {
                        0 => "first", 1 => "second", 2 => "third", _ => "unexpected record"
                    });
                    offsets.Add(message.Offset);
                    context.MarkProcessed(message);
                    if (offsets.Count == 3)
                    {
                        await context.CommitProcessedAsync(token);
                        committed.TrySetResult();
                    }
                }
            }, new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                StopPolicy = PartitionStopPolicy.Cancel
            }, stop.Token).AsTask();

            try
            {
                await Task.WhenAny(run, committed.Task).WaitAsync(timeout.Token);
                if (run.IsCompleted)
                    await run;
                await committed.Task.WaitAsync(timeout.Token);
                await Assert.That(offsets[1]).IsGreaterThan(offsets[0] + 1);
                await Assert.That(await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0), timeout.Token))
                    .IsEqualTo(offsets[2] + 1);
            }
            finally
            {
                await stop.CancelAsync();
                try
                {
                    await run.WaitAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested && !timeout.IsCancellationRequested)
                {
                    await Assert.That(run.IsCanceled).IsTrue();
                }
            }
        }

        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(new ProducerMessage<string, string>
                { Topic = topic, Partition = 0, Key = "visible", Value = "after-restart" }, timeout.Token);
            await transaction.CommitAsync(timeout.Token);
        }
        await using var restarted = await CreateConsumerAsync();
        restarted.Subscribe(topic);
        var next = await restarted.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
        await Assert.That(next.HasValue).IsTrue();
        await Assert.That(next!.Value.Value).IsEqualTo("after-restart");
        await Assert.That(next.Value.Offset).IsGreaterThan(offsets[2]);

        async ValueTask<IKafkaConsumer<string, string>> CreateConsumerAsync() =>
            await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(group)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithOffsetCommitMode(OffsetCommitMode.Manual)
                .WithIsolationLevel(IsolationLevel.ReadCommitted)
                .WithQueuedMinMessages(1)
                .BuildAsync(timeout.Token);
    }
}
