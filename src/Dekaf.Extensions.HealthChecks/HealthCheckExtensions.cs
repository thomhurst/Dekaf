using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Extension methods for registering Dekaf health checks with <see cref="IHealthChecksBuilder"/>.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds a health check that monitors consumer lag per partition.
    /// The consumer must be registered in the service collection as <see cref="IKafkaConsumer{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The consumer key type.</typeparam>
    /// <typeparam name="TValue">The consumer value type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name. Defaults to "dekaf-consumer".</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> that should be reported when the health check reports a failure.
    /// If null, the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="options">Optional consumer health check options. If null, default thresholds are used.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDekafConsumerHealthCheck<TKey, TValue>(
        this IHealthChecksBuilder builder,
        string name = "dekaf-consumer",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        DekafConsumerHealthCheckOptions? options = null)
    {
        var healthCheckOptions = options ?? new DekafConsumerHealthCheckOptions();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafConsumerHealthCheck<TKey, TValue>(
                sp.GetRequiredService<IKafkaConsumer<TKey, TValue>>(),
                healthCheckOptions),
            failureStatus,
            tags));
    }

    /// <summary>
    /// Adds a health check that verifies producer connectivity by flushing pending messages.
    /// The producer must be registered in the service collection as <see cref="IKafkaProducer{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The producer key type.</typeparam>
    /// <typeparam name="TValue">The producer value type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name. Defaults to "dekaf-producer".</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> that should be reported when the health check reports a failure.
    /// If null, the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="options">Optional producer health check options. If null, default timeout is used.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDekafProducerHealthCheck<TKey, TValue>(
        this IHealthChecksBuilder builder,
        string name = "dekaf-producer",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        DekafProducerHealthCheckOptions? options = null)
    {
        var healthCheckOptions = options ?? new DekafProducerHealthCheckOptions();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafProducerHealthCheck<TKey, TValue>(
                sp.GetRequiredService<IKafkaProducer<TKey, TValue>>(),
                healthCheckOptions),
            failureStatus,
            tags));
    }

    /// <summary>
    /// Adds a health check that verifies Kafka broker connectivity using an admin client.
    /// The admin client must be registered in the service collection as <see cref="IAdminClient"/>.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name. Defaults to "dekaf-broker".</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> that should be reported when the health check reports a failure.
    /// If null, the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="options">Optional broker health check options. If null, default timeout is used.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDekafBrokerHealthCheck(
        this IHealthChecksBuilder builder,
        DekafBrokerHealthCheckOptions options,
        string name = "dekaf-broker",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafBrokerHealthCheck(
                sp.GetRequiredService<IAdminClient>(),
                options),
            failureStatus,
            tags));
    }
}
