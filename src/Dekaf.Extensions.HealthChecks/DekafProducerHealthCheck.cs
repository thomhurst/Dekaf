using Dekaf.Producer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Health check that verifies the producer can reach Kafka brokers by flushing pending messages.
/// Reports <see cref="HealthStatus.Healthy"/> when the producer can successfully flush,
/// and <see cref="HealthStatus.Unhealthy"/> when the flush fails or times out.
/// </summary>
/// <typeparam name="TKey">The producer key type.</typeparam>
/// <typeparam name="TValue">The producer value type.</typeparam>
public sealed class DekafProducerHealthCheck<TKey, TValue> : IHealthCheck
{
    private readonly IKafkaProducer<TKey, TValue> _producer;
    private readonly DekafProducerHealthCheckOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DekafProducerHealthCheck{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="producer">The Kafka producer to check.</param>
    /// <param name="options">The health check options.</param>
    public DekafProducerHealthCheck(IKafkaProducer<TKey, TValue> producer, DekafProducerHealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(options);
        _producer = producer;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Timeout);

            await _producer.FlushAsync(timeoutCts.Token).ConfigureAwait(false);

            return HealthCheckResult.Healthy("Producer is connected and can flush successfully.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                $"Producer flush timed out after {_options.Timeout.TotalSeconds}s.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Producer health check failed.",
                exception: ex);
        }
    }
}
