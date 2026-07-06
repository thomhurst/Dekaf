using System.Net.Http.Headers;
using System.Text;
using Dekaf.Security.Sasl;

namespace Dekaf.SchemaRegistry;

internal sealed class SchemaRegistryAuthenticationHandler : DelegatingHandler
{
    private readonly AuthenticationHeaderValue? _staticAuthorization;
    private readonly OAuthBearerAuthenticator? _oauthAuthenticator;
    private readonly OAuthBearerTokenProvider? _ownedTokenProvider;

    internal SchemaRegistryAuthenticationHandler(
        HttpMessageHandler innerHandler,
        SchemaRegistryConfig config,
        Func<OAuthBearerConfig, Func<CancellationToken, ValueTask<OAuthBearerToken>>>? oauthBearerTokenProviderFactory = null)
        : base(innerHandler)
    {
        if (config.OAuthBearerTokenProvider is not null)
        {
            _oauthAuthenticator = new OAuthBearerAuthenticator(config.OAuthBearerTokenProvider);
        }
        else if (!string.IsNullOrEmpty(config.BearerAuthToken))
        {
            _staticAuthorization = new AuthenticationHeaderValue("Bearer", config.BearerAuthToken);
        }
        else if (config.OAuthBearerConfig is not null)
        {
            Func<CancellationToken, ValueTask<OAuthBearerToken>> tokenProvider;
            if (oauthBearerTokenProviderFactory is not null)
            {
                tokenProvider = oauthBearerTokenProviderFactory(config.OAuthBearerConfig);
            }
            else
            {
                _ownedTokenProvider = new OAuthBearerTokenProvider(config.OAuthBearerConfig);
                tokenProvider = _ownedTokenProvider.GetTokenAsync;
            }

            _oauthAuthenticator = new OAuthBearerAuthenticator(tokenProvider);
        }
        else if (!string.IsNullOrEmpty(config.BasicAuthUserInfo))
        {
            var authBytes = Encoding.UTF8.GetBytes(config.BasicAuthUserInfo);
            _staticAuthorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_oauthAuthenticator is not null)
        {
            var token = await _oauthAuthenticator.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.TokenValue);
        }
        else if (_staticAuthorization is not null)
        {
            request.Headers.Authorization = _staticAuthorization;
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ownedTokenProvider?.Dispose();
        }

        base.Dispose(disposing);
    }
}
