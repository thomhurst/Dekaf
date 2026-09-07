using Dekaf.Telemetry;

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    /// <inheritdoc />
    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        ThrowIfDisposed();
        _telemetryMetricCollector.RegisterMetricForSubscription(metric);
    }

    /// <inheritdoc />
    public void UnregisterMetricFromSubscription(string name)
    {
        ThrowIfDisposed();
        _telemetryMetricCollector.UnregisterMetricFromSubscription(name);
    }
}
