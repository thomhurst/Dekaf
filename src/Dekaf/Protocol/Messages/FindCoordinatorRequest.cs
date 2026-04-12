namespace Dekaf.Protocol.Messages;

/// <summary>
/// FindCoordinator request (API key 10).
/// Finds the coordinator for a group or transaction.
/// </summary>
public sealed class FindCoordinatorRequest : IKafkaRequest<FindCoordinatorResponse>
{
    public static ApiKey ApiKey => ApiKey.FindCoordinator;
    public static short LowestSupportedVersion => 3;
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

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version < 4)
        {
            writer.WriteCompactString(Key);
        }

        writer.WriteInt8((sbyte)KeyType);

        if (version >= 4)
        {
            var keys = CoordinatorKeys ?? [Key];
            writer.WriteCompactArray(
                keys,
                (ref KafkaProtocolWriter w, string k) => w.WriteCompactString(k));
        }

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
