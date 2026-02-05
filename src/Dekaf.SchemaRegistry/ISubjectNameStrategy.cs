namespace Dekaf.SchemaRegistry;

/// <summary>
/// Interface for determining the subject name under which a schema is registered
/// in the Schema Registry.
/// </summary>
/// <remarks>
/// Implement this interface to create custom subject name strategies. For standard
/// strategies, use the <see cref="SubjectNameStrategy"/> enum with built-in implementations
/// available via <see cref="SubjectNameStrategies"/>.
/// </remarks>
public interface ISubjectNameStrategy
{
    /// <summary>
    /// Gets the subject name for a given topic, record type, and key/value component.
    /// </summary>
    /// <param name="topic">The Kafka topic name.</param>
    /// <param name="recordType">The fully-qualified record type name, or null if unavailable.</param>
    /// <param name="isKey">True if this is for the key component, false for value.</param>
    /// <returns>The subject name to use for schema registration and lookup.</returns>
    string GetSubjectName(string topic, string? recordType, bool isKey);
}

/// <summary>
/// Provides built-in <see cref="ISubjectNameStrategy"/> implementations for standard strategies.
/// </summary>
public static class SubjectNameStrategies
{
    /// <summary>
    /// Gets a strategy that uses the topic name with a -key or -value suffix.
    /// Subject format: {topic}-key or {topic}-value.
    /// </summary>
    public static ISubjectNameStrategy Topic { get; } = new TopicNameStrategy();

    /// <summary>
    /// Gets a strategy that uses the fully-qualified record type name.
    /// Subject format: {recordType}.
    /// </summary>
    public static ISubjectNameStrategy Record { get; } = new RecordNameStrategy();

    /// <summary>
    /// Gets a strategy that combines the topic name with the record type name.
    /// Subject format: {topic}-{recordType}.
    /// </summary>
    public static ISubjectNameStrategy TopicRecord { get; } = new TopicRecordNameStrategy();
}

/// <summary>
/// Subject name strategy that uses the topic name with a -key or -value suffix.
/// This is the default strategy used by Confluent Schema Registry.
/// </summary>
/// <remarks>
/// Subject format: {topic}-key or {topic}-value.
/// </remarks>
public sealed class TopicNameStrategy : ISubjectNameStrategy
{
    /// <inheritdoc />
    public string GetSubjectName(string topic, string? recordType, bool isKey)
    {
        return $"{topic}-{(isKey ? "key" : "value")}";
    }
}

/// <summary>
/// Subject name strategy that uses the fully-qualified record type name.
/// This is useful for multi-schema-per-topic scenarios where different record types
/// are produced to the same topic.
/// </summary>
/// <remarks>
/// Subject format: {recordType}.
/// Throws <see cref="InvalidOperationException"/> if the record type is not available.
/// </remarks>
public sealed class RecordNameStrategy : ISubjectNameStrategy
{
    /// <inheritdoc />
    public string GetSubjectName(string topic, string? recordType, bool isKey)
    {
        if (string.IsNullOrEmpty(recordType))
        {
            throw new InvalidOperationException(
                "RecordNameStrategy requires a record type name, but none was available. " +
                "Ensure the serialized type has a discoverable fully-qualified name.");
        }

        return recordType;
    }
}

/// <summary>
/// Subject name strategy that combines the topic name with the record type name.
/// This provides topic-level isolation while supporting multiple record types per topic.
/// </summary>
/// <remarks>
/// Subject format: {topic}-{recordType}.
/// Throws <see cref="InvalidOperationException"/> if the record type is not available.
/// </remarks>
public sealed class TopicRecordNameStrategy : ISubjectNameStrategy
{
    /// <inheritdoc />
    public string GetSubjectName(string topic, string? recordType, bool isKey)
    {
        if (string.IsNullOrEmpty(recordType))
        {
            throw new InvalidOperationException(
                "TopicRecordNameStrategy requires a record type name, but none was available. " +
                "Ensure the serialized type has a discoverable fully-qualified name.");
        }

        return $"{topic}-{recordType}";
    }
}
