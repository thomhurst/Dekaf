using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientListOffsetsTests
{
    [Test]
    [Arguments(OffsetSpec.Earliest, ListOffsetsTimestamp.Earliest, (short)6)]
    [Arguments(OffsetSpec.Latest, ListOffsetsTimestamp.Latest, (short)6)]
    [Arguments(OffsetSpec.MaxTimestamp, ListOffsetsTimestamp.MaxTimestamp, (short)7)]
    [Arguments(OffsetSpec.EarliestLocal, ListOffsetsTimestamp.EarliestLocal, (short)8)]
    [Arguments(OffsetSpec.LatestTiered, ListOffsetsTimestamp.LatestTiered, (short)9)]
    [Arguments(OffsetSpec.EarliestPendingUpload, ListOffsetsTimestamp.EarliestPendingUpload, (short)11)]
    public async Task ListOffsetsAsync_MapsSpecAndUsesMinimumBrokerVersion(
        OffsetSpec spec,
        long expectedTimestamp,
        short brokerVersion)
    {
        await using var fixture = CreateFixture(broker1Version: brokerVersion);

        var result = await fixture.Admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec
            {
                TopicPartition = new TopicPartition("orders", 0),
                Spec = spec
            }
        ]);

        var sent = fixture.Connections[1].ListOffsetsRequests.Single();
        var partition = sent.Request.Topics.Single().Partitions.Single();
        await Assert.That(sent.Version).IsEqualTo(brokerVersion);
        await Assert.That(partition.Timestamp).IsEqualTo(expectedTimestamp);
        await Assert.That(partition.CurrentLeaderEpoch).IsEqualTo(7);
        await Assert.That(result[new TopicPartition("orders", 0)].Offset).IsEqualTo(100);
    }

    [Test]
    public async Task ListOffsetsAsync_TimestampSpec_PreservesTimestamp()
    {
        await using var fixture = CreateFixture(broker1Version: 6);

        await fixture.Admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec
            {
                TopicPartition = new TopicPartition("orders", 0),
                Spec = OffsetSpec.Timestamp,
                Timestamp = 1_700_000_000_000L
            }
        ]);

        var partition = fixture.Connections[1].ListOffsetsRequests.Single()
            .Request.Topics.Single().Partitions.Single();
        await Assert.That(partition.Timestamp).IsEqualTo(1_700_000_000_000L);
    }

    [Test]
    [Arguments(OffsetSpec.LatestTiered, (short)8, (short)9)]
    [Arguments(OffsetSpec.EarliestPendingUpload, (short)10, (short)11)]
    public async Task ListOffsetsAsync_UnsupportedTieredSpec_FailsBeforeWrite(
        OffsetSpec spec,
        short brokerVersion,
        short requiredVersion)
    {
        await using var fixture = CreateFixture(broker1Version: brokerVersion);

        var exception = await Assert.That(async () => await fixture.Admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec
            {
                TopicPartition = new TopicPartition("orders", 0),
                Spec = spec
            }
        ])).Throws<BrokerVersionException>();

        await Assert.That(exception!.Message).Contains($"requires v{requiredVersion}");
        await Assert.That(fixture.Connections[1].ListOffsetsRequests).IsEmpty();
    }

    [Test]
    public async Task ListOffsetsAsync_MultipleBrokers_UsesPerConnectionVersion()
    {
        await using var fixture = CreateFixture(broker1Version: 9, broker2Version: 11);

        var result = await fixture.Admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec
            {
                TopicPartition = new TopicPartition("orders", 0),
                Spec = OffsetSpec.LatestTiered
            },
            new TopicPartitionOffsetSpec
            {
                TopicPartition = new TopicPartition("orders", 1),
                Spec = OffsetSpec.EarliestPendingUpload
            }
        ], new ListOffsetsOptions { TimeoutMs = 12_345 });

        var first = fixture.Connections[1].ListOffsetsRequests.Single();
        var second = fixture.Connections[2].ListOffsetsRequests.Single();
        await Assert.That(first.Version).IsEqualTo((short)9);
        await Assert.That(second.Version).IsEqualTo((short)11);
        await Assert.That(first.Request.TimeoutMs).IsEqualTo(12_345);
        await Assert.That(second.Request.TimeoutMs).IsEqualTo(12_345);
        await Assert.That(result.Keys).IsEquivalentTo(
        [
            new TopicPartition("orders", 0),
            new TopicPartition("orders", 1)
        ]);
    }

    [Test]
    public async Task ListOffsetsAsync_OffsetNotAvailable_RetriesWithinExistingPolicy()
    {
        Queue<ErrorCode> outcomes = new([ErrorCode.OffsetNotAvailable, ErrorCode.None]);
        await using var fixture = CreateFixture(broker1Version: 11, broker1Outcomes: outcomes);

        var result = await fixture.Admin.ListOffsetsAsync(
        [
            new TopicPartitionOffsetSpec
            {
                TopicPartition = new TopicPartition("orders", 0),
                Spec = OffsetSpec.EarliestPendingUpload
            }
        ]);

        await Assert.That(fixture.Connections[1].ListOffsetsRequests.Count).IsEqualTo(2);
        await Assert.That(result[new TopicPartition("orders", 0)].Offset).IsEqualTo(100);
    }

    private static Fixture CreateFixture(
        short broker1Version,
        short broker2Version = 11,
        Queue<ErrorCode>? broker1Outcomes = null)
    {
        var metadata = CreateMetadataResponse();
        var connections = new Dictionary<int, RecordingConnection>
        {
            [1] = new RecordingConnection(1, broker1Version, metadata, broker1Outcomes),
            [2] = new RecordingConnection(2, broker2Version, metadata)
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult<IKafkaConnection>(connections[call.ArgAt<int>(0)]));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connections[1]));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(metadata);
        var admin = new AdminClient(
            new AdminClientOptions
            {
                BootstrapServers = ["localhost:9092"],
                RetryBackoffMs = 1,
                RetryBackoffMaxMs = 1
            },
            pool,
            metadataManager);

        return new Fixture(admin, metadataManager, connections);
    }

    private static MetadataResponse CreateMetadataResponse() => new()
    {
        Brokers =
        [
            new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9091 },
            new BrokerMetadata { NodeId = 2, Host = "broker-2", Port = 9092 }
        ],
        ClusterId = "test-cluster",
        ControllerId = 1,
        Topics =
        [
            new TopicMetadata
            {
                ErrorCode = ErrorCode.None,
                Name = "orders",
                Partitions =
                [
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None,
                        PartitionIndex = 0,
                        LeaderId = 1,
                        LeaderEpoch = 7,
                        ReplicaNodes = [1, 2],
                        IsrNodes = [1, 2]
                    },
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None,
                        PartitionIndex = 1,
                        LeaderId = 2,
                        LeaderEpoch = 8,
                        ReplicaNodes = [1, 2],
                        IsrNodes = [1, 2]
                    }
                ]
            }
        ]
    };

    private sealed class RecordingConnection(
        int brokerId,
        short listOffsetsMaxVersion,
        MetadataResponse metadata,
        Queue<ErrorCode>? outcomes = null) : IKafkaConnection, IKafkaCapabilityProvider
    {
        public int BrokerId => brokerId;
        public string Host => $"broker-{brokerId}";
        public int Port => 9090 + brokerId;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } =
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(ApiKey.Metadata, 9, MetadataRequest.HighestSupportedVersion),
                    new ApiVersion(ApiKey.ListOffsets, 6, listOffsetsMaxVersion)
                ]
            });
        internal List<RecordedListOffsetsRequest> ListOffsetsRequests { get; } = [];

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            IKafkaResponse response = request switch
            {
                MetadataRequest => metadata,
                ListOffsetsRequest listOffsets => CreateListOffsetsResponse(listOffsets, apiVersion),
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }

        private ListOffsetsResponse CreateListOffsetsResponse(ListOffsetsRequest request, short apiVersion)
        {
            ListOffsetsRequests.Add(new RecordedListOffsetsRequest(request, apiVersion));
            var errorCode = outcomes is { Count: > 0 } ? outcomes.Dequeue() : ErrorCode.None;
            return new ListOffsetsResponse
            {
                Topics = request.Topics.Select(topic => new ListOffsetsResponseTopic
                {
                    Name = topic.Name,
                    Partitions = topic.Partitions.Select(partition => new ListOffsetsResponsePartition
                    {
                        PartitionIndex = partition.PartitionIndex,
                        ErrorCode = errorCode,
                        Timestamp = partition.Timestamp,
                        Offset = 100 + partition.PartitionIndex,
                        LeaderEpoch = partition.CurrentLeaderEpoch
                    }).ToArray()
                }).ToArray()
            };
        }

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();
    }

    private sealed class Fixture(
        AdminClient admin,
        MetadataManager metadataManager,
        IReadOnlyDictionary<int, RecordingConnection> connections) : IAsyncDisposable
    {
        internal AdminClient Admin { get; } = admin;
        internal IReadOnlyDictionary<int, RecordingConnection> Connections { get; } = connections;

        public async ValueTask DisposeAsync()
        {
            await Admin.DisposeAsync().ConfigureAwait(false);
            await metadataManager.DisposeAsync().ConfigureAwait(false);
        }
    }

    private readonly record struct RecordedListOffsetsRequest(
        ListOffsetsRequest Request,
        short Version);
}
