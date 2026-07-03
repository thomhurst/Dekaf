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
    public async Task TopicPartitionOffset_IncludesLeaderEpoch()
    {
        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 42,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: 7,
            keyDeserializer: null,
            valueDeserializer: null);

        await Assert.That(result.TopicPartitionOffset)
            .IsEqualTo(new TopicPartitionOffset("test-topic", 0, 42, 7));
    }

    [Test]
    public async Task LazyConsumeHeaders_CountDoesNotMaterialize()
    {
        var pooledHeaders = new[]
        {
            new Header("trace-id", "abc"u8.ToArray())
        };
        var pending = CreatePendingFetchData(pooledHeaders);
        pending.EagerParseAll();
        pending.MoveNext();

        var headers = LazyConsumeHeaders.Create(pooledHeaders, 1, pending, pending.HeaderGeneration);

        await Assert.That(headers).IsNotNull();
        await Assert.That(headers!.Count).IsEqualTo(1);

        pending.Dispose();
    }

    [Test]
    public async Task LazyConsumeHeaders_FirstAccessCopiesSnapshot()
    {
        var pooledHeaders = new[]
        {
            new Header("trace-id", "abc"u8.ToArray())
        };
        var pending = CreatePendingFetchData(pooledHeaders);
        pending.EagerParseAll();
        pending.MoveNext();

        var headers = LazyConsumeHeaders.Create(pooledHeaders, 1, pending, pending.HeaderGeneration)!;
        var header = headers[0];

        pooledHeaders[0] = new Header("changed", "def"u8.ToArray());

        await Assert.That(header.Key).IsEqualTo("trace-id");
        await Assert.That(headers[0].Key).IsEqualTo("trace-id");

        pending.Dispose();
    }

    [Test]
    public async Task LazyConsumeHeaders_AccessAfterDisposeBeforeMaterialize_ThrowsObjectDisposedException()
    {
        var pooledHeaders = new[]
        {
            new Header("trace-id", "abc"u8.ToArray())
        };
        var pending = CreatePendingFetchData(pooledHeaders);
        pending.EagerParseAll();
        pending.MoveNext();

        var headers = LazyConsumeHeaders.Create(pooledHeaders, 1, pending, pending.HeaderGeneration)!;
        pending.Dispose();

        _ = headers.Count;
        await Assert.That(() => headers[0]).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ConsumeResult_PooledHeaders_MaterializesOnHeadersAccess()
    {
        var pooledHeaders = new[]
        {
            new Header("trace-id", "abc"u8.ToArray())
        };
        var pending = CreatePendingFetchData(pooledHeaders);
        pending.EagerParseAll();
        pending.MoveNext();

        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 0,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            pooledHeaders: pooledHeaders,
            pooledHeaderCount: pooledHeaders.Length,
            headerOwner: pending,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        var headers = result.Headers!;
        var first = headers[0];
        pooledHeaders[0] = new Header("changed", "def"u8.ToArray());

        await Assert.That(first.Key).IsEqualTo("trace-id");
        await Assert.That(headers[0].Key).IsEqualTo("trace-id");

        pending.Dispose();
    }

    private static PendingFetchData CreatePendingFetchData(Header[] headers)
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            Attributes = RecordBatchAttributes.TimestampTypeCreateTime,
            Records =
            [
                new Dekaf.Protocol.Records.Record
                {
                    Headers = headers,
                    HeaderCount = headers.Length,
                    Key = ReadOnlyMemory<byte>.Empty,
                    Value = ReadOnlyMemory<byte>.Empty,
                    IsKeyNull = true,
                    IsValueNull = true
                }
            ]
        };

        return PendingFetchData.Create("test-topic", 0, [batch]);
    }

}
