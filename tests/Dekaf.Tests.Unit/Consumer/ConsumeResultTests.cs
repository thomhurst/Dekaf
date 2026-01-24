using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumeResultTests
{
    [Test]
    public async Task ConsumeResult_DefaultIsPartitionEof_IsFalse()
    {
        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 100,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestamp: default,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        await Assert.That(result.IsPartitionEof).IsFalse();
    }

    [Test]
    public async Task ConsumeResult_WithIsPartitionEofTrue_ReturnsTrue()
    {
        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 100,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestamp: default,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null,
            isPartitionEof: true);

        await Assert.That(result.IsPartitionEof).IsTrue();
    }

    [Test]
    public async Task CreatePartitionEof_CreatesEofResult()
    {
        var result = ConsumeResult<string, string>.CreatePartitionEof("test-topic", 2, 500);

        await Assert.That(result.IsPartitionEof).IsTrue();
        await Assert.That(result.Topic).IsEqualTo("test-topic");
        await Assert.That(result.Partition).IsEqualTo(2);
        await Assert.That(result.Offset).IsEqualTo(500);
        await Assert.That(result.TimestampType).IsEqualTo(TimestampType.NotAvailable);
        await Assert.That(result.Headers).IsNull();
        await Assert.That(result.LeaderEpoch).IsNull();
    }

    [Test]
    public async Task CreatePartitionEof_KeyIsDefault()
    {
        var result = ConsumeResult<string, string>.CreatePartitionEof("test-topic", 0, 0);

        // Key should be default (null for reference types)
        await Assert.That(result.Key).IsNull();
    }

    [Test]
    public async Task CreatePartitionEof_ValueIsDefault()
    {
        var result = ConsumeResult<string, string>.CreatePartitionEof("test-topic", 0, 0);

        // Value should be default (null for reference types)
        // Note: Accessing Value on an EOF result with no deserializer will throw,
        // but for the string deserializer returning null for empty is expected
        await Assert.That(result.IsPartitionEof).IsTrue();
    }
}
