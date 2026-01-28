using Dekaf.Serialization;

namespace Dekaf.Producer;

/// <summary>
/// Message for topic-specific producers (no topic property since it's bound at producer construction).
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed record TopicProducerMessage<TKey, TValue>
{
    /// <summary>
    /// The message key (can be null).
    /// </summary>
    public TKey? Key { get; init; }

    /// <summary>
    /// The message value.
    /// </summary>
    public required TValue Value { get; init; }

    /// <summary>
    /// Optional headers.
    /// </summary>
    public Headers? Headers { get; init; }

    /// <summary>
    /// Optional partition. If not set, partitioner will choose.
    /// </summary>
    public int? Partition { get; init; }

    /// <summary>
    /// Optional timestamp. If not set, current time will be used.
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Creates a new topic producer message.
    /// </summary>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <returns>A new topic producer message.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types - factory method pattern
    public static TopicProducerMessage<TKey, TValue> Create(TKey? key, TValue value)
#pragma warning restore CA1000
        => new() { Key = key, Value = value };

    /// <summary>
    /// Creates a new topic producer message with headers.
    /// </summary>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="headers">The message headers.</param>
    /// <returns>A new topic producer message.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types - factory method pattern
    public static TopicProducerMessage<TKey, TValue> Create(TKey? key, TValue value, Headers headers)
#pragma warning restore CA1000
        => new() { Key = key, Value = value, Headers = headers };

    /// <summary>
    /// Creates a new topic producer message with a specific partition.
    /// </summary>
    /// <param name="partition">The partition to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <returns>A new topic producer message.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types - factory method pattern
    public static TopicProducerMessage<TKey, TValue> Create(int partition, TKey? key, TValue value)
#pragma warning restore CA1000
        => new() { Partition = partition, Key = key, Value = value };
}
