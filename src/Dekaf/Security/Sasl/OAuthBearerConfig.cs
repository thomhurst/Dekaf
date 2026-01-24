namespace Dekaf.Security.Sasl;

/// <summary>
/// Configuration for OAuth 2.0 / OpenID Connect authentication using the OAUTHBEARER SASL mechanism.
/// </summary>
public sealed class OAuthBearerConfig
{
    /// <summary>
    /// The OAuth 2.0 / OIDC token endpoint URL for obtaining access tokens.
    /// </summary>
    public required string TokenEndpointUrl { get; init; }

    /// <summary>
    /// The OAuth 2.0 client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The OAuth 2.0 client secret. May be null for public clients.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// The OAuth 2.0 scope(s) to request. Multiple scopes should be space-separated.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Additional parameters to include in the token request.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalParameters { get; init; }

    /// <summary>
    /// The number of seconds before token expiration to trigger a refresh.
    /// Default is 60 seconds.
    /// </summary>
    public int TokenRefreshBufferSeconds { get; init; } = 60;
}
