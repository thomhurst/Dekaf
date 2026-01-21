namespace Dekaf.Protocol.Messages;

/// <summary>
/// FindCoordinator response (API key 10).
/// Contains the coordinator node information.
/// </summary>
public sealed class FindCoordinatorResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.FindCoordinator;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Error code (v0-v3).
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Error message (v1-v3).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Coordinator node ID (v0-v3).
    /// </summary>
    public int NodeId { get; init; }

    /// <summary>
    /// Coordinator host (v0-v3).
    /// </summary>
    public string? Host { get; init; }

    /// <summary>
    /// Coordinator port (v0-v3).
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Coordinator responses (v4+).
    /// </summary>
    public IReadOnlyList<Coordinator>? Coordinators { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;

        ErrorCode errorCode = ErrorCode.None;
        string? errorMessage = null;
        var nodeId = -1;
        string? host = null;
        var port = -1;
        IReadOnlyList<Coordinator>? coordinators = null;

        if (version < 4)
        {
            errorCode = (ErrorCode)reader.ReadInt16();

            if (version >= 1)
            {
                errorMessage = isFlexible ? reader.ReadCompactString() : reader.ReadString();
            }

            nodeId = reader.ReadInt32();
            host = isFlexible ? reader.ReadCompactString() : reader.ReadString();
            port = reader.ReadInt32();
        }
        else
        {
            coordinators = reader.ReadCompactArray((ref KafkaProtocolReader r) => Coordinator.Read(ref r, version));
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new FindCoordinatorResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            NodeId = nodeId,
            Host = host,
            Port = port,
            Coordinators = coordinators
        };
    }
}

/// <summary>
/// Coordinator information (v4+).
/// </summary>
public sealed class Coordinator
{
    public required string Key { get; init; }
    public required int NodeId { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static Coordinator Read(ref KafkaProtocolReader reader, short version)
    {
        var key = reader.ReadCompactNonNullableString();
        var nodeId = reader.ReadInt32();
        var host = reader.ReadCompactNonNullableString();
        var port = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new Coordinator
        {
            Key = key,
            NodeId = nodeId,
            Host = host,
            Port = port,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
