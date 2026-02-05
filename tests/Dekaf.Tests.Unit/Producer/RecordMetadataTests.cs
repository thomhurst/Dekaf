using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class RecordMetadataTests
{
    [Test]
    public async Task AllProperties_SetCorrectly()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var metadata = new RecordMetadata
        {
            Topic = "my-topic",
            Partition = 3,
            Offset = 42,
            Timestamp = timestamp,
            KeySize = 10,
            ValueSize = 100
        };

        await Assert.That(metadata.Topic).IsEqualTo("my-topic");
        await Assert.That(metadata.Partition).IsEqualTo(3);
        await Assert.That(metadata.Offset).IsEqualTo(42);
        await Assert.That(metadata.Timestamp).IsEqualTo(timestamp);
        await Assert.That(metadata.KeySize).IsEqualTo(10);
        await Assert.That(metadata.ValueSize).IsEqualTo(100);
    }

    [Test]
    public async Task DefaultKeySizeAndValueSize_AreZero()
    {
        var metadata = new RecordMetadata
        {
            Topic = "topic",
            Partition = 0,
            Offset = 0,
            Timestamp = DateTimeOffset.UtcNow
        };

        await Assert.That(metadata.KeySize).IsEqualTo(0);
        await Assert.That(metadata.ValueSize).IsEqualTo(0);
    }

    [Test]
    public async Task Equality_SameValues_AreEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var m1 = new RecordMetadata { Topic = "topic", Partition = 1, Offset = 5, Timestamp = timestamp };
        var m2 = new RecordMetadata { Topic = "topic", Partition = 1, Offset = 5, Timestamp = timestamp };
        await Assert.That(m1).IsEqualTo(m2);
    }

    [Test]
    public async Task Equality_DifferentTopic_AreNotEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var m1 = new RecordMetadata { Topic = "topic1", Partition = 1, Offset = 5, Timestamp = timestamp };
        var m2 = new RecordMetadata { Topic = "topic2", Partition = 1, Offset = 5, Timestamp = timestamp };
        await Assert.That(m1).IsNotEqualTo(m2);
    }

    [Test]
    public async Task Equality_DifferentPartition_AreNotEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var m1 = new RecordMetadata { Topic = "topic", Partition = 1, Offset = 5, Timestamp = timestamp };
        var m2 = new RecordMetadata { Topic = "topic", Partition = 2, Offset = 5, Timestamp = timestamp };
        await Assert.That(m1).IsNotEqualTo(m2);
    }

    [Test]
    public async Task Equality_DifferentOffset_AreNotEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var m1 = new RecordMetadata { Topic = "topic", Partition = 1, Offset = 5, Timestamp = timestamp };
        var m2 = new RecordMetadata { Topic = "topic", Partition = 1, Offset = 10, Timestamp = timestamp };
        await Assert.That(m1).IsNotEqualTo(m2);
    }

    [Test]
    public async Task Equality_DifferentKeySize_AreNotEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var m1 = new RecordMetadata { Topic = "topic", Partition = 1, Offset = 5, Timestamp = timestamp, KeySize = 10 };
        var m2 = new RecordMetadata { Topic = "topic", Partition = 1, Offset = 5, Timestamp = timestamp, KeySize = 20 };
        await Assert.That(m1).IsNotEqualTo(m2);
    }

    [Test]
    public async Task IsReadonlyRecordStruct()
    {
        // RecordMetadata is a readonly record struct - verify it behaves as a value type
        var timestamp = DateTimeOffset.UtcNow;
        var m1 = new RecordMetadata { Topic = "topic", Partition = 1, Offset = 5, Timestamp = timestamp };
        var m2 = m1; // Value copy

        // They should be equal by value
        await Assert.That(m1).IsEqualTo(m2);
    }
}
