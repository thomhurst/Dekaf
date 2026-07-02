using Dekaf.Producer;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

public sealed class ProducerExtensionsTests
{
    #region ProduceAsync with Headers

    [Test]
    public async Task ProduceAsync_WithHeaders_DelegatesToProducer()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        var expectedMetadata = new RecordMetadata
        {
            Topic = "my-topic",
            Partition = 0,
            Offset = 42,
            Timestamp = DateTimeOffset.UtcNow
        };
        producer.ProduceAsync(Arg.Any<ProducerMessage<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(expectedMetadata));

        var headers = Headers.Create("h1", "v1");
        var result = await producer.ProduceAsync("my-topic", "key", "value", headers, CancellationToken.None);

        await Assert.That(result.Offset).IsEqualTo(42);
        await producer.Received(1).ProduceAsync(
            Arg.Is<ProducerMessage<string, string>>(m =>
                m.Topic == "my-topic" &&
                m.Key == "key" &&
                m.Value == "value" &&
                m.Headers != null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProduceAsync_WithHeaders_UsesFastPath_WhenProducerSupportsIt()
    {
        var producer = new FastPathProducerSpy<string, string>();
        var headers = Headers.Create("h1", "v1");
        using var cts = new CancellationTokenSource();

        var result = await producer.ProduceAsync("my-topic", "key", "value", headers, cts.Token);

        await Assert.That(result.Offset).IsEqualTo(42);
        await Assert.That(producer.FastPathCalls).IsEqualTo(1);
        await Assert.That(producer.MessageCalls).IsEqualTo(0);
        await Assert.That(producer.CapturedTopic).IsEqualTo("my-topic");
        await Assert.That(producer.CapturedKey).IsEqualTo("key");
        await Assert.That(producer.CapturedValue).IsEqualTo("value");
        await Assert.That(producer.CapturedHeaders).IsSameReferenceAs(headers);
        await Assert.That(producer.CapturedPartition).IsNull();
        await Assert.That(producer.CapturedCancellationToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task ProduceAsync_WithHeaders_NullProducer_ThrowsArgumentNullException()
    {
        IKafkaProducer<string, string>? producer = null;
        var headers = Headers.Create("h1", "v1");

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await producer!.ProduceAsync("topic", "key", "value", headers, CancellationToken.None));
    }

    [Test]
    public async Task ProduceAsync_WithHeaders_NullTopic_ThrowsArgumentNullException()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        var headers = Headers.Create("h1", "v1");

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await producer.ProduceAsync(null!, "key", "value", headers, CancellationToken.None));
    }

    #endregion

    #region ProduceAsync with Partition

    [Test]
    public async Task ProduceAsync_WithPartition_DelegatesToProducer()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        var expectedMetadata = new RecordMetadata
        {
            Topic = "my-topic",
            Partition = 3,
            Offset = 100,
            Timestamp = DateTimeOffset.UtcNow
        };
        producer.ProduceAsync(Arg.Any<ProducerMessage<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(expectedMetadata));

        var result = await producer.ProduceAsync("my-topic", partition: 3, "key", "value");

        await Assert.That(result.Partition).IsEqualTo(3);
        await producer.Received(1).ProduceAsync(
            Arg.Is<ProducerMessage<string, string>>(m =>
                m.Topic == "my-topic" &&
                m.Partition == 3 &&
                m.Key == "key" &&
                m.Value == "value"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProduceAsync_WithPartition_UsesFastPath_WhenProducerSupportsIt()
    {
        var producer = new FastPathProducerSpy<string, string>();

        var result = await producer.ProduceAsync("my-topic", partition: 3, "key", "value");

        await Assert.That(result.Offset).IsEqualTo(42);
        await Assert.That(producer.FastPathCalls).IsEqualTo(1);
        await Assert.That(producer.MessageCalls).IsEqualTo(0);
        await Assert.That(producer.CapturedTopic).IsEqualTo("my-topic");
        await Assert.That(producer.CapturedKey).IsEqualTo("key");
        await Assert.That(producer.CapturedValue).IsEqualTo("value");
        await Assert.That(producer.CapturedHeaders).IsNull();
        await Assert.That(producer.CapturedPartition).IsEqualTo(3);
    }

    [Test]
    public async Task ProduceAsync_WithPartition_NullProducer_ThrowsArgumentNullException()
    {
        IKafkaProducer<string, string>? producer = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await producer!.ProduceAsync("topic", partition: 0, "key", "value"));
    }

    [Test]
    public async Task ProduceAsync_WithPartition_NullTopic_ThrowsArgumentNullException()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await producer.ProduceAsync(null!, partition: 0, "key", "value"));
    }

    #endregion

    #region ProduceAsync with Headers (fire-and-forget)

    [Test]
    public async Task ProduceAsync_FireAndForget_WithHeaders_DelegatesToProducer()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        var headers = Headers.Create("h1", "v1");
        producer.FireAsync(Arg.Any<ProducerMessage<string, string>>())
            .Returns(default(ValueTask));

        await producer.FireAsync("my-topic", "key", "value", headers);

        await producer.Received(1).FireAsync(
            Arg.Is<ProducerMessage<string, string>>(m =>
                m.Topic == "my-topic" &&
                m.Key == "key" &&
                m.Value == "value" &&
                m.Headers != null));
    }

    [Test]
    public async Task ProduceAsync_FireAndForget_WithHeaders_NullProducer_ThrowsArgumentNullException()
    {
        IKafkaProducer<string, string>? producer = null;
        var headers = Headers.Create("h1", "v1");

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await producer!.FireAsync("topic", "key", "value", headers));
    }

    [Test]
    public async Task FireAsync_WithHeaders_NullTopic_ThrowsArgumentNullException()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        var headers = Headers.Create("h1", "v1");

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await producer.FireAsync(null!, "key", "value", headers));
    }

    #endregion
}
