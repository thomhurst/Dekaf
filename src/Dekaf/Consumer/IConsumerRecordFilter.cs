using Dekaf.Serialization;

namespace Dekaf.Consumer;

/// <summary>
/// Decides whether a fetched record should enter the deserialization and delivery pipeline.
/// </summary>
/// <remarks>
/// Returning <see langword="false"/> skips delivery while advancing the consumer position past
/// the record. The broker still fetches filtered records. Implementations must not retain the
/// context or its pooled memory beyond the call.
/// </remarks>
public interface IConsumerRecordFilter
{
    bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context);
}

/// <summary>
/// Allocation-free view of a parsed Kafka record before deserialization.
/// </summary>
public readonly ref struct ConsumerRecordFilterContext
{
    internal ConsumerRecordFilterContext(
        string topic,
        int partition,
        long offset,
        long timestampMs,
        TimestampType timestampType,
        int? leaderEpoch,
        ReadOnlyMemory<byte> key,
        bool isKeyNull,
        ReadOnlyMemory<byte> value,
        bool isValueNull,
        ReadOnlySpan<Header> headers)
    {
        Topic = topic;
        Partition = partition;
        Offset = offset;
        TimestampMs = timestampMs;
        TimestampType = timestampType;
        LeaderEpoch = leaderEpoch;
        Key = key;
        IsKeyNull = isKeyNull;
        Value = value;
        IsValueNull = isValueNull;
        Headers = headers;
    }

    public string Topic { get; }
    public int Partition { get; }
    public long Offset { get; }
    public long TimestampMs { get; }
    public TimestampType TimestampType { get; }
    public int? LeaderEpoch { get; }
    public ReadOnlyMemory<byte> Key { get; }
    public bool IsKeyNull { get; }
    public ReadOnlyMemory<byte> Value { get; }
    public bool IsValueNull { get; }
    public ReadOnlySpan<Header> Headers { get; }
}
