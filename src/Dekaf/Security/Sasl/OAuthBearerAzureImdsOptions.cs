namespace Dekaf.Security.Sasl;

/// <summary>
/// Azure Instance Metadata Service managed identity options for OAUTHBEARER token acquisition.
/// </summary>
public sealed class OAuthBearerAzureImdsOptions
{
    /// <summary>
    /// Default Azure IMDS managed identity token endpoint.
    /// </summary>
    public const string DefaultTokenEndpoint = "http://169.254.169.254/metadata/identity/oauth2/token";

    /// <summary>
    /// Default Azure IMDS API version for managed identity tokens.
    /// </summary>
    public const string DefaultApiVersion = "2018-02-01";

    /// <summary>
    /// Azure IMDS token endpoint. Defaults to the link-local metadata endpoint.
    /// </summary>
    public string TokenEndpoint { get; set; } = DefaultTokenEndpoint;

    /// <summary>
    /// Azure IMDS API version. Defaults to 2018-02-01.
    /// </summary>
    public string ApiVersion { get; set; } = DefaultApiVersion;

    /// <summary>
    /// App ID URI of the target resource, for example https://management.azure.com/.
    /// </summary>
    public required string Resource { get; set; }

    /// <summary>
    /// Optional client ID for a user-assigned managed identity. Omit for system-assigned identity.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The number of seconds before token expiration to trigger a refresh.
    /// </summary>
    public int TokenRefreshBufferSeconds { get; set; } = 60;

    /// <summary>
    /// Converts this options object to the shared OAuth bearer configuration model.
    /// </summary>
    internal OAuthBearerConfig ToOAuthBearerConfig()
    {
        Validate();
        return new OAuthBearerConfig
        {
            GrantType = OAuthBearerGrantType.AzureImds,
            TokenEndpointUrl = TokenEndpoint,
            ClientId = ClientId ?? string.Empty,
            AzureImds = Clone(),
            TokenRefreshBufferSeconds = TokenRefreshBufferSeconds
        };
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(TokenEndpoint))
            throw new InvalidOperationException("Azure IMDS token endpoint is required.");
        if (!Uri.TryCreate(TokenEndpoint, UriKind.Absolute, out _))
            throw new InvalidOperationException("Azure IMDS token endpoint must be an absolute URI.");
        if (string.IsNullOrWhiteSpace(ApiVersion))
            throw new InvalidOperationException("Azure IMDS API version is required.");
        if (string.IsNullOrWhiteSpace(Resource))
            throw new InvalidOperationException("Azure IMDS resource is required.");
        if (TokenRefreshBufferSeconds < 0)
            throw new InvalidOperationException("Token refresh buffer must be non-negative.");
    }

    private OAuthBearerAzureImdsOptions Clone() => new()
    {
        TokenEndpoint = TokenEndpoint,
        ApiVersion = ApiVersion,
        Resource = Resource,
        ClientId = ClientId,
        TokenRefreshBufferSeconds = TokenRefreshBufferSeconds
    };
}
