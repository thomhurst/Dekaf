using System.Diagnostics;
using System.Reflection;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.Diagnostics;

[NotInParallel("ActivityListener")]
public sealed class KafkaClientStatusTests
{
    [Test]
    public async Task BuiltInClients_ExposeOptionalStatusCapabilityBeforeInitialization()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        await using var shareConsumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("status-share-group")
            .Build();
        await using var admin = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"]
        });

        var producerStatus = ((IKafkaClientStatusProvider)producer).GetStatus();
        var consumerStatus = ((IKafkaClientStatusProvider)consumer).GetStatus();
        var shareStatus = ((IKafkaClientStatusProvider)shareConsumer).GetStatus();
        var adminStatus = ((IKafkaClientStatusProvider)admin).GetStatus();

        await Assert.That(producerStatus.Role).IsEqualTo(KafkaClientRole.Producer);
        await Assert.That(producerStatus.ClusterId).IsNull();
        await Assert.That(producerStatus.Producer).IsNotNull();
        await Assert.That(producerStatus.Producer!.Value.BufferedBytes).IsEqualTo(0);
        await Assert.That(producerStatus.Producer.Value.BufferCapacityBytes).IsGreaterThan(0UL);
        await Assert.That(consumerStatus.Role).IsEqualTo(KafkaClientRole.Consumer);
        await Assert.That(consumerStatus.ConsumerGroup).IsNotNull();
        await Assert.That(consumerStatus.ConsumerGroup!.HasConsumerGroup).IsFalse();
        await Assert.That(shareStatus.Role).IsEqualTo(KafkaClientRole.ShareConsumer);
        await Assert.That(shareStatus.ConsumerGroup!.HasConsumerGroup).IsTrue();
        await Assert.That(adminStatus.Role).IsEqualTo(KafkaClientRole.Admin);
        await Assert.That(adminStatus.ClusterId).IsNull();
    }

    [Test]
    public async Task ConsumerStatus_ManualAssignmentDoesNotReportGroupParticipation()
    {
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "status-consumer",
                GroupId = "configured-but-unused-group"
            },
            Serializers.String,
            Serializers.String);
        consumer.Assign(new TopicPartition("orders", 0));

        var group = consumer.GetStatus().ConsumerGroup!;

        await Assert.That(group.HasConsumerGroup).IsFalse();
        await Assert.That(group.Assignment).IsEquivalentTo([new TopicPartition("orders", 0)]);
    }

    [Test]
    public async Task ConsumerStatus_TopicFilterReportsGroupParticipation()
    {
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "status-consumer",
                GroupId = "filter-group"
            },
            Serializers.String,
            Serializers.String);
        consumer.Subscribe(static topic => topic.StartsWith("orders-", StringComparison.Ordinal));

        var group = consumer.GetStatus().ConsumerGroup!;

        await Assert.That(group.HasConsumerGroup).IsTrue();
    }

    [Test]
    public async Task SharedClients_ReportCachedClusterIdentityDuringConcurrentRefreshes()
    {
        await using var client = Kafka.Connect("localhost:9092");
        await using var producer = client.CreateProducer<string, string>().Build();
        await using var consumer = client.CreateConsumer<string, string>("status-group").Build();
        await using var shareConsumer = client.CreateShareConsumer<string, string>("status-share-group").Build();
        await using var admin = client.CreateAdminClient().Build();

        var metadataManager = GetField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(CreateMetadata("cluster-status"));
        SetTrustedClusterId(metadataManager, "cluster-status");

        var identities = new IKafkaClientIdentity[]
        {
            (IKafkaClientIdentity)producer,
            (IKafkaClientIdentity)consumer,
            (IKafkaClientIdentity)shareConsumer,
            (IKafkaClientIdentity)admin
        };
        foreach (var identity in identities)
            await Assert.That(identity.ClusterId).IsEqualTo("cluster-status");

        var readers = Enumerable.Range(0, Environment.ProcessorCount)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < 1_000; i++)
                {
                    foreach (var identity in identities.Where(static identity => identity.ClusterId != "cluster-status"))
                    {
                        throw new InvalidOperationException(
                            $"Observed unpublished cluster identity '{identity.ClusterId ?? "<null>"}'.");
                    }
                }
            }))
            .ToArray();
        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 1_000; i++)
                metadataManager.Metadata.Update(CreateMetadata("cluster-status"));
        });

        await Task.WhenAll(readers.Append(writer));
    }

    [Test]
    public async Task RejectedMetadataClusterIdentity_IsNeverPublished()
    {
        await using var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "status-producer"
            },
            Serializers.String,
            Serializers.String);
        var metadataManager = GetField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(CreateMetadata("cluster-a"));
        SetTrustedClusterId(metadataManager, "cluster-a");

        metadataManager.Metadata.Update(new MetadataResponse
        {
            ErrorCode = ErrorCode.RebootstrapRequired,
            ClusterId = "rejected-cluster",
            Brokers = [],
            Topics = []
        });

        await Assert.That(metadataManager.Metadata.ClusterId).IsEqualTo("rejected-cluster");
        await Assert.That(producer.ClusterId).IsEqualTo("cluster-a");
        await Assert.That(producer.GetStatus().ClusterId).IsEqualTo("cluster-a");
    }

    [Test]
    public async Task ProducerAndConsumerActivities_IncludeKnownClusterIdentity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        await using var client = Kafka.Connect("localhost:9092");
        await using var producer = (KafkaProducer<string, string>)client.CreateProducer<string, string>().Build();
        await using var consumer = (KafkaConsumer<string, string>)client.CreateConsumer<string, string>("status-group").Build();
        var metadataManager = GetField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(CreateMetadata("cluster-status"));
        SetTrustedClusterId(metadataManager, "cluster-status");

        using var producerActivity = StartProducerActivity(producer);
        using var pending = PendingFetchData.Create("orders", 0, Array.Empty<RecordBatch>());
        using var consumerActivity = StartConsumerActivity(consumer, pending);

        await Assert.That(producerActivity).IsNotNull();
        await Assert.That(producerActivity!.GetTagItem("messaging.kafka.cluster.id"))
            .IsEqualTo("cluster-status");
        await Assert.That(consumerActivity).IsNotNull();
        await Assert.That(consumerActivity!.GetTagItem("messaging.kafka.cluster.id"))
            .IsEqualTo("cluster-status");
    }

    [Test]
    public async Task ProducerActivity_OmitsUnknownClusterIdentity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        await using var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "status-producer"
            },
            Serializers.String,
            Serializers.String);

        using var activity = StartProducerActivity(producer);

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.GetTagItem("messaging.kafka.cluster.id")).IsNull();
    }

    private static MetadataResponse CreateMetadata(string clusterId) => new()
    {
        ClusterId = clusterId,
        Brokers =
        [
            new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }
        ],
        Topics = Array.Empty<TopicMetadata>()
    };

    private static Activity? StartProducerActivity(KafkaProducer<string, string> producer)
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "StartPublishActivity",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        object?[] arguments =
        [
            new ProducerMessage<string, string> { Topic = "orders", Key = "key", Value = "value" }
        ];
        return (Activity?)method.Invoke(producer, arguments);
    }

    private static Activity? StartConsumerActivity(
        KafkaConsumer<string, string> consumer,
        PendingFetchData pending)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "StartConsumeActivity",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Activity?)method.Invoke(consumer, [pending, null, 42L, false, false]);
    }

    private static T GetField<T>(object instance, string name) =>
        (T)instance.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance)!;

    private static void SetTrustedClusterId(MetadataManager metadataManager, string clusterId) =>
        typeof(MetadataManager)
            .GetMethod(
                "UpdateMetadataClusterId",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                [typeof(string)],
                modifiers: null)!
            .Invoke(metadataManager, [clusterId]);
}
