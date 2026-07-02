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
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ConsumerHotPathBenchmarks
{
    private const int MessageCount = 1_000;

    private readonly SingleRecordBatchList _batchList = new();
    private Record[] _records = null!;
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

    private PendingFetchData CreatePendingFetchData()
    {
        var recordBatch = RecordBatch.RentFromPool();
        recordBatch.BaseOffset = 0;
        recordBatch.BaseTimestamp = _timestampMs;
        recordBatch.MaxTimestamp = _timestampMs + MessageCount - 1;
        recordBatch.LastOffsetDelta = MessageCount - 1;
        recordBatch.Attributes = RecordBatchAttributes.None;
        recordBatch.Records = _records;

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
