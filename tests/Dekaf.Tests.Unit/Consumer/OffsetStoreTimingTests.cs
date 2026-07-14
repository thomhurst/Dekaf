using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.Testing;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Delivery-semantics tests for the default at-least-once offset staging
/// (<see cref="OffsetStoreTiming.AfterProcessing"/>) and the opt-in at-most-once
/// staging (<see cref="OffsetStoreTiming.OnDelivery"/>).
/// </summary>
public sealed class OffsetStoreTimingTests
{
    private const string Topic = "test-topic";
    private const int Partition = 0;

    // --- ConsumeAsync (await foreach) ---

    [Test]
    public async Task ConsumeAsync_ExceptionOnFirstRecord_LeavesRecordUnstagedForRedelivery()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);

        await Assert.That(async () =>
        {
            await foreach (var result in consumer.ConsumeAsync(CancellationToken.None))
            {
                throw new InvalidOperationException("processing failed");
            }
        }).Throws<InvalidOperationException>();

        // The failing record must not be committable, but positions advance past it.
        await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(tp)).IsFalse();
        await Assert.That(GetPositions(consumer)[tp]).IsEqualTo(21L);
    }

    [Test]
    public async Task ConsumeAsync_ExceptionAfterProcessingSomeRecords_StagesOnlyProvenPrefix()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"),
                CreateRecord(2, "c", "three"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);

        await Assert.That(async () =>
        {
            await foreach (var result in consumer.ConsumeAsync(CancellationToken.None))
            {
                if (result.Offset == 22)
                    throw new InvalidOperationException("processing failed");
            }
        }).Throws<InvalidOperationException>();

        // Records 20 and 21 were proven processed (the loop pulled past them); the
        // failing record 22 stays uncommitted and is redelivered after restart.
        await Assert.That(GetDirtyStoredOffsets(consumer)[tp]).IsEqualTo(22L);
        await Assert.That(GetPositions(consumer)[tp]).IsEqualTo(23L);
    }

    [Test]
    public async Task ConsumeAsync_LoopDrainsFetchAndContinues_StagesEverything()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);
        using var cts = new CancellationTokenSource();

        try
        {
            await foreach (var result in consumer.ConsumeAsync(cts.Token))
            {
                if (result.Offset == 21)
                {
                    // Cancel without breaking: the next MoveNextAsync proves this record
                    // was processed, flushes the fetch boundary, then observes cancellation.
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Depending on where cancellation is observed the enumerator may throw.
        }

        await Assert.That(GetDirtyStoredOffsets(consumer)[tp]).IsEqualTo(22L);
    }

    [Test]
    public async Task ConsumeAsync_BreakAfterProcessing_LastRecordStaysUnproven()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);

        await foreach (var result in consumer.ConsumeAsync(CancellationToken.None))
        {
            if (result.Offset == 21)
                break; // Dispose without pulling again: record 21 remains in doubt.
        }

        // Record 20 was proven (the loop pulled record 21); record 21 was processed but
        // the enumerator cannot distinguish break from failure, so it stays unstaged.
        // Call CommitAsync() before breaking for an exact handoff.
        await Assert.That(GetDirtyStoredOffsets(consumer)[tp]).IsEqualTo(21L);
    }

    [Test]
    public async Task CommitAsync_AfterBreak_VouchesForInDoubtRecord()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);

        await foreach (var result in consumer.ConsumeAsync(CancellationToken.None))
        {
            if (result.Offset == 21)
                break;
        }

        await consumer.CommitAsync(CancellationToken.None);

        // Explicit commit is the caller vouching for everything delivered so far.
        await Assert.That(GetDirtyStoredOffsets(consumer)[tp]).IsEqualTo(22L);
    }

    // --- ConsumeBatchAsync ---

    [Test]
    public async Task ConsumeBatchAsync_ExceptionWhileProcessingBatch_LeavesBatchUnstaged()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);

        await Assert.That(async () =>
        {
            await foreach (var batch in consumer.ConsumeBatchAsync(CancellationToken.None))
            {
                foreach (var _ in batch) { }
                throw new InvalidOperationException("processing failed");
            }
        }).Throws<InvalidOperationException>();

        // The whole batch is in doubt — nothing pulled past it, nothing staged.
        await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(tp)).IsFalse();
    }

    [Test]
    public async Task ConsumeBatchAsync_NextPullProvesBatch_StagesIteratedRecords()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);
        using var cts = new CancellationTokenSource();

        try
        {
            await foreach (var batch in consumer.ConsumeBatchAsync(cts.Token))
            {
                foreach (var _ in batch) { }
                // Cancel without breaking: the next MoveNextAsync proves this batch
                // was processed before the loop observes cancellation.
                cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
        }

        await Assert.That(GetDirtyStoredOffsets(consumer)[tp]).IsEqualTo(22L);
    }

    // --- ConsumeOneAsync ---

    [Test]
    public async Task ConsumeOneAsync_DefersStagingUntilNextCall()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        // First record delivered but not yet proven processed — nothing staged.
        await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(tp)).IsFalse();

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        // Second call proved the first record; staging happens at the fetch-boundary
        // flush, which runs on the next call after the fetch is drained.
        await Assert.That(TryConsumeOneFromPendingFetches(consumer, out _)).IsFalse();
        await Assert.That(GetDirtyStoredOffsets(consumer)[tp]).IsEqualTo(22L);
    }

    [Test]
    public async Task ConsumeOneAsync_OnDeliveryTiming_StagesImmediately()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(
            OffsetCommitMode.Auto, OffsetStoreTiming.OnDelivery, fetch);
        var tp = new TopicPartition(Topic, Partition);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();

        // At-most-once: committable the moment the record is handed out.
        await Assert.That(GetDirtyStoredOffsets(consumer)[tp]).IsEqualTo(21L);
    }

    [Test]
    public async Task CommitAsync_InAutoMode_StagesInFlightRecord()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(tp)).IsFalse();

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(GetDirtyStoredOffsets(consumer)[tp]).IsEqualTo(21L);
    }

    // --- Close path ---

    [Test]
    public async Task CloseAsync_AutoCommit_CommitsProvenRecordsButNotInDoubtRecord()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateGroupedCommitCapturingConsumer(requests);
        var tp = new TopicPartition(Topic, Partition);
        InjectPendingFetch(consumer, PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(10,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]));

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();

        // Record 10 is proven (second call pulled past it); record 11 is in doubt.
        await consumer.CloseAsync();

        await Assert.That(requests.Count).IsEqualTo(1);
        var committed = requests.Single().Topics.Single().Partitions.Single();
        await Assert.That(committed.CommittedOffset).IsEqualTo(11L);
    }

    // --- InMemoryConsumer parity ---

    [Test]
    public async Task InMemoryConsumer_Default_DoesNotCommitInDoubtRecordOnClose()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("jobs", 0);

        await producer.ProduceAsync("jobs", "a", "one");
        await producer.ProduceAsync("jobs", "b", "two");
        consumer.Subscribe("jobs");

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1))).IsNotNull();
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1))).IsNotNull();
        await consumer.CloseAsync();

        var offsets = await admin.ListConsumerGroupOffsetsAsync("workers");

        // First record proven by the second consume call; second record in doubt.
        await Assert.That(offsets[partition]).IsEqualTo(1);
    }

    [Test]
    public async Task InMemoryConsumer_CommitAsync_VouchesForInDoubtRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("jobs", 0);

        await producer.ProduceAsync("jobs", "a", "one");
        consumer.Subscribe("jobs");

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1))).IsNotNull();
        await consumer.CommitAsync();

        var offsets = await admin.ListConsumerGroupOffsetsAsync("workers");
        await Assert.That(offsets[partition]).IsEqualTo(1);
    }

    [Test]
    public async Task InMemoryConsumer_OnDelivery_CommitsImmediately()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetStoreTiming = OffsetStoreTiming.OnDelivery
            });
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("jobs", 0);

        await producer.ProduceAsync("jobs", "a", "one");
        consumer.Subscribe("jobs");

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1))).IsNotNull();

        var offsets = await admin.ListConsumerGroupOffsetsAsync("workers");
        await Assert.That(offsets[partition]).IsEqualTo(1);
    }

    [Test]
    public async Task InMemoryConsumer_NextConsumeCallCommitsProvenRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "workers",
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("jobs", 0);

        await producer.ProduceAsync("jobs", "a", "one");
        await producer.ProduceAsync("jobs", "b", "two");
        consumer.Subscribe("jobs");

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1))).IsNotNull();
        var offsetsAfterFirst = await admin.ListConsumerGroupOffsetsAsync("workers");

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1))).IsNotNull();
        var offsetsAfterSecond = await admin.ListConsumerGroupOffsetsAsync("workers");

        await Assert.That(offsetsAfterFirst.ContainsKey(partition)).IsFalse();
        await Assert.That(offsetsAfterSecond[partition]).IsEqualTo(1);
    }

    // --- Harness ---

    private static KafkaConsumer<string, string> CreateInitializedConsumer(
        OffsetCommitMode offsetCommitMode,
        params PendingFetchData[] fetches)
        => CreateInitializedConsumer(offsetCommitMode, OffsetStoreTiming.AfterProcessing, fetches);

    private static KafkaConsumer<string, string> CreateInitializedConsumer(
        OffsetCommitMode offsetCommitMode,
        OffsetStoreTiming offsetStoreTiming,
        params PendingFetchData[] fetches)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = offsetCommitMode,
                OffsetStoreTiming = offsetStoreTiming,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200
            },
            Serializers.String,
            Serializers.String,
            loggerFactory: null);

        SetInitialized(consumer);

        foreach (var fetch in fetches)
            InjectPendingFetch(consumer, fetch);

        return consumer;
    }

    private static KafkaConsumer<string, string> CreateGroupedCommitCapturingConsumer(
        List<OffsetCommitRequest> requests)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();

        connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = "group-a",
                        NodeId = 0,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));

        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<OffsetCommitRequest>()!;
                requests.Add(request);
                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics = request.Topics.Select(topic => new OffsetCommitResponseTopic
                    {
                        Name = topic.Name,
                        Partitions = topic.Partitions.Select(partition => new OffsetCommitResponsePartition
                        {
                            PartitionIndex = partition.PartitionIndex,
                            ErrorCode = ErrorCode.None
                        }).ToList()
                    }).ToList()
                });
            });

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
            Topics = []
        });
        metadataManager.SetApiVersion(
            ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion,
            FindCoordinatorRequest.HighestSupportedVersion);
        metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);

        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = OffsetCommitMode.Auto,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);

        SetInitialized(consumer);
        MarkManualAssignmentCurrent(consumer);
        return consumer;
    }

    private static void InjectPendingFetch(KafkaConsumer<string, string> consumer, PendingFetchData fetch)
    {
        var tp = new TopicPartition(fetch.Topic, fetch.PartitionIndex);
        consumer.Assign(tp);
        GetFetchPositions(consumer)[tp] = 0;
        GetPendingFetches(consumer).Enqueue(fetch);
    }

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        GetField("_initialized").SetValue(consumer, true);
    }

    private static void MarkManualAssignmentCurrent(KafkaConsumer<string, string> consumer)
    {
        var assignmentVersion = GetField("_assignmentEnsureVersion");
        GetField("_lastManualAssignmentEnsureVersion").SetValue(consumer, assignmentVersion.GetValue(consumer));
    }

    private static ConcurrentDictionary<TopicPartition, long> GetDirtyStoredOffsets(
        KafkaConsumer<string, string> consumer)
        => (ConcurrentDictionary<TopicPartition, long>)GetField("_dirtyStoredOffsets").GetValue(consumer)!;

    private static ConcurrentDictionary<TopicPartition, long> GetPositions(
        KafkaConsumer<string, string> consumer)
        => (ConcurrentDictionary<TopicPartition, long>)GetField("_positions").GetValue(consumer)!;

    private static ConcurrentDictionary<TopicPartition, long> GetFetchPositions(
        KafkaConsumer<string, string> consumer)
        => (ConcurrentDictionary<TopicPartition, long>)GetField("_fetchPositions").GetValue(consumer)!;

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer)
        => (Queue<PendingFetchData>)GetField("_pendingFetches").GetValue(consumer)!;

    private static bool TryConsumeOneFromPendingFetches(
        KafkaConsumer<string, string> consumer,
        out ConsumeResult<string, string> result)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("TryConsumeOneFromPendingFetches", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("TryConsumeOneFromPendingFetches method not found.");

        object?[] args = [null];
        var consumed = (bool)method.Invoke(consumer, args)!;
        result = args[0] is ConsumeResult<string, string> consumeResult
            ? consumeResult
            : default;
        return consumed;
    }

    private static FieldInfo GetField(string fieldName)
        => typeof(KafkaConsumer<string, string>).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{fieldName} field not found.");

    private static RecordBatch CreateBatch(long baseOffset, params Record[] records)
    {
        return new RecordBatch
        {
            BaseOffset = baseOffset,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = records
        };
    }

    private static Record CreateRecord(int offsetDelta, string key, string value)
    {
        return new Record
        {
            OffsetDelta = offsetDelta,
            TimestampDelta = 0,
            Key = Encoding.UTF8.GetBytes(key),
            IsKeyNull = false,
            Value = Encoding.UTF8.GetBytes(value),
            IsValueNull = false,
            Headers = null,
            HeaderCount = 0
        };
    }
}
