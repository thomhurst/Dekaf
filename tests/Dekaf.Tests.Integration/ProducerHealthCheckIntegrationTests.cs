using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Extensions.HealthChecks;
using Dekaf.Producer;
using Dekaf.Protocol;
using Docker.DotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
[Category("HealthChecks")]
[NotInParallel("ProducerHealthKafkaContainer")]
[ClassDataSource<ProducerHealthKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ProducerHealthCheckIntegrationTests(ProducerHealthKafkaContainer kafka)
{
    [Test]
    public async Task RejectedDelivery_DrainsQueue_AndLaterDeliverySucceeds()
    {
        var topic = $"health-rejected-{Guid.NewGuid():N}";
        await using var admin = kafka.CreateAdminClient();
        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string> { ["max.message.bytes"] = "1024" }
            }
        ]);
        await using var producer = await CreateProducerAsync();
        var health = CreateHealthCheck(producer);

        var failure = await Assert.ThrowsAsync<ProduceException>(async () =>
            await producer.ProduceAsync(topic, "key", new string('x', 8192)));
        await Assert.That(failure!.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);

        // A broker-rejected batch has left the real accumulator despite failed delivery.
        var drainedAfterFailure = await health.CheckHealthAsync(new HealthCheckContext());
        await AssertFlushCheckpointAsync(drainedAfterFailure);

        var delivered = await producer.ProduceAsync(topic, "key", "accepted");
        await Assert.That(delivered.Offset).IsEqualTo(0L);
        await AssertFlushCheckpointAsync(await health.CheckHealthAsync(new HealthCheckContext()));
    }

    [Test]
    public async Task SuccessfulDelivery_DrainsQueue()
    {
        var topic = await kafka.CreateTestTopicAsync();
        await using var producer = await CreateProducerAsync();
        var delivery = producer.ProduceAsync(topic, "key", "accepted");

        var drained = await CreateHealthCheck(producer).CheckHealthAsync(new HealthCheckContext());
        var delivered = await delivery;

        await Assert.That(delivered.Offset).IsEqualTo(0L);
        await AssertFlushCheckpointAsync(drained);
    }

    [Test]
    public async Task IdleQueue_WhenBrokerUnavailable_DrainsWithoutClaimingConnectivity()
    {
        await using var producer = await CreateProducerAsync();
        // A fresh admin must contact the broker, rather than reuse initialized metadata.
        await using var admin = kafka.CreateAdminClient();
        var connectivity = new DekafBrokerHealthCheck(admin,
            new DekafBrokerHealthCheckOptions { Timeout = TimeSpan.FromSeconds(1) });

        await kafka.SetPausedAsync(true);
        try
        {
            await AssertFlushCheckpointAsync(await CreateHealthCheck(producer)
                .CheckHealthAsync(new HealthCheckContext()));
            var brokerResult = await connectivity.CheckHealthAsync(new HealthCheckContext());
            await Assert.That(brokerResult.Status).IsEqualTo(HealthStatus.Unhealthy);
        }
        finally
        {
            await kafka.SetPausedAsync(false);
        }
    }

    private async Task<IKafkaProducer<string, string>> CreateProducerAsync() =>
        await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithAcks(Acks.All)
            .WithBatchSize(16384)
            .WithMaxRequestSize(32768)
            .WithLinger(TimeSpan.Zero)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(15))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .BuildAsync();

    private static DekafProducerHealthCheck<string, string> CreateHealthCheck(IKafkaProducer<string, string> producer) =>
        new(producer, new DekafProducerHealthCheckOptions());

    private static async Task AssertFlushCheckpointAsync(HealthCheckResult result)
    {
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).IsEqualTo(
            "Producer flush checkpoint completed. Delivery outcomes and broker connectivity are not checked.");
    }
}

// Isolated from the general Kafka fixture so pausing cannot affect unrelated tests.
public sealed class ProducerHealthKafkaContainer : KafkaContainerDefault
{
    public async Task SetPausedAsync(bool paused)
    {
        using var client = new DockerClientBuilder().Build();
        var containerId = ContainerInstance!.Id;
        if (paused)
            await client.Containers.PauseContainerAsync(containerId);
        else
            await client.Containers.UnpauseContainerAsync(containerId);
    }
}
