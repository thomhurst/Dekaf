using System.Diagnostics;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.Tooling;

internal static class ConfluentProducerBackpressure
{
    internal const int QueueBufferingMaxMessages = 10_000_000;

    internal static void ProduceWithBackpressure(
        ConfluentKafka.IProducer<string, string> producer,
        string topic,
        ConfluentKafka.Message<string, string> message,
        Action<ConfluentKafka.DeliveryReport<string, string>>? deliveryHandler,
        CancellationToken cancellationToken,
        TimeSpan? retryTimeout = null)
    {
        var startedAt = retryTimeout.HasValue ? Stopwatch.GetTimestamp() : 0;

        while (true)
        {
            try
            {
                producer.Produce(topic, message, deliveryHandler);
                return;
            }
            catch (ConfluentKafka.ProduceException<string, string> ex)
                when (ex.Error.Code == ConfluentKafka.ErrorCode.Local_QueueFull &&
                      ShouldRetry(startedAt, retryTimeout))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Background poll thread drains the local queue; short sleep is librdkafka's backpressure wait.
                Thread.Sleep(1);
            }
        }
    }

    private static bool ShouldRetry(long startedAt, TimeSpan? retryTimeout)
        => !retryTimeout.HasValue || Stopwatch.GetElapsedTime(startedAt) < retryTimeout.Value;
}
