namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterClientQuotas response (API key 49).
/// Contains per-entity alteration results.
/// </summary>
public sealed class AlterClientQuotasResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.AlterClientQuotas;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Results for each entity alteration.
    /// </summary>
    public required IReadOnlyList<AlterClientQuotasResponseEntry> Entries { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = version >= 1;
        var throttleTimeMs = reader.ReadInt32();
        var entries = flexible
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => AlterClientQuotasResponseEntry.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => AlterClientQuotasResponseEntry.Read(ref r, v),
                version);

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new AlterClientQuotasResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Entries = entries
        };
    }
}

/// <summary>
/// Per-entity result for AlterClientQuotas.
/// </summary>
public sealed class AlterClientQuotasResponseEntry
{
    /// <summary>
    /// Error code for this entity alteration.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Error message for this entity alteration.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Entity components for this result.
    /// </summary>
    public required IReadOnlyList<AlterClientQuotasEntityData> Entity { get; init; }

    public static AlterClientQuotasResponseEntry Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = version >= 1;
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = flexible ? reader.ReadCompactString() : reader.ReadString();
        var entity = flexible
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => ReadEntity(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => ReadEntity(ref r, v),
                version);

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new AlterClientQuotasResponseEntry
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Entity = entity
        };
    }

    private static AlterClientQuotasEntityData ReadEntity(ref KafkaProtocolReader reader, short version)
    {
        var flexible = version >= 1;
        var entityType = flexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;
        var entityName = flexible ? reader.ReadCompactString() : reader.ReadString();

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new AlterClientQuotasEntityData
        {
            EntityType = entityType,
            EntityName = entityName
        };
    }
}
