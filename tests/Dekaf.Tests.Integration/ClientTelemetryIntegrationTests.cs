using System.Reflection;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Integration;

[Category("Telemetry")]
[SupportsKafka(420)]
public sealed class ClientTelemetryIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private static readonly FieldInfo ProducerTelemetryManagerField = typeof(KafkaProducer<string, string>)
        .GetField("_telemetryManager", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public async Task Producer_NegotiatesBrokerTelemetryStartup()
    {
        await using var producer = (KafkaProducer<string, string>)await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId($"telemetry-producer-{Guid.NewGuid():N}")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .RegisterMetricForSubscription(new ApplicationTelemetryMetric(
                "com.example.telemetry.integration.depth",
                ApplicationTelemetryMetricKind.Gauge,
                () => 1))
            .BuildAsync();

        var telemetryManager = (ClientTelemetryManager)ProducerTelemetryManagerField.GetValue(producer)!;

        if (telemetryManager.IsStarted)
        {
            await Assert.That(telemetryManager.IsDisabled).IsFalse();
            await Assert.That(telemetryManager.ClientInstanceId).IsNotEqualTo(Guid.Empty);
            await Assert.That(telemetryManager.SubscriptionId).IsGreaterThanOrEqualTo(0);
        }
        else
        {
            await Assert.That(telemetryManager.IsDisabled).IsTrue();
            await Assert.That(telemetryManager.ClientInstanceId).IsEqualTo(Guid.Empty);
            await Assert.That(telemetryManager.SubscriptionId).IsEqualTo(-1);
        }
    }
}
