using System.Buffers.Binary;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.StressTests.Tests;

public class KeyedConsumerWorkloadTests
{
    [Test]
    public void InterleavedKeysAndPartitions_CompleteExactlyOnePass()
    {
        using var stop = new CancellationTokenSource();
        var tracker = new ThroughputTracker();
        var pass = new KeyedConsumerPass(2, 64, tracker, stop);
        for (var key = 31; key >= 0; key--)
            for (var offset = key; offset < 64; offset += 32)
                for (var partition = 1; partition >= 0; partition--)
                    pass.Complete(pass.Enter(partition, key, offset, Payload(offset)), 8);
        pass.ValidateComplete();
        if (!stop.IsCancellationRequested || tracker.MessageCount != 128)
            throw new InvalidOperationException("Full replay must stop ingress and count every completion.");
    }

    [Test]
    public async Task EqualKeysInDifferentPartitions_CanOverlap()
    {
        using var stop = new CancellationTokenSource();
        var pass = new KeyedConsumerPass(2, 32, new ThroughputTracker(), stop);
        var first = pass.Enter(0, 0, 0, Payload(0));
        var second = pass.Enter(1, 0, 0, Payload(0));
        pass.Complete(second, 8);
        pass.Complete(first, 8);
        await Assert.That(pass.Completed).IsEqualTo(2L);
    }

    [Test]
    public async Task EqualKeysInOnePartition_OverlapFails()
    {
        using var stop = new CancellationTokenSource();
        var pass = new KeyedConsumerPass(1, 64, new ThroughputTracker(), stop);
        pass.Enter(0, 0, 0, Payload(0));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            pass.Enter(0, 0, 32, Payload(32));
            return Task.CompletedTask;
        });
    }

    [Test]
    [Arguments(0, 0, 32L, 32L)] // missing earlier equal-key record
    [Arguments(0, 1, 0L, 0L)] // wrong logical key
    [Arguments(0, 0, 0L, 1L)] // corrupt payload
    [Arguments(1, 0, 0L, 0L)] // unknown partition
    [Arguments(0, 32, 0L, 0L)] // unknown key
    [Arguments(0, 0, 64L, 64L)] // beyond seeded end
    public async Task InvalidRecord_Fails(int partition, int key, long offset, long payload)
    {
        using var stop = new CancellationTokenSource();
        var pass = new KeyedConsumerPass(1, 64, new ThroughputTracker(), stop);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            pass.Enter(partition, key, offset, Payload(payload));
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task MissingFinalCompletion_FailsEvenWhenEveryOtherKeyFinished()
    {
        using var stop = new CancellationTokenSource();
        var pass = new KeyedConsumerPass(1, 32, new ThroughputTracker(), stop);
        for (var key = 0; key < 32; key++)
        {
            var index = pass.Enter(0, key, key, Payload(key));
            if (key != 31) pass.Complete(index, 8);
        }
        await Assert.That(stop.IsCancellationRequested).IsFalse();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            pass.ValidateComplete();
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task DuplicateAfterCompletion_Fails()
    {
        using var stop = new CancellationTokenSource();
        var pass = new KeyedConsumerPass(1, 32, new ThroughputTracker(), stop);
        pass.Complete(pass.Enter(0, 0, 0, Payload(0)), 8);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            pass.Enter(0, 0, 0, Payload(0));
            return Task.CompletedTask;
        });
    }

    [Test]
    [Arguments("binary")]
    [Arguments("large-distinct")]
    [Arguments("large-colliding")]
    public async Task BinaryKeys_AreIndependentBuffersWithStableContent(string shape)
    {
        var first = KeyedConsumerWorkload.CreateBinaryKey(shape, 7);
        var equal = KeyedConsumerWorkload.CreateBinaryKey(shape, 7);
        var unequal = KeyedConsumerWorkload.CreateBinaryKey(shape, 8);
        await Assert.That(ReferenceEquals(first, equal)).IsFalse();
        await Assert.That(first.AsSpan().SequenceEqual(equal)).IsTrue();
        await Assert.That(first.AsSpan().SequenceEqual(unequal)).IsFalse();
        await Assert.That(KeyedConsumerWorkload.ReadBinaryKey(shape, unequal)).IsEqualTo(8);
    }

    [Test]
    public async Task CollisionShape_PreservesEverySampledWindowButChangesFullContent()
    {
        var first = KeyedConsumerWorkload.CreateBinaryKey("large-colliding", 0);
        var other = KeyedConsumerWorkload.CreateBinaryKey("large-colliding", 31);
        foreach (var start in new[] { 0, first.Length / 3, first.Length * 2 / 3, first.Length - 16 })
            await Assert.That(first.AsSpan(start, 16).SequenceEqual(other.AsSpan(start, 16))).IsTrue();
        await Assert.That(first.AsSpan().SequenceEqual(other)).IsFalse();
    }

    [Test]
    [Arguments("bad", 2, 32, 128)]
    [Arguments("scalar", 0, 32, 128)]
    [Arguments("scalar", 7, 32, 128)]
    [Arguments("scalar", 2, 33, 128)]
    [Arguments("scalar", 2, 65568, 128)]
    [Arguments("scalar", 2, 32, 7)]
    [Arguments("scalar", 2, 32, 4097)]
    public async Task UnboundedOrInvalidDimensions_AreRejected(string shape, int partitions, int records, int bytes)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
        {
            KeyedConsumerWorkload.Validate(shape, partitions, records, bytes);
            return Task.CompletedTask;
        });
    }

    private static byte[] Payload(long offset)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, offset);
        return bytes;
    }
}
