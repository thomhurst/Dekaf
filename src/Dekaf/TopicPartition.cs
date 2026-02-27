namespace Dekaf;

/// <summary>
/// Represents a topic and partition.
/// </summary>
public readonly record struct TopicPartition(string Topic, int Partition);

/// <summary>
/// Represents a topic, partition, and offset.
/// </summary>
public readonly record struct TopicPartitionOffset(string Topic, int Partition, long Offset);

/// <summary>
/// Represents a topic, partition, and timestamp for offset lookup.
/// </summary>
/// <param name="Topic">The topic name.</param>
/// <param name="Partition">The partition index.</param>
/// <param name="Timestamp">The timestamp to search for. Use -1 for latest offset, -2 for earliest offset.</param>
public readonly record struct TopicPartitionTimestamp(string Topic, int Partition, long Timestamp)
{
    /// <summary>
    /// Special timestamp value to get the latest offset.
    /// </summary>
    public const long Latest = -1;

    /// <summary>
    /// Special timestamp value to get the earliest offset.
    /// </summary>
    public const long Earliest = -2;

    /// <summary>
    /// Creates a TopicPartitionTimestamp from a TopicPartition and timestamp.
    /// </summary>
    public TopicPartitionTimestamp(TopicPartition topicPartition, long timestamp)
        : this(topicPartition.Topic, topicPartition.Partition, timestamp)
    {
    }

    /// <summary>
    /// Creates a TopicPartitionTimestamp from a TopicPartition and DateTimeOffset.
    /// </summary>
    public TopicPartitionTimestamp(TopicPartition topicPartition, DateTimeOffset timestamp)
        : this(topicPartition.Topic, topicPartition.Partition, timestamp.ToUnixTimeMilliseconds())
    {
    }

    /// <summary>
    /// Gets the TopicPartition for this timestamp lookup.
    /// </summary>
    public TopicPartition TopicPartition => new(Topic, Partition);
}

/// <summary>
/// Represents the low and high watermark offsets for a partition.
/// Low watermark is the earliest available offset (log start offset).
/// High watermark is the next offset to be written (end of log).
/// </summary>
public readonly record struct WatermarkOffsets(long Low, long High);
