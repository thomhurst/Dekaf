using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.Protocol;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// A fetched batch whose parsed records stop short of its declared offset range must not let
/// the consumer stage the fetch end: offset 2 below was never delivered, so staging offset 3
/// would let a commit skip it permanently (#4027).
/// </summary>
public sealed class MalformedRecordTailConsumeTests
{
    private static readonly TopicPartition Partition = new("malformed-tail", 0);

    public enum ConsumeApi
    {
        Record,
        Batch,
        RawBatch
    }

    [Test]
    [MatrixDataSource]
    public async Task MalformedFinalRecord_SurfacesWithoutStagingUnreadOffsets(
        [Matrix(ConsumeApi.Record, ConsumeApi.Batch, ConsumeApi.RawBatch)] ConsumeApi api,
        [Matrix(false, true)] bool checkCrcs)
    {
        var batch = RecordBatchTests.ReadBatchWithMalformedFinalRecord(checkCrcs);

        await AssertCorruptionSurfacesWithoutStagingOffsets(batch, api);
    }

    [Test]
    [Arguments(ConsumeApi.Record)]
    [Arguments(ConsumeApi.Batch)]
    [Arguments(ConsumeApi.RawBatch)]
    public async Task DeclaredRecordCountShortfall_SurfacesWithoutStagingUnreadOffsets(ConsumeApi api)
    {
        // A complete frame holding two records but declaring three (offsets 0..2).
        var batch = ReadBatchDeclaringExtraRecord();

        await AssertCorruptionSurfacesWithoutStagingOffsets(batch, api);
    }

    private static async Task AssertCorruptionSurfacesWithoutStagingOffsets(RecordBatch batch, ConsumeApi api)
    {
        var pending = PendingFetchData.Create(Partition.Topic, Partition.Partition, [batch]);
        await Assert.That(pending.FetchEndOffsetExclusive).IsEqualTo(3L);
        await using var consumer = CreateConsumer(pending);

        var delivered = new List<long>();
        ConsumeException? exception = null;
        try
        {
            await ConsumeAsync(consumer, api, delivered);
        }
        catch (ConsumeException caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.IsRetriable).IsFalse();
        await Assert.That(delivered).IsEmpty();
        await Assert.That(consumer.DirtyStoredOffsetsForTest.ContainsKey(Partition)).IsFalse();
        await Assert.That(GetDictionary(consumer, "_positions")[Partition]).IsEqualTo(0L);
    }

    private static async Task ConsumeAsync(KafkaConsumer<string, string> consumer, ConsumeApi api, List<long> delivered)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        switch (api)
        {
            case ConsumeApi.Record:
                await foreach (var result in consumer.ConsumeAsync(cts.Token))
                {
                    delivered.Add(result.Offset);
                    break;
                }
                break;
            case ConsumeApi.Batch:
                await foreach (var batch in consumer.ConsumeBatchAsync(cts.Token))
                {
                    foreach (var result in batch)
                        delivered.Add(result.Offset);
                    break;
                }
                break;
            case ConsumeApi.RawBatch:
                await foreach (var batch in consumer.ConsumeRawBatchAsync(cts.Token))
                {
                    foreach (var result in batch)
                        delivered.Add(result.Offset);
                    break;
                }
                break;
        }
    }

    private static RecordBatch ReadBatchDeclaringExtraRecord()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var source = new RecordBatch
               {
                   Records =
                   [
                       new Record { OffsetDelta = 0, Value = "value-0"u8.ToArray() },
                       new Record { OffsetDelta = 1, Value = "value-1"u8.ToArray() }
                   ]
               })
        {
            source.Write(buffer);
        }

        var bytes = buffer.WrittenSpan.ToArray();
        // baseOffset(8) + batchLength(4) + partitionLeaderEpoch(4) + magic(1) + crc(4) + attributes(2).
        const int lastOffsetDeltaPosition = 23;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(lastOffsetDeltaPosition), 2);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
            bytes.AsSpan(RecordBatch.TotalBatchHeaderSize - sizeof(int)),
            3);
        var reader = new Dekaf.Protocol.KafkaProtocolReader(bytes);
        return RecordBatch.Read(ref reader);
    }

    private static KafkaConsumer<string, string> CreateConsumer(PendingFetchData fetch)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 50
            },
            Serializers.String,
            Serializers.String);
        consumer.Assign([Partition]);

        GetField("_initialized").SetValue(consumer, true);
        GetDictionary(consumer, "_fetchPositions").TryAdd(Partition, 0);
        GetDictionary(consumer, "_positions").TryAdd(Partition, 0);
        ((Queue<PendingFetchData>)GetField("_pendingFetches").GetValue(consumer)!).Enqueue(fetch);
        GetField("_pendingFetchDepth").SetValue(consumer, 1);
        return consumer;
    }

    private static ConcurrentDictionary<TopicPartition, long> GetDictionary(
        KafkaConsumer<string, string> consumer,
        string name) =>
        (ConcurrentDictionary<TopicPartition, long>)GetField(name).GetValue(consumer)!;

    private static FieldInfo GetField(string name) =>
        typeof(KafkaConsumer<string, string>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"{name} field not found");
}
