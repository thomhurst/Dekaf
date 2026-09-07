using Dekaf.ShareConsumer;
using Dekaf.Telemetry;
using Dekaf.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Testing;

public class InMemoryShareTelemetryTests
{
    [Test]
    public async Task Registration_ThroughDependencyInjection_SupportsApplicationTelemetry()
    {
        var services = new ServiceCollection();
        services.AddDekafInMemory();
        await using var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<IKafkaShareConsumer<string, string>>();
        var metric = new ApplicationTelemetryMetric("app.queue.depth", ApplicationTelemetryMetricKind.Gauge,
            static () => throw new InvalidOperationException("The in-memory consumer must not observe metrics."));

        consumer.RegisterMetricForSubscription(metric);
        consumer.UnregisterMetricFromSubscription(metric.Name);
        await Assert.That(consumer is IApplicationTelemetryShareConsumer).IsTrue();
    }

    [Test]
    public async Task Registration_ThroughShareConsumerInterface_DoesNotObserveMetrics()
    {
        await using IKafkaShareConsumer<string, string> consumer =
            new InMemoryShareConsumer<string, string>(new InMemoryKafkaCluster());
        var observations = 0;
        var metric = new ApplicationTelemetryMetric("app.queue.depth", ApplicationTelemetryMetricKind.Gauge,
            () => ++observations);

        consumer.RegisterMetricForSubscription(metric);
        consumer.RegisterMetricForSubscription(metric);
        consumer.UnregisterMetricFromSubscription(metric.Name);
        consumer.UnregisterMetricFromSubscription("app.missing");
        await consumer.CloseAsync();

        await Assert.That(observations).IsEqualTo(0);
    }

    [Test]
    public async Task Registration_RejectsNullMetric()
    {
        await using IKafkaShareConsumer<string, string> consumer =
            new InMemoryShareConsumer<string, string>(new InMemoryKafkaCluster());

        Assert.Throws<ArgumentNullException>(() => consumer.RegisterMetricForSubscription(null!));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Unregistration_RejectsInvalidName(string? name)
    {
        await using IKafkaShareConsumer<string, string> consumer =
            new InMemoryShareConsumer<string, string>(new InMemoryKafkaCluster());

        if (name is null)
            Assert.Throws<ArgumentNullException>(() => consumer.UnregisterMetricFromSubscription(name!));
        else
            Assert.Throws<ArgumentException>(() => consumer.UnregisterMetricFromSubscription(name));
    }

    [Test]
    public async Task RegistrationAndUnregistration_AfterDisposal_Throw()
    {
        IKafkaShareConsumer<string, string> consumer =
            new InMemoryShareConsumer<string, string>(new InMemoryKafkaCluster());
        var metric = new ApplicationTelemetryMetric("app.queue.depth", ApplicationTelemetryMetricKind.Gauge,
            static () => 0);
        await consumer.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => consumer.RegisterMetricForSubscription(metric));
        Assert.Throws<ObjectDisposedException>(() => consumer.UnregisterMetricFromSubscription(metric.Name));
    }
}
