using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionMessageKeyTests
{
    [Test]
    public async Task DefaultBinaryComparers_KeepAdaptiveHashingLocalToOneDispatcher()
    {
        var first = (BinaryPartitionMessageKeyComparer<byte[]>)GetComparer<byte[]>();
        var second = (BinaryPartitionMessageKeyComparer<byte[]>)GetComparer<byte[]>();
        var bytes = new byte[65536];
        bytes[100] = 1;
        var key = PartitionMessageKey<byte[]>.From(bytes);
        var keys = new Dictionary<PartitionMessageKey<byte[]>, int>(second) { [key] = 42 };
        var originalHash = second.GetHashCode(key);

        first.EnableFullHashing();

        await Assert.That(second.GetHashCode(key)).IsEqualTo(originalHash);
        await Assert.That(keys[PartitionMessageKey<byte[]>.From(bytes.ToArray())]).IsEqualTo(42);
        await Assert.That(first.GetHashCode(key))
            .IsNotEqualTo(first.GetHashCode(PartitionMessageKey<byte[]>.From(new byte[65536])));
    }

    [Test]
    public async Task Dispatcher_CollidingLargeKeysPreserveOrderingAndCompletion()
    {
        const int keyCount = 64;
        const int recordCount = keyCount * 2;
        var lane = new PartitionLane<byte[], string>(new TopicPartition("topic", 0), recordCount,
            static (_, _) => default, static _ => { }, static (_, error) => throw error);
        var context = new PartitionProcessorContext<byte[], string>(lane);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = new int[keyCount];
        var last = Enumerable.Repeat(-1, keyCount).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dispatcher = new KeyOrderedPartitionDispatcher<byte[], string>(context, 1, 2, recordCount,
            async (records, cancellationToken) =>
            {
                var record = records[0];
                var key = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(record.Key!.AsSpan(65536 - 32));
                if (Interlocked.Increment(ref active[key]) != 1 || record.Offset / keyCount != last[key] + 1)
                    throw new InvalidOperationException("Equal keys overlapped or arrived out of order.");
                try
                {
                    if (record.Offset < 2)
                        await release.Task.WaitAsync(cancellationToken);
                    last[key]++;
                    context.MarkProcessed(record);
                }
                finally
                {
                    Interlocked.Decrement(ref active[key]);
                }
            });
        for (var offset = 0; offset < recordCount; offset++)
        {
            var key = new byte[65536];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(key.Length - 32), offset % keyCount);
            lane.TryEnqueue(Message(offset, key));
        }
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var running = dispatcher.RunAsync(timeout.Token).AsTask();
        try
        {
            await Assert.That(dispatcher.LaneCount).IsEqualTo(keyCount);
            release.TrySetResult();
            await running.WaitAsync(timeout.Token);
            await Assert.That(last.All(value => value == 1)).IsTrue();
            await Assert.That(lane.GetCommitOffset()!.Value.Offset).IsEqualTo(recordCount);
            await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
        }
        finally
        {
            release.TrySetResult();
            await timeout.CancelAsync();
            try { await running; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        }
    }

    [Test]
    public async Task LargeBinaryKeys_WithUnsampledDifferencesRemainDistinct()
    {
        var first = new byte[65536];
        var second = new byte[65536];
        second[100] = 1;
        var comparer = GetComparer<byte[]>();
        var left = PartitionMessageKey<byte[]>.From(first);
        var right = PartitionMessageKey<byte[]>.From(second);
        var keys = new Dictionary<PartitionMessageKey<byte[]>, int>(comparer)
        {
            [left] = 1,
            [right] = 2
        };
        await Assert.That(keys.Count).IsEqualTo(2);
        await Assert.That(keys[PartitionMessageKey<byte[]>.From(first.ToArray())]).IsEqualTo(1);
        await Assert.That(keys[PartitionMessageKey<byte[]>.From(second.ToArray())]).IsEqualTo(2);
        await Assert.That(keys.Remove(left)).IsTrue();
        await Assert.That(keys[right]).IsEqualTo(2);
    }

    [Test]
    public async Task CachedBinaryHash_CollisionsStillCompareEveryByte()
    {
        var first = new byte[65536];
        var other = new byte[65536];
        other[32768] = 1;
        var comparer = GetComparer<byte[]>();
        var left = PartitionMessageKey<byte[]>.From(first).WithBinaryHashCode(42);
        var equal = PartitionMessageKey<byte[]>.From(new byte[65536]).WithBinaryHashCode(42);
        var different = PartitionMessageKey<byte[]>.From(other).WithBinaryHashCode(42);
        var keys = new Dictionary<PartitionMessageKey<byte[]>, int>(comparer)
        {
            [left] = 1,
            [different] = 2
        };
        await Assert.That(keys[equal]).IsEqualTo(1);
        await Assert.That(keys[different]).IsEqualTo(2);
        await Assert.That(keys.Remove(equal)).IsTrue();
        await Assert.That(keys.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CachedBinaryHash_PreservesBothNullKinds()
    {
        var comparer = GetComparer<byte[]>();
        var wireNull = PartitionMessageKey<byte[]>.From(null, isKeyNull: true).WithBinaryHashCode(0);
        var deserializedNull = PartitionMessageKey<byte[]>.From(null).WithBinaryHashCode(0);
        await Assert.That(comparer.Equals(wireNull, deserializedNull)).IsFalse();
        await Assert.That(comparer.GetHashCode(wireNull)).IsEqualTo(0);
        await Assert.That(comparer.GetHashCode(deserializedNull)).IsEqualTo(0);
    }

    [Test]
    public async Task DeserializedNull_IsDistinctFromWireNullAndEmptyBinaryKey()
    {
        var comparer = GetComparer<byte[]>();
        var wireNull = PartitionMessageKey<byte[]>.From(null, isKeyNull: true);
        var deserializedNull = PartitionMessageKey<byte[]>.From(null, isKeyNull: false);
        var empty = PartitionMessageKey<byte[]>.From([]);
        var keys = new Dictionary<PartitionMessageKey<byte[]>, string>(comparer)
        {
            [wireNull] = "wire-null",
            [deserializedNull] = "deserialized-null",
            [empty] = "empty"
        };

        await Assert.That(keys.Count).IsEqualTo(3);
        await Assert.That(keys[wireNull]).IsEqualTo("wire-null");
        await Assert.That(keys[deserializedNull]).IsEqualTo("deserialized-null");
        await Assert.That(keys[empty]).IsEqualTo("empty");
        await Assert.That(comparer.Equals(wireNull, deserializedNull)).IsFalse();
        await Assert.That(comparer.Equals(deserializedNull, wireNull)).IsFalse();
        await Assert.That(comparer.Equals(deserializedNull, empty)).IsFalse();
        await Assert.That(comparer.Equals(empty, deserializedNull)).IsFalse();
    }

    [Test]
    public async Task DeserializedNull_NullableScalarRetainsSeparateIdentity()
    {
        var wireNull = PartitionMessageKey<int?>.From(null, isKeyNull: true);
        var deserializedNull = PartitionMessageKey<int?>.From(null, isKeyNull: false);
        var zero = PartitionMessageKey<int?>.From(0);
        var keys = new Dictionary<PartitionMessageKey<int?>, string>
        {
            [wireNull] = "wire-null",
            [deserializedNull] = "deserialized-null",
            [zero] = "zero"
        };

        await Assert.That(keys.Count).IsEqualTo(3);
        await Assert.That(keys[wireNull]).IsEqualTo("wire-null");
        await Assert.That(keys[deserializedNull]).IsEqualTo("deserialized-null");
        await Assert.That(keys[zero]).IsEqualTo("zero");
        await Assert.That(wireNull.Equals(PartitionMessageKey<int?>.From(42, isKeyNull: true))).IsTrue();
        await Assert.That(deserializedNull.Equals(PartitionMessageKey<int?>.From(null, isKeyNull: false))).IsTrue();
    }

    [Test]
    public async Task BinaryKeys_CompareContentAcrossDifferentStorage()
    {
        await AssertEqual<byte[]>([1, 2, 3], [1, 2, 3]);
        await AssertEqual<ReadOnlyMemory<byte>>(new byte[] { 0, 1, 2, 3, 0 }.AsMemory(1, 3), new byte[] { 1, 2, 3 });
        await AssertEqual<Memory<byte>>(new byte[] { 0, 1, 2, 3, 0 }.AsMemory(1, 3), new byte[] { 1, 2, 3 });
        await AssertEqual(new ArraySegment<byte>([0, 1, 2, 3, 0], 1, 3), new ArraySegment<byte>([1, 2, 3]));
    }

    [Test]
    public async Task BinaryKeys_EmptyValuesCompareEqualAndRemainDistinctFromNull()
    {
        await AssertEqual<byte[]>([], Array.Empty<byte>());
        await AssertEqual<ReadOnlyMemory<byte>>(default, new byte[1].AsMemory(1));
        await AssertEqual<Memory<byte>>(default, new byte[1].AsMemory(1));
        await AssertEqual(default(ArraySegment<byte>), new ArraySegment<byte>(new byte[1], 1, 0));
        await AssertEqual<byte[]?>(null, null);
        await Assert.That(KeysEqual<byte[]>(null, [])).IsFalse();
    }

    [Test]
    public async Task DifferentContentAndExistingScalarKeys_PreserveEquality()
    {
        await Assert.That(KeysEqual<byte[]>([1, 2, 3], [1, 2, 4])).IsFalse();
        await Assert.That(KeysEqual<byte[]>([1, 2], [1, 2, 0])).IsFalse();
        await AssertEqual("key", new string(['k', 'e', 'y']));
        await AssertEqual(42, 42);
        await Assert.That(KeysEqual("key", "KEY")).IsFalse();
        await Assert.That(KeysEqual(42, 43)).IsFalse();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Dispatcher_EqualBinaryKeysWaitWhileDifferentKeyRuns(int keyKind)
    {
        var lane = new PartitionLane<byte[], string>(new TopicPartition("topic", 0), 8,
            static (_, _) => ValueTask.CompletedTask, static _ => { }, static (_, error) => throw error);
        var context = new PartitionProcessorContext<byte[], string>(lane);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var equalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var differentStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dispatcher = new KeyOrderedPartitionDispatcher<byte[], string>(context, 1, 2, 8,
            async (records, cancellationToken) =>
            {
                switch (records[0].Offset)
                {
                    case 0:
                        firstStarted.TrySetResult();
                        await releaseFirst.Task.WaitAsync(cancellationToken);
                        break;
                    case 1:
                        equalStarted.TrySetResult();
                        break;
                    case 2:
                        differentStarted.TrySetResult();
                        break;
                }
                context.MarkProcessed(records[0]);
            });
        var running = dispatcher.RunAsync(timeout.Token).AsTask();
        try
        {
            lane.TryEnqueue(Message(0, keyKind == 1 ? null : keyKind == 2 ? [] : [1, 2, 3]));
            await firstStarted.Task.WaitAsync(timeout.Token);
            lane.TryEnqueue(Message(1, keyKind == 1 ? null : keyKind == 2 ? [] : [1, 2, 3]));
            lane.TryEnqueue(Message(2, keyKind == 1 ? [] : [4, 5, 6]));
            // FIFO input guarantees the equal-key record was dispatched before this barrier.
            await differentStarted.Task.WaitAsync(timeout.Token);
            await Assert.That(equalStarted.Task.IsCompleted).IsFalse();
            releaseFirst.TrySetResult();
            await equalStarted.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await timeout.CancelAsync();
            try { await running; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                await Assert.That(running.IsCanceled).IsTrue();
            }
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SingleHandler_PreservesOrderAcrossPendingAndSynchronousRecords(bool binary)
    {
        if (binary)
            await AssertSingleHandlerOrder(Serializers.ByteArray);
        else
            await AssertSingleHandlerOrder(Serializers.String);
    }

    private static async Task AssertSingleHandlerOrder<TKey>(IDeserializer<TKey> deserializer)
    {
        var lane = new PartitionLane<TKey, string>(new TopicPartition("topic", 0), 8,
            static (_, _) => default, static _ => { }, static (_, error) => throw error);
        var context = new PartitionProcessorContext<TKey, string>(lane);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var offsets = new List<long>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dispatcher = new KeyOrderedPartitionDispatcher<TKey, string>(context, 1, 1, 8,
            async (records, cancellationToken) =>
            {
                offsets.Add(records[0].Offset);
                if (records[0].Offset == 0)
                {
                    started.TrySetResult();
                    await release.Task.WaitAsync(cancellationToken);
                }
                context.MarkProcessed(records[0]);
            });
        for (var offset = 0; offset < 4; offset++)
        {
            byte[] key = offset == 2 ? [2] : [1];
            lane.TryEnqueue(new ConsumeResult<TKey, string>("topic", 0, offset,
                key, false, default, false, null, 0, TimestampType.CreateTime, null,
                deserializer, Serializers.String));
        }
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var running = dispatcher.RunAsync(timeout.Token).AsTask();
        try
        {
            await started.Task.WaitAsync(timeout.Token);
            await Assert.That(offsets.Count).IsEqualTo(1);
        }
        finally
        {
            release.TrySetResult();
            await running;
        }
        await Assert.That(offsets.SequenceEqual([0L, 1L, 2L, 3L])).IsTrue();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(4);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SingleHandler_StopsBeforeNextRecordAfterCancellationOrFailure(bool fail)
    {
        var lane = new PartitionLane<byte[], string>(new TopicPartition("topic", 0), 8,
            static (_, _) => default, static _ => { }, static (_, error) => throw error);
        var context = new PartitionProcessorContext<byte[], string>(lane);
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var expected = new InvalidOperationException("handler failure");
        var dispatcher = new KeyOrderedPartitionDispatcher<byte[], string>(context, 1, 1, 8,
            (_, _) =>
            {
                calls++;
                if (fail)
                    throw expected;
                cancellation.Cancel();
                return default;
            });
        lane.TryEnqueue(Message(0, [1]));
        lane.TryEnqueue(Message(1, [2]));
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        Exception? observed = null;
        try { await dispatcher.RunAsync(cancellation.Token); }
        catch (Exception error) { observed = error; }
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(fail ? ReferenceEquals(observed, expected) : observed is OperationCanceledException).IsTrue();
        await Assert.That(lane.GetCommitOffset()).IsNull();
    }

    private static ConsumeResult<byte[], string> Message(long offset, byte[]? key) => new(
        "topic", 0, offset, key, key is null, "value"u8.ToArray(), false, null, 0,
        TimestampType.CreateTime, null, Serializers.ByteArray, Serializers.String);

    private static async Task AssertEqual<TKey>(TKey first, TKey second)
    {
        var left = PartitionMessageKey<TKey>.From(first);
        var right = PartitionMessageKey<TKey>.From(second);
        var comparer = GetComparer<TKey>();
        await Assert.That(comparer.Equals(left, right)).IsTrue();
        await Assert.That(comparer.Equals(right, left)).IsTrue();
        await Assert.That(comparer.GetHashCode(left)).IsEqualTo(comparer.GetHashCode(right));
        var cached = left.WithBinaryHashCode(comparer.GetHashCode(left));
        await Assert.That(comparer.GetHashCode(cached)).IsEqualTo(comparer.GetHashCode(right));
        await Assert.That(comparer.Equals(cached, right)).IsTrue();
    }

    private static bool KeysEqual<TKey>(TKey? first, TKey? second)
        => GetComparer<TKey>().Equals(PartitionMessageKey<TKey>.From(first), PartitionMessageKey<TKey>.From(second));

    private static IEqualityComparer<PartitionMessageKey<TKey>> GetComparer<TKey>()
        => PartitionMessageKeyComparer<TKey>.Default ?? EqualityComparer<PartitionMessageKey<TKey>>.Default;
}
