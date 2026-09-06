using Dekaf.Admin;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[NotInParallel("SchemaStoreReadiness")]
public sealed class SchemaStoreReadinessTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PrepareSchemaStore_ProvidesCompactedPartitionBeforeRegistryStartup(bool currentBroker)
    {
        var tag = currentBroker ? KafkaContainerDefault.DefaultTag : "4.0.2";
        await using var kafka = KafkaContainerDefault.ConfigureBuilderForVersion(
                new KafkaBuilder($"apache/kafka:{tag}")
                    .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m"), tag)
            .WithCommand(KafkaTestContainer.StartupCommandOverride)
            .Build();
        await kafka.StartAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await KafkaWithSchemaRegistryContainer.PrepareSchemaStoreAsync(kafka.GetBootstrapAddress(), timeout.Token);

        // No Schema Registry process has started: the fixture itself must establish this state.
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(kafka.GetBootstrapAddress()).Build();
        var descriptions = await admin.DescribeTopicsAsync(["_schemas"], timeout.Token);
        var partitions = descriptions["_schemas"].Partitions;
        await Assert.That(partitions.Count).IsEqualTo(1);
        await Assert.That(partitions[0].LeaderId).IsGreaterThanOrEqualTo(0);
        await Assert.That(partitions[0].IsrNodes.Count).IsEqualTo(1);
        var resource = ConfigResource.Topic("_schemas");
        var configs = await admin.DescribeConfigsAsync([resource], cancellationToken: timeout.Token);
        await Assert.That(configs[resource].Single(config => config.Name == "cleanup.policy").Value).IsEqualTo("compact");
        var partition = new TopicPartition("_schemas", 0);
        var offsets = await admin.ListOffsetsAsync(
            [new TopicPartitionOffsetSpec { TopicPartition = partition, Spec = OffsetSpec.Latest }],
            cancellationToken: timeout.Token);
        await Assert.That(offsets[partition].Offset).IsEqualTo(0);
    }
}
