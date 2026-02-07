using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for exactly-once processing semantics using consume-transform-produce with transactions.
/// </summary>
public sealed class ExactlyOnceProcessingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ConsumeTransformProduce_WithTransactionalOffsetCommit_ExactlyOnceSemantics()
    {
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var outputTopic = await KafkaContainer.CreateTestTopicAsync();
        var consumerGroupId = $"eo-ctp-group-{Guid.NewGuid():N}";
        var txnId = $"eo-ctp-txn-{Guid.NewGuid():N}";
        const int messageCount = 5;

        // Produce input messages
        await using var sourceProducer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < messageCount; i++)
        {
            await sourceProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Key = $"order-{i}",
                Value = $"{{\"orderId\":{i},\"amount\":{(i + 1) * 10.50}}}"
            });
        }

        // Consume-transform-produce pipeline with transactional offset commit
        await using var pipelineConsumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(consumerGroupId)
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .Build();

        await using var txnProducer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .Build();

        await txnProducer.InitTransactionsAsync();

        pipelineConsumer.Subscribe(inputTopic);

        var processedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in pipelineConsumer.ConsumeAsync(cts.Token))
        {
            await using var txn = txnProducer.BeginTransaction();

            // Transform
            var transformed = $"{{\"original\":{msg.Value},\"processed\":true}}";

            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = outputTopic,
                Key = msg.Key,
                Value = transformed
            });

            // Commit consumer offsets as part of the transaction
            var offsets = new[]
            {
                new TopicPartitionOffset(msg.Topic, msg.Partition, msg.Offset + 1)
            };
            await txn.SendOffsetsToTransactionAsync(offsets, consumerGroupId);
            await txn.CommitAsync();

            processedCount++;
            if (processedCount >= messageCount) break;
        }

        // Verify output topic has all transformed messages
        await using var outputConsumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eo-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .Build();

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
            var msg = outputMessages.First(m => m.Key == $"order-{i}");
            await Assert.That(msg.Value).Contains("\"processed\":true");
        }
    }

    [Test]
    public async Task AbortDuringProcessing_ReadCommittedConsumerSeesOnlyCommitted()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"eo-abort-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .Build();

        await producer.InitTransactionsAsync();

        // First transaction: abort
        await using (var txn1 = producer.BeginTransaction())
        {
            await txn1.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "aborted-key",
                Value = "aborted-value"
            });
            await txn1.AbortAsync();
        }

        // Second transaction: commit
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

        // ReadCommitted consumer should only see committed message
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eo-abort-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("committed-key");
        await Assert.That(result.Value.Value).IsEqualTo("committed-value");
    }

    [Test]
    public async Task ZombieFencing_SecondProducerFencesFirst()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"eo-fence-{Guid.NewGuid():N}";

        // First producer with transactional ID — produce and dispose before creating second
        {
            await using var producer1 = Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithTransactionalId(txnId)
                .WithAcks(Acks.All)
                .Build();

            await producer1.InitTransactionsAsync();

            await using (var txn = producer1.BeginTransaction())
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "p1-key",
                    Value = "p1-value"
                });
                await txn.CommitAsync();
            }
        }

        // Brief delay to allow Kafka to release the transactional ID
        await Task.Delay(1000).ConfigureAwait(false);

        // Second producer with same transactional ID - fences first
        await using var producer2 = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .Build();

        await producer2.InitTransactionsAsync();

        // Producer2 should be able to produce successfully
        await using (var txn = producer2.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "p2-key",
                Value = "p2-value"
            });
            await txn.CommitAsync();
        }

        // Verify both messages are visible
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eo-fence-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages.Any(m => m.Key == "p1-key")).IsTrue();
        await Assert.That(messages.Any(m => m.Key == "p2-key")).IsTrue();
    }
}
