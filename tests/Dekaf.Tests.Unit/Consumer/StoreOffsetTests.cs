using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Consumer;

public class StoreOffsetTests
{
    [Test]
    public async Task ConsumerBuilder_WithAutoOffsetStore_ConfiguresOption()
    {
        // Verify the builder method exists and returns the builder for chaining
        var builder = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithAutoOffsetStore(false)
            .WithAutoOffsetStore(true); // Chain test

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_WithAutoOffsetStore_DefaultIsTrue()
    {
        // The default should be true (auto store enabled)
        // We verify this by checking the ConsumerOptions default
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.EnableAutoOffsetStore).IsTrue();
    }

    [Test]
    public async Task ConsumerBuilder_WithAutoOffsetStore_CanBeDisabled()
    {
        // Verify that when building with auto offset store disabled,
        // the option is correctly configured
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableAutoOffsetStore = false
        };

        await Assert.That(options.EnableAutoOffsetStore).IsFalse();
    }

    [Test]
    public async Task TopicPartitionOffset_CanBeCreated()
    {
        // Verify TopicPartitionOffset can be created for use with StoreOffset
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
    public async Task IKafkaConsumer_StoreOffset_MethodExists()
    {
        // Verify the interface has the StoreOffset methods defined
        var interfaceType = typeof(IKafkaConsumer<string, string>);

        var storeOffsetTopicPartitionOffset = interfaceType.GetMethod(
            nameof(IKafkaConsumer<string, string>.StoreOffset),
            [typeof(TopicPartitionOffset)]);

        var storeOffsetConsumeResult = interfaceType.GetMethod(
            nameof(IKafkaConsumer<string, string>.StoreOffset),
            [typeof(ConsumeResult<string, string>)]);

        await Assert.That(storeOffsetTopicPartitionOffset).IsNotNull();
        await Assert.That(storeOffsetConsumeResult).IsNotNull();
    }

    [Test]
    public async Task IKafkaConsumer_StoreOffset_ReturnType_IsConsumer()
    {
        // Verify StoreOffset returns IKafkaConsumer for method chaining
        var interfaceType = typeof(IKafkaConsumer<string, string>);

        var storeOffsetMethod = interfaceType.GetMethod(
            nameof(IKafkaConsumer<string, string>.StoreOffset),
            [typeof(TopicPartitionOffset)]);

        await Assert.That(storeOffsetMethod!.ReturnType).IsEqualTo(typeof(IKafkaConsumer<string, string>));
    }

    [Test]
    public async Task ConsumerOptions_EnableAutoOffsetStore_DefaultValue()
    {
        // Verify the default value in ConsumerOptions
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        // Default should be true (auto offset store enabled)
        await Assert.That(options.EnableAutoOffsetStore).IsTrue();
    }

    [Test]
    public async Task ConsumerOptions_EnableAutoOffsetStore_CanBeSetToFalse()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableAutoOffsetStore = false
        };

        await Assert.That(options.EnableAutoOffsetStore).IsFalse();
    }
}
