using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerOffsetResetTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ConfiguredReset_ConsumesRecordsProducedBeforeJoin(bool byDuration, bool sharedConnections)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"share-reset-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(timeout.Token);
        await producer.ProduceAsync(topic, "key", "before-join", timeout.Token);
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers);
        var builder = sharedConnections
            ? client.CreateShareConsumer<string, string>(group)
            : Kafka.CreateShareConsumer<string, string>().WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group);
        if (byDuration) builder.WithAutoOffsetResetByDuration(TimeSpan.FromDays(1));
        else builder.WithAutoOffsetReset(AutoOffsetReset.Earliest);
        await using var consumer = await builder.WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .SubscribeTo(topic).BuildAsync(timeout.Token);
        await foreach (var record in consumer.PollAsync(timeout.Token))
        {
            await Assert.That(record.Value).IsEqualTo("before-join");
            consumer.Acknowledge(record);
            break;
        }
        await consumer.CommitAsync(timeout.Token);
        await consumer.CloseAsync(timeout.Token);
        // The borrowed admin client must not dispose the root's shared infrastructure.
        if (sharedConnections)
        {
            await using var sharedProducer = await client.CreateProducer<string, string>().BuildAsync(timeout.Token);
            await sharedProducer.ProduceAsync(topic, "key", "after-close", timeout.Token);
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments(AutoOffsetReset.Earliest)]
    [Arguments(AutoOffsetReset.Latest)]
    public async Task Initialization_ChangesOnlyExplicitGroupSetting(AutoOffsetReset? reset)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var group = $"share-reset-config-{Guid.NewGuid():N}";
        var resource = new ConfigResource { Type = ConfigResourceType.Group, Name = group };
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [resource] = [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        }, cancellationToken: timeout.Token);
        var builder = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group);
        if (reset is { } policy) builder.WithAutoOffsetReset(policy);
        await using var consumer = await builder.BuildAsync(timeout.Token);
        var configs = await admin.DescribeConfigsAsync([resource], cancellationToken: timeout.Token);
        await Assert.That(configs[resource].Single(config => config.Name == "share.auto.offset.reset").Value)
            .IsEqualTo(reset == AutoOffsetReset.Latest ? "latest" : "earliest");
        // A repeated InitializeAsync must not overwrite later changes by an administrator.
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [resource] = [ConfigAlter.Set("share.auto.offset.reset", "by_duration:PT1H")]
        }, cancellationToken: timeout.Token);
        await consumer.InitializeAsync(timeout.Token);
        configs = await admin.DescribeConfigsAsync([resource], cancellationToken: timeout.Token);
        await Assert.That(configs[resource].Single(config => config.Name == "share.auto.offset.reset").Value)
            .IsEqualTo("by_duration:PT1H");
    }
}
