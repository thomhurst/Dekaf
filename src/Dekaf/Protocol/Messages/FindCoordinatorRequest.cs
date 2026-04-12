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
    /// The keys to look up coordinators for.
    /// </summary>
    public required IReadOnlyList<string> CoordinatorKeys { get; init; }

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt8((sbyte)KeyType);
        writer.WriteCompactArray(
            CoordinatorKeys,
            (ref KafkaProtocolWriter w, string k) => w.WriteCompactString(k));
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
