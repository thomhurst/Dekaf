using Avro.Generic;
using Avro.Specific;

namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// Extension methods for integrating Avro Schema Registry serialization with Dekaf builders.
/// </summary>
public static class AvroSchemaRegistryExtensions
{
    /// <summary>
    /// Configures the producer to use Avro Schema Registry serialization for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type. Must be ISpecificRecord or GenericRecord.</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional Avro serializer configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseAvroSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        AvroSerializerConfig? config = null)
        where TValue : class
    {
        ValidateAvroType<TValue>();
        var serializer = new AvroSchemaRegistrySerializer<TValue>(schemaRegistry, config);
        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the producer to use Avro Schema Registry serialization for keys.
    /// </summary>
    /// <typeparam name="TKey">Key type. Must be ISpecificRecord or GenericRecord.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional Avro serializer configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseAvroSchemaRegistryKey<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        AvroSerializerConfig? config = null)
        where TKey : class
    {
        ValidateAvroType<TKey>();
        var serializer = new AvroSchemaRegistrySerializer<TKey>(schemaRegistry, config);
        return builder.WithKeySerializer(serializer);
    }

    /// <summary>
    /// Configures the consumer to use Avro Schema Registry deserialization for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type. Must be ISpecificRecord or GenericRecord.</typeparam>
    /// <param name="builder">The consumer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional Avro deserializer configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static ConsumerBuilder<TKey, TValue> UseAvroSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        AvroDeserializerConfig? config = null)
        where TValue : class
    {
        ValidateAvroType<TValue>();
        var deserializer = new AvroSchemaRegistryDeserializer<TValue>(schemaRegistry, config);
        return builder.WithValueDeserializer(deserializer);
    }

    /// <summary>
    /// Configures the consumer to use Avro Schema Registry deserialization for keys.
    /// </summary>
    /// <typeparam name="TKey">Key type. Must be ISpecificRecord or GenericRecord.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="builder">The consumer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional Avro deserializer configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static ConsumerBuilder<TKey, TValue> UseAvroSchemaRegistryKey<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        AvroDeserializerConfig? config = null)
        where TKey : class
    {
        ValidateAvroType<TKey>();
        var deserializer = new AvroSchemaRegistryDeserializer<TKey>(schemaRegistry, config);
        return builder.WithKeyDeserializer(deserializer);
    }

    private static void ValidateAvroType<T>()
    {
        if (!typeof(ISpecificRecord).IsAssignableFrom(typeof(T)) &&
            !typeof(GenericRecord).IsAssignableFrom(typeof(T)))
        {
            throw new ArgumentException(
                $"Type {typeof(T)} must implement ISpecificRecord or be GenericRecord for Avro serialization.");
        }
    }
}
