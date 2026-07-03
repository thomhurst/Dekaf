using System.Reflection;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using Dekaf.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryKafkaClusterTests
{
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
        await Assert.That(result.Value.Headers!.Single().GetValueAsString()).IsEqualTo("abc");
        await Assert.That(result.Value.TimestampMs).IsEqualTo(1234);
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
    public async Task Admin_TransactionIntrospection_ReturnsEmptyInMemoryState()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var topicPartition = new TopicPartition("events", 0);

        var listings = await admin.ListTransactionsAsync();
        var descriptions = await admin.DescribeTransactionsAsync(["tx-1"]);
        var producers = await admin.DescribeProducersAsync([topicPartition]);

        await Assert.That(listings.Transactions).IsEmpty();
        await Assert.That(listings.UnknownStateFilters).IsEmpty();
        await Assert.That(descriptions["tx-1"].ErrorCode).IsEqualTo(ErrorCode.TransactionalIdNotFound);
        await Assert.That(producers[topicPartition].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(producers[topicPartition].ActiveProducers).IsEmpty();
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
        var offsets = await admin.ListConsumerGroupOffsetsAsync("share-workers");

        await Assert.That(first.Offset).IsEqualTo(0);
        await Assert.That(second.Offset).IsEqualTo(0);
        await Assert.That(offsets[new TopicPartition("shared", 0)]).IsEqualTo(1);
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
        var offsets = await admin.ListConsumerGroupOffsetsAsync("share-gap");

        await Assert.That(records.Select(record => record.Offset).ToArray()).IsEquivalentTo([0L, 1L, 2L]);
        await Assert.That(offsets[new TopicPartition("shared", 0)]).IsEqualTo(1);
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
}
