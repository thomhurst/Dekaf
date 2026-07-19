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

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

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
