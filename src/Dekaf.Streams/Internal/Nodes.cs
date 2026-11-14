using Dekaf.Serialization;

namespace Dekaf.Streams.Internal;

internal enum NodeKind
{
    Source,
    Processor,
    Sink
}

/// <summary>A store a node owns, with the internal topic that logs it.</summary>
/// <param name="ChangelogTopic">The internal changelog topic, or null when the store is rebuilt from a source topic.</param>
/// <param name="RetentionMs">Changelog retention for window stores; zero for key-value stores.</param>
internal sealed record StoreInfo(string Name, string? ChangelogTopic, long RetentionMs = 0);

/// <summary>A node of the topology graph. It is immutable once the topology is built and shared by all tasks.</summary>
internal abstract class GraphNode(string name)
{
    public int Id { get; set; }

    public string Name { get; } = name;

    public List<GraphNode> Parents { get; } = [];

    public List<GraphNode> Children { get; } = [];

    /// <summary>Nodes whose stores this node reads; they must run in the same subtopology.</summary>
    public List<GraphNode> Dependencies { get; } = [];

    public List<StoreInfo> Stores { get; } = [];

    public int Subtopology { get; set; } = -1;

    public virtual NodeKind Kind => NodeKind.Processor;

    public virtual string? SourceTopic => null;

    public virtual string? SinkTopic => null;

    /// <summary>Whether the source or sink topic is an internal repartition topic.</summary>
    public virtual bool IsRepartition => false;

    public virtual void CreateStores(TaskBuilder builder)
    {
    }
}

internal interface ISourceNode
{
    string Topic { get; }

    ISourceProcessor BuildSource(TaskBuilder builder);
}

/// <summary>Implemented by nodes that consume records of one type.</summary>
internal interface IReceiverFactory<TKey, TValue>
{
    Receiver<TKey, TValue> Build(TaskBuilder builder);
}

internal abstract class Receiver<TKey, TValue>
{
    public abstract void Process(in Record<TKey, TValue> record);
}

/// <summary>Forwards a record to every downstream receiver of a node.</summary>
internal sealed class Emitter<TKey, TValue>(Receiver<TKey, TValue>[] targets) : IForwarder<TKey, TValue>
{
    public void Forward(in Record<TKey, TValue> record)
    {
        foreach (var target in targets)
        {
            target.Process(in record);
        }
    }
}

/// <summary>A node that emits records of one type.</summary>
internal abstract class StreamNode<TKey, TValue>(string name) : GraphNode(name)
{
    private readonly List<IReceiverFactory<TKey, TValue>> _downstream = [];

    public void AddDownstream<TNode>(TNode node) where TNode : GraphNode, IReceiverFactory<TKey, TValue>
    {
        _downstream.Add(node);
        Children.Add(node);
        node.Parents.Add(this);
    }

    protected Emitter<TKey, TValue> BuildEmitter(TaskBuilder builder)
    {
        var targets = new Receiver<TKey, TValue>[_downstream.Count];
        for (var i = 0; i < targets.Length; i++)
        {
            targets[i] = _downstream[i].Build(builder);
        }

        return new Emitter<TKey, TValue>(targets);
    }
}

/// <summary>A node that consumes one record type and emits another.</summary>
internal abstract class OperatorNode<TKeyIn, TValueIn, TKeyOut, TValueOut>(string name)
    : StreamNode<TKeyOut, TValueOut>(name), IReceiverFactory<TKeyIn, TValueIn>
{
    public Receiver<TKeyIn, TValueIn> Build(TaskBuilder builder)
    {
        // A node with several parents (a merge) is built once per task.
        if (!builder.TryGetBuilt<Receiver<TKeyIn, TValueIn>>(this, out var receiver))
        {
            receiver = Create(builder, BuildEmitter(builder));
            builder.SetBuilt(this, receiver);
        }

        return receiver;
    }

    protected abstract Receiver<TKeyIn, TValueIn> Create(TaskBuilder builder, Emitter<TKeyOut, TValueOut> output);
}

internal static class SourceReader
{
    public static Record<TKey, TValue> Read<TKey, TValue>(
        string topic,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        in RawRecord raw)
    {
        var key = raw.KeyIsNull
            ? default!
            : keyDeserializer.Deserialize(
                raw.Key,
                new SerializationContext { Topic = topic, Component = SerializationComponent.Key, Headers = raw.Headers });

        var value = raw.ValueIsNull
            ? default!
            : valueDeserializer.Deserialize(
                raw.Value,
                new SerializationContext
                {
                    Topic = topic,
                    Component = SerializationComponent.Value,
                    Headers = raw.Headers,
                    // A non-null empty key must not read as a null key.
                    KeyData = raw.KeyIsNull ? default : raw.Key.IsEmpty ? Array.Empty<byte>() : raw.Key
                });

        return new Record<TKey, TValue>(key, value, raw.Timestamp, raw.Headers);
    }
}

internal sealed class SourceNode<TKey, TValue>(
    string name,
    string topic,
    IDeserializer<TKey> keyDeserializer,
    IDeserializer<TValue> valueDeserializer,
    bool isRepartition) : StreamNode<TKey, TValue>(name), ISourceNode
{
    public string Topic => topic;

    public override NodeKind Kind => NodeKind.Source;

    public override string? SourceTopic => topic;

    public override bool IsRepartition => isRepartition;

    public ISourceProcessor BuildSource(TaskBuilder builder) =>
        new Processor(topic, keyDeserializer, valueDeserializer, BuildEmitter(builder));

    private sealed class Processor(
        string topic,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        Emitter<TKey, TValue> output) : ISourceProcessor
    {
        public void Process(in RawRecord record) =>
            output.Forward(SourceReader.Read(topic, keyDeserializer, valueDeserializer, in record));
    }
}

/// <summary>Reads a topic into a store, treating a null value as a delete, and emits each update.</summary>
internal sealed class TableSourceNode<TKey, TValue> : StreamNode<TKey, TValue>, ISourceNode
{
    private readonly string _storeName;
    private readonly ISerde<TKey> _keySerde;
    private readonly ISerde<TValue> _valueSerde;

    public TableSourceNode(string name, string topic, string storeName, ISerde<TKey> keySerde, ISerde<TValue> valueSerde)
        : base(name)
    {
        Topic = topic;
        _storeName = storeName;
        _keySerde = keySerde;
        _valueSerde = valueSerde;

        // The source topic already is the changelog.
        Stores.Add(new StoreInfo(storeName, ChangelogTopic: null));
    }

    public string Topic { get; }

    public override NodeKind Kind => NodeKind.Source;

    public override string? SourceTopic => Topic;

    public override void CreateStores(TaskBuilder builder) =>
        builder.AddKeyValueStore(_storeName, _keySerde, _valueSerde, changelogTopic: null, restoreTopic: Topic);

    public ISourceProcessor BuildSource(TaskBuilder builder) =>
        new Processor(this, builder.GetKeyValueStore<TKey, TValue>(_storeName), builder.Context, BuildEmitter(builder));

    private sealed class Processor(
        TableSourceNode<TKey, TValue> node,
        KeyValueStore<TKey, TValue> store,
        TaskContext context,
        Emitter<TKey, TValue> output) : ISourceProcessor
    {
        public void Process(in RawRecord raw)
        {
            if (raw.KeyIsNull)
            {
                context.DroppedRecords++;
                return;
            }

            var record = SourceReader.Read(node.Topic, node._keySerde, node._valueSerde, in raw);
            if (raw.ValueIsNull)
            {
                store.Delete(record.Key);
            }
            else
            {
                store.Put(record.Key, record.Value);
            }

            output.Forward(in record);
        }
    }
}

internal sealed class SinkNode<TKey, TValue>(
    string name,
    string topic,
    ISerializer<TKey> keySerializer,
    ISerializer<TValue> valueSerializer,
    bool isRepartition) : GraphNode(name), IReceiverFactory<TKey, TValue>
{
    public override NodeKind Kind => NodeKind.Sink;

    public override string? SinkTopic => topic;

    public override bool IsRepartition => isRepartition;

    public Receiver<TKey, TValue> Build(TaskBuilder builder) =>
        new Sink(topic, keySerializer, valueSerializer, builder.Context.Collector);

    private sealed class Sink(
        string topic,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        IRecordCollector collector) : Receiver<TKey, TValue>
    {
        private readonly ReusableBufferWriter _keyBuffer = new();
        private readonly ReusableBufferWriter _valueBuffer = new();

        public override void Process(in Record<TKey, TValue> record)
        {
            // Written as branches, not as "x = y is null": unoptimized code boxes a value type for the
            // latter, which would allocate per record until the method is re-jitted.
            var keyIsNull = true;
            _keyBuffer.Reset();
            if (record.Key is not null)
            {
                keyIsNull = false;
                var writer = _keyBuffer;
                keySerializer.Serialize(
                    record.Key,
                    ref writer,
                    new SerializationContext { Topic = topic, Component = SerializationComponent.Key, Headers = record.Headers });
            }

            var valueIsNull = true;
            _valueBuffer.Reset();
            if (record.Value is not null)
            {
                valueIsNull = false;
                var writer = _valueBuffer;
                valueSerializer.Serialize(
                    record.Value,
                    ref writer,
                    new SerializationContext { Topic = topic, Component = SerializationComponent.Value, Headers = record.Headers });
            }

            collector.Send(
                topic,
                partition: -1,
                _keyBuffer.WrittenSpan,
                keyIsNull,
                _valueBuffer.WrittenSpan,
                valueIsNull,
                record.Timestamp,
                record.Headers);
        }
    }
}

internal sealed class WhereNode<TKey, TValue>(string name, Func<TKey, TValue, bool> predicate)
    : OperatorNode<TKey, TValue, TKey, TValue>(name)
{
    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TKey, TValue> output) =>
        new Where(predicate, output);

    private sealed class Where(Func<TKey, TValue, bool> predicate, Emitter<TKey, TValue> output) : Receiver<TKey, TValue>
    {
        public override void Process(in Record<TKey, TValue> record)
        {
            if (predicate(record.Key, record.Value))
            {
                output.Forward(in record);
            }
        }
    }
}

internal sealed class SelectNode<TKey, TValue, TResult>(string name, Func<TKey, TValue, TResult> selector)
    : OperatorNode<TKey, TValue, TKey, TResult>(name)
{
    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TKey, TResult> output) =>
        new Select(selector, output);

    private sealed class Select(Func<TKey, TValue, TResult> selector, Emitter<TKey, TResult> output) : Receiver<TKey, TValue>
    {
        public override void Process(in Record<TKey, TValue> record) =>
            output.Forward(record.WithValue(selector(record.Key, record.Value)));
    }
}

internal sealed class SelectKeyNode<TKey, TValue, TNewKey>(string name, Func<TKey, TValue, TNewKey> selector)
    : OperatorNode<TKey, TValue, TNewKey, TValue>(name)
{
    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TNewKey, TValue> output) =>
        new SelectKey(selector, output);

    private sealed class SelectKey(Func<TKey, TValue, TNewKey> selector, Emitter<TNewKey, TValue> output)
        : Receiver<TKey, TValue>
    {
        public override void Process(in Record<TKey, TValue> record) =>
            output.Forward(record.WithKey(selector(record.Key, record.Value)));
    }
}

internal sealed class SelectManyNode<TKey, TValue, TResult>(string name, Action<TKey, TValue, IValueSink<TResult>> selector)
    : OperatorNode<TKey, TValue, TKey, TResult>(name)
{
    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TKey, TResult> output) =>
        new SelectMany(selector, output);

    private sealed class SelectMany(Action<TKey, TValue, IValueSink<TResult>> selector, Emitter<TKey, TResult> output)
        : Receiver<TKey, TValue>, IValueSink<TResult>
    {
        private Record<TKey, TValue> _current;

        public override void Process(in Record<TKey, TValue> record)
        {
            _current = record;
            selector(record.Key, record.Value, this);
        }

        public void Add(TResult value) => output.Forward(_current.WithValue(value));
    }
}

internal sealed class PeekNode<TKey, TValue>(string name, Action<TKey, TValue> action)
    : OperatorNode<TKey, TValue, TKey, TValue>(name)
{
    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TKey, TValue> output) =>
        new Peek(action, output);

    private sealed class Peek(Action<TKey, TValue> action, Emitter<TKey, TValue> output) : Receiver<TKey, TValue>
    {
        public override void Process(in Record<TKey, TValue> record)
        {
            action(record.Key, record.Value);
            output.Forward(in record);
        }
    }
}

internal sealed class MergeNode<TKey, TValue>(string name) : OperatorNode<TKey, TValue, TKey, TValue>(name)
{
    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TKey, TValue> output) =>
        new Merge(output);

    private sealed class Merge(Emitter<TKey, TValue> output) : Receiver<TKey, TValue>
    {
        public override void Process(in Record<TKey, TValue> record) => output.Forward(in record);
    }
}

internal sealed class JoinNode<TKey, TValue, TTableValue, TResult>(
    string name,
    string tableStoreName,
    Func<TValue, TTableValue, TResult>? innerJoiner,
    Func<TValue, TTableValue?, bool, TResult>? leftJoiner) : OperatorNode<TKey, TValue, TKey, TResult>(name)
{
    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TKey, TResult> output) =>
        new Join(innerJoiner, leftJoiner, builder.GetKeyValueStore<TKey, TTableValue>(tableStoreName), builder.Context, output);

    private sealed class Join(
        Func<TValue, TTableValue, TResult>? innerJoiner,
        Func<TValue, TTableValue?, bool, TResult>? leftJoiner,
        KeyValueStore<TKey, TTableValue> table,
        TaskContext context,
        Emitter<TKey, TResult> output) : Receiver<TKey, TValue>
    {
        public override void Process(in Record<TKey, TValue> record)
        {
            if (record.Key is null)
            {
                context.DroppedRecords++;
                return;
            }

            var found = table.TryGet(record.Key, out var tableValue);
            if (leftJoiner is not null)
            {
                output.Forward(record.WithValue(leftJoiner(record.Value, tableValue, found)));
            }
            else if (found)
            {
                output.Forward(record.WithValue(innerJoiner!(record.Value, tableValue!)));
            }
        }
    }
}

internal sealed class AggregateNode<TKey, TValue, TAggregate> : OperatorNode<TKey, TValue, TKey, TAggregate>
{
    private readonly ISerde<TKey> _keySerde;
    private readonly ISerde<TAggregate> _aggregateSerde;
    private readonly Func<TAggregate> _initializer;
    private readonly Func<TKey, TValue, TAggregate, TAggregate> _aggregator;
    private readonly StoreInfo _store;

    public AggregateNode(
        string name,
        string changelogTopic,
        ISerde<TKey> keySerde,
        ISerde<TAggregate> aggregateSerde,
        Func<TAggregate> initializer,
        Func<TKey, TValue, TAggregate, TAggregate> aggregator)
        : base(name)
    {
        _keySerde = keySerde;
        _aggregateSerde = aggregateSerde;
        _initializer = initializer;
        _aggregator = aggregator;
        _store = new StoreInfo(name, changelogTopic);
        Stores.Add(_store);
    }

    public override void CreateStores(TaskBuilder builder) =>
        builder.AddKeyValueStore(Name, _keySerde, _aggregateSerde, _store.ChangelogTopic, _store.ChangelogTopic);

    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TKey, TAggregate> output) =>
        new Aggregate(this, builder.GetKeyValueStore<TKey, TAggregate>(Name), builder.Context, output);

    private sealed class Aggregate(
        AggregateNode<TKey, TValue, TAggregate> node,
        KeyValueStore<TKey, TAggregate> store,
        TaskContext context,
        Emitter<TKey, TAggregate> output) : Receiver<TKey, TValue>
    {
        public override void Process(in Record<TKey, TValue> record)
        {
            if (record.Key is null)
            {
                context.DroppedRecords++;
                return;
            }

            if (!store.TryGet(record.Key, out var aggregate))
            {
                aggregate = node._initializer();
            }

            aggregate = node._aggregator(record.Key, record.Value, aggregate);
            store.Put(record.Key, aggregate);
            output.Forward(record.WithValue(aggregate));
        }
    }
}

internal sealed class ReduceNode<TKey, TValue> : OperatorNode<TKey, TValue, TKey, TValue>
{
    private readonly ISerde<TKey> _keySerde;
    private readonly ISerde<TValue> _valueSerde;
    private readonly Func<TValue, TValue, TValue> _reducer;
    private readonly StoreInfo _store;

    public ReduceNode(
        string name,
        string changelogTopic,
        ISerde<TKey> keySerde,
        ISerde<TValue> valueSerde,
        Func<TValue, TValue, TValue> reducer)
        : base(name)
    {
        _keySerde = keySerde;
        _valueSerde = valueSerde;
        _reducer = reducer;
        _store = new StoreInfo(name, changelogTopic);
        Stores.Add(_store);
    }

    public override void CreateStores(TaskBuilder builder) =>
        builder.AddKeyValueStore(Name, _keySerde, _valueSerde, _store.ChangelogTopic, _store.ChangelogTopic);

    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<TKey, TValue> output) =>
        new Reduce(_reducer, builder.GetKeyValueStore<TKey, TValue>(Name), builder.Context, output);

    private sealed class Reduce(
        Func<TValue, TValue, TValue> reducer,
        KeyValueStore<TKey, TValue> store,
        TaskContext context,
        Emitter<TKey, TValue> output) : Receiver<TKey, TValue>
    {
        public override void Process(in Record<TKey, TValue> record)
        {
            if (record.Key is null || record.Value is null)
            {
                context.DroppedRecords++;
                return;
            }

            var reduced = store.TryGet(record.Key, out var current) ? reducer(current, record.Value) : record.Value;
            store.Put(record.Key, reduced);
            output.Forward(record.WithValue(reduced));
        }
    }
}

internal sealed class WindowAggregateNode<TKey, TValue, TAggregate> : OperatorNode<TKey, TValue, Windowed<TKey>, TAggregate>
{
    private readonly TimeWindows _windows;
    private readonly ISerde<TKey> _keySerde;
    private readonly ISerde<TAggregate> _aggregateSerde;
    private readonly Func<TAggregate> _initializer;
    private readonly Func<TKey, TValue, TAggregate, TAggregate> _aggregator;
    private readonly StoreInfo _store;

    public WindowAggregateNode(
        string name,
        string changelogTopic,
        TimeWindows windows,
        ISerde<TKey> keySerde,
        ISerde<TAggregate> aggregateSerde,
        Func<TAggregate> initializer,
        Func<TKey, TValue, TAggregate, TAggregate> aggregator)
        : base(name)
    {
        _windows = windows;
        _keySerde = keySerde;
        _aggregateSerde = aggregateSerde;
        _initializer = initializer;
        _aggregator = aggregator;
        _store = new StoreInfo(name, changelogTopic, windows.SizeMs + windows.GraceMs);
        Stores.Add(_store);
    }

    public override void CreateStores(TaskBuilder builder) =>
        builder.AddWindowStore(Name, _keySerde, _aggregateSerde, _store.ChangelogTopic!);

    protected override Receiver<TKey, TValue> Create(TaskBuilder builder, Emitter<Windowed<TKey>, TAggregate> output) =>
        new WindowAggregate(this, (WindowStore<TKey, TAggregate>)builder.Stores[Name], builder.Context, output);

    private sealed class WindowAggregate(
        WindowAggregateNode<TKey, TValue, TAggregate> node,
        WindowStore<TKey, TAggregate> store,
        TaskContext context,
        Emitter<Windowed<TKey>, TAggregate> output) : Receiver<TKey, TValue>, IClosedWindowHandler<TKey, TAggregate>
    {
        private readonly long _size = node._windows.SizeMs;
        private readonly long _advance = node._windows.AdvanceMs;
        private readonly long _grace = node._windows.GraceMs;
        private readonly bool _emitOnClose = node._windows.IsEmitOnClose;
        private long _streamTime = long.MinValue;

        public override void Process(in Record<TKey, TValue> record)
        {
            var timestamp = record.Timestamp;
            if (record.Key is null || timestamp < 0)
            {
                context.DroppedRecords++;
                return;
            }

            var advanced = timestamp > _streamTime;
            if (advanced)
            {
                _streamTime = timestamp;
            }

            // A window is closed once stream time reaches its end plus the grace period.
            var closedEnd = _streamTime - _grace;
            var updated = false;
            store.SetKey(record.Key);
            for (var start = timestamp - (timestamp % _advance); start >= 0 && start > timestamp - _size; start -= _advance)
            {
                var end = start + _size;
                if (end <= closedEnd)
                {
                    continue;
                }

                if (!store.TryGet(start, out var aggregate))
                {
                    aggregate = node._initializer();
                }

                aggregate = node._aggregator(record.Key, record.Value, aggregate);
                store.Put(start, aggregate);
                updated = true;
                if (!_emitOnClose)
                {
                    output.Forward(new Record<Windowed<TKey>, TAggregate>(
                        new Windowed<TKey>(record.Key, start, end), aggregate, timestamp, record.Headers));
                }
            }

            if (!updated)
            {
                context.DroppedRecords++;
            }

            if (advanced)
            {
                store.CloseThrough(closedEnd - _size, _emitOnClose ? this : null);
            }
        }

        // A closed window's result carries the last timestamp the window covers.
        public void OnClosed(TKey key, long windowStart, TAggregate value) =>
            output.Forward(new Record<Windowed<TKey>, TAggregate>(
                new Windowed<TKey>(key, windowStart, windowStart + _size), value, windowStart + _size - 1));
    }
}

internal sealed class ProcessorNode<TKeyIn, TValueIn, TKeyOut, TValueOut> : OperatorNode<TKeyIn, TValueIn, TKeyOut, TValueOut>
{
    private readonly Func<IProcessor<TKeyIn, TValueIn, TKeyOut, TValueOut>> _factory;
    private readonly StoreDefinition[] _definitions;

    public ProcessorNode(
        string name,
        string applicationId,
        Func<IProcessor<TKeyIn, TValueIn, TKeyOut, TValueOut>> factory,
        StoreDefinition[] definitions)
        : base(name)
    {
        _factory = factory;
        _definitions = definitions;
        foreach (var definition in definitions)
        {
            Stores.Add(new StoreInfo(definition.Name, TopicNames.Changelog(applicationId, definition.Name)));
        }
    }

    public override void CreateStores(TaskBuilder builder)
    {
        for (var i = 0; i < _definitions.Length; i++)
        {
            _definitions[i].Create(builder, Stores[i].ChangelogTopic);
        }
    }

    protected override Receiver<TKeyIn, TValueIn> Create(TaskBuilder builder, Emitter<TKeyOut, TValueOut> output)
    {
        var processor = _factory()
            ?? throw new StreamsException($"The factory of processor '{Name}' returned null.");
        processor.Init(new Context(this, builder, output));
        return new Adapter(processor);
    }

    private sealed class Adapter(IProcessor<TKeyIn, TValueIn, TKeyOut, TValueOut> processor) : Receiver<TKeyIn, TValueIn>
    {
        public override void Process(in Record<TKeyIn, TValueIn> record) => processor.Process(in record);
    }

    private sealed class Context(
        ProcessorNode<TKeyIn, TValueIn, TKeyOut, TValueOut> node,
        TaskBuilder builder,
        Emitter<TKeyOut, TValueOut> output) : IProcessorContext<TKeyOut, TValueOut>
    {
        public string SubtopologyId => builder.Context.SubtopologyId;

        public int Partition => builder.Context.Partition;

        public void Forward(in Record<TKeyOut, TValueOut> record) => output.Forward(in record);

        public IKeyValueStore<TKey, TValue> GetKeyValueStore<TKey, TValue>(string name)
        {
            foreach (var definition in node._definitions)
            {
                if (definition.Name == name)
                {
                    return builder.GetKeyValueStore<TKey, TValue>(name);
                }
            }

            throw new StreamsException($"Processor '{node.Name}' did not declare a store named '{name}'.");
        }
    }
}

/// <summary>Adapts a <see cref="StatefulHandler{TKey, TValue, TState, TKeyOut, TValueOut}"/> to a processor.</summary>
internal sealed class StatefulProcessor<TKey, TValue, TState, TKeyOut, TValueOut>(
    string storeName,
    StatefulHandler<TKey, TValue, TState, TKeyOut, TValueOut> handler)
    : IProcessor<TKey, TValue, TKeyOut, TValueOut>, IValueState<TState>
{
    private IProcessorContext<TKeyOut, TValueOut> _context = null!;
    private IKeyValueStore<TKey, TState> _store = null!;
    private TKey _key = default!;

    public void Init(IProcessorContext<TKeyOut, TValueOut> context)
    {
        _context = context;
        _store = context.GetKeyValueStore<TKey, TState>(storeName);
    }

    public void Process(in Record<TKey, TValue> record)
    {
        // State is kept per key, so a record without a key has nowhere to keep it.
        if (record.Key is null)
        {
            return;
        }

        _key = record.Key;
        handler(in record, this, _context);
    }

    public bool TryGet(out TState value) => _store.TryGet(_key, out value!);

    public void Set(TState value) => _store.Put(_key, value);

    public void Clear() => _store.Delete(_key);
}

internal static class TopicNames
{
    public static string Changelog(string applicationId, string storeName) => $"{applicationId}-{storeName}-changelog";

    public static string Repartition(string applicationId, string name) => $"{applicationId}-{name}-repartition";
}
