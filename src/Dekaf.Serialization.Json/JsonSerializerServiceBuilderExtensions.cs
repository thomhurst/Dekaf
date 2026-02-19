using System.Text.Json;
using Dekaf.Extensions.DependencyInjection;

namespace Dekaf.Serialization.Json;

/// <summary>
/// Extension methods for configuring JSON serialization on DI service builders.
/// </summary>
public static class JsonSerializerServiceBuilderExtensions
{
    /// <summary>
    /// Configures the producer to use JSON serialization for values.
    /// </summary>
    /// <param name="builder">The producer service builder.</param>
    /// <param name="options">Optional JSON serializer options. When null, defaults to camelCase naming with no indentation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static ProducerServiceBuilder<TKey, TValue> UseJsonValueSerializer<TKey, TValue>(
        this ProducerServiceBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithValueSerializer(new JsonSerializer<TValue>(options));
    }

    /// <summary>
    /// Configures the producer to use JSON serialization for keys.
    /// </summary>
    /// <param name="builder">The producer service builder.</param>
    /// <param name="options">Optional JSON serializer options. When null, defaults to camelCase naming with no indentation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static ProducerServiceBuilder<TKey, TValue> UseJsonKeySerializer<TKey, TValue>(
        this ProducerServiceBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithKeySerializer(new JsonSerializer<TKey>(options));
    }

    /// <summary>
    /// Configures the consumer to use JSON deserialization for values.
    /// </summary>
    /// <param name="builder">The consumer service builder.</param>
    /// <param name="options">Optional JSON serializer options. When null, defaults to camelCase naming with no indentation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static ConsumerServiceBuilder<TKey, TValue> UseJsonValueDeserializer<TKey, TValue>(
        this ConsumerServiceBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithValueDeserializer(new JsonSerializer<TValue>(options));
    }

    /// <summary>
    /// Configures the consumer to use JSON deserialization for keys.
    /// </summary>
    /// <param name="builder">The consumer service builder.</param>
    /// <param name="options">Optional JSON serializer options. When null, defaults to camelCase naming with no indentation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static ConsumerServiceBuilder<TKey, TValue> UseJsonKeyDeserializer<TKey, TValue>(
        this ConsumerServiceBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithKeyDeserializer(new JsonSerializer<TKey>(options));
    }
}
