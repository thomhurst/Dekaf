using System.Collections.Concurrent;
using System.Net.Sockets;
using Dekaf.Admin;
using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Kafka container configured with SASL authentication (PLAIN, SCRAM-SHA-256, SCRAM-SHA-512).
/// Uses the apache/kafka:3.9.1 image in KRaft mode with SASL_PLAINTEXT listeners.
///
/// The container exposes a SASL_PLAINTEXT listener on the external port. The inter-broker
/// communication uses PLAINTEXT (no SASL) to simplify startup. All three SASL mechanisms
/// (PLAIN, SCRAM-SHA-256, SCRAM-SHA-512) are enabled on the external listener.
///
/// SCRAM credentials are created via kafka-configs.sh after the broker starts, since SCRAM
/// stores credentials in the cluster metadata (KRaft) and requires a running broker.
/// </summary>
public class SaslKafkaContainer : IAsyncInitializer, IAsyncDisposable
{
    /// <summary>
    /// Username for SASL authentication.
    /// </summary>
    public const string SaslUsername = "testuser";

    /// <summary>
    /// Password for SASL authentication.
    /// </summary>
    public const string SaslPassword = "testpassword";

    private KafkaContainer? _container;
    private string _bootstrapServers = string.Empty;
    private readonly ConcurrentDictionary<string, byte> _createdTopics = new();

    // JAAS config for PLAIN mechanism on the external listener.
    // Defines both the broker's own credentials (for inter-broker if needed) and the user credentials.
    private static readonly string PlainJaasConfig =
        "org.apache.kafka.common.security.plain.PlainLoginModule required " +
        $"username=\"{SaslUsername}\" " +
        $"password=\"{SaslPassword}\" " +
        $"user_{SaslUsername}=\"{SaslPassword}\";";

    /// <summary>
    /// The Kafka bootstrap servers connection string (host:port).
    /// </summary>
    public string BootstrapServers => _bootstrapServers;

    public async Task InitializeAsync()
    {
        Console.WriteLine("[SaslKafkaContainer] Starting SASL-enabled Kafka container...");

        // Build the container with SASL configuration.
        // The Testcontainers.Kafka KafkaBuilder for apache/kafka sets up:
        //   - PLAINTEXT listener on the external port (mapped to host)
        //   - BROKER listener for inter-broker communication
        // We override the security protocol map to make the external listener use SASL_PLAINTEXT.
        _container = new KafkaBuilder("apache/kafka:3.9.1")
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m")
            .WithEnvironment("KAFKA_LOG_RETENTION_MS", "30000")
            .WithEnvironment("KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS", "10000")
            .WithEnvironment("KAFKA_LOG_SEGMENT_BYTES", "1048576")
            .WithEnvironment("KAFKA_LOG_CLEANUP_POLICY", "delete")
            // Override the listener security protocol map:
            // PLAINTEXT -> SASL_PLAINTEXT for the external listener
            // BROKER -> PLAINTEXT for inter-broker (keeps startup simple)
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:SASL_PLAINTEXT,BROKER:PLAINTEXT,CONTROLLER:PLAINTEXT")
            // Enable all three SASL mechanisms on the external (PLAINTEXT-named) listener
            .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "PLAIN,SCRAM-SHA-256,SCRAM-SHA-512")
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SASL_ENABLED_MECHANISMS", "PLAIN,SCRAM-SHA-256,SCRAM-SHA-512")
            // JAAS configuration for PLAIN on the external listener
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_PLAIN_SASL_JAAS_CONFIG", PlainJaasConfig)
            // SCRAM JAAS configs for the listener (broker-side module with no predefined users;
            // SCRAM users are added dynamically via kafka-configs.sh after startup)
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SCRAM__SHA__256_SASL_JAAS_CONFIG",
                "org.apache.kafka.common.security.scram.ScramLoginModule required;")
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SCRAM__SHA__512_SASL_JAAS_CONFIG",
                "org.apache.kafka.common.security.scram.ScramLoginModule required;")
            .Build();

        await _container.StartAsync();

        var rawAddress = _container.GetBootstrapAddress();
        _bootstrapServers = ExtractHostPort(rawAddress);

        Console.WriteLine($"[SaslKafkaContainer] Kafka started at {_bootstrapServers}");

        await WaitForKafkaReadyAsync();
        await CreateScramCredentialsAsync();
    }

    /// <summary>
    /// Creates SCRAM-SHA-256 and SCRAM-SHA-512 credentials for the test user.
    /// SCRAM credentials are stored in KRaft metadata and must be created after the broker starts.
    /// </summary>
    private async Task CreateScramCredentialsAsync()
    {
        Console.WriteLine("[SaslKafkaContainer] Creating SCRAM credentials...");

        // Create SCRAM-SHA-256 credentials
        var scram256Result = await _container!.ExecAsync([
            "/opt/kafka/bin/kafka-configs.sh",
            "--bootstrap-server", "localhost:9092",
            "--alter",
            "--add-config", $"SCRAM-SHA-256=[password={SaslPassword}]",
            "--entity-type", "users",
            "--entity-name", SaslUsername
        ]);

        if (scram256Result.ExitCode != 0)
        {
            Console.WriteLine($"[SaslKafkaContainer] SCRAM-SHA-256 creation failed (exit={scram256Result.ExitCode}): {scram256Result.Stderr}");
            throw new InvalidOperationException($"Failed to create SCRAM-SHA-256 credentials: {scram256Result.Stderr}");
        }

        Console.WriteLine("[SaslKafkaContainer] SCRAM-SHA-256 credentials created");

        // Create SCRAM-SHA-512 credentials
        var scram512Result = await _container.ExecAsync([
            "/opt/kafka/bin/kafka-configs.sh",
            "--bootstrap-server", "localhost:9092",
            "--alter",
            "--add-config", $"SCRAM-SHA-512=[password={SaslPassword}]",
            "--entity-type", "users",
            "--entity-name", SaslUsername
        ]);

        if (scram512Result.ExitCode != 0)
        {
            Console.WriteLine($"[SaslKafkaContainer] SCRAM-SHA-512 creation failed (exit={scram512Result.ExitCode}): {scram512Result.Stderr}");
            throw new InvalidOperationException($"Failed to create SCRAM-SHA-512 credentials: {scram512Result.Stderr}");
        }

        Console.WriteLine("[SaslKafkaContainer] SCRAM-SHA-512 credentials created");

        // Allow time for credential propagation in KRaft metadata
        await Task.Delay(2000);
    }

    /// <summary>
    /// Creates a unique topic for a test and returns the topic name.
    /// Uses SASL/PLAIN authentication for the admin client.
    /// </summary>
    public async Task<string> CreateTestTopicAsync(int partitions = 1)
    {
        var topicName = $"sasl-test-topic-{Guid.NewGuid():N}";
        await CreateTopicAsync(topicName, partitions);
        return topicName;
    }

    /// <summary>
    /// Creates a topic with the specified name using an authenticated admin client.
    /// </summary>
    public async Task CreateTopicAsync(string topicName, int partitions = 1, int replicationFactor = 1)
    {
        if (!_createdTopics.TryAdd(topicName, 0))
        {
            Console.WriteLine($"[SaslKafkaContainer] Topic '{topicName}' already created");
            return;
        }

        Console.WriteLine($"[SaslKafkaContainer] Creating topic '{topicName}' with {partitions} partition(s)...");

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .WithSaslPlain(SaslUsername, SaslPassword)
            .Build();

        await adminClient.CreateTopicsAsync([
            new NewTopic
            {
                Name = topicName,
                NumPartitions = partitions,
                ReplicationFactor = (short)replicationFactor
            }
        ]);

        // Wait for topic metadata to propagate
        await Task.Delay(3000);
        Console.WriteLine($"[SaslKafkaContainer] Topic '{topicName}' created");
    }

    private static string ExtractHostPort(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return $"{uri.Host}:{uri.Port}";
        }
        return address.TrimEnd('/');
    }

    private async Task WaitForKafkaReadyAsync()
    {
        Console.WriteLine("[SaslKafkaContainer] Waiting for Kafka to be ready...");
        const int maxAttempts = 30;

        var colonIndex = _bootstrapServers.LastIndexOf(':');
        var host = _bootstrapServers[..colonIndex];
        var port = int.Parse(_bootstrapServers[(colonIndex + 1)..]);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port);
                if (client.Connected)
                {
                    Console.WriteLine("[SaslKafkaContainer] Kafka is accepting connections");
                    await Task.Delay(2000);
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException($"SASL Kafka not ready after {maxAttempts} attempts");
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
