using System.Text;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ShareGroupDeadLetterHeadersTests
{
    [Test]
    public async Task HeaderKeys_MatchKip1191()
    {
        await Assert.That(ShareGroupDeadLetterHeaders.TopicKey).IsEqualTo("__dlq.errors.topic");
        await Assert.That(ShareGroupDeadLetterHeaders.PartitionKey).IsEqualTo("__dlq.errors.partition");
        await Assert.That(ShareGroupDeadLetterHeaders.OffsetKey).IsEqualTo("__dlq.errors.offset");
        await Assert.That(ShareGroupDeadLetterHeaders.GroupKey).IsEqualTo("__dlq.errors.group");
        await Assert.That(ShareGroupDeadLetterHeaders.DeliveryCountKey).IsEqualTo("__dlq.errors.delivery.count");
        await Assert.That(ShareGroupDeadLetterHeaders.MessageKey).IsEqualTo("__dlq.errors.message");
    }

    [Test]
    public async Task TryParse_ReadsBrokerHeaders()
    {
        var headers = BrokerHeaders();
        headers.Insert(0, Utf8("app", "kept"));

        await Assert.That(ShareGroupDeadLetterHeaders.TryParse(headers, out var context)).IsTrue();
        await Assert.That(context).IsEqualTo(new ShareGroupDeadLetterContext(
            "orders", 3, 9_000_000_000, "workers", 5, "Offset delivery count exceeded the threshold."));
    }

    [Test]
    public async Task TryParse_TreatsMessageAsOptional()
    {
        var headers = BrokerHeaders();
        headers.RemoveAll(header => header.Key == ShareGroupDeadLetterHeaders.MessageKey);

        await Assert.That(ShareGroupDeadLetterHeaders.TryParse(headers, out var context)).IsTrue();
        await Assert.That(context.Message).IsNull();
    }

    [Test]
    [Arguments(ShareGroupDeadLetterHeaders.TopicKey)]
    [Arguments(ShareGroupDeadLetterHeaders.PartitionKey)]
    [Arguments(ShareGroupDeadLetterHeaders.OffsetKey)]
    [Arguments(ShareGroupDeadLetterHeaders.GroupKey)]
    [Arguments(ShareGroupDeadLetterHeaders.DeliveryCountKey)]
    public async Task TryParse_FailsWhenRequiredHeaderIsMissingOrNull(string key)
    {
        var missing = BrokerHeaders();
        missing.RemoveAll(header => header.Key == key);
        var nullValue = BrokerHeaders();
        nullValue[nullValue.FindIndex(header => header.Key == key)] = new Header(key, (byte[]?)null);

        await Assert.That(ShareGroupDeadLetterHeaders.TryParse(missing, out var context)).IsFalse();
        await Assert.That(context).IsEqualTo(default(ShareGroupDeadLetterContext));
        await Assert.That(ShareGroupDeadLetterHeaders.TryParse(nullValue, out _)).IsFalse();
    }

    [Test]
    [Arguments(ShareGroupDeadLetterHeaders.PartitionKey, "")]
    [Arguments(ShareGroupDeadLetterHeaders.PartitionKey, "x")]
    [Arguments(ShareGroupDeadLetterHeaders.PartitionKey, "-1")]
    [Arguments(ShareGroupDeadLetterHeaders.PartitionKey, "1 ")]
    [Arguments(ShareGroupDeadLetterHeaders.PartitionKey, "2147483648")]
    [Arguments(ShareGroupDeadLetterHeaders.OffsetKey, "-5")]
    [Arguments(ShareGroupDeadLetterHeaders.OffsetKey, "1.5")]
    [Arguments(ShareGroupDeadLetterHeaders.OffsetKey, "9223372036854775808")]
    [Arguments(ShareGroupDeadLetterHeaders.DeliveryCountKey, "0")]
    [Arguments(ShareGroupDeadLetterHeaders.DeliveryCountKey, "five")]
    [Arguments(ShareGroupDeadLetterHeaders.TopicKey, "")]
    [Arguments(ShareGroupDeadLetterHeaders.GroupKey, "")]
    public async Task TryParse_FailsWhenRequiredHeaderIsMalformed(string key, string value)
    {
        var headers = BrokerHeaders();
        headers[headers.FindIndex(header => header.Key == key)] = Utf8(key, value);

        await Assert.That(ShareGroupDeadLetterHeaders.TryParse(headers, out _)).IsFalse();
    }

    [Test]
    public async Task IndividualAccessors_ReturnValuesAndReportMissingHeaders()
    {
        var headers = BrokerHeaders();
        await Assert.That(ShareGroupDeadLetterHeaders.GetTopic(headers)).IsEqualTo("orders");
        await Assert.That(ShareGroupDeadLetterHeaders.GetGroup(headers)).IsEqualTo("workers");
        await Assert.That(ShareGroupDeadLetterHeaders.GetMessage(headers)).IsEqualTo("Offset delivery count exceeded the threshold.");
        await Assert.That(ShareGroupDeadLetterHeaders.TryGetPartition(headers, out var partition)).IsTrue();
        await Assert.That(partition).IsEqualTo(3);
        await Assert.That(ShareGroupDeadLetterHeaders.TryGetOffset(headers, out var offset)).IsTrue();
        await Assert.That(offset).IsEqualTo(9_000_000_000L);
        await Assert.That(ShareGroupDeadLetterHeaders.TryGetDeliveryCount(headers, out var deliveryCount)).IsTrue();
        await Assert.That(deliveryCount).IsEqualTo(5);

        await Assert.That(ShareGroupDeadLetterHeaders.GetTopic(null)).IsNull();
        await Assert.That(ShareGroupDeadLetterHeaders.TryGetOffset(null, out offset)).IsFalse();
        await Assert.That(offset).IsEqualTo(0L);
        await Assert.That(ShareGroupDeadLetterHeaders.TryParse([], out _)).IsFalse();
    }

    [Test]
    public async Task NumericAccessors_DoNotAllocate()
    {
        IReadOnlyList<Header> headers = BrokerHeaders();
        // Warm up JIT and tiering before measuring.
        for (var i = 0; i < 100; i++)
            Read(headers);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0L;
        for (var i = 0; i < 1_000; i++)
            sum += Read(headers);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(sum).IsEqualTo(1_000L * (3 + 9_000_000_000 + 5));
        await Assert.That(allocated).IsEqualTo(0L);

        static long Read(IReadOnlyList<Header> headers)
        {
            ShareGroupDeadLetterHeaders.TryGetPartition(headers, out var partition);
            ShareGroupDeadLetterHeaders.TryGetOffset(headers, out var offset);
            ShareGroupDeadLetterHeaders.TryGetDeliveryCount(headers, out var deliveryCount);
            return partition + offset + deliveryCount;
        }
    }

    private static List<Header> BrokerHeaders() =>
    [
        Utf8(ShareGroupDeadLetterHeaders.TopicKey, "orders"),
        Utf8(ShareGroupDeadLetterHeaders.PartitionKey, "3"),
        Utf8(ShareGroupDeadLetterHeaders.OffsetKey, "9000000000"),
        Utf8(ShareGroupDeadLetterHeaders.GroupKey, "workers"),
        Utf8(ShareGroupDeadLetterHeaders.DeliveryCountKey, "5"),
        Utf8(ShareGroupDeadLetterHeaders.MessageKey, "Offset delivery count exceeded the threshold.")
    ];

    private static Header Utf8(string key, string value) => new(key, Encoding.UTF8.GetBytes(value));
}
