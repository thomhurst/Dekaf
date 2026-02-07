using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Kafka container configured with SASL_PLAINTEXT authentication.
/// Provides PLAIN authentication with a test user.
/// </summary>
public sealed class KafkaWithSaslContainer : IAsyncInitializer, IAsyncDisposable
{
    private IContainer? _kafkaContainer;
    private INetwork? _network;
    private string _bootstrapServers = string.Empty;

    public const string SaslUsername = "testuser";
    public const string SaslPassword = "testuser-secret";
    public const string AdminUsername = "admin";
    public const string AdminPassword = "admin-secret";

    public string BootstrapServers => _bootstrapServers;

    public async Task InitializeAsync()
    {
        Console.WriteLine("[KafkaWithSasl] Creating Docker network...");

        _network = new NetworkBuilder()
            .WithName($"kafka-sasl-network-{Guid.NewGuid():N}")
            .Build();
        await _network.CreateAsync().ConfigureAwait(false);

        Console.WriteLine("[KafkaWithSasl] Starting Kafka container with SASL...");

        var jaasConfig = $$"""
            KafkaServer {
                org.apache.kafka.common.security.plain.PlainLoginModule required
                username="{{AdminUsername}}"
                password="{{AdminPassword}}"
                user_{{AdminUsername}}="{{AdminPassword}}"
                user_{{SaslUsername}}="{{SaslPassword}}";
            };
            """;

        _kafkaContainer = new ContainerBuilder("confluentinc/cp-kafka:7.5.0")
            .WithNetwork(_network)
            .WithNetworkAliases("kafka-sasl")
            .WithPortBinding(9092, true)
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@kafka-sasl:9093")
            .WithEnvironment("KAFKA_LISTENERS", "SASL_PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", "SASL_PLAINTEXT://localhost:{0}")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "SASL_PLAINTEXT:SASL_PLAINTEXT,CONTROLLER:PLAINTEXT")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "SASL_PLAINTEXT")
            .WithEnvironment("KAFKA_SASL_MECHANISM_INTER_BROKER_PROTOCOL", "PLAIN")
            .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "PLAIN,SCRAM-SHA-256")
            .WithEnvironment("KAFKA_OPTS", $"-Djava.security.auth.login.config=/etc/kafka/kafka_server_jaas.conf")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            .WithEnvironment("CLUSTER_ID", "MkU3OEVBNTcwNTJENDM2Qk")
            .WithResourceMapping(
                System.Text.Encoding.UTF8.GetBytes(jaasConfig),
                "/etc/kafka/kafka_server_jaas.conf")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("started"))
            .Build();

        await _kafkaContainer.StartAsync().ConfigureAwait(false);

        var port = _kafkaContainer.GetMappedPublicPort(9092);
        _bootstrapServers = $"localhost:{port}";

        Console.WriteLine($"[KafkaWithSasl] Kafka with SASL started at {_bootstrapServers}");

        // Wait for Kafka to be ready
        await Task.Delay(5000).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_kafkaContainer is not null)
        {
            await _kafkaContainer.DisposeAsync().ConfigureAwait(false);
        }

        if (_network is not null)
        {
            await _network.DeleteAsync().ConfigureAwait(false);
            await _network.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
