using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class ConsumerRackAwarenessTests
{
    [Test]
    [Arguments(false, 0)]
    [Arguments(true, 0)]
    [Arguments(false, 1)]
    [Arguments(true, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 2)]
    public async Task OffsetOutOfRange_MissingRequestLeaderRetriesBeforeApplyingNone(bool prefetch, int gap)
    {
        var pool = Substitute.For<IConnectionPool>();
        var previousLeader = Substitute.For<IKafkaConnection>();
        var currentLeader = Substitute.For<IKafkaConnection>();
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(previousLeader));
        pool.GetConnectionByIndexAsync(2, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(currentLeader));
        await using var metadata = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadata, "rack-a", AutoOffsetReset.None);
        consumer.Assign(Partition);
        SetInitialFetchPosition(consumer, 42);
        await Assert.That(await InvokeGroupPartitionsByBrokerAsync(consumer)).ContainsKey(1);

        var recoveredMetadata = new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093, Rack = "rack-b" },
                new BrokerMetadata { NodeId = 2, Host = "broker-2", Port = 9094, Rack = "rack-a" }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 2, LeaderEpoch = 6,
                            ReplicaNodes = [1, 2], IsrNodes = [1, 2]
                        }
                    ]
                }
            ]
        };
        // Cover an absent topic, an out-of-range partition, and a null partition slot.
        // Broker grouping precedes the gap; request construction observes the gap.
        metadata.Metadata.Update(new MetadataResponse
        {
            Brokers = recoveredMetadata.Brokers,
            Topics = gap == 0 ? [] :
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions = gap == 1 ? [] :
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None, PartitionIndex = 1, LeaderId = 2, LeaderEpoch = 6,
                            ReplicaNodes = [1, 2], IsrNodes = [1, 2]
                        }
                    ]
                }
            ]
        });
        long retryOffset = -1;
        previousLeader.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                metadata.Metadata.Update(recoveredMetadata);
                return new ValueTask<FetchResponse>(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange));
            });
        currentLeader.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                retryOffset = call.Arg<FetchRequest>().Topics[0].Partitions[0].FetchOffset;
                return new ValueTask<FetchResponse>(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange));
            });

        // An unverified broker cannot authorize either a reset or the None exception.
        await InvokeReplicaFetchAsync(consumer, 1, prefetch);
        await Assert.That(GetFetchPosition(consumer)).IsEqualTo(42);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(42);
        var routing = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(routing).ContainsKey(2);
        await Assert.That(routing).DoesNotContainKey(1);
        await Assert.That(async () => await InvokeReplicaFetchAsync(consumer, 2, prefetch))
            .Throws<KafkaException>();
        await Assert.That(retryOffset).IsEqualTo(42);
        await Assert.That(GetFetchPosition(consumer)).IsEqualTo(42);
    }

    [Test]
    [Arguments(AutoOffsetReset.Earliest, false)]
    [Arguments(AutoOffsetReset.Latest, false)]
    [Arguments(AutoOffsetReset.None, false)]
    [Arguments(AutoOffsetReset.Earliest, true)]
    [Arguments(AutoOffsetReset.Latest, true)]
    [Arguments(AutoOffsetReset.None, true)]
    public async Task OffsetOutOfRange_StaleLeaderRoutingRetriesCurrentLeaderBeforeReset(
        AutoOffsetReset reset, bool prefetch)
    {
        var pool = Substitute.For<IConnectionPool>();
        var previousLeader = Substitute.For<IKafkaConnection>();
        var currentLeader = Substitute.For<IKafkaConnection>();
        long previousLeaderOffset = -1;
        long currentLeaderOffset = -1;
        previousLeader.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                previousLeaderOffset = call.Arg<FetchRequest>().Topics[0].Partitions[0].FetchOffset;
                return new ValueTask<FetchResponse>(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange));
            });
        currentLeader.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                currentLeaderOffset = call.Arg<FetchRequest>().Topics[0].Partitions[0].FetchOffset;
                return new ValueTask<FetchResponse>(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange));
            });
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(previousLeader));
        pool.GetConnectionByIndexAsync(2, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(currentLeader));
        await using var metadata = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadata, "rack-a", reset);
        consumer.Assign(Partition);
        SetInitialFetchPosition(consumer, 42);

        // Cache routing without ever selecting a preferred replica. Metadata changes
        // after grouping selected broker 1, but before that fetch captures its snapshot.
        var originalRouting = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(originalRouting).ContainsKey(1);
        await Assert.That(metadata.TryUpdatePartitionLeader(Topic, 0, 2, 6)).IsTrue();
        await InvokeReplicaFetchAsync(consumer, 1, prefetch);

        await Assert.That(previousLeaderOffset).IsEqualTo(42);
        await Assert.That(GetFetchPosition(consumer)).IsEqualTo(42);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(42);
        var retryRouting = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(retryRouting).ContainsKey(2);
        await Assert.That(retryRouting).DoesNotContainKey(1);
        if (reset == AutoOffsetReset.None)
        {
            await Assert.That(async () => await InvokeReplicaFetchAsync(consumer, 2, prefetch))
                .Throws<KafkaException>();
            await Assert.That(GetFetchPosition(consumer)).IsEqualTo(42);
        }
        else
        {
            await InvokeReplicaFetchAsync(consumer, 2, prefetch);
            var expected = reset == AutoOffsetReset.Earliest ? -2L : -1L;
            await Assert.That(GetFetchPosition(consumer)).IsEqualTo(expected);
            await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(expected);
        }
        await Assert.That(currentLeaderOffset).IsEqualTo(42);
    }

    [Test]
    [Arguments(AutoOffsetReset.Earliest, false)]
    [Arguments(AutoOffsetReset.Latest, false)]
    [Arguments(AutoOffsetReset.None, false)]
    [Arguments(AutoOffsetReset.Earliest, true)]
    [Arguments(AutoOffsetReset.Latest, true)]
    [Arguments(AutoOffsetReset.None, true)]
    public async Task FollowerOffsetOutOfRange_RetriesLeaderBeforeReset(
        AutoOffsetReset reset, bool prefetch)
    {
        var pool = Substitute.For<IConnectionPool>();
        var leader = Substitute.For<IKafkaConnection>();
        var follower = Substitute.For<IKafkaConnection>();
        var leaderReturnsError = false;
        long leaderRequestedOffset = -1;
        long followerRequestedOffset = -1;
        leader.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                leaderRequestedOffset = call.Arg<FetchRequest>().Topics[0].Partitions[0].FetchOffset;
                return new ValueTask<FetchResponse>(CreateFetchResponse(2,
                    leaderReturnsError ? ErrorCode.OffsetOutOfRange : ErrorCode.None));
            });
        follower.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                followerRequestedOffset = call.Arg<FetchRequest>().Topics[0].Partitions[0].FetchOffset;
                return new ValueTask<FetchResponse>(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange));
            });
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>()).Returns(new ValueTask<IKafkaConnection>(leader));
        pool.GetConnectionByIndexAsync(2, 0, Arg.Any<CancellationToken>()).Returns(new ValueTask<IKafkaConnection>(follower));
        await using var metadata = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadata, "rack-a", reset);
        consumer.Assign(Partition);
        SetInitialFetchPosition(consumer, 42);

        await InvokeReplicaFetchAsync(consumer, 1, prefetch);
        await Assert.That(await InvokeGroupPartitionsByBrokerAsync(consumer)).ContainsKey(2);
        await InvokeReplicaFetchAsync(consumer, 2, prefetch);

        await Assert.That(followerRequestedOffset).IsEqualTo(42);
        await Assert.That(GetFetchPosition(consumer)).IsEqualTo(42);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(42);
        await Assert.That(await InvokeGroupPartitionsByBrokerAsync(consumer)).ContainsKey(1);

        leaderReturnsError = true;
        if (reset == AutoOffsetReset.None)
        {
            await Assert.That(async () => await InvokeReplicaFetchAsync(consumer, 1, prefetch))
                .Throws<KafkaException>();
            await Assert.That(GetFetchPosition(consumer)).IsEqualTo(42);
        }
        else
        {
            await InvokeReplicaFetchAsync(consumer, 1, prefetch);
            var expected = reset == AutoOffsetReset.Earliest ? -2L : -1L;
            await Assert.That(GetFetchPosition(consumer)).IsEqualTo(expected);
            await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(expected);
        }
        await Assert.That(leaderRequestedOffset).IsEqualTo(42);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OffsetOutOfRange_UnrelatedMetadataRefreshStillAllowsLeaderReset(bool prefetch)
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var started = NewFetchSignal();
        var response = new TaskCompletionSource<FetchResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.TrySetResult();
                return new ValueTask<FetchResponse>(response.Task);
            });
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));
        await using var metadata = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadata, "rack-a", AutoOffsetReset.Latest);
        consumer.Assign(Partition);
        SetInitialFetchPosition(consumer, 42);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var snapshot = metadata.Metadata.CaptureSnapshot();
        var fetch = InvokeReplicaFetchAsync(consumer, 1, prefetch).AsTask();
        try
        {
            await started.Task.WaitAsync(timeout.Token);
            // Replace the cluster snapshot while preserving this partition's leader and epoch.
            metadata.Metadata.Update(new MetadataResponse
            {
                Brokers =
                [
                    new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093, Rack = "rack-b" },
                    new BrokerMetadata { NodeId = 2, Host = "broker-2", Port = 9094, Rack = "rack-a" }
                ],
                Topics = []
            }, mergeTopics: true);
            await Assert.That(ReferenceEquals(snapshot, metadata.Metadata.CaptureSnapshot())).IsFalse();
        }
        finally
        {
            response.TrySetResult(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange));
            await fetch.WaitAsync(timeout.Token);
        }
        await Assert.That(GetFetchPosition(consumer)).IsEqualTo(-1);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(-1);
    }
    [Test]
    [Arguments(false, 1, 2, AutoOffsetReset.None)]
    [Arguments(false, 1, 2, AutoOffsetReset.Earliest)]
    [Arguments(false, 1, 2, AutoOffsetReset.Latest)]
    [Arguments(true, 1, 2, AutoOffsetReset.None)]
    [Arguments(true, 1, 2, AutoOffsetReset.Earliest)]
    [Arguments(true, 1, 2, AutoOffsetReset.Latest)]
    [Arguments(false, 2, 2, AutoOffsetReset.None)]
    [Arguments(false, 2, 2, AutoOffsetReset.Earliest)]
    [Arguments(false, 2, 2, AutoOffsetReset.Latest)]
    [Arguments(true, 2, 2, AutoOffsetReset.None)]
    [Arguments(true, 2, 2, AutoOffsetReset.Earliest)]
    [Arguments(true, 2, 2, AutoOffsetReset.Latest)]
    [Arguments(false, 1, 1, AutoOffsetReset.None)]
    [Arguments(false, 1, 1, AutoOffsetReset.Earliest)]
    [Arguments(false, 1, 1, AutoOffsetReset.Latest)]
    [Arguments(true, 1, 1, AutoOffsetReset.None)]
    [Arguments(true, 1, 1, AutoOffsetReset.Earliest)]
    [Arguments(true, 1, 1, AutoOffsetReset.Latest)]
    public async Task OffsetOutOfRange_ChangedLeaderOrEpochCannotReset(
        bool prefetch, int requestedBroker, int newLeader, AutoOffsetReset reset)
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var started = NewFetchSignal();
        var response = new TaskCompletionSource<FetchResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.TrySetResult();
                return new ValueTask<FetchResponse>(response.Task);
            });
        pool.GetConnectionByIndexAsync(requestedBroker, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));
        await using var metadata = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadata, "rack-a", reset);
        consumer.Assign(Partition);
        SetInitialFetchPosition(consumer, 42);
        // Populate routing before the response is held across the metadata change.
        await Assert.That(await InvokeGroupPartitionsByBrokerAsync(consumer)).ContainsKey(1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var fetch = InvokeReplicaFetchAsync(consumer, requestedBroker, prefetch).AsTask();
        try
        {
            await started.Task.WaitAsync(timeout.Token);
            await Assert.That(metadata.TryUpdatePartitionLeader(Topic, 0, newLeader, 6)).IsTrue();
        }
        finally
        {
            response.TrySetResult(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange));
            await fetch.WaitAsync(timeout.Token);
        }
        await Assert.That(GetFetchPosition(consumer)).IsEqualTo(42);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(42);
        await Assert.That(await InvokeGroupPartitionsByBrokerAsync(consumer)).ContainsKey(newLeader);
    }

    [Test]
    [Arguments(false, 1, 0, AutoOffsetReset.None)]
    [Arguments(false, 1, 0, AutoOffsetReset.Earliest)]
    [Arguments(false, 1, 0, AutoOffsetReset.Latest)]
    [Arguments(true, 1, 0, AutoOffsetReset.None)]
    [Arguments(true, 1, 0, AutoOffsetReset.Earliest)]
    [Arguments(true, 1, 0, AutoOffsetReset.Latest)]
    [Arguments(false, 2, 0, AutoOffsetReset.None)]
    [Arguments(false, 2, 0, AutoOffsetReset.Earliest)]
    [Arguments(false, 2, 0, AutoOffsetReset.Latest)]
    [Arguments(true, 2, 0, AutoOffsetReset.None)]
    [Arguments(true, 2, 0, AutoOffsetReset.Earliest)]
    [Arguments(true, 2, 0, AutoOffsetReset.Latest)]
    [Arguments(false, 1, 1, AutoOffsetReset.None)]
    [Arguments(false, 1, 1, AutoOffsetReset.Earliest)]
    [Arguments(false, 1, 1, AutoOffsetReset.Latest)]
    [Arguments(true, 1, 1, AutoOffsetReset.None)]
    [Arguments(true, 1, 1, AutoOffsetReset.Earliest)]
    [Arguments(true, 1, 1, AutoOffsetReset.Latest)]
    [Arguments(false, 2, 1, AutoOffsetReset.None)]
    [Arguments(false, 2, 1, AutoOffsetReset.Earliest)]
    [Arguments(false, 2, 1, AutoOffsetReset.Latest)]
    [Arguments(true, 2, 1, AutoOffsetReset.None)]
    [Arguments(true, 2, 1, AutoOffsetReset.Earliest)]
    [Arguments(true, 2, 1, AutoOffsetReset.Latest)]
    [Arguments(false, 1, 2, AutoOffsetReset.None)]
    [Arguments(false, 1, 2, AutoOffsetReset.Earliest)]
    [Arguments(false, 1, 2, AutoOffsetReset.Latest)]
    [Arguments(true, 1, 2, AutoOffsetReset.None)]
    [Arguments(true, 1, 2, AutoOffsetReset.Earliest)]
    [Arguments(true, 1, 2, AutoOffsetReset.Latest)]
    [Arguments(false, 2, 2, AutoOffsetReset.None)]
    [Arguments(false, 2, 2, AutoOffsetReset.Earliest)]
    [Arguments(false, 2, 2, AutoOffsetReset.Latest)]
    [Arguments(true, 2, 2, AutoOffsetReset.None)]
    [Arguments(true, 2, 2, AutoOffsetReset.Earliest)]
    [Arguments(true, 2, 2, AutoOffsetReset.Latest)]
    public async Task OffsetOutOfRange_StaleResponseCannotOverwriteSeekOrAssignment(
        bool prefetch, int requestedBroker, int mutation, AutoOffsetReset reset)
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var started = NewFetchSignal();
        var response = new TaskCompletionSource<FetchResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.TrySetResult();
                return new ValueTask<FetchResponse>(response.Task);
            });
        pool.GetConnectionByIndexAsync(requestedBroker, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));
        await using var metadata = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadata, "rack-a", reset);
        consumer.Assign(Partition);
        SetInitialFetchPosition(consumer, 42);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var fetch = InvokeReplicaFetchAsync(consumer, requestedBroker, prefetch).AsTask();
        try
        {
            await started.Task.WaitAsync(timeout.Token);
            if (mutation == 0)
            {
                consumer.Seek(new TopicPartitionOffset(Topic, 0, 99));
            }
            else
            {
                consumer.Unassign();
                if (mutation == 2)
                {
                    consumer.Assign(Partition);
                    SetInitialFetchPosition(consumer, 99);
                }
            }
        }
        finally
        {
            response.TrySetResult(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange));
            await fetch.WaitAsync(timeout.Token);
        }
        if (mutation == 1)
        {
            await Assert.That(consumer.GetPosition(Partition)).IsNull();
            await Assert.That(FetchPositions(consumer)).DoesNotContainKey(Partition);
        }
        else
        {
            await Assert.That(GetFetchPosition(consumer)).IsEqualTo(99);
            await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(99);
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task OffsetOutOfRange_DurationLookupRechecksLeaderAndSeek(bool prefetch, bool seek)
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var lookupStarted = NewFetchSignal();
        var lookup = new TaskCompletionSource<ListOffsetsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<FetchResponse>(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange)));
        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lookupStarted.TrySetResult();
                return new ValueTask<ListOffsetsResponse>(lookup.Task);
            });
        // Offset-control requests use a separate connection index from fetches.
        pool.GetConnectionByIndexAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<IKafkaConnection>(connection));
        await using var metadata = CreateMetadataManager(pool);
        metadata.SetApiVersion(ApiKey.ListOffsets, ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);
        await using var consumer = CreateConsumer(pool, metadata, "rack-a", AutoOffsetReset.ByDuration);
        consumer.Assign(Partition);
        SetInitialFetchPosition(consumer, 42);
        await Assert.That(await InvokeGroupPartitionsByBrokerAsync(consumer)).ContainsKey(1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var fetch = InvokeReplicaFetchAsync(consumer, 1, prefetch).AsTask();
        try
        {
            await lookupStarted.Task.WaitAsync(timeout.Token);
            if (seek)
                consumer.Seek(new TopicPartitionOffset(Topic, 0, 99));
            else
                await Assert.That(metadata.TryUpdatePartitionLeader(Topic, 0, 2, 6)).IsTrue();
        }
        finally
        {
            lookup.TrySetResult(new ListOffsetsResponse
            {
                Topics =
                [
                    new ListOffsetsResponseTopic
                    {
                        Name = Topic,
                        Partitions = [new ListOffsetsResponsePartition { PartitionIndex = 0, ErrorCode = ErrorCode.None, Offset = 10 }]
                    }
                ]
            });
            await fetch.WaitAsync(timeout.Token);
        }
        await Assert.That(GetFetchPosition(consumer)).IsEqualTo(seek ? 99 : 42);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(seek ? 99 : 42);
        await Assert.That(await InvokeGroupPartitionsByBrokerAsync(consumer)).ContainsKey(seek ? 1 : 2);
    }

    private static TaskCompletionSource NewFetchSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Test]
    [Arguments(false, AutoOffsetReset.Earliest)]
    [Arguments(true, AutoOffsetReset.Earliest)]
    [Arguments(false, AutoOffsetReset.Latest)]
    [Arguments(true, AutoOffsetReset.Latest)]
    [Arguments(false, AutoOffsetReset.ByDuration)]
    [Arguments(true, AutoOffsetReset.ByDuration)]
    [Arguments(false, AutoOffsetReset.None)]
    [Arguments(true, AutoOffsetReset.None)]
    public async Task OffsetOutOfRange_LeaderCannotChangeBetweenValidationAndCommit(bool prefetch, AutoOffsetReset reset)
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<FetchResponse>(CreateFetchResponse(-1, ErrorCode.OffsetOutOfRange)));
        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<ListOffsetsResponse>(new ListOffsetsResponse
            {
                Topics = [new ListOffsetsResponseTopic
                {
                    Name = Topic,
                    Partitions = [new ListOffsetsResponsePartition { PartitionIndex = 0, ErrorCode = ErrorCode.None, Offset = 10 }]
                }]
            }));
        pool.GetConnectionByIndexAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));
        await using var metadata = CreateMetadataManager(pool);
        metadata.SetApiVersion(ApiKey.ListOffsets, ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);
        await using var consumer = CreateConsumer(pool, metadata, "rack-a", reset);
        consumer.Assign(Partition);
        SetInitialFetchPosition(consumer, 42);
        var probes = 0;
        var leaderChangedBeforeCommit = false;
        // The mock transport and duration lookup complete synchronously. Run the
        // cold-path hook on that same thread; a separate thread attempts publication.
        await Task.Run(async () =>
        {
            KafkaConsumer<string, string>.BeforeOffsetResetCommitForTest = () =>
            {
                probes++;
                var publisher = new Thread(() =>
                {
                    if (!Monitor.TryEnter(metadata.Metadata.UpdateLock))
                        return;
                    try
                    {
                        leaderChangedBeforeCommit = metadata.TryUpdatePartitionLeader(Topic, 0, 2, 6);
                    }
                    finally
                    {
                        Monitor.Exit(metadata.Metadata.UpdateLock);
                    }
                }) { IsBackground = true };
                publisher.Start();
                if (!publisher.Join(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Metadata publication probe did not finish.");
            };
            try
            {
                if (reset == AutoOffsetReset.None)
                {
                    await Assert.That(async () => await InvokeReplicaFetchAsync(consumer, 1, prefetch))
                        .Throws<KafkaException>();
                }
                else
                {
                    await InvokeReplicaFetchAsync(consumer, 1, prefetch);
                }
            }
            finally
            {
                KafkaConsumer<string, string>.BeforeOffsetResetCommitForTest = null;
            }
        });
        await Assert.That(probes).IsEqualTo(1);
        await Assert.That(leaderChangedBeforeCommit).IsFalse();
        var expected = reset switch
        {
            AutoOffsetReset.Earliest => -2L,
            AutoOffsetReset.Latest => -1L,
            AutoOffsetReset.None => 42L,
            _ => 10L
        };
        await Assert.That(GetFetchPosition(consumer)).IsEqualTo(expected);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(expected);
        // Publication must be possible again once the reset has committed.
        await Assert.That(metadata.TryUpdatePartitionLeader(Topic, 0, 2, 6)).IsTrue();
    }

    private static ValueTask InvokeReplicaFetchAsync(KafkaConsumer<string, string> consumer, int brokerId, bool prefetch)
    {
        if (!prefetch)
            return InvokeFetchFromBrokerAsync(consumer, brokerId, [Partition]);

        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "PrefetchFromBrokerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ValueTask)method.Invoke(consumer,
            [brokerId, new List<TopicPartition> { Partition }, 0, 1, 0, GetFetchBufferEpoch(consumer), CancellationToken.None])!;
    }

    private static void SetInitialFetchPosition(KafkaConsumer<string, string> consumer, long offset)
    {
        // Install initial state without staging Seek's pending-buffer-clear marker.
        FetchPositions(consumer)[Partition] = offset;
        typeof(KafkaConsumer<string, string>).GetMethod("SetPosition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(consumer, [Partition, offset, false]);
    }

    private static long GetFetchPosition(KafkaConsumer<string, string> consumer) => FetchPositions(consumer)[Partition];

    private static ConcurrentDictionary<TopicPartition, long> FetchPositions(KafkaConsumer<string, string> consumer) =>
        (ConcurrentDictionary<TopicPartition, long>)typeof(KafkaConsumer<string, string>)
            .GetField("_fetchPositions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
}
