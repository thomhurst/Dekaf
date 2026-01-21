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
/// </summary>
public sealed class SerializationContext
{
    /// <summary>
    /// The topic the data is for.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Whether this is key or value data.
    /// </summary>
    public required SerializationComponent Component { get; init; }

    /// <summary>
    /// Headers associated with the record.
    /// </summary>
    public Headers? Headers { get; init; }
}

/// <summary>
/// Indicates whether serialization is for key or value.
/// </summary>
public enum SerializationComponent
{
    Key,
    Value
}
