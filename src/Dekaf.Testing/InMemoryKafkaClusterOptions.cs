using Dekaf.Admin;

namespace Dekaf.Testing;

/// <summary>
/// Options for an <see cref="InMemoryKafkaCluster"/>.
/// </summary>
public sealed class InMemoryKafkaClusterOptions
{
    /// <summary>
    /// Number of partitions created for auto-created topics.
    /// </summary>
    public int DefaultPartitionCount { get; set; } = 1;

    /// <summary>
    /// Whether producers and consumers may create missing topics on first use.
    /// </summary>
    public bool AutoCreateTopics { get; set; } = true;

    /// <summary>
    /// Cluster ID surfaced by admin operations.
    /// </summary>
    public string ClusterId { get; set; } = "dekaf-in-memory";

    /// <summary>
    /// Broker-supported feature ranges surfaced by the in-memory admin client.
    /// </summary>
    public IDictionary<string, FeatureVersionRange> SupportedFeatures { get; set; } =
        new Dictionary<string, FeatureVersionRange>(StringComparer.Ordinal);
}
