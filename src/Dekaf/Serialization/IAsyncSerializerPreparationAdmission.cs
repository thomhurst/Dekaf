using System.Buffers;

namespace Dekaf.Serialization;

/// <summary>
/// Internal producer/serializer contract that carries an exact asynchronous preparation result
/// into the synchronous serialization step.
/// </summary>
internal interface IAsyncSerializerPreparationAdmission<in T> : IAsyncSerializerPreparer<T>
{
    ValueTask<SerializerPreparationAdmission> PrepareForSerializationAsync(
        T value,
        SerializationContext context,
        CancellationToken cancellationToken = default);

    void SerializePrepared<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        in SerializerPreparationAdmission admission)
        where TWriter : IBufferWriter<byte>
#if !NETSTANDARD2_0
        , allows ref struct
#endif
        ;
}

/// <summary>
/// Opaque, allocation-free serializer preparation result. State is interpreted only by the
/// serializer that produced it.
/// </summary>
internal readonly struct SerializerPreparationAdmission(
    string subject,
    int schemaId,
    object schema)
{
    internal string? Subject { get; } = subject;
    internal int SchemaId { get; } = schemaId;
    internal object? Schema { get; } = schema;
    internal bool IsPrepared => Subject is not null;
}
