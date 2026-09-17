using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// A transaction coordinator that is SIGKILLed (no controlled shutdown) keeps being named by
/// cluster metadata until its broker session expires, so every EndTxn attempt in that window
/// fails at the transport level. The commit must retry through the window, re-discover the
/// coordinator once it moves, and complete; the raw transport exception used to escape
/// <c>CommitAsync</c> on the first attempt and reset the producer to Ready.
/// </summary>
[Category("Transaction")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class TransactionCoordinatorCrashIntegrationTests(RackAwareKafkaContainer kafka)
{
    private const int PartitionCount = 3;
    private const int RecordsPerTransaction = 30;

    [Test]
    [Timeout(240_000)]
    public async Task CoordinatorCrash_CommitRetriesThroughStaleMetadataAndNextTransactionSucceeds(
        CancellationToken cancellationToken)
    {
        var transactionalId = $"coordinator-crash-{Guid.NewGuid():N}";
        var coordinatorId = await kafka.FindTransactionCoordinatorIdAsync(transactionalId, cancellationToken)
            .ConfigureAwait(false);
        var topic = await kafka.CreateDistributedReplicatedTopicAsync(
                PartitionCount,
                excludedLeaderId: coordinatorId)
            .ConfigureAwait(false);
        int? crashedBrokerId = null;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithTransactionalId(transactionalId)
            .WithAcks(Acks.All)
            .WithMaxBlock(TimeSpan.FromSeconds(90))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await producer.InitTransactionsAsync(cancellationToken).ConfigureAwait(false);

            await using (var transaction = producer.BeginTransaction())
            {
                await ProduceRangeAsync(transaction, topic, start: 0, cancellationToken).ConfigureAwait(false);
                await producer.FlushAsync(cancellationToken).ConfigureAwait(false);

                var currentCoordinatorId = await kafka.FindTransactionCoordinatorIdAsync(
                        transactionalId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (currentCoordinatorId != coordinatorId)
                {
                    throw new InvalidOperationException(
                        $"FindCoordinator changed before the crash: expected broker {coordinatorId}, " +
                        $"actual broker {currentCoordinatorId}.");
                }

                crashedBrokerId = coordinatorId;
                await kafka.KillBrokerAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            _ = await kafka.WaitForTransactionCoordinatorChangeAsync(
                    transactionalId,
                    coordinatorId,
                    cancellationToken)
                .ConfigureAwait(false);

            await using (var transaction = producer.BeginTransaction())
            {
                await ProduceRangeAsync(transaction, topic, start: RecordsPerTransaction, cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            var committed = await ConsumeCommittedAsync(topic, cancellationToken).ConfigureAwait(false);
            await Assert.That(committed).IsEquivalentTo(
                Enumerable.Range(0, RecordsPerTransaction * 2).Select(static offset => $"value-{offset}"));
        }
        finally
        {
            if (crashedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task ProduceRangeAsync(
        ITransaction<string, string> transaction,
        string topic,
        int start,
        CancellationToken cancellationToken)
    {
        for (var offset = start; offset < start + RecordsPerTransaction; offset++)
        {
            await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = offset % PartitionCount,
                Key = $"key-{offset}",
                Value = $"value-{offset}"
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<string>> ConsumeCommittedAsync(string topic, CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"coordinator-crash-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.Subscribe(topic);

        var values = new List<string>();
        using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readTimeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await foreach (var record in consumer.ConsumeAsync(readTimeout.Token).ConfigureAwait(false))
            {
                values.Add(record.Value!);
                if (values.Count == RecordsPerTransaction * 2)
                    break;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Return what was read; the assertion reports the shortfall.
        }

        return values;
    }
}
