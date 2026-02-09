using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests comparing ReadCommitted vs ReadUncommitted isolation levels
/// across transactional scenarios, including edge cases around open transactions,
/// concurrent commit/abort, abort markers, and last stable offset blocking.
/// </summary>
[Category("Transaction")]
public sealed class TransactionIsolationLevelTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ReadUncommitted_SeesMessages_FromOpenTransactions()
    {
        // Arrange - produce messages in a transaction but do NOT commit or abort
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-open-ru-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Begin a transaction and produce messages but leave it open
        var txn = producer.BeginTransaction();

        await txn.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "open-txn-key-1",
            Value = "open-txn-value-1"
        });

        await txn.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "open-txn-key-2",
            Value = "open-txn-value-2"
        });

        // Act - ReadUncommitted consumer should see these uncommitted messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-open-ru-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadUncommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        // Assert - ReadUncommitted sees messages from open (uncommitted) transaction
        await Assert.That(messages.Count).IsEqualTo(2);
        await Assert.That(messages[0].Value).IsEqualTo("open-txn-value-1");
        await Assert.That(messages[1].Value).IsEqualTo("open-txn-value-2");

        // Clean up the open transaction
        await txn.AbortAsync();
        await txn.DisposeAsync();
    }

    [Test]
    public async Task ReadCommitted_DoesNotSee_OpenTransactionMessages()
    {
        // Arrange - produce messages in a transaction but do NOT commit or abort
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-open-rc-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Begin a transaction and produce messages but leave it open
        var txn = producer.BeginTransaction();

        await txn.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "open-txn-key",
            Value = "open-txn-value"
        });

        // Act - ReadCommitted consumer should NOT see the uncommitted messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-open-rc-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token);

        // Assert - ReadCommitted does not see messages from the open transaction
        await Assert.That(result).IsNull();

        // Clean up
        await txn.AbortAsync();
        await txn.DisposeAsync();
    }

    [Test]
    public async Task ConcurrentCommitAndAbort_ReadCommitted_SeesOnlyCommitted()
    {
        // Arrange - two separate transactional producers: one will commit, one will abort
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var commitTxnId = $"txn-commit-concurrent-{Guid.NewGuid():N}";
        var abortTxnId = $"txn-abort-concurrent-{Guid.NewGuid():N}";

        await using var commitProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(commitTxnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await using var abortProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(abortTxnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await commitProducer.InitTransactionsAsync();
        await abortProducer.InitTransactionsAsync();

        // Act - start both transactions concurrently
        await using var commitTxn = commitProducer.BeginTransaction();
        await using var abortTxn = abortProducer.BeginTransaction();

        // Produce messages from both transactions
        for (var i = 0; i < 3; i++)
        {
            await commitTxn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"committed-key-{i}",
                Value = $"committed-value-{i}"
            });

            await abortTxn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"aborted-key-{i}",
                Value = $"aborted-value-{i}"
            });
        }

        // Commit one, abort the other
        await commitTxn.CommitAsync();
        await abortTxn.AbortAsync();

        // Assert - ReadCommitted consumer should only see committed messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-concurrent-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                messages.Add(msg);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - consume until timeout
        }

        // Should only see the 3 committed messages, not the 3 aborted ones
        await Assert.That(messages.Count).IsEqualTo(3);

        var keys = messages.Select(m => m.Key!).OrderBy(k => k).ToList();
        string[] expectedKeys = ["committed-key-0", "committed-key-1", "committed-key-2"];
        await Assert.That(keys).IsEquivalentTo(expectedKeys);
    }

    [Test]
    public async Task ReadCommitted_SkipsAbortedTransactionMarkers()
    {
        // Arrange - create a topic, produce several aborted transactions, then one committed
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-abort-markers-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Produce multiple aborted transactions to generate abort markers
        for (var i = 0; i < 5; i++)
        {
            await using var abortTxn = producer.BeginTransaction();
            await abortTxn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"aborted-key-{i}",
                Value = $"aborted-value-{i}"
            });
            await abortTxn.AbortAsync();
        }

        // Now produce one committed transaction after all the abort markers
        await using (var commitTxn = producer.BeginTransaction())
        {
            await commitTxn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "final-committed-key",
                Value = "final-committed-value"
            });
            await commitTxn.CommitAsync();
        }

        // Act - ReadCommitted consumer should skip all abort markers and see only the committed message
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-markers-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                messages.Add(msg);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - consume until timeout
        }

        // Assert - consumer advanced past all abort markers and only received the committed message
        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0].Key).IsEqualTo("final-committed-key");
        await Assert.That(messages[0].Value).IsEqualTo("final-committed-value");
    }

    [Test]
    public async Task LongRunningOpenTransaction_BlocksReadCommittedProgress()
    {
        // Arrange - produce a committed message, then start an open transaction,
        // then produce another committed message after the open transaction.
        // ReadCommitted should see the first committed message but NOT the second
        // because the open transaction's offset blocks progress (last stable offset).
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var longTxnId = $"txn-long-running-{Guid.NewGuid():N}";
        var otherTxnId = $"txn-other-{Guid.NewGuid():N}";

        await using var longProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(longTxnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await using var otherProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(otherTxnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await longProducer.InitTransactionsAsync();
        await otherProducer.InitTransactionsAsync();

        // Step 1: Produce and commit a message before the open transaction
        await using (var earlyTxn = otherProducer.BeginTransaction())
        {
            await earlyTxn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "early-committed-key",
                Value = "early-committed-value"
            });
            await earlyTxn.CommitAsync();
        }

        // Step 2: Start a long-running open transaction (do not commit or abort)
        var longTxn = longProducer.BeginTransaction();
        await longTxn.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "long-running-key",
            Value = "long-running-value"
        });

        // Step 3: Produce and commit another message AFTER the open transaction's messages
        await using (var laterTxn = otherProducer.BeginTransaction())
        {
            await laterTxn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "later-committed-key",
                Value = "later-committed-value"
            });
            await laterTxn.CommitAsync();
        }

        // Act - ReadCommitted consumer: the open transaction's offset (last stable offset)
        // should block the consumer from reading past it, so only the early committed
        // message (which is before the open transaction) should be visible.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-lso-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                messages.Add(msg);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - consume until timeout
        }

        // Assert - only the early committed message is visible; the later committed message
        // is blocked by the open transaction's last stable offset
        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0].Key).IsEqualTo("early-committed-key");
        await Assert.That(messages[0].Value).IsEqualTo("early-committed-value");

        // Clean up the open transaction
        await longTxn.AbortAsync();
        await longTxn.DisposeAsync();
    }
}
