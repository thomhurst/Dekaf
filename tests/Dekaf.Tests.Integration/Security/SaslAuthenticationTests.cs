using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Integration tests for SASL authentication mechanisms (PLAIN, SCRAM-SHA-256, SCRAM-SHA-512).
/// Verifies that producers, consumers, and admin clients can authenticate using each mechanism,
/// and that invalid credentials are properly rejected with AuthenticationException.
///
/// Uses a dedicated SASL-enabled Kafka container shared across all tests in the session.
/// Tests are not run in parallel to avoid overwhelming the single SASL broker.
/// </summary>
[Category("Security")]
[ClassDataSource<SaslKafkaContainer>(Shared = SharedType.PerTestSession)]
[NotInParallel("SaslKafka")]
public class SaslAuthenticationTests(SaslKafkaContainer saslKafka)
{
    // ==========================================
    // SASL/PLAIN Tests
    // ==========================================

    [Test]
    public async Task Producer_WithSaslPlain_SuccessfullyProduces()
    {
        // Arrange
        var topic = await saslKafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-plain-producer")
            .WithSaslPlain(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "plain-key",
            Value = "plain-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Consumer_WithSaslPlain_SuccessfullyConsumes()
    {
        // Arrange
        var topic = await saslKafka.CreateTestTopicAsync();
        var groupId = $"sasl-plain-consumer-group-{Guid.NewGuid():N}";

        // Produce a message first
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-plain-producer-for-consumer")
            .WithSaslPlain(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "plain-consumer-key",
            Value = "plain-consumer-value"
        });

        // Act - consume with SASL/PLAIN
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-plain-consumer")
            .WithGroupId(groupId)
            .WithSaslPlain(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Topic).IsEqualTo(topic);
        await Assert.That(r.Key).IsEqualTo("plain-consumer-key");
        await Assert.That(r.Value).IsEqualTo("plain-consumer-value");
    }

    // ==========================================
    // SASL/SCRAM-SHA-256 Tests
    // ==========================================

    [Test]
    public async Task Producer_WithSaslScramSha256_SuccessfullyProduces()
    {
        // Arrange
        var topic = await saslKafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-scram256-producer")
            .WithSaslScramSha256(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "scram256-key",
            Value = "scram256-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Consumer_WithSaslScramSha256_SuccessfullyConsumes()
    {
        // Arrange
        var topic = await saslKafka.CreateTestTopicAsync();
        var groupId = $"sasl-scram256-consumer-group-{Guid.NewGuid():N}";

        // Produce a message first
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-scram256-producer-for-consumer")
            .WithSaslScramSha256(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "scram256-consumer-key",
            Value = "scram256-consumer-value"
        });

        // Act - consume with SCRAM-SHA-256
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-scram256-consumer")
            .WithGroupId(groupId)
            .WithSaslScramSha256(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Topic).IsEqualTo(topic);
        await Assert.That(r.Key).IsEqualTo("scram256-consumer-key");
        await Assert.That(r.Value).IsEqualTo("scram256-consumer-value");
    }

    // ==========================================
    // SASL/SCRAM-SHA-512 Tests
    // ==========================================

    [Test]
    public async Task Producer_WithSaslScramSha512_SuccessfullyProduces()
    {
        // Arrange
        var topic = await saslKafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-scram512-producer")
            .WithSaslScramSha512(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "scram512-key",
            Value = "scram512-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Consumer_WithSaslScramSha512_SuccessfullyConsumes()
    {
        // Arrange
        var topic = await saslKafka.CreateTestTopicAsync();
        var groupId = $"sasl-scram512-consumer-group-{Guid.NewGuid():N}";

        // Produce a message first
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-scram512-producer-for-consumer")
            .WithSaslScramSha512(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "scram512-consumer-key",
            Value = "scram512-consumer-value"
        });

        // Act - consume with SCRAM-SHA-512
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-scram512-consumer")
            .WithGroupId(groupId)
            .WithSaslScramSha512(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Topic).IsEqualTo(topic);
        await Assert.That(r.Key).IsEqualTo("scram512-consumer-key");
        await Assert.That(r.Value).IsEqualTo("scram512-consumer-value");
    }

    // ==========================================
    // Invalid Credentials Tests
    // ==========================================

    [Test]
    public async Task Producer_WithInvalidSaslCredentials_ThrowsAuthenticationException()
    {
        // Arrange & Act & Assert
        // The authentication failure should happen during BuildAsync (connection establishment)
        // or during the first ProduceAsync call when the connection is established lazily.
        var exception = await Assert.ThrowsAsync<AuthenticationException>(async () =>
        {
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(saslKafka.BootstrapServers)
                .WithClientId("sasl-invalid-producer")
                .WithSaslPlain("wronguser", "wrongpassword")
                .WithAcks(Acks.All)
                .BuildAsync();

            // If BuildAsync doesn't fail (lazy connection), the produce call should fail
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "any-topic",
                Key = "key",
                Value = "value"
            });
        });

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Consumer_WithInvalidSaslCredentials_ThrowsAuthenticationException()
    {
        // Arrange & Act & Assert
        // The authentication failure should happen during BuildAsync (connection establishment)
        // or when the consumer tries to join the group / fetch metadata.
        var exception = await Assert.ThrowsAsync<AuthenticationException>(async () =>
        {
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(saslKafka.BootstrapServers)
                .WithClientId("sasl-invalid-consumer")
                .WithGroupId($"sasl-invalid-group-{Guid.NewGuid():N}")
                .WithSaslPlain("wronguser", "wrongpassword")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync();

            consumer.Subscribe("any-topic");

            // If BuildAsync doesn't fail (lazy connection), consuming should fail
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await foreach (var _ in consumer.ConsumeAsync(cts.Token))
            {
                break; // Should not reach here
            }
        });

        await Assert.That(exception).IsNotNull();
    }

    // ==========================================
    // Admin Client Tests
    // ==========================================

    [Test]
    public async Task AdminClient_WithSaslPlain_SuccessfullyListsTopics()
    {
        // Arrange - create a topic first so we have something to list
        var topic = await saslKafka.CreateTestTopicAsync();

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-admin-client")
            .WithSaslPlain(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .Build();

        // Act
        var topics = await adminClient.ListTopicsAsync();

        // Assert
        await Assert.That(topics.Count).IsGreaterThan(0);
        await Assert.That(topics.Select(t => t.Name)).Contains(topic);
    }

    [Test]
    public async Task AdminClient_WithSaslScramSha256_SuccessfullyCreatesTopic()
    {
        // Arrange
        var topicName = $"sasl-admin-scram256-{Guid.NewGuid():N}";

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-admin-scram256")
            .WithSaslScramSha256(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .Build();

        // Act
        await adminClient.CreateTopicsAsync([
            new Admin.NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        ]);

        // Assert - verify by listing topics
        // Allow time for metadata propagation
        await Task.Delay(2000);
        var topics = await adminClient.ListTopicsAsync();
        await Assert.That(topics.Select(t => t.Name)).Contains(topicName);
    }

    [Test]
    public async Task AdminClient_WithSaslScramSha512_SuccessfullyCreatesTopic()
    {
        // Arrange
        var topicName = $"sasl-admin-scram512-{Guid.NewGuid():N}";

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-admin-scram512")
            .WithSaslScramSha512(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .Build();

        // Act
        await adminClient.CreateTopicsAsync([
            new Admin.NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        ]);

        // Assert - verify by listing topics
        await Task.Delay(2000);
        var topics = await adminClient.ListTopicsAsync();
        await Assert.That(topics.Select(t => t.Name)).Contains(topicName);
    }

    // ==========================================
    // Cross-Mechanism Round-Trip Tests
    // ==========================================

    [Test]
    public async Task ProduceWithPlain_ConsumeWithScramSha256_RoundTripSucceeds()
    {
        // Verifies that messages produced with one SASL mechanism can be consumed with another.
        // Authentication is per-connection, not per-message, so this should work.
        var topic = await saslKafka.CreateTestTopicAsync();
        var groupId = $"sasl-cross-mech-group-{Guid.NewGuid():N}";

        // Produce with PLAIN
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-cross-producer")
            .WithSaslPlain(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "cross-key",
            Value = "cross-value"
        });

        // Consume with SCRAM-SHA-256
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(saslKafka.BootstrapServers)
            .WithClientId("sasl-cross-consumer")
            .WithGroupId(groupId)
            .WithSaslScramSha256(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Key).IsEqualTo("cross-key");
        await Assert.That(r.Value).IsEqualTo("cross-value");
    }
}
