using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dekaf.Errors;

namespace Dekaf.Security.Sasl;

/// <summary>
/// OAuth 2.0 token provider that fetches tokens from a token endpoint.
/// </summary>
public sealed class OAuthBearerTokenProvider : IDisposable
{
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);
    private static readonly string[] TokenResponsePrincipalClaims = ["sub", "client_id", "object_id", "oid"];
    private static readonly string[] JwtPrincipalClaims = ["sub", "azp", "client_id", "appid", "oid"];

    private readonly OAuthBearerConfig _config;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private OAuthBearerToken? _cachedToken;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>
    /// Creates a new token provider with the specified configuration.
    /// </summary>
    /// <param name="config">The OAuth configuration.</param>
    public OAuthBearerTokenProvider(OAuthBearerConfig config)
        : this(config, CreateDefaultHttpClient(config), ownsHttpClient: true)
    {
    }

    /// <summary>
    /// Creates a new token provider with the specified configuration and HTTP client.
    /// </summary>
    /// <param name="config">The OAuth configuration.</param>
    /// <param name="httpClient">The HTTP client to use for token requests.</param>
    public OAuthBearerTokenProvider(OAuthBearerConfig config, HttpClient httpClient)
        : this(config, httpClient, ownsHttpClient: false)
    {
    }

    internal static HttpMessageHandler CreateHttpHandler(bool useProxy = true)
    {
#if NETSTANDARD2_0
        var handlerType = Type.GetType("System.Net.Http.SocketsHttpHandler, System.Net.Http");
        if (handlerType is not null && Activator.CreateInstance(handlerType) is HttpMessageHandler handler)
        {
            var lifetimeProperty = handlerType.GetProperty("PooledConnectionLifetime");
            if (lifetimeProperty is not null && lifetimeProperty.CanWrite)
            {
                lifetimeProperty.SetValue(handler, PooledConnectionLifetime);
                var useProxyProperty = handlerType.GetProperty("UseProxy");
                if (useProxyProperty is not null && useProxyProperty.CanWrite)
                    useProxyProperty.SetValue(handler, useProxy);

                return handler;
            }

            handler.Dispose();
        }

        return new HttpClientHandler { UseProxy = useProxy };
#else
        return new SocketsHttpHandler
        {
            PooledConnectionLifetime = PooledConnectionLifetime,
            UseProxy = useProxy
        };
#endif
    }

    private OAuthBearerTokenProvider(OAuthBearerConfig config, HttpClient httpClient, bool ownsHttpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;
    }

    private static HttpClient CreateDefaultHttpClient(OAuthBearerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var useProxy = config.GrantType != OAuthBearerGrantType.AzureImds;
        return new HttpClient(CreateHttpHandler(useProxy), disposeHandler: true);
    }

    /// <summary>
    /// Gets a valid OAuth token, fetching a new one if the cached token is expired or about to expire.
    /// </summary>
    public async ValueTask<OAuthBearerToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: return cached token if still valid
        if (_cachedToken is not null && !_cachedToken.IsExpired(_config.TokenRefreshBufferSeconds))
        {
            return _cachedToken;
        }

        // Slow path: fetch new token with synchronization
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_cachedToken is not null && !_cachedToken.IsExpired(_config.TokenRefreshBufferSeconds))
            {
                return _cachedToken;
            }

            _cachedToken = await FetchTokenAsync(cancellationToken).ConfigureAwait(false);
            return _cachedToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<OAuthBearerToken> FetchTokenAsync(CancellationToken cancellationToken)
    {
        return _config.GrantType == OAuthBearerGrantType.AzureImds
            ? await FetchAzureImdsTokenAsync(cancellationToken).ConfigureAwait(false)
            : await FetchOAuthTokenEndpointAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<OAuthBearerToken> FetchOAuthTokenEndpointAsync(CancellationToken cancellationToken)
    {
        var requestBody = BuildTokenRequestBody();

        using var request = new HttpRequestMessage(HttpMethod.Post, _config.TokenEndpointUrl)
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

        // If client secret is provided, use HTTP Basic authentication
        if (!string.IsNullOrEmpty(_config.ClientSecret))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationException(
                $"OAuth token request failed with status {response.StatusCode}: {responseContent}");
        }

        return ParseTokenResponse(responseContent);
    }

    private async Task<OAuthBearerToken> FetchAzureImdsTokenAsync(CancellationToken cancellationToken)
    {
        var options = _config.AzureImds
            ?? throw new InvalidOperationException("Azure IMDS OAuth grant requires AzureImds options");

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildAzureImdsTokenUri(options));
        request.Headers.TryAddWithoutValidation("Metadata", "true");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationException(
                $"Azure IMDS token request failed with status {response.StatusCode}: {responseContent}");
        }

        return ParseTokenResponse(responseContent);
    }

    private static Uri BuildAzureImdsTokenUri(OAuthBearerAzureImdsOptions options)
    {
        var endpoint = options.TokenEndpoint;
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var uri = new StringBuilder(endpoint)
            .Append(separator)
            .Append("api-version=").Append(Uri.EscapeDataString(options.ApiVersion))
            .Append("&resource=").Append(Uri.EscapeDataString(options.Resource));

        if (!string.IsNullOrWhiteSpace(options.ClientId))
            uri.Append("&client_id=").Append(Uri.EscapeDataString(options.ClientId!));

        return new Uri(uri.ToString(), UriKind.Absolute);
    }

    private Dictionary<string, string> BuildTokenRequestBody()
    {
        var body = _config.GrantType switch
        {
            OAuthBearerGrantType.ClientCredentials => BuildClientCredentialsTokenRequestBody(),
            OAuthBearerGrantType.JwtBearer => BuildJwtBearerTokenRequestBody(),
            _ => throw new InvalidOperationException($"Unsupported OAuth bearer grant type: {_config.GrantType}")
        };

        var scope = _config.Scope;
        if (!string.IsNullOrEmpty(scope))
        {
            body["scope"] = scope!;
        }

        if (_config.AdditionalParameters is not null)
        {
            foreach (var (key, value) in _config.AdditionalParameters)
            {
                body[key] = value;
            }
        }

        return body;
    }

    private Dictionary<string, string> BuildClientCredentialsTokenRequestBody()
    {
        return new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _config.ClientId
        };
    }

    private Dictionary<string, string> BuildJwtBearerTokenRequestBody()
    {
        if (_config.JwtBearer is null)
        {
            throw new InvalidOperationException("JWT-bearer OAuth grant requires JwtBearer options");
        }

        return new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["client_id"] = _config.ClientId,
            ["assertion"] = OAuthBearerJwtAssertion.Create(_config, DateTimeOffset.UtcNow)
        };
    }

    private static OAuthBearerToken ParseTokenResponse(string responseContent)
    {
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;

        if (!root.TryGetProperty("access_token", out var accessTokenElement))
        {
            throw new AuthenticationException("OAuth token response missing 'access_token' field");
        }

        var accessToken = accessTokenElement.GetString()
            ?? throw new AuthenticationException("OAuth token response 'access_token' is null");

        // Calculate expiration
        DateTimeOffset expiration;
        if (root.TryGetProperty("expires_in", out var expiresInElement)
            && TryGetExpiresInSeconds(expiresInElement, out var expiresIn))
        {
            expiration = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        }
        else
        {
            // Default to 1 hour if not specified
            expiration = DateTimeOffset.UtcNow.AddHours(1);
        }

        // Try to extract principal name from various claims
        var principalName = ExtractPrincipalName(accessToken, root);

        return new OAuthBearerToken
        {
            TokenValue = accessToken,
            Expiration = expiration,
            PrincipalName = principalName
        };
    }

    private static bool TryGetExpiresInSeconds(JsonElement expiresInElement, out int expiresIn)
    {
        if (expiresInElement.ValueKind == JsonValueKind.Number)
        {
            return expiresInElement.TryGetInt32(out expiresIn);
        }

        if (expiresInElement.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(expiresInElement.GetString(), out expiresIn);
        }

        expiresIn = 0;
        return false;
    }

    private static string ExtractPrincipalName(string accessToken, JsonElement tokenResponse)
    {
        // First, try to get from token response (some providers include it)
        foreach (var responseClaim in TokenResponsePrincipalClaims)
        {
            if (tokenResponse.TryGetProperty(responseClaim, out var responseClaimElement))
            {
                var value = responseClaimElement.GetString();
                if (!string.IsNullOrEmpty(value))
                    return value!;
            }
        }

        // Try to decode JWT and extract subject claim
        var parts = accessToken.Split('.');
        if (parts.Length == 3)
        {
            try
            {
                // Decode the payload (second part)
                var payload = parts[1];

                // Add padding if needed
                var paddingNeeded = payload.Length % 4;
                if (paddingNeeded > 0)
                {
                    payload += new string('=', 4 - paddingNeeded);
                }

                // Convert from base64url to base64
                payload = payload.Replace('-', '+').Replace('_', '/');

                var payloadBytes = Convert.FromBase64String(payload);
                using var payloadDoc = JsonDocument.Parse(payloadBytes);
                var payloadRoot = payloadDoc.RootElement;

                // Try 'sub' claim first, then 'azp' (authorized party), then 'client_id'
                foreach (var claim in JwtPrincipalClaims)
                {
                    if (payloadRoot.TryGetProperty(claim, out var claimElement))
                    {
                        var value = claimElement.GetString();
                        if (!string.IsNullOrEmpty(value))
                            return value!;
                    }
                }
            }
            catch
            {
                // JWT parsing failed, fall through to default
            }
        }

        // Default to "unknown" if we can't determine the principal
        return "unknown";
    }

    /// <summary>
    /// Disposes the token provider and its resources.
    /// </summary>
    public void Dispose()
    {
        _refreshLock.Dispose();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
