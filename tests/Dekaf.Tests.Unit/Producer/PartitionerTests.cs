using System.Reflection;
using System.Text;
using System.Threading;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

internal static class StickyPartitionerTestHelpers
{
    public static void SetCounter(object partitioner, uint value)
    {
        var trackerField = partitioner.GetType().GetField("_stickyPartitionTracker",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(partitioner.GetType().Name, "_stickyPartitionTracker");

        var tracker = trackerField.GetValue(partitioner)
            ?? throw new InvalidOperationException("_stickyPartitionTracker was null.");

        var counterField = tracker.GetType().GetField("_counter",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(tracker.GetType().Name, "_counter");

        if (counterField.FieldType == typeof(int))
        {
            counterField.SetValue(tracker, unchecked((int)value));
            return;
        }

        counterField.SetValue(tracker, value);
    }
}

public class DefaultPartitionerTests
{
    [Test]
    public async Task Partition_WithNullKey_ReturnsValidPartition()
    {
        var partitioner = new DefaultPartitioner();
        var partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);

        await Assert.That(partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition).IsLessThan(5);
    }

    [Test]
    public async Task Partition_WithNullKey_SticksToSamePartition()
    {
        var partitioner = new DefaultPartitioner();

        var partition1 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);
        var partition2 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);
        var partition3 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);

        await Assert.That(partition1).IsEqualTo(partition2);
        await Assert.That(partition2).IsEqualTo(partition3);
    }

    [Test]
    public async Task OnBatchComplete_ChangesPartition()
    {
        var partitioner = new DefaultPartitioner();
        const int partitionCount = 5;

        var partition1 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        partitioner.OnBatchComplete("test-topic", partitionCount);
        var partition2 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);

        await Assert.That(partition1).IsNotEqualTo(partition2);
    }

    [Test]
    public async Task Partition_WithKey_UsesHashPartitioning()
    {
        var partitioner = new DefaultPartitioner();
        var key = "test-key"u8;

        var partition1 = partitioner.Partition("test-topic", key, false, 5);
        var partition2 = partitioner.Partition("test-topic", key, false, 5);

        // Same key should always map to same partition
        await Assert.That(partition1).IsEqualTo(partition2);
        await Assert.That(partition1).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition1).IsLessThan(5);
    }

    [Test]
    public async Task OnRecordAppended_WhenBatchSizeReached_SwitchesPartition()
    {
        var partitioner = new DefaultPartitioner(stickyBatchSize: 10);
        var uniformStickyPartitioner = (IUniformStickyPartitioner)partitioner;
        const int partitionCount = 5;

        var partition1 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        uniformStickyPartitioner.OnRecordAppended("test-topic", partition1, bytes: 4, partitionCount);
        var stillSticky = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);

        uniformStickyPartitioner.OnRecordAppended("test-topic", partition1, bytes: 6, partitionCount);
        var partition2 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);

        await Assert.That(stillSticky).IsEqualTo(partition1);
        await Assert.That(partition2).IsNotEqualTo(partition1);
    }

    [Test]
    public async Task Partition_WhenIgnoreKeysTrue_UsesStickyPartitionForKeyedMessages()
    {
        var partitioner = new DefaultPartitioner(stickyBatchSize: 1024, ignoreKeys: true);

        var partition1 = partitioner.Partition("test-topic", "key-1"u8, false, 5);
        var partition2 = partitioner.Partition("test-topic", "key-2"u8, false, 5);

        await Assert.That(partition2).IsEqualTo(partition1);
    }

    [Test]
    public async Task AdaptivePartitioning_WithAvailabilityTimeout_SkipsBackedUpPartition()
    {
        var partitioner = new DefaultPartitioner(
            stickyBatchSize: 1,
            adaptivePartitioning: true,
            availabilityTimeoutMs: 1);
        var uniformStickyPartitioner = (IUniformStickyPartitioner)partitioner;
        const int partitionCount = 3;

        StickyPartitionerTestHelpers.SetCounter(partitioner, uint.MaxValue);
        var partition1 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        uniformStickyPartitioner.SetPartitionQueueByteProvider(
            (_, partition) => partition == partition1 ? 1024 : 0);

        Thread.Sleep(5);
        uniformStickyPartitioner.OnRecordAppended("test-topic", partition1, bytes: 1, partitionCount);
        var partition2 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);

        await Assert.That(partition2).IsNotEqualTo(partition1);
    }

    [Test]
    public async Task Partition_NearUIntMaxValue_NeverReturnsNegative()
    {
        var partitioner = new DefaultPartitioner();
        StickyPartitionerTestHelpers.SetCounter(partitioner, uint.MaxValue - 100);

        // Call partition and OnBatchComplete many times around the overflow point
        for (var i = 0; i < 200; i++)
        {
            var topicName = $"test-topic-{i}";
            var partition = partitioner.Partition(topicName, ReadOnlySpan<byte>.Empty, true, 7);
            await Assert.That(partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(partition).IsLessThan(7);

            partitioner.OnBatchComplete(topicName, 7);
        }
    }

    [Test]
    public async Task Partition_AtUIntMaxValue_HandlesOverflowCorrectly()
    {
        var partitioner = new DefaultPartitioner();
        const int partitionCount = 10;

        StickyPartitionerTestHelpers.SetCounter(partitioner, uint.MaxValue - 1);

        partitioner.OnBatchComplete("test-topic", partitionCount);
        var partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        await Assert.That(partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition).IsLessThan(partitionCount);

        partitioner.OnBatchComplete("test-topic", partitionCount);
        partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        await Assert.That(partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition).IsLessThan(partitionCount);
    }
}

public class StickyPartitionerTests
{
    [Test]
    public async Task Partition_WithNullKey_ReturnsValidPartition()
    {
        var partitioner = new StickyPartitioner();
        var partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);

        await Assert.That(partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition).IsLessThan(5);
    }

    [Test]
    public async Task Partition_WithNullKey_SticksToSamePartition()
    {
        var partitioner = new StickyPartitioner();

        var partition1 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);
        var partition2 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);
        var partition3 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);

        await Assert.That(partition1).IsEqualTo(partition2);
        await Assert.That(partition2).IsEqualTo(partition3);
    }

    [Test]
    public async Task OnBatchComplete_ChangesPartition()
    {
        var partitioner = new StickyPartitioner();
        const int partitionCount = 5;

        var partition1 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        partitioner.OnBatchComplete("test-topic", partitionCount);
        var partition2 = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);

        // Should have switched to a different partition
        await Assert.That(partition1).IsNotEqualTo(partition2);
    }

    [Test]
    public async Task Partition_DifferentTopics_UsesDifferentPartitions()
    {
        var partitioner = new StickyPartitioner();

        var partition1 = partitioner.Partition("topic-a", ReadOnlySpan<byte>.Empty, true, 5);
        var partition2 = partitioner.Partition("topic-b", ReadOnlySpan<byte>.Empty, true, 5);

        // Different topics should be tracked independently
        // (might be same partition by chance, but should be tracked separately)
        var sameAgain1 = partitioner.Partition("topic-a", ReadOnlySpan<byte>.Empty, true, 5);
        var sameAgain2 = partitioner.Partition("topic-b", ReadOnlySpan<byte>.Empty, true, 5);

        await Assert.That(partition1).IsEqualTo(sameAgain1);
        await Assert.That(partition2).IsEqualTo(sameAgain2);
    }

    [Test]
    public async Task Partition_WithKey_UsesHashPartitioning()
    {
        var partitioner = new StickyPartitioner();
        var key = "test-key"u8;

        var partition1 = partitioner.Partition("test-topic", key, false, 5);
        var partition2 = partitioner.Partition("test-topic", key, false, 5);

        // Same key should always map to same partition
        await Assert.That(partition1).IsEqualTo(partition2);
        await Assert.That(partition1).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition1).IsLessThan(5);
    }

    [Test]
    public async Task Partition_NearUIntMaxValue_NeverReturnsNegative()
    {
        var partitioner = new StickyPartitioner();
        StickyPartitionerTestHelpers.SetCounter(partitioner, uint.MaxValue - 100);

        // Call partition and OnBatchComplete many times around the overflow point
        for (var i = 0; i < 200; i++)
        {
            var topicName = $"test-topic-{i}";
            var partition = partitioner.Partition(topicName, ReadOnlySpan<byte>.Empty, true, 7);
            await Assert.That(partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(partition).IsLessThan(7);

            partitioner.OnBatchComplete(topicName, 7);
        }
    }

    [Test]
    public async Task OnBatchComplete_AtUIntMaxValue_HandlesOverflowCorrectly()
    {
        var partitioner = new StickyPartitioner();
        const int partitionCount = 10;

        StickyPartitionerTestHelpers.SetCounter(partitioner, uint.MaxValue - 1);

        // Trigger overflow with OnBatchComplete
        partitioner.OnBatchComplete("test-topic", partitionCount);
        var partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);

        await Assert.That(partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition).IsLessThan(partitionCount);

        // Trigger another overflow
        partitioner.OnBatchComplete("test-topic", partitionCount);
        partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);

        await Assert.That(partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition).IsLessThan(partitionCount);
    }
}

public class RoundRobinPartitionerTests
{
    [Test]
    public async Task Partition_ReturnsValidPartition()
    {
        var partitioner = new RoundRobinPartitioner();
        var partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 5);

        await Assert.That(partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition).IsLessThan(5);
    }

    [Test]
    public async Task Partition_RoundRobins()
    {
        var partitioner = new RoundRobinPartitioner();
        var partitions = new HashSet<int>();

        // Call enough times to cycle through all partitions
        for (var i = 0; i < 10; i++)
        {
            var partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 3);
            partitions.Add(partition);
        }

        await Assert.That(partitions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Partition_IgnoresKey()
    {
        var partitioner = new RoundRobinPartitioner();
        var key = "test-key"u8;

        var partitions = new HashSet<int>();

        // Even with a key, it should round-robin
        for (var i = 0; i < 10; i++)
        {
            var partition = partitioner.Partition("test-topic", key, false, 3);
            partitions.Add(partition);
        }

        // Should cycle through all partitions despite having the same key
        await Assert.That(partitions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Partition_NearUIntMaxValue_NeverReturnsNegative()
    {
        var partitioner = new RoundRobinPartitioner();

        // Set counter to near uint.MaxValue using reflection
        var counterField = typeof(RoundRobinPartitioner).GetField("_counter",
            BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(counterField).IsNotNull();
        counterField!.SetValue(partitioner, uint.MaxValue - 100);

        // Call partition many times around the overflow point
        for (var i = 0; i < 200; i++)
        {
            var partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, 7);
            await Assert.That(partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(partition).IsLessThan(7);
        }
    }

    [Test]
    public async Task Partition_AtUIntMaxValue_HandlesOverflowCorrectly()
    {
        var partitioner = new RoundRobinPartitioner();
        const int partitionCount = 10;

        // Set counter to uint.MaxValue - 1 so next increment wraps to 0
        var counterField = typeof(RoundRobinPartitioner).GetField("_counter",
            BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(counterField).IsNotNull();
        counterField!.SetValue(partitioner, uint.MaxValue - 1);

        // Get partition right before overflow
        var beforeOverflow = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        await Assert.That(beforeOverflow).IsGreaterThanOrEqualTo(0);
        await Assert.That(beforeOverflow).IsLessThan(partitionCount);

        // Get partition at overflow (counter wraps from uint.MaxValue to 0)
        var atOverflow = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        await Assert.That(atOverflow).IsGreaterThanOrEqualTo(0);
        await Assert.That(atOverflow).IsLessThan(partitionCount);

        // Get partition after overflow
        var afterOverflow = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
        await Assert.That(afterOverflow).IsGreaterThanOrEqualTo(0);
        await Assert.That(afterOverflow).IsLessThan(partitionCount);
    }

    [Test]
    public async Task Partition_ConcurrentCalls_AllReturnValidPartitions()
    {
        var partitioner = new RoundRobinPartitioner();
        const int threadCount = 10;
        const int callsPerThread = 1000;
        const int partitionCount = 7;

        var allValid = true;
        var tasks = new List<Task>();

        for (var t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < callsPerThread; i++)
                {
                    var partition = partitioner.Partition("test-topic", ReadOnlySpan<byte>.Empty, true, partitionCount);
                    if (partition < 0 || partition >= partitionCount)
                    {
                        allValid = false;
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        await Assert.That(allValid).IsTrue();
    }
}

public class LibrdkafkaPartitionerTests
{
    private static readonly LibrdkafkaHashVector[] HashVectors =
    [
        new("kafka", 10, Crc32: 0x5bbc7517, Crc32Partition: 9, Fnv1A: 0x0d33c4e1, Fnv1APartition: 5),
        new("user-123", 10, Crc32: 0xa2686a4e, Crc32Partition: 0, Fnv1A: 0x736c336d, Fnv1APartition: 3),
        new("order-123", 12, Crc32: 0x0a2544a5, Crc32Partition: 1, Fnv1A: 0x5af730e6, Fnv1APartition: 6),
        new("giberish123456789", 50, Crc32: 0x7b3a8e3f, Crc32Partition: 21, Fnv1A: 0x77a58295, Fnv1APartition: 23)
    ];

    [Test]
    public async Task ConsistentPartitioner_MatchesLibrdkafkaCrc32Vectors()
    {
        var partitioner = new ConsistentPartitioner();

        foreach (var vector in HashVectors)
        {
            var key = Encoding.UTF8.GetBytes(vector.Key);

            await Assert.That(LibrdkafkaCrc32.Hash(key)).IsEqualTo(vector.Crc32);
            await Assert.That(partitioner.Partition("topic", key, keyIsNull: false, vector.PartitionCount))
                .IsEqualTo(vector.Crc32Partition);
        }
    }

    [Test]
    public async Task ConsistentPartitioner_NullAndEmptyKeys_MapToSinglePartition()
    {
        var partitioner = new ConsistentPartitioner();

        await Assert.That(partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, keyIsNull: true, 17))
            .IsEqualTo(0);
        await Assert.That(partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, keyIsNull: false, 17))
            .IsEqualTo(0);
    }

    [Test]
    public async Task Fnv1APartitioner_MatchesLibrdkafkaVectors()
    {
        var partitioner = new Fnv1APartitioner();

        foreach (var vector in HashVectors)
        {
            var key = Encoding.UTF8.GetBytes(vector.Key);

            await Assert.That(Fnv1A.Hash(key)).IsEqualTo(vector.Fnv1A);
            await Assert.That(partitioner.Partition("topic", key, keyIsNull: false, vector.PartitionCount))
                .IsEqualTo(vector.Fnv1APartition);
        }
    }

    [Test]
    public async Task Fnv1APartitioner_NullAndEmptyKeys_MapToSinglePartition()
    {
        var partitioner = new Fnv1APartitioner();
        var expected = (int)(Fnv1A.Hash(ReadOnlySpan<byte>.Empty) % 17u);

        await Assert.That(Fnv1A.Hash(ReadOnlySpan<byte>.Empty)).IsEqualTo(0x7ee3623bu);
        await Assert.That(expected).IsEqualTo(0);
        await Assert.That(partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, keyIsNull: true, 17))
            .IsEqualTo(expected);
        await Assert.That(partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, keyIsNull: false, 17))
            .IsEqualTo(expected);
    }

    [Test]
    public async Task Murmur2RandomPartitioner_EmptyNonNullKey_UsesMurmur2()
    {
        var partitioner = new Murmur2RandomPartitioner();

        await Assert.That(partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, keyIsNull: false, 17))
            .IsEqualTo(Murmur2.Partition(ReadOnlySpan<byte>.Empty, 17));
    }

    [Test]
    public async Task Fnv1ARandomPartitioner_EmptyNonNullKey_UsesFnv1A()
    {
        var partitioner = new Fnv1ARandomPartitioner();
        var expected = (int)(Fnv1A.Hash(ReadOnlySpan<byte>.Empty) % 17u);

        await Assert.That(partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, keyIsNull: false, 17))
            .IsEqualTo(expected);
    }

    [Test]
    public async Task ConsistentRandomPartitioner_EmptyKey_UsesRandomPartition()
    {
        var partitioner = new ConsistentRandomPartitioner();

        for (var i = 0; i < 20; i++)
        {
            var partition = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, keyIsNull: false, 17);

            await Assert.That(partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(partition).IsLessThan(17);
        }
    }

    [Test]
    public async Task RandomPartitioner_IgnoresKeyAndReturnsValidPartitions()
    {
        var partitioner = new RandomPartitioner();

        for (var i = 0; i < 20; i++)
        {
            var partition = partitioner.Partition("topic", "same-key"u8, keyIsNull: false, 17);

            await Assert.That(partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(partition).IsLessThan(17);
        }
    }

    private sealed record LibrdkafkaHashVector(
        string Key,
        int PartitionCount,
        uint Crc32,
        int Crc32Partition,
        uint Fnv1A,
        int Fnv1APartition);
}
