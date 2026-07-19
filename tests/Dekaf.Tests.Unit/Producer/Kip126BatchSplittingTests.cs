using System.Buffers;
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
        var normalArenaCapacity = ProducerOptions.GetEffectiveArenaCapacity(
            options.BatchSize,
            options.ArenaCapacity);
        await Assert.That(trainedTopicBatch.Arena!.Capacity).IsEqualTo(normalArenaCapacity);

        for (var i = 0; i < 10; i++)
            Append(trainedTopicBatch, i, completionSource: null, callback: null);
        await Assert.That(trainedTopicBatch.Arena!.Capacity).IsGreaterThan(normalArenaCapacity);

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
    [NotInParallel]
    public async Task FirstLateCallback_GrowsArrayToRecordIndex()
    {
        var staleCallbackCount = 0;
        var staleCallbacks = ProducerContainerPools.Callbacks.Rent(128);
        for (var i = 0; i <= 100; i++)
            staleCallbacks[i] = (_, _) => staleCallbackCount++;
        ProducerContainerPools.Callbacks.Return(staleCallbacks, clearArray: false);

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 64 * 1024,
            InitialBatchRecordCapacity = 16
        };
        var batch = new PartitionBatch(new TopicPartition("late-callback", 0), options);
        RecordMetadata callbackMetadata = default;

        for (var i = 0; i < 100; i++)
            Append(batch, i, completionSource: null, callback: null);
        Append(batch, 100, completionSource: null, (metadata, _) => callbackMetadata = metadata);

        var ready = batch.Complete()!;
        ready.TrySetMemoryReleased();
        ready.CompleteSend(500, DateTimeOffset.UnixEpoch);

        await Assert.That(callbackMetadata.Offset).IsEqualTo(600);
        await Assert.That(staleCallbackCount).IsEqualTo(0);
    }

    [Test]
    public async Task SplitAndReenqueue_ReplacesSourceBufferMemoryReservation()
    {
        const string topic = "kip-126-accounting";
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1000,
            BufferMemory = 1024 * 1024,
            CompressionType = CompressionType.None,
            LingerMs = 60_000
        };
        var accumulator = new RecordAccumulator(options, new CompressionCodecRegistry());
        var metadataManager = AccumulatorTestHelpers.CreateMetadataManager(topic, partitionCount: 1);
        try
        {
            for (var i = 0; i < 4; i++)
            {
                await Assert.That(await accumulator.AppendAsync(
                    topic,
                    partition: 0,
                    timestamp: 1_000 + i,
                    PooledMemory.Null,
                    PooledMemory.Null,
                    headers: null,
                    headerCount: 0,
                    completionSource: null,
                    callback: null,
                    CancellationToken.None)).IsTrue();
            }

            await AccumulatorTestHelpers.SealAllAsync(accumulator);
            var source = await DrainOneAsync(accumulator, metadataManager);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(source.DataSize);
            var flushTask = accumulator.FlushAsync(CancellationToken.None).AsTask();

            await Assert.That(accumulator.SplitAndReenqueue(source, source.Generation)).IsTrue();
            await Assert.That(flushTask.IsCompleted).IsFalse();
            var child = await DrainOneAsync(accumulator, metadataManager);

            await Assert.That(child.RecordCount).IsEqualTo(4);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(child.DataSize);

            CompleteAndReturn(accumulator, child, baseOffset: 0);
            await flushTask;
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    [Arguments(BufferMemoryAllocationStrategy.Full)]
    [Arguments(BufferMemoryAllocationStrategy.Incremental)]
    public async Task SplitAndReenqueue_PreservesOrderSequencesAndDeliveryOwnership(
        BufferMemoryAllocationStrategy allocationStrategy)
    {
        const string topic = "kip-126";
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1000,
            BufferMemory = 1024 * 1024,
            CompressionType = CompressionType.Gzip,
            BufferMemoryAllocationStrategy = allocationStrategy,
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
        await Assert.That(sourceBatch.RecordBatch.PreEncodedRecordsLength).IsGreaterThan(0);
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
                CompleteAndReturn(accumulator, batch, baseOffset);
                baseOffset += batch.RecordCount;
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

    [Test]
    public async Task SplitForSenderRetry_ReturnsOrderedChildrenWithoutAccumulatorPublish()
    {
        const string topic = "sender-retry-split";
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1000,
            BufferMemory = 1024 * 1024,
            CompressionType = CompressionType.None,
            LingerMs = 0
        };
        var sourceBuilder = new PartitionBatch(new TopicPartition(topic, 0), options);
        sourceBuilder.SetSplitBatchSizeLimit(4096);
        for (var i = 0; i < 8; i++)
            Append(sourceBuilder, i, completionSource: null, callback: null);

        var source = sourceBuilder.Complete()!;
        source.RecordBatch.BaseSequence = 40;
        source.MarkAsSplitBatch(maxRecordSize: 128);
        source.MarkPreSerialized();
        source.TrySetMemoryReleased();

        await using var accumulator = new RecordAccumulator(options, new CompressionCodecRegistry());
        await using var metadataManager = AccumulatorTestHelpers.CreateMetadataManager(topic, partitionCount: 1);
        var children = accumulator.SplitForSenderRetry(source, source.Generation)!;
        try
        {
            await Assert.That(children).Count().IsGreaterThan(1);
            await Assert.That(children.All(static child => child.IsPreSerialized)).IsTrue();
            await Assert.That(children.Sum(static child => child.RecordCount)).IsEqualTo(8);

            var expectedSequence = 40;
            foreach (var child in children)
            {
                await Assert.That(child.RecordBatch.BaseSequence).IsEqualTo(expectedSequence);
                expectedSequence += child.RecordCount;
            }

            var readyNodes = new HashSet<int>();
            accumulator.Ready(metadataManager, readyNodes);
            await Assert.That(readyNodes).IsEmpty();
        }
        finally
        {
            var baseOffset = 0L;
            foreach (var child in children)
            {
                CompleteAndReturn(accumulator, child, baseOffset);
                baseOffset += child.RecordCount;
            }
        }
    }

    [Test]
    public async Task Drain_AdaptiveBatchExpandsLocally_SplitsAndReenqueues()
    {
        const string topic = "adaptive-local-split";
        const int recordCount = 100;
        const int maxRequestSize = 512;
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 256,
            MaxRequestSize = maxRequestSize,
            BufferMemory = 1024 * 1024,
            CompressionType = CompressionType.Gzip,
            LingerMs = 60_000
        };
        var codecs = new CompressionCodecRegistry();
        codecs.Register(new SizeSensitiveCompressionCodec(maxRequestSize, options.BatchSize));
        await using var accumulator = new RecordAccumulator(options, codecs);
        await using var metadataManager = AccumulatorTestHelpers.CreateMetadataManager(topic, partitionCount: 1);

        // Model a topic that has learned a strong ratio, allowing the adaptive parent
        // to grow well beyond BatchSize before its actual compression ratio is known.
        var estimator = AccumulatorTestHelpers.GetPrivateField<CompressionRatioEstimator>(
            accumulator,
            "_compressionRatioEstimator");
        for (var i = 0; i < 200; i++)
            estimator.Update(topic, CompressionType.Gzip, observedRatio: 0.1);

        for (var i = 0; i < recordCount; i++)
            await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, topic)).IsTrue();
        await AccumulatorTestHelpers.SealAllAsync(accumulator);

        var readyNodes = await WaitForReadyNodeAsync(accumulator, metadataManager);
        var firstDrain = new Dictionary<int, List<ReadyBatch>>();
        accumulator.Drain(
            metadataManager,
            readyNodes,
            maxRequestSize,
            firstDrain,
            new Stack<List<ReadyBatch>>());

        await Assert.That(firstDrain).DoesNotContainKey(1);
        await Assert.That(accumulator.HasPendingWork()).IsTrue();

        var drainedRecords = 0;
        var childCount = 0;
        while (drainedRecords < recordCount)
        {
            var child = await DrainOneAsync(accumulator, metadataManager);
            drainedRecords += child.RecordCount;
            childCount++;
            CompleteAndReturn(accumulator, child, drainedRecords - child.RecordCount);
        }

        await Assert.That(childCount).IsGreaterThan(1);
        await Assert.That(drainedRecords).IsEqualTo(recordCount);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        await Assert.That(accumulator.HasPendingWork()).IsFalse();
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
        var readyNodes = await WaitForReadyNodeAsync(accumulator, metadataManager);

        var drainResult = new Dictionary<int, List<ReadyBatch>>();
        accumulator.Drain(
            metadataManager,
            readyNodes,
            int.MaxValue,
            drainResult,
            new Stack<List<ReadyBatch>>());
        return drainResult[1][0];
    }

    private static async Task<HashSet<int>> WaitForReadyNodeAsync(
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
        return readyNodes;
    }

    private static void CompleteAndReturn(
        RecordAccumulator accumulator,
        ReadyBatch batch,
        long baseOffset)
    {
        batch.CompleteSend(baseOffset, DateTimeOffset.UnixEpoch);
        accumulator.ReleaseBatchMemory(batch);
        accumulator.OnBatchExitsPipeline(batch);
        accumulator.ReturnReadyBatch(batch);
    }

    private sealed class SizeSensitiveCompressionCodec(int expandedSize, int expandAboveBytes)
        : ICompressionCodec
    {
        public CompressionType Type => CompressionType.Gzip;

        public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
        {
            var size = source.Length > expandAboveBytes ? expandedSize : 1;
            destination.GetSpan(size)[..size].Clear();
            destination.Advance(size);
        }

        public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
        {
            foreach (var segment in source)
                destination.Write(segment.Span);
        }
    }
}
