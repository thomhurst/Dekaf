using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
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
}
