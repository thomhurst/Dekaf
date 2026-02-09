using System.Diagnostics;
using Dekaf.Observability;

namespace Dekaf.Tests.Unit.Observability;

public sealed class DekafMetricsTests
{
    [Test]
    public async Task MeterName_IsDekaf()
    {
        var meterName = DekafMetrics.MeterName;
        await Assert.That(meterName).IsEqualTo("Dekaf");
    }

    [Test]
    public async Task ProducerMessagesSent_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ProducerMessagesSent.Name).IsEqualTo("dekaf.producer.messages.sent");
    }

    [Test]
    public async Task ProducerBytesSent_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ProducerBytesSent.Name).IsEqualTo("dekaf.producer.bytes.sent");
    }

    [Test]
    public async Task ProducerErrors_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ProducerErrors.Name).IsEqualTo("dekaf.producer.errors");
    }

    [Test]
    public async Task ProducerLatency_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ProducerLatency.Name).IsEqualTo("dekaf.producer.latency");
    }

    [Test]
    public async Task ConsumerMessagesReceived_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ConsumerMessagesReceived.Name).IsEqualTo("dekaf.consumer.messages.received");
    }

    [Test]
    public async Task ConsumerBytesReceived_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ConsumerBytesReceived.Name).IsEqualTo("dekaf.consumer.bytes.received");
    }

    [Test]
    public async Task ConsumerErrors_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ConsumerErrors.Name).IsEqualTo("dekaf.consumer.errors");
    }

    [Test]
    public async Task ConsumerFetchLatency_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ConsumerFetchLatency.Name).IsEqualTo("dekaf.consumer.fetch.latency");
    }

    [Test]
    public async Task ConsumerRebalances_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ConsumerRebalances.Name).IsEqualTo("dekaf.consumer.rebalances");
    }

    [Test]
    public async Task ConnectionsActive_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ConnectionsActive.Name).IsEqualTo("dekaf.connections.active");
    }

    [Test]
    public async Task ConnectionsCreated_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ConnectionsCreated.Name).IsEqualTo("dekaf.connections.created");
    }

    [Test]
    public async Task ConnectionsClosed_HasCorrectName()
    {
        await Assert.That(DekafMetrics.ConnectionsClosed.Name).IsEqualTo("dekaf.connections.closed");
    }
}

public sealed class DekafActivitySourceTests
{
    [Test]
    public async Task SourceName_IsDekaf()
    {
        var sourceName = DekafActivitySource.SourceName;
        await Assert.That(sourceName).IsEqualTo("Dekaf");
    }

    [Test]
    public async Task Source_HasCorrectName()
    {
        await Assert.That(DekafActivitySource.Source.Name).IsEqualTo("Dekaf");
    }

    [Test]
    public async Task StartProduce_WithListener_ReturnsActivityWithCorrectTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dekaf",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafActivitySource.StartProduce("my-topic", partition: 3);

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("produce");
        await Assert.That(activity.Kind).IsEqualTo(ActivityKind.Producer);
        await Assert.That(activity.GetTagItem("messaging.system")).IsEqualTo("kafka");
        await Assert.That(activity.GetTagItem("messaging.destination.name")).IsEqualTo("my-topic");
        await Assert.That(activity.GetTagItem("messaging.operation")).IsEqualTo("publish");
        await Assert.That(activity.GetTagItem("messaging.kafka.destination.partition")).IsEqualTo(3);
    }

    [Test]
    public async Task StartProduce_WithoutPartition_DoesNotSetPartitionTag()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dekaf",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafActivitySource.StartProduce("my-topic");

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.GetTagItem("messaging.kafka.destination.partition")).IsNull();
    }

    [Test]
    public async Task StartConsume_WithListener_ReturnsActivityWithCorrectTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dekaf",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafActivitySource.StartConsume("my-topic", partition: 2, offset: 42);

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("consume");
        await Assert.That(activity.Kind).IsEqualTo(ActivityKind.Consumer);
        await Assert.That(activity.GetTagItem("messaging.system")).IsEqualTo("kafka");
        await Assert.That(activity.GetTagItem("messaging.source.name")).IsEqualTo("my-topic");
        await Assert.That(activity.GetTagItem("messaging.kafka.source.partition")).IsEqualTo(2);
        await Assert.That(activity.GetTagItem("messaging.kafka.message.offset")).IsEqualTo(42L);
        await Assert.That(activity.GetTagItem("messaging.operation")).IsEqualTo("receive");
    }

    [Test]
    public async Task StartFetch_WithListener_ReturnsActivityWithCorrectTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dekaf",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafActivitySource.StartFetch(brokerId: 5);

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("fetch");
        await Assert.That(activity.Kind).IsEqualTo(ActivityKind.Client);
        await Assert.That(activity.GetTagItem("messaging.system")).IsEqualTo("kafka");
        await Assert.That(activity.GetTagItem("server.address")).IsEqualTo("5");
    }
}
