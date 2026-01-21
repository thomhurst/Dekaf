namespace Dekaf.Protocol.Messages;

/// <summary>
/// FindCoordinator request (API key 10).
/// Finds the coordinator for a group or transaction.
/// </summary>
public sealed class FindCoordinatorRequest : IKafkaRequest<FindCoordinatorResponse>
{
    public static ApiKey ApiKey => ApiKey.FindCoordinator;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The key to find coordinator for (group ID or transactional ID).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The type of coordinator: 0 = group, 1 = transaction.
    /// </summary>
    public CoordinatorType KeyType { get; init; } = CoordinatorType.Group;

    /// <summary>
    /// Multiple keys to look up (v4+).
    /// </summary>
    public IReadOnlyList<string>? CoordinatorKeys { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 3;
    public static short GetRequestHeaderVersion(short version) => version >= 3 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 3 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 3;

        if (version < 4)
        {
            if (isFlexible)
                writer.WriteCompactString(Key);
            else
                writer.WriteString(Key);
        }

        if (version >= 1)
        {
            writer.WriteInt8((sbyte)KeyType);
        }

        if (version >= 4)
        {
            var keys = CoordinatorKeys ?? [Key];
            writer.WriteCompactArray(
                keys.ToArray().AsSpan(),
                (ref KafkaProtocolWriter w, string k) => w.WriteCompactString(k));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
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
