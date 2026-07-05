using System.Text;
using Dekaf.Errors;

namespace Dekaf.Security.Sasl;

/// <summary>
/// SASL OAUTHBEARER mechanism authenticator.
/// Implements RFC 7628 - A Set of Simple Authentication and Security Layer (SASL) Mechanisms for OAuth.
/// </summary>
public sealed class OAuthBearerAuthenticator : ISaslAuthenticator
{
    private readonly Func<CancellationToken, ValueTask<OAuthBearerToken>> _tokenProvider;
    private readonly object _tokenLock = new();
    private OAuthBearerToken? _currentToken;
    private bool _complete;

    /// <summary>
    /// Creates a new OAUTHBEARER authenticator with a static token.
    /// </summary>
    /// <param name="token">The OAuth bearer token to use.</param>
    public OAuthBearerAuthenticator(OAuthBearerToken token)
    {
        CompatibilityThrowHelpers.ThrowIfNull(token);
        _currentToken = token;
        _tokenProvider = _ => new ValueTask<OAuthBearerToken>(token);
    }

    /// <summary>
    /// Creates a new OAUTHBEARER authenticator with a token provider for dynamic token retrieval.
    /// </summary>
    /// <param name="tokenProvider">The function that provides OAuth tokens on demand.</param>
    public OAuthBearerAuthenticator(Func<CancellationToken, ValueTask<OAuthBearerToken>> tokenProvider)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    /// <inheritdoc />
    public string MechanismName => "OAUTHBEARER";

    /// <inheritdoc />
    public bool IsComplete => _complete;

    /// <summary>
    /// Gets the current token, fetching a new one if needed.
    /// </summary>
    public async ValueTask<OAuthBearerToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        OAuthBearerToken? cachedToken;
        lock (_tokenLock)
        {
            cachedToken = _currentToken;
        }

        if (cachedToken is null || cachedToken.IsExpired(bufferSeconds: 60))
        {
            var newToken = await _tokenProvider(cancellationToken).ConfigureAwait(false);
            lock (_tokenLock)
            {
                _currentToken = newToken;
                cachedToken = newToken;
            }
        }
        return cachedToken;
    }

    /// <inheritdoc />
    public byte[] GetInitialResponse()
    {
        OAuthBearerToken token;
        lock (_tokenLock)
        {
            // For synchronous interface, we need to have the token already
            // The async path should have called GetTokenAsync first
            if (_currentToken is null)
            {
                throw new InvalidOperationException(
                    "Token not available. Call GetTokenAsync before authentication for dynamic token providers.");
            }
            token = _currentToken;
        }

        if (token.IsExpired())
        {
            throw new AuthenticationException("OAuth token has expired");
        }

        // OAUTHBEARER initial client response format (RFC 7628):
        //   client-resp = gs2-header kvsep *kvpair kvsep
        //   kvpair      = key "=" value kvsep      (note: each kvpair carries its own trailing kvsep)
        //   gs2-header  = "n,,"                     (no channel binding, no authzid)
        //   kvsep       = 0x01 (ASCII SOH)
        //
        // With only the required auth pair this yields:
        //   n,,\x01auth=Bearer <token>\x01\x01
        // The final \x01 is the empty kvsep that terminates the (possibly extended) kvpair list.
        //
        // With extensions:
        //   n,,\x01auth=Bearer <token>\x01ext1=val1\x01ext2=val2\x01\x01

        var builder = new StringBuilder();

        // GS2 header: "n,," means no channel binding and no authorization identity
        builder.Append("n,,");

        // SOH separating the GS2 header from the key/value pairs
        builder.Append('\x01');

        // Authorization key/value pair, terminated by its own SOH
        builder.Append("auth=Bearer ");
        builder.Append(token.TokenValue);
        builder.Append('\x01');

        // Extension key/value pairs, each terminated by SOH
        if (token.Extensions is { Count: > 0 })
        {
            foreach (var (key, value) in token.Extensions)
            {
                builder.Append(key);
                builder.Append('=');
                builder.Append(value);
                builder.Append('\x01');
            }
        }

        // Final SOH terminating the kvpair list (the trailing kvsep in client-resp)
        builder.Append('\x01');

        // Note: do NOT mark complete here. OAUTHBEARER is a single round trip, but the client
        // is only authenticated once the server accepts the response. On failure the server
        // replies with a JSON error challenge (RFC 7628), which EvaluateChallenge must observe;
        // completing here would cause the caller to stop before reading that challenge.
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <inheritdoc />
    public byte[]? EvaluateChallenge(byte[] challenge)
    {
        // OAUTHBEARER is a single-round mechanism
        // If we receive a challenge, it's an error response from the server
        // The error will be in JSON format as per RFC 7628
        if (challenge.Length > 0)
        {
            var errorResponse = Encoding.UTF8.GetString(challenge);

            // If the response starts with '{', it's a JSON error
            if (StartsWithJsonObject(errorResponse))
            {
                throw new AuthenticationException($"OAUTHBEARER authentication failed: {errorResponse}");
            }
        }

        _complete = true;
        return null;
    }

    private static bool StartsWithJsonObject(string value)
    {
#if NETSTANDARD2_0
        return value.StartsWith("{", StringComparison.Ordinal);
#else
        return value.StartsWith('{');
#endif
    }
}
