using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer.DeadLetter;

public sealed class RetryTopicHeadersTests
{
    [Test]
    [Arguments(-62135596800000L)]
    [Arguments(0L)]
    [Arguments(253402300799999L)]
    public async Task TryGetDueAt_AcceptsDateTimeOffsetBoundaries(long timestamp)
    {
        var headers = new Headers();
        headers.Add(RetryTopicHeaders.DueTimestampMsKey, timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));

        await Assert.That(RetryTopicHeaders.TryGetDueAt(headers.ToList(), out var dueAt)).IsTrue();
        await Assert.That(dueAt.ToUnixTimeMilliseconds()).IsEqualTo(timestamp);
    }

    [Test]
    [Arguments("-62135596800001")]
    [Arguments("253402300800000")]
    [Arguments("9223372036854775807")]
    [Arguments("-9223372036854775808")]
    [Arguments("not-a-date")]
    public async Task TryGetDueAt_InvalidTimestamp_ReturnsFalse(string timestamp)
    {
        var headers = new Headers();
        headers.Add(RetryTopicHeaders.DueTimestampMsKey, timestamp);

        await Assert.That(RetryTopicHeaders.TryGetDueAt(headers.ToList(), out var dueAt)).IsFalse();
        await Assert.That(dueAt).IsEqualTo(default(DateTimeOffset));
    }

    [Test]
    public async Task Build_IncludesRetryMetadata()
    {
        var dueAt = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        var result = CreateTestConsumeResult("orders", partition: 2, offset: 100);

        var headers = RetryTopicHeaders.Build(result, 3, TimeSpan.FromSeconds(30), dueAt);

        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.SourceTopicKey)).IsEqualTo("orders");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.SourcePartitionKey)).IsEqualTo("2");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.SourceOffsetKey)).IsEqualTo("100");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.FailureCountKey)).IsEqualTo("3");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.DelayMsKey)).IsEqualTo("30000");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.DueTimestampMsKey))
            .IsEqualTo("1700000000000");
    }

    [Test]
    public async Task Build_PreservesOriginalHeadersAndReplacesRetryHeaders()
    {
        var originalHeaders = new Headers()
            .Add("trace-id", "abc123")
            .Add(RetryTopicHeaders.FailureCountKey, "1");
        var result = CreateTestConsumeResult("orders-retry-5s", headers: originalHeaders.ToList());

        var headers = RetryTopicHeaders.Build(
            result,
            failureCount: 2,
            delay: TimeSpan.FromSeconds(30),
            dueAt: DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000));

        await Assert.That(headers.Count).IsEqualTo(7);
        await Assert.That(headers.GetFirstAsString("trace-id")).IsEqualTo("abc123");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.FailureCountKey)).IsEqualTo("2");
    }

    [Test]
    public async Task Build_WhenSourceRetryHeadersExist_KeepsOriginalSource()
    {
        var sourceHeaders = RetryTopicHeaders.Build(
            CreateTestConsumeResult("orders", partition: 1, offset: 42),
            failureCount: 1,
            delay: TimeSpan.FromSeconds(5),
            dueAt: DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000));
        var result = CreateTestConsumeResult(
            "orders-retry-5s",
            partition: 3,
            offset: 99,
            headers: sourceHeaders.ToList());

        var headers = RetryTopicHeaders.Build(
            result,
            failureCount: 2,
            delay: TimeSpan.FromSeconds(30),
            dueAt: DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_030_000));

        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.SourceTopicKey)).IsEqualTo("orders");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.SourcePartitionKey)).IsEqualTo("1");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.SourceOffsetKey)).IsEqualTo("42");
        await Assert.That(headers.GetFirstAsString(RetryTopicHeaders.FailureCountKey)).IsEqualTo("2");
    }

    [Test]
    public async Task GetFailureCount_WhenHeaderMissing_ReturnsZero()
    {
        await Assert.That(RetryTopicHeaders.GetFailureCount(null)).IsEqualTo(0);
    }

    [Test]
    public async Task TryGetDueAt_WhenHeaderPresent_ReturnsTimestamp()
    {
        var dueAt = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        var headers = RetryTopicHeaders.Build(
            CreateTestConsumeResult("orders"),
            failureCount: 1,
            delay: TimeSpan.FromSeconds(5),
            dueAt: dueAt);

        var found = RetryTopicHeaders.TryGetDueAt(headers.ToList(), out var actual);

        await Assert.That(found).IsTrue();
        await Assert.That(actual).IsEqualTo(dueAt);
    }

    private static ConsumeResult<string, string> CreateTestConsumeResult(
        string topic = "test",
        int partition = 0,
        long offset = 0,
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
