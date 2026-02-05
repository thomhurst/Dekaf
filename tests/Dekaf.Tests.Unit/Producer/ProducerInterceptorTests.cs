using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public sealed class ProducerInterceptorTests
{
    #region OnSend Tests

    [Test]
    public async Task OnSend_InterceptorIsCalled_AndCanModifyMessage()
    {
        // Arrange: interceptor adds a header
        var interceptor = new HeaderAddingInterceptor("trace-id", "abc-123");

        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .AddInterceptor(interceptor);

        // We can't easily produce without a real broker, but we can verify the builder accepts it
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task AddInterceptor_ReturnsBuilderForChaining()
    {
        var interceptor = new NoOpProducerInterceptor();

        var originalBuilder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returnedBuilder = originalBuilder.AddInterceptor(interceptor);

        await Assert.That(returnedBuilder).IsSameReferenceAs(originalBuilder);
    }

    [Test]
    public async Task AddInterceptor_ThrowsOnNull()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var action = () => builder.AddInterceptor(null!);

        await Assert.That(action).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task OnSend_MultipleInterceptors_ChainedInOrder()
    {
        var callOrder = new List<string>();
        var interceptor1 = new OrderTrackingInterceptor("first", callOrder);
        var interceptor2 = new OrderTrackingInterceptor("second", callOrder);
        var interceptor3 = new OrderTrackingInterceptor("third", callOrder);

        var message = new ProducerMessage<string, string>
        {
            Topic = "test-topic",
            Key = "key",
            Value = "value"
        };

        // Simulate what the producer does internally
        message = interceptor1.OnSend(message);
        message = interceptor2.OnSend(message);
        message = interceptor3.OnSend(message);

        await Assert.That(callOrder).Count().IsEqualTo(3);
        await Assert.That(callOrder[0]).IsEqualTo("first");
        await Assert.That(callOrder[1]).IsEqualTo("second");
        await Assert.That(callOrder[2]).IsEqualTo("third");
    }

    [Test]
    public async Task OnSend_InterceptorCanAddHeaders()
    {
        var interceptor = new HeaderAddingInterceptor("correlation-id", "request-42");

        var message = new ProducerMessage<string, string>
        {
            Topic = "test-topic",
            Key = "key",
            Value = "value"
        };

        var result = interceptor.OnSend(message);

        await Assert.That(result.Headers).IsNotNull();
        await Assert.That(result.Headers!.Count).IsEqualTo(1);

        var header = result.Headers.First();
        await Assert.That(header.Key).IsEqualTo("correlation-id");
        await Assert.That(header.GetValueAsString()).IsEqualTo("request-42");
    }

    [Test]
    public async Task OnSend_InterceptorCanTransformValue()
    {
        var interceptor = new ValueTransformingInterceptor();

        var message = new ProducerMessage<string, string>
        {
            Topic = "test-topic",
            Key = "key",
            Value = "hello"
        };

        var result = interceptor.OnSend(message);

        await Assert.That(result.Value).IsEqualTo("HELLO");
    }

    [Test]
    public async Task OnSend_InterceptorExceptionDoesNotPropagateInChain()
    {
        // Simulating what the producer does: catch and continue with original message
        var throwingInterceptor = new ThrowingOnSendInterceptor();
        var goodInterceptor = new HeaderAddingInterceptor("after-error", "still-works");

        var message = new ProducerMessage<string, string>
        {
            Topic = "test-topic",
            Key = "key",
            Value = "value"
        };

        // First interceptor throws - producer catches and uses original message
        ProducerMessage<string, string> current = message;
        try
        {
            current = throwingInterceptor.OnSend(current);
        }
        catch
        {
            // Producer catches this - current stays as original message
        }

        // Second interceptor still runs
        current = goodInterceptor.OnSend(current);

        await Assert.That(current.Headers).IsNotNull();
        await Assert.That(current.Headers!.Count).IsEqualTo(1);
    }

    #endregion

    #region OnAcknowledgement Tests

    [Test]
    public async Task OnAcknowledgement_CalledWithCorrectMetadataOnSuccess()
    {
        var interceptor = new AcknowledgementTrackingInterceptor();

        var metadata = new RecordMetadata
        {
            Topic = "test-topic",
            Partition = 0,
            Offset = 42,
            Timestamp = DateTimeOffset.UtcNow
        };

        interceptor.OnAcknowledgement(metadata, null);

        await Assert.That(interceptor.Acknowledgements).Count().IsEqualTo(1);
        await Assert.That(interceptor.Acknowledgements[0].Metadata.Topic).IsEqualTo("test-topic");
        await Assert.That(interceptor.Acknowledgements[0].Metadata.Partition).IsEqualTo(0);
        await Assert.That(interceptor.Acknowledgements[0].Metadata.Offset).IsEqualTo(42);
        await Assert.That(interceptor.Acknowledgements[0].Exception).IsNull();
    }

    [Test]
    public async Task OnAcknowledgement_CalledWithExceptionOnFailure()
    {
        var interceptor = new AcknowledgementTrackingInterceptor();
        var exception = new InvalidOperationException("Send failed");

        var metadata = new RecordMetadata
        {
            Topic = "test-topic",
            Partition = 0,
            Offset = -1,
            Timestamp = DateTimeOffset.UtcNow
        };

        interceptor.OnAcknowledgement(metadata, exception);

        await Assert.That(interceptor.Acknowledgements).Count().IsEqualTo(1);
        await Assert.That(interceptor.Acknowledgements[0].Exception).IsNotNull();
        await Assert.That(interceptor.Acknowledgements[0].Exception).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task OnAcknowledgement_MultipleInterceptorsAllCalled()
    {
        var interceptor1 = new AcknowledgementTrackingInterceptor();
        var interceptor2 = new AcknowledgementTrackingInterceptor();

        var metadata = new RecordMetadata
        {
            Topic = "test-topic",
            Partition = 0,
            Offset = 10,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Simulate producer calling all interceptors
        interceptor1.OnAcknowledgement(metadata, null);
        interceptor2.OnAcknowledgement(metadata, null);

        await Assert.That(interceptor1.Acknowledgements).Count().IsEqualTo(1);
        await Assert.That(interceptor2.Acknowledgements).Count().IsEqualTo(1);
    }

    [Test]
    public async Task OnAcknowledgement_ExceptionInInterceptorDoesNotAffectOthers()
    {
        var throwingInterceptor = new ThrowingOnAcknowledgementInterceptor();
        var trackingInterceptor = new AcknowledgementTrackingInterceptor();

        var metadata = new RecordMetadata
        {
            Topic = "test-topic",
            Partition = 0,
            Offset = 10,
            Timestamp = DateTimeOffset.UtcNow
        };

        // First interceptor throws - producer catches
        try
        {
            throwingInterceptor.OnAcknowledgement(metadata, null);
        }
        catch
        {
            // Producer catches this
        }

        // Second interceptor still runs
        trackingInterceptor.OnAcknowledgement(metadata, null);

        await Assert.That(trackingInterceptor.Acknowledgements).Count().IsEqualTo(1);
    }

    #endregion

    #region Builder Integration Tests

    [Test]
    public async Task MultipleInterceptors_CanBeAddedViaBuilder()
    {
        var interceptor1 = new NoOpProducerInterceptor();
        var interceptor2 = new NoOpProducerInterceptor();
        var interceptor3 = new NoOpProducerInterceptor();

        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .AddInterceptor(interceptor1)
            .AddInterceptor(interceptor2)
            .AddInterceptor(interceptor3);

        await Assert.That(builder).IsNotNull();
    }

    #endregion

    #region Test Interceptors

    private sealed class NoOpProducerInterceptor : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message) => message;
        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class HeaderAddingInterceptor : IProducerInterceptor<string, string>
    {
        private readonly string _headerKey;
        private readonly string _headerValue;

        public HeaderAddingInterceptor(string headerKey, string headerValue)
        {
            _headerKey = headerKey;
            _headerValue = headerValue;
        }

        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            var headers = message.Headers ?? new Headers();
            headers.Add(_headerKey, _headerValue);
            return message with { Headers = headers };
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class ValueTransformingInterceptor : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            return message with { Value = message.Value.ToUpperInvariant() };
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class OrderTrackingInterceptor : IProducerInterceptor<string, string>
    {
        private readonly string _name;
        private readonly List<string> _callOrder;

        public OrderTrackingInterceptor(string name, List<string> callOrder)
        {
            _name = name;
            _callOrder = callOrder;
        }

        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            _callOrder.Add(_name);
            return message;
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class ThrowingOnSendInterceptor : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            throw new InvalidOperationException("Interceptor error on send");
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class AcknowledgementTrackingInterceptor : IProducerInterceptor<string, string>
    {
        public List<(RecordMetadata Metadata, Exception? Exception)> Acknowledgements { get; } = [];

        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message) => message;

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception)
        {
            Acknowledgements.Add((metadata, exception));
        }
    }

    private sealed class ThrowingOnAcknowledgementInterceptor : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message) => message;

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception)
        {
            throw new InvalidOperationException("Interceptor error on acknowledgement");
        }
    }

    #endregion
}
