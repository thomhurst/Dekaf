using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
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
    /// Adds a health check that waits for a producer flush checkpoint to complete within its timeout.
    /// Concurrent production can leave newer messages queued. Successful delivery and broker connectivity
    /// are not evaluated. Use <see cref="DekafBrokerHealthCheck"/> for broker connectivity.
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
        string name = "dekaf-broker",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        DekafBrokerHealthCheckOptions? options = null)
    {
        var healthCheckOptions = options ?? new DekafBrokerHealthCheckOptions();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafBrokerHealthCheck(
                sp.GetRequiredService<IAdminClient>(),
                healthCheckOptions),
            failureStatus,
            tags));
    }

    /// <summary>
    /// Adds a health check that monitors lag for a keyed consumer.
    /// The consumer must be registered as a keyed <see cref="IKafkaConsumer{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The consumer key type.</typeparam>
    /// <typeparam name="TValue">The consumer value type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="serviceKey">The key the consumer is registered under.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="options">Optional consumer health check options. If null, default thresholds are used.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDekafConsumerHealthCheck<TKey, TValue>(
        this IHealthChecksBuilder builder,
        object serviceKey,
        string name,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        DekafConsumerHealthCheckOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        var healthCheckOptions = options ?? new DekafConsumerHealthCheckOptions();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafConsumerHealthCheck<TKey, TValue>(
                sp.GetRequiredKeyedService<IKafkaConsumer<TKey, TValue>>(serviceKey),
                healthCheckOptions),
            failureStatus,
            tags));
    }

    /// <summary>
    /// Adds a health check that waits for a keyed producer's flush checkpoint to complete within its timeout.
    /// The producer must be registered as a keyed <see cref="IKafkaProducer{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The producer key type.</typeparam>
    /// <typeparam name="TValue">The producer value type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="serviceKey">The key the producer is registered under.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="options">Optional producer health check options. If null, default timeout is used.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDekafProducerHealthCheck<TKey, TValue>(
        this IHealthChecksBuilder builder,
        object serviceKey,
        string name,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        DekafProducerHealthCheckOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        var healthCheckOptions = options ?? new DekafProducerHealthCheckOptions();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafProducerHealthCheck<TKey, TValue>(
                sp.GetRequiredKeyedService<IKafkaProducer<TKey, TValue>>(serviceKey),
                healthCheckOptions),
            failureStatus,
            tags));
    }

    /// <summary>
    /// Adds a health check that verifies Kafka broker connectivity using a keyed admin client.
    /// The admin client must be registered as a keyed <see cref="IAdminClient"/>.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="serviceKey">The key the admin client is registered under.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="options">Optional broker health check options. If null, default timeout is used.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDekafBrokerHealthCheck(
        this IHealthChecksBuilder builder,
        object serviceKey,
        string name,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        DekafBrokerHealthCheckOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        var healthCheckOptions = options ?? new DekafBrokerHealthCheckOptions();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafBrokerHealthCheck(
                sp.GetRequiredKeyedService<IAdminClient>(serviceKey),
                healthCheckOptions),
            failureStatus,
            tags));
    }

    /// <summary>
    /// Adds a health check that verifies a share consumer holds live share group membership.
    /// The consumer must be registered as <see cref="IKafkaShareConsumer{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The consumer key type.</typeparam>
    /// <typeparam name="TValue">The consumer value type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name. Defaults to "dekaf-share-consumer".</param>
    /// <param name="failureStatus">The status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDekafShareConsumerHealthCheck<TKey, TValue>(
        this IHealthChecksBuilder builder,
        string name = "dekaf-share-consumer",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafShareConsumerHealthCheck<TKey, TValue>(
                sp.GetRequiredService<IKafkaShareConsumer<TKey, TValue>>()),
            failureStatus,
            tags));
    }

    /// <summary>
    /// Adds a health check that verifies a keyed share consumer holds live share group membership.
    /// The consumer must be registered as a keyed <see cref="IKafkaShareConsumer{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The consumer key type.</typeparam>
    /// <typeparam name="TValue">The consumer value type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="serviceKey">
    /// The key the consumer is registered under.
    /// Use <c>KafkaShareConsumerServiceKey</c> from Dekaf.Extensions.Hosting to check a hosted service's own consumer.
    /// </param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDekafShareConsumerHealthCheck<TKey, TValue>(
        this IHealthChecksBuilder builder,
        object serviceKey,
        string name,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DekafShareConsumerHealthCheck<TKey, TValue>(
                sp.GetRequiredKeyedService<IKafkaShareConsumer<TKey, TValue>>(serviceKey)),
            failureStatus,
            tags));
    }
}
