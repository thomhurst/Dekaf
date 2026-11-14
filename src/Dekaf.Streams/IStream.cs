using Dekaf.Serialization;

namespace Dekaf.Streams;

/// <summary>An unbounded stream of independent records.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
public interface IStream<TKey, TValue>
{
    /// <summary>Keeps the records that satisfy <paramref name="predicate"/>.</summary>
    IStream<TKey, TValue> Where(Func<TKey, TValue, bool> predicate);

    /// <summary>Replaces each value. The key is unchanged, so no repartitioning is needed.</summary>
    IStream<TKey, TResult> Select<TResult>(Func<TKey, TValue, TResult> selector);

    /// <summary>
    /// Replaces each key. Records are no longer partitioned by key afterwards: call
    /// <see cref="Repartition"/> before a keyed operator, or use <see cref="GroupBy"/>.
    /// </summary>
    IStream<TNewKey, TValue> SelectKey<TNewKey>(Func<TKey, TValue, TNewKey> selector);

    /// <summary>Emits zero or more values for each record by adding them to the sink. The key is unchanged.</summary>
    IStream<TKey, TResult> SelectMany<TResult>(Action<TKey, TValue, IValueSink<TResult>> selector);

    /// <summary>Runs <paramref name="action"/> for each record and forwards the record unchanged.</summary>
    IStream<TKey, TValue> Peek(Action<TKey, TValue> action);

    /// <summary>Interleaves this stream with <paramref name="other"/>. Order across the two streams is not defined.</summary>
    IStream<TKey, TValue> Merge(IStream<TKey, TValue> other);

    /// <summary>
    /// Writes the stream to the internal topic <c>&lt;application&gt;-&lt;name&gt;-repartition</c> and reads it back,
    /// so that records are partitioned by their current key.
    /// </summary>
    IStream<TKey, TValue> Repartition(string name, ISerde<TKey> keySerde, ISerde<TValue> valueSerde);

    /// <summary>Writes the stream to <paramref name="topic"/>.</summary>
    void ToTopic(string topic, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerializer);

    /// <summary>Groups records by their current key for aggregation.</summary>
    IGroupedStream<TKey, TValue> GroupByKey();

    /// <summary>
    /// Groups records by a new key. The records are repartitioned through an internal topic named after the
    /// aggregation that follows.
    /// </summary>
    IGroupedStream<TNewKey, TValue> GroupBy<TNewKey>(
        Func<TKey, TValue, TNewKey> keySelector,
        ISerde<TNewKey> keySerde,
        ISerde<TValue> valueSerde);

    /// <summary>
    /// Joins each record with the current table value for its key. Records without a table value are dropped.
    /// The stream and the table's topic must have the same number of partitions.
    /// </summary>
    IStream<TKey, TResult> Join<TTableValue, TResult>(
        ITable<TKey, TTableValue> table,
        Func<TValue, TTableValue, TResult> joiner);

    /// <summary>
    /// Joins each record with the current table value for its key. When the table has no value,
    /// <paramref name="joiner"/> receives <see langword="default"/> and <see langword="false"/>.
    /// </summary>
    IStream<TKey, TResult> LeftJoin<TTableValue, TResult>(
        ITable<TKey, TTableValue> table,
        Func<TValue, TTableValue?, bool, TResult> joiner);

    /// <summary>Runs a custom processor with the state stores it declares.</summary>
    /// <param name="name">The processor name, unique in the topology.</param>
    /// <param name="factory">Creates one processor per task.</param>
    /// <param name="stores">The stores the processor owns.</param>
    IStream<TKeyOut, TValueOut> Process<TKeyOut, TValueOut>(
        string name,
        Func<IProcessor<TKey, TValue, TKeyOut, TValueOut>> factory,
        params StoreDefinition[] stores);

    /// <summary>
    /// Runs a handler that receives one state value per record key. The state lives in a changelog-backed
    /// store named <paramref name="name"/>.
    /// </summary>
    IStream<TKeyOut, TValueOut> Process<TState, TKeyOut, TValueOut>(
        string name,
        ISerde<TState> stateSerde,
        StatefulHandler<TKey, TValue, TState, TKeyOut, TValueOut> handler);
}

/// <summary>Handles one record with the state stored for its key.</summary>
public delegate void StatefulHandler<TKey, TValue, TState, TKeyOut, TValueOut>(
    in Record<TKey, TValue> record,
    IValueState<TState> state,
    IForwarder<TKeyOut, TValueOut> output);

/// <summary>Receives the values a <c>SelectMany</c> selector emits for one record.</summary>
public interface IValueSink<in TValue>
{
    /// <summary>Emits a value with the key, timestamp and headers of the current record.</summary>
    void Add(TValue value);
}

/// <summary>A stream grouped by key, ready for aggregation.</summary>
public interface IGroupedStream<TKey, TValue>
{
    /// <summary>Counts records per key into the store <paramref name="name"/>.</summary>
    ITable<TKey, long> Count(string name);

    /// <summary>Aggregates records per key into the store <paramref name="name"/>.</summary>
    ITable<TKey, TAggregate> Aggregate<TAggregate>(
        string name,
        Func<TAggregate> initializer,
        Func<TKey, TValue, TAggregate, TAggregate> aggregator,
        ISerde<TAggregate> aggregateSerde);

    /// <summary>Combines the values of each key into the store <paramref name="name"/>.</summary>
    ITable<TKey, TValue> Reduce(string name, Func<TValue, TValue, TValue> reducer, ISerde<TValue> valueSerde);

    /// <summary>Aggregates per key and time window.</summary>
    IWindowedStream<TKey, TValue> WindowedBy(TimeWindows windows);
}

/// <summary>A stream grouped by key and time window.</summary>
public interface IWindowedStream<TKey, TValue>
{
    /// <summary>Counts records per key and window into the store <paramref name="name"/>.</summary>
    ITable<Windowed<TKey>, long> Count(string name);

    /// <summary>Aggregates records per key and window into the store <paramref name="name"/>.</summary>
    ITable<Windowed<TKey>, TAggregate> Aggregate<TAggregate>(
        string name,
        Func<TAggregate> initializer,
        Func<TKey, TValue, TAggregate, TAggregate> aggregator,
        ISerde<TAggregate> aggregateSerde);
}

/// <summary>A changelog: the latest value per key.</summary>
public interface ITable<TKey, TValue>
{
    /// <summary>Gets the name of the store that holds the table.</summary>
    string StoreName { get; }

    /// <summary>Returns the table's updates as a stream.</summary>
    IStream<TKey, TValue> ToStream();
}
