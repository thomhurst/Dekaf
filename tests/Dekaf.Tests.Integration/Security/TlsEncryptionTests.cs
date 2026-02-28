using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Security;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Integration tests for SSL/TLS encrypted connections.
/// Verifies that the Dekaf client can produce, consume, and administer
/// topics over SSL/TLS connections with various certificate configurations.
///
/// These tests use a dedicated TLS-enabled Kafka container with
/// auto-generated test certificates (CA, server, client).
/// </summary>
[Category("Security")]
[NotInParallel("TlsKafka")]
[ClassDataSource<TlsKafkaContainer>(Shared = SharedType.PerTestSession)]
public class TlsEncryptionTests(TlsKafkaContainer tlsKafka)
{
    // ──────────────────────────────────────────────────────────────
    // Producer with SSL/TLS connection succeeds
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Producer_WithTlsConnection_SuccessfullyProducesMessage()
    {
        // Arrange
        var topic = await tlsKafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-producer")
            .UseTls(tlsKafka.DefaultTlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "tls-key",
            Value = "tls-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithTlsMultipleMessages_AllDelivered()
    {
        // Arrange
        var topic = await tlsKafka.CreateTestTopicAsync();
        const int messageCount = 10;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-producer-multi")
            .UseTls(tlsKafka.DefaultTlsConfig)
            .BuildAsync();

        // Act
        var tasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in tasks)
        {
            results.Add(await task);
        }

        // Assert
        await Assert.That(results).Count().IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Consumer with SSL/TLS connection succeeds
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Consumer_WithTlsConnection_SuccessfullyConsumesMessages()
    {
        // Arrange
        var topic = await tlsKafka.CreateTestTopicAsync();
        var groupId = $"test-tls-group-{Guid.NewGuid():N}";

        // Produce a message first over TLS
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-consumer-producer")
            .UseTls(tlsKafka.DefaultTlsConfig)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "consume-tls-key",
            Value = "consume-tls-value"
        });

        // Act - consume over TLS
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseTls(tlsKafka.DefaultTlsConfig)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var record = result!.Value;
        await Assert.That(record.Topic).IsEqualTo(topic);
        await Assert.That(record.Key).IsEqualTo("consume-tls-key");
        await Assert.That(record.Value).IsEqualTo("consume-tls-value");
    }

    // ──────────────────────────────────────────────────────────────
    // Admin client with SSL/TLS connection succeeds
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task AdminClient_WithTlsConnection_CanCreateAndListTopics()
    {
        // Arrange
        var topicName = $"test-tls-admin-{Guid.NewGuid():N}";

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-admin")
            .UseTls(tlsKafka.DefaultTlsConfig)
            .Build();

        // Act - create topic
        await adminClient.CreateTopicsAsync([
            new Dekaf.Admin.NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        ]);

        // Wait for topic metadata propagation
        await TlsKafkaContainer.WaitForTopicAsync(adminClient, topicName);

        // Act - describe cluster
        var cluster = await adminClient.DescribeClusterAsync();

        // Assert
        await Assert.That(cluster.Nodes).Count().IsGreaterThan(0);
        await Assert.That(cluster.ClusterId).IsNotNull();
    }

    // ──────────────────────────────────────────────────────────────
    // Connection with invalid/expired certificate fails gracefully
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Producer_WithUntrustedCaCertificate_ThrowsOnConnection()
    {
        // Arrange - use an untrusted CA certificate that did NOT sign the server cert
        using var untrustedCert = TestCertificateGenerator.GenerateUntrustedCertificate();

        var tlsConfig = new TlsConfig
        {
            CaCertificateObject = untrustedCert,
            ValidateServerCertificate = true,
            TargetHost = "localhost"
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-untrusted")
            .UseTls(tlsConfig)
            .BuildAsync();

        // Act & Assert - should fail due to certificate validation
        var act = async () => await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "does-not-matter",
            Key = "key",
            Value = "value"
        });

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task Producer_WithServerValidationEnabled_AndNoCaCert_FailsValidation()
    {
        // Arrange - enable validation but do not provide the CA cert
        // The server's self-signed test cert will fail standard validation
        var tlsConfig = new TlsConfig
        {
            ValidateServerCertificate = true,
            TargetHost = "localhost"
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-no-ca")
            .UseTls(tlsConfig)
            .BuildAsync();

        // Act & Assert - should fail because server cert is signed by our test CA,
        // not a system-trusted CA
        var act = async () => await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "does-not-matter",
            Key = "key",
            Value = "value"
        });

        await Assert.That(act).ThrowsException();
    }

    // ──────────────────────────────────────────────────────────────
    // Connection with self-signed certificate (with trust store) succeeds
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Producer_WithSelfSignedCertAndCustomTrustStore_Succeeds()
    {
        // Arrange - our test CA is self-signed, providing it as the trust anchor should work
        var topic = await tlsKafka.CreateTestTopicAsync();

        // Use CA certificate collection as trust store
        var caCertCollection = new X509Certificate2Collection
        {
            tlsKafka.CaCertificate
        };

        var tlsConfig = new TlsConfig
        {
            CaCertificateCollection = caCertCollection,
            ValidateServerCertificate = true,
            TargetHost = "localhost"
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-self-signed-trust")
            .UseTls(tlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "trust-key",
            Value = "trust-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithSelfSignedCertViaCaCertPath_Succeeds()
    {
        // Arrange - use CA certificate PEM file path as trust store
        var topic = await tlsKafka.CreateTestTopicAsync();

        var tlsConfig = new TlsConfig
        {
            CaCertificatePath = tlsKafka.CaCertPemPath,
            ValidateServerCertificate = true,
            TargetHost = "localhost"
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-ca-path")
            .UseTls(tlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "ca-path-key",
            Value = "ca-path-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithValidationDisabled_SucceedsWithoutCaCert()
    {
        // Arrange - disable certificate validation entirely
        var topic = await tlsKafka.CreateTestTopicAsync();

        var tlsConfig = new TlsConfig
        {
            ValidateServerCertificate = false
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-no-validation")
            .UseTls(tlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "no-val-key",
            Value = "no-val-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    // ──────────────────────────────────────────────────────────────
    // Mutual TLS (mTLS) authentication succeeds
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Producer_WithMutualTlsInMemoryCerts_SuccessfullyProduces()
    {
        // Arrange - use mTLS with in-memory certificates
        var topic = await tlsKafka.CreateTestTopicAsync();

        var tlsConfig = new TlsConfig
        {
            ClientCertificate = tlsKafka.ClientCertificate,
            CaCertificateObject = tlsKafka.CaCertificate,
            ValidateServerCertificate = true,
            TargetHost = "localhost"
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-mtls-producer-inmemory")
            .UseTls(tlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "mtls-key",
            Value = "mtls-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithMutualTlsFilePaths_SuccessfullyProduces()
    {
        // Arrange - use mTLS with PEM file paths
        var topic = await tlsKafka.CreateTestTopicAsync();

        var tlsConfig = new TlsConfig
        {
            CaCertificatePath = tlsKafka.CaCertPemPath,
            ClientCertificatePath = tlsKafka.ClientCertPemPath,
            ClientKeyPath = tlsKafka.ClientKeyPemPath,
            ValidateServerCertificate = true,
            TargetHost = "localhost"
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-mtls-producer-files")
            .UseTls(tlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "mtls-file-key",
            Value = "mtls-file-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithMutualTlsFactoryMethod_SuccessfullyProduces()
    {
        // Arrange - use the builder's UseMutualTls convenience method
        var topic = await tlsKafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-mtls-producer-factory")
            .UseMutualTls(
                tlsKafka.CaCertPemPath,
                tlsKafka.ClientCertPemPath,
                tlsKafka.ClientKeyPemPath)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "mtls-factory-key",
            Value = "mtls-factory-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Consumer_WithMutualTls_SuccessfullyConsumes()
    {
        // Arrange - produce and consume with mTLS
        var topic = await tlsKafka.CreateTestTopicAsync();
        var groupId = $"test-mtls-group-{Guid.NewGuid():N}";

        var tlsConfig = new TlsConfig
        {
            CaCertificateObject = tlsKafka.CaCertificate,
            ClientCertificate = tlsKafka.ClientCertificate,
            ValidateServerCertificate = true,
            TargetHost = "localhost"
        };

        // Produce a message with mTLS
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-mtls-consumer-producer")
            .UseTls(tlsConfig)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "mtls-consume-key",
            Value = "mtls-consume-value"
        });

        // Act - consume with mTLS
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-mtls-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseTls(tlsConfig)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var record = result!.Value;
        await Assert.That(record.Key).IsEqualTo("mtls-consume-key");
        await Assert.That(record.Value).IsEqualTo("mtls-consume-value");
    }

    // ──────────────────────────────────────────────────────────────
    // TLS protocol version and configuration tests
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Producer_WithExplicitTls12Protocol_SuccessfullyProduces()
    {
        // Arrange - explicitly request TLS 1.2
        var topic = await tlsKafka.CreateTestTopicAsync();

        var tlsConfig = new TlsConfig
        {
            CaCertificateObject = tlsKafka.CaCertificate,
            ValidateServerCertificate = true,
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls12
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls12-producer")
            .UseTls(tlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "tls12-key",
            Value = "tls12-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithExplicitTls13Protocol_SuccessfullyProduces()
    {
        // Arrange - explicitly request TLS 1.3
        var topic = await tlsKafka.CreateTestTopicAsync();

        var tlsConfig = new TlsConfig
        {
            CaCertificateObject = tlsKafka.CaCertificate,
            ValidateServerCertificate = true,
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls13
        };

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls13-producer")
            .UseTls(tlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "tls13-key",
            Value = "tls13-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    // ──────────────────────────────────────────────────────────────
    // End-to-end produce and consume over TLS
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task ProduceAndConsume_OverTls_MessageRoundTripsSuccessfully()
    {
        // Arrange
        var topic = await tlsKafka.CreateTestTopicAsync();
        var groupId = $"test-tls-roundtrip-{Guid.NewGuid():N}";
        const string expectedKey = "roundtrip-key";
        const string expectedValue = "roundtrip-value-over-tls";

        // Act - produce
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-roundtrip-producer")
            .UseTls(tlsKafka.DefaultTlsConfig)
            .WithAcks(Acks.All)
            .BuildAsync();

        var produceResult = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = expectedKey,
            Value = expectedValue
        });

        // Act - consume
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(tlsKafka.BootstrapServers)
            .WithClientId("test-tls-roundtrip-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseTls(tlsKafka.DefaultTlsConfig)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumeResult = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(produceResult.Topic).IsEqualTo(topic);
        await Assert.That(produceResult.Offset).IsGreaterThanOrEqualTo(0);

        await Assert.That(consumeResult).IsNotNull();
        var record = consumeResult!.Value;
        await Assert.That(record.Key).IsEqualTo(expectedKey);
        await Assert.That(record.Value).IsEqualTo(expectedValue);
    }
}
