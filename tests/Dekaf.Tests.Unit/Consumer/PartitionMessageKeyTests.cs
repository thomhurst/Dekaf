using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionMessageKeyTests
{
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
    }

    private static bool KeysEqual<TKey>(TKey? first, TKey? second)
        => GetComparer<TKey>().Equals(PartitionMessageKey<TKey>.From(first), PartitionMessageKey<TKey>.From(second));

    private static IEqualityComparer<PartitionMessageKey<TKey>> GetComparer<TKey>()
        => PartitionMessageKeyComparer<TKey>.Default ?? EqualityComparer<PartitionMessageKey<TKey>>.Default;
}
