using System.Text;
using Dekaf.Outbox;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Outbox;

public class OutboxBucketTests
{
    [Test]
    public async Task Compute_SameKey_AlwaysSameBucket()
    {
        var key = Encoding.UTF8.GetBytes("order-12345");

        var first = OutboxBucket.Compute(key, Guid.NewGuid(), 8);
        var second = OutboxBucket.Compute(key, Guid.NewGuid(), 8);

        await Assert.That(first).IsEqualTo(second);
    }

    [Test]
    public async Task Compute_KnownKey_IsStableAcrossReleases()
    {
        // Bucket assignment is persisted in the outbox table; the hash must never change
        // between releases or per-key ordering breaks mid-flight. Buckets use Kafka's
        // frozen Murmur2 key hash (the default partitioner's), pinned here.
        var key = Encoding.UTF8.GetBytes("stable-key");

        var bucket = OutboxBucket.Compute(key, Guid.Empty, 8);

        var expected = new Dekaf.Producer.Murmur2Partitioner()
            .Partition(string.Empty, key, keyIsNull: false, 8);
        await Assert.That(bucket).IsEqualTo(expected);
    }

    [Test]
    public async Task Compute_AllResults_WithinRange()
    {
        for (var i = 0; i < 1000; i++)
        {
            var key = Encoding.UTF8.GetBytes($"key-{i}");
            var bucket = OutboxBucket.Compute(key, Guid.NewGuid(), 7);
            await Assert.That(bucket).IsGreaterThanOrEqualTo(0);
            await Assert.That(bucket).IsLessThan(7);
        }
    }

    [Test]
    public async Task Compute_NullKey_UsesMessageId()
    {
        var messageId = Guid.NewGuid();

        var first = OutboxBucket.Compute(null, messageId, 8);
        var second = OutboxBucket.Compute(null, messageId, 8);

        await Assert.That(first).IsEqualTo(second);
    }

    [Test]
    public async Task Compute_EmptyKey_IsARealKeyWithAStableBucket()
    {
        // A non-null key that serialized to zero bytes is still a logical key: every such
        // row must share one bucket regardless of message id, or their order would break.
        var first = OutboxBucket.Compute([], Guid.NewGuid(), 8);
        var second = OutboxBucket.Compute([], Guid.NewGuid(), 8);

        await Assert.That(first).IsEqualTo(second);
    }

    [Test]
    public async Task Compute_ExplicitPartition_OverridesKeyAndMessageId()
    {
        // Rows pinned to one Kafka partition must share one bucket regardless of key or
        // message id - their partition order is the ordering the caller asked for.
        var withNullKey = OutboxBucket.Compute(null, Guid.NewGuid(), 8, partition: 5);
        var withKey = OutboxBucket.Compute("some-key"u8.ToArray(), Guid.NewGuid(), 8, partition: 5);
        var wrapped = OutboxBucket.Compute(null, Guid.NewGuid(), 8, partition: 13);

        await Assert.That(withNullKey).IsEqualTo(5);
        await Assert.That(withKey).IsEqualTo(5);
        await Assert.That(wrapped).IsEqualTo(5);
    }

    [Test]
    public async Task Create_ExplicitPartitionWithNullKey_BucketsByPartition()
    {
        var first = OutboxMessage.Create(
            "orders", (string?)null, "a", Serializers.String, Serializers.String,
            bucketCount: 4, partition: 2);
        var second = OutboxMessage.Create(
            "orders", (string?)null, "b", Serializers.String, Serializers.String,
            bucketCount: 4, partition: 2);

        await Assert.That(first.Bucket).IsEqualTo(2);
        await Assert.That(second.Bucket).IsEqualTo(2);
    }

    [Test]
    public async Task Create_SerializesKeyValueAndComputesBucket()
    {
        var message = OutboxMessage.Create(
            "orders", "order-1", "payload",
            Serializers.String, Serializers.String,
            headers: new Headers().Add("h", "v"),
            bucketCount: 4);

        await Assert.That(message.Topic).IsEqualTo("orders");
        await Assert.That(message.Key).IsEquivalentTo("order-1"u8.ToArray());
        await Assert.That(message.Value).IsEquivalentTo("payload"u8.ToArray());
        await Assert.That(message.Bucket).IsEqualTo(
            OutboxBucket.Compute("order-1"u8.ToArray(), message.MessageId, 4));
        await Assert.That(message.MessageId).IsNotEqualTo(Guid.Empty);
        await Assert.That(message.Headers).IsNotNull();
    }

    [Test]
    public async Task Create_NullValue_ProducesTombstone()
    {
        var message = OutboxMessage.Create(
            "orders", "order-1", (string?)null,
            Serializers.String, Serializers.String);

        await Assert.That(message.Value).IsNull();
        await Assert.That(message.Key).IsNotNull();
    }
}
