using System.Collections.ObjectModel;

namespace Dekaf.Telemetry;

/// <summary>
/// Application metric kind used for broker client telemetry subscriptions.
/// </summary>
public enum ApplicationTelemetryMetricKind
{
    /// <summary>
    /// Monotonic sum encoded as an OpenTelemetry Sum.
    /// </summary>
    Counter,

    /// <summary>
    /// Current value encoded as an OpenTelemetry Gauge.
    /// </summary>
    Gauge
}

/// <summary>
/// Application metric made available to broker client telemetry subscriptions.
/// </summary>
public sealed class ApplicationTelemetryMetric
{
    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0, StringComparer.Ordinal));

    /// <summary>
    /// Creates an application telemetry metric.
    /// </summary>
    /// <param name="name">Metric name exposed to broker subscription prefixes.</param>
    /// <param name="kind">Metric kind used for OpenTelemetry encoding.</param>
    /// <param name="observe">Callback invoked when telemetry is collected. Keep it fast and non-blocking.</param>
    /// <param name="attributes">Optional metric attributes.</param>
    public ApplicationTelemetryMetric(
        string name,
        ApplicationTelemetryMetricKind kind,
        Func<double> observe,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(observe);

        Name = name;
        Kind = kind;
        Observe = observe;
        Attributes = CopyAttributes(attributes);
    }

    /// <summary>
    /// Metric name exposed to broker subscription prefixes.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Metric kind used for OpenTelemetry encoding.
    /// </summary>
    public ApplicationTelemetryMetricKind Kind { get; }

    /// <summary>
    /// Callback invoked when telemetry is collected. Keep it fast and non-blocking.
    /// </summary>
    public Func<double> Observe { get; }

    /// <summary>
    /// Optional metric attributes.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; }

    private static IReadOnlyDictionary<string, string> CopyAttributes(
        IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return EmptyAttributes;
        }

        var copy = new Dictionary<string, string>(attributes.Count, StringComparer.Ordinal);
        foreach (var (name, value) in attributes)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Attribute names must not be null or whitespace.", nameof(attributes));
            }

            if (value is null)
            {
                throw new ArgumentException("Attribute values must not be null.", nameof(attributes));
            }

            copy[name] = value;
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
