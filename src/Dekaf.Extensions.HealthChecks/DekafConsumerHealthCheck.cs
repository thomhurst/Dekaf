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

            var lagData = new Dictionary<string, object>();
            long maxLag = 0;

            foreach (var topicPartition in assignment)
            {
                var position = _consumer.GetPosition(topicPartition);
                var watermarks = _consumer.GetWatermarkOffsets(topicPartition);

                if (position is null || watermarks is null)
                {
                    continue;
                }

                var lag = watermarks.Value.High - position.Value;
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
                ["PartitionCount"] = assignment.Count
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
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check consumer health.",
                exception: ex);
        }
    }
}
