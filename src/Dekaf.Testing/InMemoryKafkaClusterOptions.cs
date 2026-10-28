using Dekaf.Admin;
using Dekaf.Producer;

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
    /// Partitioner that in-memory producers use for records produced without an explicit partition.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/> (the default), keyed records go to the partition that the real
    /// producer's <see cref="DefaultPartitioner"/> selects (Kafka-compatible Murmur2), and records
    /// with a null or empty key rotate across partitions. Set this to the partitioner the
    /// application configures, for example <see cref="Murmur2RandomPartitioner"/>, so that tests see
    /// the same placement. The partitioner is called under the cluster's lock.
    /// </remarks>
    public IPartitioner? Partitioner { get; set; }

    /// <summary>
    /// Broker-supported feature ranges surfaced by the in-memory admin client.
    /// </summary>
    public IDictionary<string, FeatureVersionRange> SupportedFeatures { get; set; } =
        new Dictionary<string, FeatureVersionRange>(StringComparer.Ordinal);
}
