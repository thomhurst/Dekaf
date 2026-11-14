using System.Buffers;
using Dekaf.Serialization;
using Dekaf.Streams.Internal;

namespace Dekaf.Streams.Testing;

/// <summary>
/// Runs a topology in memory, without a broker, for tests. Every topic has one partition, records are
/// processed synchronously in the order they are piped, and a record written to an internal repartition
/// topic is processed by the downstream subtopology before <c>Pipe</c> returns.
/// </summary>
public sealed class TopologyTestDriver : IDisposable
{
    private readonly Topology _topology;
    private readonly StreamTask[] _tasks;
    private readonly Dictionary<string, Queue<CapturedRecord>> _written = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _nextOffsets = new(StringComparer.Ordinal);
    private readonly Queue<(string Topic, CapturedRecord Record)> _pending = new();

    /// <summary>Creates a driver with one task per subtopology.</summary>
    public TopologyTestDriver(Topology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        _topology = topology;
        var collector = new Collector(this);
        _tasks = new StreamTask[topology.Subtopologies.Count];
        for (var i = 0; i < _tasks.Length; i++)
        {
            _tasks[i] = new StreamTask(topology, i, partition: 0, collector);
        }
    }

    /// <summary>Gets how many records were skipped for a null key or for arriving after their window closed.</summary>
    public long DroppedRecordCount
    {
        get
        {
            long dropped = 0;
            foreach (var task in _tasks)
            {
                dropped += task.DroppedRecords;
            }

            return dropped;
        }
    }

    /// <summary>Creates a writer for a topic the topology reads.</summary>
    public TestInputTopic<TKey, TValue> CreateInputTopic<TKey, TValue>(
        string topic,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer)
    {
        ArgumentNullException.ThrowIfNull(keySerializer);
        ArgumentNullException.ThrowIfNull(valueSerializer);
        if (_topology.SubtopologyOfSourceTopic(topic) < 0)
        {
            throw new ArgumentException($"The topology does not read topic '{topic}'.", nameof(topic));
        }

        return new TestInputTopic<TKey, TValue>(this, topic, keySerializer, valueSerializer);
    }

    /// <summary>
    /// Creates a reader for a topic the topology writes. Internal changelog and repartition topics can be
    /// read as well.
    /// </summary>
    public TestOutputTopic<TKey, TValue> CreateOutputTopic<TKey, TValue>(
        string topic,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(keyDeserializer);
        ArgumentNullException.ThrowIfNull(valueDeserializer);
        return new TestOutputTopic<TKey, TValue>(topic, WrittenTo(topic), keyDeserializer, valueDeserializer);
    }

    /// <summary>Gets a key-value store of the topology by name.</summary>
    public IReadOnlyKeyValueStore<TKey, TValue> GetKeyValueStore<TKey, TValue>(string name)
    {
        foreach (var task in _tasks)
        {
            if (task.GetStore(name) is { } store)
            {
                return store as IReadOnlyKeyValueStore<TKey, TValue>
                    ?? throw new ArgumentException(
                        $"Store '{name}' is not a key-value store of <{typeof(TKey).Name}, {typeof(TValue).Name}>.",
                        nameof(name));
            }
        }

        throw new ArgumentException($"The topology has no store named '{name}'.", nameof(name));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var task in _tasks)
        {
            task.Dispose();
        }
    }

    internal void Pipe(string topic, CapturedRecord record)
    {
        _pending.Enqueue((topic, record));
        while (_pending.TryDequeue(out var next))
        {
            var offset = _nextOffsets.GetValueOrDefault(next.Topic);
            _nextOffsets[next.Topic] = offset + 1;
            var raw = new RawRecord(
                next.Record.Key,
                next.Record.Key is null,
                next.Record.Value,
                next.Record.Value is null,
                next.Record.Timestamp,
                offset,
                next.Record.Headers);
            _tasks[_topology.SubtopologyOfSourceTopic(next.Topic)].Process(next.Topic, in raw);
        }
    }

    private Queue<CapturedRecord> WrittenTo(string topic)
    {
        if (!_written.TryGetValue(topic, out var records))
        {
            records = new Queue<CapturedRecord>();
            _written.Add(topic, records);
        }

        return records;
    }

    private sealed class Collector(TopologyTestDriver driver) : IRecordCollector
    {
        public void Send(
            string topic,
            int partition,
            ReadOnlySpan<byte> key,
            bool keyIsNull,
            ReadOnlySpan<byte> value,
            bool valueIsNull,
            long timestamp,
            Headers? headers)
        {
            var record = new CapturedRecord(
                keyIsNull ? null : key.ToArray(),
                valueIsNull ? null : value.ToArray(),
                timestamp,
                headers);
            driver.WrittenTo(topic).Enqueue(record);

            // A topic the topology also reads (a repartition topic) feeds the downstream subtopology.
            if (driver._topology.SubtopologyOfSourceTopic(topic) >= 0)
            {
                driver._pending.Enqueue((topic, record));
            }
        }
    }
}

internal sealed record CapturedRecord(byte[]? Key, byte[]? Value, long Timestamp, Headers? Headers);

/// <summary>Writes records to a source topic of a <see cref="TopologyTestDriver"/>.</summary>
public sealed class TestInputTopic<TKey, TValue>
{
    private readonly TopologyTestDriver _driver;
    private readonly string _topic;
    private readonly ISerializer<TKey> _keySerializer;
    private readonly ISerializer<TValue> _valueSerializer;

    internal TestInputTopic(
        TopologyTestDriver driver,
        string topic,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer)
    {
        _driver = driver;
        _topic = topic;
        _keySerializer = keySerializer;
        _valueSerializer = valueSerializer;
    }

    /// <summary>Writes a record and processes it. A null key or value is written as a null.</summary>
    /// <param name="timestamp">The event timestamp in Unix milliseconds.</param>
    public void Pipe(TKey key, TValue value, long timestamp = 0, Headers? headers = null) =>
        _driver.Pipe(_topic, new CapturedRecord(
            Serialize(_keySerializer, key, SerializationComponent.Key, headers),
            Serialize(_valueSerializer, value, SerializationComponent.Value, headers),
            timestamp,
            headers));

    /// <summary>Writes a record with a timestamp and processes it.</summary>
    public void Pipe(TKey key, TValue value, DateTimeOffset timestamp, Headers? headers = null) =>
        Pipe(key, value, timestamp.ToUnixTimeMilliseconds(), headers);

    private byte[]? Serialize<T>(ISerializer<T> serializer, T value, SerializationComponent component, Headers? headers)
    {
        if (value is null)
        {
            return null;
        }

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(
            value,
            ref buffer,
            new SerializationContext { Topic = _topic, Component = component, Headers = headers });
        return buffer.WrittenSpan.ToArray();
    }
}

/// <summary>Reads the records a <see cref="TopologyTestDriver"/> wrote to a topic, in order.</summary>
public sealed class TestOutputTopic<TKey, TValue>
{
    private readonly string _topic;
    private readonly Queue<CapturedRecord> _records;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;

    internal TestOutputTopic(
        string topic,
        Queue<CapturedRecord> records,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer)
    {
        _topic = topic;
        _records = records;
        _keyDeserializer = keyDeserializer;
        _valueDeserializer = valueDeserializer;
    }

    /// <summary>Gets the number of records not read yet.</summary>
    public int Count => _records.Count;

    /// <summary>Reads the next record. A null key or value is returned as <see langword="default"/>.</summary>
    public bool TryRead(out Record<TKey, TValue> record)
    {
        if (!_records.TryDequeue(out var captured))
        {
            record = default;
            return false;
        }

        var key = captured.Key is null
            ? default!
            : _keyDeserializer.Deserialize(
                captured.Key,
                new SerializationContext { Topic = _topic, Component = SerializationComponent.Key, Headers = captured.Headers });
        var value = captured.Value is null
            ? default!
            : _valueDeserializer.Deserialize(
                captured.Value,
                new SerializationContext
                {
                    Topic = _topic,
                    Component = SerializationComponent.Value,
                    Headers = captured.Headers,
                    KeyData = captured.Key
                });
        record = new Record<TKey, TValue>(key, value, captured.Timestamp, captured.Headers);
        return true;
    }

    /// <summary>Reads every record not read yet.</summary>
    public IReadOnlyList<Record<TKey, TValue>> ReadAll()
    {
        var records = new List<Record<TKey, TValue>>(_records.Count);
        while (TryRead(out var record))
        {
            records.Add(record);
        }

        return records;
    }
}
