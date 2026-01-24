namespace Dekaf.Admin;

/// <summary>
/// Type of configuration resource.
/// </summary>
public enum ConfigResourceType : sbyte
{
    /// <summary>
    /// Unknown resource type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Topic configuration.
    /// </summary>
    Topic = 2,

    /// <summary>
    /// Broker configuration.
    /// </summary>
    Broker = 4,

    /// <summary>
    /// Broker logger configuration.
    /// </summary>
    BrokerLogger = 8
}

/// <summary>
/// Source of a configuration value.
/// </summary>
public enum ConfigSource : sbyte
{
    /// <summary>
    /// Unknown source.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The configuration is a dynamic topic config that is configured for a specific topic.
    /// </summary>
    DynamicTopicConfig = 1,

    /// <summary>
    /// The configuration is a dynamic broker config that is configured for a specific broker.
    /// </summary>
    DynamicBrokerConfig = 2,

    /// <summary>
    /// The configuration is a dynamic broker config that is configured as default for all brokers.
    /// </summary>
    DynamicDefaultBrokerConfig = 3,

    /// <summary>
    /// The configuration is a static broker config provided as broker properties at start up.
    /// </summary>
    StaticBrokerConfig = 4,

    /// <summary>
    /// The configuration is a built-in default value.
    /// </summary>
    DefaultConfig = 5,

    /// <summary>
    /// The configuration is a dynamic broker logger config.
    /// </summary>
    DynamicBrokerLoggerConfig = 6
}

/// <summary>
/// Operation type for incremental config alterations.
/// </summary>
public enum ConfigAlterOperation : sbyte
{
    /// <summary>
    /// Set the configuration value.
    /// </summary>
    Set = 0,

    /// <summary>
    /// Delete/unset the configuration value.
    /// </summary>
    Delete = 1,

    /// <summary>
    /// Append to a list configuration value.
    /// </summary>
    Append = 2,

    /// <summary>
    /// Subtract from a list configuration value.
    /// </summary>
    Subtract = 3
}

/// <summary>
/// Identifies a Kafka configuration resource (topic, broker, etc.).
/// </summary>
public sealed class ConfigResource : IEquatable<ConfigResource>
{
    /// <summary>
    /// The type of resource.
    /// </summary>
    public required ConfigResourceType Type { get; init; }

    /// <summary>
    /// The name of the resource. For topics, this is the topic name.
    /// For brokers, this is the broker ID as a string, or empty for cluster-wide config.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Creates a ConfigResource for a topic.
    /// </summary>
    public static ConfigResource Topic(string topicName) =>
        new() { Type = ConfigResourceType.Topic, Name = topicName };

    /// <summary>
    /// Creates a ConfigResource for a specific broker.
    /// </summary>
    public static ConfigResource Broker(int brokerId) =>
        new() { Type = ConfigResourceType.Broker, Name = brokerId.ToString() };

    /// <summary>
    /// Creates a ConfigResource for cluster-wide broker configuration.
    /// </summary>
    public static ConfigResource ClusterBroker() =>
        new() { Type = ConfigResourceType.Broker, Name = string.Empty };

    /// <summary>
    /// Creates a ConfigResource for a broker logger.
    /// </summary>
    public static ConfigResource BrokerLogger(int brokerId) =>
        new() { Type = ConfigResourceType.BrokerLogger, Name = brokerId.ToString() };

    public bool Equals(ConfigResource? other)
    {
        if (other is null) return false;
        return Type == other.Type && Name == other.Name;
    }

    public override bool Equals(object? obj) => Equals(obj as ConfigResource);

    public override int GetHashCode() => HashCode.Combine(Type, Name);

    public override string ToString() => $"{Type}:{Name}";
}

/// <summary>
/// A configuration entry returned by DescribeConfigs.
/// </summary>
public sealed class ConfigEntry
{
    /// <summary>
    /// The configuration name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The configuration value, or null if sensitive.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Whether the config value is read-only.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Whether the config value is set to a non-default value.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Whether the config value is sensitive.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// The source of this configuration entry.
    /// </summary>
    public ConfigSource Source { get; init; } = ConfigSource.Unknown;

    /// <summary>
    /// Configuration synonyms (if requested).
    /// </summary>
    public IReadOnlyList<ConfigSynonym>? Synonyms { get; init; }

    /// <summary>
    /// The documentation for this config (if requested).
    /// </summary>
    public string? Documentation { get; init; }
}

/// <summary>
/// A synonym for a configuration entry.
/// </summary>
public sealed class ConfigSynonym
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
    /// The source of this synonym.
    /// </summary>
    public ConfigSource Source { get; init; } = ConfigSource.Unknown;
}

/// <summary>
/// A configuration alteration for IncrementalAlterConfigs.
/// </summary>
public sealed class ConfigAlter
{
    /// <summary>
    /// The configuration name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The configuration value, or null for delete operation.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// The operation to perform.
    /// </summary>
    public ConfigAlterOperation Operation { get; init; } = ConfigAlterOperation.Set;

    /// <summary>
    /// Creates a Set operation.
    /// </summary>
    public static ConfigAlter Set(string name, string? value) =>
        new() { Name = name, Value = value, Operation = ConfigAlterOperation.Set };

    /// <summary>
    /// Creates a Delete operation.
    /// </summary>
    public static ConfigAlter Delete(string name) =>
        new() { Name = name, Value = null, Operation = ConfigAlterOperation.Delete };

    /// <summary>
    /// Creates an Append operation.
    /// </summary>
    public static ConfigAlter Append(string name, string value) =>
        new() { Name = name, Value = value, Operation = ConfigAlterOperation.Append };

    /// <summary>
    /// Creates a Subtract operation.
    /// </summary>
    public static ConfigAlter Subtract(string name, string value) =>
        new() { Name = name, Value = value, Operation = ConfigAlterOperation.Subtract };
}

/// <summary>
/// Options for DescribeConfigs.
/// </summary>
public sealed class DescribeConfigsOptions
{
    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Whether to include synonyms in the response.
    /// </summary>
    public bool IncludeSynonyms { get; init; }

    /// <summary>
    /// Whether to include documentation in the response.
    /// </summary>
    public bool IncludeDocumentation { get; init; }
}

/// <summary>
/// Options for AlterConfigs.
/// </summary>
public sealed class AlterConfigsOptions
{
    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// If true, the broker will validate the request, but not change the configuration.
    /// </summary>
    public bool ValidateOnly { get; init; }
}

/// <summary>
/// Options for IncrementalAlterConfigs.
/// </summary>
public sealed class IncrementalAlterConfigsOptions
{
    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// If true, the broker will validate the request, but not change the configuration.
    /// </summary>
    public bool ValidateOnly { get; init; }
}
