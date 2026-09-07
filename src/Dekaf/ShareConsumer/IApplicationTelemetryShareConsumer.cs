using Dekaf.Telemetry;

namespace Dekaf.ShareConsumer;

/// <summary>Optional share-consumer capability for application metrics in broker telemetry subscriptions.</summary>
public interface IApplicationTelemetryShareConsumer
{
    /// <summary>Registers a metric for broker-requested prefixes. The same name replaces its previous registration.</summary>
    /// <remarks>The observation callback runs only during a subscribed telemetry collection.</remarks>
    void RegisterMetricForSubscription(ApplicationTelemetryMetric metric);

    /// <summary>Removes a metric registration. Missing names are ignored.</summary>
    void UnregisterMetricFromSubscription(string name);
}

/// <summary>Application telemetry registration through a share consumer's optional capability.</summary>
public static class ShareConsumerTelemetryExtensions
{
    /// <summary>Registers or replaces an application metric for broker telemetry subscriptions.</summary>
    /// <exception cref="NotSupportedException">The consumer does not implement <see cref="IApplicationTelemetryShareConsumer"/>.</exception>
    public static void RegisterMetricForSubscription<TKey, TValue>(
        this IKafkaShareConsumer<TKey, TValue> consumer, ApplicationTelemetryMetric metric)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        if (consumer is not IApplicationTelemetryShareConsumer telemetry)
            throw new NotSupportedException("The share consumer does not support application telemetry registration.");
        telemetry.RegisterMetricForSubscription(metric);
    }

    /// <summary>Removes an application metric registration. Missing names are ignored.</summary>
    /// <exception cref="NotSupportedException">The consumer does not implement <see cref="IApplicationTelemetryShareConsumer"/>.</exception>
    public static void UnregisterMetricFromSubscription<TKey, TValue>(
        this IKafkaShareConsumer<TKey, TValue> consumer, string name)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        if (consumer is not IApplicationTelemetryShareConsumer telemetry)
            throw new NotSupportedException("The share consumer does not support application telemetry registration.");
        telemetry.UnregisterMetricFromSubscription(name);
    }
}
