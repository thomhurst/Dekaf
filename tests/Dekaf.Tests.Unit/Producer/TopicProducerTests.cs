using Dekaf.Producer;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

public class TopicProducerTests
{
    [Test]
    public async Task ForTopic_ReturnsTopicProducer_WithCorrectTopic()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        mockProducer.ForTopic("my-topic").Returns(callInfo =>
            new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false));

        // Act
        var topicProducer = mockProducer.ForTopic("my-topic");

        // Assert
        await Assert.That(topicProducer.Topic).IsEqualTo("my-topic");
    }

    [Test]
    public async Task Topic_ReturnsConfiguredTopic()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var topicProducer = new TopicProducer<string, string>(mockProducer, "test-topic", ownsProducer: false);

        // Assert
        await Assert.That(topicProducer.Topic).IsEqualTo("test-topic");
    }

    [Test]
    public async Task ProduceAsync_KeyValue_DelegatesToInnerProducer_WithTopic()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var expectedMetadata = new RecordMetadata
        {
            Topic = "my-topic",
            Partition = 0,
            Offset = 42,
            Timestamp = DateTimeOffset.UtcNow
        };
        mockProducer.ProduceAsync("my-topic", "key", "value", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(expectedMetadata));

        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);

        // Act
        var result = await topicProducer.ProduceAsync("key", "value");

        // Assert
        await Assert.That(result.Topic).IsEqualTo("my-topic");
        await Assert.That(result.Offset).IsEqualTo(42);
        await mockProducer.Received(1).ProduceAsync("my-topic", "key", "value", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProduceAsync_WithHeaders_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var expectedMetadata = new RecordMetadata
        {
            Topic = "my-topic",
            Partition = 0,
            Offset = 42,
            Timestamp = DateTimeOffset.UtcNow
        };
        mockProducer.ProduceAsync(Arg.Any<ProducerMessage<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(expectedMetadata));

        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        var headers = Headers.Create("header-key", "header-value");

        // Act
        var result = await topicProducer.ProduceAsync("key", "value", headers);

        // Assert
        await Assert.That(result.Offset).IsEqualTo(42);
        await mockProducer.Received(1).ProduceAsync(
            Arg.Is<ProducerMessage<string, string>>(m =>
                m.Topic == "my-topic" &&
                m.Key == "key" &&
                m.Value == "value" &&
                m.Headers != null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProduceAsync_WithPartition_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var expectedMetadata = new RecordMetadata
        {
            Topic = "my-topic",
            Partition = 2,
            Offset = 42,
            Timestamp = DateTimeOffset.UtcNow
        };
        mockProducer.ProduceAsync(Arg.Any<ProducerMessage<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(expectedMetadata));

        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);

        // Act
        var result = await topicProducer.ProduceAsync(partition: 2, "key", "value");

        // Assert
        await Assert.That(result.Partition).IsEqualTo(2);
        await mockProducer.Received(1).ProduceAsync(
            Arg.Is<ProducerMessage<string, string>>(m =>
                m.Topic == "my-topic" &&
                m.Partition == 2),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProduceAsync_WithTopicProducerMessage_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var expectedMetadata = new RecordMetadata
        {
            Topic = "my-topic",
            Partition = 1,
            Offset = 100,
            Timestamp = DateTimeOffset.UtcNow
        };
        mockProducer.ProduceAsync(Arg.Any<ProducerMessage<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(expectedMetadata));

        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        var timestamp = DateTimeOffset.UtcNow.AddHours(-1);
        var message = new TopicProducerMessage<string, string>
        {
            Key = "key",
            Value = "value",
            Partition = 1,
            Timestamp = timestamp
        };

        // Act
        var result = await topicProducer.ProduceAsync(message);

        // Assert
        await Assert.That(result.Offset).IsEqualTo(100);
        await mockProducer.Received(1).ProduceAsync(
            Arg.Is<ProducerMessage<string, string>>(m =>
                m.Topic == "my-topic" &&
                m.Partition == 1 &&
                m.Timestamp == timestamp),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Send_KeyValue_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);

        // Act
        topicProducer.Send("key", "value");

        // Assert
        mockProducer.Received(1).Send("my-topic", "key", "value");
        await Task.CompletedTask; // For async test assertion
    }

    [Test]
    public async Task Send_WithHeaders_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        var headers = Headers.Create("key", "value");

        // Act
        topicProducer.Send("key", "value", headers);

        // Assert
        mockProducer.Received(1).Send(
            Arg.Is<ProducerMessage<string, string>>(m =>
                m.Topic == "my-topic" &&
                m.Headers != null));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Send_WithCallback_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        Action<RecordMetadata, Exception?> callback = (_, _) => { };

        // Act
        topicProducer.Send("key", "value", callback);

        // Assert
        mockProducer.Received(1).Send(
            Arg.Is<ProducerMessage<string, string>>(m => m.Topic == "my-topic"),
            Arg.Any<Action<RecordMetadata, Exception?>>());
        await Task.CompletedTask;
    }

    [Test]
    public async Task ProduceAllAsync_Tuples_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var expectedResults = new[]
        {
            new RecordMetadata { Topic = "my-topic", Partition = 0, Offset = 0, Timestamp = DateTimeOffset.UtcNow },
            new RecordMetadata { Topic = "my-topic", Partition = 0, Offset = 1, Timestamp = DateTimeOffset.UtcNow }
        };
        mockProducer.ProduceAllAsync("my-topic", Arg.Any<IEnumerable<(string?, string)>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResults));

        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        var messages = new (string?, string)[] { ("key1", "value1"), ("key2", "value2") };

        // Act
        var results = await topicProducer.ProduceAllAsync(messages);

        // Assert
        await Assert.That(results).Count().IsEqualTo(2);
        await mockProducer.Received(1).ProduceAllAsync(
            "my-topic",
            Arg.Any<IEnumerable<(string?, string)>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProduceAllAsync_Messages_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var expectedResults = new[]
        {
            new RecordMetadata { Topic = "my-topic", Partition = 0, Offset = 0, Timestamp = DateTimeOffset.UtcNow },
            new RecordMetadata { Topic = "my-topic", Partition = 0, Offset = 1, Timestamp = DateTimeOffset.UtcNow }
        };
        mockProducer.ProduceAllAsync(Arg.Any<IEnumerable<ProducerMessage<string, string>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResults));

        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        var messages = new[]
        {
            new TopicProducerMessage<string, string> { Key = "key1", Value = "value1" },
            new TopicProducerMessage<string, string> { Key = "key2", Value = "value2" }
        };

        // Act
        var results = await topicProducer.ProduceAllAsync(messages);

        // Assert
        await Assert.That(results).Count().IsEqualTo(2);
        await mockProducer.Received(1).ProduceAllAsync(
            Arg.Is<IEnumerable<ProducerMessage<string, string>>>(msgs =>
                msgs.All(m => m.Topic == "my-topic")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FlushAsync_DelegatesToInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        mockProducer.FlushAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);

        // Act
        await topicProducer.FlushAsync();

        // Assert
        await mockProducer.Received(1).FlushAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispose_WhenOwnsProducer_DisposesInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        mockProducer.DisposeAsync().Returns(ValueTask.CompletedTask);
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: true);

        // Act
        await topicProducer.DisposeAsync();

        // Assert
        await mockProducer.Received(1).DisposeAsync();
    }

    [Test]
    public async Task Dispose_WhenNotOwnsProducer_DoesNotDisposeInnerProducer()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);

        // Act
        await topicProducer.DisposeAsync();

        // Assert
        await mockProducer.DidNotReceive().DisposeAsync();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_OnlyDisposesOnce()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        mockProducer.DisposeAsync().Returns(ValueTask.CompletedTask);
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: true);

        // Act
        await topicProducer.DisposeAsync();
        await topicProducer.DisposeAsync();
        await topicProducer.DisposeAsync();

        // Assert
        await mockProducer.Received(1).DisposeAsync();
    }

    [Test]
    public async Task ProduceAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        await topicProducer.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await topicProducer.ProduceAsync("key", "value"));
    }

    [Test]
    public async Task Send_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        await topicProducer.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            topicProducer.Send("key", "value");
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task FlushAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();
        var topicProducer = new TopicProducer<string, string>(mockProducer, "my-topic", ownsProducer: false);
        await topicProducer.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await topicProducer.FlushAsync());
    }

    [Test]
    public async Task Constructor_NullProducer_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
        {
            _ = new TopicProducer<string, string>(null!, "topic", ownsProducer: false);
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task Constructor_NullTopic_ThrowsArgumentNullException()
    {
        // Arrange
        var mockProducer = Substitute.For<IKafkaProducer<string, string>>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
        {
            _ = new TopicProducer<string, string>(mockProducer, null!, ownsProducer: false);
            return Task.CompletedTask;
        });
    }
}

public class TopicProducerMessageTests
{
    [Test]
    public async Task Create_KeyValue_SetsProperties()
    {
        // Act
        var message = TopicProducerMessage<string, string>.Create("key", "value");

        // Assert
        await Assert.That(message.Key).IsEqualTo("key");
        await Assert.That(message.Value).IsEqualTo("value");
        await Assert.That(message.Headers).IsNull();
        await Assert.That(message.Partition).IsNull();
        await Assert.That(message.Timestamp).IsNull();
    }

    [Test]
    public async Task Create_WithHeaders_SetsProperties()
    {
        // Arrange
        var headers = Headers.Create("h1", "v1");

        // Act
        var message = TopicProducerMessage<string, string>.Create("key", "value", headers);

        // Assert
        await Assert.That(message.Key).IsEqualTo("key");
        await Assert.That(message.Value).IsEqualTo("value");
        await Assert.That(message.Headers).IsNotNull();
    }

    [Test]
    public async Task Create_WithPartition_SetsProperties()
    {
        // Act
        var message = TopicProducerMessage<string, string>.Create(partition: 5, "key", "value");

        // Assert
        await Assert.That(message.Key).IsEqualTo("key");
        await Assert.That(message.Value).IsEqualTo("value");
        await Assert.That(message.Partition).IsEqualTo(5);
    }

    [Test]
    public async Task InitSyntax_AllProperties_SetsCorrectly()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var headers = Headers.Create("h", "v");

        // Act
        var message = new TopicProducerMessage<string, string>
        {
            Key = "key",
            Value = "value",
            Headers = headers,
            Partition = 3,
            Timestamp = timestamp
        };

        // Assert
        await Assert.That(message.Key).IsEqualTo("key");
        await Assert.That(message.Value).IsEqualTo("value");
        await Assert.That(message.Headers).IsSameReferenceAs(headers);
        await Assert.That(message.Partition).IsEqualTo(3);
        await Assert.That(message.Timestamp).IsEqualTo(timestamp);
    }
}
