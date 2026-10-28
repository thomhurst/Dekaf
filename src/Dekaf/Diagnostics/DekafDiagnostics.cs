using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Dekaf.Diagnostics;

/// <summary>
/// Central holder for Dekaf's <see cref="ActivitySource"/> and <see cref="Meter"/>.
/// Uses standard .NET System.Diagnostics APIs — zero cost when no listener is attached.
/// </summary>
public static class DekafDiagnostics
{
    /// <summary>
    /// The name used for the <see cref="ActivitySource"/>. Register this with
    /// <c>TracerProviderBuilder.AddSource()</c> to capture Dekaf traces.
    /// </summary>
    public const string ActivitySourceName = "Dekaf";

    /// <summary>
    /// The name used for the <see cref="Meter"/>. Register this with
    /// <c>MeterProviderBuilder.AddMeter()</c> to capture Dekaf metrics.
    /// </summary>
    public const string MeterName = "Dekaf";

    internal static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>
    /// Parent for send spans started on this async flow in place of <see cref="Activity.Current"/>;
    /// default means none. The outbox relay sets it so a send continues the row's stored trace
    /// even when the relay span itself is not sampled. A non-default value also tells the
    /// producer to replace the stored propagation headers only after creating a send span.
    /// </summary>
    internal static readonly AsyncLocal<ActivityContext> SendParentContext = new();
    internal static readonly Meter Meter = new(MeterName);

    // OTel semantic convention attribute names — messaging
    internal const string MessagingSystem = "messaging.system";
    internal const string MessagingDestinationName = "messaging.destination.name";
    internal const string MessagingOperationName = "messaging.operation.name";
    internal const string MessagingOperationType = "messaging.operation.type";
    internal const string MessagingKafkaOffset = "messaging.kafka.offset";
    internal const string MessagingDestinationPartitionId = "messaging.destination.partition.id";
    internal const string MessagingConsumerGroupName = "messaging.consumer.group.name";
    internal const string MessagingMessageKey = "messaging.kafka.message.key";
    internal const string MessagingKafkaClusterId = "messaging.kafka.cluster.id";
    internal const string MessagingKafkaTombstone = "messaging.kafka.message.tombstone";
    internal const string MessagingClientId = "messaging.client.id";
    internal const string MessagingMessageId = "messaging.message.id";

    /// <summary>Payload (value) size only — the key is not part of the message body.</summary>
    internal const string MessagingMessageBodySize = "messaging.message.body.size";
    internal const string ErrorType = "error.type";

    // Dekaf-specific attribute — Kafka's numeric broker id has no OTel equivalent
    // (the registry models brokers as server.address/server.port), so it lives in
    // the dekaf.* namespace and is only attached to dekaf.* metrics.
    internal const string DekafBrokerId = "dekaf.broker.id";

    internal const string MessagingSystemValue = "kafka";

    // messaging.operation.type well-known values / messaging.operation.name values.
    // Consume spans come in two flavors matching their activity lifetimes:
    // the streaming ConsumeAsync span stays open while user code handles the
    // record (its duration covers handling), so it is a "process" span (CONSUMER
    // kind); the ConsumeOne span ends before the record is returned, so it is a
    // "receive" span named "poll" (CLIENT kind). "poll" is also the operation
    // name on consumed-message metrics.
    internal const string OperationTypeSend = "send";
    internal const string OperationTypeReceive = "receive";
    internal const string OperationTypeProcess = "process";
    internal const string OperationNameSend = "send";
    internal const string OperationNameProcess = "process";
    internal const string OperationNamePoll = "poll";

    // OTel semantic convention attribute names — exceptions
    internal const string ExceptionType = "exception.type";
    internal const string ExceptionMessage = "exception.message";
    internal const string ExceptionStacktrace = "exception.stacktrace";

    /// <summary>Cached box for boolean tags — SetTag(string, object) would box a fresh bool per call.</summary>
    internal static readonly object BoxedTrue = true;

    // messaging.destination.partition.id is a string attribute in the messaging semconv.
    // One cached string per partition keeps traced sends, consume spans and lag observations
    // free of per-call formatting and int boxing. Ids past the table go to a dictionary, so large
    // topics also format each id once; it holds only ids this process has tagged. Negative ids are
    // not real partitions and are formatted per call.
    private const int CachedPartitionIdCount = 1024;
    private static readonly string?[] PartitionIds = new string?[CachedPartitionIdCount];
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> OverflowPartitionIds = new();

    /// <summary>
    /// The <see cref="MessagingDestinationPartitionId"/> tag value for <paramref name="partition"/>.
    /// Concurrent first calls may each format the same id; either string is correct.
    /// </summary>
    internal static string PartitionIdTagValue(int partition)
    {
        if ((uint)partition < (uint)PartitionIds.Length)
            return PartitionIds[partition] ??= FormatPartitionId(partition);

        return partition < 0
            ? FormatPartitionId(partition)
            : OverflowPartitionIds.GetOrAdd(partition, static id => FormatPartitionId(id));
    }

    private static string FormatPartitionId(int partition) =>
        partition.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Span name per messaging semconv: "{operation name} {destination}". Derived from the
    /// same constants as the messaging.operation.name tag so the two cannot desynchronize.
    /// Callers cache the result per topic.
    /// </summary>
    internal static string SendSpanName(string topic) => string.Concat(OperationNameSend, " ", topic);

    /// <inheritdoc cref="SendSpanName"/>
    internal static string ProcessSpanName(string topic) => string.Concat(OperationNameProcess, " ", topic);

    /// <inheritdoc cref="SendSpanName"/>
    internal static string PollSpanName(string topic) => string.Concat(OperationNamePoll, " ", topic);

    /// <summary>
    /// Tag set for messaging.client.* instruments: the spec-required messaging.system and
    /// messaging.operation.name plus the destination. <see cref="TagList"/> is a stack struct
    /// holding reference-typed tag values, so building one costs no allocation; per-batch call
    /// sites (see <c>DekafMetrics.RecordBatchDelivered</c>) deliberately build it inline rather
    /// than caching per topic, which measured cheaper and avoids an unbounded per-topic cache.
    /// </summary>
    internal static System.Diagnostics.TagList ClientMetricTags(string operationName, string topic) => new()
    {
        { MessagingSystem, MessagingSystemValue },
        { MessagingOperationName, operationName },
        { MessagingDestinationName, topic }
    };

    /// <summary>
    /// Records an exception on an activity following OTel exception semantic conventions.
    /// Sets error status and adds an exception event with type, message, and stacktrace.
    /// </summary>
    internal static void RecordException(Activity activity, Exception ex)
    {
        var exceptionType = ex.GetType().FullName;
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag(ErrorType, exceptionType);
        activity.AddEvent(new ActivityEvent("exception",
            tags: new ActivityTagsCollection
            {
                { ExceptionType, exceptionType },
                { ExceptionMessage, ex.Message },
                { ExceptionStacktrace, ex.ToString() }
            }));
    }
}
