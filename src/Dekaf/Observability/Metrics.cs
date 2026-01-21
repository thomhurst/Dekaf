using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Dekaf.Observability;

/// <summary>
/// Metrics for Dekaf Kafka client.
/// </summary>
public static class DekafMetrics
{
    /// <summary>
    /// The meter name for Dekaf metrics.
    /// </summary>
    public const string MeterName = "Dekaf";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    // Producer metrics
    public static readonly Counter<long> ProducerMessagesSent = Meter.CreateCounter<long>(
        "dekaf.producer.messages.sent",
        "messages",
        "Number of messages sent by the producer");

    public static readonly Counter<long> ProducerBytesSent = Meter.CreateCounter<long>(
        "dekaf.producer.bytes.sent",
        "bytes",
        "Number of bytes sent by the producer");

    public static readonly Counter<long> ProducerErrors = Meter.CreateCounter<long>(
        "dekaf.producer.errors",
        "errors",
        "Number of producer errors");

    public static readonly Histogram<double> ProducerLatency = Meter.CreateHistogram<double>(
        "dekaf.producer.latency",
        "milliseconds",
        "Producer send latency");

    // Consumer metrics
    public static readonly Counter<long> ConsumerMessagesReceived = Meter.CreateCounter<long>(
        "dekaf.consumer.messages.received",
        "messages",
        "Number of messages received by the consumer");

    public static readonly Counter<long> ConsumerBytesReceived = Meter.CreateCounter<long>(
        "dekaf.consumer.bytes.received",
        "bytes",
        "Number of bytes received by the consumer");

    public static readonly Counter<long> ConsumerErrors = Meter.CreateCounter<long>(
        "dekaf.consumer.errors",
        "errors",
        "Number of consumer errors");

    public static readonly Histogram<double> ConsumerFetchLatency = Meter.CreateHistogram<double>(
        "dekaf.consumer.fetch.latency",
        "milliseconds",
        "Consumer fetch latency");

    public static readonly Counter<long> ConsumerRebalances = Meter.CreateCounter<long>(
        "dekaf.consumer.rebalances",
        "rebalances",
        "Number of consumer group rebalances");

    // Connection metrics
    public static readonly UpDownCounter<int> ConnectionsActive = Meter.CreateUpDownCounter<int>(
        "dekaf.connections.active",
        "connections",
        "Number of active connections");

    public static readonly Counter<long> ConnectionsCreated = Meter.CreateCounter<long>(
        "dekaf.connections.created",
        "connections",
        "Number of connections created");

    public static readonly Counter<long> ConnectionsClosed = Meter.CreateCounter<long>(
        "dekaf.connections.closed",
        "connections",
        "Number of connections closed");
}

/// <summary>
/// Activity source for Dekaf distributed tracing.
/// </summary>
public static class DekafActivitySource
{
    /// <summary>
    /// The activity source name for Dekaf.
    /// </summary>
    public const string SourceName = "Dekaf";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    /// <summary>
    /// Starts a produce activity.
    /// </summary>
    public static Activity? StartProduce(string topic, int? partition = null)
    {
        var activity = Source.StartActivity("produce", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", topic);
        activity?.SetTag("messaging.operation", "publish");

        if (partition.HasValue)
        {
            activity?.SetTag("messaging.kafka.destination.partition", partition.Value);
        }

        return activity;
    }

    /// <summary>
    /// Starts a consume activity.
    /// </summary>
    public static Activity? StartConsume(string topic, int partition, long offset)
    {
        var activity = Source.StartActivity("consume", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.source.name", topic);
        activity?.SetTag("messaging.kafka.source.partition", partition);
        activity?.SetTag("messaging.kafka.message.offset", offset);
        activity?.SetTag("messaging.operation", "receive");

        return activity;
    }

    /// <summary>
    /// Starts a fetch activity.
    /// </summary>
    public static Activity? StartFetch(int brokerId)
    {
        var activity = Source.StartActivity("fetch", ActivityKind.Client);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("server.address", brokerId.ToString());

        return activity;
    }
}
