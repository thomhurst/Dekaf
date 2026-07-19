using System.Buffers;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public class IncrementalBatchBufferTests
{
    [Test]
    public async Task ChunkPoolSize_ScalesByChunksPerLiveBatch()
    {
        const int liveBatchCount = 256;
        const int batchSize = 1024 * 1024;

        var poolSize = IncrementalBatchBuffer.ComputeChunkPoolSize(liveBatchCount, batchSize);
        var partialChunkPoolSize = IncrementalBatchBuffer.ComputeChunkPoolSize(
            liveBatchCount,
            IncrementalBatchBuffer.MaximumChunkSize + 1);

        await Assert.That(poolSize).IsEqualTo(liveBatchCount * 64);
        await Assert.That(partialChunkPoolSize).IsEqualTo(liveBatchCount * 2);
    }

    [Test]
    public async Task IncrementalStrategy_AllocatesStorageAsRecordsArrive()
    {
        var incremental = new PartitionBatch(
            new TopicPartition("topic", 0),
            CreateOptions(BufferMemoryAllocationStrategy.Incremental));
        var full = new PartitionBatch(
            new TopicPartition("topic", 0),
            CreateOptions(BufferMemoryAllocationStrategy.Full));

        await Assert.That(incremental.RetainedBatchBufferCapacity).IsEqualTo(0);
        await Assert.That(full.RetainedBatchBufferCapacity).IsGreaterThanOrEqualTo(1024 * 1024);

        var result = Append(incremental, timestamp: 1, new byte[32]);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(incremental.RetainedBatchBufferCapacity)
            .IsLessThanOrEqualTo(IncrementalBatchBuffer.MaximumChunkSize);

        incremental.Complete()!.CompleteSend(0, DateTimeOffset.UnixEpoch);
        full.FailCompleteFailure(new InvalidOperationException("test cleanup"));
    }

    [Test]
    [Arguments(CompressionType.None)]
    [Arguments(CompressionType.Gzip)]
    public async Task IncrementalStrategy_ProducesByteEquivalentBatch(CompressionType compression)
    {
        var full = CreateCompletedBatch(BufferMemoryAllocationStrategy.Full);
        var incremental = CreateCompletedBatch(BufferMemoryAllocationStrategy.Incremental);

        try
        {
            full.RecordBatch.PreCompress(compression, null);
            incremental.RecordBatch.PreCompress(compression, null);

            var fullBytes = Write(full.RecordBatch, compression);
            var incrementalBytes = Write(incremental.RecordBatch, compression);

            if (compression == CompressionType.None)
            {
                await Assert.That(incrementalBytes).IsEquivalentTo(fullBytes);
            }
            else
            {
                using var parsedFull = Read(fullBytes);
                using var parsedIncremental = Read(incrementalBytes);
                await Assert.That(parsedIncremental.Records.Count).IsEqualTo(parsedFull.Records.Count);
                for (var i = 0; i < parsedFull.Records.Count; i++)
                {
                    await Assert.That(parsedIncremental.Records[i].Value.ToArray())
                        .IsEquivalentTo(parsedFull.Records[i].Value.ToArray());
                }
            }
        }
        finally
        {
            full.CompleteSend(0, DateTimeOffset.UnixEpoch);
            incremental.CompleteSend(0, DateTimeOffset.UnixEpoch);
        }
    }

    [Test]
    public async Task IncrementalStrategy_EncodingFailureRewindsAndReturnsChunk()
    {
        var buffer = IncrementalBatchBuffer.Rent(1024 * 1024);
        try
        {
            buffer.Allocate(200, out var first, out var firstOffset, out var logicalOffset);
            first.AsSpan(firstOffset, 200).Fill(0x2a);

            await Assert.That(buffer.Length).IsEqualTo(200);
            await Assert.That(buffer.TryRewindLastAllocation(logicalOffset, 200)).IsTrue();
            await Assert.That(buffer.Length).IsEqualTo(0);
            await Assert.That(buffer.RetainedCapacity).IsEqualTo(0);
        }
        finally
        {
            IncrementalBatchBuffer.ReturnToPool(buffer);
        }
    }

    [Test]
    public async Task IncrementalStrategy_FirstOversizedRecordUsesDedicatedChunk()
    {
        var options = CreateOptions(
            BufferMemoryAllocationStrategy.Incremental,
            batchSize: 1024,
            maxRequestSize: 64 * 1024);
        var batch = new PartitionBatch(new TopicPartition("topic", 0), options);
        var value = new byte[8 * 1024];

        var result = Append(batch, timestamp: 1, value);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(batch.RetainedBatchBufferCapacity).IsGreaterThanOrEqualTo(value.Length);
        batch.Complete()!.CompleteSend(0, DateTimeOffset.UnixEpoch);
    }

    private static ProducerOptions CreateOptions(
        BufferMemoryAllocationStrategy strategy,
        int batchSize = 1024 * 1024,
        int maxRequestSize = 1024 * 1024) => new()
    {
        BootstrapServers = ["localhost:9092"],
        BufferMemory = 64 * 1024 * 1024,
        BatchSize = batchSize,
        MaxRequestSize = maxRequestSize,
        LingerMs = 10_000,
        BufferMemoryAllocationStrategy = strategy
    };

    private static ReadyBatch CreateCompletedBatch(BufferMemoryAllocationStrategy strategy)
    {
        var batch = new PartitionBatch(new TopicPartition("topic", 3), CreateOptions(strategy));
        for (var i = 0; i < 40; i++)
        {
            var value = new byte[997 + i];
            value.AsSpan().Fill((byte)i);
            var result = Append(batch, 1_700_000_000_000 + i, value);
            if (!result.Success)
                throw new InvalidOperationException($"Record {i} did not fit.");
        }

        return batch.Complete()!;
    }

    private static RecordAppendResult Append(PartitionBatch batch, long timestamp, byte[] value) =>
        batch.TryAppendFromSpans(
            timestamp,
            ReadOnlySpan<byte>.Empty,
            keyIsNull: true,
            value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            PartitionBatch.EstimateRecordSize(0, value.Length, null, 0));

    private static byte[] Write(RecordBatch batch, CompressionType compression)
    {
        var output = new ArrayBufferWriter<byte>();
        batch.Write(output, compression);
        return output.WrittenSpan.ToArray();
    }

    private static RecordBatch Read(byte[] bytes)
    {
        var reader = new KafkaProtocolReader(bytes);
        return RecordBatch.Read(ref reader, codecs: null, availableBytes: bytes.Length, checkCrcs: true);
    }
}
