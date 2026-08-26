using System.Buffers;
using System.Runtime.CompilerServices;

namespace Dekaf.Serialization;

internal interface IRecordHeaderRoutingProvider
{
    void CollectHeaderNames(List<string> names);
}

internal sealed class RecordHeaderRoutingPlan
{
    internal const int FullyIndexedWithoutTail = -1;
    internal const int InlineSlotsOnly = -2;
    internal const string MaterializedHeadersSentinel = "\0dekaf-materialized-record-headers";

    private readonly Dictionary<string, int> _slots;

    private RecordHeaderRoutingPlan(
        List<string> names,
        bool keyRequiresMaterializedHeaders,
        bool valueRequiresMaterializedHeaders)
    {
        _slots = new Dictionary<string, int>(names.Count, StringComparer.Ordinal);
        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index];
            if (!string.Equals(name, MaterializedHeadersSentinel, StringComparison.Ordinal))
                _slots.Add(name, _slots.Count);
        }

        KeyRequiresMaterializedHeaders = keyRequiresMaterializedHeaders;
        ValueRequiresMaterializedHeaders = valueRequiresMaterializedHeaders;
    }

    internal int Count => _slots.Count;
    internal bool KeyRequiresMaterializedHeaders { get; }
    internal bool ValueRequiresMaterializedHeaders { get; }

    internal bool TryGetSlot(string headerName, out int slot) =>
        _slots.TryGetValue(headerName, out slot);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetRoutingTailCapacity(int headerCount)
    {
        var indexedHeaderCount = Math.Min(Count - 2, headerCount);
        if (indexedHeaderCount <= 0)
            return 0;

        var capacity = (uint)indexedHeaderCount * 2 - 1;
        capacity |= capacity >> 1;
        capacity |= capacity >> 2;
        capacity |= capacity >> 4;
        capacity |= capacity >> 8;
        capacity |= capacity >> 16;
        return (int)(capacity + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetRoutingTailBucket(int slot, int mask) =>
        (int)((uint)slot * 2654435761U) & mask;

    internal static RecordHeaderRoutingPlan? Create<TKey, TValue>(
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer)
    {
        List<string>? names = null;
        Collect(keyDeserializer, ref names);
        Collect(valueDeserializer, ref names);
        return names is { Count: > 0 }
            ? new RecordHeaderRoutingPlan(
                names,
                RequiresMaterializedHeaders(keyDeserializer),
                RequiresMaterializedHeaders(valueDeserializer))
            : null;
    }

    private static void Collect<T>(IDeserializer<T>? deserializer, ref List<string>? names)
    {
        if (deserializer is not IRecordHeaderRoutingProvider provider)
            return;

        names ??= [];
        provider.CollectHeaderNames(names);
    }

    private static bool RequiresMaterializedHeaders<T>(IDeserializer<T>? deserializer)
    {
        if (deserializer is not IRecordHeaderRoutingProvider provider)
            return false;

        List<string> names = [];
        provider.CollectHeaderNames(names);
        for (var index = 0; index < names.Count; index++)
        {
            if (string.Equals(names[index], MaterializedHeadersSentinel, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

internal readonly struct RecordHeaderRoutingLookup(
    RecordHeaderRoutingPlan? plan,
    Header[]? headers,
    int headerCount,
    int firstIndex,
    int secondIndex,
    int routedHeaderTailOffset)
{
    internal bool KeyRequiresMaterializedHeaders => plan?.KeyRequiresMaterializedHeaders is true;
    internal bool ValueRequiresMaterializedHeaders => plan?.ValueRequiresMaterializedHeaders is true;

    internal void CopyTo(Headers destination)
    {
        destination.Clear();
        if (headers is null)
            return;

        for (var index = 0; index < headerCount; index++)
            destination.Add(headers[index]);
    }

    internal bool TryGetLast(string headerName, out Header header)
    {
        if (plan is not null && plan.TryGetSlot(headerName, out var slot))
        {
            var index = slot switch
            {
                0 => firstIndex,
                1 => secondIndex,
                _ => 0
            } - 1;
            if ((uint)index < (uint)headerCount && headers is not null)
            {
                header = headers[index];
                return true;
            }

            if (slot >= 2 && routedHeaderTailOffset > 0 && headers is not null)
            {
                var capacity = plan.GetRoutingTailCapacity(headerCount);
                var mask = capacity - 1;
                var bucket = RecordHeaderRoutingPlan.GetRoutingTailBucket(slot, mask);
                for (var probe = 0; probe < capacity; probe++)
                {
                    header = headers[routedHeaderTailOffset + bucket];
                    if (header.Key is null)
                        return false;
                    if (string.Equals(header.Key, headerName, StringComparison.Ordinal))
                        return true;
                    bucket = (bucket + 1) & mask;
                }

                header = default;
                return false;
            }

            // -1 means every configured slot was indexed inline; a positive value means
            // the slots after the first two were indexed in the pooled header-array tail.
            if (routedHeaderTailOffset == RecordHeaderRoutingPlan.FullyIndexedWithoutTail
                || routedHeaderTailOffset > 0)
            {
                header = default;
                return false;
            }

            // -2 is the compatibility form for an already-parsed record: the first two
            // slots are indexed, while later slots use the cold linear fallback below.
            if (routedHeaderTailOffset == RecordHeaderRoutingPlan.InlineSlotsOnly && slot < 2)
            {
                header = default;
                return false;
            }
        }

        // Records configured after parsing cannot reserve routing slots in their pooled
        // header array. This cold compatibility path preserves nested-router correctness;
        // network receive paths configure the plan before parsing and use the O(1) tail.
        if (headers is not null)
        {
            for (var index = headerCount - 1; index >= 0; index--)
            {
                if (string.Equals(headers[index].Key, headerName, StringComparison.Ordinal))
                {
                    header = headers[index];
                    return true;
                }
            }
        }

        header = default;
        return false;
    }
}

/// <summary>
/// Interface for serializing values to bytes.
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
public interface ISerializer<in T>
{
    /// <summary>
    /// Serializes a value to the output buffer.
    /// </summary>
    /// <typeparam name="TWriter">The buffer writer type. Supports ref struct writers for zero-allocation serialization.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="destination">The buffer to write serialized bytes to. Passed by ref to support ref struct writers.</param>
    /// <param name="context">Serialization context with topic and header information.</param>
    void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if !NETSTANDARD2_0
        , allows ref struct
#endif
        ;
}

/// <summary>
/// Optional capability for serializers that may add record headers through
/// <see cref="SerializationContext.Headers"/>.
/// </summary>
/// <remarks>
/// Serializer decorators must propagate this capability when their inner serializer produces
/// headers so producers can provide header storage before serialization begins.
/// </remarks>
public interface IRecordHeaderSerializer
{
    /// <summary>Gets whether this serializer may add record headers.</summary>
    bool ProducesRecordHeaders { get; }
}

/// <summary>
/// Optional capability for deserializers that read record headers through
/// <see cref="SerializationContext.Headers"/>.
/// </summary>
/// <remarks>
/// Deserializer decorators must propagate this capability when their inner deserializer consumes
/// headers so consumers can provide header storage before deserialization begins.
/// </remarks>
public interface IRecordHeaderDeserializer
{
    /// <summary>Gets whether this deserializer may read record headers.</summary>
    bool ConsumesRecordHeaders { get; }
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
    T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context);
}

internal interface IRecordHeaderDeserializer<out T>
{
    T Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers);
}

internal interface ICallerOwnedHeaderDeserializer<out T>
{
    T DeserializeCallerOwned(ReadOnlyMemory<byte> data, SerializationContext context);
}

internal static class RecordHeaderDeserializer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool UsesCallerOwnedHeaders<T>(IDeserializer<T> deserializer) =>
        deserializer is ICallerOwnedHeaderDeserializer<T>
        || deserializer is IRecordHeaderDeserializer { ConsumesRecordHeaders: true };

    internal static IDeserializer<T> WrapIfNeeded<T>(IDeserializer<T> deserializer) =>
        deserializer is IRecordHeaderDeserializer<T>
        || deserializer is not IRecordHeaderDeserializer { ConsumesRecordHeaders: true }
            ? deserializer
            : new RecordHeaderDeserializerAdapter<T>(deserializer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T DeserializeCallerOwned<T>(
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context)
    {
        if (deserializer is ICallerOwnedHeaderDeserializer<T> callerOwned)
            return callerOwned.DeserializeCallerOwned(data, context);

        if (deserializer is not IRecordHeaderDeserializer { ConsumesRecordHeaders: true }
            && context.Headers is not null)
            context.Headers = null;
        return deserializer.Deserialize(data, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T Deserialize<T>(
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        deserializer is IRecordHeaderDeserializer<T> headerDeserializer
            ? headerDeserializer.Deserialize(
                data,
                context,
                in headers)
            : DeserializeCallerOwned(deserializer, data, context);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T DeserializeChild<T>(
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers)
    {
        if (deserializer is IRecordHeaderDeserializer<T> nested)
            return nested.Deserialize(data, context, in headers);

        if (context.Headers is not null)
            context.Headers = null;
        return deserializer.Deserialize(data, context);
    }
}

internal static class RecordHeaderMaterializer
{
    [ThreadStatic]
    private static Headers? t_callerOwnedHeaders;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SerializationContext WithCallerOwnedHeaders(
        SerializationContext context,
        in RecordHeaderRoutingLookup headers)
    {
        context.Headers ??= GetCallerOwnedHeaders(in headers);
        return context;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Headers GetCallerOwnedHeaders(in RecordHeaderRoutingLookup headers)
    {
        var callerOwnedHeaders = t_callerOwnedHeaders ??= new Headers();
        headers.CopyTo(callerOwnedHeaders);
        return callerOwnedHeaders;
    }

    internal static SerializationContext WithOwnedHeaders(
        SerializationContext context,
        in RecordHeaderRoutingLookup headers)
    {
        context.Headers = GetOwnedHeaders(in headers);
        return context;
    }

    internal static Headers GetOwnedHeaders(in RecordHeaderRoutingLookup headers)
    {
        var ownedHeaders = new Headers();
        headers.CopyTo(ownedHeaders);
        return ownedHeaders;
    }
}

internal sealed class RecordHeaderDeserializerAdapter<T>(IDeserializer<T> inner) :
    IDeserializer<T>,
    IAsyncDeserializerPreparer<T>,
    IAsyncDeserializerPreparationRequirement,
    IRecordHeaderDeserializer<T>,
    IRecordHeaderAsyncDeserializerPreparer<T>,
    ICallerOwnedHeaderDeserializer<T>,
    IRecordHeaderRoutingProvider
{
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
        inner.Deserialize(data, context);

    bool IAsyncDeserializerPreparationRequirement.RequiresPreparation =>
        inner is IAsyncDeserializerPreparer<T>
        && inner is not IAsyncDeserializerPreparationRequirement { RequiresPreparation: false };

    bool IAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out T value)
    {
        if (inner is IAsyncDeserializerPreparer<T> preparer)
            return preparer.TryDeserialize(data, context, out value);

        value = inner.Deserialize(data, context);
        return true;
    }

    ValueTask IAsyncDeserializerPreparer<T>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        inner is IAsyncDeserializerPreparer<T> preparer
            ? preparer.PrepareAsync(data, context, cancellationToken)
            : default;

    T IRecordHeaderDeserializer<T>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        inner.Deserialize(
            data,
            RecordHeaderMaterializer.WithCallerOwnedHeaders(context, in headers));

    bool IRecordHeaderAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out T value)
    {
        context = RecordHeaderMaterializer.WithCallerOwnedHeaders(context, in headers);
        return ((IAsyncDeserializerPreparer<T>)this).TryDeserialize(data, context, out value);
    }

    ValueTask IRecordHeaderAsyncDeserializerPreparer<T>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken)
    {
        context = RecordHeaderMaterializer.WithOwnedHeaders(context, in headers);
        return ((IAsyncDeserializerPreparer<T>)this).PrepareAsync(
            data,
            context,
            cancellationToken);
    }

    T ICallerOwnedHeaderDeserializer<T>.DeserializeCallerOwned(
        ReadOnlyMemory<byte> data,
        SerializationContext context) => inner.Deserialize(data, context);

    void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names)
    {
        if (inner is IRecordHeaderRoutingProvider provider)
            provider.CollectHeaderNames(names);

        for (var index = 0; index < names.Count; index++)
        {
            if (string.Equals(
                    names[index],
                    RecordHeaderRoutingPlan.MaterializedHeadersSentinel,
                    StringComparison.Ordinal))
                return;
        }

        names.Add(RecordHeaderRoutingPlan.MaterializedHeadersSentinel);
    }
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
    /// <remarks>
    /// During consumption this instance may be cleared, refilled, and reused for later records.
    /// It is valid only for the current <see cref="IDeserializer{T}.Deserialize"/> or async
    /// deserializer call. Deserializers must not retain it; copy entries when a snapshot is needed.
    /// </remarks>
    public Headers? Headers { get; set; }

    /// <summary>
    /// Raw record-key bytes available while deserializing a consumed value.
    /// </summary>
    /// <remarks>
    /// The memory may reference a pooled receive buffer and is valid only for the duration of the
    /// current <see cref="IDeserializer{T}.Deserialize"/> or async deserializer call. Deserializers
    /// must not retain it. This property is empty for key deserialization and serialization.
    /// </remarks>
    public ReadOnlyMemory<byte> KeyData { get; set; }

    /// <summary>
    /// Whether <see cref="KeyData"/> represents a null consumed record key.
    /// </summary>
    public readonly bool IsKeyNull => KeyData.Equals(default(ReadOnlyMemory<byte>));

    internal static ReadOnlyMemory<byte> NormalizeKeyData(ReadOnlyMemory<byte> keyData, bool isKeyNull) =>
        isKeyNull ? default : keyData.IsEmpty ? Array.Empty<byte>() : keyData;

    /// <summary>
    /// Whether the original data was null (as opposed to empty).
    /// This allows deserializers to distinguish between null values and empty byte arrays.
    /// </summary>
    public bool IsNull { get; set; }
}

/// <summary>
/// Indicates whether serialization is for key or value.
/// </summary>
public enum SerializationComponent
{
    Key,
    Value
}
