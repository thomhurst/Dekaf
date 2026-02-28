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

    /// <summary>
    /// Internal bootstrap server address used by kafka-configs.sh inside the container.
    /// </summary>
    private const string InternalBootstrapServer = "localhost:9092";

    // JAAS config for PLAIN mechanism on the external listener.
    // Defines both the broker's own credentials (for inter-broker if needed) and the user credentials.
    private static readonly string PlainJaasConfig =
        "org.apache.kafka.common.security.plain.PlainLoginModule required " +
        $"username=\"{SaslUsername}\" " +
        $"password=\"{SaslPassword}\" " +
        $"user_{SaslUsername}=\"{SaslPassword}\";";

    public override string ContainerName => "apache/kafka:3.9.1";
    public override int Version => 391;

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
        // SCRAM users are added dynamically via kafka-configs.sh after startup)
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SCRAM__SHA__256_SASL_JAAS_CONFIG",
            "org.apache.kafka.common.security.scram.ScramLoginModule required;")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SCRAM__SHA__512_SASL_JAAS_CONFIG",
            "org.apache.kafka.common.security.scram.ScramLoginModule required;");

    /// <summary>
    /// Creates an admin client with SASL/PLAIN authentication.
    /// </summary>
    protected override IAdminClient CreateAdminClient()
    {
        return Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .WithSaslPlain(SaslUsername, SaslPassword)
            .Build();
    }

    /// <summary>
    /// After the base container is started and TCP-ready, verify SASL/PLAIN authentication
    /// works and create SCRAM credentials.
    /// </summary>
    protected override async ValueTask OnAfterInitializeAsync()
    {
        await WaitForSaslReadyAsync().ConfigureAwait(false);
        await CreateScramCredentialsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Polls until the broker accepts SASL/PLAIN authentication.
    /// The base class only waits for TCP connectivity; this verifies the SASL handshake works.
    /// </summary>
    private async Task WaitForSaslReadyAsync()
    {
        Console.WriteLine("[SaslKafkaContainer] Waiting for SASL/PLAIN authentication to be ready...");
        const int maxAttempts = 30;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var adminClient = CreateAdminClient();
                await using (adminClient.ConfigureAwait(false))
                {
                    await adminClient.ListTopicsAsync().ConfigureAwait(false);
                    Console.WriteLine("[SaslKafkaContainer] Kafka is ready (SASL/PLAIN authentication successful)");
                    return;
                }
            }
            catch
            {
                // Broker not fully ready yet, retry
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"SASL Kafka not ready for authentication after {maxAttempts} attempts");
    }

    /// <summary>
    /// Creates SCRAM-SHA-256 and SCRAM-SHA-512 credentials for the test user.
    /// SCRAM credentials are stored in KRaft metadata and must be created after the broker starts.
    /// </summary>
    private async Task CreateScramCredentialsAsync()
    {
        Console.WriteLine("[SaslKafkaContainer] Creating SCRAM credentials...");

        var container = ContainerInstance
            ?? throw new InvalidOperationException("Container has not been initialized.");

        // Create SCRAM-SHA-256 credentials
        var scram256Result = await container.ExecAsync([
            "/opt/kafka/bin/kafka-configs.sh",
            "--bootstrap-server", InternalBootstrapServer,
            "--alter",
            "--add-config", $"SCRAM-SHA-256=[password={SaslPassword}]",
            "--entity-type", "users",
            "--entity-name", SaslUsername
        ]).ConfigureAwait(false);

        if (scram256Result.ExitCode != 0)
        {
            Console.WriteLine($"[SaslKafkaContainer] SCRAM-SHA-256 creation failed (exit={scram256Result.ExitCode}): {scram256Result.Stderr}");
            throw new InvalidOperationException($"Failed to create SCRAM-SHA-256 credentials: {scram256Result.Stderr}");
        }

        Console.WriteLine("[SaslKafkaContainer] SCRAM-SHA-256 credentials created");

        // Create SCRAM-SHA-512 credentials
        var scram512Result = await container.ExecAsync([
            "/opt/kafka/bin/kafka-configs.sh",
            "--bootstrap-server", InternalBootstrapServer,
            "--alter",
            "--add-config", $"SCRAM-SHA-512=[password={SaslPassword}]",
            "--entity-type", "users",
            "--entity-name", SaslUsername
        ]).ConfigureAwait(false);

        if (scram512Result.ExitCode != 0)
        {
            Console.WriteLine($"[SaslKafkaContainer] SCRAM-SHA-512 creation failed (exit={scram512Result.ExitCode}): {scram512Result.Stderr}");
            throw new InvalidOperationException($"Failed to create SCRAM-SHA-512 credentials: {scram512Result.Stderr}");
        }

        Console.WriteLine("[SaslKafkaContainer] SCRAM-SHA-512 credentials created");

        // Poll until SCRAM credentials are usable by attempting authentication
        await WaitForScramCredentialsReadyAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Polls until SCRAM-SHA-256 authentication succeeds, indicating credentials have propagated.
    /// </summary>
    private async Task WaitForScramCredentialsReadyAsync()
    {
        Console.WriteLine("[SaslKafkaContainer] Waiting for SCRAM credentials to propagate...");
        const int maxAttempts = 30;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var adminClient = Kafka.CreateAdminClient()
                    .WithBootstrapServers(BootstrapServers)
                    .WithSaslScramSha256(SaslUsername, SaslPassword)
                    .Build();

                await using (adminClient.ConfigureAwait(false))
                {
                    // If ListTopicsAsync succeeds, SCRAM authentication is working
                    await adminClient.ListTopicsAsync().ConfigureAwait(false);
                    Console.WriteLine("[SaslKafkaContainer] SCRAM credentials are ready");
                    return;
                }
            }
            catch
            {
                // Credentials not yet propagated, retry
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"SCRAM credentials not ready after {maxAttempts} attempts");
    }
}
