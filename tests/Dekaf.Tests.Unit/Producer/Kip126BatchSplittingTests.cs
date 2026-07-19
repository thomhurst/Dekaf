using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

public sealed class Kip126BatchSplittingTests
{
    [Test]
    public async Task CompressionEstimate_IsPerTopic_AndResetIsConservative()
    {
        var estimator = new CompressionRatioEstimator();

        for (var i = 0; i < 100; i++)
            estimator.Update("compressible", CompressionType.Gzip, 0.1);

        await Assert.That(estimator.Get("compressible", CompressionType.Gzip)).IsLessThan(1.0);
        await Assert.That(estimator.Get("other", CompressionType.Gzip)).IsEqualTo(1.0);

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1024,
            CompressionType = CompressionType.Gzip
        };
        var trainedTopicBatch = new PartitionBatch(
            new TopicPartition("compressible", 0), options, estimator);
        var untrainedTopicBatch = new PartitionBatch(
            new TopicPartition("other", 0), options, estimator);
        await Assert.That(trainedTopicBatch.EffectiveBatchSizeLimit)
            .IsGreaterThan(untrainedTopicBatch.EffectiveBatchSizeLimit);
        trainedTopicBatch.Complete();
        untrainedTopicBatch.Complete();

        estimator.ResetAfterSplit("compressible", CompressionType.Gzip, 0.2);

        await Assert.That(estimator.Get("compressible", CompressionType.Gzip)).IsEqualTo(1.0);
    }

    [Test]
    public async Task CompletionAndCallbackOffsets_FollowOriginalRecordIndexes()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"], BatchSize = 4096 };
        var batch = new PartitionBatch(new TopicPartition("offsets", 0), options);
        var sourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var first = sourcePool.Rent();
        var third = sourcePool.Rent();
        var firstTask = first.Task;
        var thirdTask = third.Task;
        RecordMetadata callbackMetadata = default;

        Append(batch, 0, first, null);
        Append(batch, 1, null, (metadata, error) => callbackMetadata = metadata);
        Append(batch, 2, third, null);

        var ready = batch.Complete()!;
        ready.TrySetMemoryReleased();
        ready.CompleteSend(100, DateTimeOffset.UnixEpoch);

        await Assert.That((await firstTask).Offset).IsEqualTo(100);
        await Assert.That(callbackMetadata.Offset).IsEqualTo(101);
        await Assert.That((await thirdTask).Offset).IsEqualTo(102);
        await sourcePool.DisposeAsync();
    }

    [Test]
    public async Task SplitAndReenqueue_PreservesOrderSequencesAndDeliveryOwnership()
    {
        const string topic = "kip-126";
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1000,
            BufferMemory = 1024 * 1024,
            CompressionType = CompressionType.Gzip,
            LingerMs = 0
        };
        var sourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sourceBuilder = new PartitionBatch(new TopicPartition(topic, 0), options);
        sourceBuilder.SetSplitBatchSizeLimit(4096);
        var tasks = new ValueTask<RecordMetadata>[4];
        var callbacks = new RecordMetadata[4];

        for (var i = 0; i < tasks.Length; i++)
        {
            var source = sourcePool.Rent();
            tasks[i] = source.Task;
            var callbackIndex = i;
            Append(sourceBuilder, i, source, (metadata, error) => callbacks[callbackIndex] = metadata);
        }

        var sourceBatch = sourceBuilder.Complete()!;
        sourceBatch.RecordBatch.BaseSequence = 50;
        sourceBatch.RecordBatch.PreCompress(options.CompressionType, new CompressionCodecRegistry());
        sourceBatch.ObservedCompressionRatio = (double)sourceBatch.RecordBatch.PreCompressedLength
            / sourceBatch.RecordBatch.PreEncodedRecordsLength;
        sourceBatch.SetEncodedSize(sourceBatch.RecordBatch.GetEncodedSize(options.CompressionType));
        sourceBatch.MarkPreSerialized();
        sourceBatch.TrySetMemoryReleased();
        var originalCreatedTicks = sourceBatch.StopwatchCreatedTicks;

        var accumulator = new RecordAccumulator(options, new CompressionCodecRegistry());
        var metadataManager = AccumulatorTestHelpers.CreateMetadataManager(topic, partitionCount: 1);
        var drained = new List<ReadyBatch>();
        try
        {
            accumulator.MutePartition(new TopicPartition(topic, 0));
            await Assert.That(accumulator.SplitAndReenqueue(sourceBatch, sourceBatch.Generation)).IsTrue();
            accumulator.UnmutePartition(new TopicPartition(topic, 0));

            var rejectedSplit = await DrainOneAsync(accumulator, metadataManager);
            await Assert.That(rejectedSplit.RecordCount).IsEqualTo(4);
            accumulator.ReleaseBatchMemory(rejectedSplit);
            await Assert.That(accumulator.SplitAndReenqueue(rejectedSplit, rejectedSplit.Generation)).IsTrue();

            while (drained.Sum(static batch => batch.RecordCount) < tasks.Length)
                drained.Add(await DrainOneAsync(accumulator, metadataManager));

            await Assert.That(drained.Count).IsGreaterThan(1);
            await Assert.That(drained.Select(static batch => batch.RecordBatch.BaseSequence))
                .IsEquivalentTo([50, 52]);
            await Assert.That(drained.All(batch => batch.StopwatchCreatedTicks == originalCreatedTicks)).IsTrue();

            var baseOffset = 100L;
            foreach (var batch in drained)
            {
                batch.CompleteSend(baseOffset, DateTimeOffset.UnixEpoch);
                baseOffset += batch.RecordCount;
                accumulator.ReleaseBatchMemory(batch);
                accumulator.OnBatchExitsPipeline(batch);
                accumulator.ReturnReadyBatch(batch);
            }

            for (var i = 0; i < tasks.Length; i++)
            {
                await Assert.That((await tasks[i]).Offset).IsEqualTo(100 + i);
                await Assert.That(callbacks[i].Offset).IsEqualTo(100 + i);
            }

            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await metadataManager.DisposeAsync();
            await sourcePool.DisposeAsync();
        }
    }

    private static void Append(
        PartitionBatch batch,
        int index,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback)
    {
        var value = new byte[120];
        value.AsSpan().Fill((byte)index);
        var result = batch.TryAppendFromSpans(
            timestamp: 1_000 + index,
            keyData: default,
            keyIsNull: true,
            valueData: value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            completionSource,
            callback,
            estimatedSize: PartitionBatch.EstimateRecordSize(0, value.Length, null, 0));
        if (!result.Success)
            throw new InvalidOperationException("Test record did not fit.");
    }

    private static async Task<ReadyBatch> DrainOneAsync(
        RecordAccumulator accumulator,
        MetadataManager metadataManager)
    {
        var readyNodes = new HashSet<int>();
        await TestWait.UntilAsync(
            () =>
            {
                readyNodes.Clear();
                accumulator.Ready(metadataManager, readyNodes);
                return readyNodes.Contains(1);
            },
            TimeSpan.FromSeconds(5));

        var drainResult = new Dictionary<int, List<ReadyBatch>>();
        accumulator.Drain(
            metadataManager,
            readyNodes,
            int.MaxValue,
            drainResult,
            new Stack<List<ReadyBatch>>());
        return drainResult[1][0];
    }
}
