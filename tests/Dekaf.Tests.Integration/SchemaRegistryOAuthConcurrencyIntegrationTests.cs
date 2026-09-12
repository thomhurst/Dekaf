using System.Collections.Concurrent;
using Dekaf.SchemaRegistry;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Integration;

[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
[Category("Serialization")]
public sealed class SchemaRegistryOAuthConcurrencyIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task ConcurrentRegistryRequests_ShareOneCustomTokenRefresh()
    {
        var response = new TaskCompletionSource<OAuthBearerToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerCalls = 0;
        using var handler = new AuthorizationCaptureHandler(new SocketsHttpHandler());
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl,
            OAuthBearerTokenProvider = cancellationToken =>
            {
                Interlocked.Increment(ref providerCalls);
                return new ValueTask<OAuthBearerToken>(response.Task.WaitAsync(cancellationToken));
            }
        }, handler);
        var requests = new Task[16];
        for (var index = 0; index < requests.Length; index++)
            requests[index] = client.GetAllSubjectsAsync();
        var callsBeforeCompletion = Volatile.Read(ref providerCalls);
        var sentBeforeCompletion = handler.Tokens.Count;
        response.SetResult(new OAuthBearerToken
        {
            TokenValue = "shared-custom-token",
            PrincipalName = "integration-test",
            Expiration = DateTimeOffset.UtcNow.AddHours(1)
        });
        await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30));

        await Assert.That(callsBeforeCompletion).IsEqualTo(1);
        await Assert.That(providerCalls).IsEqualTo(1);
        await Assert.That(sentBeforeCompletion).IsEqualTo(0);
        await Assert.That(handler.Tokens.Count).IsEqualTo(requests.Length);
        foreach (var token in handler.Tokens)
            await Assert.That(token).IsEqualTo("Bearer shared-custom-token");
    }

    private sealed class AuthorizationCaptureHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        internal ConcurrentBag<string?> Tokens { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Tokens.Add(request.Headers.Authorization?.ToString());
            return base.SendAsync(request, cancellationToken);
        }
    }
}
