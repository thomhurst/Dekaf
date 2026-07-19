using System.Security.Cryptography;
using System.Text.Json;

namespace Dekaf.Security.Sasl;

/// <summary>
/// Configures RFC 7523 private-key JWT client authentication for the
/// <c>client_credentials</c> grant.
/// </summary>
public sealed class OAuthBearerClientAssertionOptions
{
    /// <summary>OAuth token endpoint that receives the client assertion.</summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>OAuth client identifier; defaults the assertion issuer and subject.</summary>
    public string? ClientId { get; set; }

    /// <summary>RSA or ECDSA private key used to sign each assertion.</summary>
    public AsymmetricAlgorithm? PrivateKey { get; set; }

    /// <summary>Intended audience for the assertion.</summary>
    public string? Audience { get; set; }

    /// <summary>Assertion issuer; defaults to <see cref="ClientId"/>.</summary>
    public string? Issuer { get; set; }

    /// <summary>Assertion subject; defaults to <see cref="ClientId"/>.</summary>
    public string? Subject { get; set; }

    /// <summary>Optional key identifier written to the JWT header.</summary>
    public string? KeyId { get; set; }

    /// <summary>Scopes sent as a space-delimited token request parameter.</summary>
    public IReadOnlyList<string>? Scopes { get; set; }

    /// <summary>
    /// Additional AOT-safe JWT claims. Supported values match
    /// <see cref="OAuthBearerJwtBearerOptions.AdditionalClaims"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? AdditionalClaims { get; set; }

    /// <summary>Additional non-reserved form parameters for the token request.</summary>
    public IReadOnlyDictionary<string, string>? AdditionalParameters { get; set; }

    /// <summary>Assertion lifetime. Defaults to five minutes.</summary>
    public TimeSpan AssertionLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Seconds before token expiration that trigger refresh.</summary>
    public int TokenRefreshBufferSeconds { get; set; } = 60;

    /// <summary>Signing algorithm; inferred from <see cref="PrivateKey"/> when omitted.</summary>
    public OAuthBearerJwtSigningAlgorithm? SigningAlgorithm { get; set; }

    internal OAuthBearerConfig ToOAuthBearerConfig()
    {
        Validate();
        var options = Clone();
        return new OAuthBearerConfig
        {
            GrantType = OAuthBearerGrantType.ClientCredentials,
            TokenEndpointUrl = options.TokenEndpoint!,
            ClientId = options.ClientId!,
            Scope = options.Scopes is { Count: > 0 } ? string.Join(" ", options.Scopes) : null,
            AdditionalParameters = options.AdditionalParameters,
            ClientAssertion = options,
            TokenRefreshBufferSeconds = options.TokenRefreshBufferSeconds
        };
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(TokenEndpoint))
            throw new InvalidOperationException("OAuth client-assertion token endpoint is required");
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("OAuth client-assertion client ID is required");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("OAuth client-assertion audience is required");
        if (PrivateKey is null)
            throw new InvalidOperationException("OAuth client-assertion private key is required");
        if (AssertionLifetime <= TimeSpan.Zero)
            throw new InvalidOperationException("OAuth client-assertion lifetime must be positive");
        if (TokenRefreshBufferSeconds < 0)
            throw new InvalidOperationException("OAuth client-assertion token refresh buffer must be non-negative");

        ValidateAdditionalParameters(AdditionalParameters);
    }

    internal static void ValidateAdditionalParameters(
        IReadOnlyDictionary<string, string>? additionalParameters)
    {
        if (additionalParameters is not null)
        {
            foreach (var name in additionalParameters.Keys)
            {
                if (name is "grant_type"
                    or "client_id"
                    or "client_secret"
                    or "client_assertion_type"
                    or "client_assertion")
                {
                    throw new InvalidOperationException(
                        $"OAuth client-assertion additional parameter '{name}' is reserved");
                }
            }
        }
    }

    private OAuthBearerClientAssertionOptions Clone() => new()
    {
        TokenEndpoint = TokenEndpoint,
        ClientId = ClientId,
        PrivateKey = PrivateKey,
        Audience = Audience,
        Issuer = Issuer,
        Subject = Subject,
        KeyId = KeyId,
        Scopes = Scopes is null ? null : Scopes.ToArray(),
        AdditionalClaims = AdditionalClaims is null
            ? null
            : AdditionalClaims.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        AdditionalParameters = AdditionalParameters is null
            ? null
            : AdditionalParameters.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        AssertionLifetime = AssertionLifetime,
        TokenRefreshBufferSeconds = TokenRefreshBufferSeconds,
        SigningAlgorithm = SigningAlgorithm
    };
}
