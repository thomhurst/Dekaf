using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for transaction edge cases beyond basic commit/abort.
/// Covers multi-partition atomicity, isolation level differences,
/// sequential transaction ordering, and consume-transform-produce failure scenarios.
/// </summary>
[Category("Transaction")]
public sealed class TransactionEdgeCaseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task MultiPartition_Commit_AllPartitionsVisible()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var txnId = $"txn-multi-partition-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Act - produce to all 3 partitions in a single transaction
        await using (var txn = producer.BeginTransaction())
        {
            for (var p = 0; p < 3; p++)
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-p{p}",
                    Value = $"value-p{p}",
                    Partition = p
                });
            }

            await txn.CommitAsync();
        }

        // Assert - ReadCommitted consumer sees all 3 messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 3) break;
        }

        await Assert.That(messages.Count).IsEqualTo(3);

        var partitions = messages.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 1, 2];
        await Assert.That(partitions).IsEquivalentTo(expectedPartitions);
    }

    [Test]
    public async Task MultiPartition_Abort_NoPartitionsVisible()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var txnId = $"txn-multi-abort-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Abort transaction with messages on all partitions
        await using (var txn = producer.BeginTransaction())
        {
            for (var p = 0; p < 3; p++)
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-aborted-p{p}",
                    Value = $"value-aborted-p{p}",
                    Partition = p
                });
            }

            await txn.AbortAsync();
        }

        // Produce one committed message to verify consumer works
        await using (var txn2 = producer.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "committed",
                Value = "committed-value",
                Partition = 0
            });
            await txn2.CommitAsync();
        }

        // Assert - only the committed message visible
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("committed");
    }

    [Test]
    public async Task ReadUncommitted_SeesAllMessages_IncludingAborted()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-read-uncommitted-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Aborted transaction
        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "aborted-key",
                Value = "aborted-value"
            });
            await txn.AbortAsync();
        }

        // Committed transaction
        await using (var txn2 = producer.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "committed-key",
                Value = "committed-value"
            });
            await txn2.CommitAsync();
        }

        // Act - ReadUncommitted consumer should see both
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-ru-verify-{Guid.NewGuid():N}")
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

        // Assert - ReadUncommitted sees both aborted and committed messages
        await Assert.That(messages.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SequentialTransactions_OffsetsMonotonic()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-sequential-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Act - run 5 sequential transactions
        var offsets = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            await using var txn = producer.BeginTransaction();
            var metadata = await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"seq-key-{i}",
                Value = $"seq-value-{i}"
            });
            await txn.CommitAsync();
            offsets.Add(metadata.Offset);
        }

        // Assert - offsets should be monotonically increasing
        for (var i = 1; i < offsets.Count; i++)
        {
            await Assert.That(offsets[i]).IsGreaterThan(offsets[i - 1]);
        }

        // Verify all visible via ReadCommitted
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-seq-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break;
        }

        await Assert.That(messages.Count).IsEqualTo(5);

        // Verify ordering
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"seq-value-{i}");
        }
    }

    [Test]
    public async Task ConsumeTransformProduce_AbortMidTransaction_OutputTopicEmpty()
    {
        // Arrange
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var outputTopic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"txn-ctp-abort-{Guid.NewGuid():N}";
        var txnId = $"txn-ctp-abort-{Guid.NewGuid():N}";

        // Produce input messages
        await using var sourceProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < 3; i++)
        {
            await sourceProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - consume-transform-produce, but abort after producing to output
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        await using var txnProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await txnProducer.InitTransactionsAsync();
        consumer.Subscribe(inputTopic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var processedCount = 0;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await using var txn = txnProducer.BeginTransaction();

            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = outputTopic,
                Key = msg.Key,
                Value = $"transformed-{msg.Value}"
            });

            // Simulate failure — abort instead of commit
            await txn.AbortAsync();

            processedCount++;
            if (processedCount >= 3) break;
        }

        // Assert - output topic should have no committed messages
        await using var outputConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-ctp-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        outputConsumer.Subscribe(outputTopic);

        using var verifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await outputConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), verifyCts.Token);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TransactionWithConsumerOffsets_AtomicCommit()
    {
        // Arrange
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var outputTopic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"txn-atomic-offset-{Guid.NewGuid():N}";
        var txnId = $"txn-atomic-offset-{Guid.NewGuid():N}";

        await using var sourceProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < 5; i++)
        {
            await sourceProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - consume-transform-produce with SendOffsetsToTransaction
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        await using var txnProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await txnProducer.InitTransactionsAsync();
        consumer.Subscribe(inputTopic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var processedCount = 0;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await using var txn = txnProducer.BeginTransaction();

            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = outputTopic,
                Key = msg.Key,
                Value = $"processed-{msg.Value}"
            });

            // Atomically commit offsets as part of the transaction
            var offsets = new[] { new TopicPartitionOffset(msg.Topic, msg.Partition, msg.Offset + 1) };
            await txn.SendOffsetsToTransactionAsync(offsets, groupId);
            await txn.CommitAsync();

            processedCount++;
            if (processedCount >= 5) break;
        }

        // Assert - output has all 5 transformed messages
        await using var outputConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-out-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        outputConsumer.Subscribe(outputTopic);

        var outputMessages = new List<ConsumeResult<string, string>>();
        using var verifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in outputConsumer.ConsumeAsync(verifyCts.Token))
        {
            outputMessages.Add(msg);
            if (outputMessages.Count >= 5) break;
        }

        await Assert.That(outputMessages.Count).IsEqualTo(5);

        for (var i = 0; i < 5; i++)
        {
            var msg = outputMessages.First(m => m.Key == $"key-{i}");
            await Assert.That(msg.Value).IsEqualTo($"processed-value-{i}");
        }

        // Verify committed offset - new consumer with same group should start from offset 5
        await using var resumeConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        resumeConsumer.Subscribe(inputTopic);

        using var resumeCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var resumeResult = await resumeConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), resumeCts.Token);

        // Should be null — all messages already consumed and committed
        await Assert.That(resumeResult).IsNull();
    }

    [Test]
    public async Task MixedCommitAndAbort_OnlyCommittedVisible()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-mixed-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Act - alternating commit/abort
        for (var i = 0; i < 6; i++)
        {
            await using var txn = producer.BeginTransaction();

            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });

            if (i % 2 == 0)
            {
                await txn.CommitAsync(); // Commit even indices: 0, 2, 4
            }
            else
            {
                await txn.AbortAsync(); // Abort odd indices: 1, 3, 5
            }
        }

        // Assert - only committed messages visible
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-mixed-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Collect messages until timeout — don't break at a specific count
        // because transaction markers affect offset positions
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
            // Expected — consume until timeout
        }

        // Should only have even-indexed messages (committed: 0, 2, 4)
        await Assert.That(messages.Count).IsEqualTo(3);

        var keys = messages.Select(m => m.Key!).OrderBy(k => k).ToList();
        string[] expectedKeys = ["key-0", "key-2", "key-4"];
        await Assert.That(keys).IsEquivalentTo(expectedKeys);
    }
}
