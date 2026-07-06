using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer.DeadLetter;

public class DeadLetterHeadersTests
{
    [Test]
    public async Task BuildHeaders_IncludesSourceTopic()
    {
        var result = CreateTestConsumeResult("orders", 2, 100);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 1, includeException: true);

        await Assert.That(headers.GetFirstAsString("dlq.source.topic")).IsEqualTo("orders");
    }

    [Test]
    public async Task BuildHeaders_IncludesSourcePartition()
    {
        var result = CreateTestConsumeResult("orders", 2, 100);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 1, includeException: true);

        await Assert.That(headers.GetFirstAsString("dlq.source.partition")).IsEqualTo("2");
    }

    [Test]
    public async Task BuildHeaders_IncludesSourceOffset()
    {
        var result = CreateTestConsumeResult("orders", 2, 100);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 1, includeException: true);

        await Assert.That(headers.GetFirstAsString("dlq.source.offset")).IsEqualTo("100");
    }

    [Test]
    public async Task BuildHeaders_IncludesFailureCount()
    {
        var result = CreateTestConsumeResult("orders", 0, 0);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 3, includeException: true);

        await Assert.That(headers.GetFirstAsString("dlq.failure.count")).IsEqualTo("3");
    }

    [Test]
    public async Task BuildHeaders_IncludesTimestamp()
    {
        var result = CreateTestConsumeResult("orders", 0, 0);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 1, includeException: true);

        var timestamp = headers.GetFirstAsString("dlq.timestamp");
        await Assert.That(timestamp).IsNotNull();
        await Assert.That(DateTimeOffset.TryParse(timestamp, out _)).IsTrue();
    }

    [Test]
    public async Task BuildHeaders_WhenIncludeException_IncludesErrorMessage()
    {
        var result = CreateTestConsumeResult("orders", 0, 0);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("something went wrong"), 1, includeException: true);

        await Assert.That(headers.GetFirstAsString("dlq.error.message")).IsEqualTo("something went wrong");
    }

    [Test]
    public async Task BuildHeaders_WhenIncludeException_IncludesErrorType()
    {
        var result = CreateTestConsumeResult("orders", 0, 0);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 1, includeException: true);

        await Assert.That(headers.GetFirstAsString("dlq.error.type")).IsEqualTo("InvalidOperationException");
    }

    [Test]
    public async Task BuildHeaders_WhenExcludeException_OmitsErrorHeaders()
    {
        var result = CreateTestConsumeResult("orders", 0, 0);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 1, includeException: false);

        await Assert.That(headers.GetFirst("dlq.error.message")).IsNull();
        await Assert.That(headers.GetFirst("dlq.error.type")).IsNull();
    }

    [Test]
    public async Task BuildHeaders_PreservesOriginalHeaders()
    {
        var originalHeaders = new Headers()
            .Add("trace-id", "abc123")
            .Add("correlation-id", "xyz");
        var result = CreateTestConsumeResult("orders", 0, 0, originalHeaders.ToList());
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 1, includeException: true);

        await Assert.That(headers.GetFirstAsString("trace-id")).IsEqualTo("abc123");
        await Assert.That(headers.GetFirstAsString("correlation-id")).IsEqualTo("xyz");
    }

    [Test]
    public async Task BuildHeaders_FromRetryTopic_UsesOriginalSourceMetadata()
    {
        var retryHeaders = new Headers()
            .Add(RetryTopicHeaders.SourceTopicKey, "orders")
            .Add(RetryTopicHeaders.SourcePartitionKey, "3")
            .Add(RetryTopicHeaders.SourceOffsetKey, "99");
        var result = CreateTestConsumeResult("orders-retry-5s", 0, 7, retryHeaders.ToList());

        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad"), 2, includeException: true);

        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.SourceTopicKey)).IsEqualTo("orders");
        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.SourcePartitionKey)).IsEqualTo("3");
        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.SourceOffsetKey)).IsEqualTo("99");
    }

    private static ConsumeResult<string, string> CreateTestConsumeResult(
        string topic = "test", int partition = 0, long offset = 0,
        IReadOnlyList<Header>? headers = null)
    {
        return new ConsumeResult<string, string>(
            topic: topic,
            partition: partition,
            offset: offset,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: headers,
            timestampMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);
    }
}
