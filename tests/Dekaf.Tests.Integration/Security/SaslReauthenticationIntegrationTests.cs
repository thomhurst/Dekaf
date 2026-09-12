using Dekaf.Networking;
using Dekaf.Errors;
using Dekaf.Protocol.Messages;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration.Security;

public sealed class ReauthKafkaContainer : SaslKafkaContainer
{
    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) => base.ConfigureBuilder(builder)
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_PLAIN_CONNECTIONS_MAX_REAUTH_MS", "3000");
}

public sealed class ReauthOAuthKafkaContainer : OAuthBearerKafkaContainer
{
    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) => base.ConfigureBuilder(builder)
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_OAUTHBEARER_CONNECTIONS_MAX_REAUTH_MS", "60000");
}

[Category("Authentication")]
[ClassDataSource<ReauthOAuthKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class OAuthReauthenticationIntegrationTests(ReauthOAuthKafkaContainer kafka)
{
    [Test]
    [Arguments("write lock")]
    [Arguments("pending slot")]
    [Arguments("broker throttle")]
    public async Task CancelledHandshakeBeforeWrite_PreservesSession(string wait, CancellationToken cancellationToken)
    {
        var endpoint = BootstrapServerList.Parse(kafka.BootstrapServers);
        await using var connection = new KafkaConnection(endpoint.Host, endpoint.Port, options: new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.OAuthBearer,
            OAuthBearerTokenProvider = OAuthBearerKafkaContainer.GetTokenAsync,
            SaslReauthenticationConfig = new SaslReauthenticationConfig { Enabled = false }
        });
        await connection.ConnectAsync(cancellationToken);
        using var cancelled = new CancellationTokenSource();
        SemaphoreSlim? held = null;
        var heldCount = 0;
        var throttle = GetPrivateField<BrokerThrottleState>(connection, "_brokerThrottleState");
        try
        {
            if (wait == "broker throttle")
                throttle.Observe(60_000);
            else
            {
                held = GetPrivateField<SemaphoreSlim>(connection,
                    wait == "write lock" ? "_writeLock" : "_pendingRequestSlots");
                while (held.Wait(0, cancellationToken))
                    heldCount++;
            }

            // Control the exchange token directly so cancellation occurs while the
            // resource is held, without racing the connection's receive timeout.
            var exchange = (ValueTask<long>)typeof(KafkaConnection)
                .GetMethod("PerformSaslReauthExchangeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(connection, [cancelled.Token])!;
            await Assert.That(exchange.IsCompleted).IsFalse();
            await cancelled.CancelAsync();
            await Assert.That(async () => await exchange).Throws<OperationCanceledException>();
            await Assert.That(connection.IsConnected).IsTrue();
        }
        finally
        {
            if (heldCount != 0)
                held!.Release(heldCount);
            typeof(BrokerThrottleState).GetField("_throttleUntilMs", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(throttle, 0L);
        }

        var metadata = await connection.SendAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = [] }, 9, cancellationToken);
        await Assert.That(metadata.Brokers.Count).IsGreaterThan(0);
        await connection.PerformReauthenticationAsync().WaitAsync(cancellationToken);
        await Assert.That(connection.IsConnected).IsTrue();
    }

    private static T GetPrivateField<T>(KafkaConnection connection, string name) =>
        (T)typeof(KafkaConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(connection)!;

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TokenRefreshFailure_PreservesSessionAndAllowsNextRenewal(bool timeout, CancellationToken cancellationToken)
    {
        var calls = 0;
        var endpoint = BootstrapServerList.Parse(kafka.BootstrapServers);
        await using var connection = new KafkaConnection(endpoint.Host, endpoint.Port, $"oauth-reauth-{Guid.NewGuid():N}", new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.OAuthBearer,
            OAuthBearerTokenProvider = async token =>
            {
                if (Interlocked.Increment(ref calls) == 2)
                {
                    if (timeout)
                        await Task.Delay(Timeout.Infinite, token);
                    throw new InvalidOperationException("Token provider unavailable");
                }
                return OAuthBearerKafkaContainer.CreateToken(OAuthBearerKafkaContainer.Principal);
            },
            RequestTimeout = TimeSpan.FromSeconds(3),
            SaslReauthenticationConfig = new SaslReauthenticationConfig { Enabled = false }
        });
        await connection.ConnectAsync(cancellationToken);

        // PerformReauthenticationAsync logs preparation failures and preserves the current session.
        await connection.PerformReauthenticationAsync().WaitAsync(cancellationToken);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(connection.IsConnected).IsTrue();
        var metadata = await connection.SendAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = [] }, 9, cancellationToken);
        await Assert.That(metadata.Brokers.Count).IsGreaterThan(0);

        await connection.PerformReauthenticationAsync().WaitAsync(cancellationToken);
        await Assert.That(calls).IsEqualTo(3);
        await Assert.That(connection.IsConnected).IsTrue();
        metadata = await connection.SendAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = [] }, 9, cancellationToken);
        await Assert.That(metadata.Brokers.Count).IsGreaterThan(0);
    }
}

[Category("Authentication")]
[ClassDataSource<ReauthKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class SaslReauthenticationIntegrationTests(ReauthKafkaContainer kafka)
{
    [Test]
    public async Task Timer_RenewsMultipleSessionsWithoutReconnect(CancellationToken cancellationToken)
    {
        var credentialCalls = 0;
        await using var connection = CreateConnection(_ =>
        {
            Interlocked.Increment(ref credentialCalls);
            return ValueTask.FromResult(ValidCredentials());
        }, automatic: true);
        await connection.ConnectAsync(cancellationToken);
        var started = System.Diagnostics.Stopwatch.StartNew();
        // Cross two complete broker session lifetimes while exercising the same connection.
        while (started.Elapsed < TimeSpan.FromSeconds(7))
        {
            await ReadMetadataAsync(connection, cancellationToken);
            await Task.Delay(100, cancellationToken);
        }
        await Assert.That(Volatile.Read(ref credentialCalls)).IsGreaterThanOrEqualTo(3);
        await Assert.That(connection.IsConnected).IsTrue();
    }

    [Test]
    public async Task ConcurrentTraffic_DuringCredentialRefreshCompletesOnSameConnection(CancellationToken cancellationToken)
    {
        var calls = 0;
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var credentials = new TaskCompletionSource<SaslCredentials>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateConnection(token =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return ValueTask.FromResult(ValidCredentials());
            refreshing.TrySetResult();
            return new ValueTask<SaslCredentials>(credentials.Task.WaitAsync(token));
        });
        await connection.ConnectAsync(cancellationToken);
        var renewal = connection.PerformReauthenticationAsync();
        try
        {
            await refreshing.Task.WaitAsync(cancellationToken);
            var requests = Enumerable.Range(0, 16).Select(_ => ReadMetadataAsync(connection, cancellationToken)).ToArray();
            await Task.WhenAll(requests).WaitAsync(cancellationToken);
        }
        finally
        {
            credentials.TrySetResult(ValidCredentials());
            await renewal.WaitAsync(cancellationToken);
        }
        await ReadMetadataAsync(connection, cancellationToken);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(connection.IsConnected).IsTrue();
    }

    [Test]
    public async Task RejectedRenewal_ClosesAuthenticatedConnection(CancellationToken cancellationToken)
    {
        var calls = 0;
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        await using var connection = CreateConnection(_ => ValueTask.FromResult(
            Interlocked.Increment(ref calls) == 1 ? ValidCredentials() : new SaslCredentials(SaslKafkaContainer.SaslUsername, "invalid")),
            logger: loggerFactory.CreateLogger<KafkaConnection>());
        await connection.ConnectAsync(cancellationToken);
        await ReadMetadataAsync(connection, cancellationToken);
        try { await connection.PerformReauthenticationAsync().WaitAsync(cancellationToken); }
        catch (AuthenticationException exception)
        {
            await Assert.That(exception.ErrorCode).IsEqualTo(Dekaf.Protocol.ErrorCode.SaslAuthenticationFailed);
        }
        await TestWait.WaitForConditionAsync(() => !connection.IsConnected, TimeSpan.FromSeconds(10),
            description: "broker disconnects rejected SASL session");
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(logs.Entries.Count(entry => entry.LogLevel == LogLevel.Error &&
            entry.Message.Contains("SASL re-authentication failed", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_DuringCredentialRefreshSettlesRenewal(CancellationToken cancellationToken)
    {
        var calls = 0;
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<SaslCredentials>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateConnection(token =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return ValueTask.FromResult(ValidCredentials());
            refreshing.TrySetResult();
            return new ValueTask<SaslCredentials>(release.Task.WaitAsync(token));
        });
        await connection.ConnectAsync(cancellationToken);
        var renewal = connection.PerformReauthenticationAsync();
        try
        {
            await refreshing.Task.WaitAsync(cancellationToken);
            await connection.DisposeAsync();
        }
        finally
        {
            release.TrySetResult(ValidCredentials());
            // Once disposal wins, the in-flight exchange may report its closed transport.
            try { await renewal.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) when (!connection.IsConnected) { }
        }
        await Assert.That(connection.IsConnected).IsFalse();
        await connection.PerformReauthenticationAsync();
        await Assert.That(calls).IsEqualTo(2);
    }

    private KafkaConnection CreateConnection(Func<CancellationToken, ValueTask<SaslCredentials>> credentials,
        bool automatic = false, ILogger<KafkaConnection>? logger = null)
    {
        var endpoint = BootstrapServerList.Parse(kafka.BootstrapServers);
        return new KafkaConnection(endpoint.Host, endpoint.Port, $"reauth-{Guid.NewGuid():N}", new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.Plain,
            SaslCredentialProvider = credentials,
            RequestTimeout = TimeSpan.FromSeconds(10),
            SaslReauthenticationConfig = new SaslReauthenticationConfig
            {
                Enabled = automatic, MinSessionLifetimeMs = 0, ReauthenticationThreshold = 0.5
            }
        }, logger);
    }

    private static SaslCredentials ValidCredentials() => new(SaslKafkaContainer.SaslUsername, SaslKafkaContainer.SaslPassword);

    private static async Task ReadMetadataAsync(KafkaConnection connection, CancellationToken cancellationToken)
    {
        var metadata = await connection.SendAsync<MetadataRequest, MetadataResponse>(new MetadataRequest { Topics = [] }, 9, cancellationToken);
        await Assert.That(metadata.Brokers.Count).IsGreaterThan(0);
    }
}
