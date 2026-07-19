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

public sealed class ShareConsumerRenewalTests
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
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);

        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        var moved = await poll.MoveNextAsync();

        await Assert.That(moved).IsTrue();
        await Assert.That(ReferenceEquals(poll.Current, record)).IsTrue();
        await Assert.That(fixture.Consumer.AcquisitionLockTimeoutMs).IsEqualTo(30_000);
        await Assert.That(fixture.Consumer.RenewedRecordReplayCount).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_FetchedRecord_ReservesBudgetBeforeRenewalReplay()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var batch = new RecordBatch
        {
            BaseOffset = 100,
            Records = [new Record { IsKeyNull = true, Value = "new-value"u8.ToArray() }]
        })
        {
            batch.Write(buffer);
        }

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
                                CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                                RecordBytes = buffer.WrittenMemory,
                                AcquiredRecords =
                                [
                                    new ShareFetchAcquiredRecords
                                    {
                                        FirstOffset = 100,
                                        LastOffset = 100,
                                        DeliveryCount = 1
                                    }
                                ]
                            }
                        ]
                    }
                ],
                NodeEndpoints = []
            }
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
    public async Task Poll_SessionLoss_ClearsRenewedRecords()
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
        fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        var moved = await poll.MoveNextAsync();

        await Assert.That(moved).IsFalse();
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).IsEmpty();
    }

    [Test]
    public async Task BrokerSessionLoss_ClearsOnlyBrokerRenewedRecords()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection);
        fixture.MetadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9092 },
                new BrokerMetadata { NodeId = 2, Host = "broker-2", Port = 9092 }
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
                            LeaderId = 2,
                            ReplicaNodes = [2],
                            IsrNodes = [2]
                        }
                    ]
                }
            ]
        });
        var first = CreateRecord(partition: 0, offset: 40);
        var second = CreateRecord(partition: 1, offset: 41);
        fixture.Consumer.Acknowledge(first, AcknowledgeType.Renew);
        fixture.Consumer.Acknowledge(second, AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgementsForBothPartitions());

        InvokePrivate(fixture.Consumer, "ClearRenewedRecordsForBroker", 1);

        var assignment = new HashSet<TopicPartition>
        {
            new("topic", 0),
            new("topic", 1)
        };
        var active = GetActiveRenewedRecords(fixture.Consumer, assignment);
        await Assert.That(active).HasSingleItem();
        await Assert.That(active[0].Partition).IsEqualTo(1);
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

    private static Fixture CreateFixture(
        CapturingConnection connection,
        ShareAcknowledgementMode acknowledgementMode = ShareAcknowledgementMode.Explicit,
        int maxPollRecords = 500)
    {
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-group",
            AcknowledgementMode = acknowledgementMode,
            MaxPollRecords = maxPollRecords
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>()).Returns(connection);
        var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(new MetadataResponse
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
        });
        var consumer = new KafkaShareConsumer<string, string>(
            options,
            Substitute.For<IDeserializer<string>>(),
            Substitute.For<IDeserializer<string>>(),
            pool,
            metadataManager);
        SetMemberId(consumer, "member-1");
        return new Fixture(consumer, metadataManager);
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
        long offset = 42) => new()
    {
        Topic = "topic",
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
            CancellationToken.None)!;

    private static void ApplySuccessfulAcknowledgements(
        KafkaShareConsumer<string, string> consumer,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements)
        => InvokePrivate(consumer, "ApplySuccessfulAcknowledgements", acknowledgements);

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

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>> FlushPendingAcknowledgements(
        KafkaShareConsumer<string, string> consumer)
    {
        var tracker = (AcknowledgementTracker)typeof(KafkaShareConsumer<string, string>)
            .GetField("_ackTracker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        return tracker.Flush();
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

    private sealed class CapturingConnection(ApiKey apiKey, short maximumVersion) :
        IKafkaConnection,
        IKafkaCapabilityProvider
    {
        public int BrokerId => 1;
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
        internal ShareAcknowledgeResponse? ShareAcknowledgeResponse { get; init; }
        internal Queue<ShareAcknowledgeResponse>? ShareAcknowledgeResponses { get; init; }
        internal List<ShareAcknowledgeRequest> ShareAcknowledgeRequests { get; } = [];
        internal Action? OnSend { get; init; }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            SendCount++;
            LastApiVersion = apiVersion;
            IKafkaResponse response = request switch
            {
                ShareFetchRequest fetch => Capture(
                    fetch,
                    ShareFetchResponse ?? new ShareFetchResponse
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
            return new ValueTask<TResponse>((TResponse)response);
        }

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
