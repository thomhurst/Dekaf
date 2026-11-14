using Dekaf.Serialization;

namespace Dekaf.Streams.Internal;

/// <summary>
/// A stream handle. It remembers the serdes that still describe its records: the key serde is null after
/// the key changed, which also means the records are no longer partitioned by key.
/// </summary>
internal sealed class StreamImpl<TKey, TValue>(
    StreamsBuilder builder,
    StreamNode<TKey, TValue> node,
    ISerde<TKey>? keySerde,
    ISerde<TValue>? valueSerde) : IStream<TKey, TValue>
{
    internal StreamNode<TKey, TValue> Node => node;

    public IStream<TKey, TValue> Where(Func<TKey, TValue, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Then(new WhereNode<TKey, TValue>(builder.AutoName("where"), predicate), keySerde, valueSerde);
    }

    public IStream<TKey, TResult> Select<TResult>(Func<TKey, TValue, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Then(new SelectNode<TKey, TValue, TResult>(builder.AutoName("select"), selector), keySerde, null);
    }

    public IStream<TNewKey, TValue> SelectKey<TNewKey>(Func<TKey, TValue, TNewKey> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Then(new SelectKeyNode<TKey, TValue, TNewKey>(builder.AutoName("select-key"), selector), null, valueSerde);
    }

    public IStream<TKey, TResult> SelectMany<TResult>(Action<TKey, TValue, IValueSink<TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Then(new SelectManyNode<TKey, TValue, TResult>(builder.AutoName("select-many"), selector), keySerde, null);
    }

    public IStream<TKey, TValue> Peek(Action<TKey, TValue> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Then(new PeekNode<TKey, TValue>(builder.AutoName("peek"), action), keySerde, valueSerde);
    }

    public IStream<TKey, TValue> Merge(IStream<TKey, TValue> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var otherImpl = other as StreamImpl<TKey, TValue>
            ?? throw new TopologyException("The stream was not created by a StreamsBuilder.");
        var merge = builder.Add(new MergeNode<TKey, TValue>(builder.AutoName("merge")));
        node.AddDownstream(merge);
        otherImpl.Node.AddDownstream(merge);

        // The merged stream is partitioned by key only when both inputs are.
        var mergedKeySerde = otherImpl.KeySerde is null ? null : keySerde;
        return new StreamImpl<TKey, TValue>(builder, merge, mergedKeySerde, valueSerde);
    }

    public IStream<TKey, TValue> Repartition(string name, ISerde<TKey> keySerde, ISerde<TValue> valueSerde)
    {
        ArgumentNullException.ThrowIfNull(keySerde);
        ArgumentNullException.ThrowIfNull(valueSerde);
        var source = ThroughRepartitionTopic(builder.RegisterName(name), keySerde, valueSerde);
        return new StreamImpl<TKey, TValue>(builder, source, keySerde, valueSerde);
    }

    public void ToTopic(string topic, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerializer)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(keySerializer);
        ArgumentNullException.ThrowIfNull(valueSerializer);
        node.AddDownstream(builder.Add(new SinkNode<TKey, TValue>(
            builder.AutoName("sink"), topic, keySerializer, valueSerializer, isRepartition: false)));
    }

    public IGroupedStream<TKey, TValue> GroupByKey() =>
        new GroupedStreamImpl<TKey, TValue>(builder, this, RequireKeySerde("GroupByKey"), null);

    public IGroupedStream<TNewKey, TValue> GroupBy<TNewKey>(
        Func<TKey, TValue, TNewKey> keySelector,
        ISerde<TNewKey> keySerde,
        ISerde<TValue> valueSerde)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(keySerde);
        ArgumentNullException.ThrowIfNull(valueSerde);
        var rekeyed = (StreamImpl<TNewKey, TValue>)SelectKey(keySelector);
        return new GroupedStreamImpl<TNewKey, TValue>(builder, rekeyed, keySerde, valueSerde);
    }

    public IStream<TKey, TResult> Join<TTableValue, TResult>(
        ITable<TKey, TTableValue> table,
        Func<TValue, TTableValue, TResult> joiner)
    {
        ArgumentNullException.ThrowIfNull(joiner);
        return AddJoin<TTableValue, TResult>(table, joiner, null);
    }

    public IStream<TKey, TResult> LeftJoin<TTableValue, TResult>(
        ITable<TKey, TTableValue> table,
        Func<TValue, TTableValue?, bool, TResult> joiner)
    {
        ArgumentNullException.ThrowIfNull(joiner);
        return AddJoin(table, null, joiner);
    }

    public IStream<TKeyOut, TValueOut> Process<TKeyOut, TValueOut>(
        string name,
        Func<IProcessor<TKey, TValue, TKeyOut, TValueOut>> factory,
        params StoreDefinition[] stores)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(stores);
        builder.RegisterName(name);
        foreach (var store in stores)
        {
            if (store.Name != name)
            {
                builder.RegisterName(store.Name);
            }
        }

        var processor = new ProcessorNode<TKey, TValue, TKeyOut, TValueOut>(name, builder.ApplicationId, factory, stores);

        // A processor may emit any key, so the output is not known to be partitioned by key.
        return Then(processor, null, null);
    }

    public IStream<TKeyOut, TValueOut> Process<TState, TKeyOut, TValueOut>(
        string name,
        ISerde<TState> stateSerde,
        StatefulHandler<TKey, TValue, TState, TKeyOut, TValueOut> handler)
    {
        ArgumentNullException.ThrowIfNull(stateSerde);
        ArgumentNullException.ThrowIfNull(handler);
        var store = Stores.KeyValue(name, RequireKeySerde("Process with per-key state"), stateSerde);
        return Process(name, () => new StatefulProcessor<TKey, TValue, TState, TKeyOut, TValueOut>(name, handler), store);
    }

    internal ISerde<TKey>? KeySerde => keySerde;

    internal ISerde<TKey> RequireKeySerde(string operation) =>
        keySerde ?? throw new TopologyException(
            $"{operation} needs records partitioned by key, but the key was changed by SelectKey or Process. " +
            "Call Repartition(name, keySerde, valueSerde) first, or use GroupBy.");

    /// <summary>Writes the stream to an internal repartition topic and returns the node that reads it back.</summary>
    internal SourceNode<TKey, TValue> ThroughRepartitionTopic(string name, ISerde<TKey> keySerde, ISerde<TValue> valueSerde)
    {
        var topic = TopicNames.Repartition(builder.ApplicationId, name);
        node.AddDownstream(builder.Add(new SinkNode<TKey, TValue>(
            builder.AutoName("repartition-sink"), topic, keySerde, valueSerde, isRepartition: true)));
        return builder.Add(new SourceNode<TKey, TValue>(
            builder.AutoName("repartition-source"), topic, keySerde, valueSerde, isRepartition: true));
    }

    private StreamImpl<TKeyOut, TValueOut> Then<TKeyOut, TValueOut>(
        OperatorNode<TKey, TValue, TKeyOut, TValueOut> next,
        ISerde<TKeyOut>? nextKeySerde,
        ISerde<TValueOut>? nextValueSerde)
    {
        node.AddDownstream(builder.Add(next));
        return new StreamImpl<TKeyOut, TValueOut>(builder, next, nextKeySerde, nextValueSerde);
    }

    private StreamImpl<TKey, TResult> AddJoin<TTableValue, TResult>(
        ITable<TKey, TTableValue> table,
        Func<TValue, TTableValue, TResult>? innerJoiner,
        Func<TValue, TTableValue?, bool, TResult>? leftJoiner)
    {
        ArgumentNullException.ThrowIfNull(table);
        var tableImpl = table as TableImpl<TKey, TTableValue>
            ?? throw new TopologyException("The table was not created by a StreamsBuilder.");
        RequireKeySerde("Join");
        if (tableImpl.IsWindowed)
        {
            throw new TopologyException($"Table '{tableImpl.StoreName}' is windowed. A stream can only join a key-value table.");
        }

        var join = new JoinNode<TKey, TValue, TTableValue, TResult>(
            builder.AutoName("join"), tableImpl.StoreName, innerJoiner, leftJoiner);
        join.Dependencies.Add(tableImpl.Node);
        return Then(join, keySerde, null);
    }
}

internal sealed class GroupedStreamImpl<TKey, TValue>(
    StreamsBuilder builder,
    StreamImpl<TKey, TValue> stream,
    ISerde<TKey> keySerde,
    ISerde<TValue>? repartitionValueSerde) : IGroupedStream<TKey, TValue>
{
    public ITable<TKey, long> Count(string name) =>
        Aggregate(name, static () => 0L, static (_, _, count) => count + 1, Serializers.Int64);

    public ITable<TKey, TAggregate> Aggregate<TAggregate>(
        string name,
        Func<TAggregate> initializer,
        Func<TKey, TValue, TAggregate, TAggregate> aggregator,
        ISerde<TAggregate> aggregateSerde)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(aggregateSerde);
        var input = Input(builder.RegisterName(name));
        var node = builder.Add(new AggregateNode<TKey, TValue, TAggregate>(
            name, TopicNames.Changelog(builder.ApplicationId, name), keySerde, aggregateSerde, initializer, aggregator));
        input.AddDownstream(node);
        return new TableImpl<TKey, TAggregate>(builder, node, name, keySerde, aggregateSerde);
    }

    public ITable<TKey, TValue> Reduce(string name, Func<TValue, TValue, TValue> reducer, ISerde<TValue> valueSerde)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        ArgumentNullException.ThrowIfNull(valueSerde);
        var input = Input(builder.RegisterName(name));
        var node = builder.Add(new ReduceNode<TKey, TValue>(
            name, TopicNames.Changelog(builder.ApplicationId, name), keySerde, valueSerde, reducer));
        input.AddDownstream(node);
        return new TableImpl<TKey, TValue>(builder, node, name, keySerde, valueSerde);
    }

    public IWindowedStream<TKey, TValue> WindowedBy(TimeWindows windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        return new WindowedStreamImpl<TKey, TValue>(builder, this, keySerde, windows);
    }

    /// <summary>The node aggregation reads from: the stream itself, or the repartition topic a GroupBy needs.</summary>
    internal StreamNode<TKey, TValue> Input(string name) =>
        repartitionValueSerde is null
            ? stream.Node
            : stream.ThroughRepartitionTopic(name, keySerde, repartitionValueSerde);
}

internal sealed class WindowedStreamImpl<TKey, TValue>(
    StreamsBuilder builder,
    GroupedStreamImpl<TKey, TValue> grouped,
    ISerde<TKey> keySerde,
    TimeWindows windows) : IWindowedStream<TKey, TValue>
{
    public ITable<Windowed<TKey>, long> Count(string name) =>
        Aggregate(name, static () => 0L, static (_, _, count) => count + 1, Serializers.Int64);

    public ITable<Windowed<TKey>, TAggregate> Aggregate<TAggregate>(
        string name,
        Func<TAggregate> initializer,
        Func<TKey, TValue, TAggregate, TAggregate> aggregator,
        ISerde<TAggregate> aggregateSerde)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(aggregateSerde);
        var input = grouped.Input(builder.RegisterName(name));
        var node = builder.Add(new WindowAggregateNode<TKey, TValue, TAggregate>(
            name,
            TopicNames.Changelog(builder.ApplicationId, name),
            windows,
            keySerde,
            aggregateSerde,
            initializer,
            aggregator));
        input.AddDownstream(node);
        return new TableImpl<Windowed<TKey>, TAggregate>(
            builder, node, name, WindowedSerde.Create(keySerde, windows.Size), aggregateSerde, isWindowed: true);
    }
}

internal sealed class TableImpl<TKey, TValue>(
    StreamsBuilder builder,
    StreamNode<TKey, TValue> node,
    string storeName,
    ISerde<TKey> keySerde,
    ISerde<TValue> valueSerde,
    bool isWindowed = false) : ITable<TKey, TValue>
{
    internal StreamNode<TKey, TValue> Node => node;

    internal bool IsWindowed => isWindowed;

    public string StoreName => storeName;

    public IStream<TKey, TValue> ToStream() => new StreamImpl<TKey, TValue>(builder, node, keySerde, valueSerde);
}
