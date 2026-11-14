using System.Diagnostics.CodeAnalysis;

namespace Dekaf.Streams;

/// <summary>Read access to a key-value store.</summary>
public interface IReadOnlyKeyValueStore<TKey, TValue>
{
    /// <summary>Gets the store name.</summary>
    string Name { get; }

    /// <summary>Gets the number of keys in the store.</summary>
    long Count { get; }

    /// <summary>Reads the value of <paramref name="key"/>.</summary>
    bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value);
}

/// <summary>A key-value store owned by one task. Every write is also recorded in the store's changelog topic.</summary>
public interface IKeyValueStore<TKey, TValue> : IReadOnlyKeyValueStore<TKey, TValue>
{
    /// <summary>Writes a value. A <see langword="null"/> value deletes the key.</summary>
    void Put(TKey key, TValue value);

    /// <summary>Deletes a key.</summary>
    /// <returns><see langword="true"/> when the key existed.</returns>
    bool Delete(TKey key);
}

/// <summary>Identifies the store an engine is asked to create.</summary>
/// <param name="ApplicationId">The streams application id.</param>
/// <param name="StoreName">The store name.</param>
/// <param name="SubtopologyId">The subtopology of the owning task.</param>
/// <param name="Partition">The partition of the owning task.</param>
/// <param name="Directory">
/// The directory reserved for this store, or <see langword="null"/> for in-memory state. The runtime
/// empties it before the store is created unless the previous owner closed the store cleanly.
/// </param>
public readonly record struct StateStoreContext(
    string ApplicationId,
    string StoreName,
    string SubtopologyId,
    int Partition,
    string? Directory);

/// <summary>
/// Creates the byte stores of a task. Implement this to plug in a storage engine, and select it with
/// <see cref="KafkaStreamsBuilder.WithPersistentState"/>.
/// </summary>
public interface IStateStoreEngine
{
    /// <summary>Creates a key-value store.</summary>
    IKeyValueBytesStore CreateKeyValueStore(StateStoreContext context);

    /// <summary>Creates a window store.</summary>
    IWindowBytesStore CreateWindowStore(StateStoreContext context);
}

/// <summary>A key-value store of serialized keys and values, used by one thread at a time.</summary>
public interface IKeyValueBytesStore : IDisposable
{
    /// <summary>Gets the number of keys in the store.</summary>
    long Count { get; }

    /// <summary>Reads a value. The returned memory is valid until the next write to the store.</summary>
    bool TryGet(ReadOnlySpan<byte> key, out ReadOnlyMemory<byte> value);

    /// <summary>Writes a value, replacing any existing one.</summary>
    void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

    /// <summary>Deletes a key.</summary>
    /// <returns><see langword="true"/> when the key existed.</returns>
    bool Delete(ReadOnlySpan<byte> key);
}

/// <summary>A store of serialized values per key and window start, used by one thread at a time.</summary>
public interface IWindowBytesStore : IDisposable
{
    /// <summary>Gets the number of entries in the store.</summary>
    long Count { get; }

    /// <summary>Reads a value. The returned memory is valid until the next write to the store.</summary>
    bool TryGet(ReadOnlySpan<byte> key, long windowStart, out ReadOnlyMemory<byte> value);

    /// <summary>Writes a value, replacing any existing one.</summary>
    void Put(ReadOnlySpan<byte> key, long windowStart, ReadOnlySpan<byte> value);

    /// <summary>Deletes one entry.</summary>
    /// <returns><see langword="true"/> when the entry existed.</returns>
    bool Delete(ReadOnlySpan<byte> key, long windowStart);

    /// <summary>
    /// Removes every entry whose window starts at or before <paramref name="windowStart"/>, in window order,
    /// passing each one to <paramref name="visitor"/> first.
    /// </summary>
    void RemoveThrough(long windowStart, IWindowEntryVisitor visitor);
}

/// <summary>Receives the entries removed by <see cref="IWindowBytesStore.RemoveThrough"/>.</summary>
public interface IWindowEntryVisitor
{
    /// <summary>Visits one entry. The memory is valid only during the call.</summary>
    void Visit(ReadOnlyMemory<byte> key, long windowStart, ReadOnlyMemory<byte> value);
}
