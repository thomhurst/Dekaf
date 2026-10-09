using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// A batch the caller resumes past without enumerating proves nothing, so it must be
/// redelivered. It must not be re-yielded from the head of the queue forever: that spun
/// one core, starved every partition queued behind it and skipped assignment sync.
/// </summary>
public sealed class ConsumeBatchSkipTests
{
    private const string Topic = "skip-topic";
    private static readonly TopicPartition Partition0 = new(Topic, 0);
    private static readonly TopicPartition Partition1 = new(Topic, 1);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SkippedBatch_DoesNotBlockOtherPartitions(bool raw)
    {
        await using var consumer = CreateConsumer(
            CreatePendingFetch(Partition0, 10, 2),
            CreatePendingFetch(Partition1, 20, 2));
        await using var batches = Open(consumer, raw);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition0);

        // Skip partition 0 without enumerating. Partition 1 must be delivered next.
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition1);
        await Assert.That(batches.Current.Offsets()).IsEquivalentTo([20L, 21L]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SkippedBatch_RewindsToConsumedPositionWithoutStagingOffsets(bool raw)
    {
        await using var consumer = CreateConsumer(
            CreatePendingFetch(Partition0, 10, 2),
            CreatePendingFetch(Partition1, 20, 1));
        await using var batches = Open(consumer, raw);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();

        await Assert.That(GetPendingFetches(consumer).Any(static f => f.TopicPartition == Partition0)).IsFalse();
        await Assert.That(GetDictionary(consumer, "_fetchPositions")[Partition0]).IsEqualTo(10L);
        await Assert.That(GetDictionary(consumer, "_positions")[Partition0]).IsEqualTo(10L);
        await Assert.That(GetDictionary(consumer, "_dirtyStoredOffsets").ContainsKey(Partition0)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SkippedRemainderAfterPartialProgress_RewindsToFirstUndeliveredRecord(bool raw)
    {
        await using var consumer = CreateConsumer(
            maxPollRecords: 1,
            CreatePendingFetch(Partition0, 10, 3),
            CreatePendingFetch(Partition1, 20, 1));
        await using var batches = Open(consumer, raw);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Offsets()).IsEquivalentTo([10L]);

        // Progress keeps the same fetch at the head for its next window.
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition0);

        // Skipping that window releases it; offset 11 was never delivered.
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition1);
        await Assert.That(GetDictionary(consumer, "_fetchPositions")[Partition0]).IsEqualTo(11L);
        await Assert.That(GetDictionary(consumer, "_positions")[Partition0]).IsEqualTo(11L);
        await Assert.That(GetDictionary(consumer, "_dirtyStoredOffsets")[Partition0]).IsEqualTo(11L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SkippedOnlyBatch_IsNotReyieldedFromTheQueue(bool raw)
    {
        // Prefetch is marked started but never runs, so after the release the stream waits on
        // the empty prefetch buffer: no broker I/O, and only cancellation ends the wait.
        await using var consumer = CreateConsumer(
            500, null, prefetch: true, unknownPosition: null,
            CreatePendingFetch(Partition0, 10, 2));
        using var cts = new CancellationTokenSource();
        await using var batches = Open(consumer, raw, cts.Token);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();

        var next = batches.MoveNextAsync().AsTask();
        // The release happens synchronously inside MoveNextAsync, before it waits for data.
        await Assert.That(GetPendingFetches(consumer)).IsEmpty();
        await Assert.That(next.IsCompleted).IsFalse();

        await cts.CancelAsync();
        var reyielded = false;
        try
        {
            reyielded = await next;
        }
        catch (OperationCanceledException)
        {
        }

        await Assert.That(reyielded).IsFalse();
        await Assert.That(GetPendingFetches(consumer)).IsEmpty();
        await Assert.That(GetDictionary(consumer, "_fetchPositions")[Partition0]).IsEqualTo(10L);
    }

    [Test]
    public async Task SkippedBatch_WithInterceptor_ReleasesRetainedFetch()
    {
        var interceptor = new PassThroughInterceptor();
        await using var consumer = CreateConsumer(
            maxPollRecords: 500,
            interceptors: [interceptor],
            CreatePendingFetch(Partition0, 10, 2),
            CreatePendingFetch(Partition1, 20, 1));
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(Partition0);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(Partition1);
        await Assert.That(batches.Current.Select(static r => r.Offset).ToArray()).IsEquivalentTo([20L]);
        await Assert.That(interceptor.Calls).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SkippedBatch_PausedDuringYield_StaysParkedForResume(bool raw)
    {
        await using var consumer = CreateConsumer(
            CreatePendingFetch(Partition0, 10, 2),
            CreatePendingFetch(Partition1, 20, 1));
        await using var batches = Open(consumer, raw);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        consumer.Pause(Partition0);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition1);
        _ = batches.Current.Offsets();

        consumer.Resume(Partition0);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition0);
        await Assert.That(batches.Current.Offsets()).IsEquivalentTo([10L, 11L]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SkippedBatch_SeekDuringYield_KeepsSeekPosition(bool raw)
    {
        await using var consumer = CreateConsumer(
            CreatePendingFetch(Partition0, 10, 2),
            CreatePendingFetch(Partition1, 20, 1));
        await using var batches = Open(consumer, raw);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        consumer.Seek(new TopicPartitionOffset(Topic, 0, 5));

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition1);
        await Assert.That(GetDictionary(consumer, "_fetchPositions")[Partition0]).IsEqualTo(5L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnreleasableSkip_PositionUnknownUnderPrefetch_RotatesBehindOtherPartitions(bool raw)
    {
        await using var consumer = CreateConsumer(
            500, null, prefetch: true, unknownPosition: Partition0,
            CreatePendingFetch(Partition0, 10, 2),
            CreatePendingFetch(Partition1, 20, 1),
            CreatePendingFetch(Partition0, 12, 1));
        await using var batches = Open(consumer, raw);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition0);

        // The skipped fetch cannot be refetched, so it is kept, but partition 1 goes first.
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition1);
        await Assert.That(batches.Current.Offsets()).IsEquivalentTo([20L]);

        // Partition 0 is then redelivered in order, without loss.
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Offsets()).IsEquivalentTo([10L, 11L]);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Offsets()).IsEquivalentTo([12L]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnreleasableSkip_SnapshotReadActive_RotatesBehindOtherPartitions(bool raw)
    {
        await using var consumer = CreateConsumer(
            CreatePendingFetch(Partition0, 10, 2),
            CreatePendingFetch(Partition1, 20, 1));
        var snapshotActive = GetField("_snapshotOperationActive");
        snapshotActive.SetValue(consumer, 1);
        try
        {
            await using var batches = Open(consumer, raw);

            await Assert.That(await batches.MoveNextAsync()).IsTrue();
            await Assert.That(batches.Current.Partition).IsEqualTo(Partition0);

            await Assert.That(await batches.MoveNextAsync()).IsTrue();
            await Assert.That(batches.Current.Partition).IsEqualTo(Partition1);
            _ = batches.Current.Offsets();

            await Assert.That(await batches.MoveNextAsync()).IsTrue();
            await Assert.That(batches.Current.Partition).IsEqualTo(Partition0);
            await Assert.That(batches.Current.Offsets()).IsEquivalentTo([10L, 11L]);
        }
        finally
        {
            snapshotActive.SetValue(consumer, 0);
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task UnreleasableSkip_SkipEverythingCaller_IsRateLimited(bool raw, bool snapshot)
    {
        await using var consumer = snapshot
            ? CreateConsumer(CreatePendingFetch(Partition0, 10, 2))
            : CreateConsumer(500, null, prefetch: true, unknownPosition: Partition0,
                CreatePendingFetch(Partition0, 10, 2));
        var snapshotActive = GetField("_snapshotOperationActive");
        if (snapshot)
            snapshotActive.SetValue(consumer, 1);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var yields = 0;
            try
            {
                await using var batches = Open(consumer, raw, cts.Token);
                while (await batches.MoveNextAsync())
                    yields++;
            }
            catch (OperationCanceledException)
            {
            }

            // The held batch is re-offered after a bounded poll wait (50 ms here), never in a
            // tight loop. A spinning loop yields many thousands of times per second.
            await Assert.That(yields).IsGreaterThan(0);
            await Assert.That(yields).IsLessThanOrEqualTo(40);
            await Assert.That(GetPendingFetches(consumer).Count).IsEqualTo(1);
        }
        finally
        {
            snapshotActive.SetValue(consumer, 0);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DiscardedEnumeratorWithoutMoveNext_CountsAsSkip(bool raw)
    {
        await using var consumer = CreateConsumer(
            CreatePendingFetch(Partition0, 10, 2),
            CreatePendingFetch(Partition1, 20, 1));
        await using var batches = Open(consumer, raw);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition0);
        batches.Current.CreateEnumeratorOnly();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(Partition1);
        await Assert.That(GetDictionary(consumer, "_fetchPositions")[Partition0]).IsEqualTo(10L);
    }

    private sealed class TypedEnumerator(IAsyncEnumerator<ConsumeBatch<string, string>> inner)
        : IAsyncEnumerator<BatchView>
    {
        public BatchView Current => new(
            inner.Current.TopicPartition,
            () => inner.Current.Select(static r => r.Offset).ToArray(),
            () => _ = inner.Current.GetEnumerator());
        public ValueTask<bool> MoveNextAsync() => inner.MoveNextAsync();
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class RawEnumerator(IAsyncEnumerator<ConsumeRawBatch> inner)
        : IAsyncEnumerator<BatchView>
    {
        public BatchView Current => new(
            inner.Current.TopicPartition,
            () => inner.Current.Select(static r => r.Offset).ToArray(),
            () => _ = inner.Current.GetEnumerator());
        public ValueTask<bool> MoveNextAsync() => inner.MoveNextAsync();
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class BatchView(TopicPartition partition, Func<long[]> offsets, Action createEnumerator)
    {
        public TopicPartition Partition { get; } = partition;
        public long[] Offsets() => offsets();
        public void CreateEnumeratorOnly() => createEnumerator();
    }

    private static IAsyncEnumerator<BatchView> Open(
        KafkaConsumer<string, string> consumer,
        bool raw,
        CancellationToken cancellationToken = default) =>
        raw
            ? new RawEnumerator(consumer.ConsumeRawBatchAsync(cancellationToken).GetAsyncEnumerator(CancellationToken.None))
            : new TypedEnumerator(consumer.ConsumeBatchAsync(cancellationToken).GetAsyncEnumerator(CancellationToken.None));

    private sealed class PassThroughInterceptor : IConsumerInterceptor<string, string>
    {
        public int Calls;
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            Calls++;
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    private static KafkaConsumer<string, string> CreateConsumer(params PendingFetchData[] fetches) =>
        CreateConsumer(500, null, fetches);

    private static KafkaConsumer<string, string> CreateConsumer(int maxPollRecords, params PendingFetchData[] fetches) =>
        CreateConsumer(maxPollRecords, null, fetches);

    private static KafkaConsumer<string, string> CreateConsumer(
        int maxPollRecords,
        IConsumerInterceptor<string, string>[]? interceptors,
        params PendingFetchData[] fetches) =>
        CreateConsumer(maxPollRecords, interceptors, prefetch: false, unknownPosition: null, fetches);

    /// <summary>
    /// With <paramref name="prefetch"/>, the prefetch loop is marked started without running,
    /// so only the seeded fetches exist. <paramref name="unknownPosition"/> has no consumer
    /// position, which leaves a prefetching consumer unable to refetch it.
    /// </summary>
    private static KafkaConsumer<string, string> CreateConsumer(
        int maxPollRecords,
        IConsumerInterceptor<string, string>[]? interceptors,
        bool prefetch,
        TopicPartition? unknownPosition,
        params PendingFetchData[] fetches)
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            QueuedMinMessages = prefetch ? 100 : 1,
            FetchMaxWaitMs = 50,
            MaxPollRecords = maxPollRecords,
            Interceptors = interceptors
        };

        var consumer = new KafkaConsumer<string, string>(options, Serializers.String, Serializers.String);
        var partitions = fetches.Select(static f => f.TopicPartition).Distinct().ToArray();
        consumer.Assign(partitions);

        GetField("_initialized").SetValue(consumer, true);
        var fetchPositions = GetDictionary(consumer, "_fetchPositions");
        var positions = GetDictionary(consumer, "_positions");
        var pendingFetches = GetPendingFetches(consumer);
        foreach (var fetch in fetches)
        {
            var start = fetch.GetBatches()[0].BaseOffset;
            fetchPositions.TryAdd(fetch.TopicPartition, start);
            if (fetch.TopicPartition != unknownPosition)
                positions.TryAdd(fetch.TopicPartition, start);
            pendingFetches.Enqueue(fetch);
        }
        GetField("_pendingFetchDepth").SetValue(consumer, fetches.Length);
        if (prefetch)
            GetField("_prefetchTask").SetValue(consumer, Task.CompletedTask);
        return consumer;
    }

    private static PendingFetchData CreatePendingFetch(TopicPartition partition, long baseOffset, int recordCount)
    {
        var records = new Record[recordCount];
        for (var i = 0; i < records.Length; i++)
        {
            records[i] = new Record
            {
                OffsetDelta = i,
                Key = Encoding.UTF8.GetBytes($"key-{i}"),
                Value = Encoding.UTF8.GetBytes($"value-{i}"),
                IsKeyNull = false,
                IsValueNull = false
            };
        }

        return PendingFetchData.Create(partition.Topic, partition.Partition,
        [
            new RecordBatch
            {
                BaseOffset = baseOffset,
                BaseTimestamp = 1_700_000_000_000,
                LastOffsetDelta = recordCount - 1,
                Records = records
            }
        ]);
    }

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer) =>
        (Queue<PendingFetchData>)GetField("_pendingFetches").GetValue(consumer)!;

    private static ConcurrentDictionary<TopicPartition, long> GetDictionary(
        KafkaConsumer<string, string> consumer,
        string name) =>
        (ConcurrentDictionary<TopicPartition, long>)GetField(name).GetValue(consumer)!;

    private static FieldInfo GetField(string name) =>
        typeof(KafkaConsumer<string, string>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"{name} field not found");
}
