using System.Buffers;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using KafkaRecord = Dekaf.Protocol.Records.Record;

namespace Dekaf.Tests.Unit.Performance;

/// <summary>
/// Fast, broker-free allocation regression gates for per-message hot paths.
/// </summary>
/// <remarks>
/// Allocation allowance list (bytes per measured window):
/// <list type="table">
/// <listheader><term>Path</term><description>Allowance</description></listheader>
/// <item><term>Protocol primitive writes</term><description>0</description></item>
/// <item><term>RecordAccumulator arena append</term><description>0</description></item>
/// <item><term>KafkaProducer FireAsync</term><description>0</description></item>
/// <item><term>Consumer raw-bytes decode</term><description>0</description></item>
/// </list>
/// Setup and known cold paths are deliberately outside the measured windows: buffer growth,
/// metadata/serializer-cache initialization, fetch-envelope and pool rents, batch rotation, and
/// stochastic background-drainer lag. Those paths are per-batch or cold-path work and must gain a
/// separately named, justified allowance before they are added to this gate.
/// </remarks>
[NotInParallel]
public class HotPathAllocationTests
{
    private const string Topic = "allocation-gate";
    private const int WarmupIterations = 128;
    private const int MeasuredIterations = 1_024;
    private const long ZeroAllocationBudget = 0;

    [Test]
    public async Task ProtocolWriter_PrimitiveWrites_AllocateZeroBytes()
    {
        var buffer = new ArrayBufferWriter<byte>(256);

        var measurement = WarmAndMeasure(
            () => WriteProtocolPrimitives(buffer),
            WarmupIterations,
            MeasuredIterations);

        await Assert.That(measurement.Checksum).IsGreaterThan(0);
        await Assert.That(measurement.AllocatedBytes).IsEqualTo(ZeroAllocationBudget);
    }

    [Test]
    public async Task RecordAccumulator_ArenaAppend_AllocatesZeroBytes()
    {
        var options = CreateProducerOptions();
        await using var accumulator = new RecordAccumulator(options);
        var key = "allocation-key"u8.ToArray();
        var value = "allocation-value"u8.ToArray();
        const long timestamp = 1_750_000_000_000;

        var measurement = WarmAndMeasure(
            () => AppendToAccumulator(accumulator, key, value, timestamp),
            WarmupIterations,
            MeasuredIterations);

        await Assert.That(measurement.Checksum).IsEqualTo(WarmupIterations + MeasuredIterations);
        await Assert.That(measurement.AllocatedBytes).IsEqualTo(ZeroAllocationBudget);
    }

    [Test]
    public async Task KafkaProducer_FireAsyncHotPath_AllocatesZeroBytes()
    {
        await using var producer = new KafkaProducer<string, string>(
            CreateProducerOptions(),
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer);
        SetInstanceField(producer, "_initialized", true);

        var measurement = WarmAndMeasure(
            () => Fire(producer),
            WarmupIterations,
            MeasuredIterations);

        await Assert.That(measurement.Checksum).IsEqualTo(WarmupIterations + MeasuredIterations);
        await Assert.That(measurement.AllocatedBytes).IsEqualTo(ZeroAllocationBudget);
    }

    [Test]
    public async Task Consumer_RawBytesDecodeHotPath_AllocatesZeroBytes()
    {
        using (var warmup = CreateConsumeBatch(WarmupIterations))
        {
            var warmupResult = ConsumeRawBytes(warmup.Batch);
            await Assert.That(warmupResult.RecordCount).IsEqualTo(WarmupIterations);
        }

        using var fixture = CreateConsumeBatch(MeasuredIterations);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = ConsumeRawBytes(fixture.Batch);
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(result.RecordCount).IsEqualTo(MeasuredIterations);
        await Assert.That(result.TotalBytes).IsEqualTo(MeasuredIterations * fixture.ValueLength);
        await Assert.That(allocatedBytes).IsEqualTo(ZeroAllocationBudget);
    }

    private static ProducerOptions CreateProducerOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "allocation-gate",
        BufferMemory = 32UL * 1024 * 1024,
        BatchSize = 1_048_576,
        LingerMs = 1_000,
        RequestTimeoutMs = 500,
        DeliveryTimeoutMs = 1_000,
        CloseTimeoutMs = 1_000,
        EnableIdempotence = false,
    };

    private static int WriteProtocolPrimitives(ArrayBufferWriter<byte> buffer)
    {
        buffer.Clear();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt8(-1);
        writer.WriteInt16(short.MinValue);
        writer.WriteInt32(int.MinValue);
        writer.WriteInt64(long.MinValue);
        writer.WriteVarInt(-123_456);
        writer.WriteVarLong(-123_456_789);
        writer.WriteString("dekaf");
        writer.WriteCompactString("allocation-gate");
        return writer.BytesWritten;
    }

    private static int AppendToAccumulator(
        RecordAccumulator accumulator,
        byte[] key,
        byte[] value,
        long timestamp)
    {
        var append = accumulator.AppendFromSpansAsync(
            Topic,
            partition: 0,
            timestamp,
            key,
            keyIsNull: false,
            value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            callback: null,
            CancellationToken.None);

        return append.GetAwaiter().GetResult() ? 1 : 0;
    }

    private static int Fire(KafkaProducer<string, string> producer)
    {
        producer.FireAsync(Topic, "allocation-key", "allocation-value").GetAwaiter().GetResult();
        return 1;
    }

    private static AllocationMeasurement WarmAndMeasure(
        Func<int> operation,
        int warmupIterations,
        int measuredIterations)
    {
        var checksum = 0;
        for (var i = 0; i < warmupIterations; i++)
            checksum += operation();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < measuredIterations; i++)
            checksum += operation();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        GC.KeepAlive(operation);
        return new AllocationMeasurement(allocatedBytes, checksum);
    }

    private static ConsumeBatchFixture CreateConsumeBatch(int recordCount)
    {
        var key = "key"u8.ToArray();
        var value = "allocation-value"u8.ToArray();
        var records = new KafkaRecord[recordCount];
        for (var i = 0; i < recordCount; i++)
        {
            records[i] = new KafkaRecord
            {
                OffsetDelta = i,
                TimestampDelta = i,
                Key = key,
                Value = value,
                IsKeyNull = false,
                IsValueNull = false,
            };
        }

        var recordBatch = RecordBatch.RentFromPool();
        recordBatch.BaseOffset = 0;
        recordBatch.BaseTimestamp = 1_750_000_000_000;
        recordBatch.MaxTimestamp = recordBatch.BaseTimestamp + recordCount - 1;
        recordBatch.LastOffsetDelta = recordCount - 1;
        recordBatch.Attributes = RecordBatchAttributes.None;
        recordBatch.Records = records;

        var pending = PendingFetchData.Create(Topic, partitionIndex: 0, [recordBatch]);
        pending.EagerParseAll();
        var batch = new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(
            pending,
            Serializers.Ignore,
            Serializers.RawBytes);

        return new ConsumeBatchFixture(pending, batch, value.Length);
    }

    private static ConsumeMeasurement ConsumeRawBytes(ConsumeBatch<Ignore, ReadOnlyMemory<byte>> batch)
    {
        var recordCount = 0;
        var totalBytes = 0;
        foreach (var record in batch)
        {
            recordCount++;
            totalBytes += record.Value.Length;
        }

        return new ConsumeMeasurement(recordCount, totalBytes);
    }

    private static void SeedProducerMetadata(KafkaProducer<string, string> producer)
    {
        var metadataManager = GetInstanceField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata
                {
                    NodeId = 0,
                    Host = "localhost",
                    Port = 9092,
                },
            ],
            ClusterId = "allocation-gate-cluster",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ReplicaNodes = [0],
                            IsrNodes = [0],
                        },
                    ],
                },
            ],
        });
    }

    private static async Task StopProducerBackgroundLoopsAsync(KafkaProducer<string, string> producer)
    {
        var cancellation = GetInstanceField<CancellationTokenSource>(producer, "_senderCts");
        var senderTask = GetInstanceField<Task>(producer, "_senderTask");
        var lingerTask = GetInstanceField<Task>(producer, "_lingerTask");

        await cancellation.CancelAsync();
        await Task.WhenAll(senderTask, lingerTask).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static T GetInstanceField<T>(object target, string name)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = target.GetType().GetField(name, Flags);
        return (T)field!.GetValue(target)!;
    }

    private static void SetInstanceField<T>(object target, string name, T value)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = target.GetType().GetField(name, Flags);
        field!.SetValue(target, value);
    }

    private readonly record struct AllocationMeasurement(long AllocatedBytes, int Checksum);

    private readonly record struct ConsumeMeasurement(int RecordCount, int TotalBytes);

    private sealed class ConsumeBatchFixture(
        PendingFetchData pending,
        ConsumeBatch<Ignore, ReadOnlyMemory<byte>> batch,
        int valueLength) : IDisposable
    {
        public ConsumeBatch<Ignore, ReadOnlyMemory<byte>> Batch { get; } = batch;

        public int ValueLength { get; } = valueLength;

        public void Dispose() => pending.Dispose();
    }
}
