using Dekaf.Admin;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Kafka container configured with SASL authentication (PLAIN, SCRAM-SHA-256, SCRAM-SHA-512).
/// Extends <see cref="KafkaTestContainer"/> with SASL-specific configuration.
///
/// The container exposes a SASL_PLAINTEXT listener on the external port. The inter-broker
/// communication uses PLAINTEXT (no SASL) to simplify startup. All three SASL mechanisms
/// (PLAIN, SCRAM-SHA-256, SCRAM-SHA-512) are enabled on the external listener.
///
/// SCRAM credentials are created via kafka-configs.sh after the broker starts, since SCRAM
/// stores credentials in the cluster metadata (KRaft) and requires a running broker.
/// </summary>
public class SaslKafkaContainer : KafkaTestContainer
{
    /// <summary>
    /// Username for SASL authentication.
    /// </summary>
    public const string SaslUsername = "testuser";

    /// <summary>
    /// Password for SASL authentication.
    /// </summary>
    public const string SaslPassword = "testpassword";

    // JAAS config for PLAIN mechanism on the external listener.
    // Defines both the broker's own credentials (for inter-broker if needed) and the user credentials.
    private static readonly string PlainJaasConfig =
        "org.apache.kafka.common.security.plain.PlainLoginModule required " +
        $"username=\"{SaslUsername}\" " +
        $"password=\"{SaslPassword}\" " +
        $"user_{SaslUsername}=\"{SaslPassword}\";";

    public override string ContainerName => "apache/kafka:4.0.1";
    public override int Version => 401;

    /// <summary>
    /// Adds SASL-specific environment variables to the Kafka container builder.
    /// </summary>
    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) => builder
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
        // SCRAM users are added dynamically via kafka-configs.sh after startup).
        // The apache/kafka image maps env names to properties via KafkaDockerWrapper:
        // '___' -> '-', '__' -> '_', '_' -> '.'. The mechanism segment must become
        // 'scram-sha-256'/'scram-sha-512', so the hyphens require TRIPLE underscores.
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SCRAM___SHA___256_SASL_JAAS_CONFIG",
            "org.apache.kafka.common.security.scram.ScramLoginModule required;")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SCRAM___SHA___512_SASL_JAAS_CONFIG",
            "org.apache.kafka.common.security.scram.ScramLoginModule required;")
        .WithEnvironment("KAFKA_DELEGATION_TOKEN_SECRET_KEY", "dekaf-integration-delegation-token-secret")
        .WithEnvironment("KAFKA_DELEGATION_TOKEN_EXPIRY_TIME_MS", "3600000")
        .WithEnvironment("KAFKA_DELEGATION_TOKEN_MAX_LIFETIME_MS", "7200000");

    /// <summary>
    /// Creates an admin client with SASL/PLAIN authentication.
    /// </summary>
    public override IAdminClient CreateAdminClient()
    {
        return Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .WithSaslPlain(SaslUsername, SaslPassword)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
    }

    /// <summary>
    /// After the base container is started and TCP-ready, verify SASL/PLAIN authentication
    /// works, create SCRAM credentials, and wait for each mechanism to propagate.
    /// </summary>
    protected override async ValueTask OnAfterInitializeAsync()
    {
        await WaitForAdminReadyAsync("SASL/PLAIN").ConfigureAwait(false);
        await CreateScramCredentialsAsync(SaslUsername, SaslPassword).ConfigureAwait(false);
        await WaitForAdminReadyAsync("SCRAM-SHA-256", () => CreateScramAdminClient(ScramMechanism.ScramSha256, SaslUsername, SaslPassword)).ConfigureAwait(false);
        await WaitForAdminReadyAsync("SCRAM-SHA-512", () => CreateScramAdminClient(ScramMechanism.ScramSha512, SaslUsername, SaslPassword)).ConfigureAwait(false);
    }
}
