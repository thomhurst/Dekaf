using Dekaf.Consumer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Health check that monitors consumer lag per partition.
/// Reports <see cref="HealthStatus.Healthy"/> when all partitions are within the degraded threshold,
/// <see cref="HealthStatus.Degraded"/> when any partition exceeds the degraded threshold,
/// and <see cref="HealthStatus.Unhealthy"/> when any partition exceeds the unhealthy threshold
/// or when the consumer's group membership is not live.
/// A heartbeat is considered stale after three missed broker-directed heartbeat intervals.
/// </summary>
/// <typeparam name="TKey">The consumer key type.</typeparam>
/// <typeparam name="TValue">The consumer value type.</typeparam>
public sealed class DekafConsumerHealthCheck<TKey, TValue> : IHealthCheck
{
    private const int HeartbeatStaleIntervalMultiplier = 3;

    private readonly IKafkaConsumer<TKey, TValue> _consumer;
    private readonly DekafConsumerHealthCheckOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DekafConsumerHealthCheck{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="consumer">The Kafka consumer to monitor.</param>
    /// <param name="options">The health check options.</param>
    public DekafConsumerHealthCheck(IKafkaConsumer<TKey, TValue> consumer, DekafConsumerHealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _consumer = consumer;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var partitions = _consumer.Partitions;
            var positions = _consumer.Positions;
            var offsets = _consumer.Offsets;
            var assignment = partitions.Assignment;

            var livenessResult = CheckGroupLiveness(assignment.Count);
            if (livenessResult is { } result)
                return result;

            if (assignment.Count == 0)
            {
                return HealthCheckResult.Unhealthy(
                    "Consumer has no partition assignment and no live group membership.",
                    data: CreateLivenessData("MissingGroupMembership", assignment.Count));
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Timeout);

            var partitionsWithPositions = new List<(TopicPartition TopicPartition, long Position)>();

            foreach (var topicPartition in assignment)
            {
                var position = positions.GetPosition(topicPartition);

                if (position is not null)
                {
                    partitionsWithPositions.Add((topicPartition, position.Value));
                }
            }

            if (partitionsWithPositions.Count == 0)
            {
                return HealthCheckResult.Degraded(
                    "Consumer has not yet consumed any messages.",
                    data: new Dictionary<string, object>
                    {
                        ["AssignedPartitionCount"] = assignment.Count,
                        ["MeasuredPartitionCount"] = 0
                    });
            }

            var watermarkTasks = partitionsWithPositions
                .Select(p => offsets.QueryWatermarkOffsetsAsync(p.TopicPartition, timeoutCts.Token))
                .ToArray();

            var watermarkResults = await Task.WhenAll(
                watermarkTasks.Select(static vt => vt.AsTask())).ConfigureAwait(false);

            var lagData = new Dictionary<string, object>();
            long maxLag = 0;

            for (var i = 0; i < partitionsWithPositions.Count; i++)
            {
                var (topicPartition, position) = partitionsWithPositions[i];
                var watermarks = watermarkResults[i];

                var lag = watermarks.High - position;
                if (lag < 0)
                {
                    lag = 0;
                }

                var key = $"{topicPartition.Topic}[{topicPartition.Partition}]";
                lagData[key] = lag;

                if (lag > maxLag)
                {
                    maxLag = lag;
                }
            }

            var data = new Dictionary<string, object>(lagData)
            {
                ["MaxLag"] = maxLag,
                ["AssignedPartitionCount"] = assignment.Count,
                ["MeasuredPartitionCount"] = partitionsWithPositions.Count
            };

            if (maxLag >= _options.UnhealthyThreshold)
            {
                return HealthCheckResult.Unhealthy(
                    $"Consumer lag ({maxLag}) exceeds unhealthy threshold ({_options.UnhealthyThreshold}).",
                    data: data);
            }

            if (maxLag >= _options.DegradedThreshold)
            {
                return HealthCheckResult.Degraded(
                    $"Consumer lag ({maxLag}) exceeds degraded threshold ({_options.DegradedThreshold}).",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Consumer lag ({maxLag}) is within acceptable limits.",
                data: data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                $"Consumer health check timed out after {_options.Timeout.TotalSeconds}s.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check consumer health.",
                exception: ex);
        }
    }

    private HealthCheckResult? CheckGroupLiveness(int assignedPartitionCount)
    {
        if (_consumer is not IConsumerGroupLiveness livenessSource)
            return null;

        var liveness = livenessSource.GroupLiveness;

        if (liveness.IsStopped)
        {
            return HealthCheckResult.Unhealthy(
                "Consumer is stopped.",
                data: CreateLivenessData("Stopped", assignedPartitionCount, liveness));
        }

        if (liveness.HasConsumerGroup && liveness.LastHeartbeatFailure is { Length: > 0 } failure)
        {
            return HealthCheckResult.Unhealthy(
                $"Consumer coordinator heartbeat failed: {failure}",
                data: CreateLivenessData("CoordinatorFailure", assignedPartitionCount, liveness));
        }

        if (liveness.HasConsumerGroup && !liveness.IsJoined)
        {
            return HealthCheckResult.Unhealthy(
                "Consumer is not joined to its consumer group.",
                data: CreateLivenessData("MissingGroupMembership", assignedPartitionCount, liveness));
        }

        var heartbeatAge = liveness.TimeSinceLastHeartbeat;
        var heartbeatStaleThreshold = GetHeartbeatStaleThreshold(liveness.HeartbeatInterval);
        if (liveness.HasConsumerGroup &&
            (heartbeatAge is null || heartbeatAge > heartbeatStaleThreshold))
        {
            return HealthCheckResult.Unhealthy(
                heartbeatAge is null
                    ? "Consumer has joined its group but has no successful heartbeat."
                    : $"Consumer heartbeat is stale ({heartbeatAge.Value.TotalSeconds:F1}s old; " +
                      $"expected interval is {liveness.HeartbeatInterval.TotalSeconds:F1}s).",
                data: CreateLivenessData("StaleHeartbeat", assignedPartitionCount, liveness));
        }

        if (assignedPartitionCount == 0 && liveness.HasConsumerGroup)
            return CreateStandbyResult(CreateLivenessData("Standby", assignedPartitionCount, liveness));

        return null;
    }

    private HealthCheckResult CreateStandbyResult(Dictionary<string, object> data) => new(
        _options.NoAssignmentStatus,
        "Consumer is a live standby group member with no partition assignment.",
        data: data);

    private static TimeSpan GetHeartbeatStaleThreshold(TimeSpan heartbeatInterval)
    {
        if (heartbeatInterval <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return heartbeatInterval.Ticks > TimeSpan.MaxValue.Ticks / HeartbeatStaleIntervalMultiplier
            ? TimeSpan.MaxValue
            : TimeSpan.FromTicks(heartbeatInterval.Ticks * HeartbeatStaleIntervalMultiplier);
    }

    private static Dictionary<string, object> CreateLivenessData(
        string consumerState,
        int assignedPartitionCount,
        ConsumerGroupLiveness? liveness = null)
    {
        var data = new Dictionary<string, object>
        {
            ["ConsumerState"] = consumerState,
            ["AssignedPartitionCount"] = assignedPartitionCount
        };

        if (liveness is { } snapshot)
        {
            data["HasConsumerGroup"] = snapshot.HasConsumerGroup;
            data["IsJoined"] = snapshot.IsJoined;
            data["IsStopped"] = snapshot.IsStopped;
            data["HeartbeatIntervalMilliseconds"] = snapshot.HeartbeatInterval.TotalMilliseconds;
            data["HeartbeatStaleThresholdMilliseconds"] =
                GetHeartbeatStaleThreshold(snapshot.HeartbeatInterval).TotalMilliseconds;
            if (snapshot.TimeSinceLastHeartbeat is { } heartbeatAge)
                data["HeartbeatAgeMilliseconds"] = heartbeatAge.TotalMilliseconds;
            if (snapshot.LastHeartbeatFailure is { } failure)
                data["LastHeartbeatFailure"] = failure;
        }

        return data;
    }
}
