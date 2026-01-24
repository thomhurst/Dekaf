namespace Dekaf.Security.Sasl;

/// <summary>
/// Represents an OAuth 2.0 / OpenID Connect bearer token for SASL authentication.
/// </summary>
public sealed class OAuthBearerToken
{
    /// <summary>
    /// The raw JWT or opaque access token value.
    /// </summary>
    public required string TokenValue { get; init; }

    /// <summary>
    /// The time at which this token expires.
    /// </summary>
    public required DateTimeOffset Expiration { get; init; }

    /// <summary>
    /// The principal name associated with the token (typically the subject claim).
    /// </summary>
    public required string PrincipalName { get; init; }

    /// <summary>
    /// Optional SASL extensions to include in the authentication request.
    /// These are sent as key=value pairs in the initial client response.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Extensions { get; init; }

    /// <summary>
    /// Returns true if the token has expired or is about to expire within the specified buffer.
    /// </summary>
    /// <param name="bufferSeconds">Number of seconds before actual expiration to consider the token expired.</param>
    public bool IsExpired(int bufferSeconds = 0) =>
        DateTimeOffset.UtcNow.AddSeconds(bufferSeconds) >= Expiration;
}
