using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Security;

namespace Dekaf.Tests.Integration.Security;

public sealed class TlsCertificateRotationKafkaContainer : TlsKafkaContainer;

[Category("Tls")]
[NotInParallel("TlsCertificateRotationKafkaContainer")]
[ClassDataSource<TlsCertificateRotationKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class TlsCertificateRotationTests(TlsCertificateRotationKafkaContainer kafka)
{
    [Test]
    public async Task BrokerCertificateRotation_EnforcesAndRefreshesTrustWithoutMessageLoss()
    {
        using var baselineCertificate = kafka.CertificateGenerator.GenerateServerCertificate();
        await kafka.RestartWithServerCertificateAsync(baselineCertificate).ConfigureAwait(false);

        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var staticTlsConfig = CreateTlsConfig(kafka.CaCertificate);

        await using var staticTrustProducer = await CreateProducerBuilder("tls-static-trust", staticTlsConfig)
            .BuildAsync().ConfigureAwait(false);
        await ProduceAsync(staticTrustProducer, topic, "before-rotation").ConfigureAwait(false);

        using var sameCaCertificate = kafka.CertificateGenerator.GenerateServerCertificate();
        await kafka.RestartWithServerCertificateAsync(sameCaCertificate).ConfigureAwait(false);
        await ProduceAsync(staticTrustProducer, topic, "after-same-ca-rotation").ConfigureAwait(false);

        var reloadableTrust = new ReloadableCertificateTrust(kafka.CaCertificate);
        await using var reloadableTrustProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("tls-reloadable-trust")
            .UseTls(new TlsConfig { TargetHost = "localhost" })
            .WithRemoteCertificateValidationCallback(reloadableTrust.Validate)
            .WithConnectionTimeout(TimeSpan.FromSeconds(5))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(10))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        await ProduceAsync(reloadableTrustProducer, topic, "before-ca-rotation").ConfigureAwait(false);

        using var rotatedAuthority = new TestCertificateGenerator();
        using var newCaCertificate = rotatedAuthority.GenerateServerCertificate();
        await kafka.RestartWithServerCertificateAsync(newCaCertificate).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<AuthenticationException>(async () =>
            await ProduceAsync(staticTrustProducer, topic, "untrusted-ca").ConfigureAwait(false));

        await Assert.That(exception!.IsRetriable).IsFalse();
        await Assert.That(exception.Message).Contains("TLS handshake failed");
        await Assert.That(exception.Message).Contains("Certificate validation:");
        await Assert.That(exception.InnerException).IsNotNull();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(15));

        reloadableTrust.TrustedRoot = rotatedAuthority.CaCertificate;
        await ProduceAsync(reloadableTrustProducer, topic, "after-trust-refresh").ConfigureAwait(false);

        var values = await ConsumeValuesAsync(
            topic,
            reloadableTrust,
            expectedCount: 4).ConfigureAwait(false);

        await Assert.That(values).IsEquivalentTo([
            "before-rotation",
            "after-same-ca-rotation",
            "before-ca-rotation",
            "after-trust-refresh"
        ]);
        await Assert.That(values).Count().IsEqualTo(values.Distinct().Count());
    }

    [Test]
    public async Task ExpiredBrokerCertificate_ExistingConnectionSurvivesButReconnectFailsCleanly()
    {
        using var baselineCertificate = kafka.CertificateGenerator.GenerateServerCertificate();
        await kafka.RestartWithServerCertificateAsync(baselineCertificate).ConfigureAwait(false);

        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var notAfter = DateTimeOffset.UtcNow.AddSeconds(45);
        using var expiringCertificate = kafka.CertificateGenerator.GenerateServerCertificate(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            notAfter);
        await kafka.RestartWithServerCertificateAsync(expiringCertificate).ConfigureAwait(false);

        await using var producer = await CreateProducerBuilder(
                "tls-expiring-certificate",
                CreateTlsConfig(kafka.CaCertificate))
            .BuildAsync().ConfigureAwait(false);
        await ProduceAsync(producer, topic, "before-expiry").ConfigureAwait(false);

        var expiryDelay = expiringCertificate.NotAfter.ToUniversalTime() - DateTime.UtcNow + TimeSpan.FromSeconds(2);
        if (expiryDelay > TimeSpan.Zero)
        {
            await Task.Delay(expiryDelay).ConfigureAwait(false);
        }

        await ProduceAsync(producer, topic, "existing-connection-after-expiry").ConfigureAwait(false);
        await ((KafkaProducer<string, string>)producer).CloseConnectionsForTestingAsync().ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<AuthenticationException>(async () =>
            await ProduceAsync(producer, topic, "reconnect-after-expiry").ConfigureAwait(false));

        await Assert.That(exception!.IsRetriable).IsFalse();
        await Assert.That(exception.Message).Contains("TLS handshake failed");
        await Assert.That(exception.Message).Contains("NotTimeValid");
        await Assert.That(exception.InnerException).IsNotNull();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(15));
    }

    private ProducerBuilder<string, string> CreateProducerBuilder(string clientId, TlsConfig tlsConfig)
        => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId(clientId)
            .UseTls(tlsConfig)
            .WithConnectionTimeout(TimeSpan.FromSeconds(5))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(10))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());

    private async Task<List<string>> ConsumeValuesAsync(
        string topic,
        ReloadableCertificateTrust trust,
        int expectedCount)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("tls-rotation-consumer")
            .WithGroupId($"tls-rotation-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseTls(new TlsConfig { TargetHost = "localhost" })
            .WithRemoteCertificateValidationCallback(trust.Validate)
            .WithConnectionTimeout(TimeSpan.FromSeconds(5))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var values = new List<string>(expectedCount);
        while (values.Count < expectedCount)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
            await Assert.That(result).IsNotNull();
            values.Add(result!.Value.Value);
        }

        return values;
    }

    private static TlsConfig CreateTlsConfig(X509Certificate2 caCertificate) => new()
    {
        CaCertificateObject = caCertificate,
        ValidateServerCertificate = true,
        TargetHost = "localhost"
    };

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

    private sealed class ReloadableCertificateTrust(X509Certificate2 trustedRoot)
    {
        public X509Certificate2 TrustedRoot { get; set; } = trustedRoot;

        public bool Validate(
            object sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (certificate is null ||
                (sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
            {
                return false;
            }

            using var serverCertificate = LoadCertificate(certificate.Export(X509ContentType.Cert));
            using var validationChain = new X509Chain();
            validationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            validationChain.ChainPolicy.CustomTrustStore.Add(TrustedRoot);
            validationChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return validationChain.Build(serverCertificate);
        }

        private static X509Certificate2 LoadCertificate(byte[] rawData)
        {
#if NET10_0_OR_GREATER
            return X509CertificateLoader.LoadCertificate(rawData);
#else
            return new X509Certificate2(rawData);
#endif
        }
    }
}
