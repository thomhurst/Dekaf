using System.Text;

namespace Dekaf.Security.Sasl;

/// <summary>
/// SASL OAUTHBEARER mechanism authenticator.
/// Implements RFC 7628 - A Set of Simple Authentication and Security Layer (SASL) Mechanisms for OAuth.
/// </summary>
public sealed class OAuthBearerAuthenticator : ISaslAuthenticator
{
    private readonly Func<CancellationToken, ValueTask<OAuthBearerToken>> _tokenProvider;
    private OAuthBearerToken? _currentToken;
    private bool _complete;

    /// <summary>
    /// Creates a new OAUTHBEARER authenticator with a static token.
    /// </summary>
    /// <param name="token">The OAuth bearer token to use.</param>
    public OAuthBearerAuthenticator(OAuthBearerToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
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
        if (_currentToken is null || _currentToken.IsExpired(bufferSeconds: 60))
        {
            _currentToken = await _tokenProvider(cancellationToken).ConfigureAwait(false);
        }
        return _currentToken;
    }

    /// <inheritdoc />
    public byte[] GetInitialResponse()
    {
        // For synchronous interface, we need to have the token already
        // The async path should have called GetTokenAsync first
        if (_currentToken is null)
        {
            throw new InvalidOperationException(
                "Token not available. Call GetTokenAsync before authentication for dynamic token providers.");
        }

        if (_currentToken.IsExpired())
        {
            throw new AuthenticationException("OAuth token has expired");
        }

        // OAUTHBEARER initial client response format (RFC 7628):
        // gs2-header kvsep *kvpair kvsep
        // where:
        //   gs2-header = "n,," (no channel binding, no authzid)
        //   kvsep = 0x01 (ASCII SOH)
        //   kvpair = key "=" value
        //
        // Required kvpair: auth=Bearer <token>
        // Format: n,,\x01auth=Bearer <token>\x01\x01
        //
        // If extensions are present, they are added as additional kvpairs:
        // n,,\x01auth=Bearer <token>\x01ext1=val1\x01ext2=val2\x01\x01

        var builder = new StringBuilder();

        // GS2 header: "n,," means no channel binding and no authorization identity
        builder.Append("n,,");

        // SOH separator
        builder.Append('\x01');

        // Authorization header with bearer token
        builder.Append("auth=Bearer ");
        builder.Append(_currentToken.TokenValue);

        // Add any extensions
        if (_currentToken.Extensions is { Count: > 0 })
        {
            foreach (var (key, value) in _currentToken.Extensions)
            {
                builder.Append('\x01');
                builder.Append(key);
                builder.Append('=');
                builder.Append(value);
            }
        }

        // Final SOH terminator (kvsep at end per RFC 7628)
        builder.Append('\x01');

        _complete = true;
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
            if (errorResponse.StartsWith('{'))
            {
                throw new AuthenticationException($"OAUTHBEARER authentication failed: {errorResponse}");
            }
        }

        _complete = true;
        return null;
    }
}
