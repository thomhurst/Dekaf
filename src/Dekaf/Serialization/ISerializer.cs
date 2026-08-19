using System.Buffers;
using System.Runtime.CompilerServices;

namespace Dekaf.Serialization;

internal interface IRecordHeaderRoutingProvider
{
    void CollectHeaderNames(List<string> names);
}

internal sealed class RecordHeaderRoutingPlan
{
    private readonly Dictionary<string, int> _slots;

    private RecordHeaderRoutingPlan(List<string> names)
    {
        _slots = new Dictionary<string, int>(names.Count, StringComparer.Ordinal);
        for (var index = 0; index < names.Count; index++)
            _slots.Add(names[index], index);
    }

    internal int Count => _slots.Count;

    internal bool TryGetSlot(string headerName, out int slot) =>
        _slots.TryGetValue(headerName, out slot);

    internal static RecordHeaderRoutingPlan? Create<TKey, TValue>(
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer)
    {
        List<string>? names = null;
        Collect(keyDeserializer, ref names);
        Collect(valueDeserializer, ref names);
        return names is null ? null : new RecordHeaderRoutingPlan(names);
    }

    private static void Collect<T>(IDeserializer<T>? deserializer, ref List<string>? names)
    {
        if (deserializer is not IRecordHeaderRoutingProvider provider)
            return;

        names ??= [];
        provider.CollectHeaderNames(names);
    }
}

internal readonly struct RecordHeaderRoutingLookup(
    RecordHeaderRoutingPlan? plan,
    Header[]? headers,
    int headerCount,
    int firstIndex,
    int secondIndex,
    int[]? remainingIndices)
{
    internal bool TryGetLast(string headerName, out Header header)
    {
        if (plan is not null && plan.TryGetSlot(headerName, out var slot))
        {
            var index = slot switch
            {
                0 => firstIndex,
                1 => secondIndex,
                _ when remainingIndices is not null => remainingIndices[slot - 2],
                _ => 0
            } - 1;
            if ((uint)index < (uint)headerCount && headers is not null)
            {
                header = headers[index];
                return true;
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

internal static class RecordHeaderDeserializer
{
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
            : deserializer.Deserialize(data, context);
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
