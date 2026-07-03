using System.Diagnostics;
using Dekaf.StressTests.Metrics;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal static class ConfluentStressTestHelpers
{
    /// <summary>
    /// Produces one message, blocking briefly and retrying while librdkafka's local
    /// queue is full. This mirrors Dekaf's BufferMemory backpressure: without it,
    /// queue-full messages are silently dropped and counted as errors, which makes
    /// throughput numbers incomparable (drops vs. delivered goodput).
    /// </summary>
    internal static void ProduceWithBackpressure(
        ConfluentKafka.IProducer<string, string> producer,
        string topic,
        ConfluentKafka.Message<string, string> message,
        Action<ConfluentKafka.DeliveryReport<string, string>>? deliveryHandler,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                producer.Produce(topic, message, deliveryHandler);
                return;
            }
            catch (ConfluentKafka.ProduceException<string, string> ex)
                when (ex.Error.Code == ConfluentKafka.ErrorCode.Local_QueueFull)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // The background poll thread drains the queue; a short sleep is the
                // idiomatic librdkafka backpressure wait.
                Thread.Sleep(1);
            }
        }
    }

    /// <summary>
    /// Produces one message and records the full delivery round-trip into
    /// <paramref name="latency"/> via the delivery report callback, without
    /// stalling the produce loop.
    /// </summary>
    internal static void SampleDeliveryLatency(
        ConfluentKafka.IProducer<string, string> producer,
        string topic,
        ConfluentKafka.Message<string, string> message,
        LatencyTracker latency,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        ProduceWithBackpressure(producer, topic, message, report =>
        {
            if (report.Error.IsError)
            {
                throughput.RecordError();
            }
            else
            {
                latency.RecordTicks(Stopwatch.GetTimestamp() - start);
            }
        }, cancellationToken);
    }
}
