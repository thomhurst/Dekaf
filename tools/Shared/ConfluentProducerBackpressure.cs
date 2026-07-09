using System.Diagnostics;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.Tooling;

internal static class ConfluentProducerBackpressure
{
    internal const int QueueBufferingMaxMessages = 10_000_000;

    internal static void ProduceWithBackpressure<TKey, TValue>(
        ConfluentKafka.IProducer<TKey, TValue> producer,
        string topic,
        ConfluentKafka.Message<TKey, TValue> message,
        Action<ConfluentKafka.DeliveryReport<TKey, TValue>>? deliveryHandler,
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
            catch (ConfluentKafka.ProduceException<TKey, TValue> ex)
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
