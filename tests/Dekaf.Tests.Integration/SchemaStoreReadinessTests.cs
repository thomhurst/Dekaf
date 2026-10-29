using Dekaf.Admin;
using Dekaf.SchemaRegistry;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[NotInParallel("SchemaStoreReadiness")]
public sealed class SchemaStoreReadinessTests
{
    [Test]
    [SkipWhenExternalSchemaRegistry]
    public async Task RegisterSchema_BrokerStallsBeyondDefaultStoreTimeout_WaitsForAcknowledgement(
        CancellationToken cancellationToken)
    {
        await using var fixture = new PausableSchemaRegistryContainer();
        await fixture.InitializeAsync();
        using var docker = TestcontainersSettings.OS.DockerEndpointAuthConfig
            .GetDockerClientBuilder(Guid.NewGuid()).Build();
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig { Url = fixture.RegistryUrl });
        var subject = $"paused-store-{Guid.NewGuid():N}";

        await docker.Containers.PauseContainerAsync(fixture.Kafka.Id, cancellationToken);
        Task<int>? registration = null;
        bool completedWhilePaused;
        try
        {
            registration = client.RegisterSchemaAsync(subject,
                new Schema { SchemaType = SchemaType.Avro, SchemaString = "\"int\"" }, cancellationToken);
            using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestDeadline.CancelAfter(TimeSpan.FromSeconds(5));
            // Observe the request at the server before holding the broker beyond its 500 ms default.
            while (true)
            {
                var (stdout, _) = await fixture.Registry.GetLogsAsync(ct: requestDeadline.Token);
                if (stdout.Contains(subject, StringComparison.Ordinal))
                    break;
                await Task.Delay(50, requestDeadline.Token);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            completedWhilePaused = registration.IsCompleted;
        }
        finally
        {
            await docker.Containers.UnpauseContainerAsync(fixture.Kafka.Id, CancellationToken.None);
            // Observe the request even if fault injection or cancellation fails.
            if (registration is not null)
                await registration;
        }

        await Assert.That(completedWhilePaused).IsFalse();
        var registered = await client.GetSchemaBySubjectAsync(subject, cancellationToken: cancellationToken);
        await Assert.That(registered.Schema.SchemaString).IsEqualTo("\"int\"");
    }

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

    private sealed class PausableSchemaRegistryContainer : KafkaWithSchemaRegistryContainer
    {
        protected override string SchemaRegistryImage => "confluentinc/cp-schema-registry:8.2.0";
        protected override string KafkaImage => $"apache/kafka:{KafkaContainerDefault.DefaultTag}";
        public KafkaContainer Kafka { get; private set; } = null!;
        public IContainer Registry { get; private set; } = null!;

        protected override KafkaBuilder ConfigureKafkaBuilder(KafkaBuilder builder) =>
            KafkaContainerDefault.ConfigureBuilderForVersion(builder, KafkaContainerDefault.DefaultTag);

        protected override Task StartKafkaContainerAsync(KafkaContainer container, CancellationToken cancellationToken)
        {
            Kafka = container;
            return base.StartKafkaContainerAsync(container, cancellationToken);
        }

        protected override Task StartSchemaRegistryContainerAsync(IContainer container, CancellationToken cancellationToken)
        {
            Registry = container;
            return base.StartSchemaRegistryContainerAsync(container, cancellationToken);
        }
    }
}
