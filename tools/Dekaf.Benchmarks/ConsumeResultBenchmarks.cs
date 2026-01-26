using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks for ConsumeResult construction to measure allocation patterns.
/// Tests eager deserialization approach (current implementation).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ConsumeResultBenchmarks
{
    private readonly IDeserializer<string> _keyDeserializer = Serializers.String;
    private readonly IDeserializer<string> _valueDeserializer = Serializers.String;
    private readonly ISerializer<string> _keySerializer = Serializers.String;
    private readonly ISerializer<string> _valueSerializer = Serializers.String;
    private readonly ReadOnlyMemory<byte> _keyData;
    private readonly ReadOnlyMemory<byte> _valueData;

    public ConsumeResultBenchmarks()
    {
        // Create sample key/value data
        var keyBuffer = new ArrayBufferWriter<byte>();
        _keySerializer.Serialize("test-key-12345", ref keyBuffer, new SerializationContext());
        _keyData = keyBuffer.WrittenMemory;

        var valueBuffer = new ArrayBufferWriter<byte>();
        _valueSerializer.Serialize("test-value-" + new string('x', 100), ref valueBuffer, new SerializationContext());
        _valueData = valueBuffer.WrittenMemory;
    }

    [Benchmark(Description = "Create ConsumeResult")]
    public ConsumeResult<string, string> CreateConsumeResult()
    {
        return new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 12345,
            keyData: _keyData,
            isKeyNull: false,
            valueData: _valueData,
            isValueNull: false,
            headers: null,
            timestamp: DateTimeOffset.UtcNow,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: _keyDeserializer,
            valueDeserializer: _valueDeserializer);
    }

    [Benchmark(Description = "Create and access Key/Value")]
    public (string?, string) CreateAndAccessKeyValue()
    {
        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 12345,
            keyData: _keyData,
            isKeyNull: false,
            valueData: _valueData,
            isValueNull: false,
            headers: null,
            timestamp: DateTimeOffset.UtcNow,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: _keyDeserializer,
            valueDeserializer: _valueDeserializer);

        return (result.Key, result.Value);
    }

    [Benchmark(Description = "Create 1000 ConsumeResults")]
    public ConsumeResult<string, string>[] Create1000Results()
    {
        var results = new ConsumeResult<string, string>[1000];
        for (int i = 0; i < 1000; i++)
        {
            results[i] = new ConsumeResult<string, string>(
                topic: "test-topic",
                partition: 0,
                offset: i,
                keyData: _keyData,
                isKeyNull: false,
                valueData: _valueData,
                isValueNull: false,
                headers: null,
                timestamp: DateTimeOffset.UtcNow,
                timestampType: TimestampType.CreateTime,
                leaderEpoch: null,
                keyDeserializer: _keyDeserializer,
                valueDeserializer: _valueDeserializer);
        }
        return results;
    }

    [Benchmark(Description = "Create and access Key/Value (eager baseline)")]
    public (string?, string) CreateAndAccessEagerBaseline()
    {
        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 12345,
            keyData: _keyData,
            isKeyNull: false,
            valueData: _valueData,
            isValueNull: false,
            headers: null,
            timestamp: DateTimeOffset.UtcNow,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: _keyDeserializer,
            valueDeserializer: _valueDeserializer);

        return (result.Key, result.Value);
    }

    [Benchmark(Description = "Struct size comparison")]
    public int GetStructSize()
    {
        return System.Runtime.InteropServices.Marshal.SizeOf<ConsumeResult<string, string>>();
    }
}
