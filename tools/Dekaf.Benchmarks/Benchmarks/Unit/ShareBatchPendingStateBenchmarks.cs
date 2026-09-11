using System.Buffers;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Checks pending state across retained renewed batches without changing their lifecycle.</summary>
[MemoryDiagnoser]
public class ShareBatchPendingStateBenchmarks
{
    private ShareBatchAcknowledgements<int, int> _tracker = null!;
    private KafkaShareConsumer<int, int> _consumer = null!;

    [Params(1, 128, 1024)]
    public int BatchCount { get; set; }

    [Params(false, true)]
    public bool Pending { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        var value = new ArrayBufferWriter<byte>();
        Serializers.Int32.Serialize(42, ref value, default);
        var bytes = new ArrayBufferWriter<byte>();
        using (var source = new RecordBatch
        {
            Records = [new Record { IsKeyNull = true, Value = value.WrittenMemory }]
        })
            source.Write(bytes);

        _consumer = new KafkaShareConsumer<int, int>(new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = "pending-state-benchmark",
            AcknowledgementMode = ShareAcknowledgementMode.Explicit
        }, Serializers.Int32, Serializers.Int32);
        _tracker = new ShareBatchAcknowledgements<int, int>();
        var partition = new TopicPartition("pending-state", 0);
        ShareBatchStorage<int, int>? last = null;
        using (_tracker.BeginDelivery())
        {
            for (var offset = 0; offset < BatchCount; offset++)
            {
                var reader = new KafkaProtocolReader(bytes.WrittenMemory);
                var source = RecordBatch.Read(ref reader);
                source.BaseOffset = offset;
                using var batch = await _consumer.ParseRecordBatchAsync(partition, source,
                    [new ShareFetchAcquiredRecords { FirstOffset = offset, LastOffset = offset, DeliveryCount = 1 }],
                    1, default);
                _tracker.Register(batch);
                foreach (var record in batch)
                    batch.Acknowledge(record, AcknowledgeType.Renew);
                last = batch.Storage;
            }
        }
        _tracker.ApplySuccessfulAcknowledgements(_tracker.Flush());
        if (Pending)
            last!.Acknowledge(0, AcknowledgeType.Renew);
        if (_tracker.RetainedBatchCount != BatchCount || _tracker.HasPending != Pending)
            throw new InvalidOperationException("Retained renewal pending state changed.");
    }

    [Benchmark(OperationsPerInvoke = 1024)]
    public int CheckPending()
    {
        var pendingCount = 0;
        for (var index = 0; index < 1024; index++)
            pendingCount += ReadPending(_tracker) ? 1 : 0;
        return pendingCount;
    }

    // A lone inlined field read vanishes into BenchmarkDotNet's overhead subtraction.
    // Keep each read observable and normalize the repeated calls per operation.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ReadPending(ShareBatchAcknowledgements<int, int> tracker) => tracker.HasPending;

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        _tracker.Dispose();
        await _consumer.DisposeAsync();
    }
}
