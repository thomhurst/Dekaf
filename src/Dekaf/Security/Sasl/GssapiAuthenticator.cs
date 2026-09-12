#if !NETSTANDARD2_0
using System.Net.Security;
using System.Buffers;
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
/// 4. Exchange the integrity-protected RFC 4752 security-layer offer and selection
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
        SecurityLayer,
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
            _state = GssapiState.SecurityLayer;
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

        if (_state == GssapiState.SecurityLayer)
        {
            var unwrapped = new ArrayBufferWriter<byte>(4);
            var unwrapStatus = _auth.Unwrap(challenge, unwrapped, out _);
            if (unwrapStatus != NegotiateAuthenticationStatusCode.Completed)
                throw new AuthenticationException($"GSSAPI security-layer offer verification failed: {unwrapStatus}");
            ValidateSecurityLayerOffer(unwrapped.WrittenSpan);

            // Kafka uses SASL for authentication only. Select no post-authentication
            // security layer and a zero receive buffer; omit the authorization identity.
            var wrapped = new ArrayBufferWriter<byte>();
            var wrapStatus = _auth.Wrap([1, 0, 0, 0], wrapped, requestEncryption: false, out var encrypted);
            if (wrapStatus != NegotiateAuthenticationStatusCode.Completed || encrypted)
                throw new AuthenticationException($"GSSAPI security-layer selection failed: {wrapStatus}");
            _state = GssapiState.Complete;
            return wrapped.WrittenSpan.ToArray();
        }

        var outgoingBlob = _auth.GetOutgoingBlob(challenge, out var statusCode);

        if (statusCode == NegotiateAuthenticationStatusCode.Completed)
        {
            _state = GssapiState.SecurityLayer;
            // Even an empty final context token must be sent to obtain the server's
            // security-layer offer. Kerberos context completion is not SASL completion.
            return outgoingBlob ?? [];
        }

        if (statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
        {
            return outgoingBlob ?? [];
        }

        throw new AuthenticationException($"GSSAPI authentication failed: {statusCode}");
#endif
    }

    internal static void ValidateSecurityLayerOffer(ReadOnlySpan<byte> offer)
    {
        if (offer.Length != 4)
            throw new AuthenticationException("GSSAPI security-layer offer must contain exactly four bytes.");
        if ((offer[0] & 1) == 0)
            throw new AuthenticationException("GSSAPI server does not support authentication without a security layer.");
        // Kafka's Java SASL server can advertise a nonzero buffer even for auth-only.
        // We do not use its buffer limit: our selected security layer and receive buffer
        // are always 1 (no layer) and zero respectively.
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
