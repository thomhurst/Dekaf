using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

[NotInParallel]
public sealed class QueryWatermarkOffsetsTests
{
    private const string Topic = "watermark-topic";
    private const int Partition = 0;
    private const long EarliestOffsetTimestamp = -2;
    private const long LatestOffsetTimestamp = -1;

    [Test]
    public async Task QueryWatermarkOffsetsAsync_UsesCoordinationConnectionAndStartsRequestsConcurrently()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = new LeaseTrackingConnection();
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.ListOffsets, ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());

        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);

        var earliestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.SendHandler = request =>
        {
            var timestamp = request.Topics[0].Partitions[0].Timestamp;
            var currentLeaderEpoch = request.Topics[0].Partitions[0].CurrentLeaderEpoch;

            if (currentLeaderEpoch != 3)
                throw new InvalidOperationException($"Unexpected CurrentLeaderEpoch {currentLeaderEpoch}");

            if (timestamp == EarliestOffsetTimestamp)
                earliestStarted.TrySetResult();
            else if (timestamp == LatestOffsetTimestamp)
                latestStarted.TrySetResult();
            else
                throw new InvalidOperationException($"Unexpected ListOffsets timestamp {timestamp}");

            return new ValueTask<ListOffsetsResponse>(CreateListOffsetsResponseAsync(timestamp, releaseResponses.Task));
        };

        var queryTask = consumer.QueryWatermarkOffsetsAsync(new TopicPartition(Topic, Partition), CancellationToken.None).AsTask();

        await earliestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);
        await Assert.That(connection.LeaseCount).IsEqualTo(1);
        try
        {
            await latestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch
        {
            releaseResponses.TrySetResult();
            await queryTask.WaitAsync(TimeSpan.FromSeconds(1));
            throw;
        }

        releaseResponses.SetResult();
        var watermarks = await queryTask.WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.That(watermarks.Low).IsEqualTo(10);
        await Assert.That(watermarks.High).IsEqualTo(42);
        _ = connectionPool.Received(1).GetConnectionByIndexAsync(
            0,
            1,
            Arg.Any<CancellationToken>());
        await Assert.That(connection.LeaseCount).IsEqualTo(0);
    }

    private static async Task<ListOffsetsResponse> CreateListOffsetsResponseAsync(long timestamp, Task release)
    {
        await release.ConfigureAwait(false);
        var offset = timestamp == EarliestOffsetTimestamp ? 10 : 42;

        return new ListOffsetsResponse
        {
            Topics =
            [
                new ListOffsetsResponseTopic
                {
                    Name = Topic,
                    Partitions =
                    [
                        new ListOffsetsResponsePartition
                        {
                            PartitionIndex = Partition,
                            ErrorCode = ErrorCode.None,
                            Offset = offset
                        }
                    ]
                }
            ]
        };
    }

    private static MetadataResponse CreateMetadataResponse() => new()
    {
        Brokers =
        [
            new BrokerMetadata
            {
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }
        ],
        Topics =
        [
            new TopicMetadata
            {
                Name = Topic,
                ErrorCode = ErrorCode.None,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        PartitionIndex = Partition,
                        LeaderId = 0,
                        LeaderEpoch = 3,
                        ErrorCode = ErrorCode.None,
                        ReplicaNodes = [0],
                        IsrNodes = [0]
                    }
                ]
            }
        ]
    };

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var initializedField = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found - was it renamed?");

        initializedField.SetValue(consumer, true);
    }

    private sealed class LeaseTrackingConnection : IKafkaConnection, IRetirableKafkaConnection
    {
        private int _leaseCount;
        private int _leaseAcquisitionCount;

        public Func<ListOffsetsRequest, ValueTask<ListOffsetsResponse>>? SendHandler { get; set; }
        public int BrokerId => 0;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public int LeaseCount => Volatile.Read(ref _leaseCount);
        public int LeaseAcquisitionCount => Volatile.Read(ref _leaseAcquisitionCount);
        public int ActiveOperationCount => 0;

        public bool TryAcquireLease()
        {
            Interlocked.Increment(ref _leaseAcquisitionCount);
            Interlocked.Increment(ref _leaseCount);
            return true;
        }

        public void ReleaseLease() => Interlocked.Decrement(ref _leaseCount);
        public void BeginRetirement() { }
        public void CompleteRetirement() { }
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is not ListOffsetsRequest listOffsetsRequest
                || typeof(TResponse) != typeof(ListOffsetsResponse)
                || SendHandler is null)
            {
                throw new NotSupportedException();
            }

            var response = await SendHandler(listOffsetsRequest);
            return (TResponse)(object)response;
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
    }
}
