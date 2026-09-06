using System.Net.Http.Headers;
using System.Text;
using Dekaf.Security.Sasl;

namespace Dekaf.SchemaRegistry;

internal sealed class SchemaRegistryAuthenticationHandler : DelegatingHandler
{
    private readonly AuthenticationHeaderValue? _staticAuthorization;
    private readonly OAuthBearerAuthenticator? _customTokenAuthenticator;
    private readonly Func<CancellationToken, ValueTask<OAuthBearerToken>>? _configuredTokenProvider;
    private readonly OAuthBearerTokenProvider? _ownedTokenProvider;

    internal SchemaRegistryAuthenticationHandler(
        HttpMessageHandler innerHandler,
        SchemaRegistryConfig config,
        Func<OAuthBearerConfig, Func<CancellationToken, ValueTask<OAuthBearerToken>>>? oauthBearerTokenProviderFactory = null)
        : base(innerHandler)
    {
        if (config.OAuthBearerTokenProvider is not null)
        {
            _customTokenAuthenticator = new OAuthBearerAuthenticator(config.OAuthBearerTokenProvider);
        }
        else if (!string.IsNullOrEmpty(config.BearerAuthToken))
        {
            _staticAuthorization = new AuthenticationHeaderValue("Bearer", config.BearerAuthToken);
        }
        else if (config.OAuthBearerConfig is not null)
        {
            // The configured provider owns caching and its refresh window. An authenticator's
            // separate fixed window would hide an earlier configured refresh deadline.
            if (oauthBearerTokenProviderFactory is not null)
            {
                _configuredTokenProvider = oauthBearerTokenProviderFactory(config.OAuthBearerConfig);
            }
            else
            {
                _ownedTokenProvider = new OAuthBearerTokenProvider(config.OAuthBearerConfig);
                _configuredTokenProvider = _ownedTokenProvider.GetTokenAsync;
            }
        }
        else if (!string.IsNullOrEmpty(config.BasicAuthUserInfo))
        {
            var authBytes = Encoding.UTF8.GetBytes(config.BasicAuthUserInfo);
            _staticAuthorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_customTokenAuthenticator is not null || _configuredTokenProvider is not null)
        {
            var token = _customTokenAuthenticator is not null
                ? _customTokenAuthenticator.GetTokenAsync(cancellationToken)
                : _configuredTokenProvider!(cancellationToken);
            if (!token.IsCompletedSuccessfully)
                return SendWithTokenAsync(request, token, cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Result.TokenValue);
        }
        else if (_staticAuthorization is not null)
        {
            request.Headers.Authorization = _staticAuthorization;
        }

        return base.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpRequestMessage request,
        ValueTask<OAuthBearerToken> pendingToken,
        CancellationToken cancellationToken)
    {
        var token = await pendingToken.ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.TokenValue);
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
