using System.Net;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Dekaf.Outbox.DynamoDB;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// DynamoDB Local, shared across the test session. It evaluates the real condition, update
/// and key expressions, which no hand-written fake can vouch for. Every test gets its own
/// table, so tests stay independent on the shared instance.
/// </summary>
public sealed class DynamoDbLocalContainer : IAsyncInitializer, IAsyncDisposable
{
    internal const string Image =
        "amazon/dynamodb-local:3.3.1@sha256:ff89bd48ff32cd8d9be5fee8873b65b8854dc408f1afe881be6eb00247bc0dab";

    private const ushort Port = 8000;

    private IContainer? _container;

    public string ServiceUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await ContainerStartupRetry.RunAsync(
            StartAttemptAsync,
            DisposeAttemptAsync,
            ContainerStartupRetry.IsKnownTransient).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAttemptAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A client with retries disabled: a test that expects a refused conditional write must
    /// see it once, and a throttled local instance is a test failure, not something to mask.
    /// </summary>
    public AmazonDynamoDBClient CreateClient() => new(
        new BasicAWSCredentials("dekaf", "dekaf"),
        new AmazonDynamoDBConfig
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = "us-east-1",
            MaxErrorRetry = 0
        });

    /// <summary>Creates a fresh table and returns the options addressing it.</summary>
    public static async Task<DynamoDbOutboxOptions> CreateTableAsync(
        IAmazonDynamoDB client, int bucketCount = 8, CancellationToken cancellationToken = default)
    {
        var options = new DynamoDbOutboxOptions
        {
            TableName = $"outbox-{Guid.NewGuid():N}",
            BucketCount = bucketCount
        };
        await DynamoDbOutboxTable.CreateIfNotExistsAsync(client, options, cancellationToken).ConfigureAwait(false);
        return options;
    }

    private async Task StartAttemptAsync()
    {
        _container = new ContainerBuilder(Image)
            // In memory: no state survives the session, and nothing touches the image's disk.
            .WithCommand("-jar", "DynamoDBLocal.jar", "-inMemory", "-sharedDb")
            .WithPortBinding(Port, true)
            // The endpoint answers an unsigned GET with 400 once it serves requests.
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                .ForPort(Port)
                .ForPath("/")
                .ForStatusCode(HttpStatusCode.BadRequest)))
            .Build();
        await _container.StartAsync().ConfigureAwait(false);
        ServiceUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(Port)}";
    }

    private async ValueTask DisposeAttemptAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
        }
    }
}
