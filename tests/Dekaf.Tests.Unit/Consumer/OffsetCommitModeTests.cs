using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Consumer;

public class OffsetCommitModeTests
{
    [Test]
    public async Task ConsumerBuilder_WithOffsetCommitMode_ConfiguresOption()
    {
        // Verify the builder method exists and returns the builder for chaining
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithOffsetCommitMode(OffsetCommitMode.Auto); // Chain test

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_OffsetCommitMode_DefaultIsAuto()
    {
        // The default should be Auto
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.OffsetCommitMode).IsEqualTo(OffsetCommitMode.Auto);
    }

    [Test]
    public async Task ConsumerBuilder_OffsetCommitMode_CanBeSetToManual()
    {
        // Verify that OffsetCommitMode can be set to Manual
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            OffsetCommitMode = OffsetCommitMode.Manual
        };

        await Assert.That(options.OffsetCommitMode).IsEqualTo(OffsetCommitMode.Manual);
    }

    [Test]
    public async Task TopicPartitionOffset_CanBeCreated()
    {
        // Verify TopicPartitionOffset can be created for use with CommitAsync
        var tpo = new TopicPartitionOffset("test-topic", 0, 100);

        await Assert.That(tpo.Topic).IsEqualTo("test-topic");
        await Assert.That(tpo.Partition).IsEqualTo(0);
        await Assert.That(tpo.Offset).IsEqualTo(100);
    }

    [Test]
    public async Task TopicPartition_CanBeCreated()
    {
        // Verify TopicPartition can be created
        var tp = new TopicPartition("test-topic", 0);

        await Assert.That(tp.Topic).IsEqualTo("test-topic");
        await Assert.That(tp.Partition).IsEqualTo(0);
    }

    [Test]
    public async Task IKafkaConsumer_CommitAsync_MethodExists()
    {
        // Verify the interface has the CommitAsync methods defined
        var interfaceType = typeof(IKafkaConsumer<string, string>);

        var commitAsyncNoArgs = interfaceType.GetMethod(
            nameof(IKafkaConsumer<string, string>.CommitAsync),
            [typeof(CancellationToken)]);

        var commitAsyncWithOffsets = interfaceType.GetMethod(
            nameof(IKafkaConsumer<string, string>.CommitAsync),
            [typeof(IEnumerable<TopicPartitionOffset>), typeof(CancellationToken)]);

        await Assert.That(commitAsyncNoArgs).IsNotNull();
        await Assert.That(commitAsyncWithOffsets).IsNotNull();
    }

    [Test]
    public async Task ConsumerOptions_OffsetCommitMode_DefaultValue()
    {
        // Verify the default value in ConsumerOptions
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        // Default should be Auto
        await Assert.That(options.OffsetCommitMode).IsEqualTo(OffsetCommitMode.Auto);
    }

    [Test]
    public async Task ConsumerOptions_OffsetCommitMode_AllValuesValid()
    {
        // Verify all OffsetCommitMode values can be set
        var autoOptions = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            OffsetCommitMode = OffsetCommitMode.Auto
        };
        await Assert.That(autoOptions.OffsetCommitMode).IsEqualTo(OffsetCommitMode.Auto);

        var manualOptions = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            OffsetCommitMode = OffsetCommitMode.Manual
        };
        await Assert.That(manualOptions.OffsetCommitMode).IsEqualTo(OffsetCommitMode.Manual);
    }
}
