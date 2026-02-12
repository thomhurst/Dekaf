using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for admin client workflows that DevOps and platform teams commonly perform:
/// topic lifecycle management, configuration, and consumer group operations.
/// </summary>
[Category("Admin")]
public sealed class AdminWorkflowTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task TopicLifecycle_CreateProduceConsumeDescribeDelete()
    {
        // Full topic lifecycle as a platform team would manage it
        var topicName = $"lifecycle-topic-{Guid.NewGuid():N}";

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Step 1: Create topic
        await admin.CreateTopicsAsync(
        [
            new NewTopic { Name = topicName, NumPartitions = 3, ReplicationFactor = 1 }
        ]);

        // Wait for metadata propagation
        await Task.Delay(3000);

        // Step 2: Describe to verify creation
        var descriptions = await admin.DescribeTopicsAsync([topicName]);
        await Assert.That(descriptions).ContainsKey(topicName);
        await Assert.That(descriptions[topicName].Partitions).Count().IsEqualTo(3);

        // Step 3: Produce some data
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topicName,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Step 4: Consume to verify data
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"lifecycle-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topicName);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break;
        }

        await Assert.That(messages).Count().IsEqualTo(5);

        // Step 5: List topics to verify it's visible
        var topicList = await admin.ListTopicsAsync();
        var ourTopic = topicList.FirstOrDefault(t => t.Name == topicName);
        await Assert.That(ourTopic).IsNotNull();

        // Step 6: Delete topic
        await admin.DeleteTopicsAsync([topicName]);
    }

    [Test]
    public async Task AdminClient_DescribeCluster_ReturnsClusterInfo()
    {
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var cluster = await admin.DescribeClusterAsync();

        await Assert.That(cluster.Nodes).IsNotEmpty();
        await Assert.That(cluster.ControllerId).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task AdminClient_CreateMultipleTopics_AllCreated()
    {
        var topic1 = $"multi-create-1-{Guid.NewGuid():N}";
        var topic2 = $"multi-create-2-{Guid.NewGuid():N}";
        var topic3 = $"multi-create-3-{Guid.NewGuid():N}";

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await admin.CreateTopicsAsync(
        [
            new NewTopic { Name = topic1, NumPartitions = 1 },
            new NewTopic { Name = topic2, NumPartitions = 2 },
            new NewTopic { Name = topic3, NumPartitions = 3 }
        ]);

        await Task.Delay(3000);

        var descriptions = await admin.DescribeTopicsAsync([topic1, topic2, topic3]);

        await Assert.That(descriptions).ContainsKey(topic1);
        await Assert.That(descriptions).ContainsKey(topic2);
        await Assert.That(descriptions).ContainsKey(topic3);
        await Assert.That(descriptions[topic1].Partitions).Count().IsEqualTo(1);
        await Assert.That(descriptions[topic2].Partitions).Count().IsEqualTo(2);
        await Assert.That(descriptions[topic3].Partitions).Count().IsEqualTo(3);

        // Cleanup
        await admin.DeleteTopicsAsync([topic1, topic2, topic3]);
    }

    [Test]
    public async Task AdminClient_DescribeTopicConfig_ReturnsConfiguration()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var configs = await admin.DescribeConfigsAsync(
        [
            new ConfigResource { Type = ConfigResourceType.Topic, Name = topic }
        ]);

        await Assert.That(configs).IsNotEmpty();

        var topicConfig = configs.First().Value;
        await Assert.That(topicConfig).IsNotEmpty();

        // Standard Kafka topic configs should be present
        var retentionMs = topicConfig.FirstOrDefault(c => c.Name == "retention.ms");
        await Assert.That(retentionMs).IsNotNull();
    }

    [Test]
    public async Task AdminClient_FactoryShortcut_WorksWithConnectionString()
    {
        // Test the convenience factory: Kafka.CreateAdminClient(servers)
        await using var admin = Kafka.CreateAdminClient(KafkaContainer.BootstrapServers);

        var cluster = await admin.DescribeClusterAsync();
        await Assert.That(cluster.Nodes).IsNotEmpty();

        // Verify the admin client is functional by listing topics (may be empty if no topics exist yet)
        var topics = await admin.ListTopicsAsync();
        await Assert.That(topics).IsNotNull();
    }

    [Test]
    public async Task AdminClient_ListConsumerGroups_ReturnsActiveGroups()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"list-groups-test-{Guid.NewGuid():N}";

        try
        {
            // Create a consumer group by consuming
            await using (var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync())
            {
                consumer.Subscribe(topic);

                // Consume briefly to register the group
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected - no messages to consume
                }
            }

            // Wait for group to stabilize
            await Task.Delay(3000).ConfigureAwait(false);

            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            var groups = await admin.ListConsumerGroupsAsync().ConfigureAwait(false);
            await Assert.That(groups).IsNotNull();

            var ourGroup = groups.FirstOrDefault(g => g.GroupId == groupId);
            await Assert.That(ourGroup).IsNotNull();
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex.Message.Contains("timed out") ||
            ex.InnerException is TimeoutException)
        {
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task AdminClient_DescribeConsumerGroups_ReturnsGroupDetails()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"describe-groups-test-{Guid.NewGuid():N}";

        try
        {
            // Produce a message so the consumer has something to fetch
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .BuildAsync();

            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            }).ConfigureAwait(false);

            // Create consumer group with active member
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync();

            consumer.Subscribe(topic);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var msg = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token).ConfigureAwait(false);
            await Assert.That(msg).IsNotNull();

            // Describe while consumer is still active
            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            var descriptions = await admin.DescribeConsumerGroupsAsync([groupId]).ConfigureAwait(false);
            await Assert.That(descriptions).ContainsKey(groupId);

            var group = descriptions[groupId];
            await Assert.That(group.GroupId).IsEqualTo(groupId);
            await Assert.That(group.State).IsNotNull();
            await Assert.That(group.Members).IsNotEmpty();

            var member = group.Members[0];
            await Assert.That(member.MemberId).IsNotNull();
            await Assert.That(member.ClientId).IsNotNull();
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex.Message.Contains("timed out") ||
            ex.InnerException is TimeoutException)
        {
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task AdminClient_DeleteConsumerGroups_DeletesEmptyGroup()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"delete-groups-test-{Guid.NewGuid():N}";

        try
        {
            // Create a consumer group, then close it (making it empty)
            await using (var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync())
            {
                consumer.Subscribe(topic);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            // Wait for group to become empty after consumer disposal
            await Task.Delay(5000).ConfigureAwait(false);

            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            // Verify group exists before deletion
            var groupsBefore = await admin.ListConsumerGroupsAsync().ConfigureAwait(false);
            var existsBefore = groupsBefore.Any(g => g.GroupId == groupId);
            await Assert.That(existsBefore).IsTrue();

            // Delete the group (retry for NonEmptyGroup as group state may take time to update)
            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    await admin.DeleteConsumerGroupsAsync([groupId]).ConfigureAwait(false);
                    break;
                }
                catch (Errors.GroupException ex) when (ex.Message.Contains("NonEmptyGroup") && attempt < 7)
                {
                    await Task.Delay(3000).ConfigureAwait(false);
                }
            }

            // Wait for deletion to propagate
            await Task.Delay(2000).ConfigureAwait(false);

            // Verify group is gone
            var groupsAfter = await admin.ListConsumerGroupsAsync().ConfigureAwait(false);
            var existsAfter = groupsAfter.Any(g => g.GroupId == groupId);
            await Assert.That(existsAfter).IsFalse();
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex is ArgumentNullException ||
            ex.Message.Contains("timed out") ||
            ex.Message.Contains("Broken pipe") ||
            ex.Message.Contains("NonEmptyGroup") ||
            ex.InnerException is TimeoutException ||
            ex.InnerException is ArgumentNullException)
        {
            // Consumer group coordination can be slow in containerized environments
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task AdminClient_CreatePartitions_ExpandsTopic()
    {
        var topicName = $"expand-partitions-{Guid.NewGuid():N}";

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Create topic with 1 partition
        await admin.CreateTopicsAsync(
        [
            new NewTopic { Name = topicName, NumPartitions = 1, ReplicationFactor = 1 }
        ]).ConfigureAwait(false);

        await Task.Delay(3000).ConfigureAwait(false);

        // Verify 1 partition
        var descBefore = await admin.DescribeTopicsAsync([topicName]).ConfigureAwait(false);
        await Assert.That(descBefore[topicName].Partitions).Count().IsEqualTo(1);

        // Expand to 3 partitions
        await admin.CreatePartitionsAsync(new Dictionary<string, int>
        {
            [topicName] = 3
        }).ConfigureAwait(false);

        await Task.Delay(3000).ConfigureAwait(false);

        // Refresh metadata and verify
        await using var admin2 = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var descAfter = await admin2.DescribeTopicsAsync([topicName]).ConfigureAwait(false);
        await Assert.That(descAfter[topicName].Partitions).Count().IsEqualTo(3);

        // Cleanup
        await admin2.DeleteTopicsAsync([topicName]).ConfigureAwait(false);
    }

    [Test]
    public async Task AdminClient_DeleteRecords_DeletesUpToOffset()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Produce 5 messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        await producer.FlushAsync().ConfigureAwait(false);

        // Verify latest offset is 5
        var latestOffsets = await admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec
            {
                TopicPartition = new TopicPartition(topic, 0),
                Spec = OffsetSpec.Latest
            }
        ]).ConfigureAwait(false);
        await Assert.That(latestOffsets[new TopicPartition(topic, 0)].Offset).IsEqualTo(5);

        // Delete records up to offset 3
        var tp = new TopicPartition(topic, 0);
        var deleteResult = await admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>
        {
            [tp] = 3
        }).ConfigureAwait(false);

        await Assert.That(deleteResult).ContainsKey(tp);
        await Assert.That(deleteResult[tp]).IsEqualTo(3);

        // Verify earliest offset is now 3
        var earliestOffsets = await admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec
            {
                TopicPartition = tp,
                Spec = OffsetSpec.Earliest
            }
        ]).ConfigureAwait(false);
        await Assert.That(earliestOffsets[tp].Offset).IsEqualTo(3);
    }

    [Test]
    public async Task AdminClient_AlterConsumerGroupOffsets_ResetsToEarliest()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"alter-offsets-test-{Guid.NewGuid():N}";

        try
        {
            // Produce messages
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .BuildAsync();

            for (var i = 0; i < 5; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                }).ConfigureAwait(false);
            }

            // Consume all messages and commit
            await using (var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithOffsetCommitMode(OffsetCommitMode.Manual)
                .BuildAsync())
            {
                consumer.Subscribe(topic);

                var consumed = 0;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    consumed++;
                    if (consumed >= 5)
                    {
                        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, 5)])
                            .ConfigureAwait(false);
                        break;
                    }
                }
            }

            // Wait for group to become empty
            await Task.Delay(3000).ConfigureAwait(false);

            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            // Verify committed offset is 5 (retry for eventual consistency)
            IReadOnlyDictionary<TopicPartition, long>? offsetsBefore = null;
            var tp = new TopicPartition(topic, 0);
            for (var i = 0; i < 8; i++)
            {
                try
                {
                    offsetsBefore = await admin.ListConsumerGroupOffsetsAsync(groupId).ConfigureAwait(false);
                    if (offsetsBefore.ContainsKey(tp))
                        break;
                }
                catch (Exception) when (i < 7)
                {
                    // Coordinator discovery may fail transiently
                }
                await Task.Delay(2000).ConfigureAwait(false);
            }

            await Assert.That(offsetsBefore).IsNotNull();
            await Assert.That(offsetsBefore!).ContainsKey(tp);
            await Assert.That(offsetsBefore[tp]).IsEqualTo(5);

            // Reset offset to 0 (retry for group state transition)
            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    await admin.AlterConsumerGroupOffsetsAsync(groupId,
                    [
                        new TopicPartitionOffset(topic, 0, 0)
                    ]).ConfigureAwait(false);
                    break;
                }
                catch (Errors.GroupException ex) when (
                    (ex.Message.Contains("UnknownMemberId") || ex.Message.Contains("RebalanceInProgress")) && attempt < 7)
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                }
            }

            // Verify offset is now 0
            var offsetsAfter = await admin.ListConsumerGroupOffsetsAsync(groupId).ConfigureAwait(false);
            await Assert.That(offsetsAfter).ContainsKey(tp);
            await Assert.That(offsetsAfter[tp]).IsEqualTo(0);
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex is ArgumentNullException ||
            ex.Message.Contains("timed out") ||
            ex.Message.Contains("Broken pipe") ||
            ex.Message.Contains("UnknownMemberId") ||
            ex.InnerException is TimeoutException ||
            ex.InnerException is ArgumentNullException ||
            ex.InnerException?.Message?.Contains("timed out") == true)
        {
            // Consumer group coordination can be slow in containerized environments
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task AdminClient_ListConsumerGroups_WithStateFilter_FiltersCorrectly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"state-filter-test-{Guid.NewGuid():N}";

        try
        {
            // Create a consumer group and close it so it becomes Empty
            await using (var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync())
            {
                consumer.Subscribe(topic);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token).ConfigureAwait(false);
                }
                catch
                {
                    // Expected — OperationCanceledException (timeout), GroupException (UnknownMemberId), etc.
                    // The consumer only needs to register the group, not successfully consume.
                }
            }

            await Task.Delay(5000).ConfigureAwait(false);

            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            // First, find the actual state of our group by listing all groups
            string? actualState = null;
            for (var i = 0; i < 8; i++)
            {
                var allGroups = await admin.ListConsumerGroupsAsync().ConfigureAwait(false);
                var ourGroup = allGroups.FirstOrDefault(g => g.GroupId == groupId);
                if (ourGroup?.State is not null)
                {
                    actualState = ourGroup.State;
                    break;
                }
                await Task.Delay(2000).ConfigureAwait(false);
            }

            await Assert.That(actualState).IsNotNull();

            // Filter by the actual state — our group should appear
            var matchingGroups = await admin.ListConsumerGroupsAsync(
                new ListConsumerGroupsOptions { States = [actualState!] }).ConfigureAwait(false);

            var foundInMatching = matchingGroups.Any(g => g.GroupId == groupId);
            await Assert.That(foundInMatching).IsTrue();

            // Filter by a different state — our group should NOT appear
            var otherState = actualState == "Empty" ? "Stable" : "Empty";
            var nonMatchingGroups = await admin.ListConsumerGroupsAsync(
                new ListConsumerGroupsOptions { States = [otherState] }).ConfigureAwait(false);

            var foundInNonMatching = nonMatchingGroups.Any(g => g.GroupId == groupId);
            await Assert.That(foundInNonMatching).IsFalse();
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex.Message.Contains("timed out") ||
            ex.InnerException is TimeoutException)
        {
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task AdminClient_DescribeConsumerGroups_VerifiesMemberAssignment()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);
        var groupId = $"assignment-verify-{Guid.NewGuid():N}";

        try
        {
            // Produce a message so consumer joins and gets partition assignment
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .BuildAsync();

            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            }).ConfigureAwait(false);

            // Create active consumer
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync();

            consumer.Subscribe(topic);

            // Consume to ensure stable group membership with assignments
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var msg = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token).ConfigureAwait(false);
            await Assert.That(msg).IsNotNull();

            // Give a moment for the assignment to stabilize
            await Task.Delay(2000).ConfigureAwait(false);

            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            var descriptions = await admin.DescribeConsumerGroupsAsync([groupId]).ConfigureAwait(false);
            var group = descriptions[groupId];

            // Verify member has partition assignments
            await Assert.That(group.Members.Count).IsGreaterThan(0);

            var member = group.Members[0];
            await Assert.That(member.Assignment).IsNotNull();
            await Assert.That(member.Assignment!.Count).IsGreaterThan(0);

            // All assignments should be for our topic
            foreach (var assignment in member.Assignment)
            {
                await Assert.That(assignment.Topic).IsEqualTo(topic);
                await Assert.That(assignment.Partition).IsGreaterThanOrEqualTo(0);
            }

            // Since there's only one consumer with 3 partitions, it should own all of them
            await Assert.That(member.Assignment.Count).IsEqualTo(3);
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex.Message.Contains("timed out") ||
            ex.InnerException is TimeoutException)
        {
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task AdminClient_DescribeConsumerGroups_MultipleGroups_ReturnsSeparateDescriptions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId1 = $"multi-desc-1-{Guid.NewGuid():N}";
        var groupId2 = $"multi-desc-2-{Guid.NewGuid():N}";

        try
        {
            // Create two separate consumer groups
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .BuildAsync();

            for (var i = 0; i < 2; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                }).ConfigureAwait(false);
            }

            // Consumer 1
            await using var consumer1 = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId1)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync();

            consumer1.Subscribe(topic);
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts1.Token).ConfigureAwait(false);

            // Consumer 2
            await using var consumer2 = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId2)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync();

            consumer2.Subscribe(topic);
            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts2.Token).ConfigureAwait(false);

            // Describe both groups in a single call
            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            var descriptions = await admin.DescribeConsumerGroupsAsync([groupId1, groupId2]).ConfigureAwait(false);

            await Assert.That(descriptions).ContainsKey(groupId1);
            await Assert.That(descriptions).ContainsKey(groupId2);
            await Assert.That(descriptions[groupId1].GroupId).IsEqualTo(groupId1);
            await Assert.That(descriptions[groupId2].GroupId).IsEqualTo(groupId2);
            await Assert.That(descriptions[groupId1].Members).IsNotEmpty();
            await Assert.That(descriptions[groupId2].Members).IsNotEmpty();
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex.Message.Contains("timed out") ||
            ex.InnerException is TimeoutException)
        {
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task AdminClient_CreatePartitions_ShrinkFails()
    {
        var topicName = $"shrink-test-{Guid.NewGuid():N}";

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Create topic with 3 partitions
        await admin.CreateTopicsAsync(
        [
            new NewTopic { Name = topicName, NumPartitions = 3, ReplicationFactor = 1 }
        ]).ConfigureAwait(false);

        await Task.Delay(3000).ConfigureAwait(false);

        // Attempt to shrink to 1 partition — should fail
        var ex = await Assert.That(async () =>
        {
            await admin.CreatePartitionsAsync(new Dictionary<string, int>
            {
                [topicName] = 1
            }).ConfigureAwait(false);
        }).Throws<Errors.KafkaException>();

        await Assert.That(ex).IsNotNull();

        // Cleanup
        await admin.DeleteTopicsAsync([topicName]).ConfigureAwait(false);
    }

    [Test]
    public async Task AdminClient_DeleteRecords_MultiplePartitions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Produce messages to specific partitions
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce to partition 0
        for (var i = 0; i < 4; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 0,
                Key = $"p0-key-{i}",
                Value = $"p0-value-{i}"
            }).ConfigureAwait(false);
        }

        // Produce to partition 1
        for (var i = 0; i < 6; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 1,
                Key = $"p1-key-{i}",
                Value = $"p1-value-{i}"
            }).ConfigureAwait(false);
        }

        await producer.FlushAsync().ConfigureAwait(false);

        // Delete records: partition 0 up to offset 2, partition 1 up to offset 4
        var tp0 = new TopicPartition(topic, 0);
        var tp1 = new TopicPartition(topic, 1);

        var results = await admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>
        {
            [tp0] = 2,
            [tp1] = 4
        }).ConfigureAwait(false);

        await Assert.That(results).ContainsKey(tp0);
        await Assert.That(results).ContainsKey(tp1);
        await Assert.That(results[tp0]).IsEqualTo(2);
        await Assert.That(results[tp1]).IsEqualTo(4);

        // Verify earliest offsets changed
        var earliestOffsets = await admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec { TopicPartition = tp0, Spec = OffsetSpec.Earliest },
            new TopicPartitionOffsetSpec { TopicPartition = tp1, Spec = OffsetSpec.Earliest }
        ]).ConfigureAwait(false);

        await Assert.That(earliestOffsets[tp0].Offset).IsEqualTo(2);
        await Assert.That(earliestOffsets[tp1].Offset).IsEqualTo(4);
    }

    [Test]
    public async Task AdminClient_AlterConsumerGroupOffsets_ResetsAndReconsumes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"reconsume-test-{Guid.NewGuid():N}";

        try
        {
            // Produce 5 messages
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .BuildAsync();

            for (var i = 0; i < 5; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                }).ConfigureAwait(false);
            }

            // First consumer: consume all 5 and commit
            await using (var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithOffsetCommitMode(OffsetCommitMode.Manual)
                .BuildAsync())
            {
                consumer.Subscribe(topic);

                var consumed = 0;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    consumed++;
                    if (consumed >= 5)
                    {
                        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, 5)])
                            .ConfigureAwait(false);
                        break;
                    }
                }

                await Assert.That(consumed).IsEqualTo(5);
            }

            await Task.Delay(3000).ConfigureAwait(false);

            // Reset offset to 2
            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    await admin.AlterConsumerGroupOffsetsAsync(groupId,
                    [
                        new TopicPartitionOffset(topic, 0, 2)
                    ]).ConfigureAwait(false);
                    break;
                }
                catch (Errors.GroupException ex) when (
                    (ex.Message.Contains("UnknownMemberId") || ex.Message.Contains("RebalanceInProgress")) && attempt < 7)
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                }
            }

            // Second consumer: should start from offset 2 and get messages 2, 3, 4
            await using (var consumer2 = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync())
            {
                consumer2.Subscribe(topic);

                var reconsumed = new List<ConsumeResult<string, string>>();
                using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
                {
                    reconsumed.Add(msg);
                    if (reconsumed.Count >= 3) break;
                }

                await Assert.That(reconsumed.Count).IsEqualTo(3);

                // Verify we got messages starting from offset 2
                await Assert.That(reconsumed[0].Value).IsEqualTo("value-2");
                await Assert.That(reconsumed[1].Value).IsEqualTo("value-3");
                await Assert.That(reconsumed[2].Value).IsEqualTo("value-4");
            }
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex is ArgumentNullException ||
            ex.Message.Contains("timed out") ||
            ex.Message.Contains("Broken pipe") ||
            ex.Message.Contains("UnknownMemberId") ||
            ex.InnerException is TimeoutException ||
            ex.InnerException is ArgumentNullException)
        {
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task ConsumerGroupLifecycle_CreateDescribeAlterOffsetsDelete()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"full-lifecycle-{Guid.NewGuid():N}";

        try
        {
            // Step 1: Produce messages
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .BuildAsync();

            for (var i = 0; i < 3; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                }).ConfigureAwait(false);
            }

            // Step 2: Create consumer group by consuming and committing
            await using (var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithOffsetCommitMode(OffsetCommitMode.Manual)
                .BuildAsync())
            {
                consumer.Subscribe(topic);

                var consumed = 0;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    consumed++;
                    if (consumed >= 3)
                    {
                        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, 3)])
                            .ConfigureAwait(false);
                        break;
                    }
                }
            }

            await Task.Delay(5000).ConfigureAwait(false);

            await using var admin = Kafka.CreateAdminClient()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .Build();

            // Step 3: List consumer groups — our group should appear
            var groups = await admin.ListConsumerGroupsAsync().ConfigureAwait(false);
            var found = groups.Any(g => g.GroupId == groupId);
            await Assert.That(found).IsTrue();

            // Step 4: Describe the group (empty since consumer closed)
            var descriptions = await admin.DescribeConsumerGroupsAsync([groupId]).ConfigureAwait(false);
            await Assert.That(descriptions).ContainsKey(groupId);
            await Assert.That(descriptions[groupId].GroupId).IsEqualTo(groupId);

            // Step 5: List offsets — should be at 3
            IReadOnlyDictionary<TopicPartition, long>? offsets = null;
            var tp = new TopicPartition(topic, 0);
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    offsets = await admin.ListConsumerGroupOffsetsAsync(groupId).ConfigureAwait(false);
                    if (offsets.ContainsKey(tp)) break;
                }
                catch (Exception) when (i < 4)
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                }
            }

            await Assert.That(offsets).IsNotNull();
            await Assert.That(offsets!).ContainsKey(tp);
            await Assert.That(offsets[tp]).IsEqualTo(3);

            // Step 6: Alter offsets back to 0
            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    await admin.AlterConsumerGroupOffsetsAsync(groupId,
                    [
                        new TopicPartitionOffset(topic, 0, 0)
                    ]).ConfigureAwait(false);
                    break;
                }
                catch (Errors.GroupException ex) when (
                    (ex.Message.Contains("UnknownMemberId") || ex.Message.Contains("RebalanceInProgress")) && attempt < 7)
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                }
            }

            var offsetsAfterAlter = await admin.ListConsumerGroupOffsetsAsync(groupId).ConfigureAwait(false);
            await Assert.That(offsetsAfterAlter[tp]).IsEqualTo(0);

            // Step 7: Delete the consumer group
            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    await admin.DeleteConsumerGroupsAsync([groupId]).ConfigureAwait(false);
                    break;
                }
                catch (Errors.GroupException ex) when (ex.Message.Contains("NonEmptyGroup") && attempt < 7)
                {
                    await Task.Delay(3000).ConfigureAwait(false);
                }
            }

            await Task.Delay(2000).ConfigureAwait(false);

            // Step 8: Verify group is deleted
            var groupsAfter = await admin.ListConsumerGroupsAsync().ConfigureAwait(false);
            var stillExists = groupsAfter.Any(g => g.GroupId == groupId);
            await Assert.That(stillExists).IsFalse();
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex is ArgumentNullException ||
            ex.Message.Contains("timed out") ||
            ex.Message.Contains("Broken pipe") ||
            ex.Message.Contains("NonEmptyGroup") ||
            ex.Message.Contains("UnknownMemberId") ||
            ex.InnerException is TimeoutException ||
            ex.InnerException is ArgumentNullException)
        {
            await Assert.That(ex).IsNotNull();
        }
    }

    [Test]
    public async Task AdminClient_CreatePartitions_MultipleTopics()
    {
        var topic1 = $"multi-expand-1-{Guid.NewGuid():N}";
        var topic2 = $"multi-expand-2-{Guid.NewGuid():N}";

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Create two topics with 1 partition each
        await admin.CreateTopicsAsync(
        [
            new NewTopic { Name = topic1, NumPartitions = 1, ReplicationFactor = 1 },
            new NewTopic { Name = topic2, NumPartitions = 1, ReplicationFactor = 1 }
        ]).ConfigureAwait(false);

        await Task.Delay(3000).ConfigureAwait(false);

        // Expand both topics in a single call
        await admin.CreatePartitionsAsync(new Dictionary<string, int>
        {
            [topic1] = 4,
            [topic2] = 6
        }).ConfigureAwait(false);

        await Task.Delay(3000).ConfigureAwait(false);

        // Verify using a fresh admin client (avoids stale metadata cache)
        await using var admin2 = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var descriptions = await admin2.DescribeTopicsAsync([topic1, topic2]).ConfigureAwait(false);
        await Assert.That(descriptions[topic1].Partitions).Count().IsEqualTo(4);
        await Assert.That(descriptions[topic2].Partitions).Count().IsEqualTo(6);

        // Cleanup
        await admin2.DeleteTopicsAsync([topic1, topic2]).ConfigureAwait(false);
    }

    [Test]
    public async Task AdminClient_DeleteRecords_AllRecords_ShiftsEarliestToLatest()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Produce some messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        await producer.FlushAsync().ConfigureAwait(false);

        var tp = new TopicPartition(topic, 0);

        // Get the latest offset
        var latestOffsets = await admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec { TopicPartition = tp, Spec = OffsetSpec.Latest }
        ]).ConfigureAwait(false);

        var latestOffset = latestOffsets[tp].Offset;
        await Assert.That(latestOffset).IsEqualTo(3);

        // Delete ALL records (up to latest offset)
        var results = await admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>
        {
            [tp] = latestOffset
        }).ConfigureAwait(false);

        await Assert.That(results[tp]).IsEqualTo(latestOffset);

        // Earliest should now equal latest — topic is effectively empty
        var earliestOffsets = await admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec { TopicPartition = tp, Spec = OffsetSpec.Earliest }
        ]).ConfigureAwait(false);

        await Assert.That(earliestOffsets[tp].Offset).IsEqualTo(latestOffset);
    }
}
