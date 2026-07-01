using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Integration tests for the SASL/OAUTHBEARER authentication mechanism.
/// Verifies that producers, consumers, and admin clients can authenticate with a bearer
/// token, that messages round-trip over the authenticated connection, and that invalid
/// tokens are rejected by the broker with an <see cref="AuthenticationException"/>.
///
/// Uses a dedicated OAUTHBEARER-enabled Kafka container shared across the test session.
/// Tests are not run in parallel to avoid overwhelming the single broker.
/// </summary>
[Category("Authentication")]
[ClassDataSource<OAuthBearerKafkaContainer>(Shared = SharedType.PerTestSession)]
[NotInParallel("OAuthBearerKafka")]
public class OAuthBearerAuthenticationTests(OAuthBearerKafkaContainer oauthKafka)
{
    [Test]
    public async Task Producer_WithOAuthBearer_SuccessfullyProduces()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-producer")
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "oauth-key",
            Value = "oauth-value"
        }, CancellationToken.None);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Consumer_WithOAuthBearer_SuccessfullyConsumes()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();
        var groupId = $"oauth-consumer-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-producer-for-consumer")
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "oauth-consumer-key",
            Value = "oauth-consumer-value"
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-consumer")
            .WithGroupId(groupId)
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var record = result!.Value;
        await Assert.That(record.Topic).IsEqualTo(topic);
        await Assert.That(record.Key).IsEqualTo("oauth-consumer-key");
        await Assert.That(record.Value).IsEqualTo("oauth-consumer-value");
    }

    [Test]
    public async Task AdminClient_WithOAuthBearer_SuccessfullyListsTopics()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-admin-client")
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();

        var topics = await adminClient.ListTopicsAsync();

        await Assert.That(topics.Count).IsGreaterThan(0);
        await Assert.That(topics.Select(t => t.Name)).Contains(topic);
    }

    [Test]
    public async Task ProduceAndConsume_OverOAuthBearer_RoundTripsSuccessfully()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();
        var groupId = $"oauth-roundtrip-group-{Guid.NewGuid():N}";
        const string expectedKey = "oauth-roundtrip-key";
        const string expectedValue = "oauth-roundtrip-value";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-roundtrip-producer")
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var produceResult = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = expectedKey,
            Value = expectedValue
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-roundtrip-consumer")
            .WithGroupId(groupId)
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumeResult = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(produceResult.Topic).IsEqualTo(topic);
        await Assert.That(produceResult.Offset).IsGreaterThanOrEqualTo(0);

        await Assert.That(consumeResult).IsNotNull();
        var record = consumeResult!.Value;
        await Assert.That(record.Key).IsEqualTo(expectedKey);
        await Assert.That(record.Value).IsEqualTo(expectedValue);
    }

    [Test]
    public async Task Producer_WithInvalidToken_ThrowsAuthenticationException()
    {
        // The token is well-formed and not expired from the client's perspective (so it is
        // transmitted), but its JWS exp claim is in the past, so the broker rejects it.
        await Assert.ThrowsAsync<Dekaf.Errors.AuthenticationException>(async () =>
        {
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(oauthKafka.BootstrapServers)
                .WithClientId("oauth-invalid-producer")
                .WithOAuthBearer(_ => new ValueTask<OAuthBearerToken>(
                    OAuthBearerKafkaContainer.CreateServerRejectedToken(OAuthBearerKafkaContainer.Principal)))
                .WithAcks(Acks.All)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "any-topic",
                Key = "key",
                Value = "value"
            }, CancellationToken.None);
        });
    }
}
