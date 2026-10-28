namespace Dekaf.Protocol.Messages;

/// <summary>
/// StreamsGroupTopologyDescriptionUpdate request (API key 93, KIP-1331).
/// Publishes a Streams member's processing-graph description for the group's current topology epoch.
/// </summary>
public sealed class StreamsGroupTopologyDescriptionUpdateRequest
    : IKafkaRequest<StreamsGroupTopologyDescriptionUpdateResponse>
{
    public static ApiKey ApiKey => ApiKey.StreamsGroupTopologyDescriptionUpdate;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public required string GroupId { get; init; }
    public required string MemberId { get; init; }
    public int TopologyEpoch { get; init; }
    public required StreamsTopologyDescriptionData TopologyDescription { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(MemberId);
        writer.WriteInt32(TopologyEpoch);
        TopologyDescription.Write(ref writer);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// StreamsGroupTopologyDescriptionUpdate response (API key 93, KIP-1331).
/// </summary>
public sealed class StreamsGroupTopologyDescriptionUpdateResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.StreamsGroupTopologyDescriptionUpdate;
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

        return new StreamsGroupTopologyDescriptionUpdateResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
