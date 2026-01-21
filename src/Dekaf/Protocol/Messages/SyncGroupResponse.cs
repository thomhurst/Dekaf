namespace Dekaf.Protocol.Messages;

/// <summary>
/// SyncGroup response (API key 14).
/// Contains the assignment for this member.
/// </summary>
public sealed class SyncGroupResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.SyncGroup;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Error code.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Protocol type (v5+).
    /// </summary>
    public string? ProtocolType { get; init; }

    /// <summary>
    /// Protocol name (v5+).
    /// </summary>
    public string? ProtocolName { get; init; }

    /// <summary>
    /// Assignment data.
    /// </summary>
    public required byte[] Assignment { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;
        var errorCode = (ErrorCode)reader.ReadInt16();

        string? protocolType = null;
        string? protocolName = null;
        if (version >= 5)
        {
            protocolType = reader.ReadCompactString();
            protocolName = reader.ReadCompactString();
        }

        var assignment = isFlexible ? reader.ReadCompactBytes() ?? [] : reader.ReadBytes() ?? [];

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new SyncGroupResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ProtocolType = protocolType,
            ProtocolName = protocolName,
            Assignment = assignment
        };
    }
}
