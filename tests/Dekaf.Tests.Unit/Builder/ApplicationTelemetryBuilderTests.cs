using System.Reflection;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Builder;

public sealed class ApplicationTelemetryBuilderTests
{
    [Test]
    public async Task ProducerBuilder_RegistersApplicationMetrics()
    {
        var metric = Metric("com.example.producer.depth");
        var builder = Kafka.CreateProducer<string, string>();

        var result = builder.RegisterMetricForSubscription(metric);
        await Assert.That(result).IsSameReferenceAs(builder);

        await using var producer = builder
            .WithBootstrapServers("localhost:9092")
            .Build();
        var options = GetPrivateField<ProducerOptions>(producer, "_options");

        await Assert.That(options.ApplicationMetrics.Count).IsEqualTo(1);
        await Assert.That(options.ApplicationMetrics[0]).IsSameReferenceAs(metric);
    }

    [Test]
    public async Task ConsumerBuilder_RegistersAndUnregistersApplicationMetrics()
    {
        var kept = Metric("com.example.consumer.kept");
        var removed = Metric("com.example.consumer.removed");
        var builder = Kafka.CreateConsumer<string, string>();

        var registered = builder.RegisterMetricForSubscription(kept);
        var unregistered = builder
            .RegisterMetricForSubscription(removed)
            .UnregisterMetricFromSubscription(removed.Name);
        await Assert.That(registered).IsSameReferenceAs(builder);
        await Assert.That(unregistered).IsSameReferenceAs(builder);

        await using var consumer = builder
            .WithBootstrapServers("localhost:9092")
            .Build();
        var options = GetPrivateField<ConsumerOptions>(consumer, "_options");

        await Assert.That(options.ApplicationMetrics.Count).IsEqualTo(1);
        await Assert.That(options.ApplicationMetrics[0]).IsSameReferenceAs(kept);
    }

    [Test]
    public async Task AdminClientBuilder_RegistersApplicationMetrics()
    {
        var first = Metric("com.example.admin.depth", () => 1);
        var replacement = Metric("com.example.admin.depth", () => 2);
        var builder = new AdminClientBuilder();

        var result = builder.RegisterMetricForSubscription(first)
            .RegisterMetricForSubscription(replacement);
        await Assert.That(result).IsSameReferenceAs(builder);

        await using var client = builder
            .WithBootstrapServers("localhost:9092")
            .Build();
        var options = GetPrivateField<AdminClientOptions>(client, "_options");

        await Assert.That(options.ApplicationMetrics.Count).IsEqualTo(1);
        await Assert.That(options.ApplicationMetrics[0]).IsSameReferenceAs(replacement);
    }

    [Test]
    public async Task RegisterMetricForSubscription_Null_ThrowsArgumentNullException()
    {
        await Assert.That(() => Kafka.CreateProducer<string, string>()
                .RegisterMetricForSubscription(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => Kafka.CreateConsumer<string, string>()
                .RegisterMetricForSubscription(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new AdminClientBuilder()
                .RegisterMetricForSubscription(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ClientMetricRegistration_AfterDispose_ThrowsObjectDisposedException()
    {
        var metric = Metric("com.example.disposed");

        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        await producer.DisposeAsync();

        var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        await consumer.DisposeAsync();

        var admin = new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .Build();
        await admin.DisposeAsync();

        await Assert.That(() => producer.RegisterMetricForSubscription(metric))
            .Throws<ObjectDisposedException>();
        await Assert.That(() => producer.UnregisterMetricFromSubscription(metric.Name))
            .Throws<ObjectDisposedException>();
        await Assert.That(() => consumer.RegisterMetricForSubscription(metric))
            .Throws<ObjectDisposedException>();
        await Assert.That(() => consumer.UnregisterMetricFromSubscription(metric.Name))
            .Throws<ObjectDisposedException>();
        await Assert.That(() => admin.RegisterMetricForSubscription(metric))
            .Throws<ObjectDisposedException>();
        await Assert.That(() => admin.UnregisterMetricFromSubscription(metric.Name))
            .Throws<ObjectDisposedException>();
    }

    private static ApplicationTelemetryMetric Metric(string name, Func<double>? observe = null) =>
        new(name, ApplicationTelemetryMetricKind.Gauge, observe ?? (() => 1));

    private static TField GetPrivateField<TField>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find {fieldName} field.");
        return (TField)field.GetValue(target)!;
    }
}
