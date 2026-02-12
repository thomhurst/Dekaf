using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for concurrent admin operations.
/// Verifies that concurrent admin client operations do not cause race conditions
/// or inconsistent state.
/// Closes #222
/// </summary>
[Category("Admin")]
public class ConcurrentAdminTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private IAdminClient CreateAdminClient()
    {
        return new AdminClientBuilder()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-admin-client")
            .Build();
    }

    /// <summary>
    /// Waits for a condition to become true with linear backoff.
    /// Admin operations in Kafka have eventual consistency.
    /// </summary>
    private static async Task<T> WaitForConditionAsync<T>(
        Func<Task<T>> check,
        Func<T, bool> condition,
        int maxRetries = 5,
        int initialDelayMs = 500)
    {
        T result = default!;
        for (var i = 0; i < maxRetries; i++)
        {
            await Task.Delay(initialDelayMs * (i + 1)).ConfigureAwait(false);
            result = await check().ConfigureAwait(false);
            if (condition(result))
                return result;
        }
        return result;
    }

    #region Concurrent CreateTopics

    [Test]
    public async Task ConcurrentCreateTopicsAsync_DifferentTopics_AllSucceed()
    {
        // Arrange
        await using var admin = CreateAdminClient();
        const int concurrency = 5;
        var topicNames = Enumerable.Range(0, concurrency)
            .Select(_ => $"concurrent-create-{Guid.NewGuid():N}")
            .ToList();

        // Act - create all topics concurrently
        var tasks = topicNames.Select(name =>
            admin.CreateTopicsAsync([
                new NewTopic { Name = name, NumPartitions = 2, ReplicationFactor = 1 }
            ]).AsTask()
        ).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Allow metadata to propagate
        await Task.Delay(3000).ConfigureAwait(false);

        // Assert - all topics should be describable
        var descriptions = await admin.DescribeTopicsAsync(topicNames).ConfigureAwait(false);

        await Assert.That(descriptions.Count).IsEqualTo(concurrency);
        foreach (var topicName in topicNames)
        {
            await Assert.That(descriptions).ContainsKey(topicName);
            await Assert.That(descriptions[topicName].Partitions.Count).IsEqualTo(2);
        }

        // Cleanup
        await admin.DeleteTopicsAsync(topicNames).ConfigureAwait(false);
    }

    #endregion

    #region Concurrent DescribeTopics

    [Test]
    public async Task ConcurrentDescribeTopicsAsync_DifferentTopics_AllReturnCorrectResults()
    {
        // Arrange
        await using var admin = CreateAdminClient();
        const int concurrency = 5;
        var topicNames = new List<string>();

        for (var i = 0; i < concurrency; i++)
        {
            var partitions = i + 1; // each topic has a different partition count
            var topic = await KafkaContainer.CreateTestTopicAsync(partitions).ConfigureAwait(false);
            topicNames.Add(topic);
        }

        // Act - describe all topics concurrently, each in its own call
        var results = new ConcurrentDictionary<string, TopicDescription>();
        var tasks = topicNames.Select(name =>
            Task.Run(async () =>
            {
                var desc = await admin.DescribeTopicsAsync([name]).ConfigureAwait(false);
                if (desc.TryGetValue(name, out var topicDesc))
                {
                    results[name] = topicDesc;
                }
            })
        ).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - all topics should have correct partition counts
        await Assert.That(results.Count).IsEqualTo(concurrency);
        for (var i = 0; i < concurrency; i++)
        {
            var expectedPartitions = i + 1;
            var topicName = topicNames[i];
            await Assert.That(results).ContainsKey(topicName);
            await Assert.That(results[topicName].Partitions.Count).IsEqualTo(expectedPartitions);
        }
    }

    #endregion

    #region Concurrent AlterConfigs

    [Test]
    public async Task ConcurrentAlterConfigsAsync_DifferentTopics_AllSucceed()
    {
        // Arrange
        await using var admin = CreateAdminClient();
        const int concurrency = 5;
        var topicNames = new List<string>();

        for (var i = 0; i < concurrency; i++)
        {
            var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
            topicNames.Add(topic);
        }

        // Act - alter configs concurrently with different retention values
        var retentionValues = Enumerable.Range(0, concurrency)
            .Select(i => ((i + 1) * 3600000).ToString()) // 1h, 2h, 3h, 4h, 5h
            .ToList();

        var tasks = topicNames.Select((name, index) =>
        {
            var configs = new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>
            {
                [ConfigResource.Topic(name)] =
                [
                    new ConfigEntry { Name = "retention.ms", Value = retentionValues[index] }
                ]
            };
            return admin.AlterConfigsAsync(configs).AsTask();
        }).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - each topic should have its specific retention value
        for (var i = 0; i < concurrency; i++)
        {
            var topicName = topicNames[i];
            var expectedRetention = retentionValues[i];

            var updatedValue = await WaitForConditionAsync(
                async () =>
                {
                    var configs = await admin.DescribeConfigsAsync([ConfigResource.Topic(topicName)])
                        .ConfigureAwait(false);
                    return configs[ConfigResource.Topic(topicName)]
                        .First(c => c.Name == "retention.ms").Value;
                },
                value => value == expectedRetention).ConfigureAwait(false);

            await Assert.That(updatedValue).IsEqualTo(expectedRetention);
        }
    }

    #endregion

    #region Concurrent CreatePartitions and DescribeTopics

    [Test]
    public async Task ConcurrentCreatePartitionsAndDescribeTopics_ConsistentView()
    {
        // Arrange
        await using var admin = CreateAdminClient();
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1).ConfigureAwait(false);

        // Act - simultaneously increase partitions and describe the topic
        var createPartitionsTask = admin.CreatePartitionsAsync(
            new Dictionary<string, int> { [topic] = 4 }).AsTask();

        var describeTask = admin.DescribeTopicsAsync([topic]).AsTask();

        await Task.WhenAll(createPartitionsTask, describeTask).ConfigureAwait(false);

        // The describe may have caught the state before or after partition creation.
        // Either way, no exception should have been thrown.
        var describeResult = describeTask.Result;
        await Assert.That(describeResult).ContainsKey(topic);

        var partitionCount = describeResult[topic].Partitions.Count;
        // Partition count should be either 1 (before) or 4 (after)
        await Assert.That(partitionCount is 1 or 4).IsTrue();

        // Wait for partition creation to fully propagate, then verify final state
        var finalDescription = await WaitForConditionAsync(
            async () =>
            {
                var desc = await admin.DescribeTopicsAsync([topic]).ConfigureAwait(false);
                return desc[topic];
            },
            desc => desc.Partitions.Count == 4).ConfigureAwait(false);

        await Assert.That(finalDescription.Partitions.Count).IsEqualTo(4);
    }

    #endregion

    #region Concurrent ListConsumerGroupOffsets

    [Test]
    public async Task ConcurrentListConsumerGroupOffsetsAsync_DifferentGroups_AllSucceed()
    {
        // Arrange
        await using var admin = CreateAdminClient();
        const int concurrency = 3;

        // Create topics and produce/consume so groups actually exist with committed offsets.
        // This avoids CoordinatorNotAvailable errors that occur with brand-new groups
        // that have never been seen by the broker.
        var groupIds = new List<string>();
        var topicNames = new List<string>();

        for (var i = 0; i < concurrency; i++)
        {
            var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
            topicNames.Add(topic);
            var groupId = $"test-group-{Guid.NewGuid():N}";
            groupIds.Add(groupId);

            // Produce a message to the topic
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId($"test-producer-{i}")
                .BuildAsync();

            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            }).ConfigureAwait(false);

            // Consume and commit so the group coordinator is established
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync();

            consumer.Subscribe(topic);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var msg = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token)
                .ConfigureAwait(false);
        }

        // Allow group coordinator state to stabilize
        await Task.Delay(3000).ConfigureAwait(false);

        // Act - query offsets for all groups concurrently with retry for transient errors
        var results = new ConcurrentDictionary<string, IReadOnlyDictionary<TopicPartition, long>>();
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = groupIds.Select(groupId =>
            Task.Run(async () =>
            {
                try
                {
                    // Retry with backoff for transient CoordinatorNotAvailable errors
                    for (var attempt = 0; attempt < 5; attempt++)
                    {
                        try
                        {
                            var offsets = await admin.ListConsumerGroupOffsetsAsync(groupId)
                                .ConfigureAwait(false);
                            results[groupId] = offsets;
                            return;
                        }
                        catch (Errors.GroupException) when (attempt < 4)
                        {
                            await Task.Delay(1000 * (attempt + 1)).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })
        ).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - all calls should have completed without errors
        await Assert.That(exceptions).IsEmpty();
        await Assert.That(results.Count).IsEqualTo(concurrency);

        // Each group should have offsets committed
        foreach (var groupId in groupIds)
        {
            await Assert.That(results).ContainsKey(groupId);
        }
    }

    #endregion

    #region Admin Operations During Active Production/Consumption

    [Test]
    public async Task AdminOperationsDuringActiveProduction_NoInterference()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        await using var admin = CreateAdminClient();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-concurrent")
            .BuildAsync();

        // Act - produce messages while simultaneously performing admin operations
        const int messageCount = 20;
        var produceExceptions = new ConcurrentBag<Exception>();
        var adminExceptions = new ConcurrentBag<Exception>();
        var producedCount = 0;

        var produceTask = Task.Run(async () =>
        {
            for (var i = 0; i < messageCount; i++)
            {
                try
                {
                    await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"key-{i}",
                        Value = $"value-{i}"
                    }).ConfigureAwait(false);

                    Interlocked.Increment(ref producedCount);
                }
                catch (Exception ex)
                {
                    produceExceptions.Add(ex);
                }
            }
        });

        // Run admin operations concurrently with production
        var adminTask = Task.Run(async () =>
        {
            try
            {
                // Describe the topic while producing
                var descriptions = await admin.DescribeTopicsAsync([topic]).ConfigureAwait(false);
                await Assert.That(descriptions).ContainsKey(topic);

                // Describe cluster while producing
                var cluster = await admin.DescribeClusterAsync().ConfigureAwait(false);
                await Assert.That(cluster.Nodes.Count).IsGreaterThan(0);

                // Describe configs while producing
                var configs = await admin.DescribeConfigsAsync([ConfigResource.Topic(topic)])
                    .ConfigureAwait(false);
                await Assert.That(configs).ContainsKey(ConfigResource.Topic(topic));
            }
            catch (Exception ex)
            {
                adminExceptions.Add(ex);
            }
        });

        await Task.WhenAll(produceTask, adminTask).ConfigureAwait(false);

        // Assert - neither production nor admin operations should have failed
        await Assert.That(produceExceptions).IsEmpty();
        await Assert.That(adminExceptions).IsEmpty();
        await Assert.That(producedCount).IsEqualTo(messageCount);
    }

    [Test]
    public async Task AdminOperationsDuringActiveConsumption_NoInterference()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Pre-produce messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-pre")
            .BuildAsync();

        const int messageCount = 5;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        await using var admin = CreateAdminClient();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Act - consume messages while simultaneously performing admin operations
        var consumeExceptions = new ConcurrentBag<Exception>();
        var adminExceptions = new ConcurrentBag<Exception>();
        var consumedMessages = new ConcurrentBag<string>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var consumeTask = Task.Run(async () =>
        {
            try
            {
                for (var i = 0; i < messageCount; i++)
                {
                    var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token)
                        .ConfigureAwait(false);
                    if (result is { } r)
                    {
                        consumedMessages.Add(r.Value);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected if timeout reached
            }
            catch (Exception ex)
            {
                consumeExceptions.Add(ex);
            }
        });

        var adminTask = Task.Run(async () =>
        {
            try
            {
                // Small delay to let consumer start subscribing
                await Task.Delay(1000).ConfigureAwait(false);

                // Describe topic while consuming
                var descriptions = await admin.DescribeTopicsAsync([topic]).ConfigureAwait(false);
                await Assert.That(descriptions).ContainsKey(topic);

                // Describe cluster while consuming
                var cluster = await admin.DescribeClusterAsync().ConfigureAwait(false);
                await Assert.That(cluster.Nodes.Count).IsGreaterThan(0);

                // List topics while consuming
                var topics = await admin.ListTopicsAsync().ConfigureAwait(false);
                await Assert.That(topics.Count).IsGreaterThan(0);
            }
            catch (Exception ex)
            {
                adminExceptions.Add(ex);
            }
        });

        await Task.WhenAll(consumeTask, adminTask).ConfigureAwait(false);

        // Assert
        await Assert.That(adminExceptions).IsEmpty();
        await Assert.That(consumeExceptions).IsEmpty();
        await Assert.That(consumedMessages.Count).IsGreaterThan(0);
    }

    #endregion

    #region Multiple Admin Clients Concurrently

    [Test]
    public async Task MultipleAdminClients_ConcurrentDescribeCluster_AllSucceed()
    {
        // Arrange & Act - create multiple admin clients and use them concurrently
        const int concurrency = 5;
        var results = new ConcurrentBag<ClusterDescription>();
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, concurrency).Select(_ =>
            Task.Run(async () =>
            {
                await using var admin = CreateAdminClient();
                try
                {
                    var cluster = await admin.DescribeClusterAsync().ConfigureAwait(false);
                    results.Add(cluster);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })
        ).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert
        await Assert.That(exceptions).IsEmpty();
        await Assert.That(results.Count).IsEqualTo(concurrency);

        // All clients should report the same cluster info
        var controllerIds = results.Select(r => r.ControllerId).Distinct().ToList();
        await Assert.That(controllerIds.Count).IsEqualTo(1);
    }

    #endregion

    #region Concurrent Mixed Operations

    [Test]
    public async Task ConcurrentMixedAdminOperations_NoRaceConditions()
    {
        // Arrange
        await using var admin = CreateAdminClient();

        // Create topics upfront for operations that need them
        var topic1 = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var topic2 = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var topic3 = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);

        var exceptions = new ConcurrentBag<Exception>();

        // Act - run multiple different admin operations concurrently
        var describeTopicsTask = Task.Run(async () =>
        {
            try
            {
                var desc = await admin.DescribeTopicsAsync([topic1, topic2, topic3])
                    .ConfigureAwait(false);
                await Assert.That(desc.Count).IsEqualTo(3);
            }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        var describeClusterTask = Task.Run(async () =>
        {
            try
            {
                var cluster = await admin.DescribeClusterAsync().ConfigureAwait(false);
                await Assert.That(cluster.Nodes.Count).IsGreaterThan(0);
            }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        var describeConfigsTask = Task.Run(async () =>
        {
            try
            {
                var configs = await admin.DescribeConfigsAsync([ConfigResource.Topic(topic1)])
                    .ConfigureAwait(false);
                await Assert.That(configs.Count).IsGreaterThan(0);
            }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        var listTopicsTask = Task.Run(async () =>
        {
            try
            {
                var topics = await admin.ListTopicsAsync().ConfigureAwait(false);
                await Assert.That(topics.Count).IsGreaterThan(0);
            }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        var listGroupsTask = Task.Run(async () =>
        {
            try
            {
                var groups = await admin.ListConsumerGroupsAsync().ConfigureAwait(false);
                await Assert.That(groups).IsNotNull();
            }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        await Task.WhenAll(
            describeTopicsTask,
            describeClusterTask,
            describeConfigsTask,
            listTopicsTask,
            listGroupsTask
        ).ConfigureAwait(false);

        // Assert
        await Assert.That(exceptions).IsEmpty();
    }

    #endregion
}
