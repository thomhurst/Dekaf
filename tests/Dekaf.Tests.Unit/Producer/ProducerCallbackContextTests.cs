using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class ProducerCallbackContextTests
{
    [Test]
    public async Task FlushAsync_FromDeliveryCallback_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        Exception? capturedException = null;
        ProducerCallbackContext.Invoke((_, _) =>
        {
            try
            {
                producer.FlushAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        }, default, null);

        await Assert.That(capturedException).IsTypeOf<InvalidOperationException>();
        await Assert.That(capturedException!.Message).Contains("FlushAsync cannot be called from a delivery callback");
    }

    [Test]
    public async Task DeliveryCallbackContext_ClearsFlag_WhenCallbackThrows()
    {
        var act = () => ProducerCallbackContext.Invoke((_, _) => throw new InvalidOperationException("boom"), default, null);

        await Assert.That(act).Throws<InvalidOperationException>();
        await Assert.That(ProducerCallbackContext.IsInDeliveryCallback).IsFalse();
    }
}
