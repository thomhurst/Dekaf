namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeTopicPartitions request (API key 75).
/// Describes topic partitions with broker-side pagination.
/// </summary>
public sealed class DescribeTopicPartitionsRequest : IKafkaRequest<DescribeTopicPartitionsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeTopicPartitions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The topics to fetch details for.
    /// </summary>
    public required IReadOnlyList<DescribeTopicPartitionsRequestTopic> Topics { get; init; }

    /// <summary>
    /// The maximum number of partitions included in the response.
    /// </summary>
    public int ResponsePartitionLimit { get; init; } = 2000;

    /// <summary>
    /// The first topic and partition index to fetch details for, or null for the first page.
    /// </summary>
    public DescribeTopicPartitionsRequestCursor? Cursor { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, DescribeTopicPartitionsRequestTopic topic) => topic.Write(ref w));

        writer.WriteInt32(ResponsePartitionLimit);

        DescribeTopicPartitionsRequestCursor.WriteNullable(ref writer, Cursor);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic requested by DescribeTopicPartitions.
/// </summary>
public sealed class DescribeTopicPartitionsRequestTopic
{
    public required string Name { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Name);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Cursor used by DescribeTopicPartitions pagination.
/// </summary>
public sealed class DescribeTopicPartitionsRequestCursor
{
    public required string TopicName { get; init; }
    public int PartitionIndex { get; init; }

    public static void WriteNullable(ref KafkaProtocolWriter writer, DescribeTopicPartitionsRequestCursor? cursor)
    {
        if (cursor is null)
        {
            writer.WriteInt8(-1);
            return;
        }

        writer.WriteInt8(1);
        writer.WriteCompactString(cursor.TopicName);
        writer.WriteInt32(cursor.PartitionIndex);
        writer.WriteEmptyTaggedFields();
    }
}
