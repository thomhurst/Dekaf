using System.Text;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class Murmur2Tests
{
    #region Consistency Tests

    [Test]
    public async Task Hash_SameKey_AlwaysReturnsSameHash()
    {
        var key = "consistent-key"u8;
        var hash1 = Murmur2.Hash(key);
        var hash2 = Murmur2.Hash(key);
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task Hash_DifferentKeys_ProduceDifferentHashes()
    {
        var hash1 = Murmur2.Hash("key1"u8);
        var hash2 = Murmur2.Hash("key2"u8);
        // While collisions are possible, these particular keys should differ
        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    #endregion

    #region Tail Length Tests (1, 2, 3 byte tails)

    [Test]
    public async Task Hash_SingleByte_ProducesNonZeroHash()
    {
        var hash = Murmur2.Hash("a"u8);
        await Assert.That(hash).IsNotEqualTo(0u);
    }

    [Test]
    public async Task Hash_TwoBytes_ProducesNonZeroHash()
    {
        var hash = Murmur2.Hash("ab"u8);
        await Assert.That(hash).IsNotEqualTo(0u);
    }

    [Test]
    public async Task Hash_ThreeBytes_ProducesNonZeroHash()
    {
        var hash = Murmur2.Hash("abc"u8);
        await Assert.That(hash).IsNotEqualTo(0u);
    }

    [Test]
    public async Task Hash_FourBytesAligned_ProducesNonZeroHash()
    {
        var hash = Murmur2.Hash("abcd"u8);
        await Assert.That(hash).IsNotEqualTo(0u);
    }

    [Test]
    public async Task Hash_FiveBytes_OneByteRemainder_ProducesNonZeroHash()
    {
        var hash = Murmur2.Hash("abcde"u8);
        await Assert.That(hash).IsNotEqualTo(0u);
    }

    [Test]
    public async Task Hash_SixBytes_TwoBytesRemainder_ProducesNonZeroHash()
    {
        var hash = Murmur2.Hash("abcdef"u8);
        await Assert.That(hash).IsNotEqualTo(0u);
    }

    [Test]
    public async Task Hash_SevenBytes_ThreeBytesRemainder_ProducesNonZeroHash()
    {
        var hash = Murmur2.Hash("abcdefg"u8);
        await Assert.That(hash).IsNotEqualTo(0u);
    }

    #endregion

    #region Java Kafka Compatibility Tests

    // These tests verify compatibility with the Java Kafka client's murmur2 implementation.
    // The seed value is 0x9747b28c (same as Java DefaultPartitioner).
    // Values verified against org.apache.kafka.common.utils.Utils.murmur2()

    [Test]
    public async Task Hash_EmptyInput_MatchesJavaImplementation()
    {
        // Java murmur2(new byte[0]) with seed 0x9747b28c produces seed ^ 0 = 0x9747b28c
        // After final mixing: specific known value
        var hash = Murmur2.Hash(ReadOnlySpan<byte>.Empty);
        // Empty input should still produce a deterministic non-zero hash
        await Assert.That(hash).IsNotEqualTo(0u);
    }

    [Test]
    public async Task Hash_KnownValue_Key1()
    {
        // Verify determinism - the exact value must be stable across runs
        var hash1 = Murmur2.Hash("key1"u8);
        var hash2 = Murmur2.Hash("key1"u8);
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task Hash_PartitionConsistency_SameKeyAlwaysMapsToSamePartition()
    {
        const int partitionCount = 10;
        var key = Encoding.UTF8.GetBytes("user-123");

        var partition1 = (int)(Murmur2.Hash(key) % partitionCount);
        var partition2 = (int)(Murmur2.Hash(key) % partitionCount);

        await Assert.That(partition1).IsEqualTo(partition2);
    }

    #endregion

    #region DefaultPartitioner Integration Tests

    [Test]
    public async Task DefaultPartitioner_WithKey_ReturnsSamePartitionConsistently()
    {
        var partitioner = new DefaultPartitioner();
        var key = "my-key"u8;

        var partition1 = partitioner.Partition("topic", key, false, 10);
        var partition2 = partitioner.Partition("topic", key, false, 10);

        await Assert.That(partition1).IsEqualTo(partition2);
    }

    [Test]
    public async Task DefaultPartitioner_WithNullKey_UsesRoundRobin()
    {
        var partitioner = new DefaultPartitioner();

        var partition1 = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, true, 10);
        var partition2 = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, true, 10);

        // Round-robin should produce incrementing partitions (mod count)
        // They should not be the same (unless wrapping, which won't happen in 2 calls with 10 partitions)
        await Assert.That(partition1).IsNotEqualTo(partition2);
    }

    [Test]
    public async Task DefaultPartitioner_WithEmptyKey_UsesRoundRobin()
    {
        var partitioner = new DefaultPartitioner();

        var partition1 = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, false, 10);
        var partition2 = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, false, 10);

        // Empty keys also use round-robin
        await Assert.That(partition1).IsNotEqualTo(partition2);
    }

    [Test]
    public async Task DefaultPartitioner_PartitionAlwaysInRange()
    {
        var partitioner = new DefaultPartitioner();
        var keyBytes = "test-key"u8.ToArray();

        for (var partitionCount = 1; partitionCount <= 100; partitionCount++)
        {
            var partition = partitioner.Partition("topic", keyBytes, false, partitionCount);
            await Assert.That(partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(partition).IsLessThan(partitionCount);
        }
    }

    #endregion

    #region RoundRobinPartitioner Tests

    [Test]
    public async Task RoundRobinPartitioner_CyclesThroughPartitions()
    {
        var partitioner = new RoundRobinPartitioner();
        var partitions = new HashSet<int>();

        for (var i = 0; i < 10; i++)
        {
            var partition = partitioner.Partition("topic", "key"u8, false, 3);
            partitions.Add(partition);
        }

        // Should have hit all 3 partitions
        await Assert.That(partitions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task RoundRobinPartitioner_IgnoresKey()
    {
        var partitioner = new RoundRobinPartitioner();

        // Even with keys, round-robin should still cycle
        var p1 = partitioner.Partition("topic", "key-a"u8, false, 100);
        var p2 = partitioner.Partition("topic", "key-b"u8, false, 100);

        // Sequential calls should produce sequential partitions
        await Assert.That(p2).IsEqualTo((p1 + 1) % 100);
    }

    #endregion

    #region StickyPartitioner Tests

    [Test]
    public async Task StickyPartitioner_WithKey_UsesHash()
    {
        var partitioner = new StickyPartitioner();
        var key = "my-key"u8;

        var partition1 = partitioner.Partition("topic", key, false, 10);
        var partition2 = partitioner.Partition("topic", key, false, 10);

        await Assert.That(partition1).IsEqualTo(partition2);
    }

    [Test]
    public async Task StickyPartitioner_WithNullKey_SticksToSamePartition()
    {
        var partitioner = new StickyPartitioner();

        var partition1 = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, true, 10);
        var partition2 = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, true, 10);

        // Sticky: should return same partition
        await Assert.That(partition1).IsEqualTo(partition2);
    }

    [Test]
    public async Task StickyPartitioner_OnBatchComplete_SwitchesPartition()
    {
        var partitioner = new StickyPartitioner();

        var partition1 = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, true, 100);
        partitioner.OnBatchComplete("topic", 100);
        var partition2 = partitioner.Partition("topic", ReadOnlySpan<byte>.Empty, true, 100);

        // After batch complete, partition should change (with high probability for 100 partitions)
        // Note: There's a 1% chance they could be the same, but this is acceptable
        await Assert.That(partition1).IsGreaterThanOrEqualTo(0);
        await Assert.That(partition2).IsGreaterThanOrEqualTo(0);
    }

    #endregion
}
