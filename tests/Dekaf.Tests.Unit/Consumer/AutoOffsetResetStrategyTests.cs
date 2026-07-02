using Dekaf.Consumer;
using Dekaf.Errors;

namespace Dekaf.Tests.Unit.Consumer;

public class AutoOffsetResetStrategyTests
{
    [Test]
    public async Task GetByDurationTimestamp_SubtractsDurationFromNow()
    {
        var now = DateTimeOffset.Parse("2026-07-02T12:00:00Z");

        var timestamp = AutoOffsetResetStrategy.GetByDurationTimestamp(TimeSpan.FromHours(2), now);

        await Assert.That(timestamp).IsEqualTo(now.AddHours(-2).ToUnixTimeMilliseconds());
    }

    [Test]
    public async Task ValidateOptions_ByDurationWithoutDuration_ThrowsInvalidOperationException()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            AutoOffsetReset = AutoOffsetReset.ByDuration
        };

        await Assert.That(() => AutoOffsetResetStrategy.ValidateOptions(options))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ValidateDuration_NegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => AutoOffsetResetStrategy.ValidateDuration(TimeSpan.FromSeconds(-1)))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GetListOffsetsTimestamp_NoneIncludesPartitionInExceptionMessage()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            AutoOffsetReset = AutoOffsetReset.None
        };

        await Assert.That(() => AutoOffsetResetStrategy.GetListOffsetsTimestamp(
                options,
                DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
                new TopicPartition("orders", 5)))
            .Throws<KafkaException>()
            .WithMessageContaining("orders-5");
    }
}
