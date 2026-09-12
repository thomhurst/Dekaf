using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public class PartitionedDispatchHashTransitionTests
{
    [Test]
    [Arguments(16, 1024)]
    [Arguments(128, 65536)]
    public async Task CollisionThreshold_DoesNotReadUnrelatedActiveKeys(int unrelatedCount, int keySize)
    {
        var count = unrelatedCount + 9;
        var input = new PartitionLane<ReadOnlyMemory<byte>, int>(new TopicPartition("dispatch", 0), count,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        var memory = new CountingMemory[count];
        for (var offset = 0; offset < count; offset++)
        {
            var key = memory[offset] = new CountingMemory(keySize);
            BinaryPrimitives.WriteInt32LittleEndian(
                key.Bytes.AsSpan(offset < unrelatedCount ? keySize - sizeof(int) : keySize - 32),
                offset + 1);
            var record = new ConsumeResult<ReadOnlyMemory<byte>, int>("dispatch", 0, offset,
                key.Memory, false, key.Bytes.AsMemory(0, sizeof(int)), false, null, 0, TimestampType.CreateTime, null,
                Serializers.RawBytes, Serializers.Int32);
            await Assert.That(input.TryEnqueueForTest(record)).IsTrue();
        }
        await input.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        // The first colliding key reaches the dispatcher after unrelated lanes exist.
        // Observe the transition itself without counting their initial lookup work.
        memory[unrelatedCount].FirstRead = () =>
        {
            for (var index = 0; index < unrelatedCount; index++) memory[index].Reads = 0;
        };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<ReadOnlyMemory<byte>, int>(
            new PartitionProcessorContext<ReadOnlyMemory<byte>, int>(input), 1, 2, count,
            (records, _) =>
            {
                processed++;
                return records[0].Offset < 2 ? new ValueTask(release.Task) : default;
            }, automaticCompletion: true);
        var running = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            await Assert.That(dispatcher.LaneCount).IsEqualTo(count);
            for (var index = 0; index < unrelatedCount; index++)
                await Assert.That(memory[index].Reads).IsEqualTo(0);
        }
        finally
        {
            release.TrySetResult();
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
        await Assert.That(processed).IsEqualTo(count);
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(32)]
    [Arguments(64)]
    public async Task Promotion_StopsCompatibilityProbesWhileOnlyShortLanesRemain(int heldKeySize)
    {
        const int recordCount = 12;
        var input = new PartitionLane<ReadOnlyMemory<byte>, int>(new TopicPartition("dispatch", 0), recordCount,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        var probe = new CountingMemory(1024);
        probe.Bytes[0] = 0x80;
        for (var offset = 0; offset < recordCount; offset++)
        {
            ReadOnlyMemory<byte> key;
            if (offset == 0)
                key = new byte[Math.Max(0, heldKeySize)];
            else if (offset == 1)
                key = new byte[] { 1 };
            else if (offset == recordCount - 1)
                key = probe.Memory;
            else
            {
                var bytes = new byte[1024];
                bytes[^32] = (byte)offset;
                key = bytes;
            }
            var record = new ConsumeResult<ReadOnlyMemory<byte>, int>("dispatch", 0, offset,
                key, offset == 0 && heldKeySize < 0, new byte[sizeof(int)], false, null, 0,
                TimestampType.CreateTime, null, Serializers.RawBytes, Serializers.Int32);
            await Assert.That(input.TryEnqueueForTest(record)).IsTrue();
        }
        await input.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        probe.Reads = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = new KeyOrderedPartitionDispatcher<ReadOnlyMemory<byte>, int>(
            new PartitionProcessorContext<ReadOnlyMemory<byte>, int>(input), 1, 2, recordCount,
            (records, _) => records[0].Offset < 2 ? new ValueTask(release.Task) : default,
            automaticCompletion: true);
        var running = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            await Assert.That(dispatcher.LaneCount).IsEqualTo(recordCount);
            // All nine sampled lanes have migrated. The held short/null keys must
            // not make a new large key read its memory for a compatibility probe.
            await Assert.That(probe.Reads).IsEqualTo(1);
        }
        finally
        {
            release.TrySetResult();
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Test]
    [Arguments(0, 7)]
    [Arguments(0, 8)]
    [Arguments(0, 9)]
    [Arguments(1, 7)]
    [Arguments(1, 8)]
    [Arguments(1, 9)]
    [Arguments(2, 7)]
    [Arguments(2, 8)]
    [Arguments(2, 9)]
    [Arguments(3, 7)]
    [Arguments(3, 8)]
    [Arguments(3, 9)]
    public Task CollisionThreshold_RepeatedKeysKeepOneLane(int representation, int collisionCount) => representation switch
    {
        0 => AssertRepeatedKeys(Serializers.ByteArray, collisionCount),
        1 => AssertRepeatedKeys(Serializers.RawBytes, collisionCount),
        2 => AssertRepeatedKeys(new MutableMemoryDeserializer(), collisionCount),
        _ => AssertRepeatedKeys(new SegmentDeserializer(), collisionCount)
    };

    private static async Task AssertRepeatedKeys<TKey>(IDeserializer<TKey> deserializer, int collisionCount)
    {
        // Include two unrelated sampled lanes, colliding lanes, an empty key and
        // a wire-null key, then repeat all identities using separate backing arrays.
        var keyCount = collisionCount + 4;
        var recordCount = keyCount * 2;
        var input = new PartitionLane<TKey, int>(new TopicPartition("dispatch", 0), recordCount,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        for (var offset = 0; offset < recordCount; offset++)
        {
            var identity = offset % keyCount;
            var isNull = identity == keyCount - 1;
            var key = identity >= keyCount - 2 ? [] : new byte[1024];
            if (key.Length != 0)
                BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(identity < 2 ? 1020 : 992), identity + 1);
            var value = new byte[sizeof(int)];
            var record = new ConsumeResult<TKey, int>("dispatch", 0, offset,
                key, isNull, value, false, null, 0, TimestampType.CreateTime, null,
                deserializer, Serializers.Int32);
            await Assert.That(input.TryEnqueueForTest(record)).IsTrue();
        }
        await input.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = new int[keyCount];
        var completed = new int[keyCount];
        var dispatcher = new KeyOrderedPartitionDispatcher<TKey, int>(
            new PartitionProcessorContext<TKey, int>(input), 1, 2, recordCount,
            async (records, _) =>
            {
                var record = records[0];
                var identity = checked((int)(record.Offset % keyCount));
                if (Interlocked.Increment(ref active[identity]) != 1 || completed[identity] != record.Offset / keyCount)
                    throw new InvalidOperationException("Equal keys overlapped or arrived out of order.");
                try
                {
                    if (record.Offset < 2) await release.Task;
                    completed[identity]++;
                }
                finally
                {
                    Interlocked.Decrement(ref active[identity]);
                }
            }, automaticCompletion: true);
        var running = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            // Both workers are held, so every remaining input has been assigned
            // without any lane draining. Equal keys must not create extra lanes.
            await Assert.That(dispatcher.LaneCount).IsEqualTo(keyCount);
        }
        finally
        {
            release.TrySetResult();
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
        foreach (var count in completed) await Assert.That(count).IsEqualTo(2);
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
        await Assert.That(input.GetCommitOffset()?.Offset).IsEqualTo(recordCount);
    }

    private sealed class MutableMemoryDeserializer : IDeserializer<Memory<byte>>
    {
        public Memory<byte> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
            => MemoryMarshal.AsMemory(data);
    }

    private sealed class SegmentDeserializer : IDeserializer<ArraySegment<byte>>
    {
        public ArraySegment<byte> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
            => MemoryMarshal.TryGetArray(data, out var segment) ? segment : throw new InvalidOperationException();
    }

    private sealed class CountingMemory(int length) : MemoryManager<byte>
    {
        internal byte[] Bytes { get; } = new byte[length];
        internal int Reads { get; set; }
        internal Action? FirstRead { get; set; }

        public override Span<byte> GetSpan()
        {
            var callback = FirstRead;
            FirstRead = null;
            callback?.Invoke();
            Reads++;
            return Bytes;
        }

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }
}
