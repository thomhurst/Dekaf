using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedTransactionalCommitIntegrationTests(KafkaTestContainer kafka)
    : TransactionalKafkaIntegrationTest(kafka)
{
    private const int RecordsPerPartition = 20;

    [Test]
    public async Task RunPartitionedAsync_TransactionalCommit_FailedRunLeavesNoDuplicateOutput(CancellationToken cancellationToken)
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var output = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"partitioned-txn-{Guid.NewGuid():N}";

        await using (var inputProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken))
        {
            for (var offset = 0; offset < RecordsPerPartition; offset++)
            {
                for (var partition = 0; partition < 2; partition++)
                {
                    await inputProducer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = input,
                        Partition = partition,
                        Key = $"key-{offset % 3}",
                        Value = $"{partition}-{offset}"
                    }, cancellationToken);
                }
            }
        }

        // The first run fails part way: its open transaction aborts, so its output after the last
        // committed transaction is never visible and the second run produces it again, once.
        var failingRun = await RunTransformAsync(input, output, groupId, failAt: "0-12", cancellationToken);
        await Assert.That(failingRun).IsTypeOf<InvalidOperationException>();

        var completedRun = await RunTransformAsync(input, output, groupId, failAt: null, cancellationToken);
        await Assert.That(completedRun).IsNull();

        var values = await ReadCommittedAsync(output, expected: 2 * RecordsPerPartition, cancellationToken);
        await Assert.That(values.Count).IsEqualTo(2 * RecordsPerPartition);
        await Assert.That(values.Distinct().Count()).IsEqualTo(2 * RecordsPerPartition);
    }

    private async Task<Exception?> RunTransformAsync(
        string input,
        string output,
        string groupId,
        string? failAt,
        CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"{groupId}-txn")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(input);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(90));
        // A run that does not fail stops once the group committed every input record. The second
        // run resumes after the first run's commits, so counting its own records cannot tell.
        var reachedEnd = false;
        var stopAtEnd = failAt is null ? StopAtInputEndAsync() : Task.CompletedTask;
        try
        {
            await consumer.RunPartitionedAsync(
                async (_, message, handlerCancellation) =>
                {
                    if (message.Value == failAt)
                        throw new InvalidOperationException("handler failed");

                    await producer.ProduceAsync(output, message.Key, message.Value, handlerCancellation).ConfigureAwait(false);
                },
                new PartitionedProcessingOptions
                {
                    Ordering = PartitionedProcessingOrder.Key,
                    MaxConcurrentHandlersPerPartition = 3,
                    CommitPolicy = PartitionCommitPolicy.CommitCompletedInTransaction,
                    CommitInterval = TimeSpan.FromMilliseconds(100),
                    TransactionalProducer = PartitionedTransactionalProducer.Create(producer)
                },
                cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            return exception;
        }
        finally
        {
            await cts.CancelAsync();
            try
            {
                await stopAtEnd;
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Without a handler failure, only reaching the input end counts as completion, not the safety timeout.
        return failAt is null && !reachedEnd
            ? new TimeoutException("The run did not commit every input record in time.")
            : null;

        async Task StopAtInputEndAsync()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);
                // The bulk lookup asks the coordinator; the single-partition lookup returns the consumer's
                // cached offset, which does not see commits made through the transactional producer.
                var committed = await consumer.GetCommittedOffsetsAsync(
                    [new TopicPartition(input, 0), new TopicPartition(input, 1)], cts.Token);
                var committedToEnd = committed.Count == 2 && committed.Values.All(offset => offset.Offset >= RecordsPerPartition);

                if (committedToEnd)
                {
                    reachedEnd = true;
                    await cts.CancelAsync();
                    return;
                }
            }
        }
    }

    private async Task<List<string>> ReadCommittedAsync(string topic, int expected, CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"partitioned-txn-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        var values = new List<string>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        // Read past the expected count for a moment, so a duplicate would be seen.
        while (values.Count <= expected
            && await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(values.Count < expected ? 10 : 2), cts.Token) is { } record)
        {
            values.Add(record.Value);
        }

        return values;
    }
}
