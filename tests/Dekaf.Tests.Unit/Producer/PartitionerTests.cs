using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

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
    public async Task Partition_WithNullKey_RoundRobins()
    {
        var partitioner = new DefaultPartitioner();
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
    public async Task Partition_NearUIntMaxValue_NeverReturnsNegative()
    {
        var partitioner = new DefaultPartitioner();

        // Set counter to near uint.MaxValue using reflection
        var counterField = typeof(DefaultPartitioner).GetField("_counter",
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
        var partitioner = new DefaultPartitioner();
        const int partitionCount = 10;

        // Set counter to uint.MaxValue - 1 so next increment wraps to 0
        var counterField = typeof(DefaultPartitioner).GetField("_counter",
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

        // Set counter to near uint.MaxValue using reflection
        var counterField = typeof(StickyPartitioner).GetField("_counter",
            BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(counterField).IsNotNull();
        counterField!.SetValue(partitioner, uint.MaxValue - 100);

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

        // Set counter to uint.MaxValue - 1
        var counterField = typeof(StickyPartitioner).GetField("_counter",
            BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(counterField).IsNotNull();
        counterField!.SetValue(partitioner, uint.MaxValue - 1);

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
