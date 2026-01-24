using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Dekaf.Security.Sasl;

/// <summary>
/// OAuth 2.0 token provider that fetches tokens from a token endpoint using client credentials grant.
/// </summary>
public sealed class OAuthBearerTokenProvider : IDisposable
{
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
        : this(config, new HttpClient(), ownsHttpClient: true)
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

    private OAuthBearerTokenProvider(OAuthBearerConfig config, HttpClient httpClient, bool ownsHttpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;
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

    private Dictionary<string, string> BuildTokenRequestBody()
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _config.ClientId
        };

        if (!string.IsNullOrEmpty(_config.Scope))
        {
            body["scope"] = _config.Scope;
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
        if (root.TryGetProperty("expires_in", out var expiresInElement) && expiresInElement.TryGetInt32(out var expiresIn))
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

    private static string ExtractPrincipalName(string accessToken, JsonElement tokenResponse)
    {
        // First, try to get from token response (some providers include it)
        if (tokenResponse.TryGetProperty("sub", out var subElement))
        {
            var sub = subElement.GetString();
            if (!string.IsNullOrEmpty(sub))
                return sub;
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
                foreach (var claim in new[] { "sub", "azp", "client_id" })
                {
                    if (payloadRoot.TryGetProperty(claim, out var claimElement))
                    {
                        var value = claimElement.GetString();
                        if (!string.IsNullOrEmpty(value))
                            return value;
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
