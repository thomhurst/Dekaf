using System.Buffers.Text;
using System.Text;
using Dekaf.Admin;
using Dekaf.Security.Sasl;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Kafka container configured with SASL/OAUTHBEARER authentication.
///
/// The container exposes a SASL_PLAINTEXT listener on the external port using the
/// OAUTHBEARER mechanism. Inter-broker communication uses PLAINTEXT (no SASL) to keep
/// startup simple, matching <see cref="SaslKafkaContainer"/>.
///
/// To avoid needing an external OAuth/OIDC server, the broker uses Kafka's built-in
/// <c>OAuthBearerUnsecuredValidatorCallbackHandler</c>, which validates unsecured JWS
/// tokens (RFC 7515 <c>alg=none</c>). The client supplies such a token via a token
/// provider (see <see cref="CreateToken"/>), exercising Dekaf's full OAUTHBEARER
/// SASL handshake end to end. No authorizer is enabled, so any authenticated principal
/// has full access.
/// </summary>
public class OAuthBearerKafkaContainer : KafkaTestContainer
{
    /// <summary>
    /// The principal (JWT <c>sub</c> claim) used for client tokens.
    /// </summary>
    public const string Principal = "testuser";

    // Broker-side JAAS for the OAUTHBEARER listener. The unsecured login claim gives the
    // broker its own token (used by its LoginManager at startup); incoming client tokens are
    // independently checked by the unsecured validator callback handler configured below.
    private const string JaasConfig =
        "org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginModule required " +
        "unsecuredLoginStringClaim_sub=\"admin\";";

    private const string UnsecuredValidatorCallbackHandler =
        "org.apache.kafka.common.security.oauthbearer.internals.unsecured.OAuthBearerUnsecuredValidatorCallbackHandler";

    public override string ContainerName => "apache/kafka:4.0.1";
    public override int Version => 401;

    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) => builder
        // External (PLAINTEXT-named) listener uses SASL_PLAINTEXT; inter-broker stays PLAINTEXT.
        .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:SASL_PLAINTEXT,BROKER:PLAINTEXT,CONTROLLER:PLAINTEXT")
        // Enable only OAUTHBEARER on the external listener.
        .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "OAUTHBEARER")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SASL_ENABLED_MECHANISMS", "OAUTHBEARER")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_OAUTHBEARER_SASL_JAAS_CONFIG", JaasConfig)
        // Validate incoming client tokens as unsecured JWS (no external OAuth server required).
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_OAUTHBEARER_SASL_SERVER_CALLBACK_HANDLER_CLASS", UnsecuredValidatorCallbackHandler);

    /// <summary>
    /// Creates an admin client authenticated with SASL/OAUTHBEARER.
    /// </summary>
    public override IAdminClient CreateAdminClient()
    {
        return Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .WithOAuthBearer(GetTokenAsync)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
    }

    /// <summary>
    /// Token provider supplying a fresh, valid unsecured JWS for <see cref="Principal"/>.
    /// Matches the <see cref="Func{T, TResult}"/> shape expected by <c>WithOAuthBearer</c>.
    /// </summary>
    public static ValueTask<OAuthBearerToken> GetTokenAsync(CancellationToken cancellationToken) =>
        new(CreateToken(Principal));

    /// <summary>
    /// Builds a valid unsecured JWS (<c>alg=none</c>) bearer token for the given principal.
    /// </summary>
    public static OAuthBearerToken CreateToken(string principal, TimeSpan? lifetime = null)
    {
        var ttl = lifetime ?? TimeSpan.FromHours(1);
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.Add(ttl);
        return new OAuthBearerToken
        {
            TokenValue = BuildUnsecuredJws(principal, issuedAt, expiresAt),
            Expiration = expiresAt,
            PrincipalName = principal
        };
    }

    /// <summary>
    /// Builds a token whose JWS has already expired (server should reject it), while the
    /// client-side <see cref="OAuthBearerToken.Expiration"/> is still in the future so the
    /// client transmits it rather than failing locally. Used to prove the broker rejects
    /// invalid tokens with an <see cref="Errors.AuthenticationException"/>.
    /// </summary>
    public static OAuthBearerToken CreateServerRejectedToken(string principal)
    {
        var now = DateTimeOffset.UtcNow;
        return new OAuthBearerToken
        {
            TokenValue = BuildUnsecuredJws(principal, now.AddHours(-2), now.AddHours(-1)),
            Expiration = now.AddHours(1),
            PrincipalName = principal
        };
    }

    private static string BuildUnsecuredJws(string subject, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        const string header = """{"alg":"none"}""";
        var payload =
            $$"""{"sub":"{{subject}}","iat":{{issuedAt.ToUnixTimeSeconds()}},"exp":{{expiresAt.ToUnixTimeSeconds()}}}""";

        // Unsecured JWS compact serialization: BASE64URL(header).BASE64URL(payload).<empty signature>
        return $"{Base64UrlEncode(header)}.{Base64UrlEncode(payload)}.";
    }

    private static string Base64UrlEncode(string value) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// After the broker is TCP-ready, poll until an OAUTHBEARER-authenticated admin client
    /// can list topics, confirming the SASL handshake path is fully ready.
    /// </summary>
    protected override ValueTask OnAfterInitializeAsync() =>
        new(WaitForAdminReadyAsync("OAUTHBEARER"));
}
