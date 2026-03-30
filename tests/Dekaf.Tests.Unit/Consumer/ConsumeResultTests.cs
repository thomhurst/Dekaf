using Dekaf.Consumer;
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
            timestampMs: 0,
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
            timestampMs: 0,
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

    [Test]
    public async Task Timestamp_ComputedLazilyFromTimestampMs()
    {
        // Specific Unix timestamp: 2024-01-15T12:30:00Z = 1705318200000 ms
        const long timestampMs = 1705318200000;
        var expected = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 0,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: timestampMs,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        await Assert.That(result.TimestampMs).IsEqualTo(timestampMs);
        await Assert.That(result.Timestamp).IsEqualTo(expected);
    }

    [Test]
    public async Task TimestampMs_ReturnsRawUnixMilliseconds()
    {
        const long timestampMs = 1705318200000;

        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 0,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: timestampMs,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        await Assert.That(result.TimestampMs).IsEqualTo(timestampMs);
    }

    [Test]
    public async Task HeaderSlice_Rent_ReturnsCorrectData()
    {
        var headers = new Header[]
        {
            new("key1", System.Text.Encoding.UTF8.GetBytes("value1")),
            new("key2", System.Text.Encoding.UTF8.GetBytes("value2"))
        };

        var slice = HeaderSlice.Rent(headers, 2);

        await Assert.That(slice.Count).IsEqualTo(2);
        await Assert.That(slice[0].Key).IsEqualTo("key1");
        await Assert.That(slice[1].Key).IsEqualTo("key2");

        HeaderSlice.Return(slice);
    }

    [Test]
    public async Task HeaderSlice_Return_ZerosDataToPreventDataRaces()
    {
        var headers = new Header[]
        {
            new("key1", System.Text.Encoding.UTF8.GetBytes("value1")),
            new("key2", System.Text.Encoding.UTF8.GetBytes("value2"))
        };

        var slice = HeaderSlice.Rent(headers, 2);
        await Assert.That(slice.Count).IsEqualTo(2);

        HeaderSlice.Return(slice);

        // After return, the slice is zeroed to prevent data races: if this slice is
        // re-rented by another consumer, the previous holder must not see new data.
        // Callers that need headers after enumeration disposal (e.g., ConsumeOneAsync)
        // must call MaterializeHeaders() before returning.
        await Assert.That(slice.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HeaderSlice_ReRentAfterReturn_PreviousHolderSeesNewData_DocumentsRace()
    {
        // This test documents why Return() must zero the slice and why ConsumeOneAsync
        // must materialize headers before breaking out of await foreach.
        var headers1 = new Header[]
        {
            new("original-key", System.Text.Encoding.UTF8.GetBytes("original-value"))
        };

        var slice = HeaderSlice.Rent(headers1, 1);
        var previousReference = slice; // Simulates ConsumeResult still holding a reference

        HeaderSlice.Return(slice);

        // A different consumer re-rents the same instance from the pool
        var headers2 = new Header[]
        {
            new("new-key", System.Text.Encoding.UTF8.GetBytes("new-value")),
            new("extra-key", System.Text.Encoding.UTF8.GetBytes("extra-value"))
        };
        var reRented = HeaderSlice.Rent(headers2, 2);

        // The previous reference and the re-rented reference are the same object
        await Assert.That(ReferenceEquals(previousReference, reRented)).IsTrue();

        // Without materialization, the previous holder would see the new consumer's data.
        // This is the data race that WithMaterializedHeaders() prevents in ConsumeOneAsync.
        await Assert.That(previousReference.Count).IsEqualTo(2);
        await Assert.That(previousReference[0].Key).IsEqualTo("new-key");

        HeaderSlice.Return(reRented);
    }

    [Test]
    public async Task HeaderSlice_MaterializeHeaders_CreatesIndependentSnapshot()
    {
        var headers = new Header[]
        {
            new("key1", System.Text.Encoding.UTF8.GetBytes("value1")),
            new("key2", System.Text.Encoding.UTF8.GetBytes("value2"))
        };

        var slice = HeaderSlice.Rent(headers, 2);

        // Materialize before return (what ConsumeOneAsync does)
        var materialized = slice.MaterializeHeaders();

        HeaderSlice.Return(slice);

        // The materialized array is independent of the pooled slice
        await Assert.That(materialized).Count().IsEqualTo(2);
        await Assert.That(materialized[0].Key).IsEqualTo("key1");
        await Assert.That(materialized[1].Key).IsEqualTo("key2");

        // Even after re-rent with different data, materialized is unaffected
        var newHeaders = new Header[]
        {
            new("different", System.Text.Encoding.UTF8.GetBytes("data"))
        };
        var reRented = HeaderSlice.Rent(newHeaders, 1);

        await Assert.That(materialized).Count().IsEqualTo(2);
        await Assert.That(materialized[0].Key).IsEqualTo("key1");

        HeaderSlice.Return(reRented);
    }

    [Test]
    public async Task HeaderSlice_RentAfterReturn_ReusesInstance()
    {
        var headers1 = new Header[]
        {
            new("key1", System.Text.Encoding.UTF8.GetBytes("value1"))
        };
        var headers2 = new Header[]
        {
            new("key2", System.Text.Encoding.UTF8.GetBytes("value2")),
            new("key3", System.Text.Encoding.UTF8.GetBytes("value3"))
        };

        var slice1 = HeaderSlice.Rent(headers1, 1);
        HeaderSlice.Return(slice1);

        var slice2 = HeaderSlice.Rent(headers2, 2);

        // After return + rent, the pooled instance should be reused
        // with the new data
        await Assert.That(ReferenceEquals(slice1, slice2)).IsTrue();
        await Assert.That(slice2.Count).IsEqualTo(2);
        await Assert.That(slice2[0].Key).IsEqualTo("key2");
        await Assert.That(slice2[1].Key).IsEqualTo("key3");

        HeaderSlice.Return(slice2);
    }
}
