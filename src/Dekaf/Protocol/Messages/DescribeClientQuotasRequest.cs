namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeClientQuotas request (API key 48).
/// Describes client quotas matching a filter.
/// </summary>
public sealed class DescribeClientQuotasRequest : IKafkaRequest<DescribeClientQuotasResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeClientQuotas;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// Entity filter components.
    /// </summary>
    public required IReadOnlyList<DescribeClientQuotasRequestComponent> Components { get; init; }

    /// <summary>
    /// True if only entities with exactly these components should match.
    /// </summary>
    public bool Strict { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 1;
    public static short GetRequestHeaderVersion(short version) => version >= 1 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 1 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (IsFlexibleVersion(version))
        {
            writer.WriteCompactArray(
                Components,
                static (ref KafkaProtocolWriter w, DescribeClientQuotasRequestComponent c, short v) => c.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Components,
                static (ref KafkaProtocolWriter w, DescribeClientQuotasRequestComponent c, short v) => c.Write(ref w, v),
                version);
        }

        writer.WriteBoolean(Strict);

        if (IsFlexibleVersion(version))
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Filter component for DescribeClientQuotas request.
/// </summary>
public sealed class DescribeClientQuotasRequestComponent
{
    /// <summary>
    /// Entity type name: user, client-id, or ip.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Match type: 0 exact, 1 default, 2 any specified.
    /// </summary>
    public required sbyte MatchType { get; init; }

    /// <summary>
    /// Entity name for exact matches, otherwise null.
    /// </summary>
    public string? Match { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (DescribeClientQuotasRequest.IsFlexibleVersion(version))
        {
            writer.WriteCompactString(EntityType);
            writer.WriteInt8(MatchType);
            writer.WriteCompactNullableString(Match);
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteString(EntityType);
        writer.WriteInt8(MatchType);
        writer.WriteString(Match);
    }
}
