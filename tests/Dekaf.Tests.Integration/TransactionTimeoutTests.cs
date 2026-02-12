using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for transactional producer edge cases: transaction timeouts,
/// multi-topic transactions, isolation level behavior, and failure recovery scenarios.
/// </summary>
[Category("Transaction")]
public sealed class TransactionTimeoutTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Transaction_MultipleTopicsAndPartitions_CommitsAtomically()
    {
        // Arrange - create two topics with different partition counts
        var topic1 = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var topic2 = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var txnId = $"txn-multi-topic-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Act - produce to both topics in a single transaction
        await using (var txn = producer.BeginTransaction())
        {
            // Write 3 messages to topic1
            for (var i = 0; i < 3; i++)
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic1,
                    Key = $"t1-key-{i}",
                    Value = $"t1-value-{i}"
                });
            }

            // Write 3 messages to topic2
            for (var i = 0; i < 3; i++)
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic2,
                    Key = $"t2-key-{i}",
                    Value = $"t2-value-{i}"
                });
            }

            await txn.CommitAsync();
        }

        // Assert - all messages visible on both topics with ReadCommitted
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-multi-topic-c1-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-multi-topic-c2-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer1.Subscribe(topic1);
        consumer2.Subscribe(topic2);

        var topic1Messages = new List<ConsumeResult<string, string>>();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer1.ConsumeAsync(cts1.Token))
        {
            topic1Messages.Add(msg);
            if (topic1Messages.Count >= 3) break;
        }

        var topic2Messages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
        {
            topic2Messages.Add(msg);
            if (topic2Messages.Count >= 3) break;
        }

        await Assert.That(topic1Messages.Count).IsEqualTo(3);
        await Assert.That(topic2Messages.Count).IsEqualTo(3);

        // Verify topic1 messages
        var t1Values = topic1Messages.Select(m => m.Value).OrderBy(v => v).ToList();
        string[] expectedT1 = ["t1-value-0", "t1-value-1", "t1-value-2"];
        await Assert.That(t1Values).IsEquivalentTo(expectedT1);

        // Verify topic2 messages
        var t2Values = topic2Messages.Select(m => m.Value).OrderBy(v => v).ToList();
        string[] expectedT2 = ["t2-value-0", "t2-value-1", "t2-value-2"];
        await Assert.That(t2Values).IsEquivalentTo(expectedT2);
    }

    [Test]
    [Retry(3)]
    public async Task ReadCommitted_DoesNotSee_TimedOutTransactionMessages()
    {
        // Arrange - use a very short transaction timeout so the coordinator aborts
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-timeout-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithTransactionTimeout(TimeSpan.FromSeconds(5))
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Act - begin a transaction, produce messages, but do NOT commit or abort.
        // Let the transaction timeout expire so the coordinator aborts it.
        try
        {
            await using (var txn = producer.BeginTransaction())
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "timeout-key",
                    Value = "timeout-value"
                });

                // Wait for the transaction to time out on the broker side
                await Task.Delay(TimeSpan.FromSeconds(15));

                // The transaction should have been aborted by the coordinator by now.
                // Attempting to commit may throw, but we don't commit - just let it dispose.
            }
        }
        catch
        {
            // The producer may throw when it detects the transaction was timed out/fenced.
            // This is expected behavior.
        }

        // Produce a committed message with a fresh producer to verify the consumer works
        var txnId2 = $"txn-timeout-verify-{Guid.NewGuid():N}";
        await using var producer2 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId2)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer2.InitTransactionsAsync();

        await using (var txn2 = producer2.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "committed-key",
                Value = "committed-value"
            });
            await txn2.CommitAsync();
        }

        // Assert - ReadCommitted consumer should only see the committed message, not the timed-out one
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-timeout-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("committed-value");
    }

    [Test]
    public async Task ReadUncommitted_SeesMessages_FromOpenTransactions()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-uncommitted-open-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Start a transaction and produce a message but do NOT commit or abort yet
        var txn = producer.BeginTransaction();
        try
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "open-txn-key",
                Value = "open-txn-value"
            });

            // Flush to ensure the message is written to the broker
            await producer.FlushAsync();

            // Act - ReadUncommitted consumer should see the uncommitted message
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId($"txn-open-verify-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithIsolationLevel(IsolationLevel.ReadUncommitted)
                .BuildAsync();

            consumer.Subscribe(topic);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

            // Assert - ReadUncommitted sees the message even though transaction is still open
            await Assert.That(consumed).IsNotNull();
            await Assert.That(consumed!.Value.Value).IsEqualTo("open-txn-value");
        }
        finally
        {
            // Clean up - abort the transaction
            await txn.AbortAsync();
            await txn.DisposeAsync();
        }
    }

    [Test]
    public async Task Transaction_ProduceAfterAbort_NewTransactionSucceeds()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-abort-then-new-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Act - first transaction: produce and abort
        await using (var txn1 = producer.BeginTransaction())
        {
            await txn1.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "aborted-key-1",
                Value = "aborted-value-1"
            });

            await txn1.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "aborted-key-2",
                Value = "aborted-value-2"
            });

            await txn1.AbortAsync();
        }

        // Act - second transaction after abort: produce and commit
        await using (var txn2 = producer.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "committed-key-1",
                Value = "committed-value-1"
            });

            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "committed-key-2",
                Value = "committed-value-2"
            });

            await txn2.CommitAsync();
        }

        // Assert - ReadCommitted consumer sees only the committed messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-abort-new-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        await Assert.That(messages.Count).IsEqualTo(2);

        var values = messages.Select(m => m.Value).OrderBy(v => v).ToList();
        string[] expectedValues = ["committed-value-1", "committed-value-2"];
        await Assert.That(values).IsEquivalentTo(expectedValues);

        // Verify no aborted messages leaked through
        var keys = messages.Select(m => m.Key).ToList();
        await Assert.That(keys).DoesNotContain("aborted-key-1");
        await Assert.That(keys).DoesNotContain("aborted-key-2");
    }

    [Test]
    public async Task Transaction_LargeMessageCount_CommitsSuccessfully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var txnId = $"txn-large-batch-{Guid.NewGuid():N}";
        const int messageCount = 150;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Act - produce 150 messages in a single transaction
        await using (var txn = producer.BeginTransaction())
        {
            for (var i = 0; i < messageCount; i++)
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"large-key-{i:D4}",
                    Value = $"large-value-{i:D4}"
                });
            }

            await txn.CommitAsync();
        }

        // Assert - all 150 messages are visible to ReadCommitted consumer
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-large-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages.Count).IsEqualTo(messageCount);

        // Verify all messages are present (order may vary across partitions)
        var receivedValues = messages.Select(m => m.Value).OrderBy(v => v).ToList();
        var expectedValues = Enumerable.Range(0, messageCount)
            .Select(i => $"large-value-{i:D4}")
            .OrderBy(v => v)
            .ToList();

        await Assert.That(receivedValues).IsEquivalentTo(expectedValues);
    }
}
