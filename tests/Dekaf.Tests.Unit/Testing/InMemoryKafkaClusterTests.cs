using System.Reflection;
using System.Text;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;
using Dekaf.Serialization.Routing;
using Dekaf.ShareConsumer;
using Dekaf.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryKafkaClusterTests
{
    [Test]
    public async Task AdminFeatures_KeepSupportedAndFinalizedRangesIndependent()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions
        {
            SupportedFeatures = new Dictionary<string, FeatureVersionRange>(StringComparer.Ordinal)
            {
                ["metadata.version"] = new(7, 19)
            }
        });
        var admin = new InMemoryAdminClient(cluster);

        var before = await admin.DescribeFeaturesAsync();
        await admin.UpdateFeaturesAsync(new Dictionary<string, FeatureUpdate>(StringComparer.Ordinal)
        {
            ["metadata.version"] = new() { MaxVersionLevel = 17 }
        });
        var after = await admin.DescribeFeaturesAsync();

        await Assert.That(before.SupportedFeatures["metadata.version"])
            .IsEqualTo(new FeatureVersionRange(7, 19));
        await Assert.That(before.FinalizedFeatures).IsEmpty();
        await Assert.That(after.SupportedFeatures["metadata.version"])
            .IsEqualTo(new FeatureVersionRange(7, 19));
        await Assert.That(after.FinalizedFeatures["metadata.version"])
            .IsEqualTo(new FeatureVersionRange(17, 17));
        await Assert.That(ReferenceEquals(after.SupportedFeatures, after.FinalizedFeatures)).IsFalse();
    }

    [Test]
    public async Task AdminFeatures_ConcurrentUpdatesPreserveEpochAndState()
    {
        var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
        const int updateCount = 100;

        var updates = Enumerable.Range(0, updateCount)
            .Select(index => Task.Run(async () =>
            {
                await admin.UpdateFeaturesAsync(new Dictionary<string, FeatureUpdate>
                {
                    [$"feature-{index}"] = new() { MaxVersionLevel = (short)index }
                });
                _ = await admin.DescribeFeaturesAsync();
            }))
            .ToArray();
        await Task.WhenAll(updates);

        var metadata = await admin.DescribeFeaturesAsync();
        await Assert.That(metadata.FinalizedFeaturesEpoch).IsEqualTo(updateCount - 1L);
        await Assert.That(metadata.FinalizedFeatures).Count().IsEqualTo(updateCount);
    }

    [Test]
    public async Task ProducerConsumer_RoundTripsThroughSerializers()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "orders-service",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        var headers = Headers.Create("trace-id", "abc");

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = 1,
            Key = "order-1",
            Value = "created",
            Headers = headers,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1234)
        });
        consumer.Subscribe("orders");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(metadata.Topic).IsEqualTo("orders");
        await Assert.That(metadata.Partition).IsEqualTo(1);
        await Assert.That(metadata.Offset).IsEqualTo(0);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Topic).IsEqualTo("orders");
        await Assert.That(result.Value.Partition).IsEqualTo(1);
        await Assert.That(result.Value.Offset).IsEqualTo(0);
        await Assert.That(result.Value.Key).IsEqualTo("order-1");
        await Assert.That(result.Value.Value).IsEqualTo("created");
        await Assert.That(result.Value.Headers.Single().GetValueAsString()).IsEqualTo("abc");
        await Assert.That(result.Value.TimestampMs).IsEqualTo(1234);
    }

    [Test]
    public async Task Consumer_HeaderRoutingDeserializerUsesCallerOwnedHeaders()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var headerRouter = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("fallback"),
            new HeaderDeserializerRoute<string>(
                "created"u8.ToArray(),
                new PrefixDeserializer("created")));
        var router = new TopicRoutingDeserializer<string>()
            .Register("events", headerRouter)
            .Freeze();
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new HeaderPresenceDeserializer(),
            router,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "events",
            Key = "key",
            Value = "payload",
            Headers = Headers.Create("event-type", "created")
        });
        consumer.Subscribe("events");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("no-headers");
        await Assert.That(result.Value.Value).IsEqualTo("created:payload");
        await Assert.That(result.Value.Headers).Count().IsEqualTo(1);
        await Assert.That(result.Value.Headers[0].Key).IsEqualTo("event-type");
        await Assert.That(result.Value.Headers[0].GetValueAsString()).IsEqualTo("created");
    }

    [Test]
    public async Task Consumer_AsyncPathHeaderRoutingDeserializerUsesCallerOwnedHeaders()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var headerRouter = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("fallback"),
            new HeaderDeserializerRoute<string>(
                "created"u8.ToArray(),
                new PrefixDeserializer("created")));
        var router = new TopicRoutingDeserializer<string>()
            .Register("events", headerRouter)
            .Freeze();
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new AsyncStringDeserializer(),
            router,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "events",
            Key = "key",
            Value = "payload",
            Headers = Headers.Create("event-type", "created")
        });
        consumer.Subscribe("events");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("created:payload");
    }

    [Test]
    public async Task Consumer_EmptyGroupId_BehavesAsNoGroup()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 1);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await Assert.That(consumer.ConsumerGroupMetadata).IsNull();
        await Assert.That(consumer.MemberId).IsNull();
        await Assert.That(((IConsumerCommitConfiguration)consumer).HasConsumerGroup).IsFalse();
    }

    [Test]
    public async Task Consumer_WithoutGroup_CommitOverloadsThrow()
    {
        var cluster = new InMemoryKafkaCluster();
        var consumer = new InMemoryConsumer<string, string>(cluster);

        await Assert.That(async () => await consumer.CommitAsync(CancellationToken.None))
            .Throws<InvalidOperationException>()
            .And.HasMessageContaining("WithGroupId");
        await Assert.That(async () => await consumer.CommitAsync(
                [new TopicPartitionOffset("orders", 0, 1)],
                CancellationToken.None))
            .Throws<InvalidOperationException>()
            .And.HasMessageContaining("WithGroupId");
    }

    [Test]
    public async Task RunPartitionedAsync_EmptyGroupIdWithAutoCommitDefaults_PassesCommitModeGuard()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 1);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { GroupId = "" });
        consumer.Subscribe("orders");

        // An empty GroupId means no group to auto-commit through, so the commit-mode
        // guard must not fire. The run then fails on the in-memory consumer's missing
        // batch-fetch support — proof execution got past the guard (a guard regression
        // would surface InvalidOperationException here instead). The processor drains
        // its stream rather than returning immediately so the lane exits cleanly and
        // cannot fault first with its own InvalidOperationException.
        await Assert.That(async () => await consumer.RunPartitionedAsync(
                static async (context, token) =>
                {
                    await foreach (var message in context.Messages.WithCancellation(token))
                        context.MarkProcessed(message);
                },
                new PartitionedProcessingOptions(),
                CancellationToken.None))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task RunPartitionedAsync_GroupWithAutoCommitDefaults_Throws()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 1);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("orders");

        await Assert.That(async () => await consumer.RunPartitionedAsync(
                static (_, _) => ValueTask.CompletedTask,
                new PartitionedProcessingOptions(),
                CancellationToken.None))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Consumer_ManualCommit_PersistsGroupOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        var admin = new InMemoryAdminClient(cluster);

        await producer.ProduceAsync("jobs", "a", "one");
        consumer.Subscribe("jobs");
        _ = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
        await consumer.CommitAsync();

        var offsets = await admin.ListConsumerGroupOffsetsAsync("workers");

        await Assert.That(offsets[new TopicPartition("jobs", 0)]).IsEqualTo(1);
    }

    [Test]
    public async Task Consumer_GetCommittedOffsetsAsync_ReturnsSelectedOffsetsAndLeaderEpochs()
    {
        var cluster = new InMemoryKafkaCluster();
        IKafkaConsumer<string, string> consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        var jobs = new TopicPartition("jobs", 0);
        var tasks = new TopicPartition("tasks", 1);
        var missing = new TopicPartition("missing", 2);
        var unrequested = new TopicPartition("other", 3);
        await consumer.CommitAsync(
        [
            new TopicPartitionOffset(jobs.Topic, jobs.Partition, 12, leaderEpoch: 3),
            new TopicPartitionOffset(tasks.Topic, tasks.Partition, 34, leaderEpoch: 5),
            new TopicPartitionOffset(unrequested.Topic, unrequested.Partition, 56, leaderEpoch: 7)
        ]);

        var offsets = await consumer.GetCommittedOffsetsAsync([jobs, tasks, missing]);

        await Assert.That(offsets).Count().IsEqualTo(2);
        await Assert.That(offsets[jobs])
            .IsEqualTo(new TopicPartitionOffset(jobs.Topic, jobs.Partition, 12, leaderEpoch: 3));
        await Assert.That(offsets[tasks])
            .IsEqualTo(new TopicPartitionOffset(tasks.Topic, tasks.Partition, 34, leaderEpoch: 5));
        await Assert.That(offsets).DoesNotContainKey(missing);
        await Assert.That(offsets).DoesNotContainKey(unrequested);
    }

    [Test]
    public async Task Consumer_ManualOffsetStore_WithAutoCommit_CommitsStoredOffset()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = false
            });
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("jobs", 0);

        await producer.ProduceAsync("jobs", "a", "one");
        consumer.Subscribe("jobs");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
        var offsetsBeforeStore = await admin.ListConsumerGroupOffsetsAsync("workers");
        consumer.StoreOffset(result!.Value);
        await consumer.CloseAsync();
        var offsetsAfterStore = await admin.ListConsumerGroupOffsetsAsync("workers");

        await Assert.That(offsetsBeforeStore.ContainsKey(partition)).IsFalse();
        await Assert.That(offsetsAfterStore[partition]).IsEqualTo(1);
    }

    [Test]
    public async Task Consumer_BatchOffsetStore_MatchesOrderedValidationAndCommitSemantics()
    {
        var cluster = new InMemoryKafkaCluster();
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = false
            });
        var admin = new InMemoryAdminClient(cluster);
        consumer.Assign(
            new TopicPartition("jobs", 0),
            new TopicPartition("tasks", 0));
        TopicPartitionOffset[] invalidOffsets =
        [
            new("jobs", 0, 1),
            new("jobs", -1, 2)
        ];

        await Assert.That(() => consumer.StoreOffsets(invalidOffsets))
            .Throws<ArgumentOutOfRangeException>();
        await consumer.CommitAsync();
        await Assert.That(await admin.ListConsumerGroupOffsetsAsync("workers")).IsEmpty();

        var offsets = new StructOffsetList(
        [
            new("jobs", 0, 1, leaderEpoch: 1),
            new("tasks", 0, 2, leaderEpoch: 2),
            new("jobs", 0, 3, leaderEpoch: 3)
        ]);
        consumer.StoreOffsets(offsets);
        await consumer.CommitAsync();

        var committed = await admin.ListConsumerGroupOffsetsAsync("workers");
        await Assert.That(committed[new TopicPartition("jobs", 0)]).IsEqualTo(3);
        await Assert.That(committed[new TopicPartition("tasks", 0)]).IsEqualTo(2);
        await Assert.That(cluster.GetCommittedOffsetInfo("workers", new TopicPartition("jobs", 0))!.Value.LeaderEpoch)
            .IsEqualTo(3);
        await Assert.That(cluster.GetCommittedOffsetInfo("workers", new TopicPartition("tasks", 0))!.Value.LeaderEpoch)
            .IsEqualTo(2);

        TopicPartitionOffset[] spanOffsets =
        [
            new("jobs", 0, 4, leaderEpoch: 4),
            new("tasks", 0, 5, leaderEpoch: 5)
        ];
        consumer.StoreOffsets(spanOffsets.AsSpan());
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedOffsetInfo("workers", new TopicPartition("jobs", 0))!.Value.LeaderEpoch)
            .IsEqualTo(4);
        await Assert.That(cluster.GetCommittedOffsetInfo("workers", new TopicPartition("tasks", 0))!.Value.LeaderEpoch)
            .IsEqualTo(5);

        consumer.StoreOffset(new TopicPartitionOffset("jobs", 0, 6, leaderEpoch: -1));
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedOffsetInfo("workers", new TopicPartition("jobs", 0))!.Value.LeaderEpoch)
            .IsEqualTo(-1);
    }

    [Test]
    [Arguments("Seek")]
    [Arguments("SeekToBeginning")]
    [Arguments("SeekToEnd")]
    public async Task Consumer_ManualOffsetStore_CommitAsyncDoesNotCommitSeekedPositionUntilStored(string seekOperation)
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = false
            });
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("jobs", 0);

        await producer.ProduceAsync("jobs", "a", "one");
        await producer.ProduceAsync("jobs", "b", "two");
        consumer.Subscribe("jobs");
        ApplySeek(consumer, seekOperation, partition);

        await consumer.CommitAsync();

        var offsetsBeforeStore = await admin.ListConsumerGroupOffsetsAsync("workers");

        consumer.StoreOffset(new TopicPartitionOffset("jobs", 0, 1));

        await consumer.CommitAsync();

        var offsetsAfterStore = await admin.ListConsumerGroupOffsetsAsync("workers");

        await Assert.That(offsetsBeforeStore.ContainsKey(partition)).IsFalse();
        await Assert.That(offsetsAfterStore[partition]).IsEqualTo(1);
    }

    [Test]
    public async Task Admin_CreatesDescribesAndDeletesTopics()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { AutoCreateTopics = false });
        var admin = new InMemoryAdminClient(cluster);

        await admin.CreateTopicsAsync(
        [
            new NewTopic { Name = "events", NumPartitions = 3 }
        ]);
        var listings = await admin.ListTopicsAsync();
        var descriptions = await admin.DescribeTopicsAsync(["events"]);
        await admin.DeleteTopicsAsync(["events"]);
        var afterDelete = await admin.ListTopicsAsync();

        await Assert.That(listings.Single().Name).IsEqualTo("events");
        await Assert.That(descriptions["events"].Partitions.Count).IsEqualTo(3);
        await Assert.That(afterDelete).IsEmpty();
    }

    [Test]
    public async Task Admin_DescribesAndDeletesTopicsByIdThroughInterface()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { AutoCreateTopics = false });
        IAdminClient admin = new InMemoryAdminClient(cluster);
        await admin.CreateTopicsAsync([new NewTopic { Name = "events", NumPartitions = 3 }]);
        var topicId = (await admin.ListTopicsAsync()).Single().TopicId;
        var unknownId = Guid.NewGuid();

        var descriptions = await admin.DescribeTopicsAsync([topicId, topicId, unknownId]);
        await admin.DeleteTopicsAsync([topicId, topicId]);

        await Assert.That(descriptions.Count).IsEqualTo(2);
        await Assert.That(descriptions[topicId].Name).IsEqualTo("events");
        await Assert.That(descriptions[topicId].TopicId).IsEqualTo(topicId);
        await Assert.That(descriptions[topicId].Partitions.Count).IsEqualTo(3);
        await Assert.That(descriptions[unknownId].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
        await Assert.That(await admin.ListTopicsAsync()).IsEmpty();
    }

    [Test]
    public async Task Admin_DeleteTopicsById_ValidatesAllIdsBeforeDeleting()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { AutoCreateTopics = false });
        IAdminClient admin = new InMemoryAdminClient(cluster);
        await admin.CreateTopicsAsync([new NewTopic { Name = "events", NumPartitions = 1 }]);
        var topicId = (await admin.ListTopicsAsync()).Single().TopicId;

        async Task Delete() => await admin.DeleteTopicsAsync([topicId, Guid.Empty]);

        await Assert.That(Delete).Throws<ArgumentException>();
        await Assert.That(await admin.ListTopicsAsync()).Contains(topic => topic.TopicId == topicId);
    }

    [Test]
    public async Task Admin_ClientQuotas_AlterDescribeAndRemoveRoundTrips()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var entity = ClientQuotaEntity.For(
            ClientQuotaEntityComponent.User("alice"),
            ClientQuotaEntityComponent.ClientId("orders"));

        await admin.AlterClientQuotasAsync(
        [
            new ClientQuotaAlteration
            {
                Entity = entity,
                Operations =
                [
                    ClientQuotaOperation.Set("consumer_byte_rate", 1024),
                    ClientQuotaOperation.Set("producer_byte_rate", 2048)
                ]
            }
        ]);
        var all = await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All());
        var filtered = await admin.DescribeClientQuotasAsync(new ClientQuotaFilter
        {
            Components = [ClientQuotaFilterComponent.Exact(ClientQuotaEntityType.User, "alice")]
        });
        var strictFiltered = await admin.DescribeClientQuotasAsync(new ClientQuotaFilter
        {
            Components = [ClientQuotaFilterComponent.Exact(ClientQuotaEntityType.User, "alice")],
            Strict = true
        });

        await Assert.That(all.TryGetValue(entity, out var quotas)).IsTrue();
        await Assert.That(quotas!["consumer_byte_rate"]).IsEqualTo(1024);
        await Assert.That(quotas["producer_byte_rate"]).IsEqualTo(2048);
        await Assert.That(filtered).ContainsKey(entity);
        await Assert.That(strictFiltered).IsEmpty();

        await admin.AlterClientQuotasAsync([ClientQuotaAlteration.Remove(entity, "consumer_byte_rate")]);
        var afterRemove = await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All());
        await Assert.That(afterRemove[entity]).DoesNotContainKey("consumer_byte_rate");
        await Assert.That(afterRemove[entity]).ContainsKey("producer_byte_rate");

        await admin.AlterClientQuotasAsync([ClientQuotaAlteration.Remove(entity, "producer_byte_rate")]);
        var afterRemovingAll = await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All());
        await Assert.That(afterRemovingAll).IsEmpty();
    }

    [Test]
    public async Task Admin_DelegationTokens_RoundTripAndFilterByOwner()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var owner = new DelegationTokenPrincipal("User", "owner");
        var renewer = new DelegationTokenPrincipal("User", "renewer");

        var token = await admin.CreateDelegationTokenAsync(
            owner,
            [renewer],
            TimeSpan.FromMinutes(30));
        var described = await admin.DescribeDelegationTokensAsync([owner]);
        var filtered = await admin.DescribeDelegationTokensAsync([new DelegationTokenPrincipal("User", "other")]);
        var renewed = await admin.RenewDelegationTokenAsync(token.Hmac, TimeSpan.FromMinutes(5));
        var expired = await admin.ExpireDelegationTokenAsync(token.Hmac, TimeSpan.Zero);
        var afterExpire = await admin.DescribeDelegationTokensAsync();

        await Assert.That(described.Count).IsEqualTo(1);
        await Assert.That(described[0].TokenId).IsEqualTo(token.TokenId);
        await Assert.That(described[0].Renewers.Single()).IsEqualTo(renewer);
        await Assert.That(filtered).IsEmpty();
        await Assert.That(renewed <= token.MaxTimestamp).IsTrue();
        await Assert.That(afterExpire.Single().ExpiryTimestamp).IsEqualTo(expired);
    }

    [Test]
    public async Task Admin_DelegationTokenRenewMissing_ThrowsKafkaException()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.RenewDelegationTokenAsync([1, 2, 3]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.DelegationTokenNotFound);
    }

    [Test]
    public async Task Consumer_SubscribePattern_AssignsMatchingInMemoryTopics()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders-created");
        cluster.CreateTopic("payments-created");
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "orders-service",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync("orders-created", "order-1", "created");
        await producer.ProduceAsync("payments-created", "payment-1", "created");
        consumer.SubscribePattern("orders-.*");

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(consumer.Subscription).IsEmpty();
        await Assert.That(consumer.SubscriptionPattern).IsEqualTo("orders-.*");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Topic).IsEqualTo("orders-created");
    }

    [Test]
    public async Task Admin_TransactionIntrospection_ReturnsEmptyInMemoryState()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var topicPartition = new TopicPartition("events", 0);

        var listings = await admin.ListTransactionsAsync();
        var descriptions = await admin.DescribeTransactionsAsync(["tx-1"]);
        var producers = await admin.DescribeProducersAsync([topicPartition]);
        var termination = await admin.ForceTerminateTransactionAsync("tx-1");

        await Assert.That(listings.Transactions).IsEmpty();
        await Assert.That(listings.UnknownStateFilters).IsEmpty();
        await Assert.That(descriptions["tx-1"].ErrorCode).IsEqualTo(ErrorCode.TransactionalIdNotFound);
        await Assert.That(producers[topicPartition].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(producers[topicPartition].ActiveProducers).IsEmpty();
        await Assert.That(termination.TransactionalId).IsEqualTo("tx-1");
        await Assert.That(termination.ErrorCode).IsEqualTo(ErrorCode.TransactionalIdNotFound);
        await Assert.That(termination.IsRetriable).IsFalse();
    }

    [Test]
    public async Task Admin_DescribeLogDirs_ReturnsInMemoryReplicaInfo()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var admin = new InMemoryAdminClient(cluster);

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "events",
            Partition = 1,
            Key = "k",
            Value = "v"
        });

        var result = await admin.DescribeLogDirsAsync([0], [new TopicPartition("events", 1)]);

        await Assert.That(result.Keys).IsEquivalentTo([0]);
        await Assert.That(result[0].Keys).IsEquivalentTo(["in-memory"]);
        var replica = result[0]["in-memory"].ReplicaInfos[new TopicPartition("events", 1)];
        await Assert.That(replica.Size).IsEqualTo(1);
        await Assert.That(replica.OffsetLag).IsEqualTo(0);
        await Assert.That(replica.IsFuture).IsFalse();
    }

    [Test]
    public async Task Admin_AlterReplicaLogDirs_ReturnsSuccessPerReplica()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var replica = new TopicPartitionReplica("events", 0, 0);

        var result = await admin.AlterReplicaLogDirsAsync(new Dictionary<TopicPartitionReplica, string>
        {
            [replica] = "in-memory"
        });

        await Assert.That(result[replica].TopicPartitionReplica).IsEqualTo(replica);
        await Assert.That(result[replica].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task Admin_DescribeReplicaLogDirs_ReturnsSelectedReplicaInfo()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events", partitionCount: 2);
        var admin = new InMemoryAdminClient(cluster);
        var existing = new TopicPartitionReplica("events", 1, 0);
        var missing = new TopicPartitionReplica("events", 2, 0);

        var result = await admin.DescribeReplicaLogDirsAsync([existing, missing, existing]);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[existing].CurrentReplicaLogDir).IsEqualTo("in-memory");
        await Assert.That(result[existing].CurrentReplicaOffsetLag).IsEqualTo(0);
        await Assert.That(result[existing].FutureReplicaLogDir).IsNull();
        await Assert.That(result[existing].FutureReplicaOffsetLag).IsEqualTo(-1);
        await Assert.That(result[missing].CurrentReplicaLogDir).IsNull();
        await Assert.That(result[missing].CurrentReplicaOffsetLag).IsEqualTo(-1);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Admin_DescribeReplicaLogDirs_DoesNotCreateMissingTopic(bool autoCreateTopics)
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions
        {
            AutoCreateTopics = autoCreateTopics
        });
        var admin = new InMemoryAdminClient(cluster);
        var missing = new TopicPartitionReplica("missing", 0, 0);

        var result = await admin.DescribeReplicaLogDirsAsync([missing]);

        await Assert.That(result[missing].CurrentReplicaLogDir).IsNull();
        await Assert.That(result[missing].CurrentReplicaOffsetLag).IsEqualTo(-1);
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task Producer_PurgeAsync_IsNoOpForInMemoryProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);

        await producer.PurgeAsync(PurgeOptions.None);
        await producer.PurgeAsync(PurgeOptions.All);
    }

    [Test]
    public async Task Producer_PurgeAsync_IsNoOpForAlreadyAppendedRecords()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);

        await producer.ProduceAsync("purge", "k", "v");
        await producer.PurgeAsync(PurgeOptions.All);

        await Assert.That(cluster.ReadRecords("purge").Count).IsEqualTo(1);
        await producer.PurgeAsync((PurgeOptions)8);
        await Assert.That(cluster.ReadRecords("purge").Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(PurgeOptions.Queue)]
    [Arguments(PurgeOptions.InFlight)]
    [Arguments(PurgeOptions.All)]
    public async Task Producer_PurgeAsync_RejectsActiveTransaction(PurgeOptions options)
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();

        await producer.PurgeAsync(PurgeOptions.None);
        var action = async () => await producer.PurgeAsync(options);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ShareConsumer_ReleaseDoesNotAdvanceOffset_AcceptDoes()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var shareConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-workers" });

        await producer.ProduceAsync("shared", "k", "v");
        shareConsumer.Subscribe("shared");

        var first = await shareConsumer.PollAsync().FirstAsync();
        shareConsumer.Acknowledge(first, AcknowledgeType.Release);
        await shareConsumer.CommitAsync();
        var second = await shareConsumer.PollAsync().FirstAsync();
        shareConsumer.Acknowledge(second);
        await shareConsumer.CommitAsync();
        var admin = new InMemoryAdminClient(cluster);
        var offsets = await admin.DescribeShareGroupOffsetsAsync("share-workers");

        await Assert.That(first.Offset).IsEqualTo(0);
        await Assert.That(second.Offset).IsEqualTo(0);
        await Assert.That(offsets.Single().StartOffset).IsEqualTo(1);
    }

    [Test]
    public async Task Admin_DeleteShareGroupsDeletesOnlyRequestedGroups()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("shared", 0);
        await admin.AlterShareGroupOffsetsAsync(
            "shared-id",
            [new ShareGroupOffsetAlteration { TopicPartition = partition, StartOffset = 3 }]);
        await admin.AlterConsumerGroupOffsetsAsync(
            "shared-id",
            [new TopicPartitionOffset(partition.Topic, partition.Partition, 7)]);

        var results = await admin.DeleteShareGroupsAsync(["shared-id"]);

        await Assert.That(results["shared-id"].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(await admin.DescribeShareGroupOffsetsAsync("shared-id")).IsEmpty();
        await Assert.That((await admin.ListConsumerGroupOffsetsAsync("shared-id"))[partition]).IsEqualTo(7);
    }

    [Test]
    public async Task Admin_ListShareGroupsUsesOnlyShareState()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("shared", 0);
        await admin.AlterShareGroupOffsetsAsync(
            "offset-only",
            [new ShareGroupOffsetAlteration { TopicPartition = partition, StartOffset = 3 }]);
        await admin.AlterConsumerGroupOffsetsAsync(
            "consumer-only",
            [new TopicPartitionOffset(partition.Topic, partition.Partition, 7)]);
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "member-only" });
        consumer.Subscribe("shared");

        var activeGroups = await admin.ListShareGroupsAsync();

        await Assert.That(activeGroups.Select(static group => group.GroupId))
            .IsEquivalentTo(["member-only", "offset-only"]);
        await Assert.That(activeGroups.All(static group => group.ProtocolType == "share")).IsTrue();
        await Assert.That(activeGroups.Single(static group => group.GroupId == "member-only").State)
            .IsEqualTo("Stable");
        await Assert.That(activeGroups.Single(static group => group.GroupId == "offset-only").State)
            .IsEqualTo("Empty");

        await consumer.CloseAsync();
        var inactiveGroups = await admin.ListShareGroupsAsync();

        await Assert.That(inactiveGroups.Select(static group => group.GroupId))
            .IsEquivalentTo(["member-only", "offset-only"]);
        await Assert.That(inactiveGroups.All(static group => group.State == "Empty")).IsTrue();

        var deletion = await admin.DeleteShareGroupsAsync(["member-only"]);
        await Assert.That(deletion["member-only"].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task Admin_ListShareGroupsHonorsStateFilter()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("shared", 0);
        await admin.AlterShareGroupOffsetsAsync(
            "offset-only",
            [new ShareGroupOffsetAlteration { TopicPartition = partition, StartOffset = 3 }]);
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "member-only" });
        consumer.Subscribe(partition.Topic);

        var emptyGroups = await admin.ListShareGroupsAsync(new ListShareGroupsOptions
        {
            States = ["Empty"]
        });
        var stableGroups = await admin.ListShareGroupsAsync(new ListShareGroupsOptions
        {
            States = ["stable"]
        });

        await Assert.That(emptyGroups.Select(static group => group.GroupId))
            .IsEquivalentTo(["offset-only"]);
        await Assert.That(stableGroups.Select(static group => group.GroupId))
            .IsEquivalentTo(["member-only"]);
    }

    [Test]
    public async Task Admin_AlterEmptyShareGroupOffsetsDoesNotCreateGroup()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);

        await admin.AlterShareGroupOffsetsAsync("phantom", []);
        var groups = await admin.ListShareGroupsAsync();
        var deletion = await admin.DeleteShareGroupsAsync(["phantom"]);

        await Assert.That(groups).IsEmpty();
        await Assert.That(deletion["phantom"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    [Test]
    public async Task Admin_DeleteLastShareGroupOffsetRemovesGroupState()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("shared", 0);
        await admin.AlterShareGroupOffsetsAsync(
            "offset-only",
            [new ShareGroupOffsetAlteration { TopicPartition = partition, StartOffset = 3 }]);

        await admin.DeleteShareGroupOffsetsAsync("offset-only", [partition.Topic]);
        var groups = await admin.ListShareGroupsAsync();
        var deletion = await admin.DeleteShareGroupsAsync(["offset-only"]);

        await Assert.That(groups).IsEmpty();
        await Assert.That(deletion["offset-only"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    [Test]
    public async Task ShareConsumer_SubscribeCannotRegisterAfterConcurrentClose()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var admin = new InMemoryAdminClient(cluster);
        var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "subscribe-close-race" });
        await producer.ProduceAsync("shared", "k", "v");
        var gate = typeof(InMemoryShareConsumer<string, string>).GetField(
            "_gate",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                consumer.Subscribe("shared");
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "in-memory-share-subscribe-test-worker"
        };
        var gateHeld = false;
        bool subscribeReachedGate;
        try
        {
            Monitor.Enter(gate, ref gateHeld);
            thread.Start();
            subscribeReachedGate = SpinWait.SpinUntil(
                () => thread.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin),
                TimeSpan.FromSeconds(5));
            consumer.CloseAsync().AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            if (gateHeld)
                Monitor.Exit(gate);
        }

        await Assert.That(subscribeReachedGate).IsTrue();
        await Assert.That(async () => await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)))
            .Throws<ObjectDisposedException>();
        var deletion = await admin.DeleteShareGroupsAsync(["subscribe-close-race"]);
        await Assert.That(deletion["subscribe-close-race"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    [Test]
    public async Task Admin_DeleteShareGroupsValidatesBatchBeforeDeleting()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("shared", 0);
        await admin.AlterShareGroupOffsetsAsync(
            "share-preserve",
            [new ShareGroupOffsetAlteration { TopicPartition = partition, StartOffset = 3 }]);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await admin.DeleteShareGroupsAsync(["share-preserve", "share-preserve"]));

        await Assert.That((await admin.DescribeShareGroupOffsetsAsync("share-preserve")).Single().StartOffset)
            .IsEqualTo(3);
    }

    [Test]
    public async Task Admin_DeleteShareGroupsRejectsActiveGroup()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("shared", 0);
        await admin.AlterShareGroupOffsetsAsync(
            "share-active",
            [new ShareGroupOffsetAlteration { TopicPartition = partition, StartOffset = 3 }]);
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-active" });
        consumer.Subscribe("shared");

        var activeResult = await admin.DeleteShareGroupsAsync(["share-active"]);

        await Assert.That(activeResult["share-active"].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        await Assert.That((await admin.DescribeShareGroupOffsetsAsync("share-active")).Single().StartOffset)
            .IsEqualTo(3);

        await consumer.CloseAsync();
        var inactiveResult = await admin.DeleteShareGroupsAsync(["share-active"]);

        await Assert.That(inactiveResult["share-active"].ErrorCode).IsEqualTo(ErrorCode.None);

        var missingResult = await admin.DeleteShareGroupsAsync(["share-active"]);

        await Assert.That(missingResult["share-active"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    [Test]
    public async Task Admin_DeleteShareGroupsWaitsForEverySharedMemberRegistration()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("shared", 0);
        await admin.AlterShareGroupOffsetsAsync(
            "shared-member-id",
            [new ShareGroupOffsetAlteration { TopicPartition = partition, StartOffset = 3 }]);
        await using var first = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "shared-member-id", MemberId = "worker" });
        await using var second = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "shared-member-id", MemberId = "worker" });
        first.Subscribe("shared");
        second.Subscribe("shared");

        await first.CloseAsync();
        var activeResult = await admin.DeleteShareGroupsAsync(["shared-member-id"]);

        await Assert.That(activeResult["shared-member-id"].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        await Assert.That((await admin.DescribeShareGroupOffsetsAsync("shared-member-id")).Single().StartOffset)
            .IsEqualTo(3);

        await second.CloseAsync();
        var inactiveResult = await admin.DeleteShareGroupsAsync(["shared-member-id"]);

        await Assert.That(inactiveResult["shared-member-id"].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task Admin_DeleteShareGroupsClearsShareDeliveryCounts()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var admin = new InMemoryAdminClient(cluster);
        await producer.ProduceAsync("shared", "k", "v");

        await using (var firstConsumer = new InMemoryShareConsumer<string, string>(
                         cluster,
                         new InMemoryShareConsumerOptions { GroupId = "share-recreated" }))
        {
            firstConsumer.Subscribe("shared");
            var firstDelivery = await firstConsumer.PollAsync().FirstAsync();
            await Assert.That(firstDelivery.DeliveryCount).IsEqualTo(1);
            firstConsumer.Acknowledge(firstDelivery, AcknowledgeType.Release);
            await firstConsumer.CommitAsync();
        }

        var deletion = await admin.DeleteShareGroupsAsync(["share-recreated"]);
        await Assert.That(deletion["share-recreated"].ErrorCode).IsEqualTo(ErrorCode.None);

        await using var recreatedConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-recreated" });
        recreatedConsumer.Subscribe("shared");
        var recreatedDelivery = await recreatedConsumer.PollAsync().FirstAsync();

        await Assert.That(recreatedDelivery.DeliveryCount).IsEqualTo(1);
    }

    [Test]
    public async Task ShareConsumer_ClosedMemberCannotRecreateDeletedGroupState()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("shared", 0);
        await producer.ProduceAsync(partition.Topic, "k", "v");
        await admin.AlterShareGroupOffsetsAsync(
            "deleted-share",
            [new ShareGroupOffsetAlteration { TopicPartition = partition, StartOffset = 0 }]);
        const string memberId = "closed-member";
        var registration = cluster.RegisterShareGroupMember("deleted-share", memberId);

        cluster.UnregisterShareGroupMember("deleted-share", memberId, registration);
        var deletion = await admin.DeleteShareGroupsAsync(["deleted-share"]);
        var acquired = cluster.TryAcquireShareRecord(
            "deleted-share",
            memberId,
            registration,
            partition,
            offset: 0,
            out _,
            out _);

        await Assert.That(deletion["deleted-share"].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(acquired).IsFalse();
        await Assert.That(await admin.ListShareGroupsAsync()).IsEmpty();

        await using var recreatedConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "deleted-share" });
        recreatedConsumer.Subscribe(partition.Topic);
        var delivery = await recreatedConsumer.PollAsync().FirstAsync();

        await Assert.That(delivery.DeliveryCount).IsEqualTo(1);
    }

    [Test]
    public async Task ShareConsumer_DuplicateMemberRegistrationsUseIndependentTokens()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var partition = new TopicPartition("shared", 0);
        await producer.ProduceAsync(partition.Topic, "k", "v");
        var closedRegistration = cluster.RegisterShareGroupMember("shared-group", "shared-member");
        var activeRegistration = cluster.RegisterShareGroupMember("shared-group", "shared-member");

        cluster.UnregisterShareGroupMember("shared-group", "shared-member", closedRegistration);
        var closedAcquired = cluster.TryAcquireShareRecord(
            "shared-group",
            "shared-member",
            closedRegistration,
            partition,
            offset: 0,
            out _,
            out _);
        var activeAcquired = cluster.TryAcquireShareRecord(
            "shared-group",
            "shared-member",
            activeRegistration,
            partition,
            offset: 0,
            out _,
            out _);

        await Assert.That(closedAcquired).IsFalse();
        await Assert.That(activeAcquired).IsTrue();
    }

    [Test]
    public async Task ShareConsumer_StaleRegistrationCannotReleaseReplacementLease()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var partition = new TopicPartition("shared", 0);
        await producer.ProduceAsync(partition.Topic, "k", "v");
        const string groupId = "shared-group";
        const string memberId = "shared-member";
        var staleRegistration = cluster.RegisterShareGroupMember(groupId, memberId);
        var staleAcquired = cluster.TryAcquireShareRecord(
            groupId,
            memberId,
            staleRegistration,
            partition,
            offset: 0,
            out var record,
            out var staleDeliveryCount);
        await Assert.That(staleAcquired).IsTrue();
        await Assert.That(staleDeliveryCount).IsEqualTo(1);

        var replacementRegistration = cluster.RegisterShareGroupMember(groupId, memberId);
        cluster.UnregisterShareGroupMember(groupId, memberId, staleRegistration);
        var replacementAcquired = cluster.TryAcquireShareRecord(
            groupId,
            memberId,
            replacementRegistration,
            partition,
            offset: 0,
            out _,
            out var replacementDeliveryCount);
        await Assert.That(replacementAcquired).IsTrue();
        await Assert.That(replacementDeliveryCount).IsEqualTo(2);

        cluster.ReleaseShareRecords(
            groupId,
            memberId,
            staleRegistration,
            [new TopicPartitionOffset(partition.Topic, partition.Partition, record.Offset)]);
        const string thirdMemberId = "third-member";
        var thirdRegistration = cluster.RegisterShareGroupMember(groupId, thirdMemberId);
        var thirdAcquired = cluster.TryAcquireShareRecord(
            groupId,
            thirdMemberId,
            thirdRegistration,
            partition,
            offset: 0,
            out _,
            out _);

        await Assert.That(thirdAcquired).IsFalse();
    }

    [Test]
    public async Task ShareConsumer_ReleaseGapStopsContiguousCommit()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var shareConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-gap" });

        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "shared",
                Partition = 0,
                Key = $"k-{i}",
                Value = $"v-{i}"
            });
        }

        shareConsumer.Subscribe("shared");
        var records = new List<ShareConsumeResult<string, string>>();
        await foreach (var record in shareConsumer.PollAsync())
            records.Add(record);

        shareConsumer.Acknowledge(records[0], AcknowledgeType.Accept);
        shareConsumer.Acknowledge(records[1], AcknowledgeType.Release);
        shareConsumer.Acknowledge(records[2], AcknowledgeType.Accept);
        await shareConsumer.CommitAsync();

        var redelivered = await shareConsumer.PollAsync().FirstAsync();
        var admin = new InMemoryAdminClient(cluster);
        var offsets = await admin.DescribeShareGroupOffsetsAsync("share-gap");

        await Assert.That(records.Select(record => record.Offset).ToArray()).IsEquivalentTo([0L, 1L, 2L]);
        await Assert.That(offsets.Single().StartOffset).IsEqualTo(1);
        await Assert.That(redelivered.Offset).IsEqualTo(1);
    }

    [Test]
    public async Task ShareConsumer_PartialPollOnlyCommitsYieldedRecords()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var shareConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions
            {
                GroupId = "share-partial",
                MaxPollRecords = 2
            });

        for (var i = 0; i < 2; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "shared",
                Partition = 0,
                Key = $"k-{i}",
                Value = $"v-{i}"
            });
        }

        shareConsumer.Subscribe("shared");
        var first = await shareConsumer.PollAsync().FirstAsync();
        await shareConsumer.CommitAsync();
        var second = await shareConsumer.PollAsync().FirstAsync();

        await Assert.That(first.Offset).IsEqualTo(0);
        await Assert.That(second.Offset).IsEqualTo(1);
    }

    [Test]
    public async Task ShareConsumer_LeasesRecordsAcrossMembersUntilRelease()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var firstConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-leases", MemberId = "a" });
        var secondConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-leases", MemberId = "b" });

        await producer.ProduceAsync("leased", "k", "v");
        firstConsumer.Subscribe("leased");
        secondConsumer.Subscribe("leased");

        var first = await firstConsumer.PollAsync().FirstAsync();
        var blocked = new List<ShareConsumeResult<string, string>>();
        await foreach (var record in secondConsumer.PollAsync())
            blocked.Add(record);

        firstConsumer.Acknowledge(first, AcknowledgeType.Release);
        await firstConsumer.CommitAsync();
        var redelivered = await secondConsumer.PollAsync().FirstAsync();

        await Assert.That(blocked).IsEmpty();
        await Assert.That(redelivered.Offset).IsEqualTo(0);
        await Assert.That(redelivered.DeliveryCount).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerGroupMembersSplitAssignedPartitions()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("balanced", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var firstConsumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "balanced-group",
                MemberId = "a",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        var secondConsumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "balanced-group",
                MemberId = "b",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "balanced",
            Partition = 0,
            Key = "k-0",
            Value = "v-0"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "balanced",
            Partition = 1,
            Key = "k-1",
            Value = "v-1"
        });

        firstConsumer.Subscribe("balanced");
        secondConsumer.Subscribe("balanced");

        var firstAssignment = firstConsumer.Assignment;
        var secondAssignment = secondConsumer.Assignment;
        var first = await firstConsumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(50));
        var second = await secondConsumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(50));

        await Assert.That(firstAssignment.Intersect(secondAssignment).ToArray()).IsEmpty();
        await Assert.That(firstAssignment.Concat(secondAssignment).Select(item => item.Partition).ToArray())
            .IsEquivalentTo([0, 1]);
        await Assert.That(new[] { first!.Value.Partition, second!.Value.Partition })
            .IsEquivalentTo([0, 1]);
    }

    [Test]
    public async Task ConsumerClose_RemainInGroup_DoesNotCreateImmortalInMemoryMember()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("close-remain");
        var producer = new InMemoryProducer<string, string>(cluster);
        var firstConsumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "close-remain-group",
                MemberId = "a-first",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        firstConsumer.Subscribe("close-remain");
        await Assert.That(firstConsumer.Assignment).Count().IsEqualTo(1);

        await firstConsumer.CloseAsync(new ConsumerCloseOptions
        {
            GroupMembershipOperation = ConsumerGroupMembershipOperation.RemainInGroup
        });

        await producer.ProduceAsync("close-remain", "key", "value");
        await using var secondConsumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "close-remain-group",
                MemberId = "b-second",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        secondConsumer.Subscribe("close-remain");

        var result = await secondConsumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(50));

        await Assert.That(secondConsumer.Assignment).Count().IsEqualTo(1);
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Consumer_AssignFailureDoesNotLeavePartialState()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("strict");
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.None });
        var partition = new TopicPartition("strict", 0);

        await Assert.That(() => consumer.Assign(partition)).Throws<InvalidOperationException>();
        await Assert.That(() => consumer.IncrementalAssign([new TopicPartitionOffset("strict", 0, -1)]))
            .Throws<InvalidOperationException>();

        await Assert.That(consumer.Assignment).IsEmpty();
        await Assert.That(consumer.GetPosition(partition)).IsNull();
    }

    [Test]
    public async Task WaitForRecordsAsync_ReturnsWhenRecordWasAppendedBeforeWait()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);

        await producer.ProduceAsync("wakeups", "k", "v");

        await InvokeWaitForRecordsAsync(cluster, TimeSpan.FromMilliseconds(50), CancellationToken.None);
    }

    [Test]
    public async Task ProduceLatency_ObservesCancellation()
    {
        var cluster = new InMemoryKafkaCluster
        {
            ProduceLatency = TimeSpan.FromSeconds(10)
        };
        var producer = new InMemoryProducer<string, string>(cluster);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.That(async () => await producer.ProduceAsync("slow", "k", "v", cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ProduceFailure_CanBeConfiguredAndCleared()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);

        cluster.FailProduces("failures", new InvalidOperationException("produce failed"));

        await Assert.That(async () => await producer.ProduceAsync("failures", "k", "v"))
            .Throws<InvalidOperationException>();

        await Assert.That(cluster.ClearProduceFailure("failures")).IsTrue();
        var metadata = await producer.ProduceAsync("failures", "k", "v");

        await Assert.That(metadata.Topic).IsEqualTo("failures");
        await Assert.That(cluster.ClearProduceFailure("failures")).IsFalse();
    }

    [Test]
    public async Task FireAsync_DoesNotThrowDeliveryFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        cluster.FailProduces("fire", new InvalidOperationException("produce failed"));

        await producer.FireAsync("fire", "k", "v");
    }

    [Test]
    public async Task AddDekafInMemory_RegistersClientDoubles()
    {
        var services = new ServiceCollection();

        services.AddDekafInMemory(options => options.DefaultPartitionCount = 2);

        await using var provider = services.BuildServiceProvider();
        var cluster = provider.GetRequiredService<InMemoryKafkaCluster>();
        var producer = provider.GetRequiredService<IKafkaProducer<string, string>>();
        var consumer = provider.GetRequiredService<IKafkaConsumer<string, string>>();
        var admin = provider.GetRequiredService<IAdminClient>();
        var shareConsumer = provider.GetRequiredService<IKafkaShareConsumer<string, string>>();

        var metadata = await producer.ProduceAsync("di-topic", "k", "v");

        await Assert.That(cluster.Options.DefaultPartitionCount).IsEqualTo(2);
        await Assert.That(metadata.Topic).IsEqualTo("di-topic");
        await Assert.That(consumer).IsTypeOf<InMemoryConsumer<string, string>>();
        await Assert.That(admin).IsTypeOf<InMemoryAdminClient>();
        await Assert.That(shareConsumer).IsTypeOf<InMemoryShareConsumer<string, string>>();
    }

    [Test]
    public async Task AddDekafInMemory_ReplacesClosedClientRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaProducer<string, string>>(_ => throw new InvalidOperationException("real producer"));
        services.AddSingleton<IKafkaConsumer<string, string>>(_ => throw new InvalidOperationException("real consumer"));
        services.AddSingleton<IKafkaShareConsumer<string, string>>(_ => throw new InvalidOperationException("real share consumer"));
        services.AddSingleton<IAdminClient>(_ => throw new InvalidOperationException("real admin"));
        services.AddSingleton<IInitializableKafkaClient>(_ => throw new InvalidOperationException("real initializer"));

        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider();
        var producer = provider.GetRequiredService<IKafkaProducer<string, string>>();
        var consumer = provider.GetRequiredService<IKafkaConsumer<string, string>>();
        var shareConsumer = provider.GetRequiredService<IKafkaShareConsumer<string, string>>();
        var admin = provider.GetRequiredService<IAdminClient>();

        await Assert.That(producer).IsTypeOf<InMemoryProducer<string, string>>();
        await Assert.That(consumer).IsTypeOf<InMemoryConsumer<string, string>>();
        await Assert.That(shareConsumer).IsTypeOf<InMemoryShareConsumer<string, string>>();
        await Assert.That(admin).IsTypeOf<InMemoryAdminClient>();
        await Assert.That(provider.GetServices<IInitializableKafkaClient>().ToArray()).IsEmpty();
    }

    private static Task InvokeWaitForRecordsAsync(
        InMemoryKafkaCluster cluster,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var method = typeof(InMemoryKafkaCluster).GetMethod(
            "WaitForRecordsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (Task)method.Invoke(cluster, [timeout, cancellationToken])!;
    }

    private static void ApplySeek(
        InMemoryConsumer<string, string> consumer,
        string seekOperation,
        TopicPartition partition)
    {
        switch (seekOperation)
        {
            case "Seek":
                consumer.Seek(new TopicPartitionOffset(partition.Topic, partition.Partition, 1));
                break;
            case "SeekToBeginning":
                consumer.SeekToBeginning(partition);
                break;
            case "SeekToEnd":
                consumer.SeekToEnd(partition);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(seekOperation), seekOperation, null);
        }
    }

    private sealed class PrefixDeserializer(string prefix) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            $"{prefix}:{Encoding.UTF8.GetString(data.Span)}";
    }

    private sealed class HeaderPresenceDeserializer : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            context.Headers is null ? "no-headers" : "headers";
    }

    private sealed class AsyncStringDeserializer : IAsyncDeserializer<string>
    {
        public ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Encoding.UTF8.GetString(data.Span));
    }

    private readonly struct StructOffsetList(TopicPartitionOffset[] offsets) : IReadOnlyList<TopicPartitionOffset>
    {
        public int Count => offsets.Length;

        public TopicPartitionOffset this[int index] => offsets[index];

        public IEnumerator<TopicPartitionOffset> GetEnumerator() =>
            ((IEnumerable<TopicPartitionOffset>)offsets).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => offsets.GetEnumerator();
    }
}
