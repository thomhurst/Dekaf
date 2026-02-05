using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public class ProducerMessageTests
{
    #region Create Factory Method Tests

    [Test]
    public async Task Create_WithTopicKeyValue_SetsProperties()
    {
        var message = ProducerMessage<string, string>.Create("my-topic", "key", "value");
        await Assert.That(message.Topic).IsEqualTo("my-topic");
        await Assert.That(message.Key).IsEqualTo("key");
        await Assert.That(message.Value).IsEqualTo("value");
    }

    [Test]
    public async Task Create_WithTopicKeyValue_DefaultsAreNull()
    {
        var message = ProducerMessage<string, string>.Create("my-topic", "key", "value");
        await Assert.That(message.Partition).IsNull();
        await Assert.That(message.Timestamp).IsNull();
        await Assert.That(message.Headers).IsNull();
    }

    [Test]
    public async Task Create_WithNullKey_Succeeds()
    {
        var message = ProducerMessage<string, string>.Create("my-topic", null, "value");
        await Assert.That(message.Key).IsNull();
        await Assert.That(message.Value).IsEqualTo("value");
    }

    [Test]
    public async Task Create_WithHeaders_SetsHeaders()
    {
        var headers = new Headers().Add("h1", "v1");
        var message = ProducerMessage<string, string>.Create("my-topic", "key", "value", headers);
        await Assert.That(message.Headers).IsSameReferenceAs(headers);
    }

    [Test]
    public async Task Create_WithPartition_SetsPartition()
    {
        var message = ProducerMessage<string, string>.Create("my-topic", 5, "key", "value");
        await Assert.That(message.Partition).IsEqualTo(5);
        await Assert.That(message.Topic).IsEqualTo("my-topic");
        await Assert.That(message.Key).IsEqualTo("key");
        await Assert.That(message.Value).IsEqualTo("value");
    }

    #endregion

    #region Init Property Tests

    [Test]
    public async Task Topic_IsRequired()
    {
        var message = new ProducerMessage<string, string> { Topic = "topic", Value = "value" };
        await Assert.That(message.Topic).IsEqualTo("topic");
    }

    [Test]
    public async Task Partition_CanBeSetViaInit()
    {
        var message = new ProducerMessage<string, string> { Topic = "topic", Value = "value", Partition = 3 };
        await Assert.That(message.Partition).IsEqualTo(3);
    }

    [Test]
    public async Task Timestamp_CanBeSetViaInit()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var message = new ProducerMessage<string, string> { Topic = "topic", Value = "value", Timestamp = timestamp };
        await Assert.That(message.Timestamp).IsEqualTo(timestamp);
    }

    [Test]
    public async Task Headers_CanBeSetViaInit()
    {
        var headers = new Headers().Add("key", "val");
        var message = new ProducerMessage<string, string> { Topic = "topic", Value = "value", Headers = headers };
        await Assert.That(message.Headers).IsSameReferenceAs(headers);
    }

    #endregion

    #region Record Equality Tests

    [Test]
    public async Task Equality_SameProperties_AreEqual()
    {
        var msg1 = new ProducerMessage<string, string> { Topic = "topic", Key = "key", Value = "value" };
        var msg2 = new ProducerMessage<string, string> { Topic = "topic", Key = "key", Value = "value" };
        await Assert.That(msg1).IsEqualTo(msg2);
    }

    [Test]
    public async Task Equality_DifferentTopic_AreNotEqual()
    {
        var msg1 = new ProducerMessage<string, string> { Topic = "topic1", Key = "key", Value = "value" };
        var msg2 = new ProducerMessage<string, string> { Topic = "topic2", Key = "key", Value = "value" };
        await Assert.That(msg1).IsNotEqualTo(msg2);
    }

    [Test]
    public async Task Equality_DifferentKey_AreNotEqual()
    {
        var msg1 = new ProducerMessage<string, string> { Topic = "topic", Key = "key1", Value = "value" };
        var msg2 = new ProducerMessage<string, string> { Topic = "topic", Key = "key2", Value = "value" };
        await Assert.That(msg1).IsNotEqualTo(msg2);
    }

    #endregion

    #region Int Key Type Tests

    [Test]
    public async Task Create_WithIntKey_SetsProperties()
    {
        var message = ProducerMessage<int, string>.Create("topic", 42, "value");
        await Assert.That(message.Key).IsEqualTo(42);
        await Assert.That(message.Value).IsEqualTo("value");
    }

    #endregion
}
