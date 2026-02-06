using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for concurrent access patterns commonly seen in production applications.
/// Validates thread-safety of producers and consumers under realistic contention.
/// </summary>
public sealed class ConcurrentAccessPatternTests(KafkaTestContainer kafka) : KafkaIntegrationTest
{
    [Test]
    public async Task SharedProducer_MultipleTasks_AllMessagesDelivered()
    {
        // Common pattern: single producer shared across multiple async tasks
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);
        const int taskCount = 10;
        const int messagesPerTask = 20;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        var allResults = new ConcurrentBag<RecordMetadata>();
        var errors = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, taskCount).Select(taskId => Task.Run(async () =>
        {
            for (var i = 0; i < messagesPerTask; i++)
            {
                try
                {
                    var result = await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"task-{taskId}",
                        Value = $"task-{taskId}-msg-{i}"
                    });
                    allResults.Add(result);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(errors).IsEmpty();
        await Assert.That(allResults).Count().IsEqualTo(taskCount * messagesPerTask);
    }

    [Test]
    public async Task SharedProducer_MixedFireAndForgetAndAwait_AllDelivered()
    {
        // Real-world: some code paths await, others fire-and-forget
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        const int fireAndForgetCount = 20;
        const int awaitedCount = 20;

        // Fire-and-forget from one task
        var ffTask = Task.Run(() =>
        {
            for (var i = 0; i < fireAndForgetCount; i++)
            {
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"ff-{i}",
                    Value = $"fire-and-forget-{i}"
                });
            }
        });

        // Awaited from another task
        var awaitedResults = new ConcurrentBag<RecordMetadata>();
        var awaitTask = Task.Run(async () =>
        {
            for (var i = 0; i < awaitedCount; i++)
            {
                var result = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"awaited-{i}",
                    Value = $"awaited-value-{i}"
                });
                awaitedResults.Add(result);
            }
        });

        await Task.WhenAll(ffTask, awaitTask);
        await producer.FlushAsync();

        await Assert.That(awaitedResults).Count().IsEqualTo(awaitedCount);

        // Verify all messages arrived
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"mixed-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var totalExpected = fireAndForgetCount + awaitedCount;
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= totalExpected) break;
        }

        await Assert.That(messages).Count().IsEqualTo(totalExpected);
    }

    [Test]
    public async Task MultipleTopicProducers_SharedBaseProducer_IndependentTopics()
    {
        // Pattern: one base producer, multiple topic-specific producers for different services
        var ordersTopic = await kafka.CreateTestTopicAsync();
        var eventsTopic = await kafka.CreateTestTopicAsync();
        var metricsTopic = await kafka.CreateTestTopicAsync();

        await using var baseProducer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        var ordersProducer = baseProducer.ForTopic(ordersTopic);
        var eventsProducer = baseProducer.ForTopic(eventsTopic);
        var metricsProducer = baseProducer.ForTopic(metricsTopic);

        // Concurrent production to different topics
        var tasks = new[]
        {
            Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                    await ordersProducer.ProduceAsync($"order-{i}", $"order-data-{i}");
            }),
            Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                    await eventsProducer.ProduceAsync($"event-{i}", $"event-data-{i}");
            }),
            Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                    await metricsProducer.ProduceAsync($"metric-{i}", $"metric-data-{i}");
            })
        };

        await Task.WhenAll(tasks);

        // Verify each topic got 10 messages
        await VerifyTopicMessageCount(ordersTopic, 10);
        await VerifyTopicMessageCount(eventsTopic, 10);
        await VerifyTopicMessageCount(metricsTopic, 10);
    }

    [Test]
    public async Task MultipleProducers_SameTopic_NoResponseCrossContamination()
    {
        // Multiple independent producers writing to the same topic
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);
        const int producerCount = 3;
        const int messagesPerProducer = 30;

        var producers = new List<IKafkaProducer<string, string>>();
        for (var i = 0; i < producerCount; i++)
        {
            producers.Add(Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithClientId($"concurrent-producer-{i}")
                .Build());
        }

        try
        {
            var results = new ConcurrentDictionary<int, List<RecordMetadata>>();

            var tasks = Enumerable.Range(0, producerCount).Select(producerId => Task.Run(async () =>
            {
                var producerResults = new List<RecordMetadata>();
                for (var i = 0; i < messagesPerProducer; i++)
                {
                    var result = await producers[producerId].ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"producer-{producerId}-key-{i}",
                        Value = $"producer-{producerId}-value-{i}"
                    });
                    producerResults.Add(result);
                }

                results[producerId] = producerResults;
            })).ToArray();

            await Task.WhenAll(tasks);

            // All results should reference the same topic
            var totalMessages = results.Values.Sum(r => r.Count);
            await Assert.That(totalMessages).IsEqualTo(producerCount * messagesPerProducer);

            foreach (var (_, producerResults) in results)
            {
                foreach (var result in producerResults)
                {
                    await Assert.That(result.Topic).IsEqualTo(topic);
                }
            }
        }
        finally
        {
            foreach (var producer in producers)
            {
                await producer.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task ProducerAndConsumer_ConcurrentOperation_NoInterference()
    {
        // Produce and consume happening simultaneously on the same topic
        var topic = await kafka.CreateTestTopicAsync();
        const int messageCount = 50;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"concurrent-op-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var consumed = new ConcurrentBag<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Start consumer in background
        var consumeTask = Task.Run(async () =>
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                consumed.Add(msg);
                if (consumed.Count >= messageCount) break;
            }
        });

        // Small delay to let consumer join group
        await Task.Delay(2000);

        // Produce concurrently
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"concurrent-{i}",
                Value = $"concurrent-value-{i}"
            });
        }

        await consumeTask;

        await Assert.That(consumed).Count().IsEqualTo(messageCount);
    }

    [Test]
    public async Task CallbackBasedProduction_ConcurrentCallbacks_ThreadSafe()
    {
        // Verify callbacks work correctly under concurrent load
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);
        const int messageCount = 100;
        var delivered = new ConcurrentBag<RecordMetadata>();
        var deliveryErrors = new ConcurrentBag<Exception>();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        // Send all messages with callbacks
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(
                new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"callback-{i}",
                    Value = $"callback-value-{i}"
                },
                (metadata, error) =>
                {
                    if (error is not null)
                        deliveryErrors.Add(error);
                    else
                        delivered.Add(metadata);
                });
        }

        await producer.FlushAsync();

        // Wait for callbacks to complete
        await Task.Delay(1000);

        await Assert.That(deliveryErrors).IsEmpty();
        await Assert.That(delivered).Count().IsEqualTo(messageCount);

        // Verify all have correct topic
        foreach (var meta in delivered)
        {
            await Assert.That(meta.Topic).IsEqualTo(topic);
        }
    }

    [Test]
    public async Task MultipleConsumersSameGroup_SharedPartitions_AllMessagesConsumed()
    {
        // Two consumers in the same group share partitions
        var topic = await kafka.CreateTestTopicAsync(partitions: 4);
        var groupId = $"shared-group-{Guid.NewGuid():N}";
        const int messageCount = 40;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        // Produce to all partitions
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        var allConsumed = new ConcurrentBag<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Two consumers in same group
        var consumer1Task = Task.Run(async () =>
        {
            await using var consumer = Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .Build();

            consumer.Subscribe(topic);

            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                allConsumed.Add(msg);
                if (allConsumed.Count >= messageCount) break;
            }
        });

        var consumer2Task = Task.Run(async () =>
        {
            await using var consumer = Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .Build();

            consumer.Subscribe(topic);

            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                allConsumed.Add(msg);
                if (allConsumed.Count >= messageCount) break;
            }
        });

        // Wait for either to signal completion or timeout
        await Task.WhenAny(
            Task.WhenAll(consumer1Task, consumer2Task),
            Task.Delay(TimeSpan.FromSeconds(45)));

        cts.Cancel();

        // Both consumers together should have consumed all messages
        // (some may be duplicated during rebalance, but at least messageCount should exist)
        await Assert.That(allConsumed.Count).IsGreaterThanOrEqualTo(messageCount);

        // Verify no duplicates by offset+partition
        var uniqueMessages = allConsumed
            .Select(m => (m.Partition, m.Offset))
            .Distinct()
            .Count();

        await Assert.That(uniqueMessages).IsEqualTo(messageCount);
    }

    private async Task VerifyTopicMessageCount(string topic, int expectedCount)
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= expectedCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(expectedCount);
    }
}
