using System.Text.Json;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Extension methods for integrating Schema Registry with Dekaf builders.
/// </summary>
public static class SchemaRegistryExtensions
{
    /// <summary>
    /// Configures the producer to use JSON Schema Registry serialization for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonSchema">The JSON schema for the value type.</param>
    /// <param name="jsonOptions">Optional JSON serializer options.</param>
    /// <param name="subjectNameStrategy">Subject name strategy.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSerializerOptions? jsonOptions = null,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
    {
        var serializer = new JsonSchemaRegistrySerializer<TValue>(
            schemaRegistry,
            jsonSchema,
            jsonOptions,
            subjectNameStrategy,
            autoRegisterSchemas);

        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the consumer to use JSON Schema Registry deserialization for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The consumer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonOptions">Optional JSON serializer options.</param>
    /// <returns>The builder for chaining.</returns>
    public static ConsumerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        JsonSerializerOptions? jsonOptions = null)
    {
        var deserializer = new JsonSchemaRegistryDeserializer<TValue>(
            schemaRegistry,
            jsonOptions);

        return builder.WithValueDeserializer(deserializer);
    }

    /// <summary>
    /// Configures the producer to use a custom Schema Registry serializer for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Function to serialize the value to bytes (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a subject.</param>
    /// <param name="subjectNameStrategy">Subject name strategy.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Func<TValue, byte[]> serialize,
        Func<string, Schema> getSchema,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
    {
        var serializer = new SchemaRegistrySerializer<TValue>(
            schemaRegistry,
            serialize,
            getSchema,
            subjectNameStrategy,
            autoRegisterSchemas);

        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the producer to use a custom Schema Registry serializer for values
    /// with a custom subject name strategy.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Function to serialize the value to bytes (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a subject.</param>
    /// <param name="customSubjectNameStrategy">Custom subject name strategy implementation.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Func<TValue, byte[]> serialize,
        Func<string, Schema> getSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        bool autoRegisterSchemas = true)
    {
        var serializer = new SchemaRegistrySerializer<TValue>(
            schemaRegistry,
            serialize,
            getSchema,
            customSubjectNameStrategy,
            autoRegisterSchemas);

        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the consumer to use a custom Schema Registry deserializer for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The consumer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <returns>The builder for chaining.</returns>
    public static ConsumerBuilder<TKey, TValue> UseSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Func<byte[], Schema, TValue> deserialize)
    {
        var deserializer = new SchemaRegistryDeserializer<TValue>(
            schemaRegistry,
            deserialize);

        return builder.WithValueDeserializer(deserializer);
    }
}
