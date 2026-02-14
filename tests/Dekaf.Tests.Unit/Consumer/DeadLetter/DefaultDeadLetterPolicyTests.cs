using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;

namespace Dekaf.Tests.Unit.Consumer.DeadLetter;

public class DefaultDeadLetterPolicyTests
{
    [Test]
    public async Task ShouldDeadLetter_WhenFailureCountMeetsThreshold_ReturnsTrue()
    {
        var options = new DeadLetterOptions { MaxFailures = 3 };
        var policy = new DefaultDeadLetterPolicy<string, string>(options);
        var result = CreateTestConsumeResult();

        await Assert.That(policy.ShouldDeadLetter(result, new InvalidOperationException(), 3)).IsTrue();
    }

    [Test]
    public async Task ShouldDeadLetter_WhenFailureCountExceedsThreshold_ReturnsTrue()
    {
        var options = new DeadLetterOptions { MaxFailures = 1 };
        var policy = new DefaultDeadLetterPolicy<string, string>(options);
        var result = CreateTestConsumeResult();

        await Assert.That(policy.ShouldDeadLetter(result, new InvalidOperationException(), 5)).IsTrue();
    }

    [Test]
    public async Task ShouldDeadLetter_WhenFailureCountBelowThreshold_ReturnsFalse()
    {
        var options = new DeadLetterOptions { MaxFailures = 3 };
        var policy = new DefaultDeadLetterPolicy<string, string>(options);
        var result = CreateTestConsumeResult();

        await Assert.That(policy.ShouldDeadLetter(result, new InvalidOperationException(), 2)).IsFalse();
    }

    [Test]
    public async Task GetDeadLetterTopic_AppendsConfiguredSuffix()
    {
        var options = new DeadLetterOptions { TopicSuffix = ".DLQ" };
        var policy = new DefaultDeadLetterPolicy<string, string>(options);

        await Assert.That(policy.GetDeadLetterTopic("orders")).IsEqualTo("orders.DLQ");
    }

    [Test]
    public async Task GetDeadLetterTopic_WithCustomSuffix_AppendsCustomSuffix()
    {
        var options = new DeadLetterOptions { TopicSuffix = "-dead-letters" };
        var policy = new DefaultDeadLetterPolicy<string, string>(options);

        await Assert.That(policy.GetDeadLetterTopic("orders")).IsEqualTo("orders-dead-letters");
    }

    [Test]
    public async Task DefaultOptions_MaxFailuresIsOne()
    {
        var options = new DeadLetterOptions();

        await Assert.That(options.MaxFailures).IsEqualTo(1);
    }

    [Test]
    public async Task DefaultOptions_TopicSuffixIsDotDLQ()
    {
        var options = new DeadLetterOptions();

        await Assert.That(options.TopicSuffix).IsEqualTo(".DLQ");
    }

    [Test]
    public async Task DefaultOptions_IncludeExceptionInHeadersIsTrue()
    {
        var options = new DeadLetterOptions();

        await Assert.That(options.IncludeExceptionInHeaders).IsTrue();
    }

    [Test]
    public async Task DefaultOptions_AwaitDeliveryIsFalse()
    {
        var options = new DeadLetterOptions();

        await Assert.That(options.AwaitDelivery).IsFalse();
    }

    private static ConsumeResult<string, string> CreateTestConsumeResult()
    {
        return new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 42,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestamp: DateTimeOffset.UtcNow,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);
    }
}
