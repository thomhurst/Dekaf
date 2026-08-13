using Dekaf.Consumer;
using Dekaf.Extensions.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[Category("HealthChecks")]
public sealed class ConsumerHealthCheckIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task MoreConsumersThanPartitions_UnassignedMemberRemainsHealthy()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"health-check-standby-{Guid.NewGuid():N}";

        await using var first = await CreateConsumerAsync("health-check-first", groupId);
        await using var second = await CreateConsumerAsync("health-check-second", groupId);
        first.Subscribe(topic);
        second.Subscribe(topic);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var firstPoll = ConsumeUntilCancelledAsync(first, cancellation.Token);
        var secondPoll = ConsumeUntilCancelledAsync(second, cancellation.Token);

        try
        {
            await WaitForConditionAsync(
                () => HasConfirmedHeartbeat(first) &&
                      HasConfirmedHeartbeat(second) &&
                      first.Assignment.Count + second.Assignment.Count == 1,
                TimeSpan.FromSeconds(20),
                description: "both consumers to join with one standby member");

            var standby = first.Assignment.Count == 0 ? first : second;
            var healthCheck = new DekafConsumerHealthCheck<string, string>(
                standby,
                new DekafConsumerHealthCheckOptions());

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            await Assert.That(standby.Assignment).IsEmpty();
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(result.Data["ConsumerState"]).IsEqualTo("Standby");
        }
        finally
        {
            await cancellation.CancelAsync();
            await Task.WhenAll(firstPoll, secondPoll);
        }
    }

    private async Task<IKafkaConsumer<string, string>> CreateConsumerAsync(string clientId, string groupId) =>
        await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId(clientId)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

    private static async Task ConsumeUntilCancelledAsync(
        IKafkaConsumer<string, string> consumer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in consumer.ConsumeAsync(cancellationToken))
            {
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static bool HasConfirmedHeartbeat(IKafkaConsumer<string, string> consumer)
    {
        var liveness = ((IConsumerGroupLiveness)consumer).GroupLiveness;
        return liveness.IsJoined && liveness.TimeSinceLastHeartbeat is not null;
    }
}
