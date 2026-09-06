using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed class OAuthBearerRefreshConcurrencyTests
{
    [Test]
    public async Task ConcurrentMisses_DoNotStartOutOfOrderRefreshes()
    {
        var firstResponse = new TaskCompletionSource<OAuthBearerToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResponse = new TaskCompletionSource<OAuthBearerToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var authenticator = new OAuthBearerAuthenticator(_ => new ValueTask<OAuthBearerToken>(
            Interlocked.Increment(ref calls) == 1 ? firstResponse.Task : secondResponse.Task));
        var first = authenticator.GetTokenAsync().AsTask();
        var second = authenticator.GetTokenAsync().AsTask();
        var callsBeforeCompletion = Volatile.Read(ref calls);
        var older = Token("older", 120);
        var newer = Token("newer", 3600);

        // Before the fix both provider calls start, so these responses reproduce reverse completion.
        // With serialized refreshes the second response is intentionally unused: preventing that
        // second invocation is the regression assertion. Complete both sources for either outcome.
        secondResponse.SetResult(newer);
        firstResponse.SetResult(older);
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(callsBeforeCompletion).IsEqualTo(1);
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(results[0]).IsSameReferenceAs(older);
        await Assert.That(results[1]).IsSameReferenceAs(older);
        await Assert.That(await authenticator.GetTokenAsync()).IsSameReferenceAs(older);
    }

    [Test]
    public async Task FailedRefresh_ReleasesWaitingCallerToRetry()
    {
        var response = new TaskCompletionSource<OAuthBearerToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var recovered = Token("recovered");
        var authenticator = new OAuthBearerAuthenticator(_ => Interlocked.Increment(ref calls) == 1
            ? new ValueTask<OAuthBearerToken>(response.Task) : ValueTask.FromResult(recovered));
        var first = authenticator.GetTokenAsync().AsTask();
        var waiter = authenticator.GetTokenAsync().AsTask();
        var waiterWasPending = !waiter.IsCompleted;
        var failure = new InvalidOperationException("refresh failed");
        response.SetException(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(TimeSpan.FromSeconds(10)));
        var result = await waiter.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(waiterWasPending).IsTrue();
        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(result).IsSameReferenceAs(recovered);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(await authenticator.GetTokenAsync()).IsSameReferenceAs(recovered);
    }

    [Test]
    public async Task CancelledWaiter_DoesNotCancelActiveProvider()
    {
        var response = new TaskCompletionSource<OAuthBearerToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CancellationToken activeToken = default;
        var authenticator = new OAuthBearerAuthenticator(token =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                activeToken = token;
            return new ValueTask<OAuthBearerToken>(response.Task.WaitAsync(token));
        });
        var first = authenticator.GetTokenAsync().AsTask();
        using var cancellation = new CancellationTokenSource();
        var waiter = authenticator.GetTokenAsync(cancellation.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiter.WaitAsync(TimeSpan.FromSeconds(10)));
        var token = Token("success");
        response.SetResult(token);
        var result = await first.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(activeToken.IsCancellationRequested).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(result).IsSameReferenceAs(token);
        await Assert.That(await authenticator.GetTokenAsync()).IsSameReferenceAs(token);
    }

    [Test]
    public async Task CancelledActiveRefresh_ReleasesWaitingCallerToRetry()
    {
        var response = new TaskCompletionSource<OAuthBearerToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var recovered = Token("recovered");
        var authenticator = new OAuthBearerAuthenticator(token => Interlocked.Increment(ref calls) == 1
            ? new ValueTask<OAuthBearerToken>(response.Task.WaitAsync(token)) : ValueTask.FromResult(recovered));
        using var cancellation = new CancellationTokenSource();
        var first = authenticator.GetTokenAsync(cancellation.Token).AsTask();
        var waiter = authenticator.GetTokenAsync().AsTask();
        var waiterWasPending = !waiter.IsCompleted;
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(TimeSpan.FromSeconds(10)));
        var result = await waiter.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(waiterWasPending).IsTrue();
        await Assert.That(result).IsSameReferenceAs(recovered);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task SynchronousProviderFailure_AllowsNextRefresh()
    {
        var calls = 0;
        var token = Token("recovered");
        var authenticator = new OAuthBearerAuthenticator(_ => ++calls == 1
            ? throw new InvalidOperationException("refresh failed") : ValueTask.FromResult(token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => authenticator.GetTokenAsync().AsTask());
        await Assert.That(await authenticator.GetTokenAsync()).IsSameReferenceAs(token);
    }

    [Test]
    public async Task TokensInsideRefreshBuffer_SerializeEveryWaitingRefresh()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var active = 0;
        var overlapping = 0;
        var token = Token("short-lived", 30);
        var authenticator = new OAuthBearerAuthenticator(async _ =>
        {
            Interlocked.Increment(ref calls);
            if (Interlocked.Increment(ref active) != 1)
                Interlocked.Increment(ref overlapping);
            try
            {
                await release.Task;
                return token;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });
        var requests = new Task<OAuthBearerToken>[32];
        for (var index = 0; index < requests.Length; index++)
            requests[index] = authenticator.GetTokenAsync().AsTask();
        release.SetResult();
        await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(calls).IsEqualTo(requests.Length);
        await Assert.That(overlapping).IsEqualTo(0);
        await Assert.That(active).IsEqualTo(0);
    }

    [Test]
    public async Task SynchronousProviderCancellation_ReleasesGateAndReturnsCancelledTask()
    {
        var calls = 0;
        var token = Token("recovered");
        var authenticator = new OAuthBearerAuthenticator(_ => ++calls == 1
            ? throw new OperationCanceledException() : ValueTask.FromResult(token));
        var cancelled = authenticator.GetTokenAsync().AsTask();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelled);
        await Assert.That(cancelled.IsCanceled).IsTrue();
        await Assert.That(await authenticator.GetTokenAsync()).IsSameReferenceAs(token);
    }

    [Test]
    public async Task CachedToken_CompletesSynchronouslyWithoutCallingProvider()
    {
        var calls = 0;
        var token = Token("cached");
        var authenticator = new OAuthBearerAuthenticator(_ => { calls++; return ValueTask.FromResult(token); });
        await authenticator.GetTokenAsync();
        var cached = authenticator.GetTokenAsync();
        await Assert.That(cached.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await cached).IsSameReferenceAs(token);
        await Assert.That(calls).IsEqualTo(1);
    }

    private static OAuthBearerToken Token(string value, int lifetimeSeconds = 3600) => new()
    {
        TokenValue = value,
        PrincipalName = "test-user",
        Expiration = DateTimeOffset.UtcNow.AddSeconds(lifetimeSeconds)
    };
}
