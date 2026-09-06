using Dekaf.Producer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Health check that waits for a producer flush checkpoint to complete.
/// Reports <see cref="HealthStatus.Healthy"/> when the flush completes,
/// and <see cref="HealthStatus.Unhealthy"/> when the flush throws or times out.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope:</b> Flush completion does not establish successful delivery. Failed delivery
/// attempts also leave the queue. Observe produce results or delivery callbacks for those failures;
/// this check does not retain or evaluate delivery history.
/// </para>
/// <para>Concurrent production can leave newer messages queued after the flush checkpoint
/// completes. Healthy does not assert that the current queue is empty.</para>
/// <para>
/// <see cref="IKafkaProducer{TKey, TValue}.FlushAsync"/> returns immediately when no messages are pending,
/// even if all brokers are offline. This means the check will report <see cref="HealthStatus.Healthy"/>
/// when the producer has an empty queue, regardless of actual broker connectivity.
/// </para>
/// <para>
/// For a true broker connectivity check, use <see cref="DekafBrokerHealthCheck"/> with
/// <see cref="Dekaf.Admin.IAdminClient.DescribeClusterAsync"/>, which actively queries the cluster metadata.
/// </para>
/// <para>Each invocation evaluates its own flush. A successful flush after a failed or timed-out
/// check returns Healthy; earlier failures do not latch this check into an unhealthy state.</para>
/// </remarks>
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

            return HealthCheckResult.Healthy(
                "Producer flush checkpoint completed. Delivery outcomes and broker connectivity are not checked.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                $"Producer flush timed out after {_options.Timeout.TotalSeconds}s.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Producer flush checkpoint check failed.",
                exception: ex);
        }
    }
}
