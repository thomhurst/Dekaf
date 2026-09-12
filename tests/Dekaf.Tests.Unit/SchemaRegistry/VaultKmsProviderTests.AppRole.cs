using System.Net;
using System.Text.Json;
using Dekaf.SchemaRegistry.Kms.Vault;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public partial class VaultKmsProviderTests
{
    private const string SuccessfulLogin = "{\"auth\":{\"client_token\":\"recovered-token\",\"lease_duration\":3600}}";

    [Test]
    [Arguments("{}")]
    [Arguments("{\"auth\":{\"client_token\":\"\",\"lease_duration\":3600}}")]
    [Arguments("{\"auth\":{\"client_token\":\"token\",\"lease_duration\":0}}")]
    [Arguments("{\"auth\":{\"client_token\":\"token\",\"lease_duration\":-1}}")]
    [Arguments("{\"auth\":{\"client_token\":\"token\\ninvalid\",\"lease_duration\":3600}}")]
    public async Task AppRole_InvalidLoginIsNotCachedAndNextCallRecovers(string body)
    {
        var calls = 0;
        using var http = new HttpClient(new RecordingHandler((_, _) => Task.FromResult(
            JsonResponse(++calls == 1 ? body : SuccessfulLogin))));
        var provider = new VaultAppRoleTokenProvider(http, "role", "secret");

        await Assert.That(async () => await provider.GetTokenAsync(VaultAddress, null)).Throws<InvalidOperationException>();
        await Assert.That(await provider.GetTokenAsync(VaultAddress, null)).IsEqualTo("recovered-token");
        await Assert.That(await provider.GetTokenAsync(VaultAddress, null)).IsEqualTo("recovered-token");
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AppRole_HttpOrMalformedJsonFailureReleasesGate(bool malformedJson)
    {
        var calls = 0;
        using var http = new HttpClient(new RecordingHandler((_, _) =>
        {
            if (++calls != 1)
                return Task.FromResult(JsonResponse(SuccessfulLogin));
            return Task.FromResult(malformedJson ? JsonResponse("{") : new HttpResponseMessage(HttpStatusCode.Forbidden));
        }));
        var provider = new VaultAppRoleTokenProvider(http, "role", "secret");

        if (malformedJson)
            await Assert.That(async () => await provider.GetTokenAsync(VaultAddress, null)).Throws<JsonException>();
        else
            await Assert.That(async () => await provider.GetTokenAsync(VaultAddress, null)).Throws<HttpRequestException>();

        var recovered = await provider.GetTokenAsync(VaultAddress, null).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(recovered).IsEqualTo("recovered-token");
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AppRole_CancellationDuringRefreshDoesNotStrandOtherCallers(bool cancelOwner)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var http = new HttpClient(new RecordingHandler(async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }
            return JsonResponse(SuccessfulLogin);
        }));
        var provider = new VaultAppRoleTokenProvider(http, "role", "secret");
        using var cancellation = new CancellationTokenSource();
        var owner = provider.GetTokenAsync(VaultAddress, null, cancelOwner ? cancellation.Token : default).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var waiter = provider.GetTokenAsync(VaultAddress, null, cancelOwner ? default : cancellation.Token).AsTask();
        try
        {
            cancellation.Cancel();
            await Assert.That(async () => await (cancelOwner ? owner : waiter)).Throws<OperationCanceledException>();
        }
        finally
        {
            release.TrySetResult();
        }
        var survivor = await (cancelOwner ? waiter : owner).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(survivor).IsEqualTo("recovered-token");
        await Assert.That(await provider.GetTokenAsync(VaultAddress, null)).IsEqualTo(survivor);
        await Assert.That(calls).IsEqualTo(cancelOwner ? 2 : 1);
    }

    [Test]
    public async Task AppRole_ExpiredTokenFailedRefreshNeverReturnsStaleToken()
    {
        var time = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var calls = 0;
        using var http = new HttpClient(new RecordingHandler((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse("{\"auth\":{\"client_token\":\"old-token\",\"lease_duration\":60}}"),
            2 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => JsonResponse(SuccessfulLogin)
        })));
        var provider = new VaultAppRoleTokenProvider(http, "role", "secret", "approle", time);
        await Assert.That(await provider.GetTokenAsync(VaultAddress, null)).IsEqualTo("old-token");
        time.Advance(TimeSpan.FromSeconds(60));

        await Assert.That(async () => await provider.GetTokenAsync(VaultAddress, null)).Throws<HttpRequestException>();
        await Assert.That(await provider.GetTokenAsync(VaultAddress, null)).IsEqualTo("recovered-token");
        await Assert.That(calls).IsEqualTo(3);
    }
}
