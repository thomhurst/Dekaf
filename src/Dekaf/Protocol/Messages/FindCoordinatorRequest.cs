using System.Globalization;

namespace Dekaf.Protocol.Messages;

/// <summary>
/// FindCoordinator request (API key 10).
/// Finds the coordinator for a group or transaction.
/// </summary>
public sealed class FindCoordinatorRequest : IKafkaRequest<FindCoordinatorResponse>
{
    public static ApiKey ApiKey => ApiKey.FindCoordinator;
    public static short LowestSupportedVersion => 4;
    public static short HighestSupportedVersion => 6;

    /// <summary>
    /// The type of coordinator: 0 = group, 1 = transaction, 2 = share partition.
    /// </summary>
    public CoordinatorType KeyType { get; init; } = CoordinatorType.Group;

    /// <summary>
    /// The key to look up the coordinator for.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Creates a KIP-932 share-partition coordinator request from typed key parts.
    /// </summary>
    public static FindCoordinatorRequest ForSharePartition(string groupId, Guid topicId, int partition) =>
        new()
        {
            KeyType = CoordinatorType.Share,
            Key = BuildShareCoordinatorKey(groupId, topicId, partition)
        };

    /// <summary>
    /// Builds the KIP-932 coordinator key for a share-group partition.
    /// </summary>
    public static string BuildShareCoordinatorKey(string groupId, Guid topicId, int partition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        if (topicId == Guid.Empty)
            throw new ArgumentException("A share coordinator key requires a non-empty topic ID.", nameof(topicId));
        ArgumentOutOfRangeException.ThrowIfNegative(partition);

        var topicIdBytes = new byte[16];
        KafkaGuid.WriteBigEndian(topicId, topicIdBytes);
        var encodedTopicId = Convert.ToBase64String(topicIdBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return string.Concat(
            groupId,
            ":",
            encodedTopicId,
            ":",
            partition.ToString(CultureInfo.InvariantCulture));
    }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt8((sbyte)KeyType);
        // Compact array with a single element: length = count + 1 = 2
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString(Key);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Type of coordinator.
/// </summary>
public enum CoordinatorType : sbyte
{
    Group = 0,
    Transaction = 1,
    Share = 2
}
