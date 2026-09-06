using System.Buffers;
using System.Text;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryDeliveryCallbackTests
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task ThrowingCallback_IsInvokedOnceAndDoesNotChangeDelivery(
        bool topicWrapper, bool deliveryFails, bool suspended)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var deliveryFailure = new InvalidOperationException("delivery failure");
        var callbackFailure = new InvalidOperationException("callback failure");
        var serializer = new PausedSerializer(deliveryFails ? deliveryFailure : null);
        await using var producer = suspended
            ? new InMemoryProducer<string, string>(cluster, Serializers.String, serializer)
            : new InMemoryProducer<string, string>(cluster);
        if (deliveryFails && !suspended)
            cluster.FailProduces("events", deliveryFailure);

        var invocations = 0;
        RecordMetadata observedMetadata = default;
        Exception? observedError = null;
        void Handler(RecordMetadata metadata, Exception? error)
        {
            invocations++;
            observedMetadata = metadata;
            observedError = error;
            if (invocations == 1)
                throw callbackFailure;
        }

        var pending = Send(producer, topicWrapper, Handler);
        if (suspended)
        {
            try
            {
                await serializer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.That(pending.IsCompleted).IsFalse();
                await Assert.That(invocations).IsEqualTo(0);
                await Assert.That(cluster.ReadRecords("events")).Count().IsEqualTo(0);
            }
            finally
            {
                serializer.Release.TrySetResult();
            }
        }

        Exception? escaped = null;
        try
        {
            await pending;
        }
        catch (Exception exception)
        {
            escaped = exception;
        }

        await Assert.That(invocations).IsEqualTo(1);
        await Assert.That(cluster.ReadRecords("events")).Count().IsEqualTo(deliveryFails ? 0 : 1);
        await Assert.That(escaped).IsNull();
        if (deliveryFails)
        {
            await Assert.That(observedError).IsSameReferenceAs(deliveryFailure);
            await Assert.That(observedMetadata).IsEqualTo(default(RecordMetadata));
        }
        else
        {
            await Assert.That(observedError).IsNull();
            await Assert.That(observedMetadata.Topic).IsEqualTo("events");
            await Assert.That(observedMetadata.Offset).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task NonthrowingCallback_ReceivesOriginalDeliveryOutcome(bool topicWrapper, bool deliveryFails)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var failure = new InvalidOperationException("delivery failure");
        if (deliveryFails)
            cluster.FailProduces("events", failure);

        var invocations = 0;
        Exception? observedError = null;
        await Send(producer, topicWrapper, (_, error) =>
        {
            invocations++;
            observedError = error;
        });

        await Assert.That(invocations).IsEqualTo(1);
        await Assert.That(observedError).IsSameReferenceAs(deliveryFails ? failure : null);
        await Assert.That(cluster.ReadRecords("events")).Count().IsEqualTo(deliveryFails ? 0 : 1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NullHandler_IsRejectedBeforeDelivery(bool topicWrapper)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        await using var producer = new InMemoryProducer<string, string>(cluster);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Send(producer, topicWrapper, null!).AsTask());

        await Assert.That(exception!.ParamName).IsEqualTo("deliveryHandler");
        await Assert.That(cluster.ReadRecords("events")).Count().IsEqualTo(0);
    }

    private static ValueTask Send(
        InMemoryProducer<string, string> producer,
        bool topicWrapper,
        Action<RecordMetadata, Exception?> handler) => topicWrapper
        ? producer.ForTopic("events").FireAsync("key", "value", handler)
        : producer.FireAsync(new ProducerMessage<string, string>
        {
            Topic = "events", Key = "key", Value = "value"
        }, handler);

    private sealed class PausedSerializer(Exception? failure) : IAsyncSerializer<string>
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask SerializeAsync(
            string value,
            IBufferWriter<byte> destination,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            if (failure is not null)
                throw failure;
            destination.Write(Encoding.UTF8.GetBytes(value));
        }
    }
}
