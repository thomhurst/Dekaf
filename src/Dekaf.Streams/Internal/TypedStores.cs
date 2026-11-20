using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Dekaf.Serialization;

namespace Dekaf.Streams.Internal;

internal interface IRestorableStore
{
    void Restore(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, bool valueIsNull);
}

internal sealed class KeyValueStoreDefinition<TKey, TValue>(string name, ISerde<TKey> keySerde, ISerde<TValue> valueSerde)
    : StoreDefinition(name)
{
    internal override void Create(TaskBuilder builder, string? changelogTopic) =>
        builder.AddKeyValueStore(Name, keySerde, valueSerde, changelogTopic, restoreTopic: changelogTopic);
}

/// <summary>Typed view over a byte store. Serializes into buffers it reuses, and logs each write.</summary>
internal sealed class KeyValueStore<TKey, TValue> : IKeyValueStore<TKey, TValue>, IRestorableStore
{
    private readonly IKeyValueBytesStore _bytes;
    private readonly ISerde<TKey> _keySerde;
    private readonly ISerde<TValue> _valueSerde;
    private readonly TaskContext _context;
    private readonly string? _changelogTopic;
    private readonly SerializationContext _keyContext;
    private readonly SerializationContext _valueContext;
    private readonly ReusableBufferWriter _keyBuffer = new();
    private readonly ReusableBufferWriter _valueBuffer = new();

    public KeyValueStore(
        string name,
        IKeyValueBytesStore bytes,
        ISerde<TKey> keySerde,
        ISerde<TValue> valueSerde,
        TaskContext context,
        string? changelogTopic,
        string? restoreTopic)
    {
        Name = name;
        _bytes = bytes;
        _keySerde = keySerde;
        _valueSerde = valueSerde;
        _context = context;
        _changelogTopic = changelogTopic;

        // The bytes in the store are the bytes of the topic it is restored from, so serdes are given that
        // topic. A serde that encodes per topic then reads restored entries the way it wrote them.
        var topic = changelogTopic ?? restoreTopic ?? name;
        _keyContext = new SerializationContext { Topic = topic, Component = SerializationComponent.Key };
        _valueContext = new SerializationContext { Topic = topic, Component = SerializationComponent.Value };
    }

    public string Name
    {
        get;
    }

    public long Count => _bytes.Count;

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_bytes.TryGet(SerializeKey(key), out var bytes))
        {
            value = _valueSerde.Deserialize(bytes, _valueContext with { KeyData = _keyBuffer.WrittenMemory });
            return true;
        }

        value = default;
        return false;
    }

    public void Put(TKey key, TValue value)
    {
        if (value is null)
        {
            Delete(key);
            return;
        }

        var keyBytes = SerializeKey(key);
        _valueBuffer.Reset();
        var writer = _valueBuffer;
        _valueSerde.Serialize(value, ref writer, _valueContext);
        var valueBytes = _valueBuffer.WrittenSpan;

        _bytes.Put(keyBytes, valueBytes);
        if (_changelogTopic is not null)
        {
            _context.Collector.Send(
                _changelogTopic, _context.Partition, keyBytes, false, valueBytes, false, _context.Timestamp, headers: null);
        }
    }

    public bool Delete(TKey key)
    {
        var keyBytes = SerializeKey(key);
        var existed = _bytes.Delete(keyBytes);
        if (_changelogTopic is not null)
        {
            _context.Collector.Send(
                _changelogTopic, _context.Partition, keyBytes, false, default, true, _context.Timestamp, headers: null);
        }

        return existed;
    }

    public void Restore(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, bool valueIsNull)
    {
        if (valueIsNull)
        {
            _bytes.Delete(key);
        }
        else
        {
            _bytes.Put(key, value);
        }
    }

    private ReadOnlySpan<byte> SerializeKey(TKey key)
    {
        if (key is null)
        {
            throw new StreamsException($"State store '{Name}' does not accept a null key.");
        }

        _keyBuffer.Reset();
        var writer = _keyBuffer;
        _keySerde.Serialize(key, ref writer, _keyContext);
        return _keyBuffer.WrittenSpan;
    }
}

internal interface IClosedWindowHandler<TKey, TValue>
{
    void OnClosed(TKey key, long windowStart, TValue value);
}

/// <summary>
/// Typed view over a window byte store. The changelog key is the serialized key followed by the window
/// start as a big-endian 64-bit integer.
/// </summary>
internal sealed class WindowStore<TKey, TValue> : IRestorableStore, IWindowEntryVisitor
{
    private readonly IWindowBytesStore _bytes;
    private readonly ISerde<TKey> _keySerde;
    private readonly ISerde<TValue> _valueSerde;
    private readonly TaskContext _context;
    private readonly string _changelogTopic;
    private readonly SerializationContext _keyContext;
    private readonly SerializationContext _valueContext;
    private readonly ReusableBufferWriter _keyBuffer = new();
    private readonly ReusableBufferWriter _valueBuffer = new();
    private readonly ReusableBufferWriter _closedKeyBuffer = new();
    // No window starts here, and no key is empty with it: the entry that remembers ClosedThrough.
    private const long ClosedMarkerStart = long.MaxValue;

    private IClosedWindowHandler<TKey, TValue>? _closedHandler;
    private int _keyLength;

    public WindowStore(
        IWindowBytesStore bytes,
        ISerde<TKey> keySerde,
        ISerde<TValue> valueSerde,
        TaskContext context,
        string changelogTopic)
    {
        _bytes = bytes;
        _keySerde = keySerde;
        _valueSerde = valueSerde;
        _context = context;
        _changelogTopic = changelogTopic;
        _keyContext = new SerializationContext { Topic = changelogTopic, Component = SerializationComponent.Key };
        _valueContext = new SerializationContext { Topic = changelogTopic, Component = SerializationComponent.Value };
    }

    public long Count => _bytes.Count;

    /// <summary>Selects the key that the following <see cref="TryGet"/> and <see cref="Put"/> calls use.</summary>
    public void SetKey(TKey key)
    {
        _keyBuffer.Reset();
        var writer = _keyBuffer;
        _keySerde.Serialize(key, ref writer, _keyContext);
        _keyLength = _keyBuffer.WrittenCount;
    }

    public bool TryGet(long windowStart, [MaybeNullWhen(false)] out TValue value)
    {
        if (_bytes.TryGet(_keyBuffer.WrittenSpan, windowStart, out var bytes))
        {
            value = _valueSerde.Deserialize(bytes, _valueContext with { KeyData = _keyBuffer.WrittenMemory });
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// The highest window start this store has closed, or -1. It is kept in the store under a reserved
    /// entry, so it is logged and restored with the windows and closed windows stay closed after a restart.
    /// </summary>
    public long ClosedThrough
    {
        get
        {
            return _bytes.TryGet(default, ClosedMarkerStart, out var bytes)
                ? BinaryPrimitives.ReadInt64BigEndian(bytes.Span)
                : -1;
        }
        set
        {
            Span<byte> marker = stackalloc byte[sizeof(long)];
            Span<byte> markerKey = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(marker, value);
            BinaryPrimitives.WriteInt64BigEndian(markerKey, ClosedMarkerStart);
            _bytes.Put(default, ClosedMarkerStart, marker);
            _context.Collector.Send(
                _changelogTopic, _context.Partition, markerKey, false, marker, false, _context.Timestamp, headers: null);
        }
    }

    /// <summary>Writes the aggregate of the selected key for a window. A null aggregate deletes the entry.</summary>
    public void Put(long windowStart, TValue value)
    {
        if (value is null)
        {
            _bytes.Delete(_keyBuffer.WrittenSpan, windowStart);
            BinaryPrimitives.WriteInt64BigEndian(_keyBuffer.GetSpan(sizeof(long)), windowStart);
            _keyBuffer.Advance(sizeof(long));
            _context.Collector.Send(
                _changelogTopic, _context.Partition, _keyBuffer.WrittenSpan, false, default, true, _context.Timestamp, headers: null);
            _keyBuffer.Truncate(_keyLength);
            return;
        }

        _valueBuffer.Reset();
        var writer = _valueBuffer;
        _valueSerde.Serialize(value, ref writer, _valueContext);
        var valueBytes = _valueBuffer.WrittenSpan;

        _bytes.Put(_keyBuffer.WrittenSpan, windowStart, valueBytes);

        // The changelog key is the key followed by the window start.
        BinaryPrimitives.WriteInt64BigEndian(_keyBuffer.GetSpan(sizeof(long)), windowStart);
        _keyBuffer.Advance(sizeof(long));
        _context.Collector.Send(
            _changelogTopic, _context.Partition, _keyBuffer.WrittenSpan, false, valueBytes, false, _context.Timestamp, headers: null);
        _keyBuffer.Truncate(_keyLength);
    }

    /// <summary>
    /// Removes every window that starts at or before <paramref name="windowStart"/>, writing a changelog
    /// tombstone for each entry and passing it to <paramref name="handler"/> when one is given.
    /// </summary>
    public void CloseThrough(long windowStart, IClosedWindowHandler<TKey, TValue>? handler)
    {
        _closedHandler = handler;
        _bytes.RemoveThrough(windowStart, this);
    }

    void IWindowEntryVisitor.Visit(ReadOnlyMemory<byte> key, long windowStart, ReadOnlyMemory<byte> value)
    {
        _closedKeyBuffer.Reset();
        key.Span.CopyTo(_closedKeyBuffer.GetSpan(key.Length));
        _closedKeyBuffer.Advance(key.Length);
        BinaryPrimitives.WriteInt64BigEndian(_closedKeyBuffer.GetSpan(sizeof(long)), windowStart);
        _closedKeyBuffer.Advance(sizeof(long));
        _context.Collector.Send(
            _changelogTopic,
            _context.Partition,
            _closedKeyBuffer.WrittenSpan,
            false,
            default,
            true,
            _context.Timestamp,
            headers: null);

        _closedHandler?.OnClosed(
            _keySerde.Deserialize(key, _keyContext),
            windowStart,
            _valueSerde.Deserialize(value, _valueContext with { KeyData = key }));
    }

    public void Restore(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, bool valueIsNull)
    {
        if (key.Length < sizeof(long))
        {
            throw new StreamsException($"A record in '{_changelogTopic}' has no window start in its key.");
        }

        var keyLength = key.Length - sizeof(long);
        var windowStart = BinaryPrimitives.ReadInt64BigEndian(key[keyLength..]);
        if (valueIsNull)
        {
            _bytes.Delete(key[..keyLength], windowStart);
        }
        else
        {
            _bytes.Put(key[..keyLength], windowStart, value);
        }
    }
}
