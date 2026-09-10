using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Parsed batch delivery through the consumer's configured interceptor chain.</summary>
[MemoryDiagnoser]
public class ConsumerBatchInterceptorBenchmarks
{
    private const int MessageCount = 1000;
    private const int ValueLength = 32;
    private const string Topic = "batch-interceptors";
    private Record[][] _records = null!;
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _consumer = null!;
    private Func<ConsumeResult<Ignore, ReadOnlyMemory<byte>>, ConsumeResult<Ignore, ReadOnlyMemory<byte>>>? _baselineOnConsume;

    [Params(0, 1, 3)]
    public int InterceptorCount { get; set; }

    [Params(false, true)]
    public bool ReplaceResult { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _records = [new Record[MessageCount]];
        for (var index = 0; index < MessageCount; index++)
        {
            var value = new byte[ValueLength];
            BinaryPrimitives.WriteInt32LittleEndian(value.AsSpan(ValueLength - sizeof(int)), index);
            _records[0][index] = new Record { OffsetDelta = index, Value = value, IsKeyNull = true };
        }
        var interceptors = new CountingInterceptor[InterceptorCount];
        for (var index = 0; index < interceptors.Length; index++)
            interceptors[index] = new CountingInterceptor(ReplaceResult);
        _consumer = new KafkaConsumer<Ignore, ReadOnlyMemory<byte>>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Manual,
            Interceptors = interceptors, QueuedMinMessages = 1, MaxPollRecords = MessageCount
        }, Serializers.Ignore, Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_consumer, Topic, 0);
        _baselineOnConsume = BufferedConsumerHarness.BindBaselineBatchInterceptors(_consumer, InterceptorCount > 0);
        var expectedLength = ValueLength - (ReplaceResult ? InterceptorCount : 0);
        if (await EnumerateCore(validate: true) != MessageCount * expectedLength)
            throw new InvalidOperationException("Every record must contain the result of the complete interceptor chain.");
        foreach (var interceptor in interceptors)
            if (interceptor.Calls != MessageCount)
                throw new InvalidOperationException("Each interceptor must run once per delivered record.");
    }

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public ValueTask<int> Enumerate() => EnumerateCore(validate: false);

    private async ValueTask<int> EnumerateCore(bool validate)
    {
        // Include one buffered fetch, the public batch iterator, delivery and disposal.
        // Fetch-array and iterator allocations are amortized per batch, not per message.
        BufferedConsumerHarness.ReseedPendingFetches(_consumer, Topic, 0, _records, 1, MessageCount);
        var bytes = 0;
        var index = 0;
        await foreach (var batch in _consumer.ConsumeBatchAsync())
        {
            foreach (var record in batch)
            {
                var result = _baselineOnConsume is { } onConsume ? onConsume(record) : record;
                if (validate)
                {
                    var removedBytes = ReplaceResult ? InterceptorCount : 0;
                    if (index >= MessageCount || result.Offset != index ||
                        !result.Value.Span.SequenceEqual(_records[0][index].Value.Span[removedBytes..]))
                        throw new InvalidOperationException("Every record must retain its identity and fully transformed payload in order.");
                    index++;
                }
                bytes += result.Value.Length;
            }
            break;
        }
        if (validate && index != MessageCount)
            throw new InvalidOperationException("Every seeded record must be delivered exactly once.");
        return bytes;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        BufferedConsumerHarness.DrainPendingFetches(_consumer);
        await _consumer.DisposeAsync();
    }

    private sealed class CountingInterceptor(bool replace) : IConsumerInterceptor<Ignore, ReadOnlyMemory<byte>>
    {
        public long Calls;
        public ConsumeResult<Ignore, ReadOnlyMemory<byte>> OnConsume(ConsumeResult<Ignore, ReadOnlyMemory<byte>> result)
        {
            Calls++;
            return replace
                ? new(result.Topic, result.Partition, result.Offset, result.Key, result.Value[1..], result.Headers,
                    result.Timestamp.ToUnixTimeMilliseconds(), result.TimestampType, result.LeaderEpoch)
                : result;
        }
        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }
}
