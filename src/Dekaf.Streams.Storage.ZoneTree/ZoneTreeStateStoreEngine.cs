using System.Buffers.Binary;
using ZoneTree;
using ZoneTree.Comparers;
using ZoneTree.Serializers;

namespace Dekaf.Streams.Storage.ZoneTree;

/// <summary>Selects ZoneTree-backed persistent state for a <see cref="KafkaStreams"/> instance.</summary>
public static class ZoneTreeStateExtensions
{
    /// <summary>
    /// Keeps state stores on disk under <paramref name="directory"/> in ZoneTree, a managed LSM engine.
    /// See <see cref="KafkaStreamsBuilder.WithPersistentState"/> for what survives a restart.
    /// </summary>
    public static KafkaStreamsBuilder UseZoneTreeState(this KafkaStreamsBuilder builder, string directory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithPersistentState(directory, ZoneTreeStateStoreEngine.Instance);
    }
}

/// <summary>Creates state stores that keep their data in ZoneTree.</summary>
public sealed class ZoneTreeStateStoreEngine : IStateStoreEngine
{
    private ZoneTreeStateStoreEngine()
    {
    }

    /// <summary>Gets the engine.</summary>
    public static ZoneTreeStateStoreEngine Instance { get; } = new();

    /// <inheritdoc />
    public IKeyValueBytesStore CreateKeyValueStore(StateStoreContext context) =>
        new ZoneTreeKeyValueStore(ZoneTreeStore.Open(context));

    /// <inheritdoc />
    public IWindowBytesStore CreateWindowStore(StateStoreContext context) =>
        new ZoneTreeWindowStore(ZoneTreeStore.Open(context));
}

/// <summary>
/// A ZoneTree of byte keys and values with its background maintainer. ZoneTree reads an empty value as a
/// deleted record, so every stored value carries a one-byte prefix and is never empty.
/// </summary>
internal sealed class ZoneTreeStore : IDisposable
{
    private const byte ValuePrefix = 1;
    private readonly IZoneTree<Memory<byte>, Memory<byte>> _tree;
    private readonly IMaintainer _maintainer;
    private byte[] _lookupKey = new byte[64];

    private ZoneTreeStore(IZoneTree<Memory<byte>, Memory<byte>> tree)
    {
        _tree = tree;
        _maintainer = tree.CreateMaintainer();
    }

    public static ZoneTreeStore Open(StateStoreContext context)
    {
        if (context.Directory is null)
        {
            throw new StreamsException(
                $"Store '{context.StoreName}' has no directory. ZoneTree state needs KafkaStreamsBuilder.UseZoneTreeState.");
        }

        return new ZoneTreeStore(new ZoneTreeFactory<Memory<byte>, Memory<byte>>()
            .SetDataDirectory(context.Directory)
            .SetComparer(new ByteArrayLexicographicComparerAscending())
            .SetKeySerializer(new ByteArraySerializer())
            .SetValueSerializer(new ByteArraySerializer())
            .OpenOrCreate());
    }

    public long Count => _tree.CountFullScan();

    /// <summary>Wraps a key for a lookup. The buffer is reused, so the result is valid until the next call.</summary>
    public Memory<byte> LookupKey(int length)
    {
        if (_lookupKey.Length < length)
        {
            _lookupKey = new byte[Math.Max(length, _lookupKey.Length * 2)];
        }

        return _lookupKey.AsMemory(0, length);
    }

    public bool TryGet(Memory<byte> key, out ReadOnlyMemory<byte> value)
    {
        if (_tree.TryGet(in key, out var stored))
        {
            value = stored[1..];
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Stores a value. The tree keeps <paramref name="key"/>, so it must not be reused.</summary>
    public void Put(Memory<byte> key, ReadOnlySpan<byte> value)
    {
        Memory<byte> stored = new byte[value.Length + 1];
        stored.Span[0] = ValuePrefix;
        value.CopyTo(stored.Span[1..]);
        _tree.Upsert(in key, in stored);
    }

    /// <summary>Deletes a key. The tree keeps <paramref name="key"/>, so it must not be reused.</summary>
    public void Delete(Memory<byte> key) => _tree.ForceDelete(in key);

    public IZoneTreeIterator<Memory<byte>, Memory<byte>> CreateIterator() => _tree.CreateIterator();

    public static ReadOnlyMemory<byte> ValueOf(Memory<byte> stored) => stored[1..];

    public void Dispose()
    {
        _maintainer.WaitForBackgroundThreads();
        _maintainer.Dispose();
        _tree.Dispose();
    }
}

internal sealed class ZoneTreeKeyValueStore(ZoneTreeStore store) : IKeyValueBytesStore
{
    public long Count => store.Count;

    public bool TryGet(ReadOnlySpan<byte> key, out ReadOnlyMemory<byte> value)
    {
        var lookup = store.LookupKey(key.Length);
        key.CopyTo(lookup.Span);
        return store.TryGet(lookup, out value);
    }

    public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value) => store.Put(key.ToArray(), value);

    public bool Delete(ReadOnlySpan<byte> key)
    {
        var existed = TryGet(key, out _);
        if (existed)
        {
            store.Delete(key.ToArray());
        }

        return existed;
    }

    public void Dispose() => store.Dispose();
}

/// <summary>
/// Window entries keyed by the window start as a big-endian 64-bit integer followed by the record key, so
/// the tree orders them by window and closed windows are a prefix of it.
/// </summary>
internal sealed class ZoneTreeWindowStore : IWindowBytesStore
{
    private readonly ZoneTreeStore _store;
    private readonly List<Memory<byte>> _closing = [];

    // The smallest window start in the store, or long.MaxValue when it is empty. Stream time advances far
    // more often than windows close, and this answers "nothing to close" without opening an iterator.
    private long _earliestStart = long.MaxValue;

    public ZoneTreeWindowStore(ZoneTreeStore store)
    {
        _store = store;
        using var iterator = store.CreateIterator();
        if (iterator.Next())
        {
            _earliestStart = BinaryPrimitives.ReadInt64BigEndian(iterator.CurrentKey.Span);
        }
    }

    public long Count => _store.Count;

    public bool TryGet(ReadOnlySpan<byte> key, long windowStart, out ReadOnlyMemory<byte> value)
    {
        var lookup = _store.LookupKey(sizeof(long) + key.Length);
        Compose(lookup.Span, key, windowStart);
        return _store.TryGet(lookup, out value);
    }

    public void Put(ReadOnlySpan<byte> key, long windowStart, ReadOnlySpan<byte> value)
    {
        _store.Put(Compose(key, windowStart), value);
        if (windowStart < _earliestStart)
        {
            _earliestStart = windowStart;
        }
    }

    public bool Delete(ReadOnlySpan<byte> key, long windowStart)
    {
        var existed = TryGet(key, windowStart, out _);
        if (existed)
        {
            _store.Delete(Compose(key, windowStart));
        }

        return existed;
    }

    public void RemoveThrough(long windowStart, IWindowEntryVisitor visitor)
    {
        if (windowStart < _earliestStart)
        {
            return;
        }

        _earliestStart = long.MaxValue;
        using (var iterator = _store.CreateIterator())
        {
            while (iterator.Next())
            {
                var composite = iterator.CurrentKey;
                var start = BinaryPrimitives.ReadInt64BigEndian(composite.Span);
                if (start > windowStart)
                {
                    _earliestStart = start;
                    break;
                }

                visitor.Visit(composite[sizeof(long)..], start, ZoneTreeStore.ValueOf(iterator.CurrentValue));
                _closing.Add(composite);
            }
        }

        // Deleted after the scan so the iterator never sees its own tree change.
        foreach (var composite in _closing)
        {
            _store.Delete(composite);
        }

        _closing.Clear();
    }

    public void Dispose() => _store.Dispose();

    private static Memory<byte> Compose(ReadOnlySpan<byte> key, long windowStart)
    {
        var composite = new byte[sizeof(long) + key.Length];
        Compose(composite, key, windowStart);
        return composite;
    }

    private static void Compose(Span<byte> destination, ReadOnlySpan<byte> key, long windowStart)
    {
        BinaryPrimitives.WriteInt64BigEndian(destination, windowStart);
        key.CopyTo(destination[sizeof(long)..]);
    }
}
