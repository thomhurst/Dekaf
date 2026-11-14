using System.Diagnostics.CodeAnalysis;
using Dekaf.Errors;
using Dekaf.Serialization;
using Dekaf.Streams.Internal;

namespace Dekaf.Streams;

/// <summary>A custom processing step. One instance is created per task and is called by one thread at a time.</summary>
public interface IProcessor<TKeyIn, TValueIn, TKeyOut, TValueOut>
{
    /// <summary>Called once before the first record. Resolve stores from <paramref name="context"/> here.</summary>
    void Init(IProcessorContext<TKeyOut, TValueOut> context);

    /// <summary>Processes one record.</summary>
    void Process(in Record<TKeyIn, TValueIn> record);
}

/// <summary>Forwards records to the next step of the topology.</summary>
public interface IForwarder<TKey, TValue>
{
    /// <summary>Forwards a record downstream.</summary>
    void Forward(in Record<TKey, TValue> record);
}

/// <summary>What a processor can reach from its task.</summary>
public interface IProcessorContext<TKeyOut, TValueOut> : IForwarder<TKeyOut, TValueOut>
{
    /// <summary>Gets the id of the subtopology the task runs.</summary>
    string SubtopologyId { get; }

    /// <summary>Gets the partition the task owns.</summary>
    int Partition { get; }

    /// <summary>Gets a store the processor declared.</summary>
    IKeyValueStore<TKey, TValue> GetKeyValueStore<TKey, TValue>(string name);
}

/// <summary>The state kept for the key of the record being processed.</summary>
public interface IValueState<TState>
{
    /// <summary>Reads the state of the current key.</summary>
    bool TryGet([MaybeNullWhen(false)] out TState value);

    /// <summary>Replaces the state of the current key.</summary>
    void Set(TState value);

    /// <summary>Removes the state of the current key.</summary>
    void Clear();
}

/// <summary>Declares a state store. Create one with <see cref="Stores"/>.</summary>
public abstract class StoreDefinition
{
    private protected StoreDefinition(string name) => Name = name;

    /// <summary>Gets the store name, unique in the topology.</summary>
    public string Name { get; }

    internal abstract void Create(TaskBuilder builder, string? changelogTopic);
}

/// <summary>Creates store definitions for <see cref="IStream{TKey, TValue}.Process{TKeyOut, TValueOut}"/>.</summary>
public static class Stores
{
    /// <summary>Declares a changelog-backed key-value store.</summary>
    public static StoreDefinition KeyValue<TKey, TValue>(string name, ISerde<TKey> keySerde, ISerde<TValue> valueSerde)
    {
        ArgumentNullException.ThrowIfNull(keySerde);
        ArgumentNullException.ThrowIfNull(valueSerde);
        return new KeyValueStoreDefinition<TKey, TValue>(name, keySerde, valueSerde);
    }
}

/// <summary>A failure while a record was read or processed.</summary>
public readonly struct ProcessingError
{
    internal ProcessingError(Exception exception, string topic, int partition, long offset)
    {
        Exception = exception;
        Topic = topic;
        Partition = partition;
        Offset = offset;
    }

    /// <summary>Gets the exception thrown by a deserializer, an operator or a processor.</summary>
    public Exception Exception { get; }

    /// <summary>Gets the topic the failed record was read from.</summary>
    public string Topic { get; }

    /// <summary>Gets the partition the failed record was read from.</summary>
    public int Partition { get; }

    /// <summary>Gets the offset of the failed record.</summary>
    public long Offset { get; }
}

/// <summary>What to do with a record that failed.</summary>
public readonly struct ErrorResponse
{
    private ErrorResponse(bool skip, string? deadLetterTopic)
    {
        IsSkip = skip;
        DeadLetterTopic = deadLetterTopic;
    }

    internal bool IsSkip { get; }

    internal string? DeadLetterTopic { get; }

    /// <summary>Stops the task by throwing a <see cref="StreamsException"/>. This is the default.</summary>
    public static ErrorResponse Fail => default;

    /// <summary>Skips the record and continues.</summary>
    public static ErrorResponse Continue => new(skip: true, deadLetterTopic: null);

    /// <summary>Writes the record's original key and value to <paramref name="topic"/> and continues.</summary>
    public static ErrorResponse DeadLetter(string topic)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        return new ErrorResponse(skip: true, topic);
    }
}

/// <summary>A failure in a streams topology or task.</summary>
public class StreamsException : KafkaException
{
    /// <summary>Creates the exception.</summary>
    public StreamsException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with the failure that caused it.</summary>
    public StreamsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>A topology that cannot be built as declared.</summary>
public sealed class TopologyException : StreamsException
{
    /// <summary>Creates the exception.</summary>
    public TopologyException(string message) : base(message)
    {
    }
}
