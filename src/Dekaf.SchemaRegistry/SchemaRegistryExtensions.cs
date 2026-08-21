using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Extension methods for integrating Schema Registry with Dekaf builders.
/// </summary>
public static class SchemaRegistryExtensions
{
    /// <summary>Configures JSON Schema Registry serialization with identity selection and framing.</summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [OverloadResolutionPriority(1)]
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSerializerOptions? jsonOptions,
        JsonSchemaSerializerConfig config)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var serializer = new JsonSchemaRegistrySerializer<TValue>(
            schemaRegistry,
            jsonSchema,
            jsonOptions,
            config);
        return builder.WithValueSerializer(serializer);
    }

    /// <summary>Configures NativeAOT-safe JSON Schema Registry serialization with identity framing.</summary>
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<TValue> jsonTypeInfo,
        JsonSchemaSerializerConfig config)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var serializer = new JsonSchemaRegistrySerializer<TValue>(
            schemaRegistry,
            jsonSchema,
            jsonTypeInfo,
            config);
        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the producer to use JSON Schema Registry serialization for values.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [OverloadResolutionPriority(1)]
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSerializerOptions? jsonOptions = null,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
        => UseJsonSchemaRegistry(
            builder,
            schemaRegistry,
            jsonSchema,
            useLegacySubjectNames: false,
            jsonOptions: jsonOptions,
            subjectNameStrategy: subjectNameStrategy,
            autoRegisterSchemas: autoRegisterSchemas);

    /// <summary>
    /// Configures JSON Schema Registry serialization with payload validation.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [OverloadResolutionPriority(1)]
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSerializerOptions? jsonOptions,
        JsonSchemaValidationOptions validationOptions,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
    {
        var serializer = new JsonSchemaRegistrySerializer<TValue>(
            schemaRegistry,
            jsonSchema,
            jsonOptions,
            validationOptions,
            subjectNameStrategy,
            autoRegisterSchemas);

        return builder.WithValueSerializer(serializer);
    }

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
    /// <param name="useLegacySubjectNames">Whether RecordName and TopicRecordName should use Dekaf's legacy -key/-value suffixes.</param>
    /// <returns>The builder for chaining.</returns>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [OverloadResolutionPriority(1)]
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        bool useLegacySubjectNames,
        JsonSerializerOptions? jsonOptions = null,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
    {
        var serializer = new JsonSchemaRegistrySerializer<TValue>(
            schemaRegistry,
            jsonSchema,
            useLegacySubjectNames,
            jsonOptions,
            subjectNameStrategy,
            autoRegisterSchemas);

        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the producer to use NativeAOT-safe JSON Schema Registry serialization for values.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<TValue> jsonTypeInfo,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
        => UseJsonSchemaRegistry(
            builder,
            schemaRegistry,
            jsonSchema,
            jsonTypeInfo,
            useLegacySubjectNames: false,
            subjectNameStrategy: subjectNameStrategy,
            autoRegisterSchemas: autoRegisterSchemas);

    /// <summary>
    /// Configures NativeAOT-safe JSON Schema Registry serialization with payload validation.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<TValue> jsonTypeInfo,
        JsonSchemaValidationOptions validationOptions,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
    {
        var serializer = new JsonSchemaRegistrySerializer<TValue>(
            schemaRegistry,
            jsonSchema,
            jsonTypeInfo,
            validationOptions,
            subjectNameStrategy,
            autoRegisterSchemas);

        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the producer to use NativeAOT-safe JSON Schema Registry serialization for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonSchema">The JSON schema for the value type.</param>
    /// <param name="jsonTypeInfo">Source-generated metadata for the value type.</param>
    /// <param name="subjectNameStrategy">Subject name strategy.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="useLegacySubjectNames">Whether RecordName and TopicRecordName should use Dekaf's legacy -key/-value suffixes.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<TValue> jsonTypeInfo,
        bool useLegacySubjectNames,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
    {
        var serializer = new JsonSchemaRegistrySerializer<TValue>(
            schemaRegistry,
            jsonSchema,
            jsonTypeInfo,
            useLegacySubjectNames,
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
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON deserialization uses reflection. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON deserialization may require runtime code generation. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [OverloadResolutionPriority(1)]
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

    /// <summary>Configures JSON Schema Registry deserialization with identity framing.</summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON deserialization uses reflection. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON deserialization may require runtime code generation. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [OverloadResolutionPriority(1)]
    public static ConsumerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        JsonSerializerOptions? jsonOptions,
        SchemaRegistryDeserializerConfig config)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var deserializer = new JsonSchemaRegistryDeserializer<TValue>(
            schemaRegistry,
            jsonOptions,
            config);
        return builder.WithValueDeserializer(deserializer);
    }

    /// <summary>
    /// Configures JSON Schema Registry deserialization with payload validation.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON deserialization uses reflection. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON deserialization may require runtime code generation. Use the JsonTypeInfo<TValue> overload for NativeAOT.")]
    [OverloadResolutionPriority(1)]
    public static ConsumerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        JsonSerializerOptions? jsonOptions,
        JsonSchemaValidationOptions validationOptions)
    {
        var deserializer = new JsonSchemaRegistryDeserializer<TValue>(
            schemaRegistry,
            jsonOptions,
            validationOptions);

        return builder.WithValueDeserializer(deserializer);
    }

    /// <summary>
    /// Configures the producer to use a custom Schema Registry serializer for values.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Action<TValue, IBufferWriter<byte>> serialize,
        Func<Schema> getSchema,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
        => UseSchemaRegistry(
            builder,
            schemaRegistry,
            serialize,
            getSchema,
            useLegacySubjectNames: false,
            subjectNameStrategy: subjectNameStrategy,
            autoRegisterSchemas: autoRegisterSchemas);

    /// <summary>
    /// Configures the producer to use a custom Schema Registry serializer for values.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Action<TValue, IBufferWriter<byte>> serialize,
        Func<Schema> getSchema,
        bool useLegacySubjectNames,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
    {
        var serializer = new SchemaRegistrySerializer<TValue>(
            schemaRegistry,
            serialize,
            getSchema,
            useLegacySubjectNames,
            subjectNameStrategy,
            autoRegisterSchemas);

        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the producer to use a custom Schema Registry serializer for values.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Action<TValue, IBufferWriter<byte>> serialize,
        Func<string, Schema> getSchema,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
        => UseSchemaRegistry(
            builder,
            schemaRegistry,
            serialize,
            getSchema,
            useLegacySubjectNames: false,
            subjectNameStrategy: subjectNameStrategy,
            autoRegisterSchemas: autoRegisterSchemas);

    /// <summary>
    /// Configures the consumer to use NativeAOT-safe JSON Schema Registry deserialization for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The consumer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonTypeInfo">Source-generated metadata for the value type.</param>
    /// <returns>The builder for chaining.</returns>
    public static ConsumerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        JsonTypeInfo<TValue> jsonTypeInfo)
    {
        var deserializer = new JsonSchemaRegistryDeserializer<TValue>(
            schemaRegistry,
            jsonTypeInfo);

        return builder.WithValueDeserializer(deserializer);
    }

    /// <summary>Configures NativeAOT-safe JSON Schema Registry deserialization with identity framing.</summary>
    public static ConsumerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        JsonTypeInfo<TValue> jsonTypeInfo,
        SchemaRegistryDeserializerConfig config)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var deserializer = new JsonSchemaRegistryDeserializer<TValue>(
            schemaRegistry,
            jsonTypeInfo,
            config);
        return builder.WithValueDeserializer(deserializer);
    }

    /// <summary>
    /// Configures NativeAOT-safe JSON Schema Registry deserialization with payload validation.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> UseJsonSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        JsonTypeInfo<TValue> jsonTypeInfo,
        JsonSchemaValidationOptions validationOptions)
    {
        var deserializer = new JsonSchemaRegistryDeserializer<TValue>(
            schemaRegistry,
            jsonTypeInfo,
            validationOptions);

        return builder.WithValueDeserializer(deserializer);
    }

    /// <summary>
    /// Configures the producer to use a custom Schema Registry serializer for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Action to serialize the value by writing to the provided buffer (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a subject.</param>
    /// <param name="subjectNameStrategy">Subject name strategy.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="useLegacySubjectNames">Whether RecordName and TopicRecordName should use Dekaf's legacy -key/-value suffixes.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Action<TValue, IBufferWriter<byte>> serialize,
        Func<string, Schema> getSchema,
        bool useLegacySubjectNames,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true)
    {
        var serializer = new SchemaRegistrySerializer<TValue>(
            schemaRegistry,
            serialize,
            getSchema,
            useLegacySubjectNames,
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
    /// <param name="serialize">Action to serialize the value by writing to the provided buffer (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a subject.</param>
    /// <param name="customSubjectNameStrategy">Custom subject name strategy implementation.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Action<TValue, IBufferWriter<byte>> serialize,
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

    /// <summary>
    /// Configures the consumer to use a custom Schema Registry deserializer for values
    /// using a ReadOnlyMemory payload.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The consumer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema without copying the payload.</param>
    /// <returns>The builder for chaining.</returns>
    public static ConsumerBuilder<TKey, TValue> UseSchemaRegistryMemory<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        Func<ReadOnlyMemory<byte>, Schema, TValue> deserialize)
    {
        var deserializer = SchemaRegistryDeserializer.Create(
            schemaRegistry,
            deserialize);

        return builder.WithValueDeserializer(deserializer);
    }
}
