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
    internal static readonly Meter Meter = new(MeterName);

    // OTel semantic convention attribute names — messaging
    internal const string MessagingSystem = "messaging.system";
    internal const string MessagingDestinationName = "messaging.destination.name";
    internal const string MessagingOperationType = "messaging.operation.type";
    internal const string MessagingMessageOffset = "messaging.kafka.message.offset";
    internal const string MessagingDestinationPartitionId = "messaging.destination.partition.id";
    internal const string MessagingConsumerGroupName = "messaging.consumer.group.name";
    internal const string MessagingMessageKey = "messaging.kafka.message.key";
    internal const string MessagingClientId = "messaging.client.id";
    internal const string MessagingMessageBodySize = "messaging.message.body.size";

    internal const string MessagingSystemValue = "kafka";

    // OTel semantic convention attribute names — exceptions
    internal const string ExceptionType = "exception.type";
    internal const string ExceptionMessage = "exception.message";
    internal const string ExceptionStacktrace = "exception.stacktrace";

    /// <summary>
    /// Records an exception on an activity following OTel exception semantic conventions.
    /// Sets error status and adds an exception event with type, message, and stacktrace.
    /// </summary>
    internal static void RecordException(Activity activity, Exception ex)
    {
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddEvent(new ActivityEvent("exception",
            tags: new ActivityTagsCollection
            {
                { ExceptionType, ex.GetType().FullName },
                { ExceptionMessage, ex.Message },
                { ExceptionStacktrace, ex.ToString() }
            }));
    }
}
