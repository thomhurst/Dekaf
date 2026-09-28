using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Health check that verifies a share consumer (KIP-932) holds live share group membership.
/// Reports <see cref="HealthStatus.Healthy"/> when the member is stable and its heartbeat is fresh,
/// and <see cref="HealthStatus.Unhealthy"/> otherwise.
/// A heartbeat is considered stale after three missed broker-directed heartbeat intervals.
/// </summary>
/// <remarks>
/// The check reads a local status snapshot. It does not poll, acquire or acknowledge records.
/// A share group can have more members than partitions, so an idle member without records is healthy.
/// </remarks>
/// <typeparam name="TKey">The consumer key type.</typeparam>
/// <typeparam name="TValue">The consumer value type.</typeparam>
public sealed class DekafShareConsumerHealthCheck<TKey, TValue> : IHealthCheck
{
    private const int HeartbeatStaleIntervalMultiplier = 3;

    private readonly IKafkaShareConsumer<TKey, TValue> _consumer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DekafShareConsumerHealthCheck{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="consumer">The share consumer to monitor.</param>
    public DekafShareConsumerHealthCheck(IKafkaShareConsumer<TKey, TValue> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        _consumer = consumer;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_consumer is not IKafkaClientStatusProvider statusProvider)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Share consumer does not expose operational status."));
        }

        var status = statusProvider.GetStatus();
        return Task.FromResult(Evaluate(status.IsStopped, status.ConsumerGroup));
    }

    private static HealthCheckResult Evaluate(bool isStopped, ConsumerGroupStatus? group)
    {
        if (isStopped)
            return HealthCheckResult.Unhealthy("Share consumer is stopped.");

        if (group is null || !group.HasConsumerGroup)
            return HealthCheckResult.Unhealthy("Share consumer has no share group.");

        var data = new Dictionary<string, object>
        {
            ["State"] = group.State.ToString(),
            ["MemberEpoch"] = group.GenerationOrMemberEpoch,
            ["AssignedPartitionCount"] = group.Assignment.Count
        };

        if (group.LastHeartbeatFailure is { Length: > 0 } failure)
        {
            return HealthCheckResult.Unhealthy(
                $"Share consumer heartbeat failed: {failure}",
                data: data);
        }

        if (group.State != CoordinatorState.Stable || string.IsNullOrEmpty(group.MemberId))
        {
            return HealthCheckResult.Unhealthy(
                "Share consumer is not a stable member of its share group.",
                data: data);
        }

        if (group.TimeSinceLastHeartbeat is not { } heartbeatAge
            || group.HeartbeatInterval <= TimeSpan.Zero
            || heartbeatAge > group.HeartbeatInterval * HeartbeatStaleIntervalMultiplier)
        {
            return HealthCheckResult.Unhealthy(
                "Share consumer heartbeat is missing or stale.",
                data: data);
        }

        return HealthCheckResult.Healthy("Share consumer has live share group membership.", data);
    }
}
