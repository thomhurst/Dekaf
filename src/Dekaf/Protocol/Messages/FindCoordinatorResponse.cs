namespace Dekaf.Protocol.Messages;

/// <summary>
/// FindCoordinator response (API key 10).
/// Contains the coordinator node information.
/// </summary>
public sealed class FindCoordinatorResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.FindCoordinator;
    public static short LowestSupportedVersion => 4;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Coordinator responses.
    /// </summary>
    public required IReadOnlyList<Coordinator> Coordinators { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var coordinators = reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => Coordinator.Read(ref r, v), version);

        reader.SkipTaggedFields();

        return new FindCoordinatorResponse
        {
            ThrottleTimeMs = throttleTimeMs,
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
