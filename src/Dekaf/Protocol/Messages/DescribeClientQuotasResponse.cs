namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeClientQuotas response (API key 48).
/// Contains client quota values for matching entities.
/// </summary>
public sealed class DescribeClientQuotasResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeClientQuotas;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The message-level error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The message-level error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The matching quota entries, or null when the broker returns no entry array.
    /// </summary>
    public IReadOnlyList<DescribeClientQuotasResponseEntry>? Entries { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = version >= 1;
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = ReadString(ref reader, flexible);

        var entries = flexible
            ? reader.ReadCompactNullableArray(
                static (ref KafkaProtocolReader r, short v) => DescribeClientQuotasResponseEntry.Read(ref r, v),
                version)
            : ReadNullableArray(
                ref reader,
                static (ref KafkaProtocolReader r, short v) => DescribeClientQuotasResponseEntry.Read(ref r, v),
                version);

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeClientQuotasResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Entries = entries
        };
    }

    internal static string ReadRequiredString(ref KafkaProtocolReader reader, bool flexible) =>
        flexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

    internal static string? ReadString(ref KafkaProtocolReader reader, bool flexible) =>
        flexible ? reader.ReadCompactString() : reader.ReadString();

    private static T[]? ReadNullableArray<T>(
        ref KafkaProtocolReader reader,
        KafkaProtocolReader.ReadFunc<T, short> readItem,
        short version)
    {
        var length = reader.ReadInt32();
        if (length < 0)
        {
            return null;
        }

        if (length == 0)
        {
            return [];
        }

        var result = new T[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = readItem(ref reader, version);
        }

        return result;
    }
}

/// <summary>
/// Client quota entry in a DescribeClientQuotas response.
/// </summary>
public sealed class DescribeClientQuotasResponseEntry
{
    /// <summary>
    /// Entity components for the quota entry.
    /// </summary>
    public required IReadOnlyList<DescribeClientQuotasEntityData> Entity { get; init; }

    /// <summary>
    /// Quota key/value pairs for the entity.
    /// </summary>
    public required IReadOnlyList<DescribeClientQuotasValueData> Values { get; init; }

    public static DescribeClientQuotasResponseEntry Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = version >= 1;
        var entity = flexible
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => DescribeClientQuotasEntityData.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => DescribeClientQuotasEntityData.Read(ref r, v),
                version);

        var values = flexible
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => DescribeClientQuotasValueData.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => DescribeClientQuotasValueData.Read(ref r, v),
                version);

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeClientQuotasResponseEntry
        {
            Entity = entity,
            Values = values
        };
    }
}

/// <summary>
/// Client quota entity component data.
/// </summary>
public sealed class DescribeClientQuotasEntityData
{
    /// <summary>
    /// Entity type name: user, client-id, or ip.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Entity name, or null for the default entity.
    /// </summary>
    public string? EntityName { get; init; }

    public static DescribeClientQuotasEntityData Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = version >= 1;
        var entityType = DescribeClientQuotasResponse.ReadRequiredString(ref reader, flexible);
        var entityName = DescribeClientQuotasResponse.ReadString(ref reader, flexible);

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeClientQuotasEntityData
        {
            EntityType = entityType,
            EntityName = entityName
        };
    }
}

/// <summary>
/// Client quota value data.
/// </summary>
public sealed class DescribeClientQuotasValueData
{
    /// <summary>
    /// Quota key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Quota value.
    /// </summary>
    public required double Value { get; init; }

    public static DescribeClientQuotasValueData Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = version >= 1;
        var key = DescribeClientQuotasResponse.ReadRequiredString(ref reader, flexible);
        var value = reader.ReadFloat64();

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeClientQuotasValueData
        {
            Key = key,
            Value = value
        };
    }
}
