namespace Dekaf.Protocol.Messages;

/// <summary>
/// AddRaftVoter request (API key 80).
/// Adds a voter to the KRaft metadata quorum.
/// </summary>
public sealed class AddRaftVoterRequest : IKafkaRequest<AddRaftVoterResponse>
{
    public static ApiKey ApiKey => ApiKey.AddRaftVoter;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    public string? ClusterId { get; init; }
    public int TimeoutMs { get; init; } = 30000;
    public int VoterId { get; init; }
    public Guid VoterDirectoryId { get; init; }
    public required IReadOnlyList<RaftVoterEndpointData> Listeners { get; init; }
    public bool AckWhenCommitted { get; init; } = true;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactNullableString(ClusterId);
        writer.WriteInt32(TimeoutMs);
        writer.WriteInt32(VoterId);
        writer.WriteUuid(VoterDirectoryId);
        writer.WriteCompactArray(
            Listeners,
            static (ref KafkaProtocolWriter w, RaftVoterEndpointData listener) => listener.Write(ref w));

        if (version >= 1)
        {
            writer.WriteBoolean(AckWhenCommitted);
        }

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// AddRaftVoter response (API key 80).
/// </summary>
public sealed class AddRaftVoterResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.AddRaftVoter;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new AddRaftVoterResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// RemoveRaftVoter request (API key 81).
/// Removes a voter from the KRaft metadata quorum.
/// </summary>
public sealed class RemoveRaftVoterRequest : IKafkaRequest<RemoveRaftVoterResponse>
{
    public static ApiKey ApiKey => ApiKey.RemoveRaftVoter;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public string? ClusterId { get; init; }
    public int VoterId { get; init; }
    public Guid VoterDirectoryId { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactNullableString(ClusterId);
        writer.WriteInt32(VoterId);
        writer.WriteUuid(VoterDirectoryId);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// RemoveRaftVoter response (API key 81).
/// </summary>
public sealed class RemoveRaftVoterResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.RemoveRaftVoter;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new RemoveRaftVoterResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}

public sealed class RaftVoterEndpointData
{
    public required string Name { get; init; }
    public required string Host { get; init; }
    public ushort Port { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Name);
        writer.WriteCompactString(Host);
        writer.WriteUInt16(Port);
        writer.WriteEmptyTaggedFields();
    }

    public static RaftVoterEndpointData Read(ref KafkaProtocolReader reader)
    {
        var name = reader.ReadCompactString() ?? string.Empty;
        var host = reader.ReadCompactString() ?? string.Empty;
        var port = reader.ReadUInt16();

        reader.SkipTaggedFields();

        return new RaftVoterEndpointData
        {
            Name = name,
            Host = host,
            Port = port
        };
    }
}
