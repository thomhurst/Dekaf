using Dekaf.Admin;
using Dekaf.Security;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Kafka container exposing a SASL_SSL listener: SASL authentication over a TLS-encrypted
/// transport ("not plaintext"). All four SASL mechanisms are enabled on the external listener
/// (PLAIN, SCRAM-SHA-256, SCRAM-SHA-512, OAUTHBEARER); inter-broker traffic stays PLAINTEXT to
/// keep startup simple, matching <see cref="SaslKafkaContainer"/>.
///
/// The broker presents a server certificate signed by a test CA (no client certificate is
/// required — SASL provides identity, TLS provides encryption). SCRAM credentials are created
/// after startup via the SASL/PLAIN admin client, and OAUTHBEARER uses the unsecured validator
/// (see <see cref="OAuthBearerKafkaContainer"/>).
/// </summary>
public class SaslSslKafkaContainer : KafkaTestContainer
{
    public const string SaslUsername = "testuser";
    public const string SaslPassword = "testpassword";

    private readonly TestCertificateGenerator _certGenerator = new();
    private TlsConfig? _defaultTlsConfig;

    private static readonly string PlainJaasConfig =
        "org.apache.kafka.common.security.plain.PlainLoginModule required " +
        $"username=\"{SaslUsername}\" " +
        $"password=\"{SaslPassword}\" " +
        $"user_{SaslUsername}=\"{SaslPassword}\";";

    private const string OAuthJaasConfig =
        "org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginModule required " +
        "unsecuredLoginStringClaim_sub=\"admin\";";

    private const string UnsecuredValidatorCallbackHandler =
        "org.apache.kafka.common.security.oauthbearer.internals.unsecured.OAuthBearerUnsecuredValidatorCallbackHandler";

    private const string ContainerSecretsDir = "/etc/kafka/secrets";
    private const string KeystoreContainerPath = $"{ContainerSecretsDir}/server.p12";
    private const string TruststoreContainerPath = $"{ContainerSecretsDir}/truststore.p12";

    public override string ContainerName => "apache/kafka:4.0.1";
    public override int Version => 401;

    /// <summary>
    /// TLS configuration that validates the broker certificate against the test CA.
    /// </summary>
    public TlsConfig DefaultTlsConfig =>
        _defaultTlsConfig ??= new TlsConfig
        {
            CaCertificateObject = _certGenerator.CaCertificate,
            ValidateServerCertificate = true,
            TargetHost = "localhost"
        };

    /// <summary>
    /// Token provider supplying a valid unsecured JWS for <see cref="SaslUsername"/>.
    /// </summary>
    public static ValueTask<Dekaf.Security.Sasl.OAuthBearerToken> GetTokenAsync(CancellationToken cancellationToken) =>
        new(OAuthBearerKafkaContainer.CreateToken(SaslUsername));

    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) => builder
        // External (PLAINTEXT-named) listener uses SASL_SSL; inter-broker stays PLAINTEXT.
        .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:SASL_SSL,BROKER:PLAINTEXT,CONTROLLER:PLAINTEXT")
        // SSL keystore/truststore (PKCS12). Client auth is not required; SASL provides identity.
        .WithEnvironment("KAFKA_SSL_KEYSTORE_TYPE", "PKCS12")
        .WithEnvironment("KAFKA_SSL_KEYSTORE_LOCATION", KeystoreContainerPath)
        .WithEnvironment("KAFKA_SSL_KEYSTORE_PASSWORD", TestCertificateGenerator.StorePassword)
        .WithEnvironment("KAFKA_SSL_KEY_PASSWORD", TestCertificateGenerator.StorePassword)
        .WithEnvironment("KAFKA_SSL_TRUSTSTORE_TYPE", "PKCS12")
        .WithEnvironment("KAFKA_SSL_TRUSTSTORE_LOCATION", TruststoreContainerPath)
        .WithEnvironment("KAFKA_SSL_TRUSTSTORE_PASSWORD", TestCertificateGenerator.StorePassword)
        .WithEnvironment("KAFKA_SSL_CLIENT_AUTH", "none")
        .WithEnvironment("KAFKA_SSL_ENDPOINT_IDENTIFICATION_ALGORITHM", "")
        // Enable every SASL mechanism on the external listener.
        .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "PLAIN,SCRAM-SHA-256,SCRAM-SHA-512,OAUTHBEARER")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SASL_ENABLED_MECHANISMS", "PLAIN,SCRAM-SHA-256,SCRAM-SHA-512,OAUTHBEARER")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_PLAIN_SASL_JAAS_CONFIG", PlainJaasConfig)
        // SCRAM hyphens require TRIPLE underscores (KafkaDockerWrapper maps '___' -> '-').
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SCRAM___SHA___256_SASL_JAAS_CONFIG",
            "org.apache.kafka.common.security.scram.ScramLoginModule required;")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SCRAM___SHA___512_SASL_JAAS_CONFIG",
            "org.apache.kafka.common.security.scram.ScramLoginModule required;")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_OAUTHBEARER_SASL_JAAS_CONFIG", OAuthJaasConfig)
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_OAUTHBEARER_SASL_SERVER_CALLBACK_HANDLER_CLASS", UnsecuredValidatorCallbackHandler)
        // Mount the server keystore and truststore. The byte[] overload copies the content to an
        // exact file path; the string-path overload bind-mounts and can create a directory at the
        // target on some Docker hosts (e.g. Windows).
        .WithResourceMapping(File.ReadAllBytes(_certGenerator.ServerKeystorePath), KeystoreContainerPath)
        .WithResourceMapping(File.ReadAllBytes(_certGenerator.ServerTruststorePath), TruststoreContainerPath);

    /// <summary>
    /// Creates an admin client authenticated with SASL/PLAIN over TLS.
    /// </summary>
    public override IAdminClient CreateAdminClient()
    {
        return Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .WithTls(DefaultTlsConfig)
            .WithSaslPlain(SaslUsername, SaslPassword)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
    }

    protected override async ValueTask OnAfterInitializeAsync()
    {
        await WaitForAdminReadyAsync("SASL_SSL/PLAIN").ConfigureAwait(false);
        await CreateScramCredentialsAsync(SaslUsername, SaslPassword).ConfigureAwait(false);
        await WaitForAdminReadyAsync("SASL_SSL/SCRAM-SHA-256", () => CreateScramAdminClient(ScramMechanism.ScramSha256, SaslUsername, SaslPassword)).ConfigureAwait(false);
        await WaitForAdminReadyAsync("SASL_SSL/SCRAM-SHA-512", () => CreateScramAdminClient(ScramMechanism.ScramSha512, SaslUsername, SaslPassword)).ConfigureAwait(false);
    }

    /// <summary>Admin clients built by the base class connect over TLS.</summary>
    protected override AdminClientBuilder ApplyTransportSecurity(AdminClientBuilder builder) =>
        builder.WithTls(DefaultTlsConfig);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        _certGenerator.Dispose();
        GC.SuppressFinalize(this);
    }
}
