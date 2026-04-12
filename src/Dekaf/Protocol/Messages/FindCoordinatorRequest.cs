namespace Dekaf.Protocol.Messages;

/// <summary>
/// FindCoordinator request (API key 10).
/// Finds the coordinator for a group or transaction.
/// </summary>
public sealed class FindCoordinatorRequest : IKafkaRequest<FindCoordinatorResponse>
{
    public static ApiKey ApiKey => ApiKey.FindCoordinator;
    public static short LowestSupportedVersion => 4;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The type of coordinator: 0 = group, 1 = transaction.
    /// </summary>
    public CoordinatorType KeyType { get; init; } = CoordinatorType.Group;

    /// <summary>
    /// The key to look up the coordinator for.
    /// </summary>
    public required string Key { get; init; }

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
    Transaction = 1
}
