namespace Dekaf.Serialization;

/// <summary>
/// Optional companion to <see cref="ISerializer{T}"/> for serializers whose one-time setup is
/// asynchronous — most notably Schema Registry serializers, which must fetch (or register) a schema
/// ID over the network before the first value for a subject can be encoded.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISerializer{T}.Serialize"/> is intentionally synchronous and zero-allocation (it writes
/// into a pooled <c>ref struct</c> buffer, which cannot cross an <c>await</c>), so it has no way to do
/// async work itself. Without this hook a serializer's only options are to block a thread on the async
/// fetch (sync-over-async) or to require the caller to remember to warm up first.
/// </para>
/// <para>
/// When a serializer implements this interface, the producer awaits <see cref="PrepareAsync"/> on its
/// asynchronous produce path <em>before</em> invoking the synchronous <see cref="ISerializer{T}.Serialize"/>.
/// That moves the one-time async fetch onto the async path where it belongs, so the encode never blocks
/// a thread. Once prepared, the result is expected to be cached: implementations should return an already
/// completed <see cref="ValueTask"/> with no allocation, keeping the steady-state hot path fully synchronous.
/// </para>
/// <para>
/// This is an optimization, not a correctness requirement: <see cref="ISerializer{T}.Serialize"/> must still
/// succeed if it is reached without a prior <see cref="PrepareAsync"/> (it will resolve the prerequisite
/// itself, blocking on the first call for a subject). Fire-and-forget produce paths are synchronous and
/// cannot await preparation, so callers that use them should pre-warm the serializer explicitly.
/// </para>
/// </remarks>
/// <typeparam name="T">The type the serializer handles.</typeparam>
public interface IAsyncSerializerPreparer<in T>
{
    /// <summary>
    /// Ensures any asynchronous prerequisites for serializing <paramref name="value"/> in the given
    /// <paramref name="context"/> are satisfied and cached, so a subsequent synchronous
    /// <see cref="ISerializer{T}.Serialize"/> for the same subject does not block.
    /// </summary>
    /// <param name="value">The value about to be serialized (needed to resolve its schema).</param>
    /// <param name="context">The serialization context (topic and key/value component).</param>
    /// <param name="cancellationToken">A token to cancel the preparation.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when preparation is done. Implementations should return
    /// an already completed task (no allocation) when the subject is already prepared.
    /// </returns>
    ValueTask PrepareAsync(T value, SerializationContext context, CancellationToken cancellationToken = default);
}
