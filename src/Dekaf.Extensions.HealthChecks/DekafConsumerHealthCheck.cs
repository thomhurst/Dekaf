using Dekaf.Consumer;
using Dekaf.Producer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Health check that monitors consumer lag per partition.
/// Reports <see cref="HealthStatus.Healthy"/> when all partitions are within the degraded threshold,
/// <see cref="HealthStatus.Degraded"/> when any partition exceeds the degraded threshold,
/// and <see cref="HealthStatus.Unhealthy"/> when any partition exceeds the unhealthy threshold
/// or when the consumer has no assignment.
/// </summary>
/// <typeparam name="TKey">The consumer key type.</typeparam>
/// <typeparam name="TValue">The consumer value type.</typeparam>
public sealed class DekafConsumerHealthCheck<TKey, TValue> : IHealthCheck
{
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
            var assignment = _consumer.Assignment;

            if (assignment.Count == 0)
            {
                return HealthCheckResult.Unhealthy("Consumer has no partition assignment.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Timeout);

            var partitionsWithPositions = new List<(TopicPartition TopicPartition, long Position)>();

            foreach (var topicPartition in assignment)
            {
                var position = _consumer.GetPosition(topicPartition);

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
                .Select(p => _consumer.QueryWatermarkOffsetsAsync(p.TopicPartition, timeoutCts.Token))
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
}
