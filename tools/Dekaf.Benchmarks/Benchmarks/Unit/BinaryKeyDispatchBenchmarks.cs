using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class BinaryKeyDispatchBenchmarks
{
    private const int RecordCount = 1024;
    private ConsumeResult<byte[], string>[] _binary = null!;
    private ConsumeResult<ReadOnlyMemory<byte>, string>[] _memory = null!;
    private ConsumeResult<string, string>[] _strings = null!;

    // One handler measures the sequential shortcut; two exercise keyed dispatch.
    [Params(1, 2)]
    public int MaxConcurrentHandlers { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _binary = CreateRecords(Serializers.ByteArray);
        _memory = CreateRecords(Serializers.RawBytes);
        _strings = CreateRecords(Serializers.String);
    }

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task ByteArray() => DispatchAsync(_binary, MaxConcurrentHandlers);

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task RawMemory() => DispatchAsync(_memory, MaxConcurrentHandlers);

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task StringControl() => DispatchAsync(_strings, MaxConcurrentHandlers);

    private static ConsumeResult<TKey, string>[] CreateRecords<TKey>(IDeserializer<TKey> deserializer)
    {
        var result = new ConsumeResult<TKey, string>[RecordCount];
        for (var i = 0; i < result.Length; i++)
        {
            // Distinct deserializer inputs reproduce separately fetched copies of one wire key.
            result[i] = new ConsumeResult<TKey, string>("topic", 0, i,
                "same-key"u8.ToArray(), false, default, false, null, 0,
                TimestampType.CreateTime, null, deserializer, Serializers.String);
        }
        return result;
    }

    internal static async Task DispatchAsync<TKey>(ConsumeResult<TKey, string>[] records, int maxConcurrentHandlers,
        bool holdAllWorkers = false)
    {
        var lane = new PartitionLane<TKey, string>(new TopicPartition("topic", 0), records.Length,
            static (_, _) => ValueTask.CompletedTask, static _ => { }, static (_, error) => throw error);
        var context = new PartitionProcessorContext<TKey, string>(lane);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = new KeyOrderedPartitionDispatcher<TKey, string>(context, 1, maxConcurrentHandlers, records.Length,
            async (batch, _) =>
            {
                if (batch[0].Offset == 0 || (holdAllWorkers && batch[0].Offset < maxConcurrentHandlers))
                    await releaseFirst.Task.ConfigureAwait(false);
                context.MarkProcessed(batch[0]);
            });
        // Reserve before publication, matching the candidate's manual-completion contract.
        var completionBatch = lane.CreateCompletionBatch(records.Length);
        var published = 0;
        try
        {
            for (var i = 0; i < records.Length; i++)
            {
                if (!lane.TryEnqueue(records[i], completionBatch))
                    throw new InvalidOperationException("The binary input did not fit in the partition queue.");
                published++;
            }
        }
        finally
        {
            lane.EndBatch(completionBatch, published);
        }
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        var running = dispatcher.RunAsync(CancellationToken.None);
        var bufferedKeys = dispatcher.LaneCount;
        releaseFirst.SetResult();
        await running.ConfigureAwait(false);
        if (holdAllWorkers && bufferedKeys != records.Length)
            throw new InvalidOperationException("The shared-suffix fixture did not fill the keyed buffer.");
        if (lane.GetCommitOffset()?.Offset != records.Length)
            throw new InvalidOperationException("The dispatcher did not complete every input record.");
    }
}

/// <summary>
/// Keeps lane cardinality identical before/after content equality, exposing the cost
/// of hashing large keys without the repeated-key workload's lane-coalescing benefit.
/// </summary>
[MemoryDiagnoser]
public class DistinctBinaryKeyDispatchBenchmarks
{
    private const int RecordCount = 128;
    private ConsumeResult<byte[], string>[] _binary = null!;
    private ConsumeResult<ReadOnlyMemory<byte>, string>[] _memory = null!;

    [Params(8, 1024, 65536)]
    public int KeySize { get; set; }

    [Params(1, 2)]
    public int MaxConcurrentHandlers { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _binary = CreateRecords(Serializers.ByteArray);
        _memory = CreateRecords(Serializers.RawBytes);
    }

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task ByteArray() => BinaryKeyDispatchBenchmarks.DispatchAsync(_binary, MaxConcurrentHandlers);

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task RawMemory() => BinaryKeyDispatchBenchmarks.DispatchAsync(_memory, MaxConcurrentHandlers);

    private ConsumeResult<TKey, string>[] CreateRecords<TKey>(IDeserializer<TKey> deserializer)
    {
        var result = new ConsumeResult<TKey, string>[RecordCount];
        for (var index = 0; index < result.Length; index++)
        {
            var key = new byte[KeySize];
            key.AsSpan().Fill(0x61);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(KeySize - sizeof(int)), index);
            result[index] = new ConsumeResult<TKey, string>("topic", 0, index,
                key, false, default, false, null, 0,
                TimestampType.CreateTime, null, deserializer, Serializers.String);
        }
        return result;
    }
}

/// <summary>
/// Exercises a full buffer of distinct large keys with a common prefix and suffix.
/// Their discriminator lies outside the former four-region sampling scheme.
/// </summary>
[MemoryDiagnoser]
public class SharedSuffixBinaryKeyDispatchBenchmarks
{
    private const int RecordCount = 256;
    private ConsumeResult<ReadOnlyMemory<byte>, string>[] _records = null!;

    [Params(1024, 65536)]
    public int KeySize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _records = new ConsumeResult<ReadOnlyMemory<byte>, string>[RecordCount];
        for (var index = 0; index < _records.Length; index++)
        {
            var key = new byte[KeySize];
            key.AsSpan().Fill(0x61);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(KeySize - 32), index);
            _records[index] = new ConsumeResult<ReadOnlyMemory<byte>, string>("topic", 0, index,
                key, false, default, false, null, 0, TimestampType.CreateTime, null,
                Serializers.RawBytes, Serializers.String);
        }
    }

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task RawMemory() => BinaryKeyDispatchBenchmarks.DispatchAsync(_records,
        maxConcurrentHandlers: 2, holdAllWorkers: true);
}
