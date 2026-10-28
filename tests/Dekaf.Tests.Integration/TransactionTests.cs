using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for producer transactions.
/// </summary>
[Category("Transaction")]
public class TransactionTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task InitTransactions_SetsProducerIdAndEpoch()
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"txn-init-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Should not throw
        await producer.InitTransactionsAsync();
    }

    [Test]
    [Timeout(60_000)]
    public async Task Transaction_ProduceThenCommit_InlineContinuationDoesNotDeadlock(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-commit-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        await producer.InitTransactionsAsync(cancellationToken);

        await using (var txn = producer.BeginTransaction())
        {
            // Produce resumes inline on the sender thread. Commit must yield at the
            // asynchronous flush boundary so the sender can finish batch cleanup.
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "txn-key",
                Value = "txn-value"
            }, cancellationToken);

            await txn.CommitAsync(cancellationToken);
        }

        // Consume the message - it should be visible after commit
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync(cancellationToken);

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("txn-value");
    }

    /// <summary>
    /// A transactional producer refuses a produce outside an open transaction (#3748): before
    /// InitTransactionsAsync it would be a plain non-idempotent write, and after a commit the
    /// broker would open a transaction the producer does not track, which the next commit would
    /// silently include. Only the records produced inside each transaction reach the topic.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task Produce_OutsideOpenTransaction_IsRefusedAndNeverReachesTheTopic(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"txn-outside-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        var beforeInit = await Assert.That(
                () => producer.ProduceAsync(topic, "stray", "before-init", cancellationToken).AsTask())
            .Throws<TransactionException>();
        await Assert.That(beforeInit!.ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);

        await producer.InitTransactionsAsync(cancellationToken);

        await using (var first = producer.BeginTransaction())
        {
            await first.ProduceAsync(topic, "key", "first", cancellationToken);
            await first.CommitAsync(cancellationToken);
        }

        var afterCommit = await Assert.That(
                () => producer.ProduceAsync(topic, "stray", "after-commit", cancellationToken).AsTask())
            .Throws<TransactionException>();
        await Assert.That(afterCommit!.ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);
        await Assert.That(() => producer.FireAsync(topic, "stray", "fire-after-commit").AsTask())
            .Throws<TransactionException>();

        await using (var second = producer.BeginTransaction())
        {
            await producer.ProduceAsync(topic, "key", "second", cancellationToken);
            await second.CommitAsync(cancellationToken);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-outside-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadUncommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        // Read uncommitted, so a stray record would show up whatever its transaction's outcome.
        var values = new List<string>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        while (values.Count < 2)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
            if (record is null)
                break;
            values.Add(record.Value.Value);
        }

        await Assert.That(values).IsEquivalentTo(["first", "second"]);
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(new TopicPartition(topic, 0), cts.Token);
        // Two records and two commit markers: nothing else was written.
        await Assert.That(watermarks.High).IsEqualTo(4);
    }

    [Test]
    public async Task Transaction_Abort_MessagesNotVisible()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-abort-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();

        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "abort-key",
                Value = "abort-value"
            }, CancellationToken.None);

            await txn.AbortAsync();
        }

        // Now produce a committed message to verify the consumer works
        await using (var txn2 = producer.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "committed-key",
                Value = "committed-value"
            }, CancellationToken.None);

            await txn2.CommitAsync();
        }

        // Consume with read_committed - should only see the committed message
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("committed-value");
    }

    /// <summary>
    /// The abort settles every record of the transaction before EndTxn(abort): records still
    /// buffered in the accumulator (a long linger keeps them there unless the sender drains them
    /// early) fail with TransactionAborted and are never sent, and records already sent are
    /// answered first, so none of them joins the next transaction. The next transaction then
    /// commits under the epoch the abort left, from sequence 0 (a stale sequence or epoch would be
    /// rejected by the broker), and a read_committed consumer sees exactly its records.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task Transaction_AbortWithBufferedRecords_FailsThemAndTheNextTransactionCommitsOnlyItsOwn(
        CancellationToken cancellationToken)
    {
        const int AbortedCount = 20;
        const int CommittedCount = 5;
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"txn-abort-drain-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLinger(TimeSpan.FromSeconds(30))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        await producer.InitTransactionsAsync(cancellationToken);

        var abortedProduces = new Task<RecordMetadata>[AbortedCount];
        await using (var aborted = producer.BeginTransaction())
        {
            for (var i = 0; i < AbortedCount; i++)
            {
                abortedProduces[i] = aborted.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"aborted-{i}",
                    Value = $"aborted-{i}"
                }, cancellationToken).AsTask();
            }

            await aborted.AbortAsync(cancellationToken);
        }

        // The abort does not return before every record of the transaction is settled: sent and
        // answered (the abort marker then hides it), or failed with TransactionAborted and never
        // sent. Which one depends on whether the sender drained the batch before the abort.
        foreach (var produce in abortedProduces)
        {
            await Assert.That(produce.IsCompleted).IsTrue();
            if (produce.IsFaulted)
            {
                var exception = produce.Exception!.InnerException;
                await Assert.That(exception).IsTypeOf<ProduceException>();
                await Assert.That(((ProduceException)exception!).Kind).IsEqualTo(ProduceErrorKind.TransactionAborted);
            }
        }

        await using (var committed = producer.BeginTransaction())
        {
            var committedProduces = new Task<RecordMetadata>[CommittedCount];
            for (var i = 0; i < CommittedCount; i++)
            {
                committedProduces[i] = committed.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"committed-{i}",
                    Value = $"committed-{i}"
                }, cancellationToken).AsTask();
            }

            await committed.CommitAsync(cancellationToken);
            await Task.WhenAll(committedProduces);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        // Read until the committed records have arrived, then keep reading briefly so a record
        // of the aborted transaction that leaked into the committed one would show up too.
        var values = new List<string>();
        var quietTimeout = TimeSpan.FromSeconds(30);
        while (true)
        {
            var consumed = await consumer.ConsumeOneAsync(quietTimeout, cancellationToken);
            if (consumed is null)
                break;

            values.Add(consumed.Value.Value);
            if (values.Count >= CommittedCount)
                quietTimeout = TimeSpan.FromSeconds(3);
        }

        var expected = Enumerable.Range(0, CommittedCount).Select(i => $"committed-{i}").ToArray();
        await Assert.That(values).IsEquivalentTo(expected);
    }

    /// <summary>
    /// Batches larger than the topic's max.message.bytes are split (KIP-126) while later batches
    /// of the same partition are already in flight. The broker appends none of a rejected batch,
    /// so the batches behind it come back with OUT_OF_ORDER_SEQUENCE_NUMBER; they are resent
    /// behind the split children instead of failing the transaction (#3823). The transaction
    /// commits, and a read_committed consumer sees every record once, in order.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task Transaction_BatchesSplitForTheTopicLimit_CommitsEveryRecordInOrder(
        CancellationToken cancellationToken)
    {
        const int RecordCount = 400;
        var topic = $"txn-split-{Guid.NewGuid():N}";
        await using (var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build())
        {
            await admin.CreateTopicsAsync([
                new NewTopic
                {
                    Name = topic,
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    // Each 64 KiB producer batch is four times the limit, so it is split.
                    Configs = new Dictionary<string, string> { ["max.message.bytes"] = "16384" }
                }
            ], cancellationToken: cancellationToken);
        }

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"txn-split-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithBatchSize(64 * 1024)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        await producer.InitTransactionsAsync(cancellationToken);

        var payload = new string('x', 1_000);
        await using (var transaction = producer.BeginTransaction())
        {
            var produces = new Task<RecordMetadata>[RecordCount];
            for (var i = 0; i < RecordCount; i++)
            {
                produces[i] = transaction.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Value = payload
                }, cancellationToken).AsTask();
            }

            await transaction.CommitAsync(cancellationToken);
            await Task.WhenAll(produces);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        var keys = new List<string>();
        while (keys.Count < RecordCount)
        {
            var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (consumed is null)
                break;

            keys.Add(consumed.Value.Key!);
        }

        var expected = Enumerable.Range(0, RecordCount)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        await Assert.That(keys).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Transaction_ComponentwiseProduce_CommitAndAbortSemantics()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-componentwise-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Aborted componentwise produce must not be visible to read_committed consumers
        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(topic, "aborted-key", "aborted-value", CancellationToken.None);
            await txn.AbortAsync();
        }

        // Committed componentwise produce must round-trip key and value
        await using (var txn2 = producer.BeginTransaction())
        {
            var metadata = await txn2.ProduceAsync(
                topic, "committed-key", "committed-value", CancellationToken.None);
            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

            await txn2.CommitAsync();
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Key).IsEqualTo("committed-key");
        await Assert.That(consumed!.Value.Value).IsEqualTo("committed-value");
    }

    [Test]
    public async Task Transaction_MultipleMessages_AllCommitted()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-multi-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();

        await using (var txn = producer.BeginTransaction())
        {
            for (var i = 0; i < 5; i++)
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                }, CancellationToken.None);
            }

            await txn.CommitAsync();
        }

        // Consume all 5 messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var batch in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(batch.Value);
            if (messages.Count >= 5)
                break;
        }

        await Assert.That(messages).Count().IsEqualTo(5);
    }

    [Test]
    public async Task Transaction_CommitThenBeginAnother_Succeeds()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-multi-txn-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // First transaction
        await using (var txn1 = producer.BeginTransaction())
        {
            await txn1.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "txn1-key",
                Value = "txn1-value"
            }, CancellationToken.None);
            await txn1.CommitAsync();
        }

        // Second transaction
        await using (var txn2 = producer.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "txn2-key",
                Value = "txn2-value"
            }, CancellationToken.None);
            await txn2.CommitAsync();
        }

        // Both messages should be visible
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var batch in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(batch.Value);
            if (messages.Count >= 2)
                break;
        }

        await Assert.That(messages).Count().IsEqualTo(2);
    }
}
