namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeConfigs response (API key 32).
/// Contains the configuration for the requested resources.
/// </summary>
public sealed class DescribeConfigsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeConfigs;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The results for each resource.
    /// </summary>
    public required IReadOnlyList<DescribeConfigsResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<DescribeConfigsResult> results;
        if (isFlexible)
        {
            results = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DescribeConfigsResult.Read(ref r, version)) ?? [];
        }
        else
        {
            results = reader.ReadArray(
                (ref KafkaProtocolReader r) => DescribeConfigsResult.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeConfigsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Results = results
        };
    }
}

/// <summary>
/// Per-resource result for DescribeConfigs.
/// </summary>
public sealed class DescribeConfigsResult
{
    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The resource type.
    /// </summary>
    public sbyte ResourceType { get; init; }

    /// <summary>
    /// The resource name.
    /// </summary>
    public required string ResourceName { get; init; }

    /// <summary>
    /// Each listed configuration.
    /// </summary>
    public required IReadOnlyList<DescribeConfigsResourceResult> Configs { get; init; }

    public static DescribeConfigsResult Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage;
        if (isFlexible)
        {
            errorMessage = reader.ReadCompactString();
        }
        else
        {
            errorMessage = reader.ReadString();
        }

        var resourceType = reader.ReadInt8();

        string resourceName;
        if (isFlexible)
        {
            resourceName = reader.ReadCompactString() ?? string.Empty;
        }
        else
        {
            resourceName = reader.ReadString() ?? string.Empty;
        }

        IReadOnlyList<DescribeConfigsResourceResult> configs;
        if (isFlexible)
        {
            configs = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DescribeConfigsResourceResult.Read(ref r, version)) ?? [];
        }
        else
        {
            configs = reader.ReadArray(
                (ref KafkaProtocolReader r) => DescribeConfigsResourceResult.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeConfigsResult
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ResourceType = resourceType,
            ResourceName = resourceName,
            Configs = configs
        };
    }
}

/// <summary>
/// A configuration entry in the DescribeConfigs response.
/// </summary>
public sealed class DescribeConfigsResourceResult
{
    /// <summary>
    /// The configuration name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The configuration value.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// True if the configuration is read-only.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// True if the configuration is not set (v0 only, deprecated).
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// The configuration source (v1+).
    /// </summary>
    public sbyte ConfigSource { get; init; } = -1;

    /// <summary>
    /// True if this configuration is sensitive.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// The synonyms for this configuration key (v1+).
    /// </summary>
    public IReadOnlyList<DescribeConfigsSynonym>? Synonyms { get; init; }

    /// <summary>
    /// The configuration data type (v3+).
    /// </summary>
    public sbyte ConfigType { get; init; }

    /// <summary>
    /// The configuration documentation (v3+).
    /// </summary>
    public string? Documentation { get; init; }

    public static DescribeConfigsResourceResult Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        string name;
        if (isFlexible)
        {
            name = reader.ReadCompactString() ?? string.Empty;
        }
        else
        {
            name = reader.ReadString() ?? string.Empty;
        }

        string? value;
        if (isFlexible)
        {
            value = reader.ReadCompactString();
        }
        else
        {
            value = reader.ReadString();
        }

        var readOnly = reader.ReadBoolean();

        // v0 has IsDefault; v1+ uses ConfigSource instead
        var isDefault = version == 0 && reader.ReadBoolean();

        sbyte configSource = -1;
        if (version >= 1)
        {
            configSource = reader.ReadInt8();
        }

        var isSensitive = reader.ReadBoolean();

        IReadOnlyList<DescribeConfigsSynonym>? synonyms = null;
        if (version >= 1)
        {
            if (isFlexible)
            {
                synonyms = reader.ReadCompactArray(
                    (ref KafkaProtocolReader r) => DescribeConfigsSynonym.Read(ref r, version));
            }
            else
            {
                synonyms = reader.ReadArray(
                    (ref KafkaProtocolReader r) => DescribeConfigsSynonym.Read(ref r, version));
            }
        }

        sbyte configType = 0;
        string? documentation = null;
        if (version >= 3)
        {
            configType = reader.ReadInt8();
            if (isFlexible)
            {
                documentation = reader.ReadCompactString();
            }
            else
            {
                documentation = reader.ReadString();
            }
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeConfigsResourceResult
        {
            Name = name,
            Value = value,
            ReadOnly = readOnly,
            IsDefault = isDefault,
            ConfigSource = configSource,
            IsSensitive = isSensitive,
            Synonyms = synonyms,
            ConfigType = configType,
            Documentation = documentation
        };
    }
}

/// <summary>
/// A configuration synonym in the DescribeConfigs response.
/// </summary>
public sealed class DescribeConfigsSynonym
{
    /// <summary>
    /// The synonym name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The synonym value.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// The source of this configuration.
    /// </summary>
    public sbyte Source { get; init; }

    public static DescribeConfigsSynonym Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        string name;
        string? value;

        if (isFlexible)
        {
            name = reader.ReadCompactString() ?? string.Empty;
            value = reader.ReadCompactString();
        }
        else
        {
            name = reader.ReadString() ?? string.Empty;
            value = reader.ReadString();
        }

        var source = reader.ReadInt8();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeConfigsSynonym
        {
            Name = name,
            Value = value,
            Source = source
        };
    }
}
