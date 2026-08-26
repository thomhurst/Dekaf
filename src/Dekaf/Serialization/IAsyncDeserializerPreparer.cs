using System.Runtime.CompilerServices;

namespace Dekaf.Serialization;

/// <summary>
/// Optional companion to <see cref="IDeserializer{T}"/> for deserializers whose cached setup may
/// require asynchronous work, such as fetching a writer schema from Schema Registry.
/// </summary>
/// <remarks>
/// <c>ConsumeAsync</c>, <c>ConsumeOneAsync</c>, and the share consumer's <c>PollAsync</c> await
/// <see cref="PrepareAsync"/> after <see cref="TryDeserialize"/> reports a cold miss. Once prepared,
/// <see cref="TryDeserialize"/> should complete synchronously without allocating. Batch records are
/// iterated synchronously, so callers using <c>ConsumeBatchAsync</c> must explicitly warm
/// asynchronous prerequisites first.
/// The supplied data references pooled fetch storage and must not be retained after the returned
/// task completes.
/// </remarks>
public interface IAsyncDeserializerPreparer<T>
{
    /// <summary>
    /// Attempts synchronous deserialization using already-prepared state. Returns <see langword="false"/>
    /// only when <see cref="PrepareAsync"/> must be awaited before retrying.
    /// </summary>
    bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out T value);

    /// <summary>Ensures prerequisites for synchronously deserializing the supplied data are cached.</summary>
    /// <param name="data">Serialized bytes, valid only until the returned task completes.</param>
    /// <param name="context">Serialization context containing topic, component, and null state.</param>
    /// <param name="cancellationToken">A token to cancel preparation.</param>
    ValueTask PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken = default);
}

internal interface IRecordHeaderAsyncDeserializerPreparer<T>
{
    bool TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out T value);

    ValueTask PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken);
}

internal static class DeserializerPreparation
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool RequiresPreparation<T>(IDeserializer<T> deserializer) =>
        deserializer is IAsyncDeserializerPreparer<T> &&
        deserializer is not IAsyncDeserializerPreparationRequirement { RequiresPreparation: false };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryDeserialize<T>(
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out T value)
    {
        if (deserializer is IAsyncDeserializerPreparer<T> preparer)
            return preparer.TryDeserialize(data, context, out value);

        value = RecordHeaderDeserializer.DeserializeCallerOwned(deserializer, data, context);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryDeserialize<T>(
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out T value)
    {
        if (deserializer is IRecordHeaderAsyncDeserializerPreparer<T> headerPreparer)
            return headerPreparer.TryDeserialize(data, context, in headers, out value);
        if (deserializer is IAsyncDeserializerPreparer<T> preparer)
            return preparer.TryDeserialize(data, context, out value);

        value = RecordHeaderDeserializer.DeserializeChild(deserializer, data, context, in headers);
        return true;
    }

    internal static ValueTask PrepareAsync<T>(
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        deserializer is IAsyncDeserializerPreparer<T> preparer
            ? preparer.PrepareAsync(data, context, cancellationToken)
            : default;

    internal static ValueTask PrepareAsync<T>(
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken)
    {
        if (deserializer is IRecordHeaderAsyncDeserializerPreparer<T> headerPreparer)
        {
            return headerPreparer.PrepareAsync(
                data,
                context,
                headers,
                cancellationToken);
        }

        return PrepareAsync(deserializer, data, context, cancellationToken);
    }
}

/// <summary>
/// Optional capability for an <see cref="IAsyncDeserializerPreparer{T}"/> that can skip the
/// asynchronous preparation path for its current configuration.
/// </summary>
/// <remarks>
/// Preparers that do not implement this interface are treated as requiring preparation. Consumers
/// read this once during construction, avoiding interface dispatch and asynchronous drain
/// bookkeeping when preparation is not required.
/// </remarks>
public interface IAsyncDeserializerPreparationRequirement
{
    /// <summary>Gets whether this instance requires the asynchronous preparation path.</summary>
    bool RequiresPreparation { get; }
}
