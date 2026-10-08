using System.Net.Sockets;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Restart rules shared by the hosted consumer services for failures outside record processing:
/// initialization, subscription, and polling.
/// </summary>
internal static class ServiceRestartPolicy
{
    /// <summary>
    /// Whether a failure may heal without operator action. Only transient failures qualify:
    /// retriable Kafka errors, timeouts (including an unreachable broker during initialization),
    /// transport errors, and authorization errors, because a consumer group or topic ACL is often
    /// granted after the consumer starts. Every other failure, such as misconfiguration,
    /// authentication, or an unsupported broker version, faults the service.
    /// </summary>
    public static bool CanRestartAfter(Exception exception) => exception switch
    {
        AggregateException aggregate => aggregate.InnerExceptions.Count > 0 && AllCanRestart(aggregate),
        AuthorizationException => true,
        KafkaTimeoutException => true,
        KafkaException kafka => kafka.IsRetriable || kafka.ErrorCode is
            ErrorCode.TopicAuthorizationFailed or ErrorCode.GroupAuthorizationFailed,
        TimeoutException or IOException or SocketException => true,
        _ => false
    };

    private static bool AllCanRestart(AggregateException aggregate)
    {
        foreach (var inner in aggregate.InnerExceptions)
        {
            if (!CanRestartAfter(inner))
                return false;
        }

        return true;
    }

    /// <summary>Doubles <paramref name="initialDelay"/> per consecutive failure, capped at <paramref name="maxDelay"/>.</summary>
    public static TimeSpan GetDelay(TimeSpan initialDelay, TimeSpan maxDelay, int restartAttempt)
    {
        var shift = Math.Clamp(restartAttempt - 1, 0, 62);
        return initialDelay.Ticks > maxDelay.Ticks >> shift
            ? maxDelay
            : TimeSpan.FromTicks(initialDelay.Ticks << shift);
    }
}
