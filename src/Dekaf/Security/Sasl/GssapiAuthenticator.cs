using System.Net.Security;

namespace Dekaf.Security.Sasl;

/// <summary>
/// SASL GSSAPI mechanism authenticator using Kerberos.
/// Uses System.Net.Security.NegotiateAuthentication for cross-platform GSSAPI support.
/// </summary>
/// <remarks>
/// Platform considerations:
/// - Windows: Uses native SSPI (Security Support Provider Interface)
/// - Linux: Requires libgssapi_krb5 (part of MIT Kerberos or Heimdal)
/// - macOS: Uses Heimdal Kerberos
///
/// The GSSAPI authentication flow with Kafka:
/// 1. Client sends SaslHandshake with mechanism "GSSAPI"
/// 2. Server responds with supported mechanisms
/// 3. Client initiates GSSAPI token exchange (multi-round)
/// 4. Once tokens are complete, authentication is established
/// </remarks>
public sealed class GssapiAuthenticator : ISaslAuthenticator
{
    private readonly GssapiConfig _config;
    private readonly string _targetHost;
    private NegotiateAuthentication? _auth;
    private GssapiState _state = GssapiState.Initial;
    private byte[]? _initialToken;

    private enum GssapiState
    {
        Initial,
        TokenExchange,
        Complete
    }

    /// <summary>
    /// Creates a new GSSAPI authenticator.
    /// </summary>
    /// <param name="config">The GSSAPI configuration.</param>
    /// <param name="targetHost">The target broker hostname for SPN construction.</param>
    public GssapiAuthenticator(GssapiConfig config, string targetHost)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _targetHost = targetHost ?? throw new ArgumentNullException(nameof(targetHost));
    }

    /// <inheritdoc />
    public string MechanismName => "GSSAPI";

    /// <inheritdoc />
    public bool IsComplete => _state == GssapiState.Complete;

    /// <inheritdoc />
    public byte[] GetInitialResponse()
    {
        if (_state != GssapiState.Initial)
        {
            throw new InvalidOperationException("GetInitialResponse can only be called once");
        }

        // Build the Service Principal Name (SPN)
        // Format: serviceName/hostname[@realm]
        var spn = BuildSpn();

        // Create the NegotiateAuthentication client
        // NegotiateAuthentication automatically uses GSSAPI on Unix and SSPI on Windows
        var clientOptions = new NegotiateAuthenticationClientOptions
        {
            Package = "Kerberos",
            TargetName = spn,
            RequiredProtectionLevel = ProtectionLevel.None, // Kafka uses authentication only, not integrity/encryption
            AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Identification
        };

        // If a principal is specified, use a custom credential
        // Otherwise, NegotiateAuthentication will use the default credentials from the cache
        if (_config.Principal is not null)
        {
            // Note: For keytab-based authentication, the system Kerberos configuration
            // should be set up to use the keytab. NegotiateAuthentication will use
            // the credentials from the credential cache.
            // Environment variables like KRB5_KTNAME can specify the keytab path.
            clientOptions.Credential = System.Net.CredentialCache.DefaultNetworkCredentials;
        }

        _auth = new NegotiateAuthentication(clientOptions);

        // Get the initial token
        var status = _auth.GetOutgoingBlob(ReadOnlySpan<byte>.Empty, out var outgoingBlob);

        if (status.ErrorCode != NegotiateAuthenticationStatusCode.ContinueNeeded &&
            status.ErrorCode != NegotiateAuthenticationStatusCode.Completed)
        {
            throw new AuthenticationException($"GSSAPI initial token generation failed: {status.ErrorCode}");
        }

        _state = status.ErrorCode == NegotiateAuthenticationStatusCode.Completed
            ? GssapiState.Complete
            : GssapiState.TokenExchange;

        _initialToken = outgoingBlob ?? [];
        return _initialToken;
    }

    /// <inheritdoc />
    public byte[]? EvaluateChallenge(byte[] challenge)
    {
        if (_auth is null)
        {
            throw new InvalidOperationException("GetInitialResponse must be called before EvaluateChallenge");
        }

        if (_state == GssapiState.Complete)
        {
            return null;
        }

        var status = _auth.GetOutgoingBlob(challenge, out var outgoingBlob);

        if (status.ErrorCode == NegotiateAuthenticationStatusCode.Completed)
        {
            _state = GssapiState.Complete;
            // Return any final token if present, otherwise null to indicate completion
            return outgoingBlob is { Length: > 0 } ? outgoingBlob : null;
        }

        if (status.ErrorCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
        {
            return outgoingBlob ?? [];
        }

        throw new AuthenticationException($"GSSAPI authentication failed: {status.ErrorCode}");
    }

    private string BuildSpn()
    {
        // SPN format: serviceName/hostname[@REALM]
        // Kafka brokers typically expect: kafka/broker-hostname@REALM
        var spn = $"{_config.ServiceName}/{_targetHost}";

        if (_config.Realm is not null)
        {
            spn = $"{spn}@{_config.Realm}";
        }

        return spn;
    }
}
