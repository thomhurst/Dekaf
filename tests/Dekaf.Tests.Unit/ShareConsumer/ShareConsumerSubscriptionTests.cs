using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerSubscriptionTests
{
    private static readonly string[] FirstTopics = ["first"];
    private static readonly string[] SecondTopics = ["second"];
    [Test]
    public async Task StableConsumer_ReplacementSubscriptionIsSentOnce()
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        await fixture.Heartbeat();
        fixture.Consumer.Subscribe("second");
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsEquivalentTo(SecondTopics);
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsNull();
    }

    [Test]
    public async Task IdenticalSubscription_DoesNotRepublishOrWakeAssignmentWaiters()
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first", "second");
        await fixture.Heartbeat();
        var changed = fixture.Coordinator.GetAssignmentChangeTask();
        fixture.Consumer.Subscribe("second", "first", "first");
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsNull();
        await Assert.That(changed.IsCompleted).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FailedHeartbeat_RetriesReplacementSubscription(bool cancelled)
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        await fixture.Heartbeat();
        fixture.Consumer.Subscribe("second");
        fixture.Response = _ => ValueTask.FromException<ShareGroupHeartbeatResponse>(
            cancelled ? new OperationCanceledException() : new IOException("send failed"));
        try { await fixture.Heartbeat(); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        fixture.Response = _ => new(Fixture.Success());
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsEquivalentTo(SecondTopics);
    }

    [Test]
    public async Task ReplacementDuringHeartbeat_RemainsPendingAfterOlderResponse()
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        var response = new TaskCompletionSource<ShareGroupHeartbeatResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Response = _ => new(response.Task);
        var heartbeat = fixture.Heartbeat();
        fixture.Consumer.Subscribe("second");
        response.SetResult(Fixture.Success());
        await heartbeat;
        fixture.Response = _ => new(Fixture.Success());
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsEquivalentTo(SecondTopics);
    }

    [Test]
    public async Task Unsubscribe_PublishesEmptySubscription_AndAllowsResubscribe()
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        await fixture.Heartbeat();
        fixture.Consumer.Unsubscribe();
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsNotNull();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames!.Count).IsEqualTo(0);
        fixture.Consumer.Subscribe("first");
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsEquivalentTo(FirstTopics);
    }

    [Test]
    public async Task BrokerRejectedSubscription_IsRetried()
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        await fixture.Heartbeat();
        fixture.Consumer.Subscribe("second");
        fixture.Response = _ => new(new ShareGroupHeartbeatResponse { ErrorCode = ErrorCode.CoordinatorLoadInProgress });
        await Assert.ThrowsAsync<GroupException>(async () => await fixture.Heartbeat());
        fixture.Response = _ => new(Fixture.Success());
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsEquivalentTo(SecondTopics);
    }

    [Test]
    public async Task EpochZeroHeartbeat_ResendsPreviouslyAcknowledgedSubscription()
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        await fixture.Heartbeat();
        fixture.ResetMemberEpoch();
        await fixture.Heartbeat();
        await Assert.That(fixture.Requests[^1].SubscribedTopicNames).IsEquivalentTo(FirstTopics);
    }

    [Test]
    public async Task UnsubscribeBeforeInitialHeartbeat_DoesNotSendInvalidEmptyJoin()
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        fixture.Consumer.Unsubscribe();
        fixture.ResetMemberEpoch();
        await Assert.That(await fixture.Heartbeat()).IsFalse();
        await Assert.That(fixture.Requests.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnsubscribeDuringActivation_DoesNotFetch(bool batch)
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        fixture.PrepareJoin();
        var joined = new TaskCompletionSource<ShareGroupHeartbeatResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Response = request => request.MemberEpoch == 0 ? new(joined.Task) : new(Fixture.Success());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var records = fixture.Consumer.PollAsync(timeout.Token).GetAsyncEnumerator();
        await using var batches = fixture.Consumer.PollBatchesAsync(timeout.Token).GetAsyncEnumerator();
        var pending = (batch ? batches.MoveNextAsync() : records.MoveNextAsync()).AsTask();
        await Assert.That(pending.IsCompleted).IsFalse();
        fixture.Consumer.Unsubscribe();
        joined.SetResult(Fixture.Success());
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(3))).IsFalse();
        await Assert.That(fixture.FetchRequests).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnsubscribeDuringJoin_PublishesEmptySubscription(bool failFirstPublication)
    {
        await using var fixture = new Fixture();
        fixture.Consumer.Subscribe("first");
        fixture.PrepareJoin();
        var joined = new TaskCompletionSource<ShareGroupHeartbeatResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = new TaskCompletionSource<ShareGroupHeartbeatRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = false;
        fixture.Response = request =>
        {
            if (request.MemberEpoch == 0) return new(joined.Task);
            if (request.SubscribedTopicNames is { Count: 0 })
            {
                if (failFirstPublication && !failed)
                {
                    failed = true;
                    return ValueTask.FromException<ShareGroupHeartbeatResponse>(new KafkaException(ErrorCode.NetworkException, "publication failed"));
                }
                published.TrySetResult(request);
            }
            return new(Fixture.Success());
        };
        var activation = fixture.Coordinator.EnsureActiveGroupAsync(CancellationToken.None);
        await Assert.That(activation.IsCompleted).IsFalse();
        fixture.Consumer.Unsubscribe();
        joined.SetResult(Fixture.Success());
        await activation;
        await Assert.That(published.Task.IsCompletedSuccessfully).IsTrue();
        var request = await published.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.That(request.MemberEpoch).IsEqualTo(1);
        await Assert.That(request.SubscribedTopicNames!.Count).IsEqualTo(0);
        await Assert.That(failed).IsEqualTo(failFirstPublication);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly MethodInfo SendHeartbeat = typeof(ShareConsumerCoordinator).GetMethod(
            "SendShareGroupHeartbeatAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly MetadataManager _metadata;
        public KafkaShareConsumer<string, string> Consumer { get; }
        public ShareConsumerCoordinator Coordinator { get; }
        public List<ShareGroupHeartbeatRequest> Requests { get; } = [];
        public int FetchRequests { get; private set; }
        public Func<ShareGroupHeartbeatRequest, ValueTask<ShareGroupHeartbeatResponse>> Response { get; set; }
            = _ => new(Success());

        public Fixture()
        {
            var options = new ShareConsumerOptions { BootstrapServers = ["localhost:9092"], GroupId = "subscription", ConnectionsPerBroker = 1, HeartbeatIntervalMs = 10 };
            var connection = Substitute.For<IKafkaConnection>();
            connection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
                Arg.Any<ShareGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var request = call.Arg<ShareGroupHeartbeatRequest>();
                    Requests.Add(request);
                    return Response(request);
                });
            connection.SendAsync<ShareFetchRequest, ShareFetchResponse>(
                Arg.Any<ShareFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    FetchRequests++;
                    return new ShareFetchResponse { ErrorCode = ErrorCode.None, Responses = [], NodeEndpoints = [] };
                });
            connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(new FindCoordinatorResponse
                {
                    Coordinators = [new Coordinator
                    {
                        Key = options.GroupId, NodeId = 1, Host = "localhost", Port = 9092
                    }]
                });
            var pool = Substitute.For<IConnectionPool>();
            pool.GetConnectionAsync(1, Arg.Any<CancellationToken>()).Returns(connection);
            pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>()).Returns(connection);
            _metadata = new MetadataManager(pool, options.BootstrapServers);
            _metadata.SetApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1);
            _metadata.SetApiVersion(ApiKey.ShareFetch, 1, 2);
            _metadata.SetApiVersion(ApiKey.FindCoordinator,
                FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion);
            _metadata.Metadata.Update(new MetadataResponse
            {
                Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
                Topics = [new TopicMetadata
                {
                    ErrorCode = ErrorCode.None, Name = "first", TopicId = Guid.NewGuid(),
                    Partitions = [new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1,
                        ReplicaNodes = [1], IsrNodes = [1]
                    }]
                }]
            });
            Consumer = new KafkaShareConsumer<string, string>(options, Substitute.For<IDeserializer<string>>(),
                Substitute.For<IDeserializer<string>>(), pool, _metadata);
            Coordinator = (ShareConsumerCoordinator)typeof(KafkaShareConsumer<string, string>).GetField(
                "_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Consumer)!;
            Set("_coordinatorId", 1);
            Set("_state", CoordinatorState.Stable);
            Set("_memberEpoch", 1);
        }

        private void Set(string name, object value) => typeof(ShareConsumerCoordinator)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Coordinator, value);
        public void PrepareJoin()
        {
            Set("_state", CoordinatorState.Unjoined);
            Set("_memberEpoch", 0);
            Set("_assignedPartitions", new HashSet<TopicPartition> { new("first", 0) });
            typeof(KafkaShareConsumer<string, string>).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Consumer, true);
        }
        public void ResetMemberEpoch() => Set("_memberEpoch", 0);
        public ValueTask<bool> Heartbeat() => (ValueTask<bool>)SendHeartbeat.Invoke(Coordinator, [CancellationToken.None])!;
        public static ShareGroupHeartbeatResponse Success() => new() { ErrorCode = ErrorCode.None, MemberEpoch = 1 };
        public async ValueTask DisposeAsync()
        {
            Set("_memberId", null!);
            await Consumer.DisposeAsync();
            await _metadata.DisposeAsync();
        }
    }
}
