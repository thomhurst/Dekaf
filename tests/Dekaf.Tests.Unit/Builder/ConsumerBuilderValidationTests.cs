using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Builder;

public class ConsumerBuilderValidationTests
{
    #region Build Validation

    [Test]
    public async Task Build_WithoutBootstrapServers_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_WithBootstrapServers_Succeeds()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var act = () => builder.Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task Build_WithUnsupportedKeyType_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateConsumer<DateTime, string>()
            .WithBootstrapServers("localhost:9092");

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_WithUnsupportedValueType_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateConsumer<string, DateTime>()
            .WithBootstrapServers("localhost:9092");

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    #endregion

    #region Chaining Tests

    [Test]
    public async Task WithBootstrapServers_String_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithBootstrapServers("localhost:9092");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithBootstrapServers_Array_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithBootstrapServers("host1:9092", "host2:9092");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithClientId_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithClientId("my-client");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithGroupId_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithGroupId("my-group");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithGroupInstanceId_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithGroupInstanceId("my-instance");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task SubscribeTo_Single_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.SubscribeTo("my-topic");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task SubscribeTo_Multiple_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.SubscribeTo("topic1", "topic2");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithAutoCommitInterval_Int_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithAutoCommitInterval(1000);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithAutoCommitInterval_TimeSpan_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithAutoCommitInterval(TimeSpan.FromSeconds(1));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithOffsetCommitMode_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithOffsetCommitMode(OffsetCommitMode.Manual);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithAutoOffsetReset_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithAutoOffsetReset(AutoOffsetReset.Earliest);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithMaxPollRecords_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithMaxPollRecords(100);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSessionTimeout_Int_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithSessionTimeout(10000);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSessionTimeout_TimeSpan_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithSessionTimeout(TimeSpan.FromSeconds(10));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task UseTls_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.UseTls();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSaslPlain_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithSaslPlain("user", "pass");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithPartitionEof_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithPartitionEof();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithQueuedMinMessages_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithQueuedMinMessages(1000);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithQueuedMinMessages_LessThan1_Throws()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithQueuedMinMessages(0);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Preset Methods

    [Test]
    public async Task ForHighThroughput_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.ForHighThroughput();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ForLowLatency_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.ForLowLatency();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ForHighThroughput_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForHighThroughput()
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task ForLowLatency_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForLowLatency()
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
