using Dekaf.Networking;
using Dekaf.Metadata;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Networking;

public class KafkaConnectionCapabilitiesTests
{
    [Test]
    public async Task NegotiateVersion_UsesOnlySnapshotRange()
    {
        var older = CreateCapabilities(new ApiVersion(ApiKey.Produce, 3, 7));
        var newer = CreateCapabilities(new ApiVersion(ApiKey.Produce, 3, 13));

        await Assert.That(older.NegotiateVersion(ApiKey.Produce, 3, 13)).IsEqualTo((short)7);
        await Assert.That(newer.NegotiateVersion(ApiKey.Produce, 3, 13)).IsEqualTo((short)13);
    }

    [Test]
    public async Task NegotiateVersion_WhenApiIsAbsent_ThrowsBeforeWrite()
    {
        var capabilities = CreateCapabilities(new ApiVersion(ApiKey.Metadata, 9, 13));

        await Assert.That(() => capabilities.NegotiateVersion(ApiKey.Produce, 3, 13))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("Produce");
    }

    [Test]
    public async Task NegotiateVersion_WhenRangesAreDisjoint_ThrowsBeforeWrite()
    {
        var capabilities = CreateCapabilities(new ApiVersion(ApiKey.Produce, 0, 2));

        await Assert.That(() => capabilities.NegotiateVersion(ApiKey.Produce, 3, 13))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("client [3, 13]: broker [0, 2]");
    }

    [Test]
    public async Task NegotiateVersion_WhenClientRangeIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        var capabilities = CreateCapabilities(new ApiVersion(ApiKey.Produce, 3, 13));

        await Assert.That(() => capabilities.NegotiateVersion(ApiKey.Produce, 13, 3))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => capabilities.TryNegotiateVersion(ApiKey.Produce, 13, 3, out _))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task NewGeneration_DoesNotRetainRemovedApi()
    {
        var firstGeneration = CreateCapabilities(
            new ApiVersion(ApiKey.Metadata, 9, 13),
            new ApiVersion(ApiKey.Produce, 3, 13));
        var secondGeneration = CreateCapabilities(new ApiVersion(ApiKey.Metadata, 9, 13));

        await Assert.That(firstGeneration.HasApi(ApiKey.Produce)).IsTrue();
        await Assert.That(secondGeneration.HasApi(ApiKey.Produce)).IsFalse();
    }

    [Test]
    public async Task Snapshot_CapturesFinalizedFeaturesAndEpoch()
    {
        var response = new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys = [new ApiVersion(ApiKey.Metadata, 9, 13)],
            SupportedFeatures = [new SupportedFeature("kraft.version", 0, 1)],
            FinalizedFeaturesEpoch = 42,
            FinalizedFeatures = [new FinalizedFeature("transaction.version", 2, 0)],
            ZkMigrationReady = true
        };
        var capabilities = KafkaConnectionCapabilities.Create(response);

        await Assert.That(capabilities.TryGetSupportedFeatureRange(
            "kraft.version",
            out var minVersion,
            out var maxVersion)).IsTrue();
        await Assert.That(minVersion).IsEqualTo((short)0);
        await Assert.That(maxVersion).IsEqualTo((short)1);
        await Assert.That(capabilities.TryGetSupportedFeatureRange("missing", out _, out _)).IsFalse();
        await Assert.That(capabilities.FinalizedFeaturesEpoch).IsEqualTo(42);
        await Assert.That(capabilities.GetFinalizedFeatureVersion("transaction.version")).IsEqualTo((short)2);
        await Assert.That(capabilities.GetFinalizedFeatureVersion("missing")).IsEqualTo((short)0);
        await Assert.That(capabilities.ZkMigrationReady).IsTrue();
    }

    [Test]
    public async Task MetadataVersionSelection_UsesExactTargetConnection()
    {
        await using var metadata = new MetadataManager(
            Substitute.For<IConnectionPool>(),
            ["unused:9092"]);
        var olderConnection = CreateConnection(
            CreateCapabilities(new ApiVersion(ApiKey.Fetch, 12, 14)));
        var newerConnection = CreateConnection(
            CreateCapabilities(new ApiVersion(ApiKey.Fetch, 12, 16)));

        var olderVersion = metadata.GetNegotiatedApiVersion(
            olderConnection,
            ApiKey.Fetch,
            ourMinVersion: 12,
            ourMaxVersion: 16);
        var newerVersion = metadata.GetNegotiatedApiVersion(
            newerConnection,
            ApiKey.Fetch,
            ourMinVersion: 12,
            ourMaxVersion: 16);

        await Assert.That(olderVersion).IsEqualTo((short)14);
        await Assert.That(newerVersion).IsEqualTo((short)16);
    }

    [Test]
    public async Task InitializeAsync_SeedsVersionlessCompatibilitySnapshot()
    {
        var capabilities = KafkaConnectionCapabilities.Create(new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys =
            [
                new ApiVersion(ApiKey.Metadata, 9, 13),
                new ApiVersion(ApiKey.Fetch, 12, 16)
            ],
            FinalizedFeatures = [new FinalizedFeature("transaction.version", 2, 0)]
        });
        var connection = new CapabilityConnection(capabilities);
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync("unused", 9092, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        await using var metadata = new MetadataManager(
            pool,
            ["unused:9092"],
            new MetadataOptions { EnableBackgroundRefresh = false });

        await metadata.InitializeAsync();

        await Assert.That(connection.ObservedApiVersion).IsEqualTo((short)13);
        await Assert.That(metadata.HasApiKey(ApiKey.Fetch)).IsTrue();
        await Assert.That(metadata.GetNegotiatedApiVersion(ApiKey.Fetch, 12, 18))
            .IsEqualTo((short)16);
        await Assert.That(metadata.GetFinalizedFeatureVersion("transaction.version"))
            .IsEqualTo((short)2);
    }

    [Test]
    public async Task Rebootstrap_RefreshesVersionlessCompatibilitySnapshot()
    {
        var initialCapabilities = KafkaConnectionCapabilities.Create(new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys =
            [
                new ApiVersion(ApiKey.Metadata, 9, 12),
                new ApiVersion(ApiKey.Fetch, 12, 14),
                new ApiVersion(ApiKey.Produce, 0, 11)
            ],
            FinalizedFeatures = [new FinalizedFeature("transaction.version", 1, 0)]
        });
        var replacementCapabilities = KafkaConnectionCapabilities.Create(new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys =
            [
                new ApiVersion(ApiKey.Metadata, 9, 13),
                new ApiVersion(ApiKey.Fetch, 12, 16)
            ],
            FinalizedFeatures = [new FinalizedFeature("transaction.version", 2, 0)]
        });
        var initialConnection = new CapabilityConnection(initialCapabilities);
        var replacementConnection = new CapabilityConnection(replacementCapabilities);
        var connections = new Queue<IKafkaConnection>([initialConnection, replacementConnection]);
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(connections.Dequeue()));
        await using var metadata = new MetadataManager(
            pool,
            ["localhost:9092"],
            new MetadataOptions { EnableBackgroundRefresh = false });

        await metadata.InitializeAsync();
        await Assert.That(metadata.GetNegotiatedApiVersion(ApiKey.Fetch, 12, 18))
            .IsEqualTo((short)14);
        await Assert.That(metadata.GetFinalizedFeatureVersion("transaction.version"))
            .IsEqualTo((short)1);
        await Assert.That(metadata.HasApiKey(ApiKey.Produce)).IsTrue();

        var rebootstrapped = await metadata.TryRebootstrapImmediateAsync(null, CancellationToken.None);

        await Assert.That(rebootstrapped).IsTrue();
        await Assert.That(replacementConnection.ObservedApiVersion).IsEqualTo((short)13);
        await Assert.That(metadata.GetNegotiatedApiVersion(ApiKey.Fetch, 12, 18))
            .IsEqualTo((short)16);
        await Assert.That(metadata.GetFinalizedFeatureVersion("transaction.version"))
            .IsEqualTo((short)2);
        await Assert.That(metadata.HasApiKey(ApiKey.Produce)).IsFalse();
    }

    [Test]
    public async Task ConnectionPool_InFlightSendKeepsOriginalCapabilityGenerationDuringReplacement()
    {
        var releaseFirstSend = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new GenerationConnection(
            CreateCapabilities(new ApiVersion(ApiKey.Metadata, 9, 12)),
            releaseFirstSend.Task);
        var second = new GenerationConnection(
            CreateCapabilities(new ApiVersion(ApiKey.Metadata, 9, 13)),
            Task.CompletedTask);
        var generations = new Queue<IKafkaConnection>([first, second]);
        await using var pool = new ConnectionPool(
            clientId: null,
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) =>
                ValueTask.FromResult(generations.Dequeue()));
        pool.RegisterBroker(1, "unused", 9092);

        using var firstLease = await pool.LeaseConnectionAsync(1, CancellationToken.None);
        var firstVersion = ((IKafkaCapabilityProvider)firstLease.Connection)
            .Capabilities.NegotiateVersion(ApiKey.Metadata, 9, 13);
        var inFlightSend = firstLease.Connection
            .SendAsync<MetadataRequest, MetadataResponse>(
                MetadataRequest.ForAllTopics(),
                firstVersion,
                CancellationToken.None)
            .AsTask();
        await first.SendStarted;

        first.Disconnect();
        using var secondLease = await pool.LeaseConnectionAsync(1, CancellationToken.None);
        var secondVersion = ((IKafkaCapabilityProvider)secondLease.Connection)
            .Capabilities.NegotiateVersion(ApiKey.Metadata, 9, 13);

        releaseFirstSend.SetResult();
        await inFlightSend;

        await Assert.That(secondLease.Connection).IsSameReferenceAs(second);
        await Assert.That(first.ObservedApiVersion).IsEqualTo((short)12);
        await Assert.That(secondVersion).IsEqualTo((short)13);
    }

    private static KafkaConnectionCapabilities CreateCapabilities(params ApiVersion[] versions)
        => KafkaConnectionCapabilities.Create(CreateResponse(versions));

    private static IKafkaConnection CreateConnection(KafkaConnectionCapabilities capabilities)
        => new CapabilityConnection(capabilities);

    private static ApiVersionsResponse CreateResponse(params ApiVersion[] versions)
        => new()
        {
            ErrorCode = ErrorCode.None,
            ApiKeys = versions
        };

    private sealed class CapabilityConnection(KafkaConnectionCapabilities capabilities) :
        IKafkaConnection,
        IKafkaCapabilityProvider
    {
        public int BrokerId => 1;
        public string Host => "unused";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } = capabilities;
        public short ObservedApiVersion { get; private set; } = -1;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is not MetadataRequest)
                throw new NotSupportedException();

            ObservedApiVersion = apiVersion;
            return ValueTask.FromResult((TResponse)(IKafkaResponse)new MetadataResponse
            {
                Brokers = [],
                Topics = []
            });
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GenerationConnection(
        KafkaConnectionCapabilities capabilities,
        Task releaseSend) :
        IKafkaConnection,
        IKafkaCapabilityProvider
    {
        private readonly TaskCompletionSource _sendStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _connected = 1;

        public int BrokerId => 1;
        public string Host => "unused";
        public int Port => 9092;
        public bool IsConnected => Volatile.Read(ref _connected) != 0;
        public KafkaConnectionCapabilities Capabilities { get; } = capabilities;
        public Task SendStarted => _sendStarted.Task;
        public short ObservedApiVersion { get; private set; } = -1;

        public void Disconnect() => Volatile.Write(ref _connected, 0);

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            Volatile.Write(ref _connected, 1);
            return ValueTask.CompletedTask;
        }

        public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            ObservedApiVersion = apiVersion;
            _sendStarted.TrySetResult();
            await releaseSend.WaitAsync(cancellationToken);
            return (TResponse)(IKafkaResponse)new MetadataResponse
            {
                Brokers = [],
                Topics = []
            };
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
