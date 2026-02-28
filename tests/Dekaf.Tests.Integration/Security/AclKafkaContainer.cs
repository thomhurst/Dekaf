using System.Collections.Concurrent;
using System.Net.Sockets;
using Dekaf.Admin;
using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Kafka container with SASL_PLAINTEXT authentication and ACL authorization enabled.
/// Configures two users:
///   - "admin" (super user) with full access
///   - "testuser" with no default permissions (ACLs must be explicitly granted)
///
/// Uses KRaft mode with StandardAuthorizer.
/// </summary>
public class AclKafkaContainer : IAsyncInitializer, IAsyncDisposable
{
    // JAAS config for SASL PLAIN with two users: admin and testuser
    private const string JaasConfig =
        "org.apache.kafka.common.security.plain.PlainLoginModule required " +
        """username="admin" password="admin-secret" """ +
        """user_admin="admin-secret" """ +
        """user_testuser="testuser-secret";""";

    private KafkaContainer? _container;
    private string _bootstrapServers = string.Empty;
    private readonly ConcurrentDictionary<string, byte> _createdTopics = new();

    /// <summary>
    /// The admin username that has super-user privileges.
    /// </summary>
    public const string AdminUsername = "admin";

    /// <summary>
    /// The admin password.
    /// </summary>
    public const string AdminPassword = "admin-secret";

    /// <summary>
    /// The restricted test user that has no default permissions.
    /// </summary>
    public const string TestUsername = "testuser";

    /// <summary>
    /// The restricted test user's password.
    /// </summary>
    public const string TestPassword = "testuser-secret";

    /// <summary>
    /// The Kafka bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers => _bootstrapServers;

    public async Task InitializeAsync()
    {
        Console.WriteLine("[AclKafkaContainer] Starting SASL-enabled Kafka container...");

        _container = new KafkaBuilder("apache/kafka:3.9.1")
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m")
            // Enable StandardAuthorizer for KRaft mode
            .WithEnvironment("KAFKA_AUTHORIZER_CLASS_NAME",
                "org.apache.kafka.metadata.authorizer.StandardAuthorizer")
            // Admin is a super user - bypasses all ACL checks
            .WithEnvironment("KAFKA_SUPER_USERS", "User:admin")
            // Deny access by default when no ACLs match
            .WithEnvironment("KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND", "false")
            // Configure SASL PLAIN on the external listener
            // The Testcontainers KafkaBuilder for apache/kafka creates listeners named:
            //   PLAINTEXT (controller), BROKER (inter-broker), and the external listener
            // We configure SASL on the external-facing listener
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP",
                "PLAINTEXT:SASL_PLAINTEXT,BROKER:SASL_PLAINTEXT")
            .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "PLAIN")
            .WithEnvironment("KAFKA_SASL_MECHANISM_INTER_BROKER_PROTOCOL", "PLAIN")
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SASL_ENABLED_MECHANISMS", "PLAIN")
            .WithEnvironment("KAFKA_LISTENER_NAME_BROKER_SASL_ENABLED_MECHANISMS", "PLAIN")
            // JAAS configuration for SASL PLAIN - applies to all listeners
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_PLAIN_SASL_JAAS_CONFIG", JaasConfig)
            .WithEnvironment("KAFKA_LISTENER_NAME_BROKER_PLAIN_SASL_JAAS_CONFIG", JaasConfig)
            // Inter-broker communication uses admin credentials
            .WithEnvironment("KAFKA_SASL_JAAS_CONFIG", JaasConfig)
            // Allow the controller to communicate without SASL (KRaft controller listener)
            .WithEnvironment("KAFKA_LISTENER_NAME_CONTROLLER_SASL_ENABLED_MECHANISMS", "PLAIN")
            .WithEnvironment("KAFKA_LISTENER_NAME_CONTROLLER_PLAIN_SASL_JAAS_CONFIG", JaasConfig)
            // Log retention settings (same as base container)
            .WithEnvironment("KAFKA_LOG_RETENTION_MS", "30000")
            .WithEnvironment("KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS", "10000")
            .WithEnvironment("KAFKA_LOG_SEGMENT_BYTES", "1048576")
            .WithEnvironment("KAFKA_LOG_CLEANUP_POLICY", "delete")
            .Build();

        await _container.StartAsync().ConfigureAwait(false);

        var rawAddress = _container.GetBootstrapAddress();
        _bootstrapServers = ExtractHostPort(rawAddress);

        Console.WriteLine($"[AclKafkaContainer] Kafka started at {_bootstrapServers}");

        await WaitForKafkaReadyAsync().ConfigureAwait(false);
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
        Console.WriteLine("[AclKafkaContainer] Waiting for Kafka to be ready...");
        const int maxAttempts = 30;

        var colonIndex = _bootstrapServers.LastIndexOf(':');
        var host = _bootstrapServers[..colonIndex];
        var port = int.Parse(_bootstrapServers[(colonIndex + 1)..]);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port).ConfigureAwait(false);
                if (client.Connected)
                {
                    Console.WriteLine("[AclKafkaContainer] Kafka is accepting connections");
                    // Wait for broker to fully initialize with SASL
                    await Task.Delay(2000).ConfigureAwait(false);
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"[AclKafkaContainer] Kafka not ready after {maxAttempts} attempts at {_bootstrapServers}");
    }

    /// <summary>
    /// Creates an admin client authenticated as the super user.
    /// </summary>
    public IAdminClient CreateAdminClient()
    {
        return Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .WithSaslPlain(AdminUsername, AdminPassword)
            .WithClientId("acl-test-admin")
            .Build();
    }

    /// <summary>
    /// Creates a unique topic for a test and returns the topic name.
    /// Uses the admin (super user) credentials.
    /// </summary>
    public async Task<string> CreateTestTopicAsync(int partitions = 1)
    {
        var topicName = $"acl-test-topic-{Guid.NewGuid():N}";
        await CreateTopicAsync(topicName, partitions).ConfigureAwait(false);
        return topicName;
    }

    /// <summary>
    /// Creates a topic with the specified name using admin credentials.
    /// </summary>
    public async Task CreateTopicAsync(string topicName, int partitions = 1, int replicationFactor = 1)
    {
        if (!_createdTopics.TryAdd(topicName, 0))
        {
            return;
        }

        Console.WriteLine($"[AclKafkaContainer] Creating topic '{topicName}' with {partitions} partition(s)...");

        await using var admin = CreateAdminClient();
        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topicName,
                NumPartitions = partitions,
                ReplicationFactor = (short)replicationFactor
            }
        ]).ConfigureAwait(false);

        // Wait for topic metadata to propagate
        await Task.Delay(3000).ConfigureAwait(false);
        Console.WriteLine($"[AclKafkaContainer] Topic '{topicName}' created");
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
