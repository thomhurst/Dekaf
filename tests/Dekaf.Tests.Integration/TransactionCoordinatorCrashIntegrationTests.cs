using Dekaf.Admin;
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

        await using var producer = await BuildTransactionalProducerAsync(transactionalId, cancellationToken)
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

            var committed = await ConsumeCommittedAsync(topic, RecordsPerTransaction * 2, cancellationToken)
                .ConfigureAwait(false);
            await Assert.That(committed).IsEquivalentTo(
                Enumerable.Range(0, RecordsPerTransaction * 2).Select(static offset => $"value-{offset}"));
        }
        finally
        {
            if (crashedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The abort path shares the EndTxn retry loop with commit, but nothing exercised it against
    /// a dead coordinator: the abort must retry through the stale-metadata window, its records
    /// must stay invisible, and the producer must be usable for the next transaction.
    /// </summary>
    [Test]
    [Timeout(240_000)]
    public async Task CoordinatorCrash_AbortRetriesThroughStaleMetadataAndNextTransactionCommits(
        CancellationToken cancellationToken)
    {
        var transactionalId = $"coordinator-crash-abort-{Guid.NewGuid():N}";
        var coordinatorId = await kafka.FindTransactionCoordinatorIdAsync(transactionalId, cancellationToken)
            .ConfigureAwait(false);
        var topic = await kafka.CreateDistributedReplicatedTopicAsync(
                PartitionCount,
                excludedLeaderId: coordinatorId)
            .ConfigureAwait(false);
        int? crashedBrokerId = null;

        await using var producer = await BuildTransactionalProducerAsync(transactionalId, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await producer.InitTransactionsAsync(cancellationToken).ConfigureAwait(false);

            await using (var transaction = producer.BeginTransaction())
            {
                await ProduceRangeAsync(transaction, topic, start: 0, cancellationToken).ConfigureAwait(false);
                await producer.FlushAsync(cancellationToken).ConfigureAwait(false);

                crashedBrokerId = coordinatorId;
                await kafka.KillBrokerAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

                await transaction.AbortAsync(cancellationToken).ConfigureAwait(false);
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

            // The aborted records sit at lower offsets, so a leak would surface first.
            var committed = await ConsumeCommittedAsync(topic, RecordsPerTransaction, cancellationToken)
                .ConfigureAwait(false);
            await Assert.That(committed).IsEquivalentTo(
                Enumerable.Range(RecordsPerTransaction, RecordsPerTransaction)
                    .Select(static offset => $"value-{offset}"));
        }
        finally
        {
            if (crashedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The coordinator is already dead when the producer initializes: FindCoordinator keeps
    /// naming it until its session expires, so every InitProducerId attempt fails at the
    /// transport level until the coordinator moves.
    /// </summary>
    [Test]
    [Timeout(240_000)]
    public async Task CoordinatorOutage_InitTransactionsRetriesUntilTheCoordinatorMoves(
        CancellationToken cancellationToken)
    {
        var transactionalId = $"coordinator-outage-init-{Guid.NewGuid():N}";
        var coordinatorId = await kafka.FindTransactionCoordinatorIdAsync(transactionalId, cancellationToken)
            .ConfigureAwait(false);
        var topic = await kafka.CreateDistributedReplicatedTopicAsync(
                PartitionCount,
                excludedLeaderId: coordinatorId)
            .ConfigureAwait(false);
        int? crashedBrokerId = null;

        try
        {
            crashedBrokerId = coordinatorId;
            await kafka.KillBrokerAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

            await using var producer = await BuildTransactionalProducerAsync(transactionalId, cancellationToken)
                .ConfigureAwait(false);
            await producer.InitTransactionsAsync(cancellationToken).ConfigureAwait(false);

            await using (var transaction = producer.BeginTransaction())
            {
                await ProduceRangeAsync(transaction, topic, start: 0, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            var committed = await ConsumeCommittedAsync(topic, RecordsPerTransaction, cancellationToken)
                .ConfigureAwait(false);
            await Assert.That(committed).IsEquivalentTo(
                Enumerable.Range(0, RecordsPerTransaction).Select(static offset => $"value-{offset}"));
        }
        finally
        {
            if (crashedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// SendOffsetsToTransaction talks to the consumer group's coordinator, which is a different
    /// broker from the transaction coordinator here. With that broker SIGKILLed, the lookup keeps
    /// naming it and TxnOffsetCommit cannot connect until the group moves; the offsets must still
    /// be committed with the transaction.
    /// </summary>
    [Test]
    [Timeout(240_000)]
    public async Task GroupCoordinatorCrash_SendOffsetsRetriesThroughStaleMetadataAndCommits(
        CancellationToken cancellationToken)
    {
        const long committedOffset = 10;
        var transactionalId = $"group-coordinator-crash-{Guid.NewGuid():N}";
        var transactionCoordinatorId = await kafka
            .FindTransactionCoordinatorIdAsync(transactionalId, cancellationToken)
            .ConfigureAwait(false);
        var (groupId, groupCoordinatorId) = await FindGroupOnAnotherBrokerAsync(
                transactionCoordinatorId,
                cancellationToken)
            .ConfigureAwait(false);
        var topic = await kafka.CreateDistributedReplicatedTopicAsync(
                PartitionCount,
                excludedLeaderId: groupCoordinatorId)
            .ConfigureAwait(false);
        int? crashedBrokerId = null;

        await using var producer = await BuildTransactionalProducerAsync(transactionalId, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await producer.InitTransactionsAsync(cancellationToken).ConfigureAwait(false);

            await using (var transaction = producer.BeginTransaction())
            {
                await ProduceRangeAsync(transaction, topic, start: 0, cancellationToken).ConfigureAwait(false);
                await producer.FlushAsync(cancellationToken).ConfigureAwait(false);

                crashedBrokerId = groupCoordinatorId;
                await kafka.KillBrokerAsync(groupCoordinatorId, cancellationToken).ConfigureAwait(false);

                var offsets = Enumerable.Range(0, PartitionCount)
                    .Select(partition => new TopicPartitionOffset(topic, partition, committedOffset))
                    .ToArray();
                await transaction.SendOffsetsToTransactionAsync(offsets, groupId, cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var admin = kafka.CreateAdminClient();
            var committed = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken)
                .ConfigureAwait(false);
            for (var partition = 0; partition < PartitionCount; partition++)
            {
                var topicPartition = new TopicPartition(topic, partition);
                await Assert.That(committed.TryGetValue(topicPartition, out var offset) ? offset : -1)
                    .IsEqualTo(committedOffset);
            }

            var records = await ConsumeCommittedAsync(topic, RecordsPerTransaction, cancellationToken)
                .ConfigureAwait(false);
            await Assert.That(records).IsEquivalentTo(
                Enumerable.Range(0, RecordsPerTransaction).Select(static offset => $"value-{offset}"));
        }
        finally
        {
            if (crashedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<(string GroupId, int CoordinatorId)> FindGroupOnAnotherBrokerAsync(
        int excludedBrokerId,
        CancellationToken cancellationToken)
    {
        // Group ids hash onto the __consumer_offsets partitions, so a handful of candidates is
        // enough to land on a broker other than the transaction coordinator's, as long as those
        // partitions' leaders are spread. The sibling tests in this class SIGKILL brokers, and a
        // restarted broker gets no leadership back until a preferred election runs, so every
        // __consumer_offsets leader can sit on the one broker this test must avoid. The first
        // pass that finds nothing elects preferred leaders; later passes wait for it to settle.
        for (var pass = 0; pass < 10; pass++)
        {
            for (var candidate = 0; candidate < 50; candidate++)
            {
                var groupId = $"group-coordinator-crash-{Guid.NewGuid():N}";
                var coordinatorId = await kafka.FindGroupCoordinatorIdAsync(groupId, cancellationToken)
                    .ConfigureAwait(false);
                if (coordinatorId != excludedBrokerId)
                    return (groupId, coordinatorId);
            }

            if (pass == 0)
            {
                await using var admin = kafka.CreateAdminClient();
                _ = await admin.ElectLeadersAsync(ElectionType.Preferred, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"No candidate group id was coordinated by a broker other than {excludedBrokerId}.");
    }

    private async Task<IKafkaProducer<string, string>> BuildTransactionalProducerAsync(
        string transactionalId,
        CancellationToken cancellationToken) =>
        await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithTransactionalId(transactionalId)
            .WithAcks(Acks.All)
            .WithMaxBlock(TimeSpan.FromSeconds(90))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

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

    private async Task<List<string>> ConsumeCommittedAsync(
        string topic,
        int expectedCount,
        CancellationToken cancellationToken)
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
                if (values.Count == expectedCount)
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
