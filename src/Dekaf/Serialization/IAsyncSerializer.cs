using System.Buffers;

namespace Dekaf.Serialization;

/// <summary>
/// Interface for serializers that need to perform asynchronous work per value — for example an
/// encryption stack that fetches short-lived keys, or any serializer whose encoding requires I/O.
/// </summary>
/// <remarks>
/// <para>
/// Prefer <see cref="ISerializer{T}"/> whenever the serializer can encode synchronously:
/// synchronous serializers run on the producer's zero-allocation fast path, while asynchronous
/// serializers route every message through a slower pooled-buffer path that carries async
/// state-machine overhead. When the asynchronous work is a one-time cached setup (such as a
/// Schema Registry fetch), implement <see cref="IAsyncSerializerPreparer{T}"/> on a synchronous
/// serializer instead — that keeps the steady-state encode on the fast path.
/// </para>
/// <para>
/// When an asynchronous serializer is configured (via
/// <c>ProducerBuilder.WithKeySerializer(IAsyncSerializer&lt;TKey&gt;)</c> or
/// <c>WithValueSerializer(IAsyncSerializer&lt;TValue&gt;)</c>), the producer awaits
/// <see cref="SerializeAsync"/> on its asynchronous produce path before appending the serialized
/// bytes to the batch. Fire-and-forget (<c>FireAsync</c>) is still supported: serialization is
/// awaited, and delivery errors are routed to the delivery handler or logged.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to serialize.</typeparam>
public interface IAsyncSerializer<in T>
{
    /// <summary>
    /// Serializes a value to the output buffer, awaiting any required asynchronous work.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="destination">
    /// The buffer to write serialized bytes to. The buffer is only valid until the returned task
    /// completes; implementations must not retain it.
    /// </param>
    /// <param name="context">Serialization context with topic and header information.</param>
    /// <param name="cancellationToken">A token to cancel the serialization.</param>
    ValueTask SerializeAsync(
        T value,
        IBufferWriter<byte> destination,
        SerializationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for deserializers that need to perform asynchronous work per value — for example a
/// decryption stack that fetches short-lived keys, or any decoding that requires I/O.
/// </summary>
/// <remarks>
/// <para>
/// Prefer <see cref="IDeserializer{T}"/> whenever decoding can run synchronously: synchronous
/// deserializers run inline on the consumer's poll path, while asynchronous deserializers are
/// awaited per record. When an asynchronous deserializer is configured, records are delivered via
/// <c>ConsumeAsync</c> and <c>ConsumeOneAsync</c>; the batch API (<c>ConsumeBatchAsync</c>)
/// iterates records synchronously and therefore throws <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// The <c>data</c> memory passed to <see cref="DeserializeAsync"/> references pooled fetch
/// buffers and is only valid until the returned task completes; implementations must copy the
/// bytes if they need them afterwards.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to deserialize.</typeparam>
public interface IAsyncDeserializer<T>
{
    /// <summary>
    /// Deserializes a value from the input data, awaiting any required asynchronous work.
    /// </summary>
    /// <param name="data">
    /// The serialized bytes. Only valid until the returned task completes — copy if retained.
    /// </param>
    /// <param name="context">Serialization context with topic and null-ness information.</param>
    /// <param name="cancellationToken">A token to cancel the deserialization.</param>
    ValueTask<T> DeserializeAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Combined asynchronous serializer and deserializer interface.
/// </summary>
/// <typeparam name="T">The type to serialize/deserialize.</typeparam>
public interface IAsyncSerde<T> : IAsyncSerializer<T>, IAsyncDeserializer<T>;
