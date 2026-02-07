using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for SASL authentication mechanisms.
/// </summary>
[ClassDataSource<KafkaWithSaslContainer>(Shared = SharedType.PerTestSession)]
public sealed class SaslAuthenticationTests(KafkaWithSaslContainer kafka)
{
    [Test]
    public async Task SaslPlain_RoundTrip_ProduceAndConsumeSucceeds()
    {
        var topic = $"sasl-roundtrip-{Guid.NewGuid():N}";

        // Create topic via admin with SASL auth
        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(KafkaWithSaslContainer.AdminUsername, KafkaWithSaslContainer.AdminPassword)
            .Build();

        await adminClient.CreateTopicsAsync([
            new NewTopic
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        ]);

        await Task.Delay(2000).ConfigureAwait(false);

        // Produce with SASL auth
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(KafkaWithSaslContainer.SaslUsername, KafkaWithSaslContainer.SaslPassword)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "sasl-key",
            Value = "sasl-value"
        });

        // Consume with SASL auth
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"sasl-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSaslPlain(KafkaWithSaslContainer.SaslUsername, KafkaWithSaslContainer.SaslPassword)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("sasl-key");
        await Assert.That(result.Value.Value).IsEqualTo("sasl-value");
    }

    [Test]
    public async Task SaslPlain_WrongCredentials_ThrowsAuthenticationException()
    {
        await Assert.That(async () =>
        {
            await using var producer = Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithSaslPlain("wrong-user", "wrong-password")
                .Build();

            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "any-topic",
                Key = "key",
                Value = "value"
            });
        }).Throws<Exception>();
    }

    [Test]
    public async Task SaslScramSha256_RoundTrip_ProduceAndConsumeSucceeds()
    {
        var topic = $"sasl-scram-{Guid.NewGuid():N}";
        var scramUser = $"scram-user-{Guid.NewGuid():N}";
        var scramPassword = "scram-password-123";

        // Create SCRAM credentials via admin
        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(KafkaWithSaslContainer.AdminUsername, KafkaWithSaslContainer.AdminPassword)
            .Build();

        await adminClient.AlterUserScramCredentialsAsync([
            new UserScramCredentialUpsertion
            {
                User = scramUser,
                Mechanism = ScramMechanism.ScramSha256,
                Iterations = 4096,
                Password = scramPassword
            }
        ]);

        await adminClient.CreateTopicsAsync([
            new NewTopic
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        ]);

        await Task.Delay(2000).ConfigureAwait(false);

        // Produce with SCRAM auth
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslScramSha256(scramUser, scramPassword)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "scram-key",
            Value = "scram-value"
        });

        // Consume with SCRAM auth
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"scram-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSaslScramSha256(scramUser, scramPassword)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("scram-key");
        await Assert.That(result.Value.Value).IsEqualTo("scram-value");
    }
}
