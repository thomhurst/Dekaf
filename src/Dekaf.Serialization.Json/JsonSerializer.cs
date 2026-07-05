using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Dekaf.Serialization.Json;

/// <summary>
/// JSON serializer using System.Text.Json.
/// </summary>
/// <typeparam name="T">Type to serialize.</typeparam>
public sealed class JsonSerializer<T> : ISerde<T>
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_sharedBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_jsonWriter;

    private readonly JsonSerializerOptions? _options;
    private readonly JsonTypeInfo<T>? _typeInfo;

    [RequiresDynamicCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    [RequiresUnreferencedCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    public JsonSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? CreateDefaultOptions();
    }

    public JsonSerializer(JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        _typeInfo = typeInfo;
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        // Utf8JsonWriter cannot accept the generic TWriter directly because the
        // 'allows ref struct' constraint prevents boxing to IBufferWriter<byte>.
        // A ThreadStatic ArrayBufferWriter is used as an intermediate buffer.
        // This is safe because Serialize is fully synchronous (no await points),
        // so thread migration cannot occur mid-operation.
        var sharedBuffer = t_sharedBuffer ??= new ArrayBufferWriter<byte>();
        sharedBuffer.ResetWrittenCount();

        var jsonWriter = t_jsonWriter;
        if (jsonWriter is null)
        {
            jsonWriter = new Utf8JsonWriter(sharedBuffer);
            t_jsonWriter = jsonWriter;
        }
        else
        {
            jsonWriter.Reset(sharedBuffer);
        }

        try
        {
            if (_typeInfo is not null)
            {
                System.Text.Json.JsonSerializer.Serialize(jsonWriter, value, _typeInfo);
            }
            else
            {
                System.Text.Json.JsonSerializer.Serialize(jsonWriter, value, _options);
            }

            jsonWriter.Flush();
        }
        catch
        {
            t_jsonWriter = null;
            throw;
        }

        var written = sharedBuffer.WrittenSpan;
        var span = destination.GetSpan(written.Length);
        written.CopyTo(span);
        destination.Advance(written.Length);
    }

    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return _typeInfo is not null
            ? System.Text.Json.JsonSerializer.Deserialize(data.Span, _typeInfo)!
            : System.Text.Json.JsonSerializer.Deserialize<T>(data.Span, _options)!;
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
    [RequiresDynamicCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    [RequiresUnreferencedCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    public static ProducerBuilder<TKey, TValue> UseJsonSerializer<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithValueSerializer(new JsonSerializer<TValue>(options));
    }

    /// <summary>
    /// Configures the producer to use NativeAOT-safe JSON serialization for values.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseJsonSerializer<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        JsonTypeInfo<TValue> typeInfo)
    {
        return builder.WithValueSerializer(new JsonSerializer<TValue>(typeInfo));
    }

    /// <summary>
    /// Configures the producer to use JSON serialization for keys.
    /// </summary>
    [RequiresDynamicCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    [RequiresUnreferencedCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    public static ProducerBuilder<TKey, TValue> UseJsonKeySerializer<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithKeySerializer(new JsonSerializer<TKey>(options));
    }

    /// <summary>
    /// Configures the producer to use NativeAOT-safe JSON serialization for keys.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseJsonKeySerializer<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder,
        JsonTypeInfo<TKey> typeInfo)
    {
        return builder.WithKeySerializer(new JsonSerializer<TKey>(typeInfo));
    }

    /// <summary>
    /// Configures the consumer to use JSON deserialization for values.
    /// </summary>
    [RequiresDynamicCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    [RequiresUnreferencedCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    public static ConsumerBuilder<TKey, TValue> UseJsonDeserializer<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithValueDeserializer(new JsonSerializer<TValue>(options));
    }

    /// <summary>
    /// Configures the consumer to use NativeAOT-safe JSON deserialization for values.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> UseJsonDeserializer<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        JsonTypeInfo<TValue> typeInfo)
    {
        return builder.WithValueDeserializer(new JsonSerializer<TValue>(typeInfo));
    }

    /// <summary>
    /// Configures the consumer to use JSON deserialization for keys.
    /// </summary>
    [RequiresDynamicCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    [RequiresUnreferencedCode(JsonSerializerAotMessages.ReflectionSerializationMessage)]
    public static ConsumerBuilder<TKey, TValue> UseJsonKeyDeserializer<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        JsonSerializerOptions? options = null)
    {
        return builder.WithKeyDeserializer(new JsonSerializer<TKey>(options));
    }

    /// <summary>
    /// Configures the consumer to use NativeAOT-safe JSON deserialization for keys.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> UseJsonKeyDeserializer<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder,
        JsonTypeInfo<TKey> typeInfo)
    {
        return builder.WithKeyDeserializer(new JsonSerializer<TKey>(typeInfo));
    }
}

internal static class JsonSerializerAotMessages
{
    public const string ReflectionSerializationMessage =
        "JsonSerializerOptions-based JSON serialization can require runtime reflection. " +
        "Use the JsonTypeInfo<T> overload for NativeAOT-safe serialization.";
}
