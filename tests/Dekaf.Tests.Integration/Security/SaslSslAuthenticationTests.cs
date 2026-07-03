using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Integration tests for SASL authentication over a TLS-encrypted transport (SASL_SSL) —
/// i.e. "not plaintext". Verifies that every SASL mechanism (PLAIN, SCRAM-SHA-256,
/// SCRAM-SHA-512, OAUTHBEARER) can authenticate and round-trip messages over SSL.
///
/// Uses a dedicated SASL_SSL Kafka container shared across the session. Tests are not run in
/// parallel to avoid overwhelming the single broker.
/// </summary>
[Category("Authentication")]
[ClassDataSource<SaslSslKafkaContainer>(Shared = SharedType.PerTestSession)]
[NotInParallel("SaslSslKafka")]
public class SaslSslAuthenticationTests(SaslSslKafkaContainer saslSsl)
{
    [Test]
    public async Task Producer_WithSaslPlainOverSsl_SuccessfullyProduces()
    {
        var topic = await saslSsl.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslSsl.BootstrapServers)
            .WithClientId("saslssl-plain-producer")
            .WithTls(saslSsl.DefaultTlsConfig)
            .WithSaslPlain(SaslSslKafkaContainer.SaslUsername, SaslSslKafkaContainer.SaslPassword)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "k",
            Value = "v"
        }, CancellationToken.None);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithSaslScramSha256OverSsl_SuccessfullyProduces()
    {
        var topic = await saslSsl.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslSsl.BootstrapServers)
            .WithClientId("saslssl-scram256-producer")
            .WithTls(saslSsl.DefaultTlsConfig)
            .WithSaslScramSha256(SaslSslKafkaContainer.SaslUsername, SaslSslKafkaContainer.SaslPassword)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "k",
            Value = "v"
        }, CancellationToken.None);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithSaslScramSha512OverSsl_SuccessfullyProduces()
    {
        var topic = await saslSsl.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslSsl.BootstrapServers)
            .WithClientId("saslssl-scram512-producer")
            .WithTls(saslSsl.DefaultTlsConfig)
            .WithSaslScramSha512(SaslSslKafkaContainer.SaslUsername, SaslSslKafkaContainer.SaslPassword)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "k",
            Value = "v"
        }, CancellationToken.None);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithOAuthBearerOverSsl_SuccessfullyProduces()
    {
        var topic = await saslSsl.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslSsl.BootstrapServers)
            .WithClientId("saslssl-oauth-producer")
            .WithTls(saslSsl.DefaultTlsConfig)
            .WithOAuthBearer(SaslSslKafkaContainer.GetTokenAsync)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "k",
            Value = "v"
        }, CancellationToken.None);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task ProduceAndConsume_WithSaslPlainOverSsl_RoundTrips()
    {
        var topic = await saslSsl.CreateTestTopicAsync();
        var groupId = $"saslssl-roundtrip-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(saslSsl.BootstrapServers)
            .WithClientId("saslssl-roundtrip-producer")
            .WithTls(saslSsl.DefaultTlsConfig)
            .WithSaslPlain(SaslSslKafkaContainer.SaslUsername, SaslSslKafkaContainer.SaslPassword)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "rt-key",
            Value = "rt-value"
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(saslSsl.BootstrapServers)
            .WithClientId("saslssl-roundtrip-consumer")
            .WithGroupId(groupId)
            .WithTls(saslSsl.DefaultTlsConfig)
            .WithSaslScramSha512(SaslSslKafkaContainer.SaslUsername, SaslSslKafkaContainer.SaslPassword)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var record = result!.Value;
        await Assert.That(record.Key).IsEqualTo("rt-key");
        await Assert.That(record.Value).IsEqualTo("rt-value");
    }

    [Test]
    public async Task AdminClient_WithSaslScramSha256OverSsl_ListsTopics()
    {
        var topic = await saslSsl.CreateTestTopicAsync();

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(saslSsl.BootstrapServers)
            .WithClientId("saslssl-admin")
            .WithTls(saslSsl.DefaultTlsConfig)
            .WithSaslScramSha256(SaslSslKafkaContainer.SaslUsername, SaslSslKafkaContainer.SaslPassword)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();

        var topics = await adminClient.ListTopicsAsync();

        await Assert.That(topics.Select(t => t.Name)).Contains(topic);
    }
}
