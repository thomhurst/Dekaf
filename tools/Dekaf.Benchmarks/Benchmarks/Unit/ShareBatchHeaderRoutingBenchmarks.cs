using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Complete borrowed batches, including routing, traversal and storage release. Costs are per batch.</summary>
[MemoryDiagnoser]
public class ShareBatchHeaderRoutingBenchmarks
{
    private KafkaShareConsumer<int, int> _consumer = null!;
    private ReadOnlyMemory<byte> _bytes;
    private ShareFetchAcquiredRecords[] _acquired = null!;

    [Params(1, 64)]
    public int RecordCount { get; set; }

    [Params(8, 512)]
    public int KeyLength { get; set; }

    [Params(false, true)]
    public bool UnrelatedHeader { get; set; }

    [Params(false, true)]
    public bool FullCache { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        if (FullCache)
            FillHeaderCache();
        var name = new string('r', KeyLength);
        var router = new HeaderRoutingDeserializer<int>(name, Serializers.Int32,
            new HeaderDeserializerRoute<int>("selected"u8.ToArray(), Serializers.Int32));
        _consumer = new KafkaShareConsumer<int, int>(
            new ShareConsumerOptions { BootstrapServers = ["localhost:9092"], GroupId = "routing-benchmark" },
            Serializers.Int32, router);
        var records = new Record[RecordCount];
        for (var index = 0; index < records.Length; index++)
        {
            Header[] headers = UnrelatedHeader
                ? [new(name, "selected"u8.ToArray()), new(FullCache ? $"uncached-{index}" : "unrelated", "other"u8.ToArray())]
                : [new(name, "selected"u8.ToArray())];
            records[index] = new Record
            {
                OffsetDelta = index, IsKeyNull = true, Value = new byte[4], Headers = headers, HeaderCount = headers.Length
            };
        }
        using var source = new RecordBatch { Records = records };
        var output = new ArrayBufferWriter<byte>();
        source.Write(output);
        _bytes = output.WrittenMemory;
        _acquired = [new() { FirstOffset = 0, LastOffset = RecordCount - 1, DeliveryCount = 1 }];
        if (await ParseBatch() != RecordCount)
            throw new InvalidOperationException("The fixture must deliver every acquired record.");
    }

    private static void FillHeaderCache()
    {
        // The shared header cache admits 128 names. These names differ from every
        // measured routing/unrelated name; a full cache never admits the latter.
        for (var index = 0; index < 256; index++)
            _ = ReadHeader($"cache-fill-{index}");
        var first = ReadHeader("verify-full-cache");
        var second = ReadHeader("verify-full-cache");
        if (ReferenceEquals(first.Key, second.Key))
            throw new InvalidOperationException("The fixture requires an exhausted header cache.");
    }

    private static Header ReadHeader(string name)
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(output);
        var header = new Header(name, ReadOnlyMemory<byte>.Empty);
        HeaderProtocol.Write(in header, ref writer);
        var reader = new KafkaProtocolReader(output.WrittenMemory);
        return HeaderProtocol.Read(ref reader, output.WrittenCount);
    }

    [Benchmark]
    public async ValueTask<int> ParseBatch()
    {
        var reader = new KafkaProtocolReader(_bytes);
        using var batch = await _consumer.ParseRecordBatchAsync(new TopicPartition("routing", 0),
            RecordBatch.Read(ref reader), _acquired, RecordCount, default);
        var count = 0;
        foreach (var record in batch)
            count += record.Value + 1;
        return count;
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();
}
