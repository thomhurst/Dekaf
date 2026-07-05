#if !NETSTANDARD2_0
using System.Net.Security;
#endif
using Dekaf.Errors;

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
public sealed class GssapiAuthenticator : ISaslAuthenticator, IDisposable
{
#if !NETSTANDARD2_0
    private readonly GssapiConfig _config;
    private readonly string _targetHost;
    private NegotiateAuthentication? _auth;
#endif
    private GssapiState _state = GssapiState.Initial;
    private bool _disposed;

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
#if NETSTANDARD2_0
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(targetHost);
        config.Validate();
#else
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _targetHost = targetHost ?? throw new ArgumentNullException(nameof(targetHost));
        _config.Validate();
#endif
    }

    /// <inheritdoc />
    public string MechanismName => "GSSAPI";

    /// <inheritdoc />
    public bool IsComplete => _state == GssapiState.Complete;

    /// <inheritdoc />
    public byte[] GetInitialResponse()
    {
#if NETSTANDARD2_0
        throw new PlatformNotSupportedException(
            "SASL GSSAPI requires System.Net.Security.NegotiateAuthentication, which is not available on netstandard2.0.");
#else
        if (_state != GssapiState.Initial)
        {
            throw new InvalidOperationException("GetInitialResponse can only be called once");
        }

        // Mark state early to prevent re-entry even if authentication fails
        _state = GssapiState.TokenExchange;

        _config.ApplyKeytabEnvironment();

        // NegotiateAuthentication automatically uses GSSAPI on Unix and SSPI on Windows.
        _auth = new(_config.CreateClientOptions(_targetHost));

        // Get the initial token
        var outgoingBlob = _auth.GetOutgoingBlob(ReadOnlySpan<byte>.Empty, out var statusCode);

        if (statusCode != NegotiateAuthenticationStatusCode.ContinueNeeded &&
            statusCode != NegotiateAuthenticationStatusCode.Completed)
        {
            throw new AuthenticationException($"GSSAPI initial token generation failed: {statusCode}");
        }

        if (statusCode == NegotiateAuthenticationStatusCode.Completed)
        {
            _state = GssapiState.Complete;
        }

        return outgoingBlob ?? [];
#endif
    }

    /// <inheritdoc />
    public byte[]? EvaluateChallenge(byte[] challenge)
    {
#if NETSTANDARD2_0
        throw new PlatformNotSupportedException(
            "SASL GSSAPI requires System.Net.Security.NegotiateAuthentication, which is not available on netstandard2.0.");
#else
        if (_auth is null)
        {
            throw new InvalidOperationException("GetInitialResponse must be called before EvaluateChallenge");
        }

        if (_state == GssapiState.Complete)
        {
            return null;
        }

        var outgoingBlob = _auth.GetOutgoingBlob(challenge, out var statusCode);

        if (statusCode == NegotiateAuthenticationStatusCode.Completed)
        {
            _state = GssapiState.Complete;
            // Return any final token if present, otherwise null to indicate completion
            return outgoingBlob is { Length: > 0 } ? outgoingBlob : null;
        }

        if (statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
        {
            return outgoingBlob ?? [];
        }

        throw new AuthenticationException($"GSSAPI authentication failed: {statusCode}");
#endif
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

#if !NETSTANDARD2_0
        _auth?.Dispose();
#endif
        _disposed = true;
    }
}
