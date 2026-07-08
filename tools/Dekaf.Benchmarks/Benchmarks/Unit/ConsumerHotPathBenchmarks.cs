using System.Collections;
using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Consumer hot-path allocation benchmarks over already-parsed fetch records.
/// The setup mirrors the consume loop after protocol parsing so serializer payload
/// allocation is measured separately from batch/record traversal overhead.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ConsumerHotPathBenchmarks
{
    private const int MessageCount = 1_000;

    private readonly SingleRecordBatchList _batchList = new();
    private readonly IDeserializer<string> _cachedRepeatedStringValueDeserializer =
        new CachingStringDeserializer(Serializers.String, maxCachedBytes: 4 * 1024, maxCachedEntries: 128);
    private CachingStringDeserializer _saturatedCachedStringValueDeserializer = null!;
    private Record[] _records = null!;
    private Record[] _repeated1KbValueRecords = null!;
    private string _topic = null!;
    private long _timestampMs;

    [GlobalSetup]
    public void Setup()
    {
        _topic = "consumer-hot-path";
        _timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _records = new Record[MessageCount];

        for (var i = 0; i < MessageCount; i++)
        {
            var key = Encoding.UTF8.GetBytes($"key-{i}");
            var value = Encoding.UTF8.GetBytes($"value-{i}");

            _records[i] = new Record
            {
                OffsetDelta = i,
                TimestampDelta = i,
                Key = key,
                Value = value,
                IsKeyNull = false,
                IsValueNull = false,
                Headers = null,
                HeaderCount = 0,
            };
        }

        _saturatedCachedStringValueDeserializer =
            new CachingStringDeserializer(Serializers.String, maxCachedBytes: 4 * 1024, maxCachedEntries: 128);
        var context = new SerializationContext { Topic = _topic, Component = SerializationComponent.Value };
        for (var i = 0; i < 128; i++)
        {
            _saturatedCachedStringValueDeserializer.Deserialize(
                Encoding.UTF8.GetBytes($"prefill-{i}"),
                context);
        }

        var repeatedValue = Encoding.UTF8.GetBytes(new string('x', 1000));
        _repeated1KbValueRecords = new Record[MessageCount];
        for (var i = 0; i < MessageCount; i++)
        {
            var key = Encoding.UTF8.GetBytes($"key-{i}");
            _repeated1KbValueRecords[i] = new Record
            {
                OffsetDelta = i,
                TimestampDelta = i,
                Key = key,
                Value = repeatedValue,
                IsKeyNull = false,
                IsValueNull = false,
                Headers = null,
                HeaderCount = 0,
            };
        }

        using var pending = CreatePendingFetchData();
        var batch = new ConsumeRawBatch(pending);
        foreach (var _ in batch)
        {
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = MessageCount, Description = "Raw batch enumerate")]
    public int ConsumeRawBatch_Enumerate()
    {
        using var pending = CreatePendingFetchData();
        var batch = new ConsumeRawBatch(pending);
        var bytes = 0;

        foreach (var record in batch)
        {
            bytes += record.Value.Length;
        }

        return bytes;
    }

    [Benchmark(OperationsPerInvoke = MessageCount, Description = "Typed batch raw bytes enumerate")]
    public int ConsumeBatch_RawBytes_Enumerate()
    {
        using var pending = CreatePendingFetchData();
        var batch = new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(
            pending,
            Serializers.Ignore,
            Serializers.RawBytes);
        var bytes = 0;

        foreach (var result in batch)
        {
            bytes += result.Value.Length;
        }

        return bytes;
    }

    [Benchmark(OperationsPerInvoke = MessageCount, Description = "Typed batch string deserialize")]
    public int ConsumeBatch_String_Deserialize()
    {
        using var pending = CreatePendingFetchData();
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String);
        var chars = 0;

        foreach (var result in batch)
        {
            chars += result.Value.Length;
        }

        return chars;
    }

    [Benchmark(OperationsPerInvoke = MessageCount, Description = "Typed batch string cached deserialize (saturated)")]
    public int ConsumeBatch_String_CachedDeserializeSaturated()
    {
        using var pending = CreatePendingFetchData();
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            _saturatedCachedStringValueDeserializer);
        var chars = 0;

        foreach (var result in batch)
        {
            chars += result.Value.Length;
        }

        return chars;
    }

    [Benchmark(OperationsPerInvoke = MessageCount, Description = "Typed batch repeated 1KB string deserialize")]
    public int ConsumeBatch_Repeated1KbString_Deserialize()
    {
        using var pending = CreatePendingFetchData(_repeated1KbValueRecords);
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String);
        var chars = 0;

        foreach (var result in batch)
        {
            chars += result.Value.Length;
        }

        return chars;
    }

    [Benchmark(OperationsPerInvoke = MessageCount, Description = "Typed batch repeated 1KB string cached deserialize")]
    public int ConsumeBatch_Repeated1KbString_CachedDeserialize()
    {
        using var pending = CreatePendingFetchData(_repeated1KbValueRecords);
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            _cachedRepeatedStringValueDeserializer);
        var chars = 0;

        foreach (var result in batch)
        {
            chars += result.Value.Length;
        }

        return chars;
    }

    private PendingFetchData CreatePendingFetchData(Record[]? records = null)
    {
        var recordBatch = RecordBatch.RentFromPool();
        recordBatch.BaseOffset = 0;
        recordBatch.BaseTimestamp = _timestampMs;
        recordBatch.MaxTimestamp = _timestampMs + MessageCount - 1;
        recordBatch.LastOffsetDelta = MessageCount - 1;
        recordBatch.Attributes = RecordBatchAttributes.None;
        recordBatch.Records = records ?? _records;

        _batchList.Batch = recordBatch;
        var pending = PendingFetchData.Create(_topic, partitionIndex: 0, _batchList);
        pending.EagerParseAll();
        return pending;
    }

    private sealed class SingleRecordBatchList : IReadOnlyList<RecordBatch>
    {
        public RecordBatch Batch { get; set; } = null!;

        public int Count => 1;

        public RecordBatch this[int index] => index == 0
            ? Batch
            : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<RecordBatch> GetEnumerator()
        {
            yield return Batch;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
