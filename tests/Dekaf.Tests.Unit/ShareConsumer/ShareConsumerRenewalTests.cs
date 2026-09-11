using System.Buffers;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    private static readonly Guid TopicId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    [Test]
    public async Task ShareFetch_Renewal_UsesV2AndZeroFetchLimits()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection);
        var acknowledgements = MixedAcknowledgements();

        await InvokeShareFetchAsync(
            fixture.Consumer,
            acknowledgements,
            [new TopicPartition("topic", 0), new TopicPartition("topic", 1)]);

        var request = connection.ShareFetchRequest!;
        await Assert.That(connection.LastApiVersion).IsEqualTo((short)2);
        await Assert.That(request.IsRenewAck).IsTrue();
        await Assert.That(request.MaxWaitMs).IsEqualTo(0);
        await Assert.That(request.MinBytes).IsEqualTo(0);
        await Assert.That(request.MaxBytes).IsEqualTo(0);
        await Assert.That(request.MaxRecords).IsEqualTo(0);
        await Assert.That(request.BatchSize).IsEqualTo(0);
        var acknowledgementTypes = request.Topics[0].Partitions
            .SelectMany(static partition => partition.AcknowledgementBatches!)
            .SelectMany(static batch => batch.AcknowledgeTypes)
            .ToArray();
        await Assert.That(acknowledgementTypes).Contains((byte)AcknowledgeType.Accept);
        await Assert.That(acknowledgementTypes).Contains((byte)AcknowledgeType.Renew);
        await Assert.That(acknowledgementTypes).Contains((byte)AcknowledgeType.Reject);
    }

    [Test]
    public async Task ShareFetch_Renewal_OnV1Broker_FailsLocally()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 1);
        await using var fixture = CreateFixture(connection);

        var exception = await Assert.That(async () =>
                await InvokeShareFetchAsync(fixture.Consumer, RenewalAcknowledgements()))
            .Throws<BrokerVersionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);
        await Assert.That(connection.SendCount).IsEqualTo(0);
    }

    [Test]
    public async Task ShareAcknowledge_Renewal_UsesV2AndPublishesLockTimeout()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = new ShareAcknowledgeResponse
            {
                ErrorCode = ErrorCode.None,
                AcquisitionLockTimeoutMs = 45_000,
                Responses = [],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(connection);

        await InvokeShareAcknowledgeAsync(fixture.Consumer, RenewalAcknowledgements());

        await Assert.That(connection.LastApiVersion).IsEqualTo((short)2);
        await Assert.That(connection.ShareAcknowledgeRequest!.IsRenewAck).IsTrue();
        await Assert.That(fixture.Consumer.AcquisitionLockTimeoutMs).IsEqualTo(45_000);
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(1);
    }

    [Test]
    public async Task ShareAcknowledge_Renewal_OnV1Broker_FailsLocally()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 1);
        await using var fixture = CreateFixture(connection);

        var exception = await Assert.That(async () =>
                await InvokeShareAcknowledgeAsync(fixture.Consumer, RenewalAcknowledgements()))
            .Throws<BrokerVersionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);
        await Assert.That(connection.SendCount).IsEqualTo(0);
    }

    [Test]
    public async Task Acknowledge_Renewal_InImplicitMode_IsRejected()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit);

        await Assert.That(() => fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Renew))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ShareFetch_RenewalFlag_IsScopedToBrokerPartitions()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection);
        var acknowledgements = MixedAcknowledgements();

        await InvokeShareFetchAsync(
            fixture.Consumer,
            acknowledgements,
            [new TopicPartition("topic", 0)]);
        await Assert.That(connection.ShareFetchRequest!.IsRenewAck).IsFalse();
        await Assert.That(connection.ShareFetchRequest.MaxWaitMs).IsGreaterThan(0);

        await InvokeShareFetchAsync(
            fixture.Consumer,
            acknowledgements,
            [new TopicPartition("topic", 1)]);
        await Assert.That(connection.ShareFetchRequest!.IsRenewAck).IsTrue();
        await Assert.That(connection.ShareFetchRequest.MaxWaitMs).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Poll_AcquisitionTimestamp_UsesEachBrokerResponseAndSurvivesBuffering(bool bufferRecords)
    {
        var received = new long[2];
        var first = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(partition: 0, offset: 42, recordCount: 2),
            OnSend = () => received[0] = System.Diagnostics.Stopwatch.GetTimestamp()
        };
        var second = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchResponse = CreateFetchResponse(partition: 1, offset: 100),
            OnSend = () => received[1] = System.Diagnostics.Stopwatch.GetTimestamp()
        };
        await using var fixture = CreateFixture(first, secondConnection: second);
        var hosted = (IHostedShareConsumer)fixture.Consumer;
        hosted.ObserveAcknowledgements(static _ => { });
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        if (bufferRecords)
        {
            fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Renew);
            FlushPendingAcknowledgements(fixture.Consumer);
            ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());
        }

        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        var timestamps = new Dictionary<int, long>();
        for (var index = 0; index < 3; index++)
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            var timestamp = hosted.AcquisitionStartedTimestamp;
            var responseBoundary = received[poll.Current.Partition];
            await Assert.That(timestamp).IsGreaterThanOrEqualTo(responseBoundary);
            var lastResponseBoundary = Math.Max(received[0], received[1]);
            if (responseBoundary < lastResponseBoundary)
                await Assert.That(timestamp).IsLessThan(lastResponseBoundary);
            if (timestamps.TryGetValue(poll.Current.Partition, out var earlierRecordTimestamp))
                await Assert.That(timestamp).IsEqualTo(earlierRecordTimestamp);
            timestamps[poll.Current.Partition] = timestamp;
        }
        await Assert.That(timestamps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Poll_RenewalSuccess_ReplaysRecordAndPublishesLockTimeout()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                AcquisitionLockTimeoutMs = 30_000,
                Responses = [],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(connection);
        var record = CreateRecord();
        var hosted = (IHostedShareConsumer)fixture.Consumer;
        hosted.ObserveAcknowledgements(static _ => { });
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);

        var beforePoll = System.Diagnostics.Stopwatch.GetTimestamp();
        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        var moved = await poll.MoveNextAsync();

        await Assert.That(moved).IsTrue();
        await Assert.That(ReferenceEquals(poll.Current, record)).IsTrue();
        await Assert.That(fixture.Consumer.AcquisitionLockTimeoutMs).IsEqualTo(30_000);
        await Assert.That(fixture.Consumer.RenewedRecordReplayCount).IsEqualTo(1);
        await Assert.That(hosted.AcquisitionStartedTimestamp).IsGreaterThanOrEqualTo(beforePoll);
    }

    [Test]
    public async Task Poll_FetchedRecord_ReservesBudgetBeforeRenewalReplay()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(partition: 0, offset: 100)
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 1);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());
        FlushPendingAcknowledgements(fixture.Consumer);

        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        var moved = await poll.MoveNextAsync();

        await Assert.That(moved).IsTrue();
        await Assert.That(poll.Current.Offset).IsEqualTo(100);
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).HasSingleItem();
    }

    [Test]
    public async Task Commit_RenewalRetry_PreservesEachSuccessfulResponseTimeForReplay()
    {
        var received = new List<long>();
        CapturingConnection connection = null!;
        connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareAcknowledgeResponses = new Queue<ShareAcknowledgeResponse>(
            [
                CreateAcknowledgeResponse((0, ErrorCode.None), (1, ErrorCode.NotLeaderOrFollower)),
                CreateAcknowledgeResponse((1, ErrorCode.None))
            ]),
            OnSend = () =>
            {
                if (connection.ShareAcknowledgeRequests.Count > received.Count)
                    received.Add(System.Diagnostics.Stopwatch.GetTimestamp());
            }
        };
        await using var fixture = CreateFixture(connection);
        var hosted = (IHostedShareConsumer)fixture.Consumer;
        hosted.ObserveAcknowledgements(static _ => { });
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Renew);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41), AcknowledgeType.Renew);

        await fixture.Consumer.CommitAsync();
        var beforeReplay = System.Diagnostics.Stopwatch.GetTimestamp();
        await Assert.That(received.Count).IsEqualTo(2);
        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        for (var index = 0; index < 2; index++)
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            var partition = poll.Current.Partition;
            await Assert.That(hosted.AcquisitionStartedTimestamp).IsGreaterThanOrEqualTo(received[partition]);
            await Assert.That(hosted.AcquisitionStartedTimestamp).IsLessThanOrEqualTo(beforeReplay);
            if (partition == 0)
                await Assert.That(hosted.AcquisitionStartedTimestamp).IsLessThan(received[1]);
        }
    }

    [Test]
    public async Task Poll_TerminalDispositionSkipsRemainingRenewedSnapshot()
    {
        await using var fixture = CreateFixture(new CapturingConnection(ApiKey.ShareFetch, 2));
        var hosted = (IHostedShareConsumer)fixture.Consumer;
        hosted.ObserveAcknowledgements(static _ => { });
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var first = CreateRecord(offset: 42);
        var second = CreateRecord(offset: 43);
        fixture.Consumer.Acknowledge(first, AcknowledgeType.Renew);
        fixture.Consumer.Acknowledge(second, AcknowledgeType.Renew);
        using var cancellation = new CancellationTokenSource();
        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();

        fixture.Consumer.Acknowledge(first, AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(second, AcknowledgeType.Accept);
        await cancellation.CancelAsync();

        await Assert.That(await poll.MoveNextAsync()).IsFalse();
    }

    [Test]
    public async Task Poll_MaxPollRecords_ProcessesEveryBrokerResponse()
    {
        var firstConnection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(partition: 0, offset: 100)
        };
        var secondConnection = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses = [],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(
            firstConnection,
            maxPollRecords: 1,
            secondConnection: secondConnection);
        PrepareForPoll(
            fixture.Consumer,
            new TopicPartition("topic", 0),
            new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");

        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        var moved = await poll.MoveNextAsync();

        await Assert.That(moved).IsTrue();
        await Assert.That(poll.Current.Offset).IsEqualTo(100);
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(1);
        await Assert.That(GetSessionEpoch(fixture.Consumer, 2)).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_BrokerFailure_RequeuesOnlyFailedBrokerAcknowledgements()
    {
        var firstConnection = new CapturingConnection(ApiKey.ShareFetch, 1);
        var secondConnection = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses = [],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(
            firstConnection,
            secondConnection: secondConnection);
        PrepareForPoll(
            fixture.Consumer,
            new TopicPartition("topic", 0),
            new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Renew);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41), AcknowledgeType.Accept);

        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(async () => await poll.MoveNextAsync())
            .Throws<BrokerVersionException>();

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo([new TopicPartition("topic", 0)]);
        await Assert.That(GetSessionEpoch(fixture.Consumer, 2)).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_CancelledRetry_RequeuesFailedBrokerAndProcessesSuccessfulBroker()
    {
        using var cancellation = new CancellationTokenSource();
        var firstConnection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchException = new KafkaException(
                ErrorCode.RequestTimedOut,
                "simulated timeout"),
            OnSend = cancellation.Cancel
        };
        var secondConnection = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses = [],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(
            firstConnection,
            secondConnection: secondConnection);
        PrepareForPoll(
            fixture.Consumer,
            new TopicPartition("topic", 0),
            new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Renew);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41), AcknowledgeType.Accept);

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await Assert.That(async () => await poll.MoveNextAsync())
            .Throws<OperationCanceledException>();

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo([new TopicPartition("topic", 0)]);
        await Assert.That(GetSessionEpoch(fixture.Consumer, 2)).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_CancelledResponseRetry_RequeuesFailedBrokerAndProcessesSuccessfulBroker()
    {
        using var cancellation = new CancellationTokenSource();
        var firstConnection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.RequestTimedOut,
                Responses = [],
                NodeEndpoints = []
            },
            OnSend = cancellation.Cancel
        };
        var secondConnection = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses = [],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(
            firstConnection,
            secondConnection: secondConnection);
        PrepareForPoll(
            fixture.Consumer,
            new TopicPartition("topic", 0),
            new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 42), AcknowledgeType.Renew);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 43), AcknowledgeType.Accept);

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await Assert.That(async () => await poll.MoveNextAsync())
            .Throws<OperationCanceledException>();

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo([new TopicPartition("topic", 0)]);
        await Assert.That(GetSessionEpoch(fixture.Consumer, 2)).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_InlineAcknowledgementError_RequeuesAndDoesNotReplayRenewal()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                AcquisitionLockTimeoutMs = 30_000,
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
                                AcknowledgeErrorCode = ErrorCode.InvalidRecordState,
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
        await using var fixture = CreateFixture(connection);
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Renew);

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        var moved = await poll.MoveNextAsync();

        await Assert.That(moved).IsFalse();
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).IsEmpty();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsTrue();
    }

    [Test]
    public async Task Poll_InlineAcknowledgementError_RequeuesOnlyFailedPartition()
    {
        using var cancellation = new CancellationTokenSource();
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
                                AcknowledgeErrorCode = ErrorCode.None,
                                CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                                AcquiredRecords = []
                            },
                            new ShareFetchResponsePartition
                            {
                                PartitionIndex = 1,
                                AcknowledgeErrorCode = ErrorCode.InvalidRecordState,
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
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(
            fixture.Consumer,
            new TopicPartition("topic", 0),
            new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41), AcknowledgeType.Renew);

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await poll.MoveNextAsync();

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo([new TopicPartition("topic", 1)]);
    }

    [Test]
    public async Task Commit_PartitionError_RequeuesOnlyFailedPartition()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
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
                                ErrorCode = ErrorCode.None,
                                CurrentLeader = new ShareAcknowledgeLeaderIdAndEpoch()
                            },
                            new ShareAcknowledgeResponsePartition
                            {
                                PartitionIndex = 1,
                                ErrorCode = ErrorCode.InvalidRecordState,
                                CurrentLeader = new ShareAcknowledgeLeaderIdAndEpoch()
                            }
                        ]
                    }
                ],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41), AcknowledgeType.Renew);

        await Assert.That(async () => await fixture.Consumer.CommitAsync())
            .Throws<KafkaException>();

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo([new TopicPartition("topic", 1)]);
    }

    [Test]
    public async Task Commit_RetriablePartitionError_RetriesOnlyFailedPartition()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponses = new Queue<ShareAcknowledgeResponse>(
            [
                CreateAcknowledgeResponse(
                    (0, ErrorCode.None),
                    (1, ErrorCode.NotLeaderOrFollower)),
                CreateAcknowledgeResponse((1, ErrorCode.None))
            ])
        };
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41), AcknowledgeType.Renew);

        await fixture.Consumer.CommitAsync();

        await Assert.That(connection.ShareAcknowledgeRequests).Count().IsEqualTo(2);
        var retryPartitions = connection.ShareAcknowledgeRequests[1].Topics
            .SelectMany(static topic => topic.Partitions)
            .Select(static partition => partition.PartitionIndex)
            .ToArray();
        await Assert.That(retryPartitions).IsEquivalentTo([1]);
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    public async Task Commit_Success_ReportsOrderedPartitionOutcomes()
    {
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2);
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41));
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 42));
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));

        await fixture.Consumer.CommitAsync();

        await Assert.That(outcomes).IsNotNull();
        await Assert.That(outcomes!).Count().IsEqualTo(2);
        await Assert.That(outcomes![0].TopicPartition).IsEqualTo(new TopicPartition("topic", 0));
        await Assert.That(CopyOffsets(outcomes[0].Offsets)).IsEquivalentTo([40L, 42L]);
        await Assert.That(outcomes[0].Succeeded).IsTrue();
        await Assert.That(outcomes[1].TopicPartition).IsEqualTo(new TopicPartition("topic", 1));
        await Assert.That(CopyOffsets(outcomes[1].Offsets)).IsEquivalentTo([41L]);
        await Assert.That(outcomes[1].Succeeded).IsTrue();
    }

    [Test]
    public async Task Commit_RetriableFailure_ReportsOnlyFinalSuccess()
    {
        var callbackCount = 0;
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponses = new Queue<ShareAcknowledgeResponse>(
            [
                CreateAcknowledgeResponse((0, ErrorCode.NotLeaderOrFollower)),
                CreateAcknowledgeResponse((0, ErrorCode.None))
            ])
        };
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: results =>
            {
                callbackCount++;
                outcomes = results.ToArray();
            });
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord());

        await fixture.Consumer.CommitAsync();

        await Assert.That(connection.ShareAcknowledgeRequests).Count().IsEqualTo(2);
        await Assert.That(callbackCount).IsEqualTo(1);
        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(outcomes![0].Succeeded).IsTrue();
    }

    [Test]
    public async Task Commit_TerminalFailure_ReportsPerPartitionAfterRequeue()
    {
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var pendingDuringCallback = false;
        KafkaShareConsumer<string, string>? consumer = null;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = CreateAcknowledgeResponse(
                (0, ErrorCode.None),
                (1, ErrorCode.InvalidRecordState))
        };
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: results =>
            {
                outcomes = results.ToArray();
                pendingDuringCallback = HasPendingAcknowledgements(consumer!);
            });
        consumer = fixture.Consumer;
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41));

        await Assert.That(async () => await fixture.Consumer.CommitAsync())
            .Throws<KafkaException>();

        await Assert.That(outcomes).IsNotNull();
        await Assert.That(outcomes![0].Succeeded).IsTrue();
        await Assert.That(outcomes[1].Exception).IsTypeOf<KafkaException>();
        await Assert.That(((KafkaException)outcomes[1].Exception!).ErrorCode)
            .IsEqualTo(ErrorCode.InvalidRecordState);
        await Assert.That(pendingDuringCallback).IsTrue();
    }

    [Test]
    public async Task Commit_UnresolvedLeader_RequeuesAndReportsPartitionFailure()
    {
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2);
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(topic: "missing-topic"));

        await Assert.That(async () => await fixture.Consumer.CommitAsync())
            .Throws<KafkaException>();

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys)
            .IsEquivalentTo([new TopicPartition("missing-topic", 0)]);
        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(((KafkaException)outcomes![0].Exception!).ErrorCode)
            .IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(connection.ShareAcknowledgeRequests).IsEmpty();
    }

    [Test]
    public async Task Commit_CallbackException_DoesNotChangeSuccess()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2);
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: static _ => throw new InvalidOperationException("callback"));
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord());

        await fixture.Consumer.CommitAsync();

        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    public async Task Commit_Cancellation_RequeuesAndReportsFailure()
    {
        using var cancellation = new CancellationTokenSource();
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = CreateAcknowledgeResponse(
                (0, ErrorCode.None),
                (1, ErrorCode.NotLeaderOrFollower)),
            OnSend = cancellation.Cancel
        };
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41));

        await Assert.That(async () => await fixture.Consumer.CommitAsync(cancellation.Token))
            .Throws<OperationCanceledException>();

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo([new TopicPartition("topic", 1)]);
        await Assert.That(outcomes).IsNotNull();
        await Assert.That(outcomes![0].Succeeded).IsTrue();
        await Assert.That(outcomes[1].Exception).IsTypeOf<OperationCanceledException>();
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit)]
    [Arguments(ShareAcknowledgementMode.Explicit)]
    public async Task Poll_InlineAcknowledgement_ReportsOutcomeInBothModes(
        ShareAcknowledgementMode acknowledgementMode)
    {
        using var cancellation = new CancellationTokenSource();
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            OnSend = cancellation.Cancel
        };
        await using var fixture = CreateFixture(
            connection,
            acknowledgementMode,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        if (acknowledgementMode == ShareAcknowledgementMode.Implicit)
            TrackDeliveredRecord(fixture.Consumer, new TopicPartition("topic", 0), 42);
        else
            fixture.Consumer.Acknowledge(CreateRecord());

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await poll.MoveNextAsync();

        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(outcomes![0].Succeeded).IsTrue();
        await Assert.That(CopyOffsets(outcomes[0].Offsets)).IsEquivalentTo([42L]);
    }

    [Test]
    public async Task Dispose_Flush_ReportsAcknowledgementOutcome()
    {
        var callbackCount = 0;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2);
        var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: _ => callbackCount++);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord());

        await fixture.DisposeAsync();

        await Assert.That(callbackCount).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_SessionLoss_PreservesRenewalForRetry()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.ShareSessionNotFound,
                Responses = [],
                NodeEndpoints = []
            },
            OnSend = cancellation.Cancel
        };
        await using var fixture = CreateFixture(connection);
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var record = CreateRecord();
        fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        var moved = await poll.MoveNextAsync();

        await Assert.That(moved).IsFalse();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsTrue();
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());
        var active = GetActiveRenewedRecords(fixture.Consumer, assignment);
        await Assert.That(active).HasSingleItem();
        await Assert.That(ReferenceEquals(active[0], record)).IsTrue();
    }

    [Test]
    public async Task Poll_SessionLoss_ReplaysActiveRenewal()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.ShareSessionNotFound,
                Responses = [],
                NodeEndpoints = []
            },
            OnSend = cancellation.Cancel
        };
        await using var fixture = CreateFixture(connection);
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var record = CreateRecord();
        fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        var moved = await poll.MoveNextAsync();

        await Assert.That(moved).IsTrue();
        await Assert.That(ReferenceEquals(poll.Current, record)).IsTrue();
        var active = GetActiveRenewedRecords(fixture.Consumer, assignment);
        await Assert.That(active).HasSingleItem();
        await Assert.That(ReferenceEquals(active[0], record)).IsTrue();
    }

    [Test]
    public async Task SuccessfulRenewal_ReplaysRecordUntilTerminalAcknowledgementSucceeds()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection);
        var record = CreateRecord();
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };

        fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());

        var replayed = GetActiveRenewedRecords(fixture.Consumer, assignment);
        await Assert.That(replayed).HasSingleItem();
        await Assert.That(ReferenceEquals(replayed[0], record)).IsTrue();
        await Assert.That(fixture.Consumer.RenewalRequestCount).IsEqualTo(1);

        fixture.Consumer.Acknowledge(record, AcknowledgeType.Accept);
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).HasSingleItem();

        ApplySuccessfulAcknowledgements(fixture.Consumer, TerminalAcknowledgements());
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).IsEmpty();
    }

    [Test]
    [Arguments(AcknowledgeType.Accept)]
    [Arguments(AcknowledgeType.Release)]
    [Arguments(AcknowledgeType.Reject)]
    public async Task HostedTerminalAcknowledgement_RemovesLocalReplayButRetainsPendingSubmission(AcknowledgeType type)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection);
        ((IHostedShareConsumer)fixture.Consumer).ObserveAcknowledgements(static _ => { });
        var record = CreateRecord();
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };

        fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).HasSingleItem();

        fixture.Consumer.Acknowledge(record, type);

        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).IsEmpty();
        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending[new TopicPartition("topic", 0)].Single().AcknowledgeTypes)
            .IsEquivalentTo(new byte[] { (byte)type });
    }

    [Test]
    public async Task RenewedRecord_IsRemovedByRedeliveryOrAssignmentLoss()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection);
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };

        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());
        InvokePrivate(fixture.Consumer, "RemoveRenewedRecord", "topic", 0, 42L);
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).IsEmpty();

        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());
        InvokePrivate(
            fixture.Consumer,
            "RemoveRenewedRecordsOutsideAssignment",
            new HashSet<TopicPartition>());
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).IsEmpty();
    }

    [Test]
    public async Task Dispose_ClearsRenewedRecords()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        var fixture = CreateFixture(connection);
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };
        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());

        await fixture.DisposeAsync();

        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).IsEmpty();
    }

    [Test]
    public async Task Dispose_FlushesPendingAcknowledgements()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = new ShareAcknowledgeResponse
            {
                ErrorCode = ErrorCode.None,
                Responses = [],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Accept);

        await fixture.Consumer.DisposeAsync();

        await Assert.That(connection.ShareAcknowledgeRequest).IsNotNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Close_ImplicitDelivery_ReleasesInsteadOfAccepting(bool dispose)
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2);
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit);
        PrepareForPoll(fixture.Consumer);
        TrackDeliveredRecord(fixture.Consumer, new TopicPartition("topic", 0), 42);

        if (dispose)
            await fixture.Consumer.DisposeAsync();
        else
            await fixture.Consumer.CloseAsync();

        var acknowledgement = connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0].AcknowledgementBatches[0];
        await Assert.That(acknowledgement.AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Release);
    }

    [Test]
    [Arguments(AcknowledgeType.Accept)]
    [Arguments(AcknowledgeType.Release)]
    [Arguments(AcknowledgeType.Reject)]
    [Arguments(AcknowledgeType.Renew)]
    public async Task Close_ExplicitDisposition_PreservesSelectedOutcome(AcknowledgeType type)
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2);
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(), type);

        await fixture.Consumer.CloseAsync();

        var acknowledgement = connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0].AcknowledgementBatches[0];
        await Assert.That(acknowledgement.AcknowledgeTypes[0]).IsEqualTo((byte)type);
    }

    [Test]
    public async Task Close_CancelledAcknowledgement_NeverRequeuesImplicitAccept()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            OnSend = () => throw new OperationCanceledException(cancellation.Token)
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit);
        PrepareForPoll(fixture.Consumer);
        TrackDeliveredRecord(fixture.Consumer, new TopicPartition("topic", 0), 42);

        // Close handles acknowledgement failures as best effort; unexpected exceptions must fail this test.
        await fixture.Consumer.CloseAsync(cancellation.Token);

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending[new TopicPartition("topic", 0)][0].AcknowledgeTypes[0])
            .IsEqualTo((byte)AcknowledgeType.Release);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Poll_PartialEnumeration_DoesNotTrackUnyieldedRecords(bool requiresPreparation)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 42, recordCount: 2)
        };
        var preparer = requiresPreparation ? new PausedDeserializerPreparer() : null;
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit, valueDeserializer: preparer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using (var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator())
        {
            var moveNext = poll.MoveNextAsync();
            if (preparer is not null)
            {
                await preparer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.That(moveNext.IsCompleted).IsFalse();
                preparer.Release.SetResult();
            }
            await Assert.That(await moveNext).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(42);
        }

        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        var batch = pending[new TopicPartition("topic", 0)][0];
        await Assert.That(batch.FirstOffset).IsEqualTo(42);
        await Assert.That(batch.LastOffset).IsEqualTo(42);
    }

    private static Fixture CreateFixture(
        CapturingConnection connection,
        ShareAcknowledgementMode acknowledgementMode = ShareAcknowledgementMode.Explicit,
        int maxPollRecords = 500,
        CapturingConnection? secondConnection = null,
        ShareAcknowledgementCommitCallback? acknowledgementCommitCallback = null,
        IDeserializer<string>? valueDeserializer = null,
        Func<CancellationToken, ValueTask<IKafkaConnection>>? leaseHandler = null,
        int fetchMaxWaitMs = 200)
    {
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-group",
            AcknowledgementMode = acknowledgementMode,
            MaxPollRecords = maxPollRecords,
            FetchMaxWaitMs = fetchMaxWaitMs,
            AcknowledgementCommitCallback = acknowledgementCommitCallback
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>()).Returns(connection);
        if (leaseHandler is not null)
            pool.GetConnectionAsync(1, Arg.Any<CancellationToken>())
                .Returns(call => leaseHandler(call.Arg<CancellationToken>()));
        if (secondConnection is not null)
            pool.GetConnectionAsync(2, Arg.Any<CancellationToken>()).Returns(secondConnection);
        var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = secondConnection is null
                ? [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }]
                :
                [
                    new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 },
                    new BrokerMetadata { NodeId = 2, Host = "localhost", Port = 9093 }
                ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "topic",
                    TopicId = TopicId,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        },
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 1,
                            LeaderId = secondConnection is null ? 1 : 2,
                            ReplicaNodes = [secondConnection is null ? 1 : 2],
                            IsrNodes = [secondConnection is null ? 1 : 2]
                        }
                    ]
                }
            ]
        });
        var consumer = new KafkaShareConsumer<string, string>(
            options,
            Substitute.For<IDeserializer<string>>(),
            valueDeserializer ?? Substitute.For<IDeserializer<string>>(),
            pool,
            metadataManager);
        SetMemberId(consumer, "member-1");
        return new Fixture(consumer, metadataManager);
    }

    private sealed class PausedDeserializerPreparer : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        private bool _prepared;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            Serializers.String.Deserialize(data, context);

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            value = _prepared ? Deserialize(data, context) : string.Empty;
            return _prepared;
        }

        public async ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            _prepared = true;
        }
    }

    private static ShareFetchResponse CreateFetchResponse(int partition, long offset, int recordCount = 1)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var batch = new RecordBatch
        {
            BaseOffset = offset,
            LastOffsetDelta = recordCount - 1,
            Records = Enumerable.Range(0, recordCount).Select(index => new Record { OffsetDelta = index, IsKeyNull = true, Value = "new-value"u8.ToArray() }).ToList()
        };
        batch.Write(buffer);

        return new ShareFetchResponse
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
                            PartitionIndex = partition,
                            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                            RecordBytes = buffer.WrittenMemory,
                            AcquiredRecords =
                            [
                                new ShareFetchAcquiredRecords
                                {
                                    FirstOffset = offset,
                                    LastOffset = offset + recordCount - 1,
                                    DeliveryCount = 1
                                }
                            ]
                        }
                    ]
                }
            ],
            NodeEndpoints = []
        };
    }

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>> RenewalAcknowledgements()
        => new()
        {
            [new TopicPartition("topic", 0)] =
            [
                new AcknowledgementBatchData(
                    42,
                    42,
                    [(byte)AcknowledgeType.Renew])
            ]
        };

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>> TerminalAcknowledgements()
        => new()
        {
            [new TopicPartition("topic", 0)] =
            [
                new AcknowledgementBatchData(
                    42,
                    42,
                    [(byte)AcknowledgeType.Accept])
            ]
        };

    private static ShareAcknowledgeResponse CreateAcknowledgeResponse(
        params (int Partition, ErrorCode ErrorCode)[] partitions)
        => new()
        {
            ErrorCode = ErrorCode.None,
            Responses =
            [
                new ShareAcknowledgeResponseTopic
                {
                    TopicId = TopicId,
                    Partitions = partitions.Select(static partition =>
                        new ShareAcknowledgeResponsePartition
                        {
                            PartitionIndex = partition.Partition,
                            ErrorCode = partition.ErrorCode,
                            CurrentLeader = new ShareAcknowledgeLeaderIdAndEpoch()
                        }).ToArray()
                }
            ],
            NodeEndpoints = []
        };

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>> RenewalAcknowledgementsForBothPartitions()
        => new()
        {
            [new TopicPartition("topic", 0)] =
            [
                new AcknowledgementBatchData(
                    40,
                    40,
                    [(byte)AcknowledgeType.Renew])
            ],
            [new TopicPartition("topic", 1)] =
            [
                new AcknowledgementBatchData(
                    41,
                    41,
                    [(byte)AcknowledgeType.Renew])
            ]
        };

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>> MixedAcknowledgements()
        => new()
        {
            [new TopicPartition("topic", 0)] =
            [
                new AcknowledgementBatchData(
                    40,
                    40,
                    [(byte)AcknowledgeType.Accept])
            ],
            [new TopicPartition("topic", 1)] =
            [
                new AcknowledgementBatchData(
                    41,
                    42,
                    [(byte)AcknowledgeType.Renew, (byte)AcknowledgeType.Reject])
            ]
        };

    private static ShareConsumeResult<string, string> CreateRecord(
        int partition = 0,
        long offset = 42,
        string topic = "topic") => new()
    {
        Topic = topic,
        Partition = partition,
        Offset = offset,
        Value = "value",
        DeliveryCount = 1
    };

    private static Task InvokeShareFetchAsync(
        KafkaShareConsumer<string, string> consumer,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements,
        List<TopicPartition>? partitions = null)
        => (Task)InvokePrivate(
            consumer,
            "SendShareFetchForPartitionsAsync",
            1,
            partitions ?? [new TopicPartition("topic", 0)],
            acknowledgements,
            CancellationToken.None)!;

    private static Task InvokeShareAcknowledgeAsync(
        KafkaShareConsumer<string, string> consumer,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements)
        => (Task)InvokePrivate(
            consumer,
            "SendAcknowledgeAsync",
            1,
            acknowledgements,
            false,
            CancellationToken.None,
            false)!;

    private static void ApplySuccessfulAcknowledgements(
        KafkaShareConsumer<string, string> consumer,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements)
        => InvokePrivate(consumer, "ApplySuccessfulAcknowledgements", acknowledgements, System.Diagnostics.Stopwatch.GetTimestamp());

    private static List<ShareConsumeResult<string, string>> GetActiveRenewedRecords(
        KafkaShareConsumer<string, string> consumer,
        IReadOnlySet<TopicPartition> assignment)
        => (List<ShareConsumeResult<string, string>>)InvokePrivate(
            consumer,
            "GetActiveRenewedRecords",
            assignment,
            10)!;

    private static bool HasPendingAcknowledgements(
        KafkaShareConsumer<string, string> consumer)
    {
        var tracker = typeof(KafkaShareConsumer<string, string>)
            .GetField("_ackTracker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        return (bool)tracker.GetType()
            .GetProperty("HasPending", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(tracker)!;
    }

    private static long[] CopyOffsets(ShareAcknowledgedOffsets offsets)
    {
        var copy = new long[offsets.Length];
        offsets.CopyTo(copy);
        return copy;
    }

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>> FlushPendingAcknowledgements(
        KafkaShareConsumer<string, string> consumer)
    {
        var tracker = (AcknowledgementTracker)typeof(KafkaShareConsumer<string, string>)
            .GetField("_ackTracker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        return tracker.Flush();
    }

    private static void TrackDeliveredRecord(
        KafkaShareConsumer<string, string> consumer,
        TopicPartition topicPartition,
        long offset)
    {
        var tracker = (AcknowledgementTracker)typeof(KafkaShareConsumer<string, string>)
            .GetField("_ackTracker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        tracker.TrackDeliveredRecords(topicPartition, offset, offset);
    }

    private static int GetSessionEpoch(
        KafkaShareConsumer<string, string> consumer,
        int brokerId)
    {
        var sessionManager = (ShareSessionManager)typeof(KafkaShareConsumer<string, string>)
            .GetField("_sessionManager", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        return sessionManager.GetSessionEpoch(brokerId);
    }

    private static object? InvokePrivate(
        KafkaShareConsumer<string, string> consumer,
        string methodName,
        params object?[] arguments)
        => typeof(KafkaShareConsumer<string, string>)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(consumer, arguments);

    private static void SetMemberId(
        KafkaShareConsumer<string, string> consumer,
        string memberId)
    {
        var coordinator = typeof(KafkaShareConsumer<string, string>)
            .GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        typeof(ShareConsumerCoordinator)
            .GetField("_memberId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(coordinator, memberId);
    }

    private static void PrepareForPoll(
        KafkaShareConsumer<string, string> consumer,
        params TopicPartition[] assignedPartitions)
    {
        typeof(KafkaShareConsumer<string, string>)
            .GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(consumer, true);
        var coordinator = typeof(KafkaShareConsumer<string, string>)
            .GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        if (assignedPartitions.Length == 0)
            assignedPartitions = [new TopicPartition("topic", 0)];
        typeof(ShareConsumerCoordinator)
            .GetField("_assignedPartitions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(coordinator, assignedPartitions.ToHashSet());
        typeof(ShareConsumerCoordinator)
            .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(coordinator, CoordinatorState.Stable);
    }

    private sealed class Fixture(
        KafkaShareConsumer<string, string> consumer,
        MetadataManager metadataManager) : IAsyncDisposable
    {
        internal KafkaShareConsumer<string, string> Consumer { get; } = consumer;
        internal MetadataManager MetadataManager { get; } = metadataManager;

        public async ValueTask DisposeAsync()
        {
            await Consumer.DisposeAsync();
            await MetadataManager.DisposeAsync();
        }
    }

    private sealed class CapturingConnection(
        ApiKey apiKey,
        short maximumVersion,
        int brokerId = 1,
        bool includeShareAcknowledge = false,
        bool supportShareFetch = false) :
        IKafkaConnection,
        IKafkaCapabilityProvider,
        IKafkaRequestCancellationConnection
    {
        public int BrokerId => brokerId;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } =
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(apiKey, 0, maximumVersion),
                    .. supportShareFetch ? new[] { new ApiVersion(ApiKey.ShareFetch, 0, 2) } : [],
                    .. includeShareAcknowledge ? new[] { new ApiVersion(ApiKey.ShareAcknowledge, 0, maximumVersion) } : [],
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            });
        internal int SendCount { get; private set; }
        internal short LastApiVersion { get; private set; }
        internal ShareFetchRequest? ShareFetchRequest { get; private set; }
        internal ShareAcknowledgeRequest? ShareAcknowledgeRequest { get; private set; }
        internal ShareFetchResponse? ShareFetchResponse { get; init; }
        internal Queue<ShareFetchResponse>? ShareFetchResponses { get; init; }
        internal ShareAcknowledgeResponse? ShareAcknowledgeResponse { get; init; }
        internal Queue<ShareAcknowledgeResponse>? ShareAcknowledgeResponses { get; init; }
        internal List<ShareAcknowledgeRequest> ShareAcknowledgeRequests { get; } = [];
        internal Exception? ShareFetchException { get; init; }
        internal Action? OnSend { get; init; }
        internal TaskCompletionSource<ShareAcknowledgeResponse>? DelayedFinalAcknowledgement { get; init; }
        internal Func<CancellationToken, ValueTask>? BeforeWrite { get; init; }
        internal Func<ShareFetchRequest, CancellationToken, ValueTask<ShareFetchResponse>>? ShareFetchHandler { get; init; }
        internal Func<ShareAcknowledgeRequest, CancellationToken, ValueTask<ShareAcknowledgeResponse>>? ShareAcknowledgeHandler { get; init; }

        public async ValueTask<TResponse> SendWithResponseCancellationAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, KafkaRequestWriteContext context,
            CancellationToken cancellationToken)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (BeforeWrite is not null)
                await BeforeWrite(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            context.MarkWriteStarted();
            return await SendAsync<TRequest, TResponse>(request, apiVersion, context.ResponseCancellationToken);
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            SendCount++;
            LastApiVersion = apiVersion;
            if (request is ShareFetchRequest fetchRequest && ShareFetchHandler is not null)
            {
                ShareFetchRequest = fetchRequest;
                OnSend?.Invoke();
                return AwaitFetchAsync<TResponse>(ShareFetchHandler(fetchRequest, cancellationToken));
            }
            if (request is ShareAcknowledgeRequest acknowledgeRequest && ShareAcknowledgeHandler is not null)
            {
                ShareAcknowledgeRequest = acknowledgeRequest;
                ShareAcknowledgeRequests.Add(acknowledgeRequest);
                OnSend?.Invoke();
                return AwaitAcknowledgementAsync<TResponse>(ShareAcknowledgeHandler(acknowledgeRequest, cancellationToken));
            }
            IKafkaResponse response = request switch
            {
                ShareFetchRequest fetch => Capture(
                    fetch,
                    ShareFetchResponses is { Count: > 0 } ? ShareFetchResponses.Dequeue() : ShareFetchResponse ?? new ShareFetchResponse
                    {
                        ErrorCode = ErrorCode.None,
                        Responses = [],
                        NodeEndpoints = []
                    }),
                ShareAcknowledgeRequest acknowledge => Capture(
                    acknowledge,
                    ShareAcknowledgeResponses is { Count: > 0 }
                        ? ShareAcknowledgeResponses.Dequeue()
                        : ShareAcknowledgeResponse ?? new ShareAcknowledgeResponse
                    {
                        ErrorCode = ErrorCode.None,
                        Responses = [],
                        NodeEndpoints = []
                    }),
                MetadataRequest => new MetadataResponse
                {
                    Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
                    Topics =
                    [
                        new TopicMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            Name = "topic",
                            TopicId = TopicId,
                            Partitions =
                            [
                                new PartitionMetadata
                                {
                                    ErrorCode = ErrorCode.None,
                                    PartitionIndex = 0,
                                    LeaderId = 1,
                                    ReplicaNodes = [1],
                                    IsrNodes = [1]
                                },
                                new PartitionMetadata
                                {
                                    ErrorCode = ErrorCode.None,
                                    PartitionIndex = 1,
                                    LeaderId = 1,
                                    ReplicaNodes = [1],
                                    IsrNodes = [1]
                                }
                            ]
                        }
                    ]
                },
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            OnSend?.Invoke();
            if (request is ShareFetchRequest && ShareFetchException is not null)
                throw ShareFetchException;
            if (request is ShareAcknowledgeRequest { ShareSessionEpoch: ShareSessionManager.CloseEpoch }
                && DelayedFinalAcknowledgement is { } delayed)
                return new ValueTask<TResponse>(AwaitResponseAsync(delayed.Task));

            return new ValueTask<TResponse>((TResponse)response);

            static async Task<TResponse> AwaitResponseAsync(Task<ShareAcknowledgeResponse> pending)
                => (TResponse)(IKafkaResponse)await pending;
        }

        private static async ValueTask<TResponse> AwaitFetchAsync<TResponse>(ValueTask<ShareFetchResponse> response)
            where TResponse : IKafkaResponse
            => (TResponse)(IKafkaResponse)await response;

        private static async ValueTask<TResponse> AwaitAcknowledgementAsync<TResponse>(ValueTask<ShareAcknowledgeResponse> response)
            where TResponse : IKafkaResponse
            => (TResponse)(IKafkaResponse)await response;

        private ShareFetchResponse Capture(
            ShareFetchRequest request,
            ShareFetchResponse response)
        {
            ShareFetchRequest = request;
            return response;
        }

        private ShareAcknowledgeResponse Capture(
            ShareAcknowledgeRequest request,
            ShareAcknowledgeResponse response)
        {
            ShareAcknowledgeRequest = request;
            ShareAcknowledgeRequests.Add(request);
            return response;
        }

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

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
