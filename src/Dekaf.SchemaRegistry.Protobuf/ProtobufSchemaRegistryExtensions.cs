using Google.Protobuf;

namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Extension methods for integrating Protobuf Schema Registry serialization with Dekaf builders.
/// </summary>
public static class ProtobufSchemaRegistryExtensions
{
    /// <summary>
    /// Configures the producer to use Protobuf Schema Registry serialization for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type (must implement IMessage).</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional serializer configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseProtobufSchemaRegistry<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        ProtobufSerializerConfig? config = null)
        where TValue : IMessage<TValue>
    {
        var serializer = new ProtobufSchemaRegistrySerializer<TValue>(
            schemaRegistry,
            config);

        return builder.WithValueSerializer(serializer);
    }

    /// <summary>
    /// Configures the producer to use Protobuf Schema Registry serialization for both keys and values.
    /// </summary>
    /// <typeparam name="TKey">Key type (must implement IMessage).</typeparam>
    /// <typeparam name="TValue">Value type (must implement IMessage).</typeparam>
    /// <param name="builder">The producer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="keyConfig">Optional key serializer configuration.</param>
    /// <param name="valueConfig">Optional value serializer configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static ProducerBuilder<TKey, TValue> UseProtobufSchemaRegistryForKeyAndValue<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        ProtobufSerializerConfig? keyConfig = null,
        ProtobufSerializerConfig? valueConfig = null)
        where TKey : IMessage<TKey>
        where TValue : IMessage<TValue>
    {
        var keySerializer = new ProtobufSchemaRegistrySerializer<TKey>(schemaRegistry, keyConfig);
        var valueSerializer = new ProtobufSchemaRegistrySerializer<TValue>(schemaRegistry, valueConfig);

        return builder
            .WithKeySerializer(keySerializer)
            .WithValueSerializer(valueSerializer);
    }

    /// <summary>
    /// Configures the consumer to use Protobuf Schema Registry deserialization for values.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type (must implement IMessage and have a parameterless constructor).</typeparam>
    /// <param name="builder">The consumer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional deserializer configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static ConsumerBuilder<TKey, TValue> UseProtobufSchemaRegistry<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        ProtobufDeserializerConfig? config = null)
        where TValue : IMessage<TValue>, new()
    {
        var deserializer = new ProtobufSchemaRegistryDeserializer<TValue>(
            schemaRegistry,
            config);

        return builder.WithValueDeserializer(deserializer);
    }

    /// <summary>
    /// Configures the consumer to use Protobuf Schema Registry deserialization for both keys and values.
    /// </summary>
    /// <typeparam name="TKey">Key type (must implement IMessage and have a parameterless constructor).</typeparam>
    /// <typeparam name="TValue">Value type (must implement IMessage and have a parameterless constructor).</typeparam>
    /// <param name="builder">The consumer builder.</param>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="keyConfig">Optional key deserializer configuration.</param>
    /// <param name="valueConfig">Optional value deserializer configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static ConsumerBuilder<TKey, TValue> UseProtobufSchemaRegistryForKeyAndValue<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        ISchemaRegistryClient schemaRegistry,
        ProtobufDeserializerConfig? keyConfig = null,
        ProtobufDeserializerConfig? valueConfig = null)
        where TKey : IMessage<TKey>, new()
        where TValue : IMessage<TValue>, new()
    {
        var keyDeserializer = new ProtobufSchemaRegistryDeserializer<TKey>(schemaRegistry, keyConfig);
        var valueDeserializer = new ProtobufSchemaRegistryDeserializer<TValue>(schemaRegistry, valueConfig);

        return builder
            .WithKeyDeserializer(keyDeserializer)
            .WithValueDeserializer(valueDeserializer);
    }
}
