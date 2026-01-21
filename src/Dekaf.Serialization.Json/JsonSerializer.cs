using System.Buffers;
using System.Text.Json;
using Dekaf.Serialization;

namespace Dekaf.Serialization.Json;

/// <summary>
/// JSON serializer using System.Text.Json.
/// </summary>
/// <typeparam name="T">Type to serialize.</typeparam>
public sealed class JsonSerializer<T> : ISerde<T>
{
    private readonly JsonSerializerOptions _options;

    public JsonSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public void Serialize(T value, IBufferWriter<byte> destination, SerializationContext context)
    {
        using var writer = new Utf8JsonWriter(destination);
        System.Text.Json.JsonSerializer.Serialize(writer, value, _options);
    }

    public T Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        var reader = new Utf8JsonReader(data);
        return System.Text.Json.JsonSerializer.Deserialize<T>(ref reader, _options)!;
    }
}

/// <summary>
/// Extension methods for using JSON serialization.
/// </summary>
public static class JsonSerializerExtensions
{
    /// <summary>
    /// Configures the producer to use JSON serialization for values.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseJsonSerializer<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithValueSerializer(new JsonSerializer<TValue>(options));
    }

    /// <summary>
    /// Configures the producer to use JSON serialization for keys.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseJsonKeySerializer<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithKeySerializer(new JsonSerializer<TKey>(options));
    }

    /// <summary>
    /// Configures the consumer to use JSON deserialization for values.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> UseJsonDeserializer<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithValueDeserializer(new JsonSerializer<TValue>(options));
    }

    /// <summary>
    /// Configures the consumer to use JSON deserialization for keys.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> UseJsonKeyDeserializer<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithKeyDeserializer(new JsonSerializer<TKey>(options));
    }
}
