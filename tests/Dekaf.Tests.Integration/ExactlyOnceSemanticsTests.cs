using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Advanced edge case tests for exactly-once semantics (EOS).
/// Covers transactional offset commit atomicity, read-committed consistent snapshots,
/// multi-producer isolation, epoch bumping/fencing, and consume-transform-produce patterns.
/// </summary>
[Category("Transaction")]
public sealed class ExactlyOnceSemanticsTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task TransactionOffsetCommit_Atomicity_OffsetsAndMessagesCommittedTogether()
    {
        // Arrange: Set up input and output topics, produce seed messages
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var outputTopic = await KafkaContainer.CreateTestTopicAsync();
        var consumerGroupId = $"eos-atomic-group-{Guid.NewGuid():N}";
        var txnId = $"eos-atomic-txn-{Guid.NewGuid():N}";
        const int messageCount = 3;

        await using var seedProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await seedProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Key = $"atomic-key-{i}",
                Value = $"atomic-value-{i}"
            });
        }

        // Act: Consume-transform-produce with SendOffsetsToTransaction
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(consumerGroupId)
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

        var processedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await using var txn = txnProducer.BeginTransaction();

            // Produce transformed message to output topic
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = outputTopic,
                Key = msg.Key,
                Value = $"transformed-{msg.Value}"
            });

            // Atomically commit consumer offsets as part of the transaction
            var offsets = new[] { new TopicPartitionOffset(msg.Topic, msg.Partition, msg.Offset + 1) };
            await txn.SendOffsetsToTransactionAsync(offsets, consumerGroupId);
            await txn.CommitAsync();

            processedCount++;
            if (processedCount >= messageCount) break;
        }

        // Assert: Verify output topic has all transformed messages
        await using var outputConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eos-atomic-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        outputConsumer.Subscribe(outputTopic);

        var outputMessages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in outputConsumer.ConsumeAsync(cts2.Token))
        {
            outputMessages.Add(msg);
            if (outputMessages.Count >= messageCount) break;
        }

        await Assert.That(outputMessages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            var msg = outputMessages.First(m => m.Key == $"atomic-key-{i}");
            await Assert.That(msg.Value).IsEqualTo($"transformed-atomic-value-{i}");
        }

        // Assert: Verify consumer offsets were committed atomically --
        // a new consumer with the same group should have no messages left to consume
        await using var resumeConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(consumerGroupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        resumeConsumer.Subscribe(inputTopic);

        using var resumeCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var resumeResult = await resumeConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), resumeCts.Token);

        // Should be null because all offsets were atomically committed with the transaction
        await Assert.That(resumeResult).IsNull();
    }

    [Test]
    public async Task ReadCommitted_ConsistentSnapshot_NoPartialTransactions()
    {
        // Arrange: Produce multiple messages in a single transaction across the same topic.
        // A ReadCommitted consumer must never see a subset of a committed transaction's messages.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"eos-consistent-{Guid.NewGuid():N}";
        const int messagesPerTxn = 5;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Produce batch 1: committed transaction with 5 messages
        await using (var txn1 = producer.BeginTransaction())
        {
            for (var i = 0; i < messagesPerTxn; i++)
            {
                await txn1.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"committed-batch-key-{i}",
                    Value = $"committed-batch-value-{i}"
                });
            }

            await txn1.CommitAsync();
        }

        // Produce batch 2: aborted transaction with 3 messages (should be invisible)
        await using (var txn2 = producer.BeginTransaction())
        {
            for (var i = 0; i < 3; i++)
            {
                await txn2.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"aborted-key-{i}",
                    Value = $"aborted-value-{i}"
                });
            }

            await txn2.AbortAsync();
        }

        // Produce batch 3: another committed transaction with 2 messages
        await using (var txn3 = producer.BeginTransaction())
        {
            for (var i = 0; i < 2; i++)
            {
                await txn3.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"committed2-key-{i}",
                    Value = $"committed2-value-{i}"
                });
            }

            await txn3.CommitAsync();
        }

        // Act: ReadCommitted consumer reads all available messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eos-consistent-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        const int expectedCommittedCount = messagesPerTxn + 2; // 5 from batch 1 + 2 from batch 3
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= expectedCommittedCount) break;
        }

        // Assert: Should see exactly 7 committed messages (5 from batch 1 + 2 from batch 3)
        // None of the 3 aborted messages should be visible
        await Assert.That(messages).Count().IsEqualTo(messagesPerTxn + 2);

        // Verify batch 1 is complete (no partial transaction)
        var batch1Messages = messages.Where(m => m.Key!.StartsWith("committed-batch-key-", StringComparison.Ordinal)).ToList();
        await Assert.That(batch1Messages).Count().IsEqualTo(messagesPerTxn);

        // Verify batch 3 is complete
        var batch3Messages = messages.Where(m => m.Key!.StartsWith("committed2-key-", StringComparison.Ordinal)).ToList();
        await Assert.That(batch3Messages).Count().IsEqualTo(2);

        // Verify no aborted messages leaked through
        var abortedMessages = messages.Where(m => m.Key!.StartsWith("aborted-key-", StringComparison.Ordinal)).ToList();
        await Assert.That(abortedMessages).Count().IsEqualTo(0);
    }

    [Test]
    public async Task MultipleTransactionalProducers_SamePartition_IsolatedCorrectly()
    {
        // Arrange: Two independent transactional producers writing to the same topic/partition.
        // Each producer's transaction should be isolated -- committing one does not affect the other.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId1 = $"eos-iso-producer1-{Guid.NewGuid():N}";
        var txnId2 = $"eos-iso-producer2-{Guid.NewGuid():N}";

        await using var producer1 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId1)
            .WithAcks(Acks.All)
            .BuildAsync();

        await using var producer2 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId2)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer1.InitTransactionsAsync();
        await producer2.InitTransactionsAsync();

        // Act: Producer1 commits, Producer2 aborts -- both writing to same partition
        await using (var txn1 = producer1.BeginTransaction())
        {
            for (var i = 0; i < 3; i++)
            {
                await txn1.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p1-key-{i}",
                    Value = $"p1-committed-{i}",
                    Partition = 0
                });
            }

            await txn1.CommitAsync();
        }

        await using (var txn2 = producer2.BeginTransaction())
        {
            for (var i = 0; i < 3; i++)
            {
                await txn2.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p2-key-{i}",
                    Value = $"p2-aborted-{i}",
                    Partition = 0
                });
            }

            await txn2.AbortAsync();
        }

        // Then Producer2 commits a separate batch
        await using (var txn2b = producer2.BeginTransaction())
        {
            for (var i = 0; i < 2; i++)
            {
                await txn2b.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p2-committed-key-{i}",
                    Value = $"p2-committed-{i}",
                    Partition = 0
                });
            }

            await txn2b.CommitAsync();
        }

        // Assert: ReadCommitted consumer sees only committed messages from both producers
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eos-iso-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        const int expectedCommittedCount = 5; // 3 from producer1 commit + 2 from producer2 second commit
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= expectedCommittedCount) break;
        }

        // Should see 3 from producer1 commit + 2 from producer2 second commit = 5 total
        await Assert.That(messages).Count().IsEqualTo(5);

        // Verify producer1's committed messages
        var p1Messages = messages.Where(m => m.Key!.StartsWith("p1-key-", StringComparison.Ordinal)).ToList();
        await Assert.That(p1Messages).Count().IsEqualTo(3);

        // Verify producer2's aborted messages are NOT present
        var p2AbortedMessages = messages.Where(m => m.Value!.StartsWith("p2-aborted-", StringComparison.Ordinal)).ToList();
        await Assert.That(p2AbortedMessages).Count().IsEqualTo(0);

        // Verify producer2's committed messages ARE present
        var p2CommittedMessages = messages.Where(m => m.Key!.StartsWith("p2-committed-key-", StringComparison.Ordinal)).ToList();
        await Assert.That(p2CommittedMessages).Count().IsEqualTo(2);
    }

    [Test]
    public async Task EpochBump_AfterProducerRestart_OldProducerFenced()
    {
        // Arrange: Create a transactional producer, use it, dispose it, then create a new one
        // with the same transactional.id. The new producer should fence the old one's epoch
        // and be able to produce successfully.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var sharedTxnId = $"eos-fence-{Guid.NewGuid():N}";

        // First producer epoch: produce and commit
        {
            await using var producer1 = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithTransactionalId(sharedTxnId)
                .WithAcks(Acks.All)
                .BuildAsync();

            await producer1.InitTransactionsAsync();

            await using (var txn = producer1.BeginTransaction())
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "epoch1-key",
                    Value = "epoch1-value"
                });
                await txn.CommitAsync();
            }
        }

        // Allow Kafka to release the transactional.id. The broker holds a lock on the
        // transactional.id for a short period after a producer disconnects; without this
        // delay, InitTransactionsAsync on the next producer can fail with a concurrent
        // transactions error. 2 seconds provides margin for CI environments under load.
        await Task.Delay(2000);

        // Second producer with same transactional.id -- bumps epoch, fencing first producer
        await using var producer2 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(sharedTxnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer2.InitTransactionsAsync();

        // The second producer should successfully produce after fencing the first
        await using (var txn2 = producer2.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "epoch2-key",
                Value = "epoch2-value"
            });
            await txn2.CommitAsync();
        }

        // Third producer with same transactional.id -- bumps epoch again
        // (while producer2 is still alive but idle)
        await using var producer3 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(sharedTxnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer3.InitTransactionsAsync();

        await using (var txn3 = producer3.BeginTransaction())
        {
            await txn3.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "epoch3-key",
                Value = "epoch3-value"
            });
            await txn3.CommitAsync();
        }

        // Assert: All three committed messages should be visible
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eos-fence-verify-{Guid.NewGuid():N}")
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

        await Assert.That(messages).Count().IsEqualTo(3);

        var hasEpoch1 = messages.Any(m => m.Key == "epoch1-key" && m.Value == "epoch1-value");
        var hasEpoch2 = messages.Any(m => m.Key == "epoch2-key" && m.Value == "epoch2-value");
        var hasEpoch3 = messages.Any(m => m.Key == "epoch3-key" && m.Value == "epoch3-value");

        await Assert.That(hasEpoch1).IsTrue();
        await Assert.That(hasEpoch2).IsTrue();
        await Assert.That(hasEpoch3).IsTrue();
    }

    [Test]
    public async Task ConsumeTransformProduce_WithExactlyOnce_NoDuplicates()
    {
        // Arrange: Full consume-transform-produce pipeline with exactly-once guarantees.
        // Verifies that even when processing multiple messages, each is transformed exactly once
        // and the output topic contains no duplicates.
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var outputTopic = await KafkaContainer.CreateTestTopicAsync();
        var consumerGroupId = $"eos-ctp-nodup-group-{Guid.NewGuid():N}";
        var txnId = $"eos-ctp-nodup-txn-{Guid.NewGuid():N}";
        const int messageCount = 10;

        // Produce input messages with unique identifiers
        await using var seedProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await seedProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Key = $"input-{i}",
                Value = $"payload-{i}"
            });
        }

        // Act: Consume-transform-produce with per-message transactions and offset commits
        await using var pipelineConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(consumerGroupId)
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
        pipelineConsumer.Subscribe(inputTopic);

        var processedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in pipelineConsumer.ConsumeAsync(cts.Token))
        {
            await using var txn = txnProducer.BeginTransaction();

            // Transform: uppercase the value
            var transformedValue = msg.Value.ToUpperInvariant();

            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = outputTopic,
                Key = msg.Key,
                Value = transformedValue
            });

            // Commit consumer offset atomically with the produced message
            var offsets = new[] { new TopicPartitionOffset(msg.Topic, msg.Partition, msg.Offset + 1) };
            await txn.SendOffsetsToTransactionAsync(offsets, consumerGroupId);
            await txn.CommitAsync();

            processedCount++;
            if (processedCount >= messageCount) break;
        }

        // Assert: Verify output topic has exactly the right number of unique transformed messages
        await using var outputConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eos-ctp-nodup-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        outputConsumer.Subscribe(outputTopic);

        var outputMessages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in outputConsumer.ConsumeAsync(cts2.Token))
        {
            outputMessages.Add(msg);
            if (outputMessages.Count >= messageCount) break;
        }

        // Exactly messageCount messages -- no duplicates, no missing
        await Assert.That(outputMessages).Count().IsEqualTo(messageCount);

        // Verify each message was transformed exactly once
        var uniqueKeys = outputMessages.Select(m => m.Key).Distinct().ToList();
        await Assert.That(uniqueKeys).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            var msg = outputMessages.First(m => m.Key == $"input-{i}");
            await Assert.That(msg.Value).IsEqualTo($"PAYLOAD-{i}");
        }

        // Verify no duplicate values
        var uniqueValues = outputMessages.Select(m => m.Value).Distinct().ToList();
        await Assert.That(uniqueValues).Count().IsEqualTo(messageCount);

        // Verify consumer group offsets are committed -- re-reading with same group yields nothing
        await using var rereadConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(consumerGroupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        rereadConsumer.Subscribe(inputTopic);

        using var rereadCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var rereadResult = await rereadConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), rereadCts.Token);

        await Assert.That(rereadResult).IsNull();
    }
}
