using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Aws;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public partial class AwsKmsProviderTests
{
    [Test]
    [Arguments("arn:aws:kms:eu-west-2:123456789012:key/1234", "kms.eu-west-2.amazonaws.com", "eu-west-2")]
    [Arguments("aws-kms://arn:aws:kms:eu-west-1:123456789012:key/1234", "kms.eu-west-1.amazonaws.com", "eu-west-1")]
    [Arguments("arn:aws-cn:kms:cn-north-1:123456789012:key/1234", "kms.cn-north-1.amazonaws.com.cn", "cn-north-1")]
    [Arguments("arn:aws-us-gov:kms:us-gov-west-1:123456789012:alias/orders", "kms.us-gov-west-1.amazonaws.com", "us-gov-west-1")]
    [Arguments("alias/orders", "kms.us-east-1.amazonaws.com", "us-east-1")]
    [Arguments("1234", "kms.us-east-1.amazonaws.com", "us-east-1")]
    public async Task OwnedClient_RoutesWrapAndUnwrapToKeyRegion(string keyId, string host, string region)
    {
        var transport = new KmsHttpClientFactory();
        var config = CreateRoutingConfig(transport);
        using var provider = new AwsKmsProvider(config);

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(keyId));
        await provider.UnwrapKeyAsync(new byte[] { 4 }, CreateKeyReference(keyId));

        await Assert.That(transport.Requests.Count).IsEqualTo(2);
        foreach (var request in transport.Requests)
        {
            await Assert.That(request.Host).IsEqualTo(host);
            await Assert.That(request.Authorization).Contains($"/{region}/kms/aws4_request");
            await Assert.That(request.Authorization).Contains("Credential=test-access-key/");
            await Assert.That(request.Body).Contains(keyId.Replace(AwsKmsProvider.KeyUriPrefix, "", StringComparison.Ordinal));
        }
        await Assert.That(config.RegionEndpoint).IsEqualTo(RegionEndpoint.USEast1);
    }

    [Test]
    public async Task OwnedClient_CustomEndpointOverridesKeyRegion()
    {
        var transport = new KmsHttpClientFactory();
        var config = CreateRoutingConfig(transport);
        config.ServiceURL = "https://localstack.example:4566";
        config.AuthenticationRegion = "us-east-1";
        using var provider = new AwsKmsProvider(config);

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference());
        await provider.UnwrapKeyAsync(new byte[] { 4 }, CreateKeyReference());

        foreach (var request in transport.Requests)
            await Assert.That(request.Host).IsEqualTo("localstack.example");
        await Assert.That(transport.Configurations.Count).IsEqualTo(1);
    }

    [Test]
    public async Task OwnedClient_ConcurrentRegionalCallsReuseClientsAndPreserveConfiguration()
    {
        var transport = new KmsHttpClientFactory();
        var config = CreateRoutingConfig(transport);
        config.MaxErrorRetry = 3;
        config.Timeout = TimeSpan.FromSeconds(17);
        config.ClientAppId = "dekaf-test";
        config.UseDualstackEndpoint = true;
        using var provider = new AwsKmsProvider(config);

        await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(async () =>
            await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(index % 2 == 0
                ? KeyArn : "arn:aws:kms:eu-west-1:123456789012:key/1234")))));

        await Assert.That(transport.Configurations.Count).IsEqualTo(2);
        foreach (var regionalConfig in transport.Configurations.Keys)
        {
            await Assert.That(regionalConfig.MaxErrorRetry).IsEqualTo(3);
            await Assert.That(regionalConfig.Timeout).IsEqualTo(TimeSpan.FromSeconds(17));
            await Assert.That(regionalConfig.ClientAppId).IsEqualTo("dekaf-test");
            await Assert.That(regionalConfig.UseDualstackEndpoint).IsTrue();
            await Assert.That(regionalConfig.DefaultAWSCredentials).IsEqualTo(config.DefaultAWSCredentials);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task InjectedClient_KeepsItsEndpointEvenWhenOwned(bool ownsClient)
    {
        var transport = new KmsHttpClientFactory();
        using var client = new AmazonKeyManagementServiceClient(CreateRoutingConfig(transport));
        using var provider = new AwsKmsProvider(client, ownsClient);

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference());
        await provider.UnwrapKeyAsync(new byte[] { 4 }, CreateKeyReference());

        foreach (var request in transport.Requests)
            await Assert.That(request.Host).IsEqualTo("kms.us-east-1.amazonaws.com");
        await Assert.That(transport.Configurations.Count).IsEqualTo(1);
    }

    [Test]
    public async Task OwnedClient_CacheOverflowStillRoutesWithoutRetainingClients()
    {
        var transport = new KmsHttpClientFactory();
        using var provider = new AwsKmsProvider(CreateRoutingConfig(transport));
        // Synthetic region names exercise overflow without coupling the test to AWS's region inventory.
        for (var index = 1; index <= 17; index++)
        {
            await provider.WrapKeyAsync(new byte[] { 1 },
                CreateKeyReference($"arn:aws:kms:us-test-{index}:123456789012:key/1234"));
        }
        await provider.UnwrapKeyAsync(new byte[] { 4 },
            CreateKeyReference("arn:aws:kms:us-test-1:123456789012:key/1234"));
        await provider.UnwrapKeyAsync(new byte[] { 4 },
            CreateKeyReference("arn:aws:kms:us-test-17:123456789012:key/1234"));

        await Assert.That(transport.Configurations.Count).IsEqualTo(18);
        await Assert.That(transport.Requests.Last().Host).IsEqualTo("kms.us-test-17.amazonaws.com");
        foreach (var (regionalConfig, handler) in transport.Configurations)
        {
            var expected = regionalConfig.RegionEndpoint.SystemName == "us-test-17" ? 1 : 0;
            await Assert.That(handler.DisposeCount).IsEqualTo(expected);
        }

        provider.Dispose();

        foreach (var handler in transport.Configurations.Values)
            await Assert.That(handler.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task OwnedClient_DisposesActiveOverflowClientExactlyOnce()
    {
        var transport = new KmsHttpClientFactory();
        using var provider = new AwsKmsProvider(CreateRoutingConfig(transport));
        for (var index = 1; index <= 16; index++)
        {
            await provider.WrapKeyAsync(new byte[] { 1 },
                CreateKeyReference($"arn:aws:kms:us-test-{index}:123456789012:key/1234"));
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.RespondAsync = _ =>
        {
            started.TrySetResult();
            return completion.Task;
        };
        var operation = provider.WrapKeyAsync(new byte[] { 1 },
            CreateKeyReference("arn:aws:kms:us-test-17:123456789012:key/1234")).AsTask();

        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(transport.Configurations.Count).IsEqualTo(17);
            provider.Dispose();

            // The transport ignores cancellation so the overflow lease is still active here.
            await Assert.That(operation.IsCompleted).IsFalse();
            foreach (var handler in transport.Configurations.Values)
                await Assert.That(handler.DisposeCount).IsEqualTo(1);
        }
        finally
        {
            completion.TrySetCanceled();
            await Assert.That(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)))
                .Throws<OperationCanceledException>();
        }

        provider.Dispose();
        foreach (var handler in transport.Configurations.Values)
            await Assert.That(handler.DisposeCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task OwnedClient_CancelsRegionalOperation(bool dispose)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new KmsHttpClientFactory
        {
            RespondAsync = async token =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Cancellation was not observed.");
            }
        };
        using var provider = new AwsKmsProvider(CreateRoutingConfig(transport));
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        if (dispose)
            provider.Dispose();
        else
            await cancellation.CancelAsync();

        await Assert.That(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [Arguments("eu-west-2.attacker.example")]
    [Arguments("")]
    public async Task OwnedClient_InvalidArnRegionIsRejectedBeforeTransport(string region)
    {
        var transport = new KmsHttpClientFactory();
        using var provider = new AwsKmsProvider(CreateRoutingConfig(transport));

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 },
            CreateKeyReference($"arn:aws:kms:{region}:123456789012:key/1234")))
            .Throws<SchemaRegistryKmsException>();

        await Assert.That(transport.Requests).IsEmpty();
    }

    private static AmazonKeyManagementServiceConfig CreateRoutingConfig(KmsHttpClientFactory transport) => new()
    {
        RegionEndpoint = RegionEndpoint.USEast1,
        DefaultAWSCredentials = new BasicAWSCredentials("test-access-key", "test-secret-key"),
        HttpClientFactory = transport,
        HttpClientCacheSize = 1
    };

    private sealed class KmsHttpClientFactory : HttpClientFactory
    {
        public Func<CancellationToken, Task<HttpResponseMessage>>? RespondAsync { get; set; }

        public ConcurrentDictionary<AmazonKeyManagementServiceConfig, KmsHttpHandler> Configurations { get; } = new();
        public ConcurrentQueue<(string Host, string Authorization, string Body)> Requests { get; } = new();

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            var handler = new KmsHttpHandler(this);
            Configurations.TryAdd((AmazonKeyManagementServiceConfig)clientConfig, handler);
            return new HttpClient(handler);
        }

        // A null GetConfigUniqueString keeps the SDK cache local to each service client.
        // Disposing that client then disposes its transport, rather than each request doing so.
        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => true;
        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;

        public sealed class KmsHttpHandler(KmsHttpClientFactory owner) : HttpMessageHandler
        {
            private int _disposeCount;
            public int DisposeCount => Volatile.Read(ref _disposeCount);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Interlocked.Increment(ref _disposeCount);
                base.Dispose(disposing);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                owner.Requests.Enqueue((request.RequestUri!.Host, string.Join(",", request.Headers.GetValues("Authorization")), body));
                if (owner.RespondAsync is not null)
                    return await owner.RespondAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"CiphertextBlob\":\"BAUG\",\"Plaintext\":\"AQID\"}", Encoding.UTF8, "application/x-amz-json-1.1")
                };
            }
        }
    }
}
