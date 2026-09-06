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

    [GlobalSetup]
    public void Setup()
    {
        _binary = CreateRecords(Serializers.ByteArray);
        _memory = CreateRecords(Serializers.RawBytes);
        _strings = CreateRecords(Serializers.String);
    }

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task ByteArray() => DispatchAsync(_binary);

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task RawMemory() => DispatchAsync(_memory);

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public Task StringControl() => DispatchAsync(_strings);

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

    private static async Task DispatchAsync<TKey>(ConsumeResult<TKey, string>[] records)
    {
        var lane = new PartitionLane<TKey, string>(new TopicPartition("topic", 0), RecordCount,
            static (_, _) => ValueTask.CompletedTask, static _ => { }, static (_, error) => throw error);
        var context = new PartitionProcessorContext<TKey, string>(lane);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = new KeyOrderedPartitionDispatcher<TKey, string>(context, 1, 1, RecordCount,
            async (batch, _) =>
            {
                if (batch[0].Offset == 0)
                    await releaseFirst.Task.ConfigureAwait(false);
                context.MarkProcessed(batch[0]);
            });
        for (var i = 0; i < records.Length; i++)
            lane.TryEnqueue(records[i]);
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        var running = dispatcher.RunAsync(CancellationToken.None);
        releaseFirst.SetResult();
        await running.ConfigureAwait(false);
    }
}
