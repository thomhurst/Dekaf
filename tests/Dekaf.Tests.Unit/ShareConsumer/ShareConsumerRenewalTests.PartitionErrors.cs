using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(ErrorCode.NotLeaderOrFollower)]
    [Arguments(ErrorCode.FencedLeaderEpoch)]
    public async Task Poll_LeaderChangeWithHint_ReroutesNextFetchWithoutMetadataRefresh(ErrorCode errorCode)
    {
        var refreshes = 0;
        var oldLeader = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreatePartitionErrorResponse(
                (0, errorCode, LeaderId: 2, LeaderEpoch: 5)),
            MetadataHandler = _ =>
            {
                refreshes++;
                return new(BufferedLeaderMetadata(1));
            }
        };
        var newLeader = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100)
        };
        await using var fixture = CreateFixture(oldLeader, ShareAcknowledgementMode.Implicit,
            secondConnection: newLeader, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();

        await Assert.That(poll.Current.Offset).IsEqualTo(100);
        await Assert.That(oldLeader.ShareFetchRequests).HasSingleItem();
        await Assert.That(newLeader.ShareFetchRequests).HasSingleItem();
        await Assert.That(refreshes).IsEqualTo(0);
        var leader = fixture.MetadataManager.Metadata.GetPartitionLeader("topic", 0);
        await Assert.That(leader!.NodeId).IsEqualTo(2);
    }

    [Test]
    [Arguments(ErrorCode.NotLeaderOrFollower)]
    [Arguments(ErrorCode.FencedLeaderEpoch)]
    [Arguments(ErrorCode.UnknownTopicId)]
    [Arguments(ErrorCode.UnknownTopicOrPartition)]
    [Arguments(ErrorCode.UnknownServerError)]
    public async Task Poll_PartitionErrorWithoutHint_RefreshesMetadataAndBacksOff(ErrorCode errorCode)
    {
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshes = 0;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreatePartitionErrorResponse((0, errorCode, LeaderId: -1, LeaderEpoch: -1)),
            MetadataHandler = _ =>
            {
                Interlocked.Increment(ref refreshes);
                refreshed.TrySetResult();
                return new(BufferedLeaderMetadata(1));
            }
        };
        // The backoff outlasts the test, so any further fetch would be a fast loop.
        await using var fixture = CreateFixture(connection, retryBackoffMs: 60_000);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        var pending = poll.MoveNextAsync().AsTask();
        try
        {
            await refreshed.Task.WaitAsync(stop.Token);
            // The poll now waits out the partition's backoff instead of fetching again.
            await Task.Delay(200);

            await Assert.That(pending.IsCompleted).IsFalse();
            await Assert.That(connection.ShareFetchRequests).HasSingleItem();
            await Assert.That(Volatile.Read(ref refreshes)).IsEqualTo(1);
        }
        finally
        {
            await stop.CancelAsync();
            try { await pending; } catch (OperationCanceledException) { }
        }
    }

    [Test]
    public async Task Poll_DeletedTopicPartition_KeepsFetchingOtherPartitions()
    {
        var responses = new Queue<ShareFetchResponse>(
        [
            CreatePartitionErrorResponse(
                (0, ErrorCode.UnknownTopicId, LeaderId: -1, LeaderEpoch: -1),
                (1, ErrorCode.None, LeaderId: -1, LeaderEpoch: -1)),
            CreateFetchResponse(1, 200)
        ]);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (_, _) => new(responses.Dequeue())
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            valueDeserializer: Serializers.String, retryBackoffMs: 60_000);
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();

        await Assert.That(poll.Current.Partition).IsEqualTo(1);
        await Assert.That(poll.Current.Offset).IsEqualTo(200);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(2);
        var retried = connection.ShareFetchRequests[1].Topics.Single().Partitions;
        await Assert.That(retried.Select(static partition => partition.PartitionIndex)).IsEquivalentTo([1]);
    }

    [Test]
    public async Task Poll_SuspendedPartitionReportedBySession_DoesNotRefreshMetadataAgain()
    {
        // The broker's share session keeps the suspended partition 0 while the client fetches
        // partition 1 from the same broker, so every response repeats partition 0's error.
        var responses = new Queue<ShareFetchResponse>();
        for (var i = 0; i < 5; i++)
        {
            responses.Enqueue(CreatePartitionErrorResponse(
                (0, ErrorCode.UnknownServerError, LeaderId: -1, LeaderEpoch: -1),
                (1, ErrorCode.None, LeaderId: -1, LeaderEpoch: -1)));
        }
        responses.Enqueue(CreateFetchResponse(1, 200));
        var refreshes = 0;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (_, _) => new(responses.Dequeue()),
            MetadataHandler = _ =>
            {
                Interlocked.Increment(ref refreshes);
                return new(BufferedLeaderMetadata(1));
            }
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            valueDeserializer: Serializers.String, retryBackoffMs: 60_000);
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();

        await Assert.That(poll.Current.Offset).IsEqualTo(200);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(6);
        await Assert.That(Volatile.Read(ref refreshes)).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_PartitionRecoversAfterBackoff()
    {
        var responses = new Queue<ShareFetchResponse>(
        [
            CreatePartitionErrorResponse((0, ErrorCode.UnknownServerError, LeaderId: -1, LeaderEpoch: -1)),
            CreateFetchResponse(0, 300)
        ]);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (_, _) => new(responses.Dequeue())
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            valueDeserializer: Serializers.String, retryBackoffMs: 1);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();

        await Assert.That(poll.Current.Offset).IsEqualTo(300);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(ErrorCode.NotLeaderOrFollower)]
    [Arguments(ErrorCode.FencedLeaderEpoch)]
    [Arguments(ErrorCode.UnknownTopicId)]
    public async Task Poll_InlineAcknowledgementLostToLeaderChange_DropsAndReportsIt(ErrorCode errorCode)
    {
        using var cancellation = new CancellationTokenSource();
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses =
                [
                    new ShareFetchResponseTopic
                    {
                        TopicId = TopicId,
                        Partitions =
                        [
                            new ShareFetchResponsePartition
                            {
                                PartitionIndex = 0,
                                AcknowledgeErrorCode = errorCode,
                                CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                                AcquiredRecords = []
                            }
                        ]
                    }
                ],
                NodeEndpoints = []
            },
            OnSend = cancellation.Cancel
        };
        await using var fixture = CreateFixture(connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await poll.MoveNextAsync();

        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
        await Assert.That(((KafkaException)outcomes!.Single().Exception!).ErrorCode).IsEqualTo(errorCode);
    }

    [Test]
    [Arguments(ErrorCode.UnknownTopicId)]
    [Arguments(ErrorCode.UnknownTopicOrPartition)]
    [Arguments(ErrorCode.TopicAuthorizationFailed)]
    public async Task Poll_InPollAcknowledgePartitionError_KeepsDeliveringAndReportsIt(ErrorCode errorCode)
    {
        // A deleted topic rejects the in-poll drain's acknowledgements for its partitions. Like the
        // ShareFetch-carried path, the poll reports that through the callback and keeps delivering.
        var outcomes = new List<(long FirstOffset, int Length, Exception? Exception)>();
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 100, 3)]),
            ShareAcknowledgeResponses = new([
                CreateAcknowledgeResponse((0, errorCode)),
                CreateAcknowledgeResponse((0, ErrorCode.None))
            ])
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit, maxPollRecords: 1,
            acknowledgementCommitCallback: results =>
            {
                foreach (var result in results)
                    outcomes.Add((result.Offsets[0], result.Offsets.Length, result.Exception));
            });
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        for (var index = 0; index < 3; index++)
        {
            await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(100 + index);
        }

        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(connection.ShareAcknowledgeRequests.Count).IsEqualTo(2);
        var failed = outcomes[0];
        await Assert.That(failed.FirstOffset).IsEqualTo(100);
        await Assert.That(failed.Length).IsEqualTo(1);
        await Assert.That(((KafkaException)failed.Exception!).ErrorCode).IsEqualTo(errorCode);
        // A lost acquisition is dropped; any other failure is retried with the next drain.
        var retried = connection.ShareAcknowledgeRequests[1].Topics[0].Partitions[0].AcknowledgementBatches;
        await Assert.That(retried[0].FirstOffset)
            .IsEqualTo(errorCode == ErrorCode.TopicAuthorizationFailed ? 100L : 101L);
        await Assert.That(outcomes.Skip(1).All(static outcome => outcome.Exception is null)).IsTrue();
    }

    [Test]
    [Arguments(ErrorCode.NotLeaderOrFollower)]
    [Arguments(ErrorCode.FencedLeaderEpoch)]
    public async Task Commit_AcknowledgementLostToLeaderChange_FailsWithoutRetryAndAppliesHint(ErrorCode errorCode)
    {
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var oldLeader = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = new ShareAcknowledgeResponse
            {
                ErrorCode = ErrorCode.None,
                Responses =
                [
                    new ShareAcknowledgeResponseTopic
                    {
                        TopicId = TopicId,
                        Partitions =
                        [
                            new ShareAcknowledgeResponsePartition
                            {
                                PartitionIndex = 0,
                                ErrorCode = errorCode,
                                CurrentLeader = new ShareAcknowledgeLeaderIdAndEpoch { LeaderId = 2, LeaderEpoch = 5 }
                            }
                        ]
                    }
                ],
                NodeEndpoints = [new ShareAcknowledgeNodeEndpoint { NodeId = 2, Host = "localhost", Port = 9093 }]
            }
        };
        var newLeader = new CapturingConnection(ApiKey.ShareAcknowledge, 2, brokerId: 2);
        await using var fixture = CreateFixture(oldLeader, secondConnection: newLeader,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));

        await Assert.That(async () => await fixture.Consumer.CommitAsync()).Throws<KafkaException>();

        // The old leader's acquisition is gone: no retry to it, and no resend to the new leader.
        await Assert.That(oldLeader.ShareAcknowledgeRequests).HasSingleItem();
        await Assert.That(newLeader.ShareAcknowledgeRequests).IsEmpty();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
        var error = (KafkaException)outcomes!.Single().Exception!;
        await Assert.That(error.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(error.Message).Contains("leader changed");
        await Assert.That(fixture.MetadataManager.Metadata.GetPartitionLeader("topic", 0)!.NodeId).IsEqualTo(2);
    }

    [Test]
    public async Task Commit_AcknowledgementLostToLeaderChangeWithoutCallback_IsDropped()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.NotLeaderOrFollower))
        };
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));

        await Assert.That(async () => await fixture.Consumer.CommitAsync()).Throws<KafkaException>();

        await Assert.That(connection.ShareAcknowledgeRequests).HasSingleItem();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    public async Task Poll_AcknowledgementForSuspendedPartition_IsDeferredWithoutFailure()
    {
        var responses = new Queue<ShareFetchResponse>(
        [
            CreatePartitionErrorResponse(
                (0, ErrorCode.UnknownServerError, LeaderId: -1, LeaderEpoch: -1),
                (1, ErrorCode.None, LeaderId: -1, LeaderEpoch: -1)),
            CreateFetchResponse(1, 200),
            CreateFetchResponse(1, 201)
        ]);
        var outcomes = new List<ShareAcknowledgementCommitResult>();
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (_, _) => new(responses.Dequeue())
        };
        await using var fixture = CreateFixture(connection, valueDeserializer: Serializers.String,
            retryBackoffMs: 60_000, acknowledgementCommitCallback: results => outcomes.AddRange(results));
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        fixture.Consumer.Acknowledge(poll.Current);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));
        await Assert.That(await poll.MoveNextAsync()).IsTrue();

        // Partition 0 is backing off, so its acknowledgement waits instead of failing.
        await Assert.That(poll.Current.Offset).IsEqualTo(201);
        var sent = connection.ShareFetchRequests[2].Topics.Single().Partitions;
        await Assert.That(sent.Select(static partition => partition.PartitionIndex)).IsEquivalentTo([1]);
        await Assert.That(outcomes.Select(static outcome => outcome.TopicPartition.Partition)).IsEquivalentTo([1]);
        await Assert.That(outcomes.All(static outcome => outcome.Succeeded)).IsTrue();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsTrue();
    }

    [Test]
    public async Task Commit_UnknownTopicAcknowledgementError_DoesNotDropOtherTopicsAcknowledgements()
    {
        // The share session still reports a topic that metadata has removed. Its error must not
        // be applied to the known topic's acknowledgement, which failed with a retriable error.
        var deletedTopic = new ShareAcknowledgeResponseTopic
        {
            TopicId = Guid.NewGuid(),
            Partitions =
            [
                new ShareAcknowledgeResponsePartition
                {
                    PartitionIndex = 0,
                    ErrorCode = ErrorCode.UnknownTopicId,
                    CurrentLeader = new ShareAcknowledgeLeaderIdAndEpoch()
                }
            ]
        };
        var timedOut = CreateAcknowledgeResponse((0, ErrorCode.RequestTimedOut));
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponses = new Queue<ShareAcknowledgeResponse>(
            [
                new ShareAcknowledgeResponse
                {
                    ErrorCode = ErrorCode.None,
                    Responses = [deletedTopic, .. timedOut.Responses],
                    NodeEndpoints = []
                },
                CreateAcknowledgeResponse((0, ErrorCode.None))
            ])
        };
        await using var fixture = CreateFixture(connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));

        await fixture.Consumer.CommitAsync();

        await Assert.That(connection.ShareAcknowledgeRequests).Count().IsEqualTo(2);
        await Assert.That(outcomes!.Single().Succeeded).IsTrue();
    }

    [Test]
    public async Task Commit_AcknowledgementErrorForRecreatedTopic_IsReported()
    {
        // The topic is deleted and recreated while the acknowledgement is in flight. Metadata names
        // the new topic ID by the time the response for the old one arrives.
        ShareAcknowledgementCommitResult[]? outcomes = null;
        Fixture? fixture = null;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.UnknownTopicId)),
            OnSend = () => fixture!.MetadataManager.Metadata.Update(RecreatedTopicMetadata())
        };
        await using var created = CreateFixture(connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        fixture = created;
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));

        await Assert.That(async () => await fixture.Consumer.CommitAsync()).Throws<KafkaException>();

        await Assert.That(connection.ShareAcknowledgeRequests).HasSingleItem();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
        await Assert.That(((KafkaException)outcomes!.Single().Exception!).ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
    }

    [Test]
    public async Task Poll_InlineAcknowledgementErrorForRecreatedTopic_IsReported()
    {
        using var cancellation = new CancellationTokenSource();
        ShareAcknowledgementCommitResult[]? outcomes = null;
        Fixture? fixture = null;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses =
                [
                    new ShareFetchResponseTopic
                    {
                        TopicId = TopicId,
                        Partitions =
                        [
                            new ShareFetchResponsePartition
                            {
                                PartitionIndex = 0,
                                AcknowledgeErrorCode = ErrorCode.UnknownTopicId,
                                CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                                AcquiredRecords = []
                            }
                        ]
                    }
                ],
                NodeEndpoints = []
            },
            OnSend = () =>
            {
                fixture!.MetadataManager.Metadata.Update(RecreatedTopicMetadata());
                cancellation.Cancel();
            }
        };
        await using var created = CreateFixture(connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        fixture = created;
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await poll.MoveNextAsync();

        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
        await Assert.That(((KafkaException)outcomes!.Single().Exception!).ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
    }

    private static MetadataResponse RecreatedTopicMetadata() => new()
    {
        Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
        Topics =
        [
            new TopicMetadata
            {
                ErrorCode = ErrorCode.None,
                Name = "topic",
                TopicId = Guid.NewGuid(),
                Partitions =
                [
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1,
                        ReplicaNodes = [1], IsrNodes = [1]
                    }
                ]
            }
        ]
    };

    private static ShareFetchResponse CreatePartitionErrorResponse(
        params (int Partition, ErrorCode ErrorCode, int LeaderId, int LeaderEpoch)[] partitions)
        => new()
        {
            ErrorCode = ErrorCode.None,
            Responses =
            [
                new ShareFetchResponseTopic
                {
                    TopicId = TopicId,
                    Partitions = partitions.Select(static partition => new ShareFetchResponsePartition
                    {
                        PartitionIndex = partition.Partition,
                        ErrorCode = partition.ErrorCode,
                        CurrentLeader = new ShareFetchLeaderIdAndEpoch
                        {
                            LeaderId = partition.LeaderId,
                            LeaderEpoch = partition.LeaderEpoch
                        },
                        AcquiredRecords = []
                    }).ToArray()
                }
            ],
            NodeEndpoints = [new ShareFetchNodeEndpoint { NodeId = 2, Host = "localhost", Port = 9093 }]
        };
}
