using Dekaf.Serialization;

namespace Dekaf.Streams.Internal;

/// <summary>Receives every record a task produces: sink output, repartition records and changelog writes.</summary>
internal interface IRecordCollector
{
    /// <param name="partition">The target partition, or -1 to partition by key.</param>
    void Send(
        string topic,
        int partition,
        ReadOnlySpan<byte> key,
        bool keyIsNull,
        ReadOnlySpan<byte> value,
        bool valueIsNull,
        long timestamp,
        Headers? headers);
}

/// <summary>A consumed record before deserialization.</summary>
internal readonly struct RawRecord(
    ReadOnlyMemory<byte> key,
    bool keyIsNull,
    ReadOnlyMemory<byte> value,
    bool valueIsNull,
    long timestamp,
    long offset,
    Headers? headers)
{
    public ReadOnlyMemory<byte> Key { get; } = key;

    public bool KeyIsNull { get; } = keyIsNull;

    public ReadOnlyMemory<byte> Value { get; } = value;

    public bool ValueIsNull { get; } = valueIsNull;

    public long Timestamp { get; } = timestamp;

    public long Offset { get; } = offset;

    public Headers? Headers { get; } = headers;
}

internal interface ISourceProcessor
{
    void Process(in RawRecord record);
}

/// <summary>State shared by the nodes and stores of one task.</summary>
internal sealed class TaskContext(string applicationId, string subtopologyId, int partition, IRecordCollector collector)
{
    public string ApplicationId { get; } = applicationId;

    public string SubtopologyId { get; } = subtopologyId;

    public int Partition { get; } = partition;

    public IRecordCollector Collector { get; } = collector;

    /// <summary>Timestamp of the record being processed; changelog writes carry it.</summary>
    public long Timestamp { get; set; }

    /// <summary>Records skipped because they had a null key or arrived after their window closed.</summary>
    public long DroppedRecords { get; set; }
}

/// <summary>Builds the runtime nodes and stores of one task from the topology graph.</summary>
internal sealed class TaskBuilder(TaskContext context, IStateStoreEngine engine, string? stateDirectory)
{
    private readonly Dictionary<GraphNode, object> _built = [];

    public TaskContext Context { get; } = context;

    public Dictionary<string, object> Stores { get; } = [];

    public Dictionary<string, IRestorableStore> RestoreTargets { get; } = [];

    public List<IDisposable> Disposables { get; } = [];

    public bool TryGetBuilt<T>(GraphNode node, out T built) where T : class
    {
        if (_built.TryGetValue(node, out var value))
        {
            built = (T)value;
            return true;
        }

        built = null!;
        return false;
    }

    public void SetBuilt(GraphNode node, object built) => _built.Add(node, built);

    /// <param name="changelogTopic">Topic that receives every write, or null when writes are not logged.</param>
    /// <param name="restoreTopic">Topic the store is rebuilt from, or null when it is not restorable.</param>
    public KeyValueStore<TKey, TValue> AddKeyValueStore<TKey, TValue>(
        string name,
        ISerde<TKey> keySerde,
        ISerde<TValue> valueSerde,
        string? changelogTopic,
        string? restoreTopic)
    {
        var bytes = engine.CreateKeyValueStore(StoreContext(name));
        var store = new KeyValueStore<TKey, TValue>(name, bytes, keySerde, valueSerde, Context, changelogTopic);
        Register(name, store, bytes, restoreTopic);
        return store;
    }

    public WindowStore<TKey, TValue> AddWindowStore<TKey, TValue>(
        string name,
        ISerde<TKey> keySerde,
        ISerde<TValue> valueSerde,
        string changelogTopic)
    {
        var bytes = engine.CreateWindowStore(StoreContext(name));
        var store = new WindowStore<TKey, TValue>(bytes, keySerde, valueSerde, Context, changelogTopic);
        Register(name, store, bytes, changelogTopic);
        return store;
    }

    public KeyValueStore<TKey, TValue> GetKeyValueStore<TKey, TValue>(string name)
    {
        if (!Stores.TryGetValue(name, out var store))
        {
            throw new StreamsException($"State store '{name}' does not exist in subtopology {Context.SubtopologyId}.");
        }

        return store as KeyValueStore<TKey, TValue>
            ?? throw new StreamsException(
                $"State store '{name}' is not a key-value store of <{typeof(TKey).Name}, {typeof(TValue).Name}>.");
    }

    private StateStoreContext StoreContext(string name) =>
        new(
            Context.ApplicationId,
            name,
            Context.SubtopologyId,
            Context.Partition,
            stateDirectory is null ? null : Path.Combine(stateDirectory, name));

    private void Register(string name, IRestorableStore store, IDisposable bytes, string? restoreTopic)
    {
        Stores.Add(name, store);
        Disposables.Add(bytes);
        if (restoreTopic is not null)
        {
            RestoreTargets.Add(restoreTopic, store);
        }
    }
}

/// <summary>
/// Runs one subtopology for one partition. Not thread-safe: a task is driven by a single lane.
/// </summary>
internal sealed class StreamTask : IDisposable
{
    private readonly Dictionary<string, ISourceProcessor> _sources = [];
    private readonly Dictionary<string, object> _stores;
    private readonly Dictionary<string, IRestorableStore> _restoreTargets;
    private readonly List<IDisposable> _disposables;
    private readonly Func<ProcessingError, ErrorResponse>? _errorHandler;
    private readonly TaskContext _context;

    public StreamTask(
        Topology topology,
        int subtopology,
        int partition,
        IRecordCollector collector,
        IStateStoreEngine? engine = null,
        string? stateDirectory = null)
    {
        var description = topology.Subtopologies[subtopology];
        _context = new TaskContext(topology.ApplicationId, description.Id, partition, collector);
        _errorHandler = topology.ErrorHandler;

        var builder = new TaskBuilder(_context, engine ?? InMemoryStateStoreEngine.Instance, stateDirectory);
        var nodes = topology.NodesBySubtopology[subtopology];

        // Stores first: a join reads the store of a table node that may be built after it.
        foreach (var node in nodes)
        {
            node.CreateStores(builder);
        }

        foreach (var node in nodes)
        {
            if (node is ISourceNode source)
            {
                _sources.Add(source.Topic, source.BuildSource(builder));
            }
        }

        _stores = builder.Stores;
        _restoreTargets = builder.RestoreTargets;
        _disposables = builder.Disposables;
    }

    public long DroppedRecords => _context.DroppedRecords;

    /// <summary>The topics this task consumes: source topics and internal repartition topics.</summary>
    public IReadOnlyCollection<string> SourceTopics => _sources.Keys;

    /// <summary>The topics this task's stores are rebuilt from.</summary>
    public IReadOnlyCollection<string> RestoreTopics => _restoreTargets.Keys;

    public void Process(string topic, in RawRecord record)
    {
        if (!_sources.TryGetValue(topic, out var source))
        {
            throw new StreamsException($"Subtopology {_context.SubtopologyId} does not read topic '{topic}'.");
        }

        _context.Timestamp = record.Timestamp;
        try
        {
            source.Process(in record);
        }
        catch (Exception exception)
        {
            HandleError(exception, topic, in record);
        }
    }

    /// <summary>Applies one changelog record to the store it belongs to, without logging it again.</summary>
    public void Restore(string changelogTopic, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, bool valueIsNull)
    {
        if (!_restoreTargets.TryGetValue(changelogTopic, out var store))
        {
            throw new StreamsException($"No state store of subtopology {_context.SubtopologyId} restores from '{changelogTopic}'.");
        }

        store.Restore(key, value, valueIsNull);
    }

    public object? GetStore(string name) => _stores.GetValueOrDefault(name);

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private void HandleError(Exception exception, string topic, in RawRecord record)
    {
        var response = _errorHandler is null
            ? ErrorResponse.Fail
            : _errorHandler(new ProcessingError(exception, topic, _context.Partition, record.Offset));

        if (!response.IsSkip)
        {
            throw new StreamsException(
                $"Processing failed for {topic}[{_context.Partition}]@{record.Offset} in subtopology {_context.SubtopologyId}.",
                exception);
        }

        if (response.DeadLetterTopic is { } deadLetterTopic)
        {
            _context.Collector.Send(
                deadLetterTopic,
                partition: -1,
                record.Key.Span,
                record.KeyIsNull,
                record.Value.Span,
                record.ValueIsNull,
                record.Timestamp,
                record.Headers);
        }
    }
}
