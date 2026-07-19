using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Integration.Security;

[Category("Authentication")]
[ClassDataSource<SaslKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class SaslCredentialRotationTests(SaslKafkaContainer kafka)
{
    [Test]
    public async Task Producer_ReconnectWithRevokedCredential_ThrowsAuthenticationExceptionWithoutRetryStorm()
    {
        var userA = $"rotation-a-{Guid.NewGuid():N}";
        var passwordA = $"password-a-{Guid.NewGuid():N}";
        var userB = $"rotation-b-{Guid.NewGuid():N}";
        var passwordB = $"password-b-{Guid.NewGuid():N}";
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await kafka.UpsertScramSha256CredentialAsync(userA, passwordA).ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslScramSha256(userA, passwordA)
            .WithConnectionTimeout(TimeSpan.FromSeconds(5))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(10))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);

        await ProduceAsync(producer, topic, "before-rotation").ConfigureAwait(false);

        await kafka.UpsertScramSha256CredentialAsync(userB, passwordB).ConfigureAwait(false);
        await kafka.DeleteScramSha256CredentialAsync(userA, passwordA).ConfigureAwait(false);

        // Revocation does not terminate an already-authenticated broker connection.
        await ProduceAsync(producer, topic, "existing-connection").ConfigureAwait(false);

        await ((KafkaProducer<string, string>)producer).CloseConnectionsForTestingAsync().ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<AuthenticationException>(async () =>
            await ProduceAsync(producer, topic, "after-reconnect").ConfigureAwait(false));

        await Assert.That(exception!.IsRetriable).IsFalse();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Producer_CredentialProvider_UsesRotatedCredentialAfterReconnect()
    {
        var userA = $"provider-a-{Guid.NewGuid():N}";
        var passwordA = $"password-a-{Guid.NewGuid():N}";
        var userB = $"provider-b-{Guid.NewGuid():N}";
        var passwordB = $"password-b-{Guid.NewGuid():N}";
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var current = new CredentialState(new SaslCredentials(userA, passwordA));

        await kafka.UpsertScramSha256CredentialAsync(userA, passwordA).ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslScramSha256(current.GetAsync)
            .WithConnectionTimeout(TimeSpan.FromSeconds(5))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(15))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);

        await ProduceAsync(producer, topic, "before-rotation").ConfigureAwait(false);

        await kafka.UpsertScramSha256CredentialAsync(userB, passwordB).ConfigureAwait(false);
        await kafka.DeleteScramSha256CredentialAsync(userA, passwordA).ConfigureAwait(false);
        current.Value = new SaslCredentials(userB, passwordB);

        await ((KafkaProducer<string, string>)producer).CloseConnectionsForTestingAsync().ConfigureAwait(false);
        await ProduceAsync(producer, topic, "after-reconnect").ConfigureAwait(false);

        await Assert.That(current.InvocationCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Consumer_CredentialProvider_UsesRotatedCredentialAfterReconnect()
    {
        var userA = $"consumer-a-{Guid.NewGuid():N}";
        var passwordA = $"password-a-{Guid.NewGuid():N}";
        var userB = $"consumer-b-{Guid.NewGuid():N}";
        var passwordB = $"password-b-{Guid.NewGuid():N}";
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var current = new CredentialState(new SaslCredentials(userA, passwordA));

        await kafka.UpsertScramSha256CredentialAsync(userA, passwordA).ConfigureAwait(false);
        await ProduceWithPlainAsync(topic, "before-rotation").ConfigureAwait(false);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"rotation-group-{Guid.NewGuid():N}")
            .WithSaslScramSha256(current.GetAsync)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithConnectionTimeout(TimeSpan.FromSeconds(5))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);

        consumer.Subscribe(topic);
        await ConsumeValueAsync(consumer, "before-rotation").ConfigureAwait(false);

        await kafka.UpsertScramSha256CredentialAsync(userB, passwordB).ConfigureAwait(false);
        await kafka.DeleteScramSha256CredentialAsync(userA, passwordA).ConfigureAwait(false);
        await ProduceWithPlainAsync(topic, "existing-connection").ConfigureAwait(false);
        await ConsumeValueAsync(consumer, "existing-connection").ConfigureAwait(false);

        current.Value = new SaslCredentials(userB, passwordB);
        await ((KafkaConsumer<string, string>)consumer).CloseConnectionsForTestingAsync().ConfigureAwait(false);
        await ProduceWithPlainAsync(topic, "after-reconnect").ConfigureAwait(false);
        await ConsumeValueAsync(consumer, "after-reconnect").ConfigureAwait(false);

        await Assert.That(current.InvocationCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Consumer_ReconnectWithRevokedCredential_ThrowsAuthenticationException()
    {
        var user = $"revoked-consumer-{Guid.NewGuid():N}";
        var password = $"password-{Guid.NewGuid():N}";
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await kafka.UpsertScramSha256CredentialAsync(user, password).ConfigureAwait(false);
        await ProduceWithPlainAsync(topic, "before-revocation").ConfigureAwait(false);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"revoked-group-{Guid.NewGuid():N}")
            .WithSaslScramSha256(user, password)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithConnectionTimeout(TimeSpan.FromSeconds(5))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);

        consumer.Subscribe(topic);
        await ConsumeValueAsync(consumer, "before-revocation").ConfigureAwait(false);

        await kafka.DeleteScramSha256CredentialAsync(user, password).ConfigureAwait(false);
        await ProduceWithPlainAsync(topic, "existing-connection").ConfigureAwait(false);
        await ConsumeValueAsync(consumer, "existing-connection").ConfigureAwait(false);

        await ((KafkaConsumer<string, string>)consumer).CloseConnectionsForTestingAsync().ConfigureAwait(false);
        await ProduceWithPlainAsync(topic, "after-reconnect").ConfigureAwait(false);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(async () =>
            await ConsumeValueAsync(consumer, "after-reconnect").ConfigureAwait(false));
        await Assert.That(exception!.IsRetriable).IsFalse();
    }

    private static async Task ProduceAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        string value)
    {
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = value,
            Value = value
        }, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ProduceWithPlainAsync(string topic, string value)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        await ProduceAsync(producer, topic, value).ConfigureAwait(false);
    }

    private static async Task ConsumeValueAsync(IKafkaConsumer<string, string> consumer, string expected)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo(expected);
    }

    private sealed class CredentialState(SaslCredentials value)
    {
        private int _invocationCount;

        public SaslCredentials Value { get; set; } = value;
        public int InvocationCount => Volatile.Read(ref _invocationCount);

        public ValueTask<SaslCredentials> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _invocationCount);
            return ValueTask.FromResult(Value);
        }
    }
}
