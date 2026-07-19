using System.Buffers;

namespace Dekaf.Serialization;

/// <summary>
/// Placeholder occupying the synchronous serializer slot when only an
/// <see cref="IAsyncSerializer{T}"/> is configured. The producer routes every message through the
/// asynchronous serialization path in that mode, so this is never invoked; reaching it means a
/// produce path bypassed the async-serializer routing and must fail loudly rather than encode
/// nothing.
/// </summary>
internal sealed class AsyncOnlySerializerPlaceholder<T> : ISerializer<T>
{
    public static readonly AsyncOnlySerializerPlaceholder<T> Instance = new();

    private AsyncOnlySerializerPlaceholder()
    {
    }

    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if !NETSTANDARD2_0
        , allows ref struct
#endif
        => throw new InvalidOperationException(
            "An asynchronous serializer is configured for this component, so the synchronous " +
            "serialization path must not be reached. This is a bug in Dekaf — please report it.");
}

/// <summary>
/// Placeholder occupying the synchronous deserializer slot when only an
/// <see cref="IAsyncDeserializer{T}"/> is configured. See
/// <see cref="AsyncOnlySerializerPlaceholder{T}"/> for the rationale.
/// </summary>
internal sealed class AsyncOnlyDeserializerPlaceholder<T> : IDeserializer<T>
{
    public static readonly AsyncOnlyDeserializerPlaceholder<T> Instance = new();

    private AsyncOnlyDeserializerPlaceholder()
    {
    }

    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        => throw new InvalidOperationException(
            "An asynchronous deserializer is configured for this component, so the synchronous " +
            "deserialization path must not be reached. This is a bug in Dekaf — please report it.");
}
