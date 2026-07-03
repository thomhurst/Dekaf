namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterClientQuotas request (API key 49).
/// Alters client quota values.
/// </summary>
public sealed class AlterClientQuotasRequest : IKafkaRequest<AlterClientQuotasResponse>
{
    public static ApiKey ApiKey => ApiKey.AlterClientQuotas;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// Quota entries to alter.
    /// </summary>
    public required IReadOnlyList<AlterClientQuotasRequestEntry> Entries { get; init; }

    /// <summary>
    /// True if the broker should validate the request but not change quotas.
    /// </summary>
    public bool ValidateOnly { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 1;
    public static short GetRequestHeaderVersion(short version) => version >= 1 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 1 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (IsFlexibleVersion(version))
        {
            writer.WriteCompactArray(
                Entries,
                static (ref KafkaProtocolWriter w, AlterClientQuotasRequestEntry e, short v) => e.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Entries,
                static (ref KafkaProtocolWriter w, AlterClientQuotasRequestEntry e, short v) => e.Write(ref w, v),
                version);
        }

        writer.WriteBoolean(ValidateOnly);

        if (IsFlexibleVersion(version))
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Client quota alteration entry.
/// </summary>
public sealed class AlterClientQuotasRequestEntry
{
    /// <summary>
    /// Entity components to alter.
    /// </summary>
    public required IReadOnlyList<AlterClientQuotasEntityData> Entity { get; init; }

    /// <summary>
    /// Operations to apply to the entity.
    /// </summary>
    public required IReadOnlyList<AlterClientQuotasOpData> Ops { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (AlterClientQuotasRequest.IsFlexibleVersion(version))
        {
            writer.WriteCompactArray(
                Entity,
                static (ref KafkaProtocolWriter w, AlterClientQuotasEntityData e, short v) => e.Write(ref w, v),
                version);
            writer.WriteCompactArray(
                Ops,
                static (ref KafkaProtocolWriter w, AlterClientQuotasOpData o, short v) => o.Write(ref w, v),
                version);
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteArray(
            Entity,
            static (ref KafkaProtocolWriter w, AlterClientQuotasEntityData e, short v) => e.Write(ref w, v),
            version);
        writer.WriteArray(
            Ops,
            static (ref KafkaProtocolWriter w, AlterClientQuotasOpData o, short v) => o.Write(ref w, v),
            version);
    }
}

/// <summary>
/// Client quota entity component data for AlterClientQuotas.
/// </summary>
public sealed class AlterClientQuotasEntityData
{
    /// <summary>
    /// Entity type name: user, client-id, or ip.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Entity name, or null for the default entity.
    /// </summary>
    public string? EntityName { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (AlterClientQuotasRequest.IsFlexibleVersion(version))
        {
            writer.WriteCompactString(EntityType);
            writer.WriteCompactNullableString(EntityName);
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteString(EntityType);
        writer.WriteString(EntityName);
    }
}

/// <summary>
/// Client quota operation data for AlterClientQuotas.
/// </summary>
public sealed class AlterClientQuotasOpData
{
    /// <summary>
    /// Quota key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Quota value for set operations.
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// True to remove this quota key.
    /// </summary>
    public bool Remove { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (AlterClientQuotasRequest.IsFlexibleVersion(version))
        {
            writer.WriteCompactString(Key);
            writer.WriteFloat64(Value);
            writer.WriteBoolean(Remove);
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteString(Key);
        writer.WriteFloat64(Value);
        writer.WriteBoolean(Remove);
    }
}
