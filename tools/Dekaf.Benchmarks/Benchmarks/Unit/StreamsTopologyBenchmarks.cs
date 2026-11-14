using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Internal;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the per-record cost of a Dekaf.Streams task and verifies zero steady-state allocation:
/// a stateless pipeline to a sink, and a keyed count with its changelog write.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class StreamsTopologyBenchmarks
{
    private const int RecordCount = 64;
    private readonly RawRecord[] _records = new RawRecord[RecordCount];
    private StreamTask _stateless = null!;
    private StreamTask _count = null!;
    private int _next;

    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < RecordCount; i++)
        {
            var key = new byte[sizeof(long)];
            var value = new byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(key, i % 8);
            BinaryPrimitives.WriteInt64BigEndian(value, i);
            _records[i] = new RawRecord(key, false, value, false, timestamp: i, offset: i, headers: null);
        }

        var stateless = new StreamsBuilder("bench");
        stateless.Stream("numbers", Serializers.Int64, Serializers.Int64)
            .Where(static (_, value) => value >= 0)
            .Select(static (_, value) => value * 2)
            .ToTopic("doubled", Serializers.Int64, Serializers.Int64);
        _stateless = new StreamTask(stateless.Build(), 0, 0, new DiscardingCollector());

        var count = new StreamsBuilder("bench");
        count.Stream("numbers", Serializers.Int64, Serializers.Int64)
            .GroupByKey()
            .Count("counts")
            .ToStream()
            .ToTopic("counts-out", Serializers.Int64, Serializers.Int64);
        _count = new StreamTask(count.Build(), 0, 0, new DiscardingCollector());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _stateless.Dispose();
        _count.Dispose();
    }

    [Benchmark]
    public void WhereSelectToTopic() => _stateless.Process("numbers", in _records[Next()]);

    [Benchmark]
    public void GroupByKeyCount() => _count.Process("numbers", in _records[Next()]);

    private int Next() => _next = (_next + 1) % RecordCount;

    private sealed class DiscardingCollector : IRecordCollector
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
        }
    }
}
