using System.Buffers;

namespace Dekaf.Serialization;

/// <summary>
/// Interface for serializing values to bytes.
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
public interface ISerializer<in T>
{
    /// <summary>
    /// Serializes a value to the output buffer.
    /// </summary>
    void Serialize(T value, IBufferWriter<byte> destination, SerializationContext context);
}

/// <summary>
/// Interface for deserializing values from bytes.
/// </summary>
/// <typeparam name="T">The type to deserialize.</typeparam>
public interface IDeserializer<out T>
{
    /// <summary>
    /// Deserializes a value from the input data.
    /// </summary>
    T Deserialize(ReadOnlySequence<byte> data, SerializationContext context);
}

/// <summary>
/// Combined serializer and deserializer interface.
/// </summary>
/// <typeparam name="T">The type to serialize/deserialize.</typeparam>
public interface ISerde<T> : ISerializer<T>, IDeserializer<T>;

/// <summary>
/// Context for serialization/deserialization operations.
/// This is a struct to avoid heap allocations in the hot path.
/// Mutable to allow thread-local reuse without allocations.
/// </summary>
/// <remarks>
/// <para><b>Design Rationale - Mutable Struct Pattern:</b></para>
/// <para>
/// This struct is intentionally mutable (using <c>set</c> instead of <c>init</c>) to enable
/// zero-allocation reuse via ThreadStatic storage. This is a deliberate anti-pattern exception
/// justified by performance requirements in the hot path.
/// </para>
/// <para><b>Usage Pattern:</b></para>
/// <para>
/// Producer and Consumer implementations maintain thread-local instances of this struct
/// and update properties in-place rather than creating new instances:
/// <code>
/// [ThreadStatic]
/// private static SerializationContext t_context;
///
/// // Zero-allocation reuse
/// t_context.Topic = topic;
/// t_context.Component = SerializationComponent.Key;
/// t_context.Headers = headers;
/// serializer.Serialize(key, buffer, t_context);
/// </code>
/// </para>
/// <para><b>Safety Considerations:</b></para>
/// <list type="bullet">
/// <item>ThreadStatic ensures no cross-thread sharing or race conditions</item>
/// <item>Struct is passed by value to serializers, preventing external mutation</item>
/// <item>Properties contain only reference types, avoiding defensive copying issues</item>
/// <item>Pattern is internal to Dekaf - user serializers receive immutable copies</item>
/// </list>
/// <para><b>Performance Impact:</b></para>
/// <para>
/// Eliminates ~32 bytes of allocation per message (8 bytes header + 24 bytes struct)
/// in the hot path. At 1M msg/s, this saves 32MB/s of allocation pressure.
/// </para>
/// </remarks>
public struct SerializationContext
{
    /// <summary>
    /// The topic the data is for.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Whether this is key or value data.
    /// </summary>
    public SerializationComponent Component { get; set; }

    /// <summary>
    /// Headers associated with the record.
    /// </summary>
    public Headers? Headers { get; set; }
}

/// <summary>
/// Indicates whether serialization is for key or value.
/// </summary>
public enum SerializationComponent
{
    Key,
    Value
}
