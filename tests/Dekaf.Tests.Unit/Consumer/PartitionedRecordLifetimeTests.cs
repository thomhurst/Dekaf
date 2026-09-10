using System.Runtime.CompilerServices;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionedRecordLifetimeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InterceptorBatch_RetainsStorageUntilRevocationGuard(bool disposeDuringDeserialization)
    {
        var memory = new ReusedMemory();
        var pending = CreatePending(memory);
        var disposedInsideCallback = -1;
        var calls = 0;
        KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer = null!;
        void Revoke()
        {
            consumer.Unassign();
            disposedInsideCallback = memory.DisposeCount;
        }
        consumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                Interceptors = [new CallbackRawInterceptor(() => { calls++; Revoke(); })]
            }, Serializers.RawBytes,
            disposeDuringDeserialization ? new CallbackRawDeserializer(Revoke) : Serializers.RawBytes);
        await using var ownedConsumer = consumer;
        var consumerType = consumer.GetType();
        System.Reflection.FieldInfo Field(string name) => consumerType.GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{name} field not found.");
        Field("_initialized").SetValue(consumer, true);
        consumer.Assign(pending.TopicPartition);
        Field("_lastManualAssignmentEnsureVersion").SetValue(consumer, Field("_assignmentEnsureVersion").GetValue(consumer));
        ((System.Collections.Concurrent.ConcurrentDictionary<TopicPartition, long>)Field("_fetchPositions").GetValue(consumer)!)
            [pending.TopicPartition] = 0;
        ((Queue<PendingFetchData>)Field("_pendingFetches").GetValue(consumer)!).Enqueue(pending);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var batches = consumer.ConsumeBatchAsync(timeout.Token).GetAsyncEnumerator())
        {
            await Assert.That(await batches.MoveNextAsync()).IsTrue();
            using var records = batches.Current.GetEnumerator();
            await Assert.That(records.MoveNext()).IsFalse();
            await Assert.That(calls).IsEqualTo(disposeDuringDeserialization ? 0 : 1);
            await Assert.That(disposedInsideCallback).IsEqualTo(0);
            await Assert.That(memory.DisposeCount).IsEqualTo(0);
        }
        await Assert.That(memory.DisposeCount).IsEqualTo(1);
    }

    private sealed class CallbackRawInterceptor(Action callback)
        : IConsumerInterceptor<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>
    {
        public ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> OnConsume(
            ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> result)
        {
            callback();
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    private sealed class CallbackRawDeserializer(Action callback) : IDeserializer<ReadOnlyMemory<byte>>
    {
        public ReadOnlyMemory<byte> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            callback();
            return data;
        }
    }

    [Test]
    public async Task FullOrCompletedQueue_DoesNotRetainRejectedRecords()
    {
        var acceptedMemory = new ReusedMemory();
        var rejectedMemory = new ReusedMemory();
        var lane = new PartitionLane<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new TopicPartition("lifetime", 0), 1, static (_, _) => default, static _ => { }, static (_, _) => { });
        try
        {
            ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> accepted;
            using (var acceptedPending = CreatePending(acceptedMemory))
            {
                using (var rejectedPending = CreatePending(rejectedMemory))
                {
                    var acceptedBatch = new ConsumeBatch<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
                        acceptedPending, Serializers.RawBytes, Serializers.RawBytes);
                    var rejectedBatch = new ConsumeBatch<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
                        rejectedPending, Serializers.RawBytes, Serializers.RawBytes);
                    var acceptedRecords = acceptedBatch.GetEnumerator();
                    var rejectedRecords = rejectedBatch.GetEnumerator();
                    acceptedRecords.MoveNext();
                    rejectedRecords.MoveNext();
                    accepted = acceptedRecords.Current;

                    await Assert.That(lane.TryEnqueue(accepted)).IsTrue();
                    for (var attempt = 0; attempt < 256; attempt++)
                        await Assert.That(lane.TryEnqueue(rejectedRecords.Current)).IsFalse();
                }
                await Assert.That(rejectedMemory.DisposeCount).IsEqualTo(1);
            }
            await Assert.That(acceptedMemory.DisposeCount).IsEqualTo(0);
            await lane.StopAsync(PartitionStopPolicy.Cancel, TimeSpan.FromSeconds(1));
            await Assert.That(lane.TryEnqueue(accepted)).IsFalse();
            var dequeued = lane.TryReadMessage(out var message);
            if (dequeued)
                message.ReleaseStorage();
            await Assert.That(dequeued).IsTrue();
            await Assert.That(acceptedMemory.DisposeCount).IsEqualTo(1);
        }
        finally
        {
            while (lane.TryReadMessage(out var remaining))
                remaining.ReleaseStorage();
        }
    }

    [Test]
    [Arguments(PartitionedProcessingOrder.Partition, PartitionBackpressureMode.AwaitCapacity, false)]
    [Arguments(PartitionedProcessingOrder.Partition, PartitionBackpressureMode.PauseResume, false)]
    [Arguments(PartitionedProcessingOrder.Key, PartitionBackpressureMode.AwaitCapacity, false)]
    [Arguments(PartitionedProcessingOrder.Key, PartitionBackpressureMode.PauseResume, false)]
    [Arguments(PartitionedProcessingOrder.Partition, PartitionBackpressureMode.AwaitCapacity, true)]
    [Arguments(PartitionedProcessingOrder.Partition, PartitionBackpressureMode.PauseResume, true)]
    [Arguments(PartitionedProcessingOrder.Key, PartitionBackpressureMode.AwaitCapacity, true)]
    [Arguments(PartitionedProcessingOrder.Key, PartitionBackpressureMode.PauseResume, true)]
    public async Task RawRecords_RemainValidAfterFetchAdvances(
        PartitionedProcessingOrder ordering, PartitionBackpressureMode backpressure, bool replaceResult)
    {
        using var timeout = new CancellationTokenSource();
        var handlerStarted = NewSignal();
        var fetchAdvanced = NewSignal();
        var releaseHandler = NewSignal();
        var memory = new ReusedMemory();
        var pending = CreatePending(memory);
        await using var interceptorConsumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                Interceptors = replaceResult ? [new BorrowingReplacementInterceptor()] : null
            }, Serializers.RawBytes, Serializers.RawBytes);
        var onConsume = (Func<ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>,
            ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>?)typeof(KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>)
            .GetField("_onBatchConsume", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(interceptorConsumer);
        var consumer = Substitute.For<IKafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>();
        consumer.Partitions.Assignment.Returns(new HashSet<TopicPartition> { new("lifetime", 0) });
        consumer.ConsumeBatchAsync(Arg.Any<CancellationToken>()).Returns(call =>
            Fetch(pending, handlerStarted, fetchAdvanced, call.Arg<CancellationToken>(), onConsume: onConsume));

        // Start the operation budget after pooled storage and proxy setup.
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var run = consumer.RunPartitionedAsync(async (_, message, token) =>
        {
            handlerStarted.TrySetResult();
            await releaseHandler.Task.WaitAsync(token);
            await Assert.That(Encoding.UTF8.GetString(message.Key.Span)).IsEqualTo("key");
            await Assert.That(Encoding.UTF8.GetString(message.Value.Span)).IsEqualTo("original");
            await Assert.That(Encoding.UTF8.GetString(message.Headers[0].Value.Span)).IsEqualTo("header");
        }, new PartitionedProcessingOptions
        {
            Ordering = ordering,
            BackpressureMode = backpressure,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            MaxBufferedRecordsPerPartition = 4
        }, timeout.Token).AsTask();

        try
        {
            await fetchAdvanced.Task.WaitAsync(timeout.Token);
            await Assert.That(memory.DisposeCount).IsEqualTo(0);
        }
        finally
        {
            releaseHandler.TrySetResult();
            await run;
        }

        await Assert.That(memory.DisposeCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(PartitionedProcessingOrder.Partition, false)]
    [Arguments(PartitionedProcessingOrder.Partition, true)]
    [Arguments(PartitionedProcessingOrder.Key, false)]
    [Arguments(PartitionedProcessingOrder.Key, true)]
    public async Task StringBatches_HeadersRemainValidAndStorageIsReleased(
        PartitionedProcessingOrder ordering, bool failHandler)
    {
        using var timeout = new CancellationTokenSource();
        var handlerStarted = NewSignal();
        var fetchAdvanced = NewSignal();
        var releaseHandler = NewSignal();
        var memory = new ReusedMemory();
        var pending = CreatePending(memory);
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.Partitions.Assignment.Returns(new HashSet<TopicPartition> { new("lifetime", 0) });
        consumer.ConsumeBatchAsync(Arg.Any<CancellationToken>()).Returns(call =>
            FetchStrings(pending, handlerStarted, fetchAdvanced, call.Arg<CancellationToken>()));
        var failure = new InvalidOperationException("handler failure");

        // Start the operation budget after pooled storage and proxy setup.
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var run = consumer.RunPartitionedBatchesAsync(async (_, messages, token) =>
        {
            handlerStarted.TrySetResult();
            await releaseHandler.Task.WaitAsync(token);
            await Assert.That(messages[0].Key).IsEqualTo("key");
            await Assert.That(messages[0].Value).IsEqualTo("original");
            await Assert.That(Encoding.UTF8.GetString(messages[0].Headers[0].Value.Span)).IsEqualTo("header");
            if (failHandler)
                throw failure;
        }, new PartitionedProcessingOptions
        {
            Ordering = ordering,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            MaxBufferedRecordsPerPartition = 4
        }, timeout.Token).AsTask();

        try
        {
            await fetchAdvanced.Task.WaitAsync(timeout.Token);
            await Assert.That(memory.DisposeCount).IsEqualTo(0);
        }
        finally
        {
            releaseHandler.TrySetResult();
            if (failHandler)
                await Assert.That(async () => await run).Throws<InvalidOperationException>();
            else
                await run;
        }
        await Assert.That(memory.DisposeCount).IsEqualTo(1);
    }

    private static async IAsyncEnumerable<ConsumeBatch<string, string>> FetchStrings(
        PendingFetchData pending, TaskCompletionSource handlerStarted, TaskCompletionSource fetchAdvanced,
        [EnumeratorCancellation] CancellationToken cancellationToken, bool keepOpen = false)
    {
        using (pending)
        {
            yield return new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);
            await handlerStarted.Task.WaitAsync(cancellationToken);
        }
        fetchAdvanced.TrySetResult();
        if (keepOpen)
            await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    [Test]
    [Arguments(PartitionedProcessingOrder.Partition, false)]
    [Arguments(PartitionedProcessingOrder.Partition, true)]
    [Arguments(PartitionedProcessingOrder.Key, false)]
    [Arguments(PartitionedProcessingOrder.Key, true)]
    public async Task CancelledLane_ReleasesQueuedStorageAfterActiveHandlerExits(
        PartitionedProcessingOrder ordering, bool ignoreCancellation)
    {
        using var timeout = new CancellationTokenSource();
        var handlerStarted = NewSignal();
        var fetchAdvanced = NewSignal();
        var cancellationObserved = NewSignal();
        var releaseHandler = NewSignal();
        var memory = new ReusedMemory();
        var pending = CreatePending(memory, recordCount: 4);
        var consumer = Substitute.For<IKafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>();
        consumer.Partitions.Assignment.Returns(new HashSet<TopicPartition> { new("lifetime", 0) });
        consumer.ConsumeBatchAsync(Arg.Any<CancellationToken>()).Returns(call =>
            Fetch(pending, handlerStarted, fetchAdvanced, call.Arg<CancellationToken>()));

        // Start the operation budget after pooled storage and proxy setup.
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var run = consumer.RunPartitionedAsync(async (_, message, token) =>
        {
            using var registration = token.Register(() => cancellationObserved.TrySetResult());
            handlerStarted.TrySetResult();
            if (ignoreCancellation)
                await releaseHandler.Task;
            else
                await releaseHandler.Task.WaitAsync(token);
            await Assert.That(Encoding.UTF8.GetString(message.Value.Span)).IsEqualTo("original");
        }, new PartitionedProcessingOptions
        {
            Ordering = ordering,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            BackpressureMode = PartitionBackpressureMode.AwaitCapacity,
            StopPolicy = PartitionStopPolicy.Cancel,
            StopTimeout = ignoreCancellation ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(5),
            MaxBufferedRecordsPerPartition = 4
        }, timeout.Token).AsTask();

        try
        {
            await fetchAdvanced.Task.WaitAsync(timeout.Token);
            if (ignoreCancellation)
            {
                await cancellationObserved.Task.WaitAsync(timeout.Token);
                await Assert.That(async () => await run).Throws<TimeoutException>();
                await Assert.That(memory.DisposeCount).IsEqualTo(0);
            }
            else
            {
                await run;
            }
        }
        finally
        {
            releaseHandler.TrySetResult();
        }

        await memory.Disposed.Task.WaitAsync(timeout.Token);
        await Assert.That(memory.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task PartitionStream_RetainsCurrentRecordUntilEnumeratorAdvances()
    {
        using var timeout = new CancellationTokenSource();
        var handlerStarted = NewSignal();
        var fetchAdvanced = NewSignal();
        var releaseHandler = NewSignal();
        var memory = new ReusedMemory();
        var pending = CreatePending(memory, recordCount: 3);
        var consumer = Substitute.For<IKafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>();
        consumer.Partitions.Assignment.Returns(new HashSet<TopicPartition> { new("lifetime", 0) });
        consumer.ConsumeBatchAsync(Arg.Any<CancellationToken>()).Returns(call =>
            Fetch(pending, handlerStarted, fetchAdvanced, call.Arg<CancellationToken>()));

        // Start the operation budget after pooled storage and proxy setup.
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var run = consumer.RunPartitionedAsync(async (context, token) =>
        {
            await foreach (var message in context.Messages.WithCancellation(token))
            {
                handlerStarted.TrySetResult();
                await releaseHandler.Task.WaitAsync(token);
                await Assert.That(Encoding.UTF8.GetString(message.Value.Span)).IsEqualTo("original");
                await Assert.That(Encoding.UTF8.GetString(message.Headers[0].Value.Span)).IsEqualTo("header");
                context.MarkProcessed(message);
            }
        }, new PartitionedProcessingOptions
        {
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            MaxBufferedRecordsPerPartition = 4
        }, timeout.Token).AsTask();

        try
        {
            await fetchAdvanced.Task.WaitAsync(timeout.Token);
            await Assert.That(memory.DisposeCount).IsEqualTo(0);
        }
        finally
        {
            releaseHandler.TrySetResult();
            await run;
        }
        await Assert.That(memory.DisposeCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Rebalance_ReleasesCancelledActiveAndQueuedStorage(bool lost)
    {
        using var timeout = new CancellationTokenSource();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var started = NewSignal();
        var advanced = NewSignal();
        var memory = new ReusedMemory();
        var pending = CreatePending(memory, recordCount: 3);
        var partition = new TopicPartition("lifetime", 0);
        var consumer = new RebalanceConsumer(token => FetchStrings(pending, started, advanced, token, keepOpen: true));
        consumer.SetAssignment(partition);
        // Start the operation budget after pooled storage and proxy setup.
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var run = consumer.RunPartitionedAsync(async (_, _, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        }, new PartitionedProcessingOptions
        {
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            BackpressureMode = PartitionBackpressureMode.AwaitCapacity,
            StopPolicy = PartitionStopPolicy.Cancel,
            MaxBufferedRecordsPerPartition = 4
        }, stop.Token).AsTask();
        try
        {
            await advanced.Task.WaitAsync(timeout.Token);
            await Assert.That(memory.DisposeCount).IsEqualTo(0);
            if (lost)
                consumer.LoseFromCoordinator(partition);
            else
                consumer.RevokeFromCoordinator(partition);
            await memory.Disposed.Task.WaitAsync(timeout.Token);
            await Assert.That(memory.DisposeCount).IsEqualTo(1);
        }
        finally
        {
            await stop.CancelAsync();
            try
            {
                await run;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                await Assert.That(run.IsCanceled).IsTrue();
            }
        }
    }

    private static async IAsyncEnumerable<ConsumeBatch<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>> Fetch(
        PendingFetchData pending, TaskCompletionSource handlerStarted, TaskCompletionSource fetchAdvanced,
        [EnumeratorCancellation] CancellationToken cancellationToken, bool keepOpen = false,
        Func<ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>, ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>? onConsume = null)
    {
        using (pending)
        {
            yield return new ConsumeBatch<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
                pending, Serializers.RawBytes, Serializers.RawBytes, onConsume: onConsume);
            await handlerStarted.Task.WaitAsync(cancellationToken);
        }
        fetchAdvanced.TrySetResult();
        if (keepOpen)
            await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    [Test]
    public async Task BorrowedDictionaryKey_OutlivesFirstRecordWhileSameKeyIsActive()
    {
        using var timeout = new CancellationTokenSource();
        var firstStarted = NewSignal();
        var firstRelease = NewSignal();
        var secondStarted = NewSignal();
        var secondRelease = NewSignal();
        var sameKeyFound = NewSignal();
        var advanced = NewSignal();
        var firstMemory = new ReusedMemory();
        var secondMemory = new ReusedMemory();
        var first = CreatePending(firstMemory);
        var second = CreatePending(secondMemory, baseOffset: 1);
        var consumer = Substitute.For<IKafkaConsumer<BorrowedKey, ReadOnlyMemory<byte>>>();
        consumer.Partitions.Assignment.Returns(new HashSet<TopicPartition> { new("lifetime", 0) });
        consumer.ConsumeBatchAsync(Arg.Any<CancellationToken>()).Returns(FetchBoth());

        async IAsyncEnumerable<ConsumeBatch<BorrowedKey, ReadOnlyMemory<byte>>> FetchBoth()
        {
            using (first)
            {
                yield return new ConsumeBatch<BorrowedKey, ReadOnlyMemory<byte>>(first, new BorrowedKeyDeserializer(sameKeyFound), Serializers.RawBytes);
                await firstStarted.Task.WaitAsync(timeout.Token);
            }
            using (second)
                yield return new ConsumeBatch<BorrowedKey, ReadOnlyMemory<byte>>(second, new BorrowedKeyDeserializer(sameKeyFound), Serializers.RawBytes);
            advanced.TrySetResult();
        }

        // Start the operation budget after pooled storage and proxy setup.
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var run = consumer.RunPartitionedAsync(async (_, message, token) =>
        {
            var started = message.Offset == 0 ? firstStarted : secondStarted;
            var release = message.Offset == 0 ? firstRelease : secondRelease;
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            await Assert.That(Encoding.UTF8.GetString(message.Key.Bytes.Span)).IsEqualTo("key");
        }, new PartitionedProcessingOptions
        {
            Ordering = PartitionedProcessingOrder.Key,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            MaxBufferedRecordsPerPartition = 4
        }, timeout.Token).AsTask();

        try
        {
            await advanced.Task.WaitAsync(timeout.Token);
            // Fetch advancement only proves routing into the partition queue. The
            // key lookup runs under the dispatcher's gate, so idle removal cannot
            // release the first key before the second record is enqueued there.
            await sameKeyFound.Task.WaitAsync(timeout.Token);
            firstRelease.TrySetResult();
            await secondStarted.Task.WaitAsync(timeout.Token);
            await Assert.That(firstMemory.DisposeCount).IsEqualTo(0);
            await Assert.That(secondMemory.DisposeCount).IsEqualTo(0);
        }
        finally
        {
            firstRelease.TrySetResult();
            secondRelease.TrySetResult();
            await run;
        }
        await Assert.That(firstMemory.DisposeCount).IsEqualTo(1);
        await Assert.That(secondMemory.DisposeCount).IsEqualTo(1);
    }

    private static PendingFetchData CreatePending(ReusedMemory memory, int recordCount = 1, long baseOffset = 0)
    {
        var records = new Record[recordCount];
        for (var index = 0; index < recordCount; index++)
        {
            records[index] = new Record
            {
                OffsetDelta = index,
                Key = memory.Memory[..3],
                Value = memory.Memory.Slice(3, 8),
                Headers = [new Header("test", memory.Memory[11..])],
                HeaderCount = 1
            };
        }
        var batch = new RecordBatch
        {
            BaseOffset = baseOffset,
            LastOffsetDelta = recordCount - 1,
            Records = records
        };
        var pending = PendingFetchData.Create("lifetime", 0, [batch]);
        pending.SetMemoryOwner(memory);
        pending.EagerParseAll();
        return pending;
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class BorrowingReplacementInterceptor : IConsumerInterceptor<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>
    {
        public ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> OnConsume(
            ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> result)
            => new(result.Topic, result.Partition, result.Offset, result.Key, result.Value, result.Headers,
                result.Timestamp.ToUnixTimeMilliseconds(), result.TimestampType, result.LeaderEpoch);

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    // Public so NSubstitute can construct the generic consumer proxy for this test key.
    public readonly struct BorrowedKey(ReadOnlyMemory<byte> bytes, TaskCompletionSource sameKeyFound) : IEquatable<BorrowedKey>
    {
        public ReadOnlyMemory<byte> Bytes => bytes;
        public bool Equals(BorrowedKey other)
        {
            var equal = Bytes.Span.SequenceEqual(other.Bytes.Span);
            if (equal)
                sameKeyFound.TrySetResult();
            return equal;
        }
        public override bool Equals(object? obj) => obj is BorrowedKey other && Equals(other);
        public override int GetHashCode() => Bytes.IsEmpty ? 0 : Bytes.Span[0];
        public static bool operator ==(BorrowedKey left, BorrowedKey right) => left.Equals(right);
        public static bool operator !=(BorrowedKey left, BorrowedKey right) => !left.Equals(right);
    }

    private sealed class BorrowedKeyDeserializer(TaskCompletionSource sameKeyFound) : IDeserializer<BorrowedKey>
    {
        public BorrowedKey Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => new(data, sameKeyFound);
    }

    private sealed class RebalanceConsumer(Func<CancellationToken, IAsyncEnumerable<ConsumeBatch<string, string>>> fetch)
        : PartitionedConsumerRuntimeTests.TestConsumer
    {
        public override IAsyncEnumerable<ConsumeBatch<string, string>> ConsumeBatchAsync(CancellationToken cancellationToken = default)
            => fetch(cancellationToken);
    }

    private sealed class ReusedMemory : IPooledMemory
    {
        private readonly byte[] _bytes = "keyoriginalheader"u8.ToArray();
        public ReadOnlyMemory<byte> Memory => _bytes;
        public int DisposeCount;
        public TaskCompletionSource Disposed { get; } = NewSignal();
        public void Dispose()
        {
            Interlocked.Increment(ref DisposeCount);
            _bytes.AsSpan().Fill((byte)'X');
            Disposed.TrySetResult();
        }
    }
}
