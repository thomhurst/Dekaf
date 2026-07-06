namespace Dekaf.Security.Sasl;

/// <summary>
/// Configuration for OAuth 2.0 / OpenID Connect authentication using the OAUTHBEARER SASL mechanism.
/// </summary>
public sealed class OAuthBearerConfig
{
    /// <summary>
    /// OAuth grant type to use when fetching access tokens.
    /// </summary>
    public OAuthBearerGrantType GrantType { get; init; } = OAuthBearerGrantType.ClientCredentials;

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
    /// JWT-bearer assertion settings. Required when <see cref="GrantType"/> is
    /// <see cref="OAuthBearerGrantType.JwtBearer"/>.
    /// </summary>
    public OAuthBearerJwtBearerOptions? JwtBearer { get; init; }

    /// <summary>
    /// Azure IMDS managed identity settings. Required when <see cref="GrantType"/> is
    /// <see cref="OAuthBearerGrantType.AzureImds"/>.
    /// </summary>
    public OAuthBearerAzureImdsOptions? AzureImds { get; init; }

    /// <summary>
    /// The number of seconds before token expiration to trigger a refresh.
    /// Default is 60 seconds.
    /// </summary>
    public int TokenRefreshBufferSeconds { get; init; } = 60;
}

/// <summary>
/// OAuth grant types supported by the built-in OAUTHBEARER token provider.
/// </summary>
public enum OAuthBearerGrantType
{
    /// <summary>
    /// OAuth 2.0 client credentials grant.
    /// </summary>
    ClientCredentials,

    /// <summary>
    /// OAuth 2.0 JWT bearer assertion grant.
    /// </summary>
    JwtBearer,

    /// <summary>
    /// Azure Instance Metadata Service managed identity token source.
    /// </summary>
    AzureImds
}
