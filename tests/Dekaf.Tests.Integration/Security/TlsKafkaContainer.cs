using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Kafka container configured with SSL/TLS encryption for integration testing.
/// Generates test certificates at startup, mounts them into the container,
/// and configures Kafka to use SSL listeners.
///
/// Uses PEM-based SSL configuration (supported in Kafka 3.x+) to avoid
/// needing Java keytool for JKS keystore generation.
///
/// The container exposes an SSL listener on the external port and uses
/// PLAINTEXT for inter-broker communication (controller).
/// </summary>
public class TlsKafkaContainer : IAsyncInitializer, IAsyncDisposable
{
    private KafkaContainer? _container;
    private TestCertificateGenerator? _certGenerator;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _createdTopics = new();

    /// <summary>
    /// The SSL bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers { get; private set; } = string.Empty;

    /// <summary>
    /// The test certificate generator providing CA, server, and client certificates.
    /// </summary>
    internal TestCertificateGenerator CertificateGenerator =>
        _certGenerator ?? throw new InvalidOperationException("Container not initialized");

    /// <summary>
    /// The CA certificate for validating the server's certificate.
    /// </summary>
    public X509Certificate2 CaCertificate => CertificateGenerator.CaCertificate;

    /// <summary>
    /// The client certificate for mutual TLS authentication.
    /// </summary>
    public X509Certificate2 ClientCertificate => CertificateGenerator.ClientCertificate;

    /// <summary>
    /// Path to the CA certificate PEM file on the host.
    /// </summary>
    public string CaCertPemPath => CertificateGenerator.CaCertPemPath;

    /// <summary>
    /// Path to the client certificate PEM file on the host.
    /// </summary>
    public string ClientCertPemPath => CertificateGenerator.ClientCertPemPath;

    /// <summary>
    /// Path to the client private key PEM file on the host.
    /// </summary>
    public string ClientKeyPemPath => CertificateGenerator.ClientKeyPemPath;

    public async Task InitializeAsync()
    {
        Console.WriteLine("[TlsKafkaContainer] Generating test certificates...");
        _certGenerator = new TestCertificateGenerator();

        Console.WriteLine("[TlsKafkaContainer] Starting TLS-enabled Kafka container...");

        // Container paths for certificate files
        const string containerCertDir = "/etc/kafka/secrets";
        const string caCertContainerPath = $"{containerCertDir}/ca-cert.pem";
        const string serverCertContainerPath = $"{containerCertDir}/server-cert.pem";
        const string serverKeyContainerPath = $"{containerCertDir}/server-key.pem";
        const string serverKeystoreContainerPath = $"{containerCertDir}/server.p12";
        const string truststoreContainerPath = $"{containerCertDir}/truststore.p12";
        const string clientCertContainerPath = $"{containerCertDir}/client-cert.pem";
        const string clientKeyContainerPath = $"{containerCertDir}/client-key.pem";

        // Export server cert and key as PEM for mounting
        var serverCertPemPath = Path.Combine(_certGenerator.CertificateDirectory, "server-cert.pem");
        var serverKeyPemPath = Path.Combine(_certGenerator.CertificateDirectory, "server-key.pem");
        ExportCertificateToPem(_certGenerator.ServerCertificate, serverCertPemPath);
        ExportPrivateKeyToPem(_certGenerator.ServerCertificate, serverKeyPemPath);

        _container = new KafkaBuilder("apache/kafka:3.9.1")
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m")
            // SSL listener configuration
            // The Testcontainers KafkaBuilder manages KAFKA_LISTENERS and KAFKA_LISTENER_SECURITY_PROTOCOL_MAP
            // internally. We override the security protocol map to make the external listener use SSL.
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:SSL,BROKER:PLAINTEXT,CONTROLLER:PLAINTEXT")
            // SSL configuration using PKCS12 keystore (Kafka supports PKCS12 natively)
            .WithEnvironment("KAFKA_SSL_KEYSTORE_TYPE", "PKCS12")
            .WithEnvironment("KAFKA_SSL_KEYSTORE_LOCATION", serverKeystoreContainerPath)
            .WithEnvironment("KAFKA_SSL_KEYSTORE_PASSWORD", TestCertificateGenerator.StorePassword)
            .WithEnvironment("KAFKA_SSL_KEY_PASSWORD", TestCertificateGenerator.StorePassword)
            .WithEnvironment("KAFKA_SSL_TRUSTSTORE_TYPE", "PKCS12")
            .WithEnvironment("KAFKA_SSL_TRUSTSTORE_LOCATION", truststoreContainerPath)
            .WithEnvironment("KAFKA_SSL_TRUSTSTORE_PASSWORD", TestCertificateGenerator.StorePassword)
            // Enable client authentication for mTLS tests (requested but not required)
            .WithEnvironment("KAFKA_SSL_CLIENT_AUTH", "requested")
            // Endpoint identification disabled for test certificates
            .WithEnvironment("KAFKA_SSL_ENDPOINT_IDENTIFICATION_ALGORITHM", "")
            // Mount certificate files into the container
            .WithResourceMapping(_certGenerator.CaCertPemPath, caCertContainerPath)
            .WithResourceMapping(serverCertPemPath, serverCertContainerPath)
            .WithResourceMapping(serverKeyPemPath, serverKeyContainerPath)
            .WithResourceMapping(_certGenerator.ServerKeystorePath, serverKeystoreContainerPath)
            .WithResourceMapping(_certGenerator.ServerTruststorePath, truststoreContainerPath)
            .WithResourceMapping(_certGenerator.ClientCertPemPath, clientCertContainerPath)
            .WithResourceMapping(_certGenerator.ClientKeyPemPath, clientKeyContainerPath)
            .Build();

        await _container.StartAsync();

        var rawAddress = _container.GetBootstrapAddress();
        BootstrapServers = ExtractHostPort(rawAddress);

        Console.WriteLine($"[TlsKafkaContainer] TLS Kafka started at {BootstrapServers}");

        await WaitForKafkaSslAsync();
    }

    private static string ExtractHostPort(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return $"{uri.Host}:{uri.Port}";
        }
        return address.TrimEnd('/');
    }

    private async Task WaitForKafkaSslAsync()
    {
        Console.WriteLine("[TlsKafkaContainer] Waiting for Kafka SSL listener to be ready...");
        const int maxAttempts = 30;

        var colonIndex = BootstrapServers.LastIndexOf(':');
        var host = BootstrapServers[..colonIndex];
        var port = int.Parse(BootstrapServers[(colonIndex + 1)..]);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port);
                if (client.Connected)
                {
                    Console.WriteLine("[TlsKafkaContainer] Kafka SSL listener is accepting connections");
                    // Give it a moment to fully initialize
                    await Task.Delay(3000);
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException($"Kafka SSL listener not ready after {maxAttempts} attempts");
    }

    /// <summary>
    /// Creates a unique topic for a test and returns the topic name.
    /// Uses the admin client with TLS to create the topic.
    /// </summary>
    public async Task<string> CreateTestTopicAsync(int partitions = 1)
    {
        var topicName = $"test-tls-topic-{Guid.NewGuid():N}";
        await CreateTopicAsync(topicName, partitions);
        return topicName;
    }

    /// <summary>
    /// Creates a topic with the specified name via the admin client.
    /// </summary>
    public async Task CreateTopicAsync(string topicName, int partitions = 1, int replicationFactor = 1)
    {
        if (!_createdTopics.TryAdd(topicName, 0))
        {
            Console.WriteLine($"[TlsKafkaContainer] Topic '{topicName}' already created");
            return;
        }

        Console.WriteLine($"[TlsKafkaContainer] Creating topic '{topicName}' with {partitions} partition(s)...");

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .UseTls()
            .Build();

        await adminClient.CreateTopicsAsync([
            new Dekaf.Admin.NewTopic
            {
                Name = topicName,
                NumPartitions = partitions,
                ReplicationFactor = (short)replicationFactor
            }
        ]);

        // Wait for topic metadata to propagate
        await Task.Delay(3000);
        Console.WriteLine($"[TlsKafkaContainer] Topic '{topicName}' created");
    }

    private static void ExportCertificateToPem(X509Certificate2 cert, string path)
    {
        var pem = new System.Text.StringBuilder();
        pem.AppendLine("-----BEGIN CERTIFICATE-----");
        pem.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
        pem.AppendLine("-----END CERTIFICATE-----");
        File.WriteAllText(path, pem.ToString());
    }

    private static void ExportPrivateKeyToPem(X509Certificate2 cert, string path)
    {
        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Certificate does not have an RSA private key");

        var privateKeyBytes = rsa.ExportPkcs8PrivateKey();
        var pem = new System.Text.StringBuilder();
        pem.AppendLine("-----BEGIN PRIVATE KEY-----");
        pem.AppendLine(Convert.ToBase64String(privateKeyBytes, Base64FormattingOptions.InsertLineBreaks));
        pem.AppendLine("-----END PRIVATE KEY-----");
        File.WriteAllText(path, pem.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        _certGenerator?.Dispose();
        GC.SuppressFinalize(this);
    }
}
