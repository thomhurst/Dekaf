using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer.DeadLetter;

public class KafkaConsumerServiceDlqTests
{
    [Test]
    public async Task DefaultPolicy_WithDefaultOptions_DeadLettersOnFirstFailure()
    {
        var options = new DeadLetterOptions();
        var policy = new DefaultDeadLetterPolicy<string, string>(options);
        var result = CreateTestConsumeResult("orders");

        await Assert.That(policy.ShouldDeadLetter(result, new InvalidOperationException("fail"), 1)).IsTrue();
    }

    [Test]
    public async Task DefaultPolicy_WithMaxFailures3_DoesNotDeadLetterBelowThreshold()
    {
        var options = new DeadLetterOptions { MaxFailures = 3 };
        var policy = new DefaultDeadLetterPolicy<string, string>(options);
        var result = CreateTestConsumeResult("orders");

        await Assert.That(policy.ShouldDeadLetter(result, new InvalidOperationException("fail"), 1)).IsFalse();
        await Assert.That(policy.ShouldDeadLetter(result, new InvalidOperationException("fail"), 2)).IsFalse();
        await Assert.That(policy.ShouldDeadLetter(result, new InvalidOperationException("fail"), 3)).IsTrue();
    }

    [Test]
    public async Task DeadLetterTopic_WithDottedSourceTopic()
    {
        var options = new DeadLetterOptions();
        var policy = new DefaultDeadLetterPolicy<string, string>(options);

        await Assert.That(policy.GetDeadLetterTopic("events.user.created")).IsEqualTo("events.user.created.DLQ");
    }

    [Test]
    public async Task DeadLetterTopic_WithHyphenatedSourceTopic()
    {
        var options = new DeadLetterOptions();
        var policy = new DefaultDeadLetterPolicy<string, string>(options);

        await Assert.That(policy.GetDeadLetterTopic("order-events")).IsEqualTo("order-events.DLQ");
    }

    [Test]
    public async Task Headers_ContainAll7RequiredDlqKeys()
    {
        var result = CreateTestConsumeResult("orders", partition: 3, offset: 42);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("bad data"), 2, includeException: true);

        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.SourceTopicKey)).IsNotNull();
        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.SourcePartitionKey)).IsNotNull();
        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.SourceOffsetKey)).IsNotNull();
        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.ErrorMessageKey)).IsNotNull();
        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.ErrorTypeKey)).IsNotNull();
        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.FailureCountKey)).IsNotNull();
        await Assert.That(headers.GetFirstAsString(DeadLetterHeaders.TimestampKey)).IsNotNull();
    }

    [Test]
    public async Task Headers_CountIs7_WhenNoOriginalHeaders()
    {
        var result = CreateTestConsumeResult("orders", partition: 0, offset: 0);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("fail"), 1, includeException: true);

        await Assert.That(headers.Count).IsEqualTo(7);
    }

    [Test]
    public async Task Headers_CountIs5_WhenExcludingException()
    {
        var result = CreateTestConsumeResult("orders", partition: 0, offset: 0);
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("fail"), 1, includeException: false);

        await Assert.That(headers.Count).IsEqualTo(5);
    }

    [Test]
    public async Task Headers_OriginalHeadersPreservedAndDlqHeadersAdded()
    {
        var originalHeaders = new Headers()
            .Add("trace-id", "abc123")
            .Add("span-id", "def456");
        var result = CreateTestConsumeResult("orders", headers: originalHeaders.ToList());
        var headers = DeadLetterHeaders.Build(result, new InvalidOperationException("fail"), 1, includeException: true);

        // 2 original + 7 DLQ = 9 total
        await Assert.That(headers.Count).IsEqualTo(9);
        await Assert.That(headers.GetFirstAsString("trace-id")).IsEqualTo("abc123");
        await Assert.That(headers.GetFirstAsString("span-id")).IsEqualTo("def456");
    }

    [Test]
    public async Task CustomPolicy_IsUsedForDecision()
    {
        var callCount = 0;
        var policy = new CountingDeadLetterPolicy(() => callCount++);
        var result = CreateTestConsumeResult("orders");

        policy.ShouldDeadLetter(result, new InvalidOperationException(), 1);
        policy.ShouldDeadLetter(result, new InvalidOperationException(), 2);

        await Assert.That(callCount).IsEqualTo(2);
    }

    [Test]
    public async Task CustomPolicy_CanReturnFalseToPreventDlq()
    {
        var policy = new NeverDeadLetterPolicy();
        var result = CreateTestConsumeResult("orders");

        await Assert.That(policy.ShouldDeadLetter(result, new InvalidOperationException(), 100)).IsFalse();
    }

    [Test]
    public async Task DeadLetterQueueBuilder_FluentApi_ProducesCorrectOptions()
    {
        var builder = new DeadLetterQueueBuilder();
        var options = builder
            .WithTopicSuffix("-dlq")
            .WithMaxFailures(5)
            .AwaitDelivery()
            .ExcludeExceptionFromHeaders()
            .WithBootstrapServers("kafka:9092")
            .Build();

        await Assert.That(options.TopicSuffix).IsEqualTo("-dlq");
        await Assert.That(options.MaxFailures).IsEqualTo(5);
        await Assert.That(options.AwaitDelivery).IsTrue();
        await Assert.That(options.IncludeExceptionInHeaders).IsFalse();
        await Assert.That(options.BootstrapServers).IsEqualTo("kafka:9092");
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
            timestamp: DateTimeOffset.UtcNow,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);
    }

    private sealed class CountingDeadLetterPolicy(Action onInvoke) : IDeadLetterPolicy<string, string>
    {
        public bool ShouldDeadLetter(ConsumeResult<string, string> result, Exception exception, int failureCount)
        {
            onInvoke();
            return true;
        }

        public string GetDeadLetterTopic(string sourceTopic) => sourceTopic + ".DLQ";
    }

    private sealed class NeverDeadLetterPolicy : IDeadLetterPolicy<string, string>
    {
        public bool ShouldDeadLetter(ConsumeResult<string, string> result, Exception exception, int failureCount)
            => false;

        public string GetDeadLetterTopic(string sourceTopic) => sourceTopic + ".DLQ";
    }
}
