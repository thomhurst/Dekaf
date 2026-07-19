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
    public async Task GetListOffsetsTimestamp_NoneIncludesGroupAndPartitionInExceptionMessage()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "orders-group",
            AutoOffsetReset = AutoOffsetReset.None
        };

        await Assert.That(() => AutoOffsetResetStrategy.GetListOffsetsTimestamp(
                options,
                DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
                new TopicPartition("orders", 5)))
            .Throws<KafkaException>()
            .WithMessageContaining("orders-group")
            .And.WithMessageContaining("orders-5");
    }

    [Test]
    public async Task GetListOffsetsTimestamp_NewPartitionPolicyOverridesBasePolicy()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "orders-group",
            AutoOffsetReset = AutoOffsetReset.Latest,
            AutoOffsetResetNewPartitions = AutoOffsetReset.Earliest
        };

        var existingTimestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(
            options,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"));
        var newTimestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(
            options,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
            isNewPartition: true);

        await Assert.That(existingTimestamp).IsEqualTo(-1);
        await Assert.That(newTimestamp).IsEqualTo(-2);
    }

    [Test]
    public async Task GetListOffsetsTimestamp_NewPartitionByDurationUsesIndependentDuration()
    {
        var now = DateTimeOffset.Parse("2026-07-02T12:00:00Z");
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "orders-group",
            AutoOffsetReset = AutoOffsetReset.ByDuration,
            AutoOffsetResetDuration = TimeSpan.FromHours(1),
            AutoOffsetResetNewPartitions = AutoOffsetReset.ByDuration,
            AutoOffsetResetNewPartitionsDuration = TimeSpan.FromMinutes(5)
        };

        var existingTimestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(options, now);
        var newTimestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(
            options,
            now,
            isNewPartition: true);

        await Assert.That(existingTimestamp).IsEqualTo(now.AddHours(-1).ToUnixTimeMilliseconds());
        await Assert.That(newTimestamp).IsEqualTo(now.AddMinutes(-5).ToUnixTimeMilliseconds());
    }

    [Test]
    public async Task GetListOffsetsTimestamp_UnsetNewPartitionPolicyUsesBasePolicy()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "orders-group",
            AutoOffsetReset = AutoOffsetReset.Latest
        };

        var timestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(
            options,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
            isNewPartition: true);

        await Assert.That(timestamp).IsEqualTo(-1);
    }

    [Test]
    public async Task GetListOffsetsTimestamp_BaseNoneStillAllowsConfiguredNewPartitionPolicy()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "orders-group",
            AutoOffsetReset = AutoOffsetReset.None,
            AutoOffsetResetNewPartitions = AutoOffsetReset.Earliest
        };

        var timestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(
            options,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
            isNewPartition: true);

        await Assert.That(timestamp).IsEqualTo(-2);
    }

    [Test]
    public async Task ValidateOptions_NewPartitionByDurationWithoutDuration_Throws()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "orders-group",
            AutoOffsetResetNewPartitions = AutoOffsetReset.ByDuration
        };

        await Assert.That(() => AutoOffsetResetStrategy.ValidateOptions(options))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ValidateOptions_NewPartitionNone_Throws()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "orders-group",
            AutoOffsetResetNewPartitions = AutoOffsetReset.None
        };

        await Assert.That(() => AutoOffsetResetStrategy.ValidateOptions(options))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ValidateOptions_NewPartitionPolicyWithoutGroup_Throws()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            AutoOffsetResetNewPartitions = AutoOffsetReset.Earliest
        };

        await Assert.That(() => AutoOffsetResetStrategy.ValidateOptions(options))
            .Throws<InvalidOperationException>();
    }
}
