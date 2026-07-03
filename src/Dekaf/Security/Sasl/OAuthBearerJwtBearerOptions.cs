namespace Dekaf.Security.Sasl;

using System.Security.Cryptography;

/// <summary>
/// Configuration for OAuth 2.0 JWT-bearer assertion grants used with SASL/OAUTHBEARER.
/// </summary>
public sealed class OAuthBearerJwtBearerOptions
{
    /// <summary>
    /// OAuth token endpoint that receives the JWT assertion.
    /// </summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>
    /// OAuth client identifier. Used as <c>client_id</c> in the token request and as the
    /// default JWT issuer and subject.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Private key used to sign the assertion. RSA and ECDSA keys are supported.
    /// </summary>
    public AsymmetricAlgorithm? PrivateKey { get; set; }

    /// <summary>
    /// Intended audience for the JWT assertion.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// JWT issuer claim. Defaults to <see cref="ClientId"/>.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// JWT subject claim. Defaults to <see cref="ClientId"/>.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Optional key identifier to include in the JWT header.
    /// </summary>
    public string? KeyId { get; set; }

    /// <summary>
    /// Scopes to request. Values are sent as a space-delimited <c>scope</c> token request parameter.
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; set; }

    /// <summary>
    /// Additional JWT claims to include in the assertion.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? AdditionalClaims { get; set; }

    /// <summary>
    /// Additional form parameters to include in the token request.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalParameters { get; set; }

    /// <summary>
    /// Assertion lifetime. Defaults to five minutes.
    /// </summary>
    public TimeSpan AssertionLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Number of seconds before token expiration to trigger a refresh.
    /// </summary>
    public int TokenRefreshBufferSeconds { get; set; } = 60;

    /// <summary>
    /// Signing algorithm. When unset, RSA keys use RS256 and ECDSA keys infer ES256/ES384/ES512
    /// from key size.
    /// </summary>
    public OAuthBearerJwtSigningAlgorithm? SigningAlgorithm { get; set; }

    internal OAuthBearerConfig ToOAuthBearerConfig()
    {
        if (string.IsNullOrWhiteSpace(TokenEndpoint))
            throw new InvalidOperationException("JWT-bearer OAuth token endpoint is required");
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("JWT-bearer OAuth client ID is required");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("JWT-bearer OAuth audience is required");
        if (PrivateKey is null)
            throw new InvalidOperationException("JWT-bearer OAuth private key is required");
        if (AssertionLifetime <= TimeSpan.Zero)
            throw new InvalidOperationException("JWT-bearer assertion lifetime must be positive");
        if (TokenRefreshBufferSeconds < 0)
            throw new InvalidOperationException("JWT-bearer token refresh buffer must be non-negative");

        var options = Clone();
        return new OAuthBearerConfig
        {
            GrantType = OAuthBearerGrantType.JwtBearer,
            TokenEndpointUrl = options.TokenEndpoint!,
            ClientId = options.ClientId!,
            Scope = options.Scopes is { Count: > 0 } ? string.Join(' ', options.Scopes) : null,
            AdditionalParameters = options.AdditionalParameters,
            JwtBearer = options,
            TokenRefreshBufferSeconds = TokenRefreshBufferSeconds
        };
    }

    private OAuthBearerJwtBearerOptions Clone() => new()
    {
        TokenEndpoint = TokenEndpoint,
        ClientId = ClientId,
        PrivateKey = PrivateKey,
        Audience = Audience,
        Issuer = Issuer,
        Subject = Subject,
        KeyId = KeyId,
        Scopes = Scopes is null ? null : Scopes.ToArray(),
        AdditionalClaims = AdditionalClaims is null ? null : new Dictionary<string, object?>(AdditionalClaims, StringComparer.Ordinal),
        AdditionalParameters = AdditionalParameters is null ? null : new Dictionary<string, string>(AdditionalParameters, StringComparer.Ordinal),
        AssertionLifetime = AssertionLifetime,
        TokenRefreshBufferSeconds = TokenRefreshBufferSeconds,
        SigningAlgorithm = SigningAlgorithm
    };
}

/// <summary>
/// JWS signing algorithms supported for OAuth JWT-bearer assertions.
/// </summary>
public enum OAuthBearerJwtSigningAlgorithm
{
    /// <summary>RSA PKCS#1 v1.5 with SHA-256.</summary>
    Rs256,

    /// <summary>RSA PKCS#1 v1.5 with SHA-384.</summary>
    Rs384,

    /// <summary>RSA PKCS#1 v1.5 with SHA-512.</summary>
    Rs512,

    /// <summary>RSA-PSS with SHA-256.</summary>
    Ps256,

    /// <summary>RSA-PSS with SHA-384.</summary>
    Ps384,

    /// <summary>RSA-PSS with SHA-512.</summary>
    Ps512,

    /// <summary>ECDSA with SHA-256.</summary>
    Es256,

    /// <summary>ECDSA with SHA-384.</summary>
    Es384,

    /// <summary>ECDSA with SHA-512.</summary>
    Es512
}
