using System.Reflection;
using Dekaf.ShareConsumer;
using Dekaf.Telemetry;
using NSubstitute;

namespace Dekaf.Tests.Unit.Builder;

public sealed class ShareConsumerTelemetryRegistrationTests
{
    [Test]
    public async Task Builder_ReplacesRemovesAndSnapshotsRegistrations()
    {
        var original = Metric("com.example.share.depth", 1);
        var replacement = Metric(original.Name, 2);
        var removed = Metric("com.example.share.removed", 3);
        var builder = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("telemetry");
        await Assert.That(builder.RegisterMetricForSubscription(original)).IsSameReferenceAs(builder);
        await using var first = builder.Build();
        await Assert.That(builder.RegisterMetricForSubscription(replacement)
            .RegisterMetricForSubscription(removed).UnregisterMetricFromSubscription(removed.Name)
            .UnregisterMetricFromSubscription("missing")).IsSameReferenceAs(builder);
        await using var second = builder.Build();
        var firstOptions = Options(first);
        var secondOptions = Options(second);
        await Assert.That(firstOptions.ApplicationMetrics).HasSingleItem();
        await Assert.That(firstOptions.ApplicationMetrics[0]).IsSameReferenceAs(original);
        await Assert.That(secondOptions.ApplicationMetrics).HasSingleItem();
        await Assert.That(secondOptions.ApplicationMetrics[0]).IsSameReferenceAs(replacement);
        await Assert.That(firstOptions.ApplicationMetrics).IsNotSameReferenceAs(secondOptions.ApplicationMetrics);
    }

    [Test]
    public async Task Registration_ValidatesNullMetricAndDisposedConsumer()
    {
        var builder = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("telemetry");
        await Assert.That(() => builder.RegisterMetricForSubscription(null!)).Throws<ArgumentNullException>();
        var consumer = builder.Build();
        try
        {
            await Assert.That(consumer is IApplicationTelemetryShareConsumer).IsTrue();
            await Assert.That(() => consumer.RegisterMetricForSubscription(null!)).Throws<ArgumentNullException>();
            consumer.UnregisterMetricFromSubscription("missing");
        }
        finally
        {
            await consumer.DisposeAsync();
        }
        await Assert.That(() => consumer.RegisterMetricForSubscription(Metric("com.example.share.depth", 1)))
            .Throws<ObjectDisposedException>();
        await Assert.That(() => consumer.UnregisterMetricFromSubscription("missing"))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Removal_ValidatesMetricName(string? name)
    {
        var builder = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("telemetry");
        await Assert.That(() => builder.UnregisterMetricFromSubscription(name!)).Throws<ArgumentException>();
        await using var consumer = builder.Build();
        await Assert.That(() => consumer.UnregisterMetricFromSubscription(name!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task OptionalCapability_PreservesExistingInterfaceImplementers()
    {
        var legacyConsumer = Substitute.For<IKafkaShareConsumer<string, string>>();
        await Assert.That(() => legacyConsumer.RegisterMetricForSubscription(Metric("com.example.share.depth", 1)))
            .Throws<NotSupportedException>();
        await Assert.That(() => legacyConsumer.UnregisterMetricFromSubscription("com.example.share.depth"))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task Extensions_RejectNullConsumer()
    {
        IKafkaShareConsumer<string, string> consumer = null!;
        await Assert.That(() => consumer.RegisterMetricForSubscription(Metric("com.example.share.depth", 1)))
            .Throws<ArgumentNullException>();
        await Assert.That(() => consumer.UnregisterMetricFromSubscription("com.example.share.depth"))
            .Throws<ArgumentNullException>();
    }

    private static ApplicationTelemetryMetric Metric(string name, double value) =>
        new(name, ApplicationTelemetryMetricKind.Gauge, () => value);

    private static ShareConsumerOptions Options(IKafkaShareConsumer<string, string> consumer) =>
        (ShareConsumerOptions)consumer.GetType().GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
}
