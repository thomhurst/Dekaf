using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the Kafka producer.
/// </summary>
[Category("Producer")]
public class ProducerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Producer_ProduceWithAcksAll_SuccessfullyProduces()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-acks-all")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceWithAcksOne_SuccessfullyProduces()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-acks-one")
            .WithAcks(Acks.Leader)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceWithAcksNone_SuccessfullyProduces()
    {
        // With acks=0, Kafka doesn't send a response, so this is fire-and-forget.
        // The producer returns immediately without waiting for broker acknowledgment.
        // Offset will be -1 since we don't receive it from the broker.

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-acks-none")
            .WithAcks(Acks.None)
            .WithIdempotence(false)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert - topic and partition are known, but offset is -1 for fire-and-forget
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsEqualTo(-1); // Unknown for acks=0

        // Verify the message was actually produced by consuming it
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-verify")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That(msg.Key).IsEqualTo("key1");
            await Assert.That(msg.Value).IsEqualTo("value1");
            break;
        }
    }

    [Test]
    public async Task Producer_ProduceWithNullKey_SuccessfullyProduces()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-null-key")
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = null,
            Value = "value-without-key"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceWithHeaders_SuccessfullyProduces()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-headers")
            .BuildAsync();

        var headers = new Headers
        {
            { "header1", "value1"u8.ToArray() },
            { "header2", "value2"u8.ToArray() }
        };

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1",
            Headers = headers
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceToSpecificPartition_SuccessfullyProduces()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-partition")
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1",
            Partition = 1
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsEqualTo(1);
    }

    [Test]
    public async Task Producer_ProduceMultipleMessages_AllSucceed()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 10;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-batch")
            .BuildAsync();

        // Act
        var tasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in tasks)
        {
            results.Add(await task);
        }

        // Assert
        await Assert.That(results).Count().IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Producer_ProduceWithCustomTimestamp_SuccessfullyProduces()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var timestamp = DateTimeOffset.UtcNow.AddHours(-1);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-timestamp")
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1",
            Timestamp = timestamp
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceLargeMessage_SuccessfullyProduces()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var largeValue = new string('x', 100_000); // 100KB message

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-large")
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "large-key",
            Value = largeValue
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceEmptyValue_SuccessfullyProduces()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-empty")
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = string.Empty
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_Flush_WaitsForPendingMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-flush")
            .WithLinger(TimeSpan.FromMilliseconds(1000)) // Long linger to test flush
            .BuildAsync();

        // Act - produce without awaiting
        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Flush should complete the pending produce
        await producer.FlushAsync();
        var metadata = await produceTask;

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithStickyPartitioner_DistributesMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-sticky")
            .WithPartitioner(PartitionerType.Sticky)
            .BuildAsync();

        // Act - produce messages without keys (should use sticky partitioner)
        var tasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = null,
                Value = $"value-{i}"
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in tasks)
        {
            results.Add(await task);
        }

        // Assert - all messages should go to same partition (sticky)
        await Assert.That(results).Count().IsEqualTo(10);
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        // With sticky partitioner and quick produces, should mostly go to same partition
        await Assert.That(partitions.Count).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task Producer_WithRoundRobinPartitioner_DistributesMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-roundrobin")
            .WithPartitioner(PartitionerType.RoundRobin)
            .BuildAsync();

        // Act - produce messages without keys
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 9; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = null,
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }

        // Assert - should distribute across partitions
        var partitionCounts = results.GroupBy(r => r.Partition).ToDictionary(g => g.Key, g => g.Count());
        await Assert.That(partitionCounts.Count).IsEqualTo(3);
        // Each partition should have 3 messages
        foreach (var count in partitionCounts.Values)
        {
            await Assert.That(count).IsEqualTo(3);
        }
    }

    [Test]
    public async Task Producer_ConcurrentProducesToSameTopic_AllMessagesDelivered()
    {
        // Test high-contention scenario: multiple threads producing to the same topic
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        const int threadCount = 10;
        const int messagesPerThread = 50;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-concurrent-same-topic")
            .WithAcks(Acks.All)
            .BuildAsync();

        var allResults = new System.Collections.Concurrent.ConcurrentBag<RecordMetadata>();
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Launch multiple threads producing concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            for (var i = 0; i < messagesPerThread; i++)
            {
                try
                {
                    var result = await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"thread-{threadId}-key-{i}",
                        Value = $"thread-{threadId}-value-{i}"
                    });
                    allResults.Add(result);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        await Assert.That(errors).Count().IsEqualTo(0).Because($"No errors should occur, but got: {string.Join("; ", errors.Take(5).Select(e => e.Message))}");
        await Assert.That(allResults).Count().IsEqualTo(threadCount * messagesPerThread);

        // Verify all results reference the correct topic
        foreach (var result in allResults)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Producer_ConcurrentProducersToDifferentTopics_ResponsesMatchRequests()
    {
        // Test isolation: multiple producers to different topics should never get mixed responses
        const int producerCount = 5;
        const int messagesPerProducer = 20;

        var topics = new string[producerCount];
        var producers = new IKafkaProducer<string, string>[producerCount];

        // Create topics and producers
        for (var i = 0; i < producerCount; i++)
        {
            topics[i] = await KafkaContainer.CreateTestTopicAsync();
            producers[i] = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId($"test-producer-isolation-{i}")
                .WithAcks(Acks.All)
                .BuildAsync();
        }

        try
        {
            var allResults = new System.Collections.Concurrent.ConcurrentDictionary<int, List<RecordMetadata>>();
            var errors = new System.Collections.Concurrent.ConcurrentBag<(int ProducerId, string ExpectedTopic, string ActualTopic)>();

            // Launch all producers concurrently
            var tasks = Enumerable.Range(0, producerCount).Select(async producerId =>
            {
                var results = new List<RecordMetadata>();
                var expectedTopic = topics[producerId];
                var producer = producers[producerId];

                for (var i = 0; i < messagesPerProducer; i++)
                {
                    var result = await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = expectedTopic,
                        Key = $"producer-{producerId}-key-{i}",
                        Value = $"producer-{producerId}-value-{i}"
                    });

                    // Critical check: response topic must match what we sent
                    if (result.Topic != expectedTopic)
                    {
                        errors.Add((producerId, expectedTopic, result.Topic));
                    }

                    results.Add(result);
                }

                allResults[producerId] = results;
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert no response mismatches
            await Assert.That(errors).IsEmpty().Because(
                $"Response topic mismatches: {string.Join("; ", errors.Take(5).Select(e => $"Producer {e.ProducerId}: expected '{e.ExpectedTopic}', got '{e.ActualTopic}'"))}");

            // Verify all producers got all their messages
            for (var i = 0; i < producerCount; i++)
            {
                await Assert.That(allResults[i]).Count().IsEqualTo(messagesPerProducer);
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
    public async Task Producer_HighThroughput_HandlesLoadWithoutErrors()
    {
        // Stress test: high message volume from single producer
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);
        const int totalMessages = 1000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-high-throughput")
            .WithAcks(Acks.Leader) // Faster acks for throughput test
            .WithLinger(TimeSpan.FromMilliseconds(5)) // Small linger for batching
            .WithBatchSize(65536) // Larger batches
            .BuildAsync();

        var pendingTasks = new List<ValueTask<RecordMetadata>>(totalMessages);
        var errors = new List<Exception>();

        // Fire all messages as fast as possible
        for (var i = 0; i < totalMessages; i++)
        {
            pendingTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i:D6}"
            }));
        }

        // Await all results
        var results = new List<RecordMetadata>(totalMessages);
        foreach (var task in pendingTasks)
        {
            try
            {
                results.Add(await task);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        // Assert
        await Assert.That(errors).Count().IsEqualTo(0).Because($"Errors: {string.Join("; ", errors.Take(5).Select(e => e.Message))}");
        await Assert.That(results).Count().IsEqualTo(totalMessages);

        // All results should have valid offsets and correct topic
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Producer_BurstTraffic_RecoversBetweenBursts()
    {
        // Test burst traffic pattern: bursts followed by idle periods
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        const int burstCount = 5;
        const int messagesPerBurst = 100;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-burst")
            .WithAcks(Acks.All)
            .BuildAsync();

        var allResults = new List<RecordMetadata>();

        for (var burst = 0; burst < burstCount; burst++)
        {
            var burstTasks = new List<ValueTask<RecordMetadata>>();

            // Send burst of messages
            for (var i = 0; i < messagesPerBurst; i++)
            {
                burstTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"burst-{burst}-key-{i}",
                    Value = $"burst-{burst}-value-{i}"
                }));
            }

            // Wait for all burst messages
            foreach (var task in burstTasks)
            {
                var result = await task;
                await Assert.That(result.Topic).IsEqualTo(topic);
                allResults.Add(result);
            }

            // Short idle period between bursts
            await Task.Delay(50);
        }

        await Assert.That(allResults).Count().IsEqualTo(burstCount * messagesPerBurst);
    }

    [Test]
    public async Task Producer_ParallelProducersSharedBootstrap_NoResponseCrossContamination()
    {
        // Critical test: verify responses never get mixed between producers sharing same broker
        const int iterations = 10;
        const int messagesPerIteration = 10;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var topicA = await KafkaContainer.CreateTestTopicAsync();
            var topicB = await KafkaContainer.CreateTestTopicAsync();

            await using var producerA = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId($"producer-a-{iteration}")
                .WithAcks(Acks.All)
                .BuildAsync();

            await using var producerB = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId($"producer-b-{iteration}")
                .WithAcks(Acks.All)
                .BuildAsync();

            var tasksA = new List<ValueTask<RecordMetadata>>();
            var tasksB = new List<ValueTask<RecordMetadata>>();

            // Interleave messages from both producers
            for (var i = 0; i < messagesPerIteration; i++)
            {
                tasksA.Add(producerA.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topicA,
                    Key = $"a-key-{i}",
                    Value = $"a-value-{i}"
                }));

                tasksB.Add(producerB.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topicB,
                    Key = $"b-key-{i}",
                    Value = $"b-value-{i}"
                }));
            }

            // Verify producer A results all have topicA
            foreach (var task in tasksA)
            {
                var result = await task;
                await Assert.That(result.Topic).IsEqualTo(topicA)
                    .Because($"Producer A should only receive responses for topicA, but got {result.Topic}");
            }

            // Verify producer B results all have topicB
            foreach (var task in tasksB)
            {
                var result = await task;
                await Assert.That(result.Topic).IsEqualTo(topicB)
                    .Because($"Producer B should only receive responses for topicB, but got {result.Topic}");
            }
        }
    }

    [Test]
    public async Task Producer_MinimalCrossContaminationTest_SingleIteration()
    {
        // Minimal test: just two producers, one message each
        var topicA = await KafkaContainer.CreateTestTopicAsync();
        var topicB = await KafkaContainer.CreateTestTopicAsync();

        await using var producerA = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("producer-a-minimal")
            .WithAcks(Acks.All)
            .BuildAsync();

        await using var producerB = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("producer-b-minimal")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Send one message from each producer concurrently
        var taskA = producerA.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topicA,
            Key = "a-key",
            Value = "a-value"
        });

        var taskB = producerB.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topicB,
            Key = "b-key",
            Value = "b-value"
        });

        var resultA = await taskA;
        var resultB = await taskB;

        await Assert.That(resultA.Topic).IsEqualTo(topicA)
            .Because($"Producer A should receive response for topicA, but got {resultA.Topic}");
        await Assert.That(resultB.Topic).IsEqualTo(topicB)
            .Because($"Producer B should receive response for topicB, but got {resultB.Topic}");
    }

    [Test]
    public async Task Producer_SequentialProducers_NoContamination()
    {
        // Test without any concurrency - should always pass
        var topicA = await KafkaContainer.CreateTestTopicAsync();
        var topicB = await KafkaContainer.CreateTestTopicAsync();

        await using var producerA = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("producer-a-sequential")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Send from producer A first, wait for result
        var resultA = await producerA.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topicA,
            Key = "a-key",
            Value = "a-value"
        });

        await Assert.That(resultA.Topic).IsEqualTo(topicA);

        // Now create producer B and send
        await using var producerB = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("producer-b-sequential")
            .WithAcks(Acks.All)
            .BuildAsync();

        var resultB = await producerB.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topicB,
            Key = "b-key",
            Value = "b-value"
        });

        await Assert.That(resultB.Topic).IsEqualTo(topicB);
    }

    [Test]
    public async Task Producer_SynchronousProduce_SuccessfullyProducesMessage()
    {
        // Test fire-and-forget synchronous produce
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-sync")
            .WithAcks(Acks.Leader)
            .BuildAsync();

        // Act - fire-and-forget send
        producer.Send(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "sync-key",
            Value = "sync-value"
        });

        // Flush to ensure delivery
        await producer.FlushAsync();

        // Verify by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-sync-verify")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That(msg.Key).IsEqualTo("sync-key");
            await Assert.That(msg.Value).IsEqualTo("sync-value");
            break;
        }
    }

    [Test]
    public async Task Producer_SynchronousProduceWithCallback_InvokesCallbackOnSuccess()
    {
        // Test synchronous produce with delivery callback
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var callbackInvoked = new TaskCompletionSource<(RecordMetadata Metadata, Exception? Error)>();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-callback")
            .WithAcks(Acks.Leader)
            .BuildAsync();

        // Act - send with callback
        producer.Send(
            new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "callback-key",
                Value = "callback-value"
            },
            (metadata, error) => callbackInvoked.TrySetResult((metadata, error)));

        // Wait for callback
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => callbackInvoked.TrySetCanceled());

        var (resultMetadata, resultError) = await callbackInvoked.Task;

        // Assert
        await Assert.That(resultError).IsNull();
        await Assert.That(resultMetadata.Topic).IsEqualTo(topic);
        await Assert.That(resultMetadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_SynchronousProduceMultiple_FlushDeliverAll()
    {
        // Test multiple fire-and-forget produces followed by flush
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 100;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-sync-batch")
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .BuildAsync();

        // Act - fire-and-forget multiple messages
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Flush to ensure all delivered
        await producer.FlushAsync();

        // Verify by consuming all messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-sync-batch-verify")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumedCount++;
            if (consumedCount >= messageCount)
                break;
        }

        await Assert.That(consumedCount).IsEqualTo(messageCount);
    }

    [Test]
    public async Task Producer_SynchronousProduceConcurrent_ThreadSafe()
    {
        // Test thread-safety of synchronous produce
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        const int threadCount = 10;
        const int messagesPerThread = 50;
        var deliveredCount = 0;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-sync-concurrent")
            .WithAcks(Acks.Leader)
            .BuildAsync();

        // Act - concurrent fire-and-forget sends with callbacks
        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            for (var i = 0; i < messagesPerThread; i++)
            {
                producer.Send(
                    new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"thread-{threadId}-key-{i}",
                        Value = $"thread-{threadId}-value-{i}"
                    },
                    (metadata, error) =>
                    {
                        if (error is null)
                            Interlocked.Increment(ref deliveredCount);
                    });
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        await producer.FlushAsync();

        // Allow callbacks to complete
        await Task.Delay(500);

        // Assert
        await Assert.That(deliveredCount).IsEqualTo(threadCount * messagesPerThread);
    }
}
